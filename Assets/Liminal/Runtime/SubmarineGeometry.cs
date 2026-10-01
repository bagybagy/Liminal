using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    internal static class SubmarineGeometry
    {
        public const int ParticleCount = 82000;
        public const int SubmarineTargets = 24;
        public const int FleetTargets = 24;
        public const int GiantTargets = 32;

        public static Vector3 RoomCenter => new(240f, -1020f, 3370f);

        public static Mesh Build()
        {
            const int count = ParticleCount;
            var vertices = new List<Vector3>(count * 4);
            var colors = new List<Color>(count * 4);
            var corners = new List<Vector4>(count * 4);
            var identity = new List<Vector4>(count * 4);
            var fleet = new List<Vector3>(count * 4);
            var giant = new List<Vector3>(count * 4);
            var reef = new List<Vector3>(count * 4);
            var indices = new List<int>(count * 6);

            for (int i = 0; i < count; i++)
            {
                float seed = Hash(i, 17);
                float accent;
                int joint;
                Vector3 source = SampleSubmarine(i, seed, 72f, 10f, out accent);
                Vector3 fleetPoint = SampleFleet(i, seed, out accent);
                Vector3 giantPoint = SampleGiant(i, seed, out joint, out accent);
                Vector3 reefPoint = SampleReef(i, seed, out accent);
                float size = 0.045f + Hash(i, 61) * 0.095f;
                float brightness = 0.42f + Hash(i, 67) * 0.76f;
                var color = new Color(brightness, brightness, brightness, 1f);
                int start = vertices.Count;

                for (int corner = 0; corner < 4; corner++)
                {
                    float x = corner == 0 || corner == 1 ? -1f : 1f;
                    float y = corner == 0 || corner == 3 ? -1f : 1f;
                    vertices.Add(source);
                    colors.Add(color);
                    corners.Add(new Vector4(x, y, size, 0f));
                    identity.Add(new Vector4(seed, accent, joint, Hash(i, 71)));
                    fleet.Add(fleetPoint);
                    giant.Add(giantPoint);
                    reef.Add(reefPoint);
                }

                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }

            var mesh = new Mesh { name = "Scarlet Engine / persistent matter", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, identity);
            mesh.SetUVs(2, fleet);
            mesh.SetUVs(3, giant);
            mesh.SetUVs(4, reef);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1400f);
            mesh.UploadMeshData(true);
            return mesh;
        }

        public static Vector3 SubmarineTarget(int index)
        {
            if (index == 0) return new Vector3(0f, 20f, 7f);
            if (index == 1) return new Vector3(0f, 26f, 12f);
            if (index < 10)
            {
                int point = index - 2;
                float z = -48f + point * 13.5f;
                float angle = (point % 4) * (Mathf.PI * 0.5f) + 0.28f;
                return HullSurface(z, angle, 72f, 10f);
            }
            if (index < 18)
            {
                int point = index - 10;
                int side = point % 2 == 0 ? -1 : 1;
                int tube = point / 2;
                return new Vector3(side * 13.1f, -1.8f + (tube % 2) * 0.35f, -39f + tube * 20f);
            }
            if (index < 22)
            {
                int point = index - 18;
                int side = point % 2 == 0 ? -1 : 1;
                return new Vector3(side * 23f, point < 2 ? -0.6f : 5.7f, point < 2 ? -48f : -63f);
            }
            float engineAngle = (index - 22) * Mathf.PI;
            return new Vector3(Mathf.Cos(engineAngle) * 5f, Mathf.Sin(engineAngle) * 3.4f, -70f);
        }

        public static Vector3 FleetTarget(int craft, int point)
        {
            Vector3 local;
            switch (point)
            {
                case 0: local = HullSurface(22f, Mathf.PI * 0.5f, 28f, 4.4f); break;
                case 1: local = HullSurface(8f, 0f, 28f, 4.4f); break;
                case 2: local = HullSurface(-9f, Mathf.PI, 28f, 4.4f); break;
                case 3: local = new Vector3(-5.9f, -0.8f, 1f); break;
                case 4: local = new Vector3(5.9f, -0.8f, 1f); break;
                case 5: local = new Vector3(0f, 8.1f, 2.2f); break;
                case 6: local = new Vector3(-1.7f, 0f, -27.2f); break;
                default: local = new Vector3(1.7f, 0f, -27.2f); break;
            }
            return FleetLocal(craft, local);
        }

        public static Vector3 FleetLocal(int craft, Vector3 local)
        {
            float angle = craft * Mathf.PI * 2f / 3f;
            Vector3 center = new Vector3(Mathf.Cos(angle) * 72f, Mathf.Sin(angle * 2f) * 5f,
                Mathf.Sin(angle) * 72f);
            return center + RotateY(local, angle + Mathf.PI * 0.5f);
        }

        public static Vector3 GiantTarget(int index, out int joint)
        {
            joint = 0;
            if (index < 4)
            {
                switch (index)
                {
                    case 0: return new Vector3(0f, -127f, 0f);
                    case 1: return new Vector3(-7.4f, -139f, 5.7f);
                    case 2: return new Vector3(7.4f, -139f, 5.7f);
                    default: return new Vector3(0f, -151f, 7.8f);
                }
            }
            if (index < 12)
            {
                int point = index - 4;
                float angle = point * Mathf.PI * 0.25f;
                return new Vector3(Mathf.Cos(angle) * 19f, -204f + Mathf.Sin(angle * 2f) * 12f,
                    Mathf.Sin(angle) * 12.5f);
            }
            if (index < 16)
            {
                int point = index - 12;
                return new Vector3(point % 2 == 0 ? -13f : 13f, -245f + (point / 2) * 8f,
                    point < 2 ? 7f : -7f);
            }
            if (index < 20)
            {
                int point = index - 16;
                bool left = point < 2;
                joint = left ? 1 : 3;
                float side = left ? -1f : 1f;
                float t = point % 2 == 0 ? 0.34f : 0.78f;
                return Vector3.Lerp(new Vector3(side * 22f, -183f, 0f),
                    new Vector3(side * 35f, -209f, 0f), t) + Vector3.forward * (point % 2 == 0 ? 5.3f : -5.3f);
            }
            if (index < 26)
            {
                int point = index - 20;
                bool left = point < 3;
                joint = left ? 2 : 4;
                float side = left ? -1f : 1f;
                float t = point % 3 == 0 ? 0.18f : point % 3 == 1 ? 0.56f : 0.91f;
                return Vector3.Lerp(new Vector3(side * 35f, -209f, 0f),
                    new Vector3(side * 49f, -247f, 0f), t) + Vector3.forward * ((point % 3 - 1) * 5f);
            }
            {
                int point = index - 26;
                bool left = point < 3;
                joint = left ? 2 : 4;
                float side = left ? -1f : 1f;
                int finger = point % 3;
                return new Vector3(side * (49f + finger * 1.5f), -258f - (finger == 1 ? 5f : 0f),
                    (finger - 1) * 3.4f);
            }
        }

        public static Vector3 PoseGiant(Vector3 point, int joint, float beat)
        {
            if (joint == 0) return point;
            bool left = joint == 1 || joint == 2;
            float side = left ? -1f : 1f;
            float phase = beat * 0.47f + (left ? 0f : Mathf.PI);
            float shoulderAngle = 0.15f + Mathf.Sin(phase) * 0.19f;
            Vector3 shoulder = new Vector3(side * 22f, -183f, 0f);
            Vector3 posed = shoulder + RotateZ(point - shoulder, shoulderAngle);
            if (joint == 2 || joint == 4)
            {
                Vector3 elbow = new Vector3(side * 35f, -209f, 0f);
                Vector3 posedElbow = shoulder + RotateZ(elbow - shoulder, shoulderAngle);
                float bend = 0.18f + Mathf.Sin(phase + 1.1f) * 0.12f;
                posed = posedElbow + RotateZ(posed - posedElbow, side * bend);
            }
            return posed;
        }

        static Vector3 SampleSubmarine(int index, float seed, float halfLength, float radius, out float accent)
        {
            float kind = Hash(index, 23);
            float a = Hash(index, 29);
            float b = Hash(index, 31);
            float c = Hash(index, 37);
            float d = Hash(index, 41);
            float angle = a * Mathf.PI * 2f;
            accent = 0.2f + seed * 0.48f;

            if (kind < 0.49f)
            {
                float z = (b * 2f - 1f) * halfLength * 0.985f;
                float profile = HullProfile(z, halfLength);
                float shell = 0.88f + c * 0.2f;
                accent = 0.18f + seed * 0.42f;
                return new Vector3(Mathf.Cos(angle) * radius * 0.88f * profile * shell,
                    Mathf.Sin(angle) * radius * 0.72f * profile * shell, z);
            }
            if (kind < 0.58f)
            {
                accent = 0.36f + seed * 0.26f;
                float scale = radius / 10f;
                Vector3 direction = SphereDirection(a, b, c);
                if (d < 0.16f)
                {
                    float t = c;
                    return new Vector3((a - 0.5f) * 0.75f * scale,
                        radius * (1.2f + t * 1.2f), halfLength * (0.045f + b * 0.08f));
                }
                return new Vector3(direction.x * radius * 0.38f,
                    radius * (1.02f + direction.y * 0.74f),
                    halfLength * 0.06f + direction.z * halfLength * 0.105f);
            }
            if (kind < 0.69f)
            {
                int ribCount = Mathf.Max(8, Mathf.RoundToInt(halfLength * 0.19f));
                int rib = Mathf.FloorToInt(b * ribCount);
                float z = Mathf.Lerp(-halfLength * 0.84f, halfLength * 0.84f, rib / (float)(ribCount - 1));
                float profile = HullProfile(z, halfLength) * 1.045f;
                accent = 0.76f + seed * 0.22f;
                return new Vector3(Mathf.Cos(angle) * radius * 0.88f * profile,
                    Mathf.Sin(angle) * radius * 0.72f * profile, z + (c - 0.5f) * 0.8f * radius * 0.055f);
            }
            if (kind < 0.8f)
            {
                float side = b < 0.5f ? -1f : 1f;
                int tube = Mathf.FloorToInt(c * 4f);
                float z = -halfLength * 0.52f + tube * halfLength * 0.22f + (d - 0.5f) * radius * 0.42f;
                float radial = radius * (0.99f + Mathf.Abs(Mathf.Cos(angle)) * 0.28f);
                accent = 0.62f + seed * 0.3f;
                return new Vector3(side * radial, -radius * 0.2f + Mathf.Sin(angle) * radius * 0.15f,
                    z + Mathf.Cos(angle) * radius * 0.13f);
            }
            if (kind < 0.9f)
            {
                float side = b < 0.5f ? -1f : 1f;
                bool vertical = c < 0.22f;
                float span = radius * (1.25f + d * 1.65f);
                accent = 0.28f + seed * 0.32f;
                if (vertical)
                    return new Vector3(side * radius * (0.42f + a * 0.16f),
                        (a - 0.5f) * radius * 1.7f, -halfLength * (0.76f + d * 0.17f));
                return new Vector3(side * span, (a - 0.5f) * radius * 0.18f,
                    -halfLength * 0.59f - (span / radius - 1.2f) * halfLength * 0.055f);
            }
            if (kind < 0.965f)
            {
                int engine = Mathf.FloorToInt(b * 4f);
                float engineAngle = angle;
                float offsetX = engine % 2 == 0 ? -radius * 0.48f : radius * 0.48f;
                float offsetY = engine < 2 ? -radius * 0.27f : radius * 0.27f;
                float tubeRadius = radius * (0.18f + (engine % 2) * 0.035f);
                accent = 0.84f + seed * 0.16f;
                return new Vector3(offsetX + Mathf.Cos(engineAngle) * tubeRadius,
                    offsetY + Mathf.Sin(engineAngle) * tubeRadius, -halfLength * (0.93f + (c - 0.5f) * 0.09f));
            }

            accent = 0.5f + seed * 0.5f;
            float line = (b * 2f - 1f) * halfLength * 0.92f;
            float lineAngle = angle * 0.5f;
            float lineRadius = HullProfile(line, halfLength) * radius;
            return new Vector3(Mathf.Cos(lineAngle) * lineRadius,
                Mathf.Sin(lineAngle) * lineRadius * 0.76f, line);
        }

        static Vector3 SampleFleet(int index, float seed, out float accent)
        {
            int craft = index % 3;
            Vector3 point = SampleSubmarine(index, seed, 28f, 4.4f, out accent);
            return FleetLocal(craft, point);
        }

        static Vector3 SampleGiant(int index, float seed, out int joint, out float accent)
        {
            float kind = Hash(index, 47);
            float a = Hash(index, 53);
            float b = Hash(index, 59);
            float c = Hash(index, 61);
            float d = Hash(index, 67);
            float angle = a * Mathf.PI * 2f;
            float radial = 0.84f + b * 0.2f;
            joint = 0;
            accent = 0.22f + seed * 0.4f;

            if (kind < 0.29f)
            {
                accent = 0.2f + seed * 0.35f;
                return EllipsoidPoint(new Vector3(0f, -204f, 0f), new Vector3(20f, 22f, 13f), angle, b, radial);
            }
            if (kind < 0.4f)
            {
                accent = 0.24f + seed * 0.32f;
                return EllipsoidPoint(new Vector3(0f, -245f, 0f), new Vector3(15f, 12f, 10f), angle, b, radial);
            }
            if (kind < 0.5f)
            {
                accent = 0.34f + seed * 0.28f;
                return EllipsoidPoint(new Vector3(0f, -139f, 0f), new Vector3(9f, 13f, 8f), angle, b, radial);
            }
            if (kind < 0.56f)
            {
                accent = 0.68f + seed * 0.28f;
                if (a < 0.35f)
                    return new Vector3((b - 0.5f) * 1.4f, -164f + c * 64f, (d - 0.5f) * 1.5f);
                if (a < 0.58f)
                    return new Vector3(b < 0.5f ? -5.4f : 5.4f, -139f + (c - 0.5f) * 1.6f, 7.6f);
                float ringY = -202f + b * 33f;
                float ringRadius = 15f + c * 7f;
                return new Vector3(Mathf.Cos(angle) * ringRadius, ringY, Mathf.Sin(angle) * 10f);
            }
            if (kind < 0.66f)
            {
                bool left = b < 0.5f;
                float side = left ? -1f : 1f;
                joint = left ? 1 : 3;
                Vector3 start = new Vector3(side * 22f, -183f, 0f);
                Vector3 end = new Vector3(side * 35f, -209f, 0f);
                accent = 0.32f + seed * 0.34f;
                return SegmentSurface(start, end, 5.8f, c, angle, radial);
            }
            if (kind < 0.82f)
            {
                bool left = b < 0.5f;
                float side = left ? -1f : 1f;
                joint = left ? 2 : 4;
                Vector3 start = new Vector3(side * 35f, -209f, 0f);
                Vector3 end = new Vector3(side * 49f, -247f, 0f);
                accent = 0.56f + seed * 0.36f;
                return SegmentSurface(start, end, 5.2f + (1f - c) * 1.7f, d, angle, radial);
            }
            if (kind < 0.9f)
            {
                bool left = b < 0.5f;
                float side = left ? -1f : 1f;
                joint = left ? 2 : 4;
                int finger = Mathf.FloorToInt(c * 4f);
                Vector3 start = new Vector3(side * (49f + finger * 1.2f), -249f, (finger - 1.5f) * 2.4f);
                Vector3 end = new Vector3(side * (50f + finger * 2.2f), -278f + (finger == 2 ? 4f : 0f),
                    (finger - 1.5f) * 4.3f);
                accent = 0.82f + seed * 0.17f;
                return SegmentSurface(start, end, 1.05f + Hash(index, 73) * 0.75f, d, angle, radial);
            }
            if (kind < 0.96f)
            {
                accent = 0.9f + seed * 0.1f;
                if (a < 0.42f)
                {
                    bool left = b < 0.5f;
                    float side = left ? -1f : 1f;
                    joint = left ? 2 : 4;
                    float band = 0.16f + Mathf.Floor(c * 4f) * 0.22f;
                    return SegmentSurface(new Vector3(side * 35f, -209f, 0f),
                        new Vector3(side * 49f, -247f, 0f), 6.7f, band, angle, 1.03f);
                }
                int ring = Mathf.FloorToInt(c * 5f);
                float y = -195f + ring * 6.3f;
                float radius = 15f + Mathf.Sin((y + 204f) * 0.08f) * 3.5f;
                return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * (radius * 0.65f));
            }

            accent = 0.72f + seed * 0.28f;
            float cable = (a - 0.5f) * 36f;
            return new Vector3(Mathf.Cos(angle) * (12f + b * 10f), -174f - c * 86f,
                cable + Mathf.Sin(angle) * 4f);
        }

        static Vector3 SampleReef(int index, float seed, out float accent)
        {
            float kind = Hash(index, 79);
            float a = Hash(index, 83) * Mathf.PI * 2f;
            float b = Hash(index, 89);
            float c = Hash(index, 97);
            float d = Hash(index, 101);
            accent = 0.54f + seed * 0.38f;

            if (kind < 0.31f)
            {
                float angle = a;
                float minor = (b - 0.5f) * 6f;
                accent = 0.58f + seed * 0.35f;
                return new Vector3(Mathf.Cos(angle) * (36f + minor), -251f + (c - 0.5f) * 5f,
                    Mathf.Sin(angle) * (36f + minor));
            }
            if (kind < 0.52f)
            {
                float angle = a;
                float radius = 21f + (b - 0.5f) * 4f;
                accent = 0.78f + seed * 0.2f;
                return new Vector3(Mathf.Cos(angle) * radius, -218f + (c - 0.5f) * 4f,
                    Mathf.Sin(angle) * radius);
            }
            if (kind < 0.79f)
            {
                float t = b;
                float horizontal = (t * 2f - 1f) * (27f + c * 8f);
                float plane = Mathf.Cos(a) * horizontal;
                float depth = Mathf.Sin(a) * horizontal;
                accent = 0.38f + seed * 0.38f;
                return new Vector3(plane, -252f + Mathf.Sin(t * Mathf.PI) * (43f + d * 15f), depth);
            }
            if (kind < 0.93f)
            {
                float height = b * 66f;
                float radius = 31f + Mathf.Sin(height * 0.055f + a * 3f) * 2.5f;
                accent = 0.7f + seed * 0.28f;
                return new Vector3(Mathf.Cos(a) * radius + Mathf.Cos(a + 1.4f) * c * 4f,
                    -252f + height, Mathf.Sin(a) * radius + Mathf.Sin(a + 1.4f) * c * 4f);
            }

            accent = 0.84f + seed * 0.16f;
            float engineAngle = a;
            float engineRadius = 8f + c * 18f;
            return new Vector3(Mathf.Cos(engineAngle) * engineRadius, -242f + (b - 0.5f) * 18f,
                Mathf.Sin(engineAngle) * engineRadius);
        }

        static Vector3 HullSurface(float z, float angle, float halfLength, float radius)
        {
            float profile = HullProfile(z, halfLength);
            return new Vector3(Mathf.Cos(angle) * radius * 0.88f * profile,
                Mathf.Sin(angle) * radius * 0.72f * profile, z);
        }

        static float HullProfile(float z, float halfLength)
        {
            float t = Mathf.Clamp01(1f - Mathf.Abs(z) / halfLength);
            return Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.58f);
        }

        static Vector3 EllipsoidPoint(Vector3 center, Vector3 radii, float angle, float vertical, float scale)
        {
            float y = vertical * 2f - 1f;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)) * scale;
            return center + new Vector3(Mathf.Cos(angle) * radii.x * radial,
                y * radii.y * scale, Mathf.Sin(angle) * radii.z * radial);
        }

        static Vector3 SegmentSurface(Vector3 start, Vector3 end, float radius, float t, float angle, float scale)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.forward);
            if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(axis, Vector3.up);
            side.Normalize();
            Vector3 other = Vector3.Cross(axis, side).normalized;
            float taper = 0.86f + Mathf.Sin(t * Mathf.PI) * 0.22f;
            Vector3 offset = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
            return Vector3.Lerp(start, end, t) + offset * radius * taper * scale;
        }

        static Vector3 SphereDirection(float a, float b, float c)
        {
            float y = b * 2f - 1f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = a * Mathf.PI * 2f;
            return new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle)) * (0.8f + c * 0.2f);
        }

        static Vector3 RotateY(Vector3 point, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector3(c * point.x + s * point.z, point.y, -s * point.x + c * point.z);
        }

        static Vector3 RotateZ(Vector3 point, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector3(c * point.x - s * point.y, s * point.x + c * point.y, point.z);
        }

        static float Hash(int value, int salt)
        {
            uint x = (uint)value * 747796405u + (uint)salt * 2891336453u + 277803737u;
            x = ((x >> ((int)(x >> 28) + 4)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x & 0x00ffffffu) / 16777216f;
        }
    }
}
