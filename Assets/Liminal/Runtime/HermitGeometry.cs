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

            return BuildMesh("Hermit crab matter", vertices, colors, uv0, uv1, uv2, triangles,
                new Bounds(Vector3.zero, Vector3.one * 1200f));
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
                float angle = (index + 0.5f) * (Mathf.PI * 0.25f);
                float x = Mathf.Cos(angle) * 22.5f;
                float z = -5f + Mathf.Sin(angle) * 14.5f;
                float q = (x * x) / (27f * 27f) + ((z + 5f) * (z + 5f)) / (18f * 18f);
                float y = 1.6f + 22f * Mathf.Sqrt(Mathf.Max(0f, 1f - q));
                return new Vector3(x, y + 0.65f, z);
            }

            if (index < 12)
            {
                int claw = index - 8;
                float side = (claw & 1) == 0 ? -1f : 1f;
                float flex = Mathf.Sin((beat + claw * 0.25f) * Mathf.PI) * 0.65f;
                return new Vector3(side * (30.5f + (claw >= 2 ? 1.1f : 0f)), 4.2f + flex,
                    19f + (claw >= 2 ? 3.2f : 0f) + flex * 0.55f);
            }

            int leg = index - 12;
            float legSide = (leg & 1) == 0 ? -1f : 1f;
            float fore = leg < 2 ? 1f : -1f;
            float sway = Mathf.Sin((beat + leg * 0.25f) * Mathf.PI) * 0.35f;
            return new Vector3(legSide * 23.5f, 2.7f + Mathf.Abs(sway), fore * 12f + sway);
        }

        public static Vector3 BossClawOrigin(int side, float beat)
        {
            float flex = Mathf.Sin((beat + (side > 0 ? 0.25f : 0f)) * Mathf.PI) * 0.6f;
            return new Vector3(side * 31f, 4.1f + flex, 19f + flex * 0.5f);
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
                float radial = Mathf.Sqrt((index + r1) / 1720f);
                float angle = index * 2.39996323f + r2 * 0.12f;
                position = new Vector3(Mathf.Cos(angle) * 4.05f * radial,
                    0.52f + 4.35f * Mathf.Sqrt(Mathf.Max(0f, 1f - radial * radial)),
                    -0.2f + Mathf.Sin(angle) * 3.05f * radial);
                bool rim = radial > 0.9f;
                color = rim && r3 < 0.22f
                    ? new Color(0.72f, 0.44f, 0.13f)
                    : Color.Lerp(new Color(0.025f, 0.31f, 0.35f), new Color(0.06f, 0.52f, 0.42f), radial * 0.62f);
                size = 0.035f + r0 * 0.035f;
                part = 0f;
            }
            else if (index < 1848)
            {
                float t = (index - 1720 + r1) / 128f;
                float angle = t * Mathf.PI * 6f;
                float radius = 0.12f + 1.62f * t;
                float x = 0.32f + Mathf.Cos(angle) * radius;
                float z = -0.2f + Mathf.Sin(angle) * radius * 0.72f;
                float q = (x * x) / (4.05f * 4.05f) + (z * z) / (3.05f * 3.05f);
                position = new Vector3(x, 0.58f + 4.35f * Mathf.Sqrt(Mathf.Max(0f, 1f - q)), z);
                color = Color.Lerp(new Color(0.7f, 0.38f, 0.1f), new Color(1f, 0.72f, 0.25f), r2 * 0.65f);
                size = 0.055f + r3 * 0.04f;
                part = 1f;
            }
            else if (index < 2808)
            {
                int legIndex = (index - 1848) / 160;
                float along = ((index - 1848) % 160 + r1) / 160f;
                float side = legIndex < 3 ? -1f : 1f;
                int foreIndex = legIndex % 3;
                float zBase = foreIndex == 0 ? 1.75f : foreIndex == 1 ? 0f : -1.75f;
                Vector3 root = new Vector3(side * 1.4f, 1.02f, zBase * 0.72f);
                Vector3 knee = new Vector3(side * 2.85f, 0.58f, zBase + (foreIndex == 0 ? 0.52f : foreIndex == 2 ? -0.48f : 0f));
                Vector3 foot = new Vector3(side * 4.55f, 0.13f, zBase + (foreIndex == 0 ? 1f : foreIndex == 2 ? -0.92f : 0f));
                position = along < 0.56f
                    ? Vector3.Lerp(root, knee, along / 0.56f)
                    : Vector3.Lerp(knee, foot, (along - 0.56f) / 0.44f);
                position += new Vector3((r2 - 0.5f) * 0.1f, (r3 - 0.5f) * 0.08f, 0f);
                color = Color.Lerp(new Color(0.04f, 0.4f, 0.39f), new Color(0.08f, 0.66f, 0.62f), r0 * 0.7f);
                size = 0.035f + r3 * 0.032f;
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
