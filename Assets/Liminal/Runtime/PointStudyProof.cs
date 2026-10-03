using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class PointStudyProof : MonoBehaviour
    {
        const float DeadlineSeconds = 45f;
        const int Width = 1600;
        const int Height = 900;

        Experience game;
        PointStudy study;
        string output;
        float started, song;
        bool finished;
        readonly Report report = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length
                ? args[at + 1]
                : Path.GetFullPath("Verification/PointStudy");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            float readyDeadline = Time.realtimeSinceStartup + 8f;
            while (!game.Ready && Time.realtimeSinceStartup < readyDeadline) yield return null;
            Check(game.Ready, "Experience did not become ready within 8 seconds.");
            if (report.errors.Length > 0) { Finish(); yield break; }

            game.ManualProofTick = true;
            study = game.PointStudy;
            Check(study != null, "Experience.PointStudy was not initialized.");
            if (report.errors.Length > 0) { Finish(); yield break; }

            song = (float)game.Music.Time;
            report.gpu = SystemInfo.graphicsDeviceName;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.width = Width;
            report.height = Height;
            report.actualVrMeasured = false;

            Camera camera = game.Flight.View;
            int oldMask = camera.cullingMask;
            CameraClearFlags oldClearFlags = camera.clearFlags;
            Color oldBackground = camera.backgroundColor;
            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            bool oldPostProcessing = cameraData != null && cameraData.renderPostProcessing;
            try
            {
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                study.SetVisible(true);
                study.SetMode(PointStudy.RenderMode.NativePoint);
                study.SetDensity(1);
                study.SetGain(4f);
                study.SetFlow(1f);
                study.Tick(song, .016f);
                report.jellyPointCount1x = study.JellyPointCount;
                report.serpentPointCount1x = study.SerpentPointCount;
                Check(report.jellyPointCount1x == 1706 && report.serpentPointCount1x == 262144,
                    "Native 1x point counts did not match the published sample budgets.");

                study.SetDensity(3);
                report.jellyPointCount3x = study.JellyPointCount;
                report.serpentPointCount3x = study.SerpentPointCount;
                Check(report.jellyPointCount3x == 1706 * 3 && report.serpentPointCount3x == 262144 * 3,
                    "Native 3x point counts did not match the published sample budgets.");
                report.nativeTopology = study.NativeTopology.ToString();
                Check(study.NativeTopology == MeshTopology.Points,
                    "Native Point mode did not expose MeshTopology.Points.");

                Frame(study.JellyPosition, 12f, false);
                study.SetVisible(false);
                var empty = Capture("isolation-empty.png", song, .016f);
                report.isolationNonBlackPixels = empty.nonBlackPixels;
                Check(empty.nonBlackPixels == 0, "Existing renderers leaked into the isolated layer-30 camera.");
                study.SetVisible(true);

                CaptureRange("jelly", study.JellyPosition, new[] { 12f, 35f, 80f }, false);
                CaptureInside("jelly", study.JellyPosition);
                CaptureRange("serpent", study.SerpentPosition, new[] { 20f, 65f, 160f }, true);
                CaptureInside("serpent", study.SerpentPosition);

                Frame(study.JellyPosition, 12f, false);
                if (cameraData != null) cameraData.renderPostProcessing = false;
                report.captures.Add(Capture("nativepoint-no-postprocess-jelly-near.png", song, .016f));
                Frame(study.SerpentPosition, 65f, true);
                report.captures.Add(Capture("nativepoint-no-postprocess-serpent-mid.png", song, .016f));
                if (cameraData != null) cameraData.renderPostProcessing = oldPostProcessing;

                study.SetMode(PointStudy.RenderMode.SharpQuad);
                Frame(study.JellyPosition, 12f, false);
                report.captures.Add(Capture("sharpquad-jelly-near.png", song, .016f));
                study.SetMode(PointStudy.RenderMode.NativePoint);
                study.SetDensity(1);
                Frame(study.JellyPosition, 12f, false);
                report.captures.Add(Capture("nativepoint-1x-jelly-near.png", song, .016f));

                study.SetDensity(3);
                study.SetFlow(1f);
                Frame(study.JellyPosition, 12f, false);
                var flowA = Capture("jelly-flow-start.png", song, .016f);
                song += .7f;
                var flowB = Capture("jelly-flow-later.png", song, .7f);
                report.captures.Add(flowA);
                report.captures.Add(flowB);
                report.flowImageDifference = DifferenceFraction(flowA.pixels, flowB.pixels);
                Check(report.flowImageDifference > .0001f,
                    "A 0.7-second surface-flow step did not measurably change the isolated point image.");

                report.maximumWhiteFraction = 0f;
                foreach (CaptureReport capture in report.captures)
                {
                    if (capture.whiteFraction > report.maximumWhiteFraction)
                        report.maximumWhiteFraction = capture.whiteFraction;
                    if (capture.file.StartsWith("jelly-") || capture.file.StartsWith("serpent-"))
                        Check(capture.nonBlank, capture.file + " did not contain enough visible layer-30 pixels.");
                }
                Check(report.maximumWhiteFraction < .12f,
                    "A capture exceeded the diagnostic white-pixel fraction limit of 0.12.");
            }
            catch (Exception exception)
            {
                AddError(exception.ToString());
            }
            finally
            {
                if (cameraData != null) cameraData.renderPostProcessing = oldPostProcessing;
                camera.cullingMask = oldMask;
                camera.clearFlags = oldClearFlags;
                camera.backgroundColor = oldBackground;
                study.SetVisible(true);
                study.SetMode(PointStudy.RenderMode.NativePoint);
                study.SetDensity(3);
                study.SetGain(4f);
                study.SetFlow(1f);
            }

            Finish();
        }

        void CaptureRange(string subject, Vector3 focus, float[] distances, bool serpent)
        {
            foreach (float distance in distances)
            {
                Frame(focus, distance, serpent);
                string band = distance == distances[0] ? "near" : distance == distances[1] ? "mid" : "far";
                report.captures.Add(Capture(subject + "-" + band + ".png", song, .016f));
            }
        }

        void CaptureInside(string subject, Vector3 focus)
        {
            Frame(focus, .35f, subject == "serpent");
            report.captures.Add(Capture(subject + "-inside.png", song, .016f));
        }

        void Frame(Vector3 focus, float distance, bool serpent)
        {
            Vector3 offset = serpent
                ? new Vector3(distance * .22f, distance * .045f, -distance)
                : new Vector3(distance * .08f, distance * .025f, -distance);
            Vector3 position = focus + offset;
            game.Flight.SetPose(position, Quaternion.LookRotation(focus - position, Vector3.up));
        }

        CaptureReport Capture(string name, float atSong, float dt)
        {
            if (Time.realtimeSinceStartup - started > DeadlineSeconds)
                throw new TimeoutException("Point study proof exceeded its 45-second total deadline.");
            study.Tick(atSong, dt);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(game.Flight.View,
                    new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
                if (Time.realtimeSinceStartup - started > DeadlineSeconds)
                    throw new TimeoutException("Point study proof exceeded its 45-second total deadline.");

                var result = new CaptureReport { file = name, pixels = image.GetPixels32() };
                result.mode = study.Mode.ToString();
                result.densityMultiplier = study.DensityMultiplier;
                result.song = atSong;
                foreach (Color32 pixel in result.pixels)
                {
                    if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) result.whitePixels++;
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) result.nonBlackPixels++;
                }
                result.whiteFraction = result.whitePixels / (float)result.pixels.Length;
                result.nonBlackFraction = result.nonBlackPixels / (float)result.pixels.Length;
                result.nonBlank = result.nonBlackPixels > 20;
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Destroy(image);
                target.Release();
                Destroy(target);
            }
        }

        static float DifferenceFraction(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0f;
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) +
                    Mathf.Abs(a[i].b - b[i].b) >= 30) changed++;
            return changed / (float)a.Length;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                AddError(message + "\n" + stack);
        }

        void Check(bool condition, string message)
        {
            report.tests.Add(condition ? "PASS: check " + (report.tests.Count + 1) : "FAIL: " + message);
            if (!condition) AddError(message);
        }

        void AddError(string message)
        {
            var next = new string[report.errors.Length + 1];
            Array.Copy(report.errors, next, report.errors.Length);
            next[next.Length - 1] = message;
            report.errors = next;
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > DeadlineSeconds)
            {
                AddError("Point study proof exceeded its 45-second total deadline.");
                Finish();
            }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            game.ManualProofTick = false;
            report.elapsedSeconds = Time.realtimeSinceStartup - started;
            report.passed = report.errors.Length == 0;
            report.captureList = report.captures.ToArray();
            report.testList = report.tests.ToArray();
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        [Serializable]
        sealed class CaptureReport
        {
            public string file, mode;
            public int whitePixels, nonBlackPixels;
            public int densityMultiplier;
            public float song, whiteFraction, nonBlackFraction;
            public bool nonBlank;
            [NonSerialized] public Color32[] pixels;
        }

        [Serializable]
        sealed class Report
        {
            public bool passed, actualVrMeasured;
            public int width, height, jellyPointCount1x, serpentPointCount1x, jellyPointCount3x, serpentPointCount3x, isolationNonBlackPixels;
            public float elapsedSeconds, maximumWhiteFraction, flowImageDifference;
            public string gpu, graphicsApi, nativeTopology;
            public string[] errors = Array.Empty<string>();
            public CaptureReport[] captureList;
            public string[] testList;
            [NonSerialized] public readonly System.Collections.Generic.List<CaptureReport> captures = new();
            [NonSerialized] public readonly System.Collections.Generic.List<string> tests = new();
        }
    }
}
