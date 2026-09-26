using UnityEngine;

namespace Liminal
{
    public static class Anatomy
    {
        public const float SpineDelay = 11.5f;
        public const int SpineSamples = 257;
        public static Vector3 Head(float song)
        {
            float a = song * 0.065f - 0.85f;
            return new Vector3(Mathf.Sin(a) * 88 + Mathf.Sin(a * 2) * 14,
                10 + Mathf.Sin(a * 2 + 0.6f) * 24 + Mathf.Sin(a * 0.5f) * 8,
                35 + Mathf.Cos(a) * 104);
        }
        public static Vector3 Focus(float song) => Center(0.43f, song);
        public static Vector3 Center(float u, float song)
        {
            // Each vertebra follows the head's history, so turns propagate down the body.
            float time = song - u * SpineDelay;
            Vector3 forward = (Head(time + 0.02f) - Head(time - 0.02f)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            return Head(time) + side * Mathf.Sin(u * 15 - song * 1.2f) * Mathf.Sin(u * Mathf.PI) * 1.8f;
        }
        public static float Width(float u) => (0.3f + Mathf.Pow(Mathf.Clamp01(Mathf.Sin((u * 0.88f + 0.07f) * Mathf.PI)), 0.65f) * 2.9f) *
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 1, u)) * 0.9f) *
            (1+Mathf.Exp(-Mathf.Pow((u-0.047f)/0.032f,2))*0.7f) *
            (0.12f+0.88f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,0.03f,u)));
        public static Vector3 Node(float u, float song)
        {
            Vector3 c = Center(u, song);
            Vector3 tangent = (Center(u + 0.002f, song) - Center(u - 0.002f, song)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 up = Vector3.Cross(tangent, side).normalized;
            float angle = 0.55f + Mathf.Round(u / 0.058f) % 2 * Mathf.PI;
            return c + (up * Mathf.Sin(angle) + side * Mathf.Cos(angle)) * Width(u) * 1.06f;
        }
        public static void WriteSpine(Vector4[] samples, float song)
        {
            for (int i = 0; i < samples.Length; i++) {
                float u = i / (float)(samples.Length - 1);
                Vector3 p = Center(u, song);
                samples[i] = new Vector4(p.x, p.y, p.z, Width(u));
            }
        }
    }
}
