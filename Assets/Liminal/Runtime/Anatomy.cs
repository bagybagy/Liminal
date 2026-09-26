using UnityEngine;

namespace Liminal
{
    public static class Anatomy
    {
        public static Vector3 Center(float u, float song)
        {
            float a = (u * 1.48f - 0.55f) * Mathf.PI + Mathf.Sin(song * 0.17f) * 0.22f;
            float evolution=Mathf.SmoothStep(0,1,Mathf.InverseLerp(104,164,song));
            return new Vector3(Mathf.Sin(a) * 26,
                Mathf.Cos(a * 1.8f + song * 0.27f) * (6+evolution*2) + Mathf.Sin(u * 16 - song * 0.65f) * 1.1f + evolution*2,
                34 + Mathf.Cos(a) * 9 + Mathf.Sin(u * 10 + song * 0.3f) * (3+evolution*3));
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
            return c + up * Width(u) * 0.42f - side * Width(u) * 0.92f;
        }
    }
}
