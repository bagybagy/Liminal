using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Liminal
{
    public sealed class StageAudioProof : MonoBehaviour
    {
        Experience game;
        string output;
        readonly List<string> errors = new();
        readonly Report report = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Verification/ApprovedStageMusic");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!game.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            if (!game.Ready) { errors.Add("Experience readiness timeout."); Finish(); yield break; }
            game.ManualProofTick = true;
            AudioListener.volume = 0;
            try
            {
                Check(game.Music.StageMusicEnabled && game.Music.CurrentTheme == 0, "Approved stage music must be enabled on startup.");
                Check(game.Music.ActiveSoundtrack == game.soundtrack, "The first room must use the unchanged TidalMemory clip.");
                report.firstRoomTidalMemory = true;
                game.Music.RequestTheme(1);
                Check(game.Music.PendingTheme == 1, "Serpent theme was not queued.");
                game.Music.RequestTheme(0);
                Check(game.Music.PendingTheme == -1, "Returning to the active theme must cancel a pending transition.");
                report.pendingCancellation = true;
            }
            catch (Exception exception) { errors.Add(exception.ToString()); Finish(); yield break; }

            foreach (int theme in new[] { 1, 3, 4, 2, 5 })
            {
                try
                {
                    game.Music.RequestTheme(theme == 5 ? 2 : theme, theme == 5);
                    Check(!game.Music.IsCrossfading && game.Music.IncomingFadeGain == 0 &&
                        Mathf.Abs(game.Music.OutgoingFadeGain - 1) < .00001f,
                        "A scheduled crossfade must begin with incoming zero and outgoing full gain.");
                }
                catch (Exception exception) { errors.Add(exception.ToString()); break; }
                double boundary = game.Music.ScheduledBoundary;
                deadline = Time.realtimeSinceStartup + 18;
                while (game.Music.CurrentTheme != theme && Time.realtimeSinceStartup < deadline)
                    yield return null;
                try
                {
                    Check(game.Music.CurrentTheme == theme, "Theme transition timeout: " + theme);
                    var timeline = AuthoredScore.ThemeData(theme);
                    var clip = game.Music.ActiveSoundtrack;
                    Check(clip && clip.samples == timeline.sampleCount && clip.frequency == timeline.sampleRate,
                        "Active clip and measured timeline differ for theme " + theme);
                    Check(theme == 2 ? clip == game.soundtrack : clip.name == AuthoredScore.ThemeNames[theme],
                        "Incorrect recording assigned to theme " + theme);
                    Check(Math.Abs(game.Music.LastTransitionTime - boundary) < 1.0 / timeline.sampleRate,
                        "DSP transition did not use the scheduled outgoing measure boundary.");
                    double before = AuthoredScore.BeatPosition(boundary - .0001);
                    double after = AuthoredScore.BeatPosition(boundary + .0001);
                    Check(after > before && after - before < .01, "Musical phase discontinuity on transition.");
                    for (int i = 0; i < 32; i++)
                    {
                        double song = boundary + timeline.eighths[i] / (double)timeline.sampleRate;
                        Check(AuthoredScore.GridError(song) <= 1.0 / timeline.sampleRate,
                            "Measured eighth grid mismatch for theme " + theme);
                    }
                    report.transitionThemes.Add(theme);
                    if (theme == 2) report.whaleTidalMemory = true;
                    if (theme == 5) report.endingTheme = true;
                    if (theme == 1) VerifyReleasePhrase();
                }
                catch (Exception exception) { errors.Add(exception.ToString()); break; }
                yield return VerifyCrossfade(boundary, theme == 1);
                if (errors.Count > 0) break;
                CheckSilentVolume();
            }

            if (errors.Count == 0)
            {
                double beforePause = game.Music.Time;
                game.Music.SetPaused(true);
                yield return new WaitForSecondsRealtime(.15f);
                try
                {
                    Check(Math.Abs(game.Music.Time - beforePause) < .03, "Pausing must preserve the music clock.");
                    report.pauseClockPreserved = true;
                    game.Music.SetPaused(false);
                    report.maxPlaybackPhaseError = game.Music.MaxPlaybackPhaseError;
                    report.maxReleaseGridError = game.Music.MaxReleaseGridError;
                    game.Music.Restart();
                    Check(game.Music.CurrentTheme == 0 && game.Music.ActiveSoundtrack == game.soundtrack,
                        "Restart must return to TidalMemory without pending endings.");
                    report.restartTidalMemory = true;
                }
                catch (Exception exception) { errors.Add(exception.ToString()); }
            }
            Finish();
        }

        IEnumerator VerifyCrossfade(double boundary, bool pauseDuringFade)
        {
            var music = game.Music;
            double duration = music.CrossfadeDuration;
            double expectedEnd = AuthoredScore.TimeAfterBeats(boundary, 8);
            double sampleTolerance = 1.0 / AuthoredScore.TimelineAt(boundary).sampleRate;
            try
            {
                Check(music.IsCrossfading && music.CrossfadeProgress < .2f,
                    "The proof must observe the start of a nontrivial crossfade.");
                Check(duration > 2 && Math.Abs(boundary + duration - expectedEnd) <= sampleTolerance,
                    "A crossfade must last eight measured incoming beats (two bars).");
            }
            catch (Exception exception) { errors.Add(exception.ToString()); yield break; }

            float previousProgress = 0, previousIncoming = 0, previousOutgoing = 1;
            int interiorSamples = 0;
            bool early = false, middle = false, late = false, paused = false;
            float deadline = Time.realtimeSinceStartup + (float)duration + 2;
            while (music.IsCrossfading && Time.realtimeSinceStartup < deadline)
            {
                float progress = music.CrossfadeProgress;
                float incoming = music.IncomingFadeGain;
                float outgoing = music.OutgoingFadeGain;
                try
                {
                    Check(incoming >= 0 && incoming <= 1 && outgoing >= 0 && outgoing <= 1,
                        "Crossfade gains must stay within the original music level.");
                    float powerError = Mathf.Abs(incoming * incoming + outgoing * outgoing - 1);
                    Check(powerError < .0002f, "Crossfade squared gains must sum to one.");
                    report.maxCrossfadePowerError = Mathf.Max(report.maxCrossfadePowerError, powerError);
                    double smooth = progress * progress * (3 - 2 * progress);
                    Check(Math.Abs(incoming - Math.Sin(smooth * Math.PI * .5)) < .0002 &&
                        Math.Abs(outgoing - Math.Cos(smooth * Math.PI * .5)) < .0002,
                        "Actual source gains must follow the smoothstep equal-power curve.");
                    Check(progress >= previousProgress && incoming + .00002f >= previousIncoming &&
                        outgoing <= previousOutgoing + .00002f, "Crossfade gains must change monotonically.");
                    // Both curve derivatives are bounded by 3*pi/4 with respect to progress.
                    float maxChange = 2.36f * (progress - previousProgress) + .0002f;
                    Check(incoming - previousIncoming <= maxChange && previousOutgoing - outgoing <= maxChange,
                        "Crossfade gains must change smoothly between observed frames.");
                    Check(Math.Abs(music.Time - (boundary + progress * duration)) <= Math.Max(.05, Time.unscaledDeltaTime * 2),
                        "Crossfade progress must track the full measured duration.");
                    if (progress >= .2f && progress <= .8f)
                    {
                        Check(incoming > .1f && outgoing > .1f, "Both sources must have audible interior fade gains.");
                        interiorSamples++;
                        early |= progress <= .35f;
                        middle |= progress >= .4f && progress <= .6f;
                        late |= progress >= .65f;
                    }
                }
                catch (Exception exception) { errors.Add(exception.ToString()); yield break; }

                previousProgress = progress;
                previousIncoming = incoming;
                previousOutgoing = outgoing;
                if (pauseDuringFade && !paused && progress >= .35f && progress < .8f)
                {
                    double clock = music.Time;
                    paused = true;
                    music.SetPaused(true);
                    yield return new WaitForSecondsRealtime(.2f);
                    try
                    {
                        Check(music.IsCrossfading && music.CrossfadeProgress == progress &&
                            music.IncomingFadeGain == incoming && music.OutgoingFadeGain == outgoing,
                            "Pausing during a crossfade must freeze both source volumes and fade progress.");
                        Check(Math.Abs(music.Time - clock) < .03, "Pausing a crossfade must freeze the music clock.");
                        CheckSilentVolume();
                        report.crossfadePauseFrozen = true;
                    }
                    catch (Exception exception) { errors.Add(exception.ToString()); music.SetPaused(false); yield break; }
                    music.SetPaused(false);
                }
                yield return null;
            }

            try
            {
                Check(!music.IsCrossfading && music.CrossfadeProgress == 1 && music.OutgoingFadeGain == 0 &&
                    Mathf.Abs(music.IncomingFadeGain - 1) < .00001f,
                    "A completed crossfade must reach outgoing zero and incoming full gain.");
                Check(music.Time >= expectedEnd - sampleTolerance &&
                    music.Time <= expectedEnd + Math.Max(.25, Time.unscaledDeltaTime * 2),
                    "The crossfade must complete at the eighth incoming beat, with only frame latency.");
                Check(interiorSamples >= 6 && early && middle && late,
                    "Crossfade verification must sample both audible sources across early, middle and late interiors.");
                Check(!pauseDuringFade || paused && report.crossfadePauseFrozen,
                    "The first crossfade must include an interior pause and resume.");
                report.crossfadeDurations.Add(duration);
                report.crossfadeInteriorSamples += interiorSamples;
                report.verifiedCrossfades++;
            }
            catch (Exception exception) { errors.Add(exception.ToString()); }
        }

        void VerifyReleasePhrase()
        {
            int events = game.Music.BossReleaseEvents;
            int notes = game.Music.ScheduledReleaseNotes;
            game.Music.BossRelease((float)game.Music.Time);
            Check(game.Music.BossReleaseEvents == events + 1 && game.Music.ScheduledReleaseNotes == notes + 3,
                "A generated boss release must schedule three tones as one event.");
            int[] pitches = game.Music.LastReleasePitches;
            double[] onsets = game.Music.LastReleaseOnsets;
            Check(pitches.Length == 3 && onsets.Length == 3 && onsets[0] < onsets[1] && onsets[1] < onsets[2],
                "Boss release phrase is not sequential.");
            for (int i = 0; i < 3; i++)
                Check(pitches[i] == AuthoredScore.Note(i, onsets[i]) && AuthoredScore.GridError(onsets[i]) < .00003,
                    "Release pitches and onsets must follow the active measured score.");
            report.releasePitches = (int[])pitches.Clone();
            report.releaseOnsets = (double[])onsets.Clone();
            report.threeNoteRelease = true;
            var settings = BossAudioSettings.Current;
            AudioClip saved = settings.defaultRelease;
            var replacement = AudioClip.Create("Replacement release phrase", 4410, 1, 44100, false);
            try
            {
                settings.defaultRelease = replacement;
                notes = game.Music.ScheduledReleaseNotes;
                events = game.Music.BossReleaseEvents;
                game.Music.BossRelease((float)game.Music.Time);
                Check(game.Music.ScheduledReleaseNotes == notes + 1 && game.Music.BossReleaseEvents == events + 1,
                    "A configured replacement phrase must play once, not three times.");
                report.customReleaseOnce = true;
            }
            finally { settings.defaultRelease = saved; Destroy(replacement); }
        }

        void CheckSilentVolume()
        {
            if (AudioListener.volume > 0) errors.Add("Background acceptance must remain silent.");
        }

        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
                if (errors.Count < 10) errors.Add(message);
        }

        void Finish()
        {
            game.Music.SetPaused(false);
            report.maxPlaybackPhaseError = Math.Max(report.maxPlaybackPhaseError, game.Music.MaxPlaybackPhaseError);
            report.maxReleaseGridError = Math.Max(report.maxReleaseGridError, game.Music.MaxReleaseGridError);
            report.errors = errors.ToArray();
            report.passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "runtime-report.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= OnLog;
            Debug.Log("LIMINAL_STAGE_AUDIO_PROOF passed=" + report.passed);
            Application.Quit(report.passed ? 0 : 1);
        }

        [Serializable] sealed class Report
        {
            public bool passed, firstRoomTidalMemory, whaleTidalMemory, endingTheme, pendingCancellation;
            public bool threeNoteRelease, customReleaseOnce, pauseClockPreserved, restartTidalMemory;
            public bool crossfadePauseFrozen;
            public int verifiedCrossfades, crossfadeInteriorSamples;
            public float maxCrossfadePowerError;
            public List<double> crossfadeDurations = new();
            public List<int> transitionThemes = new();
            public int[] releasePitches;
            public double[] releaseOnsets;
            public double maxPlaybackPhaseError, maxReleaseGridError;
            public bool audibleReviewPerformed = false;
            public string[] errors;
        }
    }
}
