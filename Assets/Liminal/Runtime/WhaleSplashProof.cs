using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class WhaleSplashProof : MonoBehaviour
    {
        const float TimeoutSeconds = 90f;
        const float StepSeconds = 1f / 60f;
        const int CaptureWidth = 1600;
        const int CaptureHeight = 900;
        static readonly float[] SplashCaptureAges = { .3f, 1.2f, 2.2f, 4.4f, 6.1f };

        Experience game;
        string output;
        float started, simulationSong;
        bool finished;
        readonly Report report = new();
        readonly string[] captureNames =
        {
            "splash-age-0.3.png", "splash-age-1.2.png", "splash-age-2.2.png",
            "splash-age-4.4.png", "splash-age-6.1.png"
        };

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length
                ? args[at + 1]
                : Path.GetFullPath("Verification/WhaleSplash");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                report.errors.Length < 16)
            {
                var errors = new string[report.errors.Length + 1];
                Array.Copy(report.errors, errors, report.errors.Length);
                errors[errors.Length - 1] = message + "\n" + stack;
                report.errors = errors;
            }
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > TimeoutSeconds)
            {
                AddError("Whale splash proof exceeded its ninety-second deadline.");
                Finish();
            }
        }

        IEnumerator Start()
        {
            while (!game.Ready && Time.realtimeSinceStartup - started < TimeoutSeconds)
                yield return null;
            if (!game.Ready)
            {
                AddError("Experience did not become ready before the proof deadline.");
                Finish();
                yield break;
            }

            Check(game.CavernMode && game.ProofActive, "Whale splash proof requires cavern proof mode.");
            if (report.errors.Length > 0) { Finish(); yield break; }

            game.Restart();
            game.Tutorial.SetEnabled(false);
            var marine = game.Marine;
            var arrival = marine.Arrival;
            var splash = game.Horizon.ImpactSplash;
            Check(game.Horizon.Ready && splash != null && splash.Ready,
                "Horizon water and its whale impact splash must be ready after Restart.");
            if (report.errors.Length > 0) { Finish(); yield break; }

            report.initialImpactCount = splash.ImpactCount;
            report.initialSplashAge = splash.Age;
            Check(report.initialImpactCount == 0 && report.initialSplashAge == -1f,
                "Restart must clear the whale splash impact count and reset its age to -1.");
            Vector3 player = arrival.Origin + new Vector3(0f, 10f, -140f);
            game.Flight.SetPose(player, Quaternion.LookRotation(arrival.Origin - player, Vector3.up));

            float song = (float)game.Music.Time;
            simulationSong = song;
            bool latefallCaptured = false;
            bool[] captured = new bool[SplashCaptureAges.Length];
            int initialImpactCount = splash.ImpactCount;
            for (int step = 0; step < 1800; step++)
            {
                song += StepSeconds;
                simulationSong = song;
                marine.Tick(song, StepSeconds, player);
                game.Horizon.Tick(song, StepSeconds, marine.WhalePosition, marine.WhaleRotation,
                    marine.WhaleVelocity, marine.WhaleReleased);

                if (!arrival.Triggered) continue;

                if (!latefallCaptured && arrival.Age >= arrival.ImpactAge - .08f)
                {
                    report.latefallAge = arrival.Age;
                    report.latefallPosition = marine.WhalePosition;
                    Frame(report.latefallPosition + new Vector3(340f, 80f, 300f),
                        arrival.Origin + Vector3.up * 90f);
                    report.latefallCapture = Capture("whale-late-fall.png");
                    latefallCaptured = true;
                }

                if (splash.ImpactCount > initialImpactCount)
                {
                    if (report.impactCountAtFirstActive == 0)
                    {
                        report.impactCountAtFirstActive = splash.ImpactCount;
                        report.splashOrigin = splash.Origin;
                        report.lastVelocity = splash.LastVelocity;
                        report.impactAge = arrival.ImpactAge;
                    }

                    for (int i = 0; i < SplashCaptureAges.Length; i++)
                    {
                        if (captured[i] || splash.Age < SplashCaptureAges[i]) continue;
                        Vector3 focus = splash.Origin + Vector3.up * 90f;
                        Frame(splash.Origin + new Vector3(340f, 80f, 300f), focus);
                        report.captures[i] = Capture(captureNames[i]);
                        captured[i] = true;
                        if (i == 1) SamplePlume(splash, 1.2f, out report.heightAt1_2, out report.extentAt1_2);
                        if (i == 2) SamplePlume(splash, 2.2f, out report.heightAt2_2, out report.extentAt2_2);
                        if (i == 4)
                        {
                            SamplePlume(splash, 6.1f, out report.expiredMaxHeight, out report.expiredLateralExtent);
                            report.expiredSplashActive = splash.Active;
                        }
                    }
                }

                if (splash.ImpactCount > 1)
                {
                    AddError("One whale arrival created more than one impact splash.");
                    break;
                }

                bool allCaptured = true;
                foreach (bool value in captured) allCaptured &= value;
                if (arrival.Complete && allCaptured && splash.ImpactCount == initialImpactCount + 1)
                {
                    report.arrivalCompleted = true;
                    break;
                }
            }

            report.finalImpactCount = splash.ImpactCount;
            report.arrivalTriggered = arrival.Triggered;
            report.arrivalCompleted &= arrival.Complete;
            report.splashParticleCount = splash.ParticleCount;
            report.splashPeakHeight = splash.PeakHeight;
            Check(latefallCaptured, "The actual descending whale surface contact was not captured.");
            Check(report.arrivalTriggered && report.arrivalCompleted,
                "The real proximity-triggered whale arrival must complete within the bounded simulation.");
            Check(report.initialImpactCount == 0 && report.impactCountAtFirstActive == 1 &&
                report.finalImpactCount == 1, "The initial arrival must produce exactly one splash, with no duplicate impact.");
            Check(Vector3.Distance(report.splashOrigin, report.latefallPosition) <= 80f &&
                Mathf.Abs(report.splashOrigin.y - game.Horizon.SurfaceHeight) <= 80f,
                "Splash origin must remain near the whale's actual surface contact.");
            Check(report.lastVelocity.y < 0f, "The splash must retain the whale's descending impact velocity.");
            Check(report.heightAt1_2 >= 180f || report.heightAt2_2 >= 180f,
                "CPU-mirrored plume samples must reach at least 180 metres above impact origin at 1.2 or 2.2 seconds.");
            Check(report.extentAt1_2 >= 200f || report.extentAt2_2 >= 200f,
                "CPU-mirrored plume samples must extend at least 200 metres laterally at 1.2 or 2.2 seconds.");
            Check(report.latefallCapture != null && report.latefallCapture.nonBlank &&
                report.latefallCapture.whiteFraction < .15f,
                "The late-fall scene capture must be nonblank and not washed out.");
            for (int i = 0; i < report.captures.Length; i++)
                Check(captured[i] && report.captures[i] != null && report.captures[i].nonBlank &&
                    report.captures[i].whiteFraction < .15f,
                    "Splash capture at age " + SplashCaptureAges[i] + " must be nonblank and not washed out.");
            CaptureReport afterLifetime = report.captures[4];
            Check(afterLifetime != null && !report.expiredSplashActive && report.expiredMaxHeight == 0f,
                "At 6.1 seconds the splash must be inactive and its sampled plume height must be zero.");
            for (int i = 0; i <= 2; i++)
            {
                report.captures[i].afterLifetimeDifferenceFraction =
                    ImageDifferenceFraction(report.captures[i], afterLifetime);
                Check(report.captures[i].blueCyanFraction >= .005f &&
                    report.captures[i].afterLifetimeDifferenceFraction >= .005f,
                    "Active plume capture at age " + SplashCaptureAges[i] +
                    " must contain blue-cyan pixels and visibly differ from the after-lifetime frame.");
            }
            Finish();
        }

        void SamplePlume(WhaleImpactSplash splash, float age, out float height, out float extent)
        {
            height = 0f;
            extent = 0f;
            int count = splash.ParticleCount;
            for (int id = 0; id < count; id++)
            {
                Vector3 position = splash.EvaluateParticle(id, age);
                height = Mathf.Max(height, position.y - splash.Origin.y);
                float lateral = Vector2.Distance(new Vector2(position.x, position.z),
                    new Vector2(splash.Origin.x, splash.Origin.z));
                extent = Mathf.Max(extent, lateral);
            }
        }

        CaptureReport Capture(string name)
        {
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                game.World.Tick(simulationSong, 1f, 0f, game.ReducedMotion);
                RenderPipeline.SubmitRenderRequest(game.Flight.View,
                    new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());

                var result = new CaptureReport { file = name };
                Color32[] pixels = image.GetPixels32();
                result.pixels = pixels;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) result.whitePixels++;
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) result.nonBlackPixels++;
                    if (pixel.b > 55 && pixel.b > pixel.r * 1.2f && pixel.g > pixel.r * 1.1f)
                        result.blueCyanPixels++;
                }
                result.whiteFraction = result.whitePixels / (float)pixels.Length;
                result.nonBlackFraction = result.nonBlackPixels / (float)pixels.Length;
                result.blueCyanFraction = result.blueCyanPixels / (float)pixels.Length;
                result.nonBlank = result.nonBlackPixels > pixels.Length / 10000;
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

        static float ImageDifferenceFraction(CaptureReport image, CaptureReport baseline)
        {
            if (image == null || baseline == null || image.pixels == null || baseline.pixels == null ||
                image.pixels.Length != baseline.pixels.Length) return 0f;
            int changed = 0;
            for (int i = 0; i < image.pixels.Length; i++)
            {
                Color32 a = image.pixels[i], b = baseline.pixels[i];
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) >= 30)
                    changed++;
            }
            return changed / (float)image.pixels.Length;
        }

        void Frame(Vector3 position, Vector3 focus) =>
            game.Flight.SetPose(position, Quaternion.LookRotation(focus - position, Vector3.up));

        void Check(bool value, string message)
        {
            if (!value) AddError(message);
        }

        void AddError(string message)
        {
            var errors = new string[report.errors.Length + 1];
            Array.Copy(report.errors, errors, report.errors.Length);
            errors[errors.Length - 1] = message;
            report.errors = errors;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            report.elapsedSeconds = Time.realtimeSinceStartup - started;
            report.passed = report.errors.Length == 0;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        [Serializable] sealed class CaptureReport
        {
            public string file;
            public int whitePixels, nonBlackPixels, blueCyanPixels;
            public float whiteFraction, nonBlackFraction, blueCyanFraction, afterLifetimeDifferenceFraction;
            public bool nonBlank;
            [NonSerialized] public Color32[] pixels;
        }

        [Serializable] sealed class Report
        {
            public bool passed, arrivalTriggered, arrivalCompleted;
            public int initialImpactCount, impactCountAtFirstActive, finalImpactCount, splashParticleCount;
            public float elapsedSeconds, impactAge, latefallAge, splashPeakHeight, initialSplashAge;
            public float heightAt1_2, heightAt2_2, extentAt1_2, extentAt2_2;
            public float expiredMaxHeight, expiredLateralExtent;
            public Vector3 splashOrigin, lastVelocity, latefallPosition;
            public bool expiredSplashActive;
            public CaptureReport latefallCapture;
            public CaptureReport[] captures = new CaptureReport[SplashCaptureAges.Length];
            public string[] errors = Array.Empty<string>();
        }
    }
}
