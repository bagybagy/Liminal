using System;
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
            public string sourceSha256;
            public int[] beats, eighths, sections;
            public Harmony[] harmony;
        }
        static Timeline data;
        public static Timeline Data => data ??= Load();
        public static double Duration => Data.sampleCount / (double)Data.sampleRate;

        static Timeline Load()
        {
            var asset = Resources.Load<TextAsset>("TidalMemoryTimeline");
            if (!asset) throw new InvalidOperationException("Missing authored soundtrack timeline. Run Tools/compose.mjs.");
            var result = JsonUtility.FromJson<Timeline>(asset.text);
            if (result.sampleRate <= 0 || result.sampleCount <= 0 || result.beats.Length == 0 || result.harmony.Length == 0)
                throw new InvalidOperationException("Invalid soundtrack timeline.");
            return result;
        }

        public static double Next(double song, double lead, bool quarter)
        {
            double sample = Math.Max(0, song + lead) * Data.sampleRate;
            long loop = (long)Math.Floor(sample / Data.sampleCount);
            double local = sample - loop * Data.sampleCount;
            var marks = quarter ? Data.beats : Data.eighths;
            int lo = 0, hi = marks.Length;
            while (lo < hi) {
                int mid = (lo + hi) / 2;
                if (marks[mid] + .00001 < local) lo = mid + 1; else hi = mid;
            }
            return (loop * Data.sampleCount + (lo < marks.Length ? marks[lo] : Data.sampleCount)) / (double)Data.sampleRate;
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
            double sample = LocalSample(song);
            int beat = Previous(Data.beats, sample);
            int end = beat + 1 < Data.beats.Length ? Data.beats[beat + 1] : Data.sampleCount;
            return Mathf.Exp(-(float)((sample - Data.beats[beat]) / (end - Data.beats[beat])) * decay);
        }
        public static double BeatPosition(double song)
        {
            double sample = Math.Max(0, song) * Data.sampleRate;
            long loop = (long)Math.Floor(sample / Data.sampleCount);
            double local = LocalSample(song);
            int beat = Previous(Data.beats, local);
            int end = beat + 1 < Data.beats.Length ? Data.beats[beat + 1] : Data.sampleCount;
            return loop * Data.beats.Length + beat + (local - Data.beats[beat]) / Math.Max(1, end - Data.beats[beat]);
        }
        public static int Note(int index, double song)
        {
            double sample = LocalSample(song);
            int chord = 0;
            while (chord + 1 < Data.harmony.Length && Data.harmony[chord + 1].sample <= sample) chord++;
            int[] notes = Data.harmony[chord].notes;
            return notes[index % notes.Length] + 12 + 12 * (index / notes.Length);
        }
        public static double GridError(double song)
        {
            double sample = LocalSample(song);
            int i = Previous(Data.eighths, sample);
            int next = i + 1 < Data.eighths.Length ? Data.eighths[i + 1] : Data.sampleCount;
            return Math.Min(Math.Abs(sample - Data.eighths[i]), Math.Abs(next - sample)) / Data.sampleRate;
        }
    }
}
