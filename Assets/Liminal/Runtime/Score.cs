using System;
using UnityEngine;

namespace Liminal
{
    public static class Score
    {
        public const double Bpm = 124.0;
        public const double BeatSeconds = 60.0 / Bpm;
        public const int Bars = 104;
        public const double Duration = Bars * 4 * BeatSeconds;
        public static readonly int[] Scale = { 62, 65, 69, 72, 74, 77, 81, 84 };
        public static double NextEighth(double songTime, double lead = 0.24)
        {
            return AuthoredScore.Next(songTime, lead, false);
        }
        public static float Pulse(double songTime, float decay = 6f)
        {
            return AuthoredScore.Pulse(songTime, decay);
        }
        public static int Section(double time) => AuthoredScore.Previous(AuthoredScore.Data.sections, AuthoredScore.LocalSample(time));
        public static string SectionName(int section) => section switch
        {
            0 => "SUBMERGENCE", 1 => "FIRST CONTACT", 2 => "THE ABYSSAL CHOIR",
            3 => "METAMORPHOSIS", 4 => "TRANSCENDENCE", _ => "AFTERGLOW"
        };
    }
}
