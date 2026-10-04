using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class EndingReleaseProof : MonoBehaviour
    {
        Experience game;
        string output;
        readonly Report report = new();
        readonly List<string> errors = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Verification/EndingRelease");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            while (!game.Ready) yield return null;
            game.ManualProofTick = true;
            AudioListener.volume = 0;
            try {
                game.Tutorial.SetEnabled(false);
                CheckVrLockFeedback();
                game.Combat.EnterAfterglow();
                int targetCount = game.Combat.Targets.Count;
                float song = (float)game.Music.Time;
                for (int mask = 0; mask < 8; mask++) {
                    game.Progress.Reset();
                    foreach (BossId boss in new[] { BossId.Serpent, BossId.Hermit, BossId.Submarine })
                        if (((BossId)mask & boss) != 0) {
                            Check(game.Progress.Record(boss), "Optional boss must count once.");
                            Check(!game.Progress.Record(boss), "Revisit must not increase the ending count.");
                        }
                    Check(game.Progress.Record(BossId.Whale) && game.Progress.EndingStarted &&
                        game.Progress.EndingMask == (BossId)mask, "Whale completion must freeze the exact optional boss mask.");
                    int theme = AuthoredScore.EndingThemeFor(game.Progress.EndingMask);
                    Check(theme == 5 + RunProgress.Count((BossId)mask), "Incorrect ending recording for a progress mask.");
                    game.Finale.Begin(game.Progress.EndingMask, song);
                    for (int step = 0; step < 190; step++) game.Finale.Tick(song + step * .1f, .1f);
                    Check(game.Finale.Formation > .99f && game.Finale.LayerCount == 1 + RunProgress.Count((BossId)mask),
                        "Atlantis layers must preserve all eight ending combinations.");
                    Check(game.Combat.Targets.Count == targetCount && game.Finale.FaunaTargetCount == 0,
                        "Celebration creatures must never become combat targets.");
                    Check(game.Finale.CelebrationFaunaActive == (mask == 7), "Extra fauna must be exclusive to full completion.");
                    if (mask != 7) Check(game.Finale.ActiveFaunaPointCount == 0, "Partial endings must not draw celebration fauna.");
                    report.maskChecks++;
                }
                report.desktopExtraPoints = game.Finale.ActiveFaunaPointCount;
                report.extraFish = game.Finale.ActiveFaunaFishCount;
                report.totalFish = game.Finale.SerpentFishCount + report.extraFish;
                report.hermits = game.Finale.CelebrationHermitCrabCount;
                report.pufferfish = game.Finale.CelebrationPufferfishCount;
                Check(report.desktopExtraPoints > 0 && report.desktopExtraPoints <= game.Finale.DesktopFaunaPointBudget,
                    "Desktop celebration must remain inside its bounded point budget.");
                Check(report.extraFish >= game.Finale.SerpentFishCount, "Full completion must at least double the finale fish population.");
                game.Flight.EnableVr(true);
                game.Finale.Tick(song + 19, .1f);
                report.vrExtraPoints = game.Finale.ActiveFaunaPointCount;
                Check(game.Finale.UsesReducedDensity && report.vrExtraPoints > 0 &&
                    report.vrExtraPoints <= game.Finale.VrFaunaPointBudget && report.vrExtraPoints < report.desktopExtraPoints,
                    "VR must switch to a smaller real mesh, not just dim dense particles.");
                game.Flight.EnableVr(false);
                game.Finale.Tick(song + 20, .1f);
                Check(game.Finale.ActiveFaunaPointCount == report.desktopExtraPoints, "Exiting VR must restore the desktop fauna mesh.");

                // Exercise the existing whale-to-city transfer before capturing the actual finale matter.
                Vector3 entrance = game.Marine.Arrival.Origin;
                game.Marine.Tick(song, .05f, entrance);
                game.Marine.Tick(song + 12, .05f, entrance);
                var release = typeof(MarineLife).GetMethod("ReleaseWhale", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(release != null, "The existing whale release hook must be available.");
                release.Invoke(game.Marine, new object[] { song + 12 });
                for (int step = 1; step <= 400; step++) game.Marine.Tick(song + 12 + step * .05f, .05f, entrance);
                game.Brightness.SetFinaleGlow(1);

                var credits = game.Finale.Credits;
                credits.Begin(song);
                report.creditLines = ParticleCredits.LineCount;
                report.contributors = ParticleCredits.ContributorCount;
                report.creditSeconds = ParticleCredits.Duration;
                report.creditParticles = credits.StableParticleCount;
                Check(Mathf.Abs(report.creditSeconds - 180) < .01f && report.creditLines > 19 && report.contributors > 50,
                    "Expanded staff roll must last three minutes and contain the contributor roster.");
                for (int step = 0; step < 180; step++) credits.Tick(song, .1f);
                report.visibleRows = credits.VisibleLineCount;
                Check(report.visibleRows >= 4, "Several credit rows must form simultaneously.");
                Check(Hud.ShouldShowCredits(credits, false, false) && !Hud.ShouldShowCredits(credits, true, false) &&
                    !Hud.ShouldShowCredits(credits, false, true), "Desktop overlay must disappear in pause and VR only.");
                for (int step = 0; step < 1900 && !credits.Completed; step++) credits.Tick(song, .1f);
                report.seenLines = credits.SeenLines;
                report.completedCycles = credits.CycleCount;
                Check(credits.Completed && report.seenLines == report.creditLines && report.completedCycles == report.creditLines,
                    "Every contributor line must complete the full gather, roll, scatter and return cycle.");
                Check(credits.StableParticleCount == report.creditParticles && !Hud.ShouldShowCredits(credits, false, false),
                    "Ending must reuse stable pools and finish the overlay.");
                credits.Begin(song);
                for (int step = 0; step < 180; step++) credits.Tick(song, .1f);
                Frame(AtlantisGeometry.CityOrigin + new Vector3(210, 150, -400), AtlantisGeometry.CityOrigin + Vector3.up * 36);
            }
            catch (Exception exception) { errors.Add(exception.ToString()); Finish(); yield break; }
            yield return null;
            try {
                Capture("atlantis-full-clear.png");
                CheckFaunaVisibility(false);
                CheckFaunaVisibility(true);
                game.Flight.EnableVr(false);
                game.Finale.Tick(64f, .1f);
                Frame(game.Finale.Credits.Position + Vector3.back * 300, game.Finale.Credits.Position);
            }
            catch (Exception exception) { errors.Add(exception.ToString()); Finish(); yield break; }
            yield return null;
            try {
                Capture("credits-world.png");
                game.Finale.ResetFinale();
                Check(!game.Finale.CelebrationFaunaActive && game.Finale.ActiveFaunaPointCount == 0 &&
                    !game.Finale.Credits.Active, "Retry must retire the celebration and credits.");
            }
            catch (Exception exception) { errors.Add(exception.ToString()); }
            Finish();
        }

        void Frame(Vector3 position, Vector3 target) => game.Flight.SetPose(position, Quaternion.LookRotation(target - position));
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 12)
                errors.Add(message);
        }
        void CheckFaunaVisibility(bool useVr)
        {
            game.Flight.EnableVr(useVr);
            game.Finale.Tick(64f, .1f);
            Check(game.Finale.CelebrationFaunaActive && game.Finale.UsesReducedDensity == useVr,
                "Full-clear fauna must remain active during a VR density switch.");
            string mode = useVr ? "vr-density" : "desktop";
            Vector3 crab = AtlantisGeometry.CityOrigin + AtlantisFauna.HermitCenter(0, useVr);
            Frame(crab + new Vector3(25f, 18f, -32f), crab + Vector3.up * 4f);
            int crabPixels = VisibleContribution("Atlantis / peaceful seabed hermit crabs", mode + "-hermits.png");
            Vector3 school = AtlantisGeometry.CityOrigin + AtlantisGeometry.EvaluateSchoolPose(0, 64f).Center;
            Frame(school + new Vector3(60f, 24f, -80f), school);
            int fishPixels = VisibleContribution("Atlantis / expanded fish and puffer schools", mode + "-fish.png");
            Check(crabPixels >= 600 && fishPixels >= 2500,
                "Fauna must contribute visible rendered pixels, not merely exist in mesh counts: " +
                mode + " crabs=" + crabPixels + " fish=" + fishPixels);
            if (useVr) { report.vrHermitVisiblePixels = crabPixels; report.vrFishVisiblePixels = fishPixels; }
            else { report.desktopHermitVisiblePixels = crabPixels; report.desktopFishVisiblePixels = fishPixels; }
        }
        void CheckVrLockFeedback()
        {
            VrWorldHud hud = game.GetComponent<VrWorldHud>();
            Check(hud != null, "VR lock feedback must have a world-space HUD owner.");
            game.Music.SetPaused(false);
            game.Flight.EnableVr(true);
            hud.SetVrActive(true);
            hud.Tick(0f, .1f);
            var visual = new GameObject("Bounded VR lock proof target");
            visual.transform.position = game.Flight.View.transform.position + game.Flight.View.transform.forward * 35f;
            LockTarget target = game.Combat.RegisterEnvironment(visual, null);
            target.hp = 8;
            target.lockCapacity = 8;
            Texture2D absent = null, present = null;
            try {
                absent = ReadFrame();
                for (int i = 0; i < 8; i++) Check(game.Combat.Acquire(target), "VR must acquire each available lock.");
                hud.Tick(0f, .1f);
                VrLockFeedbackProof.Verify(game);
                Check(hud.VisibleLocks == 8 && hud.GameplayHudHidden,
                    "All acquired VR locks must be visible without restoring HP or counters.");
                present = ReadFrame();
                report.vrLockVisiblePixels = ChangedPixels(absent, present);
                File.WriteAllBytes(Path.Combine(output, "vr-lock-feedback.png"), present.EncodeToPNG());
                Check(report.vrLockVisiblePixels >= 400, "VR acquired rings must actually render visible pixels.");
                game.Music.SetPaused(true);
                hud.Tick(0f, .1f);
                VrLockFeedbackProof.Verify(game);
                Check(hud.VisibleLocks == 0 && hud.PauseMenuVisible, "Pause must hide rings, not its controls.");
                game.Music.SetPaused(false);
                hud.Tick(0f, .1f);
                Check(hud.VisibleLocks == 8, "Resuming must restore any retained acquired-ring feedback.");
                game.Combat.Release();
                hud.Tick(0f, .1f);
                VrLockFeedbackProof.Verify(game);
                Check(hud.VisibleLocks == 0, "Firing must retire acquired rings.");
                target.reserved = 0;
                Check(game.Combat.Acquire(target), "VR exit must also clear a currently drawn acquired ring.");
                hud.Tick(0f, .1f);
                hud.SetVrActive(false);
                VrLockFeedbackProof.Verify(game);
                Check(hud.VisibleLocks == 0 && hud.ActiveLockRingRendererCount == 0,
                    "VR exit must not leave ghost ring renderers.");
                report.vrLockLifecyclePassed = true;
            }
            finally {
                game.Combat.AbandonLocks();
                game.Combat.Targets.Remove(target);
                Destroy(visual);
                game.Music.SetPaused(false);
                hud.SetVrActive(false);
                game.Flight.EnableVr(false);
                if (absent) Destroy(absent);
                if (present) Destroy(present);
            }
        }
        int VisibleContribution(string objectName, string fileName)
        {
            MeshRenderer renderer = null;
            foreach (MeshRenderer candidate in game.World.GetComponentsInChildren<MeshRenderer>(true))
                if (candidate.name == objectName) { renderer = candidate; break; }
            Check(renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy,
                "Fauna renderer must be active in the actual scene hierarchy.");
            Texture2D absent = null, present = null;
            try {
                renderer.enabled = false;
                absent = ReadFrame();
                renderer.enabled = true;
                present = ReadFrame();
                File.WriteAllBytes(Path.Combine(output, fileName), present.EncodeToPNG());
                return ChangedPixels(absent, present);
            }
            finally {
                renderer.enabled = true;
                if (absent) Destroy(absent);
                if (present) Destroy(present);
            }
        }
        static int ChangedPixels(Texture2D absent, Texture2D present)
        {
            Color32[] baseline = absent.GetPixels32(), pixels = present.GetPixels32();
            int visible = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r - baseline[i].r > 14 || pixels[i].g - baseline[i].g > 14 ||
                    pixels[i].b - baseline[i].b > 14) visible++;
            return visible;
        }
        void Capture(string name)
        {
            Texture2D image = ReadFrame();
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            Destroy(image);
        }
        Texture2D ReadFrame()
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            target.Create();
            RenderPipeline.SubmitRenderRequest(game.Flight.View, new RenderPipeline.StandardRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            target.Release();
            Destroy(target);
            return image;
        }
        void Finish()
        {
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "ending-report.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= OnLog;
            Debug.Log("LIMINAL_ENDING_RELEASE_PROOF passed=" + report.passed);
            Application.Quit(report.passed ? 0 : 1);
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public int maskChecks, desktopExtraPoints, vrExtraPoints, extraFish, totalFish, hermits, pufferfish;
            public int desktopHermitVisiblePixels, desktopFishVisiblePixels, vrHermitVisiblePixels, vrFishVisiblePixels;
            public int vrLockVisiblePixels;
            public bool vrLockLifecyclePassed;
            public int creditLines, contributors, creditParticles, seenLines, completedCycles, visibleRows;
            public float creditSeconds;
            public bool offscreenSilent = true;
            public bool physicalVrReviewPerformed = false;
            public string[] errors;
        }
    }
}
