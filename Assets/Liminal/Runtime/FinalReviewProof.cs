using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class FinalReviewProof : MonoBehaviour
    {
        const float TimeoutSeconds = 90f;
        const float StepSeconds = .05f;
        Experience game;
        string output;
        float started;
        bool finished;
        readonly List<string> errors = new();
        readonly Report report = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Verification/FinalReview");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 16)
                errors.Add(message + "\n" + stack);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > TimeoutSeconds)
            {
                errors.Add("Final review proof exceeded its ninety-second deadline.");
                Finish();
            }
        }

        void Check(bool value, string message)
        {
            if (value) return;
            errors.Add(message);
            Finish();
            throw new InvalidOperationException(message);
        }

        IEnumerator Start()
        {
            while (!game.Ready) yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.CavernMode && game.ProofActive, "Final review requires the cavern scene in proof mode.");
            Check(game.Vr != null && !game.Vr.Enabled, "Headless HUD review must not start an XR session.");
            var settings=ParticleTransitionSettings.Current;
            Check(Resources.Load<ParticleTransitionSettings>("ParticleTransitions")==settings &&
                settings.Envelope(.1f).y<settings.Envelope(settings.Timings.x+settings.Timings.y).y &&
                settings.Duration(true)>settings.Duration(),"Shared transitions must build from weak turbulence and keep large deaths longer.");
            yield return VerifyVrHud();
            yield return VerifyRing();
            yield return ExportReleaseTone();
            yield return VerifyAtlantis();
            Finish();
        }

        IEnumerator VerifyVrHud()
        {
            report.pauseTruth = new bool[8];
            for (int mask = 0; mask < report.pauseTruth.Length; mask++)
            {
                report.pauseTruth[mask] = PcVrSession.ComposePauseInput((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0);
                Check(report.pauseTruth[mask] == (mask != 0), "Menu / Y / left stick pause composition failed for mask " + mask + ".");
            }

            game.Restart();
            game.Tutorial.SetEnabled(false);
            var hud = game.GetComponent<VrWorldHud>();
            if (hud == null)
            {
                hud = game.gameObject.AddComponent<VrWorldHud>();
                hud.Initialize(game, game.Vr);
            }
            game.Flight.EnableVr(true);
            game.Flight.SetVrHeadPose(Vector3.zero, Quaternion.identity);
            hud.SetVrActive(true);
            report.pauseButtonLabel = hud.PauseButtonLabel;
            string[] names = { "SERPENT", "HORIZON", "HERMIT", "SUBMARINE" };
            report.bossStatusTexts = new string[4];
            for (int room = 1; room <= 4; room++)
            {
                game.Flight.SetPose(CaveLayout.Rooms[room].Center, Quaternion.identity);
                yield return null;
                hud.Tick((float)game.Music.Time, StepSeconds);
                Check(game.CurrentRoom == room, "Flight pose must select HUD room " + room + ".");
                int maximum = room == 1 ? game.Combat.BossDamageGoal : room == 2 ? MarineLife.WhaleDamageGoal :
                    room == 3 ? HermitGeometry.MergeSourceCount : SubmarineEncounter.SubmarineGoal;
                int completed = room == 1 ? game.Combat.BossDamage : room == 2 ? game.Marine.WhaleResonance :
                    room == 3 ? game.Hermits.SmallDefeated : game.Submarines.PhaseHits;
                string remaining = Mathf.Clamp(maximum - completed, 0, maximum).ToString("D2") + "/" + maximum.ToString("D2");
                string text = report.bossStatusTexts[room - 1] = hud.BossStatusText;
                Check(text.Contains(names[room - 1]) && text.Contains(remaining) && text.Contains("LEFT") &&
                    text.Contains("CHARGE") && text.Contains("%"), "Room " + room + " must show boss name, remaining HP, progress, and charge.");
            }
            report.headlessVrHud = !game.Vr.Enabled && game.Flight.VrEnabled;
            hud.SetVrActive(false);
            game.Flight.EnableVr(false);
        }

        IEnumerator VerifyRing()
        {
            game.Restart();
            game.Tutorial.SetEnabled(false);
            Vector3 center = CaveLayout.Rooms[1].Center;
            game.Flight.SetPose(center, Quaternion.identity);
            game.Combat.ActiveRoom = 1;
            yield return null;
            var combat = game.Combat;
            float song = (float)game.Music.Time;
            var ring = combat.RegisterPressureShot(center + Vector3.forward * 45f, Vector3.back, song,
                new Color(.08f, .55f, 1f), owner: this, speed: 1f, radius: 8f, bubbleRing: true, lifetimeOverride: 12f);
            Check(ring != null && ring.pressureRing && ring.pressurePattern == 0 && ring.hp == 1 && ring.reserved == 0 &&
                Mathf.Approximately(ring.pressureRadius, 8f), "An ordinary eight-metre pressure ring must start with one unreserved HP.");
            report.ringInitialHp = ring.hp;
            Check(combat.Acquire(ring), "The ordinary ring must accept a lock.");
            Check(ring.hp == 1 && ring.reserved == 0, "Acquiring a ring must preserve HP until the scheduled shot is released.");
            int hits = combat.Hits;
            double dspStarted = AudioSettings.dspTime;
            combat.Release();
            report.ringPendingHp = ring.hp;
            report.ringPendingReserved = ring.reserved;
            Check(combat.HasPending && combat.Locks.Count == 0 && ring.hp == 1 && ring.reserved == 1 &&
                ring.pressureDissolveAt < 0, "Release must reserve one HP until its real DSP hit.");
            float deadline = Time.realtimeSinceStartup + 6f;
            while (combat.HasPending && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            report.ringHitDspElapsed = AudioSettings.dspTime - dspStarted;
            report.ringHitCount = combat.Hits - hits;
            report.ringHitHp = ring.hp;
            report.ringHitReserved = ring.reserved;
            report.ringDissolveAt = ring.pressureDissolveAt;
            Check(!combat.HasPending && report.ringHitCount == 1 && ring.hp == 0 && ring.reserved == 0 &&
                report.ringHitDspElapsed > 0,
                "The actual DSP hit must consume one HP and clear its reservation.");
            Check(ring.pressureDissolveAt >= 0, "Lethal ring interception must start the shared dissolve envelope.");
            deadline = Time.realtimeSinceStartup + 2f;
            while (game.Music.Time - ring.pressureDissolveAt < .3f && Time.realtimeSinceStartup < deadline) yield return null;
            report.ringVisibleAge = (float)game.Music.Time - ring.pressureDissolveAt;
            report.ringVisibleAtPointThree = ring.visual != null && ring.visual.activeSelf;
            Check(report.ringVisibleAge >= .3f && report.ringVisibleAtPointThree && combat.Targets.Contains(ring) &&
                ring.hp == 0 && ring.reserved == 0, "Destroyed ordinary rings must remain visible at .3 seconds without reviving HP or reservations.");
            report.ringDuration = ParticleTransitionSettings.Current.Duration();
            deadline = Time.realtimeSinceStartup + report.ringDuration + 1f;
            while (game.Music.Time - ring.pressureDissolveAt < report.ringDuration + .2f &&
                Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            report.ringRemovedAfterDuration = !combat.Targets.Contains(ring);
            Check(report.ringRemovedAfterDuration && ring.hp == 0 && ring.reserved == 0,
                "Ring targets must retire after the transition duration without changing resolved HP or reservations.");
        }

        IEnumerator ExportReleaseTone()
        {
            report.releaseRoot = AuthoredScore.Note(0, game.Music.Time);
            AudioClip clip = game.Music.ReleaseTonePreview(report.releaseRoot);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (clip == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                clip = game.Music.ReleaseTonePreview(report.releaseRoot);
            }
            Check(clip != null, "The current authored root must have a boss release tone preview.");
            report.releaseFrequency = MusicTransport.ReleaseToneFrequency(report.releaseRoot);
            report.releaseSampleRate = clip.frequency;
            report.releaseChannels = clip.channels;
            report.releaseFrames = clip.samples;
            var samples = new float[clip.samples * clip.channels];
            Check(clip.GetData(samples, 0), "Release tone PCM samples must be readable.");
            foreach (float sample in samples) report.releasePeak = Mathf.Max(report.releasePeak, Mathf.Abs(sample));
            WriteWav(Path.Combine(output, "boss-crystal.wav"), clip, samples);
        }

        IEnumerator VerifyAtlantis()
        {
            game.Restart();
            game.Tutorial.SetEnabled(false);
            var marine = game.Marine;
            Vector3 player = marine.Arrival.Origin + new Vector3(0f, 10f, -140f);
            Frame(player, marine.Arrival.Origin);
            yield return null;
            float song = (float)game.Music.Time;
            for (int step = 0; step < 400 && !marine.WhaleEntranceComplete; step++)
            {
                song += StepSeconds;
                marine.Tick(song, StepSeconds, player);
                report.entranceSteps++;
            }
            Check(marine.WhaleEntranceComplete && marine.WhaleVisible && marine.Matter.Ready,
                "Real whale arrival and persistent matter must be ready before the finale review.");
            var release = typeof(MarineLife).GetMethod("ReleaseWhale", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "The real whale release hook must exist.");
            release.Invoke(marine, new object[] { song });
            game.Combat.EnterAfterglow();
            game.Finale.Begin(RunProgress.OptionalBosses, song);
            report.simulationStepsBefore = marine.Matter.SimulationSteps;
            for (int step = 0; step < 400; step++)
            {
                song += StepSeconds;
                marine.Tick(song, StepSeconds, player);
                game.Finale.Tick(song, StepSeconds);
            }
            report.simulationStepsAfter = marine.Matter.SimulationSteps;
            report.finaleFormation = game.Finale.Formation;
            report.finaleLayers = game.Finale.LayerCount;
            report.whaleReleased = marine.WhaleReleased;
            game.Brightness.SetOffset(0f);
            game.Brightness.SetFinaleGlow(1f);
            ParticleTransitionSettings.Current.ApplyGlobals();
            Vector3 city = report.cityOrigin = AtlantisGeometry.CityOrigin;

            // Capture before a real-clock frame can reset the accelerated GPU simulation.
            Frame(city + new Vector3(310f, 120f, -320f), city + Vector3.up * 55f);
            game.World.Tick(song, 1f, 0f, game.ReducedMotion);
            report.wideCamera = game.Flight.View.transform.position;
            report.wide = Capture("atlantis-wide.png");
            Vector3 column=city+new Vector3(0,27,-31);
            Frame(column+new Vector3(25,8,-20),column);
            game.World.Tick(song,1f,0f,game.ReducedMotion);
            report.near=Capture("atlantis-near.png");
            Check(report.whaleReleased && game.Combat.Peaceful && report.finaleFormation > .99f && report.finaleLayers == 4,
                "The actual released whale finale must finish with all three optional layers.");
            Check(report.simulationStepsAfter - report.simulationStepsBefore >= 400,
                "The real persistent whale-city simulation must dispatch before capture.");
            Check(report.wide.nonBlank && report.wide.whiteFraction < .08f,
                "Saved wide Atlantis image must be nonblank with fewer than eight percent white pixels.");
            Check(report.near.nonBlank && report.near.whiteFraction<.15f,
                "Saved near Atlantis image must retain shape without a screen-filling white glow.");
        }

        void Frame(Vector3 position, Vector3 focus) => game.Flight.SetPose(position, Quaternion.LookRotation(focus - position, Vector3.up));

        CaptureReport Capture(string name)
        {
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(game.Flight.View, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
                var result = new CaptureReport { file = name, width = width, height = height };
                Color32[] pixels = image.GetPixels32();
                double luminance = 0;
                float min = 255f, max = 0f;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) result.whitePixels++;
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) result.nonBlackPixels++;
                    float value = .2126f * pixel.r + .7152f * pixel.g + .0722f * pixel.b;
                    luminance += value;
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }
                result.whiteFraction = result.whitePixels / (float)pixels.Length;
                result.nonBlackFraction = result.nonBlackPixels / (float)pixels.Length;
                result.meanLuminance = (float)(luminance / (pixels.Length * 255.0));
                result.luminanceRange = (max - min) / 255f;
                result.nonBlank = result.nonBlackPixels > pixels.Length / 10000 && max - min > 8f;
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

        static void WriteWav(string path, AudioClip clip, float[] samples)
        {
            using var writer = new BinaryWriter(File.Create(path));
            int size = samples.Length * 2;
            WriteFourCc(writer, "RIFF");
            writer.Write(36 + size);
            WriteFourCc(writer, "WAVE");
            WriteFourCc(writer, "fmt ");
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((ushort)(clip.channels * 2));
            writer.Write((ushort)16);
            WriteFourCc(writer, "data");
            writer.Write(size);
            foreach (float sample in samples)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767f));
        }

        static void WriteFourCc(BinaryWriter writer, string value)
        {
            foreach (char character in value) writer.Write((byte)character);
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            report.elapsedSeconds = Time.realtimeSinceStartup - started;
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        [Serializable] sealed class CaptureReport
        {
            public string file;
            public int width, height, whitePixels, nonBlackPixels;
            public float whiteFraction, nonBlackFraction, meanLuminance, luminanceRange;
            public bool nonBlank;
        }

        [Serializable] sealed class Report
        {
            public bool passed, headlessVrHud, ringVisibleAtPointThree, ringRemovedAfterDuration, whaleReleased;
            public bool[] pauseTruth;
            public string pauseButtonLabel;
            public string[] bossStatusTexts, errors;
            public int ringInitialHp, ringPendingHp, ringPendingReserved, ringHitHp, ringHitReserved, ringHitCount,
                entranceSteps, simulationStepsBefore, simulationStepsAfter, finaleLayers, releaseRoot,
                releaseSampleRate, releaseChannels, releaseFrames;
            public float elapsedSeconds, ringDissolveAt, ringVisibleAge, ringDuration,
                finaleFormation, releaseFrequency, releasePeak;
            public double ringHitDspElapsed;
            public Vector3 cityOrigin, wideCamera;
            public CaptureReport wide,near;
        }
    }
}
