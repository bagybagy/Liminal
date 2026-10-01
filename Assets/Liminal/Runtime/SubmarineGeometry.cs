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
        public static Vector3 SpearBase => new(65f, -282f, 5f);
        public static Vector3 SpearNeck => new(49f, -120f, 5f);
        public static Vector3 SpearTip => new(46.8f, -98f, 5f);

        static Vector3 Shoulder(float side) => new(side * 26f, -178f, 0f);
        static Vector3 Elbow(float side) => new(side * 43f, -204f, 0f);
        static Vector3 Hand(float side) => new(side < 0f ? -64f : 60f, side < 0f ? -250f : -231f, 5f);

        public static Mesh Build()
        {
            const int count = ParticleCount;
            var vertices = new List<Vector3>(count * 4);
            var colors = new List<Color>(count * 4);
            var corners = new List<Vector4>(count * 4);
            var identity = new List<Vector4>(count * 4);
            var fleet = new List<Vector4>(count * 4);
            var giant = new List<Vector3>(count * 4);
            var reef = new List<Vector4>(count * 4);
            var giantFlowCenters = new List<Vector4>(count * 4);
            var giantFlowTangents = new List<Vector4>(count * 4);
            var hullNormals = new List<Vector4>(count * 4);
            var indices = new List<int>(count * 6);

            for (int i = 0; i < count; i++)
            {
                float seed = Hash(i, 17);
                int joint;
                Vector3 source = SampleSubmarine(i, seed, out float submarineAccent, out Vector3 submarineNormal);
                Vector3 fleetPoint = SampleFleet(i, seed, out float fleetAccent, out Vector3 fleetNormal);
                Vector3 giantPoint = SampleGiant(i, seed, out joint, out _,
                    out Vector4 flowCenter, out Vector4 flowTangent);
                Vector3 reefPoint = SampleReef(i, seed, out float reefAccent);
                Vector2 submarineOct = EncodeNormal(submarineNormal);
                Vector2 fleetOct = EncodeNormal(fleetNormal);
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
                    identity.Add(new Vector4(seed, submarineAccent, joint, Hash(i, 71)));
                    fleet.Add(new Vector4(fleetPoint.x, fleetPoint.y, fleetPoint.z, fleetAccent));
                    giant.Add(giantPoint);
                    reef.Add(new Vector4(reefPoint.x, reefPoint.y, reefPoint.z, reefAccent));
                    giantFlowCenters.Add(flowCenter);
                    giantFlowTangents.Add(flowTangent);
                    hullNormals.Add(new Vector4(submarineOct.x, submarineOct.y, fleetOct.x, fleetOct.y));
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
            mesh.SetUVs(5, giantFlowCenters);
            mesh.SetUVs(6, giantFlowTangents);
            mesh.SetUVs(7, hullNormals);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1400f);
            mesh.UploadMeshData(true);
            return mesh;
        }

        public static Vector3 SubmarineTarget(int index)
        {
            if (index == 0) return TowerSurface(1, Mathf.PI * 0.5f, 0.64f, out _);
            if (index == 1) return TowerSurface(2, 0f, 0.92f, out _);
            if (index < 10)
            {
                int point = index - 2;
                float z = -52f + point * 15f;
                float angle = point % 2 == 0 ? 0.58f : Mathf.PI - 0.58f;
                return PressureSurface(z, angle, out _);
            }
            if (index < 18)
            {
                int point = index - 10;
                int side = point % 2 == 0 ? -1 : 1;
                int tube = point / 2;
                return SubmarineMuzzle(side, tube) + Vector3.up * 1.95f;
            }
            if (index < 22)
            {
                int point = index - 18;
                int side = point % 2 == 0 ? -1 : 1;
                return SponsonSurface(side, point < 2 ? -24f : 8f,
                    side < 0 ? Mathf.PI : 0f, out _);
            }
            return RetroJetCenter(index - 22) + Vector3.up * 4.8f;
        }

        public static Vector3 SubmarineMuzzle(int side, int tube)
        {
            return new Vector3(side * 21f + (tube % 2 == 0 ? -2.2f : 2.2f),
                -5f + (tube / 2 == 0 ? -2.1f : 2.1f), 28.5f);
        }

        public static Vector3 FleetTarget(int craft, int point)
        {
            Vector3 local;
            switch (point)
            {
                case 0: local = NeedleSurface(20f, 0.8f, out _); break;
                case 1: local = NeedleSurface(6f, Mathf.PI - 0.7f, out _); break;
                case 2: local = KeelSurface(1, 0.3f, 0.4f, 1f, out _); break;
                case 3: local = ReactorHoop(-9.5f, 0f, 0f, out _); break;
                case 4: local = ReactorHoop(-9.5f, Mathf.PI, 0f, out _); break;
                case 5: local = ReactorSurface(Mathf.PI * 0.5f, 0.68f, out _); break;
                case 6: local = InterceptorJetCenter(1) + Vector3.up * 1.98f; break;
                default: local = InterceptorJetCenter(2) + Vector3.up * 1.98f; break;
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
                    case 0: return new Vector3(0f, -126f, 4f);
                    case 1: return new Vector3(-6.5f, -139f, 10.7f);
                    case 2: return new Vector3(6.5f, -139f, 10.7f);
                    default: return new Vector3(0f, -152f, 10.7f);
                }
            }
            if (index < 12)
            {
                int point = index - 4;
                float y = -180f - (point / 2) * 9f;
                float radius = ChestRadius(y);
                float angle = point % 2 == 0 ? 0.95f : Mathf.PI - 0.95f;
                return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius * 0.55f + 0.7f);
            }
            if (index < 16)
            {
                int point = index - 12;
                return new Vector3(point % 2 == 0 ? -9f : 9f, -225f - (point / 2) * 8f, 9.5f);
            }
            if (index < 20)
            {
                int point = index - 16;
                bool left = point < 2;
                joint = left ? 1 : 3;
                float side = left ? -1f : 1f;
                float t = point % 2 == 0 ? 0.34f : 0.78f;
                return SegmentSurface(Shoulder(side), Elbow(side), 5.8f, t, Mathf.PI * 1.5f, 1.04f);
            }
            if (index < 26)
            {
                int point = index - 20;
                bool left = point < 3;
                joint = left ? 2 : 4;
                float side = left ? -1f : 1f;
                float t = point % 3 == 0 ? 0.18f : point % 3 == 1 ? 0.56f : 0.91f;
                return SegmentSurface(Elbow(side), Hand(side), 5f, t, Mathf.PI * 1.5f, 1.05f);
            }
            {
                int point = index - 26;
                joint = 4;
                if (point == 5) return SpearTip;
                if (point == 4) return Vector3.Lerp(SpearNeck, SpearTip, 0.45f) + Vector3.forward * 3.3f;
                return Vector3.Lerp(SpearBase, SpearNeck, 0.18f + point * 0.25f) + Vector3.forward * 2.1f;
            }
        }

        public static Vector3 PoseGiant(Vector3 point, int joint, float beat)
        {
            if (joint == 0) return point;
            bool left = joint == 1 || joint == 2;
            float side = left ? -1f : 1f;
            float phase = beat * 0.47f + (left ? 0f : Mathf.PI);
            float shoulderAngle = left ? 0.06f + Mathf.Sin(phase) * 0.16f : -0.04f + Mathf.Sin(phase) * 0.07f;
            Vector3 shoulder = Shoulder(side);
            Vector3 posed = shoulder + RotateZ(point - shoulder, shoulderAngle);
            if (joint == 2 || joint == 4)
            {
                Vector3 elbow = Elbow(side);
                Vector3 posedElbow = shoulder + RotateZ(elbow - shoulder, shoulderAngle);
                float bend = left ? 0.12f + Mathf.Sin(phase + 1.1f) * 0.12f : 0.06f + Mathf.Sin(phase + 1.1f) * 0.06f;
                posed = posedElbow + RotateZ(posed - posedElbow, side * bend);
            }
            return posed;
        }

        static Vector3 SampleSubmarine(int index, float seed, out float accent, out Vector3 normal)
        {
            float kind = Hash(index, 23);
            float a = Hash(index, 29);
            float b = Hash(index, 31);
            float c = Hash(index, 37);
            float d = Hash(index, 41);
            float angle = a * Mathf.PI * 2f;
            accent = 0.14f + seed * 0.4f;

            if (kind < 0.48f)
            {
                // A pressure cylinder with true ellipsoidal endcaps, never a pointed bow.
                float z = (b * 2f - 1f) * 70f;
                return PressureSurface(z, angle, out normal);
            }
            if (kind < 0.60f)
            {
                accent = c > 0.82f ? 0.74f : 0.24f + seed * 0.3f;
                if (d < 0.12f)
                {
                    Vector3 start = new Vector3(b < 0.5f ? -2.6f : 2.6f, 32f, 2f);
                    return TubeSurface(start, start + Vector3.up * 8f, 0.6f, c, angle, out normal);
                }
                return TowerSurface(Mathf.FloorToInt(c * 3f), angle, b, out normal);
            }
            if (kind < 0.77f)
            {
                int side = b < 0.5f ? -1 : 1;
                accent = 0.2f + seed * 0.32f;
                if (d < 0.2f)
                {
                    float z = -29f + Mathf.Floor(c * 5f) * 12f;
                    accent = 0.76f;
                    return TorusZ(new Vector3(side * 21f, -5f, z), 6.8f, 6.5f, 0.4f,
                        angle, d * Mathf.PI * 10f, out normal);
                }
                return SponsonSurface(side, -38f + c * 68f, angle, out normal);
            }
            if (kind < 0.85f)
            {
                int hoop = Mathf.FloorToInt(b * 9f);
                float z = -44f + hoop * 11f;
                accent = 0.67f + seed * 0.14f;
                return TorusZ(new Vector3(0f, 0f, z), 17.35f, 15.85f, 0.5f,
                    angle, c * Mathf.PI * 2f, out normal);
            }
            if (kind < 0.9f)
            {
                float pipeAngle = (Mathf.Floor(b * 4f) + 0.5f) * Mathf.PI * 0.5f;
                Vector3 start = new Vector3(Mathf.Cos(pipeAngle) * 17.8f, Mathf.Sin(pipeAngle) * 16.3f, -44f);
                accent = 0.71f + seed * 0.09f;
                return TubeSurface(start, start + Vector3.forward * 88f, 0.65f, c, angle, out normal);
            }
            if (kind < 0.945f)
            {
                Vector3 muzzle = SubmarineMuzzle(b < 0.5f ? -1 : 1, Mathf.FloorToInt(c * 4f));
                accent = d < 0.5f ? 0.79f : 0.9f;
                if (d < 0.5f)
                    return TorusZ(muzzle, 1.65f, 1.65f, 0.3f, angle, d * Mathf.PI * 4f, out normal);
                return TubeSurface(muzzle - Vector3.forward * 6f, muzzle, 1.65f,
                    d * 2f - 1f, angle, out normal);
            }
            if (kind < 0.965f)
            {
                int side = b < 0.5f ? -1 : 1;
                Vector3 center = new Vector3(side * 17.1f, 3.5f, -42f + Mathf.Floor(c * 7f) * 14f);
                if (d < 0.3f) center = new Vector3(side * 5.8f, 26f, 7f);
                accent = 0.97f + seed * 0.02f;
                return EllipsoidSurface(center, new Vector3(0.8f, 0.85f, 1.5f), angle, c, out normal);
            }
            if (kind < 0.985f)
            {
                accent = 0.34f + seed * 0.16f;
                if (b < 0.28f)
                    return EllipsoidSurface(new Vector3(0f, 10f, -54f), new Vector3(1.2f, 10f, 7f), angle, c, out normal);
                return EllipsoidSurface(new Vector3(b < 0.64f ? -14f : 14f, 0f, -52f),
                    new Vector3(10f, 1.2f, 7f), angle, c, out normal);
            }
            accent = d < 0.6f ? 0.77f : 0.96f;
            Vector3 jet = RetroJetCenter(b < 0.5f ? 0 : 1);
            if (d < 0.6f)
                return TorusZ(jet, 4.1f, 4.1f, 0.7f, angle, c * Mathf.PI * 2f, out normal);
            return TubeSurface(jet, jet + Vector3.forward * 9f, 4.1f, c, angle, out normal);
        }

        static Vector3 SampleFleet(int index, float seed, out float accent, out Vector3 normal)
        {
            int craft = index % 3;
            Vector3 point = SampleInterceptor(index, seed, out accent, out normal);
            float angle = craft * Mathf.PI * 2f / 3f;
            normal = RotateY(normal, angle + Mathf.PI * 0.5f);
            return FleetLocal(craft, point);
        }

        static Vector3 SampleInterceptor(int index, float seed, out float accent, out Vector3 normal)
        {
            float kind = Hash(index, 103);
            float a = Hash(index, 107);
            float b = Hash(index, 109);
            float c = Hash(index, 113);
            float d = Hash(index, 127);
            float angle = a * Mathf.PI * 2f;
            accent = 0.15f + seed * 0.38f;
            if (kind < 0.4f)
                return NeedleSurface(-2f + b * 40f, angle, out normal);
            if (kind < 0.5f)
                return NeedleSurface(-28f + b * 11f, angle, out normal);
            if (kind < 0.7f)
            {
                int keel = Mathf.FloorToInt(b * 3f);
                float u = Mathf.Sqrt(c);
                float v = d * u;
                accent = d > 0.86f ? 0.72f : 0.2f + seed * 0.35f;
                return KeelSurface(keel, 1f - u, v, a < 0.5f ? -1f : 1f, out normal);
            }
            if (kind < 0.8f)
            {
                accent = d < 0.55f ? 0.69f : 0.4f;
                if (d < 0.55f)
                {
                    int hoop = Mathf.FloorToInt(b * 3f);
                    return ReactorHoop(-17f + hoop * 7.5f, angle, c * Mathf.PI * 2f, out normal);
                }
                float railAngle = Mathf.Floor(b * 6f) * Mathf.PI / 3f;
                Vector3 start = new Vector3(Mathf.Cos(railAngle) * 6f, Mathf.Sin(railAngle) * 6f, -17f);
                return TubeSurface(start, start + Vector3.forward * 15f, 0.38f, c, angle, out normal);
            }
            if (kind < 0.9f)
            {
                accent = 0.85f + seed * 0.07f;
                if (d < 0.28f)
                    return TorusZ(new Vector3(0f, 0f, -9.5f), 2.8f, 2.8f, 0.25f,
                        angle, c * Mathf.PI * 2f, out normal);
                return ReactorSurface(angle, b, out normal);
            }
            Vector3 jet = InterceptorJetCenter(Mathf.FloorToInt(b * 3f));
            accent = d < 0.45f ? 0.98f : 0.62f;
            if (d < 0.45f)
                return TorusZ(jet, 1.7f, 1.7f, 0.28f, angle, c * Mathf.PI * 2f, out normal);
            return TubeSurface(jet, jet + Vector3.forward * 7f, 1.7f, c, angle, out normal);
        }

        static Vector3 SampleGiant(int index, float seed, out int joint, out float accent,
            out Vector4 flowCenter, out Vector4 flowTangent)
        {
            float kind = Hash(index, 47);
            float a = Hash(index, 53);
            float b = Hash(index, 59);
            float c = Hash(index, 61);
            float d = Hash(index, 67);
            float angle = a * Mathf.PI * 2f;
            float radial = 0.97f + d * 0.05f;
            float rate = 0.16f + seed * 0.11f;
            joint = 0;
            accent = 0.22f + seed * 0.4f;

            if (kind < 0.26f)
            {
                // Overlapping scalloped cuirass plates, not a solid rectangular chest.
                int plate = Mathf.FloorToInt(b * 6f);
                float y = -177f - plate * 6.8f - c * 7.8f;
                float scallop = Mathf.Cos(angle * 3f) * 1.2f * c;
                float radius = ChestRadius(y) * radial + Mathf.Sin(c * Mathf.PI) * 1.1f;
                accent = c > 0.86f ? 0.96f : 0.22f + seed * 0.43f;
                return RingFlow(new Vector3(0f, y + scallop, 0f), Vector3.right * radius,
                    Vector3.forward * radius * 0.55f, angle, rate, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.34f)
            {
                int plate = Mathf.FloorToInt(b * 4f);
                float y = -218f - plate * 6.4f - c * 7f;
                float radius = 12.8f + plate * 1.4f + Mathf.Sin(c * Mathf.PI) * 1.2f;
                accent = c > 0.88f ? 0.97f : 0.3f + seed * 0.36f;
                return RingFlow(new Vector3(0f, y, 0f), Vector3.right * radius,
                    Vector3.forward * radius * 0.64f, angle, rate, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.44f)
            {
                // Concentric dome courses and a tall, smooth crown.
                float y = b * 2f - 1f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float course = 1f + Mathf.Sin(b * Mathf.PI * 8f) * 0.035f;
                accent = c > 0.9f ? 0.95f : 0.46f + seed * 0.4f;
                return RingFlow(new Vector3(0f, -143f + y * 18f, 0f), Vector3.right * (12f * r * course),
                    Vector3.forward * (10f * r * course), angle, rate * 0.7f, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.50f)
            {
                bool eye = c < 0.22f;
                Vector3 center = eye ? new Vector3(b < 0.5f ? -5.5f : 5.5f, -139f, 10.6f) : new Vector3(0f, -144f, 9.9f);
                float scale = eye ? 0.7f + d * 0.3f : 0.65f + b * 0.35f;
                accent = eye ? 0.91f : 0.97f;
                return RingFlow(center, Vector3.right * (eye ? 2.6f : 10f) * scale,
                    Vector3.up * (eye ? 0.8f : 12f) * scale, angle, rate, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.55f)
            {
                float y = -161f - b * 17f;
                float radius = 7f + b * 15f;
                accent = c > 0.65f ? 0.96f : 0.83f;
                return RingFlow(new Vector3(0f, y, 0f), Vector3.right * radius,
                    Vector3.forward * radius * 0.66f, angle, rate, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.59f)
            {
                int crest = Mathf.FloorToInt(b * 3f) - 1;
                Vector3 start = new Vector3(crest * 8f, -132f, 0f);
                Vector3 end = new Vector3(crest * 16f, crest == 0 ? -111f : -119f, -2f);
                accent = 0.97f;
                return SegmentFlow(start, end, 1.8f * (1f - c) + 0.1f, c, angle, 1f, rate,
                    accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.69f)
            {
                bool left = b < 0.5f;
                float side = left ? -1f : 1f;
                joint = left ? 1 : 3;
                accent = d > 0.85f ? 0.97f : 0.35f + seed * 0.4f;
                if (c < 0.3f)
                {
                    float y = d * 2f - 1f;
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    return RingFlow(Shoulder(side) + Vector3.up * y * 9f, Vector3.right * r * 11f,
                        Vector3.forward * r * 9f, angle, rate, accent, out flowCenter, out flowTangent);
                }
                return SegmentFlow(Shoulder(side), Elbow(side), 5.8f + Mathf.Sin(c * Mathf.PI * 4f) * 0.65f,
                    c, angle, radial, rate, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.82f)
            {
                bool left = b < 0.5f;
                float side = left ? -1f : 1f;
                joint = left ? 2 : 4;
                float plate = Mathf.Repeat(c * 5f, 1f);
                accent = plate > 0.82f ? 0.98f : 0.52f + seed * 0.32f;
                return SegmentFlow(Elbow(side), Hand(side), 4.2f + (1f - c) * 2f + Mathf.Sin(plate * Mathf.PI) * 0.8f,
                    c, angle, radial, rate, accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.87f)
            {
                bool left = b < 0.5f;
                float side = left ? -1f : 1f;
                joint = left ? 2 : 4;
                int finger = Mathf.FloorToInt(c * 4f);
                Vector3 hand = Hand(side);
                Vector3 start = hand + new Vector3((finger - 1.5f) * 1.9f, 0f, 0f);
                Vector3 end = left ? start + new Vector3(-2f, -14f + finger, 3f) :
                    hand + new Vector3(2.5f, -3f - finger * 2f, 4f);
                accent = 0.86f + seed * 0.13f;
                return SegmentFlow(start, end, 1.25f, d, angle, radial, rate,
                    accent, out flowCenter, out flowTangent);
            }
            if (kind < 0.96f)
            {
                joint = 4;
                float t = b;
                float radius = 1.55f;
                if (c < 0.18f) { t = 0.93f + d * 0.025f; radius = 5.7f; }
                else if (c < 0.36f) { t = (Mathf.Floor(b * 9f) + 0.5f) / 9f; radius = 2.2f; }
                accent = c < 0.36f ? 0.98f : 0.83f + seed * 0.1f;
                return SegmentFlow(SpearBase, SpearNeck, radius, t, angle, 1f, rate * 2f,
                    accent, out flowCenter, out flowTangent);
            }
            joint = 4;
            accent = c < 0.35f ? 0.99f : 0.9f;
            float bladeT = d < 0.2f ? 0.9f + b * 0.1f : b;
            return SegmentFlow(SpearNeck, SpearTip, Mathf.Sin(bladeT * Mathf.PI) * 7.2f,
                bladeT, angle, 1f, rate, accent, out flowCenter, out flowTangent, 0.42f);
        }

        static float ChestRadius(float y)
        {
            float t = Mathf.Clamp01((-y - 177f) / 43f);
            return 24f - t * 10f + Mathf.Sin(t * Mathf.PI) * 2f;
        }

        // The two stored vectors are a closed elliptical surface orbit and its derivative.
        // Rotating them in the shader slides stable particle IDs without resampling or jitter.
        static Vector3 RingFlow(Vector3 center, Vector3 u, Vector3 v, float angle, float rate, float accent,
            out Vector4 flowCenter, out Vector4 flowTangent)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Vector3 tangent = -u * s + v * c;
            flowCenter = new Vector4(center.x, center.y, center.z, rate);
            flowTangent = new Vector4(tangent.x, tangent.y, tangent.z, accent);
            return center + u * c + v * s;
        }

        static Vector3 SegmentFlow(Vector3 start, Vector3 end, float radius, float t, float angle, float scale,
            float rate, float accent, out Vector4 flowCenter, out Vector4 flowTangent, float flatten = 1f)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.forward).normalized;
            Vector3 other = Vector3.Cross(axis, side).normalized;
            float taper = 0.86f + Mathf.Sin(t * Mathf.PI) * 0.22f;
            return RingFlow(Vector3.Lerp(start, end, t), side * radius * taper * scale,
                other * radius * taper * scale * flatten, angle, rate, accent, out flowCenter, out flowTangent);
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

        static Vector3 PressureSurface(float z, float angle, out Vector3 normal)
        {
            return CapsuleSurface(Vector3.zero, z, angle, 46f, 24f, 17f, 15.5f, out normal);
        }

        static Vector3 SponsonSurface(int side, float z, float angle, out Vector3 normal)
        {
            return CapsuleSurface(new Vector3(side * 21f, -5f, -4f), z + 4f, angle,
                26f, 8f, 6.5f, 6.2f, out normal);
        }

        static Vector3 CapsuleSurface(Vector3 center, float z, float angle, float straight, float cap,
            float radiusX, float radiusY, out Vector3 normal)
        {
            float end = Mathf.Max(0f, Mathf.Abs(z) - straight);
            float q = Mathf.Clamp01(end / cap);
            float profile = Mathf.Sqrt(Mathf.Max(0f, 1f - q * q));
            Vector3 local = new Vector3(Mathf.Cos(angle) * radiusX * profile,
                Mathf.Sin(angle) * radiusY * profile, z);
            normal = new Vector3(local.x / (radiusX * radiusX), local.y / (radiusY * radiusY),
                Mathf.Sign(z) * end / (cap * cap)).normalized;
            return center + local;
        }

        static Vector3 TowerSurface(int step, float angle, float vertical, out Vector3 normal)
        {
            Vector3 center = step == 0 ? new Vector3(0f, 18f, 5f) :
                step == 1 ? new Vector3(0f, 25f, 4f) : new Vector3(0f, 31f, 2f);
            Vector3 radii = step == 0 ? new Vector3(8.5f, 7f, 16f) :
                step == 1 ? new Vector3(6.5f, 5f, 11f) : new Vector3(4.2f, 3.5f, 7.5f);
            return EllipsoidSurface(center, radii, angle, vertical, out normal);
        }

        static Vector3 RetroJetCenter(int jet) => new(jet == 0 ? -6f : 6f, -2f, -69f);

        static Vector3 NeedleSurface(float z, float angle, out Vector3 normal)
        {
            bool nose = z >= -2f;
            float u = Mathf.Clamp01((38f - z) / 40f);
            float radius = nose ? 3.8f * Mathf.Pow(u, 0.84f) : 3f + (z + 28f) * (0.8f / 11f);
            float slope = nose ? -3.8f * 0.84f / 40f * Mathf.Pow(Mathf.Max(u, 0.0001f), -0.16f) : 0.8f / 11f;
            normal = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) / 0.7f, -slope).normalized;
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.7f, z);
        }

        static Vector3 KeelSurface(int keel, float u, float v, float face, out Vector3 normal)
        {
            float angle = Mathf.PI * 0.5f + keel * Mathf.PI * 2f / 3f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            normal = new Vector3(-radial.y, radial.x, 0f) * face;
            Vector3 front = radial + Vector3.forward * 30f;
            Vector3 swept = radial * 11f - Vector3.forward * 14f;
            Vector3 rear = radial * 3.3f - Vector3.forward * 25f;
            return front + (swept - front) * u + (rear - front) * v + normal * 0.3f;
        }

        static Vector3 ReactorHoop(float z, float angle, float section, out Vector3 normal)
        {
            return TorusZ(new Vector3(0f, 0f, z), 6f, 6f, 0.38f, angle, section, out normal);
        }

        static Vector3 ReactorSurface(float angle, float vertical, out Vector3 normal)
        {
            return EllipsoidSurface(new Vector3(0f, 0f, -9.5f), new Vector3(2.3f, 2.3f, 5.2f),
                angle, vertical, out normal);
        }

        static Vector3 InterceptorJetCenter(int jet)
        {
            float angle = Mathf.PI * 0.5f + jet * Mathf.PI * 2f / 3f;
            return new Vector3(Mathf.Cos(angle) * 4.2f, Mathf.Sin(angle) * 4.2f, -32f);
        }

        static Vector3 TorusZ(Vector3 center, float radiusX, float radiusY, float minor,
            float angle, float section, out Vector3 normal)
        {
            Vector3 rim = new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
            Vector3 radial = new Vector3(Mathf.Cos(angle) / radiusX, Mathf.Sin(angle) / radiusY, 0f).normalized;
            normal = radial * Mathf.Cos(section) + Vector3.forward * Mathf.Sin(section);
            return center + rim + normal * minor;
        }

        static Vector3 TubeSurface(Vector3 start, Vector3 end, float radius, float t, float angle, out Vector3 normal)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.forward);
            if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(axis, Vector3.up);
            side.Normalize();
            Vector3 other = Vector3.Cross(axis, side).normalized;
            normal = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
            return Vector3.Lerp(start, end, t) + normal * radius;
        }

        static Vector3 EllipsoidSurface(Vector3 center, Vector3 radii, float angle, float vertical, out Vector3 normal)
        {
            float y = vertical * 2f - 1f;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            Vector3 direction = new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
            normal = new Vector3(direction.x / radii.x, direction.y / radii.y, direction.z / radii.z).normalized;
            return center + Vector3.Scale(direction, radii);
        }

        static Vector2 EncodeNormal(Vector3 normal)
        {
            normal /= Mathf.Abs(normal.x) + Mathf.Abs(normal.y) + Mathf.Abs(normal.z);
            Vector2 oct = new Vector2(normal.x, normal.y);
            if (normal.z < 0f)
                oct = new Vector2((1f - Mathf.Abs(oct.y)) * (oct.x >= 0f ? 1f : -1f),
                    (1f - Mathf.Abs(oct.x)) * (oct.y >= 0f ? 1f : -1f));
            return oct;
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
