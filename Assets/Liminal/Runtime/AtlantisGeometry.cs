using UnityEngine;

namespace Liminal
{
    internal static class AtlantisGeometry
    {
        public const int AuxiliaryPointBudget = 240000;
        public const int VrAuxiliaryPointBudget = 100000;
        public const int SerpentSchoolCount = 7;
        public const int DesktopFishPerSchool = 112;
        public const int VrFishPerSchool = 80;
        public const int FishPerSchool = DesktopFishPerSchool;
        const float Tau = Mathf.PI * 2f;

        static readonly Color DeepCobalt = new(0.035f, 0.09f, 0.20f);
        static readonly Color Cobalt = new(0.055f, 0.18f, 0.34f);
        static readonly Color Azure = new(0.075f, 0.34f, 0.50f);
        static readonly Color Aqua = new(0.09f, 0.47f, 0.45f);
        static readonly Color Pearl = new(0.58f, 0.75f, 0.79f);
        static readonly Color Gold = new(0.68f, 0.43f, 0.19f);
        static readonly Vector2[] RuinSites = {
            new(-132f, 88f), new(-108f, -126f), new(126f, -95f), new(156f, 40f),
            new(90f, 160f), new(-184f, 12f), new(30f, 212f), new(-15f, -204f),
            new(215f, -45f), new(-208f, -55f), new(73f, -174f), new(-160f, 130f)
        };
        static readonly Vector2[] PlazaCenters = {
            new(-112f, 70f), new(109f, 93f), new(118f, -98f), new(-115f, -104f)
        };

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

        public static Bounds DestinationBounds => new Bounds(CityOrigin + Vector3.up * 35f,
            new Vector3(620f, 106f, 620f));

        public static Bounds SerpentPoseBounds
        {
            get
            {
                const int samplesPerSchool = 192;
                Vector3 padding = new(42f, 12f, 42f);
                Bounds bounds = default;
                bool initialized = false;
                for (int school = 0; school < SerpentSchoolCount; school++)
                {
                    float period = Tau / SchoolSpeed(school);
                    for (int i = 0; i < samplesPerSchool; i++)
                    {
                        Vector3 center = CityOrigin + EvaluateSchoolPose(school,
                            period * i / samplesPerSchool).Center;
                        if (!initialized)
                        {
                            bounds = new Bounds(center, Vector3.zero);
                            initialized = true;
                        }
                        bounds.Encapsulate(center - padding);
                        bounds.Encapsulate(center + padding);
                    }
                }
                return bounds;
            }
        }

        public static int FishCount(bool reducedDensity) =>
            SerpentSchoolCount * (reducedDensity ? VrFishPerSchool : DesktopFishPerSchool);

        public static int ExpectedAuxiliaryPointCount(bool reducedDensity) => reducedDensity ?
            35000 + 25870 + 19600 + 15008 : 101000 + 60542 + 35280 + 39396;

        public static Vector3 SampleWhaleDestination(int index)
        {
            uint id = (uint)Mathf.Max(0, index);
            int band = (int)(id % 100u);
            int sample = (int)(id / 100u);
            float u = RadicalInverse(sample + 1, 2);
            float v = RadicalInverse(sample + 1, 3);
            float angle = Tau * v;
            Vector3 local;

            if (band < 38)
            {
                float radius = CoastRadius(angle) * 0.985f * Mathf.Sqrt(u);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                local = new Vector3(x, GroundHeight(x, z) + 0.25f + Hash01(id + 13u) * 0.8f, z);
            }
            else if (band < 54)
            {
                float radius = CoastRadius(angle) * (0.83f + u * 0.14f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                local = new Vector3(x, GroundHeight(x, z) + 1.4f + Hash01(id + 71u) * 3.1f, z);
            }
            else if (band < 70)
            {
                int route = sample % 7;
                float across = (v - 0.5f) * (route < 4 ? 11f : 4.5f);
                Vector3 point = CityRoutePoint(route, u, across);
                point.y = GroundHeight(point.x, point.z) + (route < 4 ? 0.42f : 0.28f);
                local = point;
            }
            else if (band < 91)
            {
                local = SampleRuinPoint(sample, u, v);
            }
            else
            {
                float radius = CoastRadius(angle) * (0.70f + 0.28f * u);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                local = new Vector3(x, GroundHeight(x, z) + 0.8f + Hash01(id + 521u) * 7.4f, z);
            }

            return CityOrigin + local;
        }

        public static float GroundHeight(float x, float z)
        {
            CaveLayout.Chamber room = CaveLayout.Rooms[2];
            float nx = x / room.Radius.x;
            float nz = z / room.Radius.z;
            float floor = room.Center.y - room.Radius.y *
                Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - nz * nz)) - CityOrigin.y;
            float radius = Mathf.Sqrt(x * x + z * z);
            float shelves = SmoothStep(82f, 92f, radius) * 1.7f +
                SmoothStep(142f, 158f, radius) * 2.5f + SmoothStep(204f, 222f, radius) * 3.2f;
            float rolling = 0.8f * Mathf.Sin(x * 0.027f + Mathf.Sin(z * 0.019f) * 1.7f) +
                0.6f * Mathf.Cos(z * 0.036f - x * 0.014f) +
                0.4f * Mathf.Sin((x + z) * 0.07f);
            return floor + 18.5f + shelves + rolling;
        }

        public static Mesh BuildContinent(bool reducedDensity)
        {
            var cloud = new PointCloud();
            int seed = 0;
            AddTerrainSurface(cloud, reducedDensity ? 19000 : 58000, ref seed);
            AddCoastalCliffs(cloud, reducedDensity ? 3500 : 8000, ref seed);
            AddTerraceRidges(cloud, reducedDensity ? 3500 : 10000, ref seed);
            AddRoads(cloud, reducedDensity ? 1600 : 4500, ref seed);
            AddCanals(cloud, reducedDensity ? 1300 : 3000, ref seed);
            AddPlazas(cloud, reducedDensity ? 1300 : 3000, ref seed);
            AddRuinDistricts(cloud, reducedDensity ? 4800 : 14500, ref seed);
            return cloud.Build("Atlantis / fractured island and ruined districts", 720f);
        }

        public static Mesh BuildPalace(bool reducedDensity)
        {
            var cloud = new PointCloud();
            int seed = 0;
            float ground = GroundHeight(0f, 0f);
            bool reduced = reducedDensity;
            int outerColumns = reduced ? 32 : 42;
            int innerColumns = reduced ? 6 : 8;
            int around = reduced ? 18 : 28;
            int levels = reduced ? 20 : 24;

            AddTemplePlinth(cloud, ground, reduced, ref seed);
            AddColumnSurfaces(cloud, ground + 6f, 29f, outerColumns, around, levels, 1.72f, ref seed);
            AddColumnBasesAndCapitals(cloud, ground + 6f, 29f, outerColumns,
                reduced ? 20 : 28, ref seed);
            AddInnerColumns(cloud, ground, innerColumns, reduced, ref seed);
            AddGrandStairs(cloud, ground, reduced, ref seed);
            AddEntablature(cloud, ground, reduced, ref seed);
            AddFrieze(cloud, ground, reduced, ref seed);
            AddPediments(cloud, ground, reduced, ref seed);
            AddRoofSurfaces(cloud, ground, reduced, ref seed);
            AddInteriorRelief(cloud, ground, reduced, ref seed);
            return cloud.Build("Atlantis / Hermit restored temple", 720f);
        }

        public static Mesh BuildSerpentSchools(bool reducedDensity)
        {
            var cloud = new PointCloud();
            int seed = 0;
            int fishPerSchool = reducedDensity ? VrFishPerSchool : DesktopFishPerSchool;
            SchoolPose[] initialPoses = new SchoolPose[SerpentSchoolCount];
            Quaternion[] initialRotations = new Quaternion[SerpentSchoolCount];
            Vector3[] initialTangents = new Vector3[SerpentSchoolCount];
            Vector3[] initialLaterals = new Vector3[SerpentSchoolCount];
            for (int school = 0; school < SerpentSchoolCount; school++)
            {
                SchoolPose pose = EvaluateSchoolPose(school, 0f);
                initialPoses[school] = pose;
                initialRotations[school] = Quaternion.Euler(0f,
                    pose.HeadingRadians * Mathf.Rad2Deg, 0f);
                initialTangents[school] = initialRotations[school] * Vector3.forward;
                initialLaterals[school] = initialRotations[school] * Vector3.right;
            }

            for (int school = 0; school < SerpentSchoolCount; school++)
            {
                for (int fish = 0; fish < fishPerSchool; fish++)
                {
                    float along = ((fish + 0.5f) / fishPerSchool - 0.5f) * 72f;
                    float lane = ((fish % 5) - 2) * 1.7f + Mathf.Sin(fish * 0.71f + school) * 0.25f +
                        Mathf.Sin(along * 0.045f) * 3f;
                    float height = Mathf.Sin(fish * 0.37f + school * 0.83f) * 0.6f;
                    Vector3 center = initialPoses[school].Center + initialTangents[school] * along +
                        initialLaterals[school] * lane + Vector3.up * height;
                    float jitter = ((fish % 5) - 2) * 2.2f;
                    Quaternion fishRotation = Quaternion.AngleAxis(jitter, Vector3.up);
                    AddSchoolFish(cloud, center, fishRotation * initialTangents[school],
                        fishRotation * initialLaterals[school], school, fish, reducedDensity, ref seed);
                }
            }
            return cloud.Build("Atlantis / traveling serpent schools", 720f);
        }

        public static Mesh BuildSubmarines(bool reducedDensity)
        {
            var cloud = new PointCloud();
            int seed = 0;
            Vector3[] centers = {
                new(18f, GroundHeight(18f, -108f) + 22f, -108f),
                new(-170f, GroundHeight(-170f, 10f) + 22f, 10f),
                new(165f, GroundHeight(165f, 24f) + 22f, 24f),
                new(-35f, GroundHeight(-35f, 186f) + 22f, 186f)
            };
            float[] headings = { -0.38f, 0.44f, -0.82f, 2.16f };
            for (int craft = 0; craft < centers.Length; craft++)
            {
                bool stage = craft == 0;
                int slices = reducedDensity ? (stage ? 112 : 64) : (stage ? 176 : 96);
                int around = reducedDensity ? (stage ? 64 : 32) : (stage ? 112 : 56);
                float craftScale = stage ? 1f : 38f / 15f;
                float length = stage ? 40f : 38f;
                float width = stage ? 5.1f : 2.25f * craftScale;
                float height = stage ? 3.65f : 1.75f * craftScale;
                Color hull = stage ? new Color(0.055f, 0.19f, 0.34f) :
                    craft == 2 ? new Color(0.075f, 0.31f, 0.40f) : new Color(0.08f, 0.28f, 0.39f);
                AddSubmarineHull(cloud, centers[craft], headings[craft], length, width, height,
                    slices, around, hull, craft, ref seed);
                AddSubmarineDetails(cloud, centers[craft], headings[craft], length, width, height,
                    stage, reducedDensity, hull, craft, ref seed);
            }
            return cloud.Build("Atlantis / retro stage and observation craft", 720f);
        }

        public static SchoolPose EvaluateSchoolPose(int school, float song)
        {
            school = Mathf.Clamp(school, 0, SerpentSchoolCount - 1);
            float speed = SchoolSpeed(school);
            float phase = school * 2.3999632f + song * speed;
            float radialLane = (school - 3) * 2.7f;
            float radius = 164f + 19f * Mathf.Sin(phase * 3f + school * 0.47f) +
                7f * Mathf.Sin(phase * 5f - school * 0.31f) + radialLane;
            float angle = phase + 0.08f * Mathf.Sin(phase * 2f + school * 0.61f) +
                0.03f * Mathf.Sin(phase * 4f - school * 0.23f);
            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);
            float y = GroundHeight(x, z) + 14.5f +
                Mathf.Sin(phase * 0.67f + school * 0.37f) * 0.8f;

            float radialVelocity = 57f * Mathf.Cos(phase * 3f + school * 0.47f) +
                35f * Mathf.Cos(phase * 5f - school * 0.31f);
            float angularVelocity = 1f + 0.16f * Mathf.Cos(phase * 2f + school * 0.61f) +
                0.12f * Mathf.Cos(phase * 4f - school * 0.23f);
            float velocityX = (radialVelocity * Mathf.Cos(angle) - radius * Mathf.Sin(angle) * angularVelocity) * speed;
            float velocityZ = (radialVelocity * Mathf.Sin(angle) + radius * Mathf.Cos(angle) * angularVelocity) * speed;
            return new SchoolPose(new Vector3(x, y, z), Mathf.Atan2(velocityX, velocityZ));
        }

        static float SchoolSpeed(int school) => 0.038f + (school % 3) * 0.004f;

        static void AddTerrainSurface(PointCloud cloud, int count, ref int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Tau * RadicalInverse(i + 1, 2);
                float radius = CoastRadius(angle) * 0.985f * Mathf.Sqrt(RadicalInverse(i + 1, 3));
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float ground = GroundHeight(x, z);
                Color color = TerrainColor(radius, Hash01((uint)i + 17u));
                int style = i % 181 == 0 ? 2 : i % 13 == 0 ? 1 : 0;
                Add(cloud, new Vector3(x, ground, z), 0.25f + Hash01((uint)i + 31u) * 0.28f,
                    color, ref seed, style);
            }
        }

        static Color TerrainColor(float radius, float noise)
        {
            if (radius > 210f) return Color.Lerp(Cobalt, Azure, noise * 0.65f);
            if (radius > 142f) return Color.Lerp(DeepCobalt, Azure, 0.24f + noise * 0.42f);
            return Color.Lerp(DeepCobalt, Cobalt, 0.28f + noise * 0.48f);
        }

        static void AddCoastalCliffs(PointCloud cloud, int count, ref int seed)
        {
            int levels = 5;
            int angularSamples = count / levels;
            for (int i = 0; i < count; i++)
            {
                int level = i % levels;
                int angleIndex = i / levels;
                float angle = Tau * angleIndex / angularSamples;
                float wobble = (Hash01((uint)i + 97u) - 0.5f) * 2.4f;
                float radius = CoastRadius(angle) - 1.6f - level * 0.42f + wobble;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = GroundHeight(x, z) + 6.7f - level * 1.55f +
                    Mathf.Sin(angle * 19f + level) * 0.55f;
                Color color = level == 0 ? Azure : Color.Lerp(Cobalt, Pearl, (4 - level) * 0.075f);
                Add(cloud, new Vector3(x, y, z), 0.28f + (level % 2) * 0.08f,
                    color, ref seed, level == 0 ? 1 : 0);
            }
        }

        static void AddTerraceRidges(PointCloud cloud, int count, ref int seed)
        {
            float[] bands = { 76f, 119f, 164f, 211f };
            for (int i = 0; i < count; i++)
            {
                int band = i % bands.Length;
                float angle = Tau * RadicalInverse(i + 1, 2);
                float wave = 8f * Mathf.Sin(angle * 2f + band * 0.83f) +
                    4.3f * Mathf.Sin(angle * 5f - band * 0.57f) +
                    2.1f * Mathf.Sin(angle * 11f + band);
                float radius = bands[band] + wave + (Hash01((uint)i + 223u) - 0.5f) * 7f;
                if (Mathf.Abs(Mathf.Sin(angle * 6f + band * 1.7f)) < 0.16f)
                    radius -= 10f + band * 2.2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = GroundHeight(x, z) + 1.1f + (band % 2) * 0.8f +
                    Mathf.Sin(angle * 3f + band) * 0.45f;
                Color color = band % 2 == 0 ? Azure : Aqua;
                Add(cloud, new Vector3(x, y, z), 0.16f + Hash01((uint)i + 281u) * 0.13f,
                    color, ref seed, 1);
            }
        }

        static void AddRoads(PointCloud cloud, int count, ref int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int route = i % 4;
                int sample = i / 4;
                float t = RadicalInverse(sample + 1, 2);
                float across = (Hash01((uint)i + 401u) - 0.5f) * 12f;
                Vector3 point = CityRoutePoint(route, t, across);
                point.y = GroundHeight(point.x, point.z) + 0.3f;
                Color color = Hash01((uint)i + 419u) > 0.86f ? Pearl :
                    Color.Lerp(Cobalt, Azure, Hash01((uint)i + 431u) * 0.52f);
                Add(cloud, point, 0.18f + Hash01((uint)i + 443u) * 0.13f, color, ref seed,
                    i % 97 == 0 ? 2 : 1);
            }
        }

        static void AddCanals(PointCloud cloud, int count, ref int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int route = 4 + i % 3;
                int sample = i / 3;
                float t = RadicalInverse(sample + 1, 2);
                float across = (Hash01((uint)i + 487u) - 0.5f) * 4.2f;
                Vector3 point = CityRoutePoint(route, t, across);
                point.y = GroundHeight(point.x, point.z) + 0.16f;
                Color color = Color.Lerp(DeepCobalt, Aqua, 0.25f + Hash01((uint)i + 503u) * 0.38f);
                Add(cloud, point, 0.21f + Hash01((uint)i + 521u) * 0.17f, color, ref seed,
                    i % 113 == 0 ? 2 : 1);
            }
        }

        static void AddPlazas(PointCloud cloud, int count, ref int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int plaza = i % PlazaCenters.Length;
                int sample = i / PlazaCenters.Length;
                float x = (RadicalInverse(sample + 1, 2) - 0.5f) * 39f;
                float z = (RadicalInverse(sample + 1, 3) - 0.5f) * 31f;
                float rotation = (plaza % 2 == 0 ? -1f : 1f) * 0.18f;
                float rx = x * Mathf.Cos(rotation) - z * Mathf.Sin(rotation);
                float rz = x * Mathf.Sin(rotation) + z * Mathf.Cos(rotation);
                float px = PlazaCenters[plaza].x + rx;
                float pz = PlazaCenters[plaza].y + rz;
                Color color = (i % 19 == 0) ? Pearl : Color.Lerp(Cobalt, Azure,
                    0.24f + Hash01((uint)i + 557u) * 0.35f);
                Add(cloud, new Vector3(px, GroundHeight(px, pz) + 0.46f, pz),
                    0.20f + Hash01((uint)i + 569u) * 0.16f, color, ref seed,
                    i % 89 == 0 ? 2 : 0);
            }
        }

        static void AddRuinDistricts(PointCloud cloud, int count, ref int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float u = RadicalInverse(i + 1, 2);
                float v = RadicalInverse(i + 1, 3);
                Vector3 point = SampleRuinPoint(i, u, v);
                int style = i % 131 == 0 ? 2 : i % 8 == 0 ? 1 : 0;
                Color color = style == 2 ? Gold : style == 1 ? Pearl :
                    Color.Lerp(Cobalt, Aqua, Hash01((uint)i + 601u) * 0.43f);
                Add(cloud, point, 0.16f + Hash01((uint)i + 613u) * 0.19f,
                    color, ref seed, style);
            }
        }

        static Vector3 SampleRuinPoint(int sample, float u, float v)
        {
            int site = sample % RuinSites.Length;
            int form = site % 4;
            Vector2 center = RuinSites[site];
            float direction = site * 0.47f + 0.2f;
            float angle = Tau * v + site * 0.19f;
            float x, z, y;
            if (form == 0)
            {
                float radius = 1.25f + u * 1.4f;
                x = center.x + Mathf.Cos(angle) * radius;
                z = center.y + Mathf.Sin(angle) * radius;
                y = GroundHeight(x, z) + 0.8f + v * (8f + site % 7 * 2.4f);
            }
            else if (form == 1)
            {
                float along = (u - 0.5f) * 23f;
                float across = (v - 0.5f) * 1.8f;
                float brokenTop = 4.5f + 5.5f * (0.5f + 0.5f * Mathf.Sin(u * Tau * 1.7f + site));
                x = center.x + Mathf.Cos(direction) * along - Mathf.Sin(direction) * across;
                z = center.y + Mathf.Sin(direction) * along + Mathf.Cos(direction) * across;
                y = GroundHeight(x, z) + 0.5f + v * brokenTop;
            }
            else if (form == 2)
            {
                float along = (u - 0.5f) * 16f;
                float across = (v - 0.5f) * 2.1f;
                x = center.x + Mathf.Cos(direction) * along - Mathf.Sin(direction) * across;
                z = center.y + Mathf.Sin(direction) * along + Mathf.Cos(direction) * across;
                y = GroundHeight(x, z) + 4.5f + Mathf.Sin(u * Mathf.PI) * 6.5f +
                    (v - 0.5f) * 0.55f;
            }
            else
            {
                float radius = 1.7f + (sample % 5) * 0.28f;
                x = center.x + Mathf.Cos(angle) * radius;
                z = center.y + Mathf.Sin(angle) * radius;
                y = GroundHeight(x, z) + 0.8f + u * (12f + site % 5 * 2.1f);
            }
            return new Vector3(x, y, z);
        }

        static void AddTemplePlinth(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int density = reduced ? 105 : 180;
            float[] widths = reduced ? new[] { 74f, 70f, 66f } : new[] { 75f, 72f, 68f };
            float[] depths = reduced ? new[] { 59f, 56f, 53f } : new[] { 60f, 57f, 54f };
            for (int tier = 0; tier < widths.Length; tier++)
            {
                float y = ground + 1.8f + tier * 1.25f;
                Color color = tier == 1 ? Azure : tier == 2 ? Pearl : Cobalt;
                AddRectangle(cloud, widths[tier], depths[tier], y, density,
                    color, tier == 2 ? 0.17f : 0.14f, ref seed, 1);
            }
            AddRectangle(cloud, 63f, 49f, ground + 5.7f, reduced ? 85 : 145,
                Aqua, 0.13f, ref seed, 1);
        }

        static void AddColumnSurfaces(PointCloud cloud, float baseY, float height,
            int columnCount, int aroundCount, int levelCount, float radius, ref int seed)
        {
            int pointCount = columnCount * aroundCount * levelCount;
            for (int i = 0; i < pointCount; i++)
            {
                int column = i % columnCount;
                int sample = i / columnCount;
                int level = sample % levelCount;
                int around = (sample / levelCount) % aroundCount;
                Vector3 center = OuterColumnCenter(column, columnCount);
                float t = level / (float)(levelCount - 1);
                float fluteAngle = around * Tau / aroundCount;
                float entasis = 1.14f - 0.14f * t + 0.035f * Mathf.Sin(t * Mathf.PI);
                float flute = 1f + 0.027f * Mathf.Cos(fluteAngle * 18f);
                Vector3 point = new(center.x + Mathf.Cos(fluteAngle) * radius * entasis * flute,
                    baseY + t * height,
                    center.z + Mathf.Sin(fluteAngle) * radius * entasis * flute);
                Color color = around % 8 == 0 ? Pearl : Color.Lerp(Pearl, Azure,
                    0.08f + 0.13f * (1f - t));
                int style = (level % 17 == 0 && around % 4 == 0) ? 2 :
                    level % 6 == 0 ? 1 : 0;
                Add(cloud, point, 0.13f + (level % 3) * 0.012f, color, ref seed, style);
            }
        }

        static Vector3 OuterColumnCenter(int index, int count)
        {
            int longCount = count == 32 ? 10 : 13;
            int shortCount = (count - longCount * 2) / 2;
            if (index < longCount * 2)
            {
                int side = index / longCount;
                int step = index % longCount;
                return new Vector3(-65f + 130f * step / (longCount - 1), 0f,
                    side == 0 ? -48f : 48f);
            }
            int end = (index - longCount * 2) / shortCount;
            int along = (index - longCount * 2) % shortCount;
            return new Vector3(end == 0 ? -65f : 65f, 0f,
                -37f + 74f * along / (shortCount - 1));
        }

        static void AddColumnBasesAndCapitals(PointCloud cloud, float shaftBase, float height,
            int columnCount, int around, ref int seed)
        {
            int rings = 4;
            for (int i = 0; i < columnCount * rings * around; i++)
            {
                int column = i % columnCount;
                int ring = (i / columnCount) % rings;
                int point = (i / (columnCount * rings)) % around;
                Vector3 center = OuterColumnCenter(column, columnCount);
                float theta = point * Tau / around;
                bool capital = ring >= 2;
                float y = capital ? shaftBase + height + 0.7f + (ring - 2) * 0.72f :
                    shaftBase - 1.4f + ring * 0.64f;
                float r = capital ? 2.3f - (ring - 2) * 0.18f : 2.15f - ring * 0.2f;
                Vector3 p = new(center.x + Mathf.Cos(theta) * r, y,
                    center.z + Mathf.Sin(theta) * r);
                bool accent = point % 11 == 0 && (ring == 0 || ring == 3);
                Add(cloud, p, 0.15f, accent ? Gold : ring % 2 == 0 ? Pearl : Aqua,
                    ref seed, accent ? 2 : 1);
            }
        }

        static void AddInnerColumns(PointCloud cloud, float ground, int count,
            bool reduced, ref int seed)
        {
            int around = reduced ? 14 : 18;
            int levels = reduced ? 16 : 20;
            for (int i = 0; i < count * around * levels; i++)
            {
                int column = i % count;
                int sample = i / count;
                int level = sample % levels;
                int angleIndex = (sample / levels) % around;
                Vector3 center = InnerColumnCenter(column, count);
                float angle = angleIndex * Tau / around;
                float t = level / (float)(levels - 1);
                float radius = 1.15f * (1.13f - 0.13f * t);
                Vector3 p = new(center.x + Mathf.Cos(angle) * radius,
                    ground + 6f + t * 20f,
                    center.z + Mathf.Sin(angle) * radius);
                Add(cloud, p, 0.12f, angleIndex % 7 == 0 ? Pearl : Cobalt,
                    ref seed, level % 7 == 0 ? 1 : 0);
            }
            for (int column = 0; column < count; column++)
            {
                Vector3 center = InnerColumnCenter(column, count);
                AddCircularRing(cloud, center, ground + 6f, 1.8f, reduced ? 16 : 24,
                    Pearl, 0.14f, ref seed, 1);
                AddCircularRing(cloud, center, ground + 26.8f, 1.9f, reduced ? 16 : 24,
                    Aqua, 0.14f, ref seed, 1);
            }
        }

        static Vector3 InnerColumnCenter(int index, int count)
        {
            if (count == 8)
            {
                Vector3[] positions = {
                    new(-30f, 0f, -26f), new(0f, 0f, -26f), new(30f, 0f, -26f),
                    new(-30f, 0f, 26f), new(0f, 0f, 26f), new(30f, 0f, 26f),
                    new(-30f, 0f, 0f), new(30f, 0f, 0f)
                };
                return positions[index];
            }
            Vector3[] reducedPositions = {
                new(-30f, 0f, -25f), new(30f, 0f, -25f), new(-30f, 0f, 25f),
                new(30f, 0f, 25f), new(-30f, 0f, 0f), new(30f, 0f, 0f)
            };
            return reducedPositions[index];
        }

        static void AddGrandStairs(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int steps = reduced ? 15 : 20;
            int samples = reduced ? 38 : 70;
            for (int step = 0; step < steps; step++)
            {
                float z = -99f + step * (37f / (steps - 1));
                float y = ground + 0.4f + step * (6.7f / (steps - 1));
                AddSegment(cloud, new Vector3(-27f, y, z), new Vector3(27f, y, z),
                    samples, step % 4 == 0 ? Pearl : Azure, 0.17f, ref seed, 1);
                AddSegment(cloud, new Vector3(-27f, y, z), new Vector3(-27f, y + 0.34f, z),
                    4, Aqua, 0.13f, ref seed, 1);
                AddSegment(cloud, new Vector3(27f, y, z), new Vector3(27f, y + 0.34f, z),
                    4, Aqua, 0.13f, ref seed, 1);
            }
            AddSegment(cloud, new Vector3(-31f, ground + 0.4f, -99f),
                new Vector3(-31f, ground + 7.5f, -62f), reduced ? 38 : 64,
                Azure, 0.22f, ref seed, 1);
            AddSegment(cloud, new Vector3(31f, ground + 0.4f, -99f),
                new Vector3(31f, ground + 7.5f, -62f), reduced ? 38 : 64,
                Azure, 0.22f, ref seed, 1);
        }

        static void AddEntablature(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int count = reduced ? 78 : 142;
            float[] levels = { ground + 36.7f, ground + 38.2f, ground + 40.1f, ground + 42.7f };
            for (int level = 0; level < levels.Length; level++)
                AddRectangle(cloud, 74f - level * 0.8f, 59f - level * 0.7f,
                    levels[level], count, level == 1 ? Pearl : level == 2 ? Aqua : Cobalt,
                    level == 3 ? 0.17f : 0.13f, ref seed, level == 2 ? 1 : 0);
        }

        static void AddFrieze(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int motifs = reduced ? 34 : 52;
            int around = reduced ? 8 : 12;
            for (int motif = 0; motif < motifs; motif++)
            {
                Vector3 p = TemplePerimeterPoint(motif / (float)motifs, 70f, 55f, ground + 40.8f);
                for (int stroke = -1; stroke <= 1; stroke++)
                {
                    Vector3 offset = new(stroke * 0.32f, 0f, 0f);
                    AddSegment(cloud, p + offset + Vector3.down * 0.9f,
                        p + offset + Vector3.up * 0.9f, 3, stroke == 0 ? Pearl : Azure,
                        0.12f, ref seed, stroke == 0 ? 1 : 0);
                }
                AddCircularRing(cloud, p + Vector3.up * 0.08f, 0f, 0.36f, around,
                    motif % 9 == 0 ? Gold : Aqua, 0.11f, ref seed, motif % 9 == 0 ? 2 : 1,
                    horizontal: false);
            }
        }

        static void AddPediments(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int samples = reduced ? 78 : 150;
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? -58f : 58f;
                float direction = face == 0 ? 1f : -1f;
                float baseline = ground + 43.4f;
                Vector3 left = new(-73f, baseline, z);
                Vector3 peak = new(0f, baseline + 16f, z);
                Vector3 right = new(73f, baseline, z);
                AddSegment(cloud, left, peak, samples, Pearl, 0.16f, ref seed, 1);
                AddSegment(cloud, peak, right, samples, Azure, 0.16f, ref seed, 1);
                AddSegment(cloud, left, right, samples * 2, Cobalt, 0.13f, ref seed, 0);
                AddSegment(cloud, new Vector3(-56f, baseline + 1.3f, z - direction * 0.25f),
                    new Vector3(0f, baseline + 13.6f, z - direction * 0.25f),
                    samples / 2, Aqua, 0.11f, ref seed, 1);
                AddSegment(cloud, new Vector3(0f, baseline + 13.6f, z - direction * 0.25f),
                    new Vector3(56f, baseline + 1.3f, z - direction * 0.25f),
                    samples / 2, Aqua, 0.11f, ref seed, 1);
                for (int ornament = -2; ornament <= 2; ornament++)
                {
                    float x = ornament * 12f;
                    float y = baseline + 2.2f + (1f - Mathf.Abs(ornament) / 2f) * 5.5f;
                    Add(cloud, new Vector3(x, y, z - direction * 0.65f), 0.17f,
                        ornament == 0 ? Gold : Pearl, ref seed, ornament == 0 ? 2 : 1);
                }
            }
        }

        static void AddRoofSurfaces(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int count = reduced ? 4000 : 12000;
            float baseline = ground + 43.4f;
            for (int i = 0; i < count; i++)
            {
                float x = (RadicalInverse(i + 1, 2) * 2f - 1f) * 73f;
                float z = (RadicalInverse(i + 1, 3) * 2f - 1f) * 58f;
                float ridge = 1f - Mathf.Abs(x) / 73f;
                float y = baseline + ridge * 16f;
                float relief = 0.5f + 0.5f * Mathf.Sin(x * 1.37f + z * 0.83f);
                Color color = Color.Lerp(Azure, Pearl, 0.12f + ridge * 0.34f + relief * 0.08f);
                int role = i % 251 == 0 ? 2 : ridge > .98f || Mathf.Abs(z) > 56.5f ? 1 : 0;
                Add(cloud, new Vector3(x, y + relief * .1f, z), .23f + relief * .08f,
                    color, ref seed, role);
            }

            int facadeCount = reduced ? 600 : 1800;
            for (int i = 0; i < facadeCount; i++)
            {
                float spread = Mathf.Sqrt(RadicalInverse(i / 2 + 1, 2));
                float x = (RadicalInverse(i / 2 + 1, 3) * 2f - 1f) * 73f * spread;
                float y = baseline + (1f - spread) * 16f;
                float carved = 0.5f + 0.5f * Mathf.Sin(x * .22f + y * .68f);
                float z = (i % 2 == 0 ? -58f : 58f) + (i % 2 == 0 ? -.3f : .3f) * carved;
                Add(cloud, new Vector3(x, y, z), .22f,
                    Color.Lerp(Azure, Pearl, .22f + carved * .26f), ref seed,
                    carved > .93f ? 1 : 0);
            }
        }

        static void AddInteriorRelief(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int count = reduced ? 44 : 76;
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = side switch
                {
                    0 => new Vector3(-38f, ground + 7f, -29f),
                    1 => new Vector3(38f, ground + 7f, -29f),
                    2 => new Vector3(38f, ground + 7f, 29f),
                    _ => new Vector3(-38f, ground + 7f, 29f)
                };
                Vector3 b = side switch
                {
                    0 => new Vector3(38f, ground + 7f, -29f),
                    1 => new Vector3(38f, ground + 7f, 29f),
                    2 => new Vector3(-38f, ground + 7f, 29f),
                    _ => new Vector3(-38f, ground + 7f, -29f)
                };
                AddSegment(cloud, a, b, count, side % 2 == 0 ? Aqua : Pearl,
                    0.11f, ref seed, 1);
                AddSegment(cloud, a + Vector3.up * 17f, b + Vector3.up * 17f,
                    count, Cobalt, 0.12f, ref seed, 0);
            }
            for (int i = 0; i < (reduced ? 160 : 360); i++)
            {
                float angle = Tau * RadicalInverse(i + 1, 2);
                float radius = 3.2f + (i % 5) * 0.55f;
                Vector3 p = new(Mathf.Cos(angle) * radius, ground + 8f + (i % 7) * 0.9f,
                    Mathf.Sin(angle) * radius - 4f);
                Add(cloud, p, 0.13f, i % 19 == 0 ? Gold : Azure, ref seed,
                    i % 19 == 0 ? 2 : 1);
            }
            AddAcroteria(cloud, ground, reduced, ref seed);
        }

        static void AddAcroteria(PointCloud cloud, float ground, bool reduced, ref int seed)
        {
            int count = reduced ? 16 : 28;
            for (int i = 0; i < count; i++)
            {
                float x = -70f + i * (140f / (count - 1));
                float y = ground + 43.8f + Mathf.Max(0f, 15.5f - Mathf.Abs(x) * 0.215f);
                float z = i % 2 == 0 ? -58.5f : 58.5f;
                AddSegment(cloud, new Vector3(x - 1.4f, y - 1.2f, z),
                    new Vector3(x, y + 1.3f, z), 5, i % 7 == 0 ? Gold : Pearl,
                    0.12f, ref seed, i % 7 == 0 ? 2 : 1);
                AddSegment(cloud, new Vector3(x, y + 1.3f, z),
                    new Vector3(x + 1.4f, y - 1.2f, z), 5, Azure, 0.12f, ref seed, 1);
            }
        }

        static void AddSchoolFish(PointCloud cloud, Vector3 center, Vector3 forward, Vector3 side,
            int school, int fish, bool reduced, ref int seed)
        {
            Color body = fish % 13 == 0 ? new Color(0.29f, 0.36f, 0.34f) :
                Color.Lerp(Cobalt, Aqua, ((fish * 3 + school * 5) % 17) / 20f);
            int segments = reduced ? 9 : 11;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1);
                float z = 1.55f - t * 3.1f;
                float width = 0.12f + Mathf.Sin(t * Mathf.PI) * 0.56f;
                Add(cloud, center + forward * z, 0.095f, body, ref seed, school);
                Add(cloud, center + forward * z + side * width, 0.105f, body, ref seed, school);
                Add(cloud, center + forward * z - side * width, 0.105f, body, ref seed, school);
            }
            int tailSegments = reduced ? 3 : 4;
            for (int i = 0; i < tailSegments; i++)
            {
                float t = i / (float)(tailSegments - 1);
                float z = -1.25f - t * 0.78f;
                float flare = Mathf.Sin(t * Mathf.PI) * 0.48f;
                Add(cloud, center + forward * z + side * flare, 0.088f, Aqua, ref seed, school);
                Add(cloud, center + forward * z - side * flare, 0.088f, Aqua, ref seed, school);
            }
            int finSegments = reduced ? 2 : 4;
            for (int i = 0; i < finSegments; i++)
            {
                float t = i / (float)(finSegments - 1);
                Add(cloud, center + forward * (0.28f - t * 0.92f) + Vector3.up * (0.2f + t * 0.42f),
                    0.08f, i == finSegments - 1 ? Pearl : body, ref seed, school);
            }
        }

        static void AddSubmarineHull(PointCloud cloud, Vector3 center, float heading,
            float length, float width, float height, int slices, int around, Color hull,
            int craft, ref int seed)
        {
            Quaternion rotation = Quaternion.Euler(0f, heading * Mathf.Rad2Deg, 0f);
            for (int slice = 0; slice < slices; slice++)
            {
                float u = (slice + 0.5f) / slices;
                float z = (u - 0.5f) * length;
                float envelope = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.62f);
                for (int aroundIndex = 0; aroundIndex < around; aroundIndex++)
                {
                    float angle = aroundIndex * Tau / around;
                    Vector3 local = new(Mathf.Cos(angle) * width * envelope,
                        Mathf.Sin(angle) * height * envelope, z);
                    Vector3 point = center + rotation * local;
                    Color color = aroundIndex % 17 == 0 ? Pearl :
                        Color.Lerp(hull, Azure, 0.08f + 0.19f * (0.5f + 0.5f * Mathf.Sin(angle * 3f)));
                    Add(cloud, point, aroundIndex % 17 == 0 ? 0.19f : 0.15f,
                        color, ref seed, craft);
                }
            }
        }

        static void AddSubmarineDetails(PointCloud cloud, Vector3 center, float heading,
            float length, float width, float height, bool stage, bool reduced, Color hull,
            int craft, ref int seed)
        {
            Quaternion rotation = Quaternion.Euler(0f, heading * Mathf.Rad2Deg, 0f);
            float detailScale = stage ? 1f : length / 15f;
            int ringCount = reduced ? (stage ? 64 : 36) : (stage ? 104 : 52);
            int ribCount = stage ? (reduced ? 5 : 9) : (reduced ? 3 : 5);
            for (int rib = 0; rib < ribCount; rib++)
            {
                float u = 0.18f + rib * (0.64f / Mathf.Max(1, ribCount - 1));
                float z = (u - 0.5f) * length;
                float envelope = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.62f);
                for (int i = 0; i < ringCount; i++)
                {
                    float angle = i * Tau / ringCount;
                    Vector3 local = new(Mathf.Cos(angle) * width * envelope * 1.01f,
                        Mathf.Sin(angle) * height * envelope * 1.02f, z);
                    Add(cloud, center + rotation * local, 0.13f,
                        i % 13 == 0 ? Gold : Aqua, ref seed, craft);
                }
            }

            int portholes = stage ? 8 : 5;
            int portholePoints = reduced ? 10 : 16;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int porthole = 0; porthole < portholes; porthole++)
                {
                    float z = length * 0.34f - porthole * (length * 0.68f / Mathf.Max(1, portholes - 1));
                    Vector3 portCenter = new(side * width * 0.91f, height * 0.22f, z);
                    for (int point = 0; point < portholePoints; point++)
                    {
                        float angle = point * Tau / portholePoints;
                        Vector3 local = portCenter + new Vector3(0f,
                            Mathf.Cos(angle) * (stage ? 0.55f : 0.31f * detailScale),
                            Mathf.Sin(angle) * (stage ? 0.75f : 0.43f * detailScale));
                        Add(cloud, center + rotation * local, 0.15f,
                            point % 4 == 0 ? Pearl : Gold, ref seed, craft);
                    }
                }
            }

            float towerWidth = stage ? 2.4f : 1.15f * detailScale;
            float towerBase = height * 0.77f;
            float towerTop = height + (stage ? 4.4f : 2.2f * detailScale);
            float towerZ = 0.7f * detailScale;
            AddSegment(cloud, center + rotation * new Vector3(-towerWidth, towerBase, towerZ),
                center + rotation * new Vector3(-towerWidth * 0.62f, towerTop, towerZ),
                reduced ? 18 : 34, hull, 0.16f, ref seed, craft);
            AddSegment(cloud, center + rotation * new Vector3(towerWidth, towerBase, towerZ),
                center + rotation * new Vector3(towerWidth * 0.62f, towerTop, towerZ),
                reduced ? 18 : 34, hull, 0.16f, ref seed, craft);
            AddSegment(cloud, center + rotation * new Vector3(-towerWidth * 0.62f, towerTop, towerZ),
                center + rotation * new Vector3(towerWidth * 0.62f, towerTop, towerZ),
                reduced ? 22 : 40, Pearl, 0.15f, ref seed, craft);
            AddSegment(cloud, center + rotation * new Vector3(0f, towerTop, towerZ),
                center + rotation * new Vector3(0f, towerTop + (stage ? 2.2f : 1.15f * detailScale), towerZ),
                reduced ? 10 : 20, Gold, 0.11f, ref seed, craft);

            for (int fin = -1; fin <= 1; fin += 2)
            {
                Vector3 root = new(fin * width * 0.55f, -height * 0.12f, length * 0.34f);
                Vector3 tip = new(fin * width * 1.55f, -height * 0.08f, length * 0.48f);
                Vector3 rear = new(fin * width * 0.2f, -height * 0.15f, length * 0.49f);
                AddSegment(cloud, center + rotation * root, center + rotation * tip,
                    reduced ? 16 : 30, hull, 0.13f, ref seed, craft);
                AddSegment(cloud, center + rotation * tip, center + rotation * rear,
                    reduced ? 12 : 22, Gold, 0.11f, ref seed, craft);
                AddSegment(cloud, center + rotation * rear, center + rotation * root,
                    reduced ? 12 : 22, Azure, 0.12f, ref seed, craft);
            }
        }

        static Vector3 CityRoutePoint(int route, float t, float across)
        {
            Vector3 a, b, c, d;
            switch (route)
            {
                case 0: a = new(-234f, 0f, -72f); b = new(-193f, 0f, -28f); c = new(-138f, 0f, -18f); d = new(-86f, 0f, 4f); break;
                case 1: a = new(210f, 0f, 118f); b = new(176f, 0f, 140f); c = new(130f, 0f, 118f); d = new(80f, 0f, 60f); break;
                case 2: a = new(-18f, 0f, -236f); b = new(-38f, 0f, -193f); c = new(-96f, 0f, -148f); d = new(-63f, 0f, -84f); break;
                case 3: a = new(66f, 0f, 232f); b = new(42f, 0f, 178f); c = new(4f, 0f, 128f); d = new(20f, 0f, 72f); break;
                case 4: a = new(-226f, 0f, 34f); b = new(-174f, 0f, 76f); c = new(-123f, 0f, 43f); d = new(-83f, 0f, 18f); break;
                case 5: a = new(205f, 0f, -132f); b = new(154f, 0f, -137f); c = new(104f, 0f, -104f); d = new(48f, 0f, -66f); break;
                default: a = new(-155f, 0f, 185f); b = new(-136f, 0f, 161f); c = new(-94f, 0f, 115f); d = new(-52f, 0f, 78f); break;
            }
            float oneMinus = 1f - t;
            Vector3 point = oneMinus * oneMinus * oneMinus * a +
                3f * oneMinus * oneMinus * t * b + 3f * oneMinus * t * t * c + t * t * t * d;
            Vector3 tangent = 3f * oneMinus * oneMinus * (b - a) +
                6f * oneMinus * t * (c - b) + 3f * t * t * (d - c);
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x).normalized;
            return point + side * across;
        }

        static float CoastRadius(float angle) => 270f +
            9f * Mathf.Sin(angle * 5f + 0.37f) +
            5f * Mathf.Sin(angle * 9f - 1.1f) +
            3f * Mathf.Sin(angle * 15f + 0.82f);

        static float SmoothStep(float from, float to, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((value - from) / (to - from)));

        static void AddRectangle(PointCloud cloud, float halfX, float halfZ, float y,
            int countPerSide, Color color, float size, ref int seed, int style)
        {
            Vector3[] corners = {
                new(-halfX, y, -halfZ), new(halfX, y, -halfZ),
                new(halfX, y, halfZ), new(-halfX, y, halfZ), new(-halfX, y, -halfZ)
            };
            for (int side = 0; side < 4; side++)
                AddSegment(cloud, corners[side], corners[side + 1], countPerSide,
                    color, size, ref seed, style);
        }

        static Vector3 TemplePerimeterPoint(float t, float halfX, float halfZ, float y)
        {
            float perimeter = 4f * (halfX + halfZ);
            float distance = Mathf.Repeat(t, 1f) * perimeter;
            float first = 2f * halfX;
            float second = first + 2f * halfZ;
            float third = second + 2f * halfX;
            if (distance < first) return new Vector3(-halfX + distance, y, -halfZ);
            if (distance < second) return new Vector3(halfX, y, -halfZ + distance - first);
            if (distance < third) return new Vector3(halfX - (distance - second), y, halfZ);
            return new Vector3(-halfX, y, halfZ - (distance - third));
        }

        static void AddCircularRing(PointCloud cloud, Vector3 center, float y, float radius,
            int count, Color color, float size, ref int seed, int style,
            bool horizontal = true)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Tau / count;
                Vector3 offset = horizontal ?
                    new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius) :
                    new Vector3(0f, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                Add(cloud, center + offset, size, color, ref seed,
                    i % 19 == 0 && style == 1 ? 2 : style);
            }
        }

        static void AddSegment(PointCloud cloud, Vector3 from, Vector3 to, int count,
            Color color, float size, ref int seed, int style = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count < 2 ? 0.5f : i / (float)(count - 1);
                Add(cloud, Vector3.Lerp(from, to, t), size, color, ref seed, style);
            }
        }

        static void Add(PointCloud cloud, Vector3 point, float size, Color color, ref int seed,
            int style = 0)
        {
            cloud.Add(point, size, color, Mathf.Repeat(seed++ * 0.6180339f, 1f), style);
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
