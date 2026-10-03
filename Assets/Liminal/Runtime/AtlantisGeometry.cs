using UnityEngine;

namespace Liminal
{
    internal static class AtlantisGeometry
    {
        public const int AuxiliaryPointBudget = 35000;
        public const int SerpentSchoolCount = 7;
        public const int FishPerSchool = 22;
        const float Tau = Mathf.PI * 2f;
        static readonly Color Pearl = new(0.68f, 0.94f, 0.91f);
        static readonly Color Aqua = new(0.12f, 0.78f, 0.71f);
        static readonly Color Gold = new(1f, 0.61f, 0.24f);
        static readonly Color Blue = new(0.07f, 0.20f, 0.38f);

        public readonly struct SchoolPose
        {
            public readonly Vector3 Center;
            public readonly float HeadingRadians;

            public SchoolPose(Vector3 center, float headingRadians)
            {
                Center = center;
                HeadingRadians = headingRadians;
            }
        }

        public static Vector3 CityOrigin
        {
            get
            {
                CaveLayout.Chamber room = CaveLayout.Rooms[2];
                return new Vector3(room.Center.x, room.Center.y - room.Radius.y, room.Center.z);
            }
        }

        public static Bounds DestinationBounds => new Bounds(CityOrigin + Vector3.up * 23f,
            new Vector3(310f, 82f, 310f));

        public static Vector3 SampleWhaleDestination(int index)
        {
            uint id = (uint)Mathf.Max(0, index);
            int band = (int)(id % 100u);
            int sample = (int)(id / 100u);
            float u = RadicalInverse(sample + 1, 2);
            float v = RadicalInverse(sample + 1, 3);
            float angle = Tau * v;
            Vector3 local;

            if (band < 20)
            {
                float radius = 136f * Mathf.Sqrt(u);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                local = new Vector3(x, FloorOffset(x, z) + 0.55f + Hash01(id + 13u) * 1.1f, z);
            }
            else if (band < 50)
            {
                int tier = (band - 20) / 10;
                float ring = tier == 0 ? 43f : tier == 1 ? 77f : 111f;
                float height = tier == 0 ? 31f : tier == 1 ? 21f : 11f;
                float radius = ring + (u - 0.5f) * 12f;
                float theta = angle + tier * 0.19f;
                local = new Vector3(Mathf.Cos(theta) * radius, height + Hash01(id + 71u) * 1.4f,
                    Mathf.Sin(theta) * radius);
            }
            else if (band < 76)
            {
                int column = sample % 28;
                float theta = column * Tau / 28f;
                float ring = 50f + (column % 2) * 31f;
                float height = 14f + (column * 11 % 16);
                float brokenT = u < 0.37f ? u * 1.12f : 0.43f + (u - 0.37f) * 0.88f;
                float y = FloorOffset(Mathf.Cos(theta) * ring, Mathf.Sin(theta) * ring) + 4f + brokenT * height;
                float around = v * Tau + Hash01(id + 101u) * 0.2f;
                float radius = 0.82f + Hash01(id + 211u) * 0.34f;
                local = new Vector3(Mathf.Cos(theta) * ring + Mathf.Cos(around) * radius, y,
                    Mathf.Sin(theta) * ring + Mathf.Sin(around) * radius);
            }
            else if (band < 90)
            {
                int arch = sample % 8;
                float direction = arch * Tau / 8f;
                Vector3 forward = new(Mathf.Cos(direction), 0f, Mathf.Sin(direction));
                Vector3 side = new(-forward.z, 0f, forward.x);
                float t = u;
                float across;
                float y;
                if (t < 0.24f)
                {
                    across = -5.4f;
                    y = 3f + t / 0.24f * 21f;
                }
                else if (t < 0.48f)
                {
                    across = 5.4f;
                    y = 3f + (t - 0.24f) / 0.24f * 21f;
                }
                else
                {
                    float arc = Mathf.PI * (t - 0.48f) / 0.52f;
                    across = Mathf.Cos(arc) * 5.4f;
                    y = 24f + Mathf.Sin(arc) * 5.4f;
                }
                local = forward * 34f + side * across + Vector3.up * (y + Hash01(id + 311u) * 0.5f);
            }
            else
            {
                float radius = 24f + 100f * Mathf.Sqrt(u);
                float theta = angle + Hash01(id + 401u) * 0.4f;
                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;
                float floor = FloorOffset(x, z);
                float rubble = Hash01(id + 521u);
                local = new Vector3(x, floor + 1.5f + rubble * 6.5f, z);
            }

            return CityOrigin + local;
        }

        public static Mesh BuildPalace()
        {
            var cloud = new PointCloud();
            int seed = 0;
            Color limestone = new(0.48f, 0.93f, 0.81f);
            Color highlight = new(1f, 0.76f, 0.37f);

            // The raised square court and its four colonnades give the optional layer a clear palace silhouette.
            for (int side = 0; side < 4; side++)
            {
                for (int i = 0; i < 10; i++)
                {
                    float along = -39f + i * 8.65f;
                    Vector3 column = side switch
                    {
                        0 => new Vector3(along, 0f, -39f),
                        1 => new Vector3(39f, 0f, along),
                        2 => new Vector3(-along, 0f, 39f),
                        _ => new Vector3(-39f, 0f, -along)
                    };
                    AddColumn(cloud, column, 13f, 28f + (i % 4 == 0 ? 3f : 0f), 0.58f,
                        limestone, highlight, ref seed);
                }
            }

            for (int step = 0; step < 10; step++)
            {
                float z = -53f + step * 1.9f;
                float y = 4f + step * 0.72f;
                AddSegment(cloud, new Vector3(-31f, y, z), new Vector3(31f, y, z), 56,
                    step % 3 == 0 ? highlight : limestone, 0.12f, ref seed);
                AddSegment(cloud, new Vector3(-31f, y, z), new Vector3(-31f, y + 0.65f, z), 4,
                    limestone, 0.10f, ref seed);
                AddSegment(cloud, new Vector3(31f, y, z), new Vector3(31f, y + 0.65f, z), 4,
                    limestone, 0.10f, ref seed);
            }

            // A front portico, lintel, and open triangular pediment read as classical architecture at range.
            for (int i = 0; i < 7; i++)
            {
                float x = -24f + i * 8f;
                AddColumn(cloud, new Vector3(x, 11f, -31f), 19f, 24f, 0.72f,
                    limestone, highlight, ref seed);
            }
            AddSegment(cloud, new Vector3(-32f, 36f, -31f), new Vector3(32f, 36f, -31f), 110,
                highlight, 0.16f, ref seed);
            AddSegment(cloud, new Vector3(-30f, 37f, -31f), new Vector3(0f, 49f, -31f), 58,
                limestone, 0.16f, ref seed);
            AddSegment(cloud, new Vector3(0f, 49f, -31f), new Vector3(30f, 37f, -31f), 58,
                limestone, 0.16f, ref seed);
            AddSegment(cloud, new Vector3(-30f, 37f, -31f), new Vector3(30f, 37f, -31f), 92,
                highlight, 0.14f, ref seed);

            for (int arch = 0; arch < 4; arch++)
            {
                Vector3 forward = arch switch
                {
                    0 => Vector3.forward,
                    1 => Vector3.right,
                    2 => Vector3.back,
                    _ => Vector3.left
                };
                Vector3 side = new(-forward.z, 0f, forward.x);
                Vector3 center = forward * 28f;
                AddSegment(cloud, center - side * 8f + Vector3.up * 10f,
                    center - side * 8f + Vector3.up * 24f, 34, limestone, 0.13f, ref seed);
                AddSegment(cloud, center + side * 8f + Vector3.up * 10f,
                    center + side * 8f + Vector3.up * 24f, 34, limestone, 0.13f, ref seed);
                for (int point = 0; point <= 56; point++)
                {
                    float angle = Mathf.PI * point / 56f;
                    Vector3 p = center + side * (Mathf.Cos(angle) * 8f) +
                        Vector3.up * (24f + Mathf.Sin(angle) * 8f);
                    Add(cloud, p, 0.14f, point % 7 == 0 ? highlight : limestone, ref seed);
                }
            }

            AddSquareRing(cloud, 48f, 2f, .18f, Aqua, ref seed);
            AddSquareRing(cloud, 43f, 14f, .16f, highlight, ref seed);
            AddSquareRing(cloud, 35f, 38f, .20f, limestone, ref seed);
            AddSquareRing(cloud, 26f, 51f, .18f, highlight, ref seed);
            AddBrokenColumns(cloud, ref seed);
            return cloud.Build("Atlantis / Hermit palace", 600f);
        }

        public static Mesh BuildSerpentSchools()
        {
            var cloud = new PointCloud();
            int seed = 0;
            for (int school = 0; school < SerpentSchoolCount; school++)
            {
                SchoolPose pose = EvaluateSchoolPose(school, 0f);
                Quaternion orientation = Quaternion.Euler(0f, pose.HeadingRadians * Mathf.Rad2Deg, 0f);
                Vector3 tangent = orientation * Vector3.forward;
                Vector3 lateral = orientation * Vector3.right;
                for (int fish = 0; fish < FishPerSchool; fish++)
                {
                    float shell = Mathf.Sqrt((fish + 0.5f) / FishPerSchool);
                    float scatter = fish * 2.3999632f + school * 0.73f;
                    Vector3 fishCenter = pose.Center + tangent * (Mathf.Cos(scatter) * shell * 15f) +
                        lateral * (Mathf.Sin(scatter) * shell * 12f) + Vector3.up * Mathf.Sin(scatter * 0.7f) * 3f;
                    float headingJitter = ((fish % 5) - 2) * 3f;
                    Quaternion fishOrientation = Quaternion.AngleAxis(headingJitter, Vector3.up);
                    AddSchoolFish(cloud, fishCenter, fishOrientation * tangent,
                        fishOrientation * lateral, school, fish, ref seed);
                }
            }
            return cloud.Build("Atlantis / Serpent shoals", 760f);
        }

        public static Mesh BuildSubmarines()
        {
            var cloud = new PointCloud();
            int seed = 0;
            Vector3[] centers = {
                new(-133f, 33f, -9f), new(139f, 37f, 5f), new(16f, 29f, 142f)
            };
            for (int craft = 0; craft < centers.Length; craft++)
            {
                Vector3 center = centers[craft];
                Color hull = craft == 1 ? new Color(0.30f, 0.70f, 0.91f) : new Color(0.42f, 0.84f, 0.78f);
                for (int slice = 0; slice < 56; slice++)
                {
                    float u = (slice + 0.5f) / 56f;
                    float z = 10f - u * 20f;
                    float radius = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.72f);
                    for (int around = 0; around < 14; around++)
                    {
                        float angle = around * Tau / 14f;
                        Vector3 p = center + new Vector3(Mathf.Cos(angle) * radius * 1.42f,
                            Mathf.Sin(angle) * radius * 1.06f, z);
                        Add(cloud, p, 0.105f, around % 7 == 0 ? Pearl : hull, ref seed, craft);
                    }
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    for (int porthole = 0; porthole < 4; porthole++)
                    {
                        float z = 3.8f - porthole * 2.2f;
                        Vector3 port = center + new Vector3(side * 1.22f, 0.18f, z);
                        for (int point = 0; point < 16; point++)
                        {
                            float angle = point * Tau / 16f;
                            Add(cloud, port + new Vector3(0f, Mathf.Cos(angle) * 0.42f,
                                Mathf.Sin(angle) * 0.42f), 0.12f, Gold, ref seed, craft);
                        }
                    }
                }

                AddSegment(cloud, center + new Vector3(0f, 0.78f, 0.7f),
                    center + new Vector3(0f, 2.7f, 0.7f), 24, hull, 0.12f, ref seed, craft);
                AddSegment(cloud, center + new Vector3(-1.8f, 0.85f, 0.7f),
                    center + new Vector3(1.8f, 0.85f, 0.7f), 28, hull, 0.12f, ref seed, craft);
                AddSegment(cloud, center + new Vector3(0f, 2.7f, -0.7f),
                    center + new Vector3(0f, 2.7f, 2.1f), 16, Pearl, 0.08f, ref seed, craft);

                for (int fin = -1; fin <= 1; fin += 2)
                {
                    AddSegment(cloud, center + new Vector3(fin * 0.7f, 0f, 7.1f),
                        center + new Vector3(fin * 2.2f, 0f, 9.5f), 26, hull, 0.095f, ref seed, craft);
                    AddSegment(cloud, center + new Vector3(fin * 2.2f, 0f, 9.5f),
                        center + new Vector3(fin * 0.3f, 0f, 9.5f), 24, Gold, 0.08f, ref seed, craft);
                }
                Add(cloud, center + new Vector3(0f, 0.3f, -9.2f), 0.24f,
                    new Color(0.96f, 0.92f, 0.66f), ref seed, craft);
            }
            return cloud.Build("Atlantis / observation craft", 600f);
        }

        public static Vector3 SchoolCenter(int school)
        {
            return EvaluateSchoolPose(school, 0f).Center;
        }

        public static SchoolPose EvaluateSchoolPose(int school, float song)
        {
            school = Mathf.Clamp(school, 0, SerpentSchoolCount - 1);
            float phaseOffset = school * 2.3999632f;
            float speed = 0.115f + (school % 3) * 0.017f;
            float phase = phaseOffset + song * speed;
            float laneAngle = school * Tau / SerpentSchoolCount;
            float radius = 218f + (school % 3) * 24f;
            float orbitX = 22f + (school % 2) * 8f;
            float orbitZ = 18f + ((school + 1) % 3) * 5f;
            float altitude = 82f + (school % 4) * 14f;
            Vector3 center = new(
                Mathf.Cos(laneAngle) * radius + Mathf.Cos(phase) * orbitX,
                altitude + Mathf.Sin(phase * 0.67f + school * 0.37f) * 3f,
                Mathf.Sin(laneAngle) * radius + Mathf.Sin(phase) * orbitZ);
            float velocityX = -Mathf.Sin(phase) * orbitX * speed;
            float velocityZ = Mathf.Cos(phase) * orbitZ * speed;
            return new SchoolPose(center, Mathf.Atan2(velocityX, velocityZ));
        }

        static void AddSchoolFish(PointCloud cloud, Vector3 center, Vector3 forward, Vector3 side,
            int school, int fish, ref int seed)
        {
            Color color = fish % 7 == 0 ? new Color(0.42f, 0.22f, 0.08f) :
                Color.Lerp(Blue, new Color(0.12f, 0.42f, 0.40f),
                    ((fish + school) % 7) / 7f);
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                float z = 3.8f - t * 7.6f;
                float width = 0.15f + Mathf.Sin(t * Mathf.PI) * 0.76f;
                Add(cloud, center + forward * z, 0.105f, color, ref seed, school);
                Add(cloud, center + forward * z + side * width, 0.08f, color, ref seed, school);
                Add(cloud, center + forward * z - side * width, 0.08f, color, ref seed, school);
            }
            Color tail = new(0.045f, 0.28f, 0.29f);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float z = -3.35f - t * 1.15f;
                float flare = Mathf.Sin(t * Mathf.PI) * 0.68f;
                Add(cloud, center + forward * z + side * flare, 0.074f, tail, ref seed, school);
                Add(cloud, center + forward * z - side * flare, 0.074f, tail, ref seed, school);
            }
        }

        static void AddColumn(PointCloud cloud, Vector3 center, float bottom, float height,
            float radius, Color color, Color accent, ref int seed)
        {
            const int aroundCount = 8;
            const int levels = 23;
            for (int level = 0; level <= levels; level++)
            {
                float t = level / (float)levels;
                float y = bottom + t * height;
                float r = radius * (level < 2 || level > levels - 2 ? 1.32f : 1f);
                for (int around = 0; around < aroundCount; around++)
                {
                    float angle = around * Tau / aroundCount;
                    Vector3 p = center + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                    Add(cloud, p, 0.115f, level % 6 == 0 ? accent : color, ref seed);
                }
            }
            AddSquareRing(cloud, 1.25f, bottom + height, .09f, accent, ref seed,
                new Vector2(center.x, center.z));
        }

        static void AddBrokenColumns(PointCloud cloud, ref int seed)
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Tau / 16f;
                float radius = 68f + (i % 3) * 12f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float height = 8f + (i * 7 % 15);
                int levels = Mathf.RoundToInt(height * 1.35f);
                for (int level = 0; level <= levels; level++)
                {
                    if (i % 3 == 0 && level > levels * 0.54f) continue;
                    float y = FloorOffset(x, z) + 2f + level * 0.72f;
                    for (int around = 0; around < 6; around++)
                    {
                        float theta = around * Tau / 6f;
                        Add(cloud, new Vector3(x + Mathf.Cos(theta) * 0.5f, y,
                            z + Mathf.Sin(theta) * 0.5f), 0.09f,
                            level % 5 == 0 ? Gold : new Color(0.18f, 0.65f, 0.61f), ref seed);
                    }
                }
            }
        }

        static void AddSquareRing(PointCloud cloud, float halfSize, float y, float particleRadius,
            Color color, ref int seed, Vector2 center = default)
        {
            int count = Mathf.Max(24, Mathf.CeilToInt(halfSize * 2.4f));
            Vector3[] corners = {
                new(center.x - halfSize, y, center.y - halfSize),
                new(center.x + halfSize, y, center.y - halfSize),
                new(center.x + halfSize, y, center.y + halfSize),
                new(center.x - halfSize, y, center.y + halfSize),
                new(center.x - halfSize, y, center.y - halfSize)
            };
            for (int side = 0; side < 4; side++)
                AddSegment(cloud, corners[side], corners[side + 1], count, color, particleRadius, ref seed);
        }

        static void AddSegment(PointCloud cloud, Vector3 from, Vector3 to, int count,
            Color color, float size, ref int seed, int motion = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count < 2 ? 0.5f : i / (float)(count - 1);
                Add(cloud, Vector3.Lerp(from, to, t), size, color, ref seed, motion);
            }
        }

        static void Add(PointCloud cloud, Vector3 point, float size, Color color, ref int seed,
            int motion = 0)
        {
            cloud.Add(point, size, color, Mathf.Repeat(seed++ * 0.6180339f, 1f), motion);
        }

        static float FloorOffset(float x, float z)
        {
            CaveLayout.Chamber room = CaveLayout.Rooms[2];
            float nx = x / room.Radius.x;
            float nz = z / room.Radius.z;
            float floor = room.Center.y - room.Radius.y * Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - nz * nz));
            return floor - CityOrigin.y;
        }

        static float RadicalInverse(int value, int radix)
        {
            float inverse = 1f / radix;
            float result = 0f;
            while (value > 0)
            {
                result += (value % radix) * inverse;
                value /= radix;
                inverse /= radix;
            }
            return result;
        }

        static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }

        static float Hash01(uint value) => (Hash(value) & 0x00ffffffu) / 16777216f;
    }
}
