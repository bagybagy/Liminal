using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    internal static class HermitGeometry
    {
        public const int SwarmSize = 24;
        public const int MergeSourceCount = 8;
        public const int ParticlesPerCrab = 3200;
        public const int BossPointCount = 16;
        public const float GiantScale = 11.25f;
        public const float ReefScale = 2.5f;
        public const int ReefFishCount = 16;
        const int ReefFishPoints = 32;
        const int MarkerParticleCount = 64;

        static readonly Vector2[] Corners = {
            new Vector2(-1f, -1f), new Vector2(-1f, 1f),
            new Vector2(1f, 1f), new Vector2(1f, -1f)
        };

        public static Mesh BuildSwarmMesh()
        {
            var vertices = new List<Vector3>(ParticlesPerCrab * 4);
            var colors = new List<Color>(ParticlesPerCrab * 4);
            var uv0 = new List<Vector4>(ParticlesPerCrab * 4);
            var uv1 = new List<Vector4>(ParticlesPerCrab * 4);
            var uv2 = new List<Vector4>(ParticlesPerCrab * 4);
            var triangles = new List<int>(ParticlesPerCrab * 6);
            var random = new System.Random(724193);

            for (int i = 0; i < ParticlesPerCrab; i++)
            {
                SampleCrabPoint(i, random, out Vector3 position, out float size, out Color color,
                    out float part, out float seed, out float leg, out float legT);
                AddPoint(vertices, colors, uv0, uv1, uv2, triangles, position, size, color,
                    new Vector4(i, part, seed, 0f), new Vector4(leg, legT, 0f, 0f));
            }

            // Refuge points are GPU-positioned around one shared root, far from some crab transforms.
            return BuildMesh("Hermit crab matter", vertices, colors, uv0, uv1, uv2, triangles,
                new Bounds(Vector3.zero, Vector3.one * 1400f));
        }

        public static Mesh BuildReefShoalMesh()
        {
            int particleCount = ReefFishCount * ReefFishPoints;
            var vertices = new List<Vector3>(particleCount * 4);
            var colors = new List<Color>(particleCount * 4);
            var uv0 = new List<Vector4>(particleCount * 4);
            var uv1 = new List<Vector4>(particleCount * 4);
            var uv2 = new List<Vector4>(particleCount * 4);
            var triangles = new List<int>(particleCount * 6);
            var random = new System.Random(613249);
            Color[] palette = {
                new Color(0.1f, 0.88f, 0.82f), new Color(0.23f, 0.68f, 1f),
                new Color(1f, 0.68f, 0.31f), new Color(0.72f, 0.94f, 0.48f)
            };

            for (int fish = 0; fish < ReefFishCount; fish++)
            {
                Color body = palette[fish % palette.Length];
                for (int point = 0; point < ReefFishPoints; point++)
                {
                    float r0 = (float)random.NextDouble();
                    float r1 = (float)random.NextDouble();
                    float r2 = (float)random.NextDouble();
                    float r3 = (float)random.NextDouble();
                    Vector3 position;
                    Color color;
                    float size;
                    if (point < 24)
                    {
                        position = Vector3.Scale(RandomSphere(r1, r2, r3), new Vector3(0.55f, 0.32f, 0.92f));
                        color = Color.Lerp(body, Color.white, r0 * 0.24f);
                        size = 0.12f + r0 * 0.07f;
                    }
                    else if (point < 30)
                    {
                        int tailPoint = point - 24;
                        float side = tailPoint < 3 ? -1f : 1f;
                        float t = (tailPoint % 3 + r1) / 3f;
                        position = new Vector3(side * t * 0.45f, side * t * 0.12f, -0.78f - t * 0.62f);
                        color = Color.Lerp(body, new Color(1f, 0.76f, 0.39f), 0.38f);
                        size = 0.13f + r0 * 0.05f;
                    }
                    else
                    {
                        float side = point == 30 ? -1f : 1f;
                        position = new Vector3(side * 0.2f, 0.11f, 0.62f);
                        color = new Color(1f, 0.9f, 0.63f);
                        size = 0.13f;
                    }

                    AddPoint(vertices, colors, uv0, uv1, uv2, triangles, position, size, color,
                        new Vector4(point, fish, r0, 0f), Vector4.zero);
                }
            }

            return BuildMesh("Reef shoaling fish", vertices, colors, uv0, uv1, uv2, triangles,
                new Bounds(Vector3.zero, new Vector3(200f, 100f, 200f)));
        }

        public static Mesh BuildMarkerMesh()
        {
            var vertices = new List<Vector3>(MarkerParticleCount * 4);
            var colors = new List<Color>(MarkerParticleCount * 4);
            var uv0 = new List<Vector4>(MarkerParticleCount * 4);
            var uv1 = new List<Vector4>(MarkerParticleCount * 4);
            var uv2 = new List<Vector4>(MarkerParticleCount * 4);
            var triangles = new List<int>(MarkerParticleCount * 6);

            for (int i = 0; i < MarkerParticleCount; i++)
            {
                float a = i * 2.39996323f;
                float radius = i < 48 ? 0.82f + (i % 4) * 0.035f : 0.14f + (i % 4) * 0.13f;
                Vector3 position = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius,
                    (i % 7 - 3) * 0.035f);
                Color color = i % 5 == 0
                    ? new Color(0.12f, 0.82f, 0.69f)
                    : new Color(1f, 0.61f, 0.2f);
                AddPoint(vertices, colors, uv0, uv1, uv2, triangles, position,
                    i < 48 ? 0.09f : 0.13f, color,
                    new Vector4(i, 0f, i / (float)MarkerParticleCount, 0f), Vector4.zero);
            }

            return BuildMesh("Hermit lock constellation", vertices, colors, uv0, uv1, uv2, triangles,
                new Bounds(Vector3.zero, Vector3.one * 4f));
        }

        public static float FloorHeight(Vector3 center, Vector3 radii, float x, float z, float clearance)
        {
            float nx = (x - center.x) / radii.x;
            float nz = (z - center.z) / radii.z;
            float profile = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - nz * nz));
            return center.y - radii.y * profile + clearance;
        }

        public static Vector3 BossTargetLocalPosition(int index, float beat)
        {
            if (index < 8)
            {
                return ShellSurface(0.32f + index * 0.095f, (index & 1) == 0 ? 0.45f : 1.15f) * GiantScale;
            }
            if (index < 12)
            {
                int claw = index - 8;
                float side = (claw & 1) == 0 ? -1f : 1f;
                float scale = side > 0f ? 1.23f : 0.82f;
                Vector3 local = claw < 2
                    ? new Vector3(side * 3.72f, 0.9f, 2.42f)
                    : new Vector3(side * (3.72f + 0.32f * scale), 1.18f, 2.8f + 0.7f * scale);
                return (local + ClawFlex(side, beat)) * GiantScale;
            }
            int leg = index == 12 ? 0 : index == 13 ? 3 : index == 14 ? 2 : 5;
            return LegSurface(leg, 0.52f, GiantFoot(leg, beat)) * GiantScale;
        }

        public static Vector3 SmallTargetLocalPosition(int index)
        {
            switch (index)
            {
                case 0: return ShellSurface(0.88f, 0f);
                case 1: return ShellSurface(0.88f, Mathf.PI * 0.5f);
                case 2: return ShellSurface(0.88f, Mathf.PI);
                default: return ShellSurface(0.88f, Mathf.PI * 1.5f);
            }
        }

        public static Vector3 BossClawOrigin(int side, float beat)
        {
            return (new Vector3(side * 3.72f, 0.94f, 3.5f) + ClawFlex(side, beat)) * GiantScale;
        }

        // Mirrored in HermitMatter: a growing tube swept around a descending helix,
        // not a spiral painted on a dome. t=1 is the open, forward-facing aperture.
        public static Vector3 ShellSurface(float t, float v, float extension = 0f)
        {
            float angle = -(1f - t) * Mathf.PI * 2f * 2.65f;
            float radius = 0.08f + 2.15f * Mathf.Pow(t, 1.25f);
            float tube = 0.12f + 1.28f * Mathf.Pow(t, 1.35f);
            float ridge = 0.10f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(v * 9f + t * 12f), 8f);
            tube += ridge + extension;
            float radial = radius + Mathf.Cos(v) * tube;
            return new Vector3(Mathf.Cos(angle) * radial,
                9.5f - 6.7f * t + Mathf.Sin(v) * tube * 1.4f,
                -1.35f + Mathf.Sin(angle) * radial);
        }

        public static Vector3 LegRoot(int leg)
        {
            float side = leg < 3 ? -1f : 1f;
            float fore = 1f - leg % 3;
            return new Vector3(side * 1.35f, 1.12f, fore * 1.25f);
        }

        public static Vector3 RestFoot(int leg)
        {
            float side = leg < 3 ? -1f : 1f;
            float fore = 1f - leg % 3;
            return new Vector3(side * (4.35f - Mathf.Abs(fore) * 0.2f), 0.08f, fore * 2.65f);
        }

        public static Vector3 LegSurface(int leg, float t, Vector3 foot)
        {
            Vector3 root = LegRoot(leg);
            Vector3 knee = Vector3.Lerp(root, foot, 0.52f);
            knee.y = 2.75f + Mathf.Max(0f, foot.y - 0.08f) * 0.42f;
            return t < 0.52f ? Vector3.Lerp(root, knee, t / 0.52f)
                : Vector3.Lerp(knee, foot, (t - 0.52f) / 0.48f);
        }

        public static Vector3 GiantFoot(int leg, float beat)
        {
            Vector3 foot = RestFoot(leg);
            float phase = Mathf.Repeat(beat + leg % 2 * 0.5f, 1f);
            if (phase >= 0.62f)
            {
                float swing = (phase - 0.62f) / 0.38f;
                foot.y += Mathf.Sin(swing * Mathf.PI) * 0.85f;
                foot.z += Mathf.Sin(swing * Mathf.PI * 2f) * 0.45f;
            }
            return foot;
        }

        public static Vector3 ClawFlex(float side, float beat)
        {
            float flex = Mathf.Sin((beat + (side > 0f ? 0.25f : 0f)) * Mathf.PI) * 0.12f;
            return new Vector3(0f, flex, flex * 0.5f);
        }

        static void SampleCrabPoint(int index, System.Random random, out Vector3 position, out float size,
            out Color color, out float part, out float seed, out float leg, out float legT)
        {
            float r0 = (float)random.NextDouble();
            float r1 = (float)random.NextDouble();
            float r2 = (float)random.NextDouble();
            float r3 = (float)random.NextDouble();
            seed = r0;
            leg = -1f;
            legT = 0f;

            if (index < 1720)
            {
                float t = Mathf.Pow((index + r1) / 1720f, 0.62f);
                float v = index * 2.39996323f;
                // Reserve dense rings for the raised aperture lip, with no cap.
                if (index >= 1560)
                    t = 0.985f + r1 * 0.015f;
                position = ShellSurface(t, v);
                leg = t;
                legT = v;
                color = Color.Lerp(new Color(0.08f, 0.36f, 0.35f), new Color(0.2f, 0.64f, 0.45f), t);
                if (index >= 1560)
                    color = new Color(0.88f, 0.65f, 0.31f);
                size = 0.065f + r0 * 0.035f;
                part = 0f;
            }
            else if (index < 1848)
            {
                int spine = (index - 1720) / 8;
                float t = 0.32f + spine * 0.042f;
                float v = 0.55f + (spine % 3) * 0.58f;
                float extension = ((index - 1720) % 8 + r1) / 8f * (0.3f + 0.65f * t);
                position = ShellSurface(t, v, extension);
                leg = t;
                legT = v;
                color = Color.Lerp(new Color(0.7f, 0.38f, 0.1f), new Color(1f, 0.72f, 0.25f), r2 * 0.65f);
                size = 0.055f + r3 * 0.04f;
                part = 1f;
            }
            else if (index < 2808)
            {
                int legIndex = (index - 1848) / 160;
                float along = ((index - 1848) % 160 + r1) / 160f;
                position = LegSurface(legIndex, along, RestFoot(legIndex));
                position += new Vector3((r2 - 0.5f) * 0.17f, (r3 - 0.5f) * 0.14f, 0f);
                color = Color.Lerp(new Color(0.04f, 0.4f, 0.39f), new Color(0.08f, 0.66f, 0.62f), r0 * 0.7f);
                size = 0.055f + r3 * 0.035f;
                part = 2f;
                leg = legIndex;
                legT = along;
            }
            else if (index < 3088)
            {
                int local = index - 2808;
                int sideIndex = local / 140;
                int clawPoint = local % 140;
                float side = sideIndex == 0 ? -1f : 1f;
                float clawScale = side > 0f ? 1.23f : 0.82f;
                if (clawPoint < 30)
                {
                    float t = (clawPoint + r1) / 30f;
                    position = Vector3.Lerp(new Vector3(side * 1.45f, 0.93f, 1.4f),
                        new Vector3(side * 3.5f, 0.86f, 2.3f), t);
                    position.y += Mathf.Sin(t * Mathf.PI) * 0.24f;
                }
                else if (clawPoint < 100)
                {
                    Vector3 sphere = RandomSphere(r1, r2, r3);
                    position = new Vector3(side * 3.72f, 0.9f, 2.42f) +
                        Vector3.Scale(sphere, new Vector3(0.48f, 0.48f, 0.55f) * clawScale);
                }
                else
                {
                    int finger = (clawPoint - 100) / 20;
                    float t = ((clawPoint - 100) % 20 + r1) / 20f;
                    float inner = finger == 0 ? -1f : 1f;
                    float x = side * (3.72f + (0.14f + t * 0.23f) * clawScale);
                    float y = 0.94f + inner * (0.18f + 0.1f * Mathf.Sin(t * Mathf.PI));
                    float z = 2.8f + t * 0.94f * clawScale;
                    position = new Vector3(x + (r2 - 0.5f) * 0.07f, y + (r3 - 0.5f) * 0.07f, z);
                }
                color = clawPoint % 9 == 0
                    ? new Color(0.93f, 0.62f, 0.19f)
                    : new Color(0.08f, 0.55f, 0.51f);
                size = 0.04f + r0 * 0.04f;
                part = 3f;
            }
            else if (index < 3168)
            {
                Vector3 sphere = RandomSphere(r1, r2, r3);
                position = new Vector3(sphere.x * 1.43f, 0.85f + sphere.y * 0.62f, 0.8f + sphere.z * 1.62f);
                color = new Color(0.025f, 0.29f, 0.34f);
                size = 0.035f + r0 * 0.035f;
                part = 4f;
            }
            else
            {
                int eyePoint = index - 3168;
                int eye = eyePoint / 16;
                int local = eyePoint % 16;
                float side = eye == 0 ? -1f : 1f;
                if (local < 8)
                {
                    float t = (local + r1) / 8f;
                    position = new Vector3(side * (0.62f + t * 0.08f), 1.15f + t * 1.12f, 2.1f + t * 0.55f);
                }
                else
                {
                    Vector3 sphere = RandomSphere(r1, r2, r3);
                    position = new Vector3(side * 0.71f, 2.29f, 2.66f) + sphere * 0.17f;
                }
                color = local >= 8 ? new Color(0.94f, 0.68f, 0.24f) : new Color(0.04f, 0.65f, 0.61f);
                size = 0.05f + r0 * 0.04f;
                part = 5f;
            }
        }

        static Vector3 RandomSphere(float yValue, float angleValue, float radiusValue)
        {
            float y = yValue * 2f - 1f;
            float angle = angleValue * Mathf.PI * 2f;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float radius = Mathf.Pow(radiusValue, 1f / 3f);
            return new Vector3(Mathf.Cos(angle) * radial * radius, y * radius, Mathf.Sin(angle) * radial * radius);
        }

        static void AddPoint(List<Vector3> vertices, List<Color> colors, List<Vector4> uv0,
            List<Vector4> uv1, List<Vector4> uv2, List<int> triangles, Vector3 center,
            float size, Color color, Vector4 data, Vector4 extra)
        {
            int start = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(center);
                colors.Add(color);
                uv0.Add(new Vector4(Corners[i].x, Corners[i].y, size, 0f));
                uv1.Add(data);
                uv2.Add(extra);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        static Mesh BuildMesh(string name, List<Vector3> vertices, List<Color> colors,
            List<Vector4> uv0, List<Vector4> uv1, List<Vector4> uv2, List<int> triangles, Bounds bounds)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
