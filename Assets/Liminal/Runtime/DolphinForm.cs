using UnityEngine;

namespace Liminal
{
    public static class DolphinForm
    {
        static float Hash(int i)
        {
            unchecked
            {
                uint h = (uint)i;
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xffffffu) / 16777216f;
            }
        }

        public static Vector4 Sample(int i, int count)
        {
            float u = (i + 0.5f) / Mathf.Max(1, count), v = Hash(i + 713), a = i * 2.399963f;
            Vector3 p;
            if (u < 0.69f)
            {
                float z = Mathf.Lerp(-9.2f, 9.5f, u / 0.69f);
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-10f, -1f, z));
                float head = Mathf.Sqrt(Mathf.Clamp01((10f - z) / 5f));
                float r = 1.95f * taper * head;
                p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.78f, z);
                if (z > 6f) p.y -= (z - 6f) * 0.09f;
            }
            else if (u < 0.73f)
            {
                float t = (u - 0.69f) / 0.04f;
                float r = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.35f) * 0.38f;
                p = new Vector3(Mathf.Cos(a) * r, -0.75f + Mathf.Sin(a) * r * 0.6f, 7.8f + t * 5f);
            }
            else if (u < 0.85f)
            {
                float t = (u - 0.73f) / 0.12f, side = (i & 1) == 0 ? 1f : -1f;
                float chord = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f)) * 1.5f;
                p = new Vector3(side * (1.3f + t * 3.6f), -0.7f - t * 0.65f + Mathf.Sin(v * Mathf.PI) * 0.12f,
                    1.4f - t * 3.3f + (v - 0.5f) * chord * 2f);
            }
            else if (u < 0.97f)
            {
                float t = (u - 0.85f) / 0.12f, side = (i & 1) == 0 ? 1f : -1f;
                p = new Vector3(side * t * 4.1f, Mathf.Sin(t * Mathf.PI) * 0.36f,
                    -9.1f + t * 1.9f - Mathf.Sin(t * Mathf.PI) * v * 2.8f);
            }
            else
            {
                float t = (u - 0.97f) / 0.03f;
                p = new Vector3((v - 0.5f) * (1f - t) * 0.32f, 1.5f + t * 1.9f,
                    -1.5f - t * 1.4f + (v - 0.5f) * (1f - t) * 3f);
            }
            return new Vector4(p.x, p.y, p.z, 0.055f);
        }

        public static Vector3 Deform(Vector3 point, float song)
        {
            float tail = Mathf.Clamp01((-point.z - 2f) / 7.5f);
            point.y += Mathf.Sin(song * 2.2f + point.z * 0.22f) * tail * 0.82f;
            point.x += Mathf.Sin(song * 1.1f + point.z * 0.18f) * tail * 0.22f;

            float finSpan = Mathf.Clamp01((Mathf.Abs(point.x) - 0.75f) / 3.75f);
            float finRoot = Mathf.Clamp01((point.z + 2.5f) / 2.5f) *
                Mathf.Clamp01((4.8f - point.z) / 2f);
            float fin = finSpan * finRoot;
            float finPhase = song * 1.35f + point.z * 0.12f;
            point.y += Mathf.Sin(finPhase) * fin * 0.36f;
            point.z += Mathf.Cos(finPhase) * fin * 0.10f;
            return point;
        }
    }
}
