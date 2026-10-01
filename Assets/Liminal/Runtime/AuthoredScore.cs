using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    // This is exported by the audio composer, not inferred from a nominal BPM.
    public static class AuthoredScore
    {
        [Serializable] public sealed class Harmony { public int sample; public int[] notes; }
        [Serializable] public sealed class Timeline
        {
            public int sampleRate, sampleCount;
            public int bars, bpm, themeId;
            public string theme;
            public string sourceSha256;
            public int[] beats, eighths, sections;
            public Harmony[] harmony;
        }
        sealed class ThemeSpan
        {
            public int theme;
            public double start;
            public Timeline timeline;
        }

        static readonly string[] themeNames = {
            "JellyGrotto", "SerpentSanctum", "HorizonWhale", "TidalShells", "ScarletEngine", "Afterglow"
        };
        static readonly Timeline[] themeTimelines = new Timeline[6];
        static readonly List<ThemeSpan> themeSpans = new();
        static Timeline data;
        public static Timeline Data => data ??= Load();
        public static int ThemeCount => themeNames.Length;
        public static IReadOnlyList<string> ThemeNames => themeNames;
        public static double Duration => Data.sampleCount / (double)Data.sampleRate;

        static Timeline Load()
        {
            var asset = Resources.Load<TextAsset>("TidalMemoryTimeline");
            if (!asset) throw new InvalidOperationException("Missing authored soundtrack timeline. Run Tools/compose.mjs.");
            var result = JsonUtility.FromJson<Timeline>(asset.text);
            if (result == null || result.sampleRate <= 0 || result.sampleCount <= 0 ||
                result.beats == null || result.beats.Length == 0 || result.eighths == null ||
                result.eighths.Length == 0 || result.harmony == null || result.harmony.Length == 0)
                throw new InvalidOperationException("Invalid soundtrack timeline.");
            return result;
        }

        public static string ThemeResourcePath(int theme) => "StageAudio/" + themeNames[theme];
        public static string ThemeTimelineResourcePath(int theme) => ThemeResourcePath(theme) + "Timeline";

        public static Timeline ThemeData(int theme)
        {
            if (theme < 0 || theme >= themeNames.Length) throw new ArgumentOutOfRangeException(nameof(theme));
            if (themeTimelines[theme] != null) return themeTimelines[theme];
            var asset = Resources.Load<TextAsset>(ThemeTimelineResourcePath(theme));
            if (!asset) throw new InvalidOperationException("Missing authored stage timeline: " + themeNames[theme]);
            var result = JsonUtility.FromJson<Timeline>(asset.text);
            int expectedSamples = result == null ? 0 : (int)Math.Round(result.bars * 4 * Score.BeatSeconds * result.sampleRate);
            if (result == null || result.themeId != theme || result.theme != themeNames[theme] || result.bpm != Score.Bpm ||
                result.sampleRate != Data.sampleRate || result.sampleCount != expectedSamples || result.bars != 64 ||
                result.beats == null || result.beats.Length != result.bars * 4 ||
                result.eighths == null || result.eighths.Length != result.bars * 8 || result.sections == null ||
                result.harmony == null || result.harmony.Length != result.bars / 4)
                throw new InvalidOperationException("Invalid authored stage timeline: " + themeNames[theme]);
            themeTimelines[theme] = result;
            return result;
        }

        public static void ResetThemeSchedule(int initialTheme)
        {
            themeSpans.Clear();
            if (initialTheme >= 0) themeSpans.Add(new ThemeSpan { theme = initialTheme, start = 0, timeline = ThemeData(initialTheme) });
        }

        public static void QueueThemeTransition(int theme, double startSongTime)
        {
            var timeline = ThemeData(theme);
            int keep = 0;
            while (keep < themeSpans.Count && themeSpans[keep].start < startSongTime - 1e-9) keep++;
            if (keep > 0 && themeSpans[keep - 1].theme == theme) {
                if (keep < themeSpans.Count) themeSpans.RemoveRange(keep, themeSpans.Count - keep);
                return;
            }
            if (keep < themeSpans.Count) themeSpans.RemoveRange(keep, themeSpans.Count - keep);
            themeSpans.Add(new ThemeSpan { theme = theme, start = startSongTime, timeline = timeline });
        }

        public static void CancelThemeTransitionsFrom(double startSongTime)
        {
            int keep = 0;
            while (keep < themeSpans.Count && themeSpans[keep].start < startSongTime - 1e-9) keep++;
            if (keep < themeSpans.Count) themeSpans.RemoveRange(keep, themeSpans.Count - keep);
        }

        static ThemeSpan SpanAt(double songTime)
        {
            int lo = 0, hi = themeSpans.Count;
            while (lo < hi) {
                int mid = (lo + hi) / 2;
                if (themeSpans[mid].start <= songTime + 1e-9) lo = mid + 1; else hi = mid;
            }
            return lo == 0 ? null : themeSpans[lo - 1];
        }

        public static Timeline TimelineAt(double songTime)
        {
            var span = SpanAt(songTime);
            return span == null ? Data : span.timeline;
        }

        public static double LocalSampleAt(double songTime)
        {
            var span = SpanAt(songTime);
            if (span == null) return LocalSample(songTime);
            double sample = Math.Max(0, songTime - span.start) * span.timeline.sampleRate;
            double nearest = Math.Round(sample);
            if (Math.Abs(sample - nearest) < .00001) sample = nearest;
            return sample % span.timeline.sampleCount;
        }

        public static int ThemeAt(double songTime)
        {
            var span = SpanAt(songTime);
            return span == null ? -1 : span.theme;
        }

        public static double NextFourBarBoundary(double songTime, double schedulingLead)
        {
            double bar = Score.BeatSeconds * 16;
            double target = Math.Max(0, songTime + Math.Max(0, schedulingLead));
            return Math.Round((Math.Floor(target / bar) + 1) * bar * Data.sampleRate) / Data.sampleRate;
        }

        public static double Next(double song, double lead, bool quarter)
        {
            var span = SpanAt(song + lead);
            var timeline = span == null ? Data : span.timeline;
            double start = span == null ? 0 : span.start;
            double sample = Math.Max(0, song + lead - start) * timeline.sampleRate;
            long loop = (long)Math.Floor(sample / timeline.sampleCount);
            double local = sample - loop * timeline.sampleCount;
            var marks = quarter ? timeline.beats : timeline.eighths;
            int lo = 0, hi = marks.Length;
            while (lo < hi) {
                int mid = (lo + hi) / 2;
                if (marks[mid] + .00001 < local) lo = mid + 1; else hi = mid;
            }
            return start + (loop * timeline.sampleCount + (lo < marks.Length ? marks[lo] : timeline.sampleCount)) / (double)timeline.sampleRate;
        }

        public static int Previous(int[] marks, double localSample)
        {
            int lo = 0, hi = marks.Length;
            while (lo < hi) { int mid = (lo + hi) / 2; if (marks[mid] <= localSample) lo = mid + 1; else hi = mid; }
            return Math.Max(0, lo - 1);
        }
        public static double LocalSample(double song)
        {
            double sample = Math.Max(0, song) * Data.sampleRate;
            double nearest = Math.Round(sample);
            if (Math.Abs(sample - nearest) < .00001) sample = nearest;
            return sample % Data.sampleCount;
        }
        public static float Pulse(double song, float decay)
        {
            var timeline = TimelineAt(song);
            double sample = LocalSampleAt(song);
            int beat = Previous(timeline.beats, sample);
            int end = beat + 1 < timeline.beats.Length ? timeline.beats[beat + 1] : timeline.sampleCount;
            return Mathf.Exp(-(float)((sample - timeline.beats[beat]) / (end - timeline.beats[beat])) * decay);
        }
        public static double BeatPosition(double song)
        {
            var span = SpanAt(song);
            var timeline = span == null ? Data : span.timeline;
            double start = span == null ? 0 : span.start;
            double sample = Math.Max(0, song - start) * timeline.sampleRate;
            long loop = (long)Math.Floor(sample / timeline.sampleCount);
            double local = LocalSampleAt(song);
            int beat = Previous(timeline.beats, local);
            int end = beat + 1 < timeline.beats.Length ? timeline.beats[beat + 1] : timeline.sampleCount;
            return start / Score.BeatSeconds + loop * timeline.beats.Length + beat +
                (local - timeline.beats[beat]) / Math.Max(1, end - timeline.beats[beat]);
        }
        public static int Note(int index, double song)
        {
            var span = SpanAt(song);
            Timeline timeline = span == null ? Data : span.timeline;
            double sample = span == null ? LocalSample(song) : LocalSampleAt(song);
            int chord = 0;
            while (chord + 1 < timeline.harmony.Length && timeline.harmony[chord + 1].sample <= sample) chord++;
            int[] notes = timeline.harmony[chord].notes;
            return notes[index % notes.Length] + 12 + 12 * (index / notes.Length);
        }
        public static double GridError(double song)
        {
            var timeline = TimelineAt(song);
            double sample = LocalSampleAt(song);
            int i = Previous(timeline.eighths, sample);
            int next = i + 1 < timeline.eighths.Length ? timeline.eighths[i + 1] : timeline.sampleCount;
            return Math.Min(Math.Abs(sample - timeline.eighths[i]), Math.Abs(next - sample)) / timeline.sampleRate;
        }
    }
}
