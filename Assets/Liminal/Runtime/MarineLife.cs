using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class MarineLife : MonoBehaviour
    {
        const int JellyTotal = 15;
        const int SchoolTotal = 7;
        const int FishPerSchool = 28;
        const int WhaleRounds = 2;
        const int WhaleRingCount = PersistentMatter.WhaleRingCount;
        const int WhaleTargetsPerRing = PersistentMatter.WhaleTargetsPerRing;
        public const int WhaleOrganCount = PersistentMatter.WhalePatchCount;
        public const int WhaleDamageGoal = WhaleOrganCount * WhaleRounds;
        const int AmbientCount = 16000;
        const float FishScatterSeconds = 8f;
        const float JellySettleSeconds = 10f;
        const int JellyKind = 1, FishKind = 2, WhaleKind = 3, AmbientKind = 0;
        static readonly Color Aqua = new(0.24f, 1f, 0.87f);
        static readonly Color Pearl = new(0.76f, 0.94f, 1f);
        static readonly Color Blue = new(0.28f, 0.63f, 1f);

        sealed class Jelly
        {
            public GameObject anchor;
            public LockTarget target;
            public Vector3 home, frozenPosition;
            public Quaternion rotation, frozenRotation;
            public int group, hits;
            public float phase, age, scale;
            public bool depleted;
        }

        sealed class Fish
        {
            public GameObject anchor;
            public LockTarget target;
            public Vector3 position, velocity, homeOffset, scatterOrigin, scatterVelocity;
            public Quaternion rotation, homeRotation;
            public int group, school;
            public float phase, scatterAge;
            public bool scattering;
        }

        sealed class School
        {
            public Vector3 home;
            public float phase;
            public readonly List<Fish> fish = new();
        }

        readonly List<Jelly> jellies = new();
        readonly List<School> schools = new();
        readonly List<Fish> fish = new();
        readonly List<LockTarget> jellyTargets = new();
        readonly List<LockTarget> fishTargets = new();
        readonly List<LockTarget> whaleTargets = new();
        readonly List<GameObject> whaleAnchors = new();
        readonly HashSet<int> litJellies = new();
        readonly HashSet<int> depletedJellies = new();
        readonly HashSet<int> litResonators = new();
        readonly float[] whaleOrganHeat = new float[WhaleOrganCount];
        readonly Vector4[] whaleOrganPatches = new Vector4[WhaleOrganCount];
        readonly List<MatterSeed> seeds = new();
        readonly List<Matrix4x4> initialMatrices = new();
        PersistentMatter matter;
        ParticleWorld world;
        Encounter combat;
        GameObject whaleRoot;
        Vector3 whalePosition, whaleVelocity, previousPlayer;
        Quaternion whaleRotation = Quaternion.identity;
        int whaleGroup, ambientGroup, fishScatteringCount, fishRegroupingCount;
        float whalePulse, whaleReleaseAt, whaleRoundReadyAt = -1, whaleTurn, jellyImpactAge = -100;
        int whaleDamage, whaleRound = 1;
        Vector3 jellyImpactPoint;
        int jellyImpactIndex = -1;
        bool whaleReleased;
        bool whalePoseStarted, playerPoseStarted;
        Matrix4x4 whaleReleaseMatrix;

        public PersistentMatter Matter => matter;
        public int IlluminatedJellies => litJellies.Count;
        public int JellyCount => jellies.Count;
        public int CompletedJellies => depletedJellies.Count;
        public int SettledJellies { get; private set; }
        public int FishResponses { get; private set; }
        public int FishScatteringCount => fishScatteringCount;
        public int FishRegroupingCount => fishRegroupingCount;
        public int WhaleResonance => whaleDamage;
        public int WhaleRound => whaleRound;
        public bool WhaleWaveReady => !whaleReleased && whaleRoundReadyAt == -1f;
        public float WhaleOrganHeat(int index) => whaleOrganHeat[index];
        public bool WhaleReleased => whaleReleased;
        public Vector3 WhalePosition => whalePosition;
        public Vector3 WhaleVelocity => whaleVelocity;
        public float WhaleSurfaceActivity { get; private set; }
        public Quaternion WhaleRotation => whaleRotation;
        public IReadOnlyList<LockTarget> JellyTargets => jellyTargets;
        public IReadOnlyList<LockTarget> FishTargets => fishTargets;
        public IReadOnlyList<LockTarget> WhaleResonatorTargets => whaleTargets;
        public int ParticleCount => matter != null ? matter.ParticleCount : seeds.Count;
        public int FirstJellyGroup => jellies.Count > 0 ? jellies[0].group : -1;
        public int WhaleGroup => whaleGroup;
        public MatterSeed SeedAt(int particleId) => seeds[particleId];

        public void Initialize(ParticleWorld particleWorld, Encounter encounter)
        {
            world = particleWorld;
            combat = encounter;
            EvaluateWhalePose(0, out whalePosition, out whaleRotation);
            if (!world.matterSimulation || !world.matterLight)
                throw new InvalidOperationException("MarineLife requires ParticleWorld.matterSimulation and matterLight.");
            BuildJellies();
            BuildSchools();
            BuildWhaleSeeds();
            BuildAmbientSeeds();
            matter = new PersistentMatter();
            matter.Initialize(world.matterSimulation, world.matterLight, seeds, initialMatrices.Count);
            matter.SetWhaleGroup(whaleGroup);
            whaleRoot = new GameObject("THE HORIZON WHALE / transform");
            whaleRoot.transform.SetParent(transform, false);
            BuildWhaleTargets();
            ResetLife();
        }

        void BuildJellies()
        {
            var room = CaveLayout.Rooms[0];
            for (int i = 0; i < JellyTotal; i++)
            {
                float lane = (i % 5 - 2) * 22f;
                Vector3 home = room.Center + new Vector3(lane, 14f + (i % 3) * 14f, -57f + i / 5 * 37f);
                if (i < 4) home = CaveLayout.Spawn + new Vector3((i - 1.5f) * 10f, 4f + i * 2f, 20f + i * 18f);
                float scale = i < 4 ? 1.25f - i * 0.09f : 0.72f + (i % 4) * 0.12f;
                int group = NewGroup(Matrix4x4.TRS(home, Quaternion.identity, Vector3.one * scale));
                AddJellySeeds(group, i);
                var anchor = NewAnchor("Lantern jelly target " + i, home);
                jellies.Add(new Jelly { anchor = anchor, home = home, rotation = Quaternion.identity,
                    group = group, phase = i * 1.71f, scale = scale });
            }
        }

        void AddJellySeeds(int group, int index)
        {
            int ribs = 30 + index % 9;
            for (int r = 0; r < ribs; r++)
            {
                float a = r * Mathf.PI * 2 / ribs;
                for (int j = 0; j <= 20; j++)
                {
                    float u = j / 20f, radius = Mathf.Sin(u * Mathf.PI * 0.5f) * 4.6f;
                    Vector3 p = new(Mathf.Cos(a) * radius, 0.25f + Mathf.Cos(u * Mathf.PI * 0.5f) * 3.5f, Mathf.Sin(a) * radius);
                    AddSeed(group, p, 0.075f, Color.Lerp(Aqua, Pearl, u * 0.65f) * (0.56f + 0.44f * u),
                        JellyKind, index * 0.31f + r * 0.13f, u, JellyGarden(index, r, u));
                }
            }
            for (int t = 0; t < 16 + index % 5; t++)
            {
                float a = t * Mathf.PI * 2 / (16 + index % 5), length = 8f + (t * 17 % 9) * 1.15f;
                for (int j = 0; j < 56; j++)
                {
                    float u = j / 55f, sway = Mathf.Sin(u * 5.2f + t * 1.7f) * u * 1.25f;
                    Vector3 p = new(Mathf.Cos(a) * (2.2f + u * 0.6f) + Mathf.Cos(a + Mathf.PI / 2) * sway,
                        -0.15f - u * length, Mathf.Sin(a) * (2.2f + u * 0.6f) + Mathf.Sin(a + Mathf.PI / 2) * sway);
                    AddSeed(group, p, Mathf.Lerp(0.052f, 0.024f, u), Color.Lerp(Aqua, Pearl, u * 0.45f) * (1 - u * 0.35f),
                        JellyKind, t * 0.7f, u, JellyGarden(index, ribs + t, u));
                }
            }
            for (int j = 0; j < 180; j++)
            {
                float a = j * 2.399f, y = 0.2f + (j % 18) * 0.22f, radius = (j % 18) * 0.20f;
                Vector3 p = new(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                AddSeed(group, p, 0.075f, Color.Lerp(Pearl, Aqua, (j % 18) / 18f), JellyKind,
                    j * 0.18f, 0, JellyGarden(index, ribs + 40 + j / 60, (j % 60) / 59f));
            }
        }

        static Vector3 JellyGarden(int jelly, int strand, float u)
        {
            var room = CaveLayout.Rooms[0];
            float x = (jelly % 5 - 2) * 30f, z = -55f + jelly / 5 * 45f;
            float floor = -room.Radius.y * Mathf.Sqrt(1-x*x/(room.Radius.x*room.Radius.x)-z*z/(room.Radius.z*room.Radius.z));
            Vector3 root = room.Center + new Vector3(x, floor+7f, z);
            float angle = strand * 2.399f + jelly * 0.71f;
            float spread = Mathf.Pow(u,1.4f) * (5f + strand % 5);
            return root + new Vector3(Mathf.Cos(angle+u*.65f)*spread,
                u*(17f+strand%7*1.2f)+Mathf.Sin(u*Mathf.PI)*2,Mathf.Sin(angle+u*.65f)*spread);
        }

        void BuildSchools()
        {
            Vector3[] homes = {
                CaveLayout.Rooms[0].Center + new Vector3(36,-18,28),
                CaveLayout.Rooms[0].Center + new Vector3(-44,4,65),
                CaveLayout.Rooms[1].Center + new Vector3(60,15,-55),
                CaveLayout.Rooms[1].Center + new Vector3(-80,-20,75),
                (CaveLayout.Passages[1][1]+CaveLayout.Passages[1][2])*0.5f,
                CaveLayout.Rooms[2].Center+new Vector3(-105,28,-72),
                CaveLayout.Rooms[2].Center+new Vector3(96,-48,112)
            };
            for (int s = 0; s < SchoolTotal; s++)
            {
                var school = new School { home = homes[s], phase = s * 1.93f };
                for (int f = 0; f < FishPerSchool; f++)
                {
                    float a = f * 2.399f + s * 0.31f, shell = Mathf.Sqrt((f + 0.5f) / FishPerSchool);
                    Vector3 offset = new(Mathf.Cos(a) * shell * 13f, Mathf.Sin(f * 1.31f + s) * 2.7f,
                        Mathf.Sin(a) * shell * 18f - shell * 6f);
                    Quaternion yaw = Quaternion.Euler(0, Mathf.Sin(a) * 14f, 0);
                    Vector3 start = school.home + offset;
                    int group = NewGroup(Matrix4x4.TRS(start, yaw, Vector3.one));
                    AddFishSeeds(group, s, f, yaw);
                    Fish item = new() { school = s, group = group, phase = f * 0.47f + s,
                        homeOffset = offset, position = start, rotation = yaw, homeRotation = yaw,
                        anchor = NewAnchor("School " + s + " fish " + f, start) };
                    school.fish.Add(item); fish.Add(item);
                }
                schools.Add(school);
            }
        }

        void AddFishSeeds(int group, int school, int fishIndex, Quaternion yaw)
        {
            Color silver = Color.Lerp(Pearl, new Color(0.32f, 0.89f, 1f), ((fishIndex + school) % 7) / 12f);
            for (int j = 0; j < 18; j++)
            {
                float z = 1.05f - j * 0.115f, taper = 1 - Mathf.Abs(z) * 0.52f;
                AddSeed(group, new Vector3(0, 0.03f, z), 0.10f * taper, silver, FishKind, fishIndex * 0.47f, 0,
                    Vector3.zero);
                if (j < 12)
                    AddSeed(group, new Vector3((j % 2 == 0 ? 1 : -1) * (0.12f + 0.1f * taper), 0, z),
                        0.075f * taper, silver, FishKind, fishIndex, 0, Vector3.zero);
            }
            for (int j = 0; j < 9; j++)
            {
                float u = j / 8f, z = -1.0f - u * 1.1f, width = Mathf.Sin(u * Mathf.PI) * 0.42f;
                AddSeed(group, new Vector3(width, 0, z), 0.07f, Color.Lerp(silver, Aqua, 0.35f), FishKind, u, 1, Vector3.zero);
                AddSeed(group, new Vector3(-width, 0, z), 0.07f, Color.Lerp(silver, Aqua, 0.35f), FishKind, u, 1, Vector3.zero);
                AddSeed(group, new Vector3(0, 0.23f + u * 0.05f, z), 0.06f, silver, FishKind, u, 1, Vector3.zero);
            }
            AddSeed(group, new Vector3(0, 0.05f, 0.78f), 0.055f, new Color(0.06f, 0.18f, 0.23f), FishKind, 0, 0, Vector3.zero);
        }

        void BuildWhaleSeeds()
        {
            EvaluateWhalePose(0, out Vector3 initial, out Quaternion initialRotation);
            whaleRotation = initialRotation;
            whaleGroup = NewGroup(Matrix4x4.TRS(initial, whaleRotation, Vector3.one));
            var random = new System.Random(72941);
            const int body = 86000;
            const int profileSteps = 256;
            const float minZ = -78f, maxZ = 75f;
            var surfaceArea = new float[profileSteps + 1];
            float previousWeight = WhaleSurfaceWeight(minZ);
            for (int step = 1; step <= profileSteps; step++)
            {
                float z = Mathf.Lerp(minZ, maxZ, step / (float)profileSteps);
                float weight = WhaleSurfaceWeight(z);
                surfaceArea[step] = surfaceArea[step - 1] + (previousWeight + weight) *
                    0.5f * ((maxZ - minZ) / profileSteps);
                previousWeight = weight;
            }

            for (int i = 0; i < body; i++)
            {
                float quantile = (i + 0.2f + WhaleHash(i + 17) * 0.6f) / body;
                float area = quantile * surfaceArea[profileSteps];
                int low = 0, high = profileSteps;
                while (high - low > 1)
                {
                    int middle = (low + high) >> 1;
                    if (surfaceArea[middle] < area) low = middle;
                    else high = middle;
                }
                float segment = Mathf.InverseLerp(surfaceArea[low], surfaceArea[high], area);
                float z = Mathf.Lerp(minZ + (maxZ - minZ) * low / profileSteps,
                    minZ + (maxZ - minZ) * high / profileSteps, segment);
                Vector2 radius = WhaleRadius(z);
                float a = Mathf.Repeat(i * 0.6180339f + (WhaleHash(i + 86017) - 0.5f) * 0.15f, 1f) * Mathf.PI * 2f;
                float shell = 0.94f + WhaleHash(i + 120013) * 0.08f;
                Vector3 p = new(Mathf.Cos(a) * radius.x * shell, Mathf.Sin(a) * radius.y * shell, z);
                float ribPhase = a * 9f + z * 0.13f + Mathf.Sin(z * 0.045f + a * 2.1f) * 0.7f;
                float rib = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ribPhase), 12f);
                float flow = 0.5f + 0.5f * Mathf.Sin(a * 3.2f + z * 0.035f + Mathf.Sin(a * 1.7f - z * 0.02f) * 0.6f);
                Color c = Color.Lerp(new Color(0.014f, 0.055f, 0.19f), new Color(0.025f, 0.22f, 0.49f), 0.24f + flow * 0.38f);
                c = Color.Lerp(c, new Color(0.015f, 0.48f, 0.66f), rib * 0.40f);
                if (Mathf.Sin(a) < -0.58f) c = Color.Lerp(c, new Color(0.14f, 0.055f, 0.34f), 0.24f);
                float seed = WhaleHash(i + 172009);
                float radiusJitter = WhaleHash(i + 215017);
                AddSeed(whaleGroup, p, 0.036f + radiusJitter * 0.034f, c, WhaleKind, seed, Mathf.Abs(z) / 82f,
                    WhaleGarden(i, body));
            }

            // Keep fin and fluke samples stable as body density changes.
            for (int i = 0; i < 60000 * 6; i++) random.NextDouble();
            AddWhaleFinSeeds(new Vector3(-10, -5, 4), -1, random);
            AddWhaleFinSeeds(new Vector3(10, -5, 4), 1, random);
            AddFlukeSeeds(random);
            AddWhaleHeadDetails();
            AddWhaleLuminousLines();
            ambientGroup = NewGroup(Matrix4x4.identity);
        }

        void AddWhaleLuminousLines()
        {
            // A small continuous layer supplies anatomy and moving light among quiet surface grains.
            for(int strand=0;strand<8;strand++) for(int j=0;j<480;j++) {
                float z=Mathf.Lerp(-74f,74f,j/479f);
                float angle=strand*Mathf.PI*.25f+Mathf.Sin(z*.028f+strand*.4f)*.20f+z*.006f;
                Vector2 radius=WhaleRadius(z)*1.035f;
                Vector3 point=new(Mathf.Cos(angle)*radius.x,Mathf.Sin(angle)*radius.y,z);
                AddSeed(whaleGroup,point,.14f,new Color(.08f,.75f,1f),WhaleKind,strand*.127f,2f,WhaleGarden(seeds.Count,0));
            }
        }

        void AddWhaleFinSeeds(Vector3 root, int side, System.Random random)
        {
            for (int j = 0; j < 130; j++)
            {
                float u = (j + (float)random.NextDouble()) / 130f;
                float span = u * 45f;
                float chord = Mathf.Lerp(15f, 0f, Mathf.Pow(u, 0.72f)) + 3.5f * Mathf.Sin(u * Mathf.PI);
                float twist = Mathf.Lerp(0f, -5f, u) + 1.4f * Mathf.Sin(u * Mathf.PI);
                for (int k = 0; k < 33; k++)
                {
                    float v = (k + (float)random.NextDouble()) / 33f;
                    float chordPos = (v - 0.5f) * 2f;
                    float camber = Mathf.Sin(v * Mathf.PI) * (0.9f + chord * 0.11f);
                    float thickness = Mathf.Sin(v * Mathf.PI) * 0.72f;
                    for (int layer = -1; layer <= 1; layer += 2)
                    {
                        Vector3 p = root + new Vector3(side * span, -1.6f + camber + layer * thickness,
                            -u * 22f - chordPos * chord + twist * (0.5f - v));
                        bool rim=v<.035f || v>.965f;
                        AddSeed(whaleGroup, p, rim?.13f:0.065f, Color.Lerp(new Color(0.10f, 0.48f, 0.78f), new Color(0.58f, 0.84f, 0.87f), v) * 0.92f,
                            WhaleKind, u, rim?2f:v, WhaleGarden(seeds.Count, seeds.Capacity));
                    }
                }
            }
        }

        void AddFlukeSeeds(System.Random random)
        {
            for (int side = -1; side <= 1; side += 2) for (int j = 0; j < 4200; j++)
            {
                float u = (float)random.NextDouble(), v = (float)random.NextDouble();
                float x = side * (2f + u * 28f), z = -82f + u * 5f + Mathf.Sin(u * Mathf.PI) * v * 12f;
                float y = 1.2f + Mathf.Sin(u * Mathf.PI) * v * 4.2f;
                AddSeed(whaleGroup, new Vector3(x, y, z), 0.07f,
                    Color.Lerp(new Color(0.25f, 0.55f, 0.95f), Pearl, v * 0.72f), WhaleKind, u, v,
                    WhaleGarden(seeds.Count, seeds.Capacity));
            }
        }

        void AddWhaleHeadDetails()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < 300; j++)
                {
                    float u = j / 299f;
                    AddSeed(whaleGroup, new Vector3(side * (3.5f + u * 4.2f), 2.2f + u * 0.25f, 47 + u * 27),
                        0.085f, Color.Lerp(Pearl, new Color(0.34f, 0.88f, 1f), u) * 0.8f, WhaleKind, u, 0, WhaleGarden(seeds.Count, seeds.Capacity));
                }
                for (int j = 0; j < 72; j++)
                {
                    float a = j * Mathf.PI * 2 / 72;
                    AddSeed(whaleGroup, new Vector3(side * 6.1f + Mathf.Cos(a) * 0.46f, 3.2f + Mathf.Sin(a) * 0.46f, 65.5f),
                        0.11f, Pearl, WhaleKind, a, 0, WhaleGarden(seeds.Count, seeds.Capacity));
                }
            }
        }

        Vector3 WhaleGarden(int index, int total)
        {
            float u = Mathf.Repeat(index * 0.6180339f, 1f);
            float a = index * 2.399963f;
            float radius = 38f + 98f * Mathf.Sqrt(u);
            Vector3 destination = CaveLayout.Rooms[2].Center + new Vector3(Mathf.Cos(a) * radius,
                -125f + Mathf.Sin(a * 2.1f) * 38f, Mathf.Sin(a) * radius - 100f);
            Vector3 velocity = Vector3.zero;
            return CaveLayout.Constrain(destination, ref velocity, 20f);
        }

        void BuildAmbientSeeds()
        {
            var random = new System.Random(11873);
            for (int i = 0; i < AmbientCount; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2;
                float z = ((float)random.NextDouble() * 2 - 1) * 300f;
                float r = Mathf.Sqrt(Mathf.Max(0, 1 - z * z / (300f * 300f))) * Mathf.Sqrt((float)random.NextDouble()) * 570f;
                Vector3 p = CaveLayout.Rooms[2].Center + new Vector3(Mathf.Cos(a) * r, z, Mathf.Sin(a) * r);
                float bright = Mathf.Pow((float)random.NextDouble(), 4);
                Color color = Color.Lerp(new Color(0.12f, 0.34f, 0.48f), Pearl, bright) * 0.48f;
                AddSeed(ambientGroup, p, 0.025f + (float)random.NextDouble() * 0.035f, color,
                    AmbientKind, (float)random.NextDouble(), 0.8f, p);
            }
        }

        void BuildWhaleTargets()
        {
            whaleRoot.transform.SetPositionAndRotation(whalePosition, whaleRotation);
            whaleRoot.transform.localScale = Vector3.one * 1.8f;
            for (int i = 0; i < WhaleOrganCount; i++)
            {
                int ring = i / WhaleTargetsPerRing;
                int quadrant = i % WhaleTargetsPerRing;
                bool lateral = quadrant == 0 || quadrant == 2;
                string region = ring == 0 && lateral ? "fluke" :
                    (ring == 5 || ring == 6) && lateral ? "fin" : ring >= 10 ? "head" : "body";
                Vector3 position = whaleRoot.transform.TransformPoint(WhaleTargetLocal(i, 0f));
                whaleAnchors.Add(NewAnchor("Whale resonator " + (i + 1).ToString("00") + " / " + region, position));
            }
        }

        void AddSeed(int group, Vector3 local, float radius, Color color, int kind, float seed, float tail, Vector3 destination)
        {
            seeds.Add(new MatterSeed {
                form = new Vector4(local.x, local.y, local.z, radius),
                destination = new Vector4(destination.x, destination.y, destination.z, radius),
                color = new Vector4(color.r, color.g, color.b, color.a),
                traits = new Vector4(group, kind, seed, tail)
            });
        }

        int NewGroup(Matrix4x4 matrix)
        {
            int index = initialMatrices.Count;
            initialMatrices.Add(matrix);
            return index;
        }

        GameObject NewAnchor(string name, Vector3 position)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(transform, false);
            anchor.transform.position = position;
            return anchor;
        }

        static Vector2 WhaleRadius(float z)
        {
            float tail = Mathf.Pow(Mathf.Clamp01((z + 86f) / 102f), 0.9f);
            float head = Mathf.Sqrt(Mathf.Clamp01((77f - z) / 27f));
            float width = 18.5f * tail * head;
            return new Vector2(width, width * 0.77f);
        }

        static float WhaleHash(int value)
        {
            unchecked
            {
                uint hash = (uint)value;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777216f;
            }
        }

        static float WhaleSurfaceWeight(float z)
        {
            const float offset = 0.25f;
            Vector2 radius = WhaleRadius(z);
            Vector2 before = WhaleRadius(z - offset), after = WhaleRadius(z + offset);
            float dx = (after.x - before.x) / (offset * 2f);
            float dy = (after.y - before.y) / (offset * 2f);
            float a = radius.x, b = radius.y;
            float perimeter = Mathf.PI * (3f * (a + b) -
                Mathf.Sqrt(Mathf.Max(0f, (3f * a + b) * (a + 3f * b))));
            return perimeter * Mathf.Sqrt(1f + dx * dx + dy * dy);
        }

        static Vector3 WhaleTargetLocal(int index, float song)
        {
            int ring = index / WhaleTargetsPerRing;
            int quadrant = index % WhaleTargetsPerRing;
            float z = Mathf.Lerp(-68f, 68f, ring / (float)(WhaleRingCount - 1));
            float angle = quadrant * Mathf.PI * 0.5f + ((ring & 1) == 0 ? -0.045f : 0.045f);
            Vector2 radius = WhaleRadius(z) * 1.08f;
            Vector3 local = new(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y, z);

            if (ring == 0 && (quadrant == 0 || quadrant == 2))
                local = new Vector3(quadrant == 0 ? 21f : -21f, 3.2f, -74f);
            else if ((ring == 5 || ring == 6) && (quadrant == 0 || quadrant == 2))
            {
                float u = ring == 5 ? 0.48f : 0.23f;
                float chord = Mathf.Lerp(15f, 0f, Mathf.Pow(u, 0.72f)) + 3.5f * Mathf.Sin(u * Mathf.PI);
                local = new Vector3((quadrant == 0 ? 1f : -1f) * (10f + u * 45f),
                    -5f - 1.6f + 0.9f + chord * 0.11f + 0.72f, 4f - u * 22f);
            }

            return DeformWhaleLocal(local, song);
        }

        void RegisterTargets()
        {
            jellyTargets.Clear(); fishTargets.Clear(); whaleTargets.Clear();
            foreach (Jelly jelly in jellies)
            {
                jelly.target = combat.RegisterEnvironment(jelly.anchor, (target, song) => HitJelly(jelly, target, song));
                jellyTargets.Add(jelly.target);
            }
            foreach (Fish swimmer in fish)
            {
                swimmer.target = combat.RegisterEnvironment(swimmer.anchor, (target, song) => HitFish(swimmer, target, song));
                fishTargets.Add(swimmer.target);
            }
            for (int i = 0; i < whaleAnchors.Count; i++)
            {
                int index = i;
                LockTarget target = combat.RegisterEnvironment(whaleAnchors[i], (hitTarget, song) => HitWhale(index, hitTarget, song));
                target.organIndex = index;
                target.acquireRange = 220f;
                target.isWhale = true;
                whaleTargets.Add(target);
            }
        }

        void HitJelly(Jelly jelly, LockTarget target, float song)
        {
            if (jelly.depleted) return;
            jelly.hits++;
            int index = jellies.IndexOf(jelly);
            litJellies.Add(index);
            jellyImpactIndex = index; jellyImpactPoint = target.position; jellyImpactAge = 0;
            if (world.Caverns) world.Caverns.Illuminate(target.position, jelly.hits == 1 ? 1.8f : 1.2f);
            world.BurstAt(target.position, song, Aqua, jelly.hits == 1 ? 1.8f : 1.1f);
            target.hp = 2 - jelly.hits;
            if (jelly.hits >= 2)
            {
                jelly.depleted = true;
                depletedJellies.Add(index);
                jelly.age = 0;
                jelly.frozenPosition = target.position;
                jelly.frozenRotation = jelly.anchor.transform.rotation;
                jelly.target.hp = jelly.target.reserved;
            }
        }

        void HitFish(Fish struck, LockTarget target, float song)
        {
            FishResponses++;
            Vector3 origin = target.position;
            foreach (Fish swimmer in schools[struck.school].fish)
            {
                if (Vector3.Distance(swimmer.position, origin) > 37f) continue;
                Vector3 away = swimmer.position - origin;
                if (away.sqrMagnitude < 0.01f) away = Quaternion.Euler(0, swimmer.phase * 81f, 0) * Vector3.forward;
                swimmer.scatterOrigin = swimmer.position;
                swimmer.scatterVelocity = away.normalized * (22f + 12f * Mathf.Clamp01(1f - away.magnitude / 37f)) + Vector3.up * 2f;
                swimmer.scatterAge = 0;
                swimmer.scattering = true;
                swimmer.target.hp = swimmer.target.reserved;
            }
            fishScatteringCount = CountFishInState(true);
            fishRegroupingCount = 0;
            if (world.Caverns) world.Caverns.Illuminate(origin, 0.75f);
            world.BurstAt(origin, song, Pearl, 0.75f);
        }

        void HitWhale(int index, LockTarget target, float song)
        {
            if (litResonators.Contains(index)) return;
            litResonators.Add(index);
            whaleDamage++;
            whaleOrganHeat[index] = 1f;
            target.hp = target.reserved;
            whalePulse = 1.6f;
            if (world.Caverns) world.Caverns.Illuminate(target.position, 1.45f);
            Color hitColor = Color.Lerp(new Color(1f, 0.30f, 0.42f), new Color(1f, 0.72f, 0.42f), (index % 3) * 0.5f);
            world.BurstAt(target.position, song, hitColor, 1.25f);
            if (litResonators.Count == WhaleOrganCount)
            {
                if (whaleDamage >= WhaleDamageGoal && whaleRound >= WhaleRounds) ReleaseWhale(song);
                else whaleRoundReadyAt = -2f;
            }
        }

        void ReleaseWhale(float song)
        {
            whaleReleased = true;
            whaleReleaseAt = song;
            whaleReleaseMatrix = Matrix4x4.TRS(whalePosition, whaleRotation, Vector3.one * 1.8f);
            Vector3 impulse = whaleVelocity * 0.9f + whaleRotation * Vector3.up * 6f;
            matter.SetGroup(whaleGroup, whaleReleaseMatrix,
                MatterPhase.Transfer, 0, 2.5f, whalePosition, impulse.magnitude);
            matter.SetGroup(ambientGroup, Matrix4x4.identity,
                MatterPhase.Form, 0, 1.2f, whalePosition, 0);
        }

        int CountFishInState(bool scattering)
        {
            int count = 0;
            foreach (Fish swimmer in fish) if (swimmer.scattering == scattering) count++;
            return count;
        }

        public void ResetLife()
        {
            litJellies.Clear(); depletedJellies.Clear(); litResonators.Clear();
            Array.Clear(whaleOrganHeat, 0, whaleOrganHeat.Length);
            Array.Clear(whaleOrganPatches, 0, whaleOrganPatches.Length);
            whaleDamage = 0; whaleRound = 1; whaleRoundReadyAt = -1;
            FishResponses = 0; SettledJellies = 0; whaleReleased = false;
            jellyImpactIndex = -1; jellyImpactAge = -100; whalePulse = 0; whaleReleaseAt = 0;
            fishScatteringCount = fishRegroupingCount = 0;
            whaleVelocity = Vector3.zero;
            WhaleSurfaceActivity = 0f;
            whaleTurn = 0f;
            EvaluateWhalePose(0, out whalePosition, out whaleRotation);
            whaleRoot.transform.SetPositionAndRotation(whalePosition, whaleRotation);
            whaleRoot.transform.localScale = Vector3.one * 1.8f;
            previousPlayer = CaveLayout.Spawn;
            whalePoseStarted = playerPoseStarted = false;
            bool registered = jellyTargets.Count == JellyTotal && combat.Targets.Contains(jellyTargets[0]);
            if (!registered) RegisterTargets();
            for (int i = 0; i < jellies.Count; i++)
            {
                Jelly jelly = jellies[i]; jelly.hits = 0; jelly.depleted = false; jelly.age = 0;
                jelly.anchor.transform.SetPositionAndRotation(jelly.home, jelly.rotation);
                jelly.target.hp = 2; jelly.target.reserved = 0;
            }
            foreach (Fish swimmer in fish)
            {
                swimmer.position = schools[swimmer.school].home + swimmer.homeOffset;
                swimmer.velocity = Vector3.zero; swimmer.scattering = false; swimmer.scatterAge = 0;
                swimmer.rotation = swimmer.homeRotation;
                swimmer.anchor.transform.SetPositionAndRotation(swimmer.position, swimmer.rotation);
                swimmer.target.hp = 1; swimmer.target.reserved = 0;
            }
            for (int i = 0; i < whaleTargets.Count; i++)
            {
                LockTarget target = whaleTargets[i];
                Vector3 position = whaleRoot.transform.TransformPoint(WhaleTargetLocal(i, 0f));
                target.position = position;
                target.visual.transform.SetPositionAndRotation(position, whaleRotation);
                target.hp = 1; target.reserved = 0;
            }
            for (int i = 0; i < initialMatrices.Count; i++)
                matter.SetGroup(i, initialMatrices[i], MatterPhase.Form, 0, 0, Vector3.zero, 0);
            matter.SetCurrent(whalePosition, Vector3.zero, 180f, 0);
            matter.SetWhaleMotion(Vector3.zero, 0f, 0f);
            matter.SetWhalePatches(whaleOrganPatches);
            matter.SetPlayer(CaveLayout.Spawn, Vector3.zero);
            matter.ResetSimulation();
        }

        public void Tick(float song, float dt, Vector3 player)
        {
            if (world == null || combat == null || matter == null) return;
            dt = Mathf.Clamp(dt, 0, 0.05f);
            Shader.SetGlobalFloat("_MarineSong", song);
            TickJellies(song, dt);
            TickFish(song, dt, player);
            TickWhale(song, dt);
            TickWhaleTargets(song);
            Vector3 currentVelocity = whaleVelocity + whaleRotation * Vector3.forward * (whalePulse * 9f);
            float currentEnergy = 0.28f + whalePulse * 1.8f + (whaleReleased ? 0.7f : 0);
            matter.SetCurrent(whalePosition, currentVelocity, whaleReleased ? 210f : 165f, currentEnergy);
            Vector3 travel = player - previousPlayer;
            Vector3 playerVelocity = playerPoseStarted && dt > 0 && travel.sqrMagnitude < 100f
                ? Vector3.ClampMagnitude(travel / dt, 60f) : Vector3.zero;
            matter.SetPlayer(player, playerVelocity);
            previousPlayer = player;
            playerPoseStarted = true;
            matter.Tick(song, dt);
            whalePulse = Mathf.MoveTowards(whalePulse, 0, dt * 0.24f);
        }

        void TickJellies(float song, float dt)
        {
            SettledJellies = 0;
            for (int i = 0; i < jellies.Count; i++)
            {
                Jelly jelly = jellies[i];
                if (!jelly.depleted)
                {
                    Vector3 pos = jelly.home + Vector3.up * (Mathf.Sin(song * 0.38f + jelly.phase) * 2.4f) +
                        new Vector3(Mathf.Sin(song * 0.19f + jelly.phase) * 2f, 0, Mathf.Cos(song * 0.23f + jelly.phase) * 2f);
                    Quaternion rotation = Quaternion.Euler(Mathf.Sin(song * 0.27f + jelly.phase) * 4f,
                        Mathf.Sin(song * 0.12f + jelly.phase) * 9f, Mathf.Sin(song * 0.21f + jelly.phase) * 3f);
                    if (jelly.hits > 0) rotation = Quaternion.Slerp(rotation, Quaternion.identity, 0.45f);
                    jelly.anchor.transform.SetPositionAndRotation(pos, rotation);
                    jelly.target.position = pos;
                    jelly.target.hp = 2 - jelly.hits;
                    jelly.target.visual.transform.SetPositionAndRotation(pos, rotation);
                    float contraction = jelly.hits > 0 ? 0.78f : 1f;
                    float flash = jellyImpactIndex == i && jellyImpactAge < 0.9f ? 4f * (1f - jellyImpactAge / 0.9f) : 0;
                    matter.SetGroup(jelly.group, Matrix4x4.TRS(pos, rotation, Vector3.one * jelly.scale * contraction),
                        MatterPhase.Form, 0, 0.25f + flash, jellyImpactPoint, flash > 0 ? 2.2f : 0);
                }
                else
                {
                    jelly.age += dt;
                    MatterPhase phase = jelly.age >= JellySettleSeconds ? MatterPhase.Settled : MatterPhase.Transfer;
                    matter.SetGroup(jelly.group, Matrix4x4.TRS(jelly.frozenPosition, jelly.frozenRotation, Vector3.one), phase,
                        jelly.age, phase == MatterPhase.Settled ? 0.3f : 1.8f, jelly.frozenPosition, 0.8f);
                    jelly.target.position = jelly.frozenPosition;
                    jelly.target.hp = jelly.target.reserved;
                    if (jelly.age >= JellySettleSeconds) SettledJellies++;
                }
            }
            if (jellyImpactIndex >= 0)
            {
                jellyImpactAge += dt;
                if (jellyImpactAge > 0.8f) jellyImpactIndex = -1;
            }
        }

        void TickFish(float song, float dt, Vector3 player)
        {
            fishScatteringCount = fishRegroupingCount = 0;
            for (int s = 0; s < schools.Count; s++)
            {
                School school = schools[s];
                Vector3 schoolDrift = new(Mathf.Sin(song * 0.21f + school.phase) * 6f,
                    Mathf.Sin(song * 0.31f + school.phase) * 3f, Mathf.Cos(song * 0.18f + school.phase) * 8f);
                for (int f = 0; f < school.fish.Count; f++)
                {
                    Fish swimmer = school.fish[f];
                    Vector3 desired = school.home + schoolDrift + swimmer.homeOffset +
                        new Vector3(0, Mathf.Sin(song * 1.8f + swimmer.phase) * 0.5f, 0);
                    bool wasScattering = swimmer.scattering;
                    if (swimmer.scattering)
                    {
                        swimmer.scatterAge += dt;
                        float t = swimmer.scatterAge;
                        if (t < 1.2f)
                        {
                            swimmer.velocity = Vector3.Lerp(swimmer.scatterVelocity, Vector3.zero, t / 1.2f) +
                                Vector3.up * Mathf.Sin(t * 5f + swimmer.phase) * 0.8f;
                            desired = swimmer.position + swimmer.velocity * dt;
                            fishScatteringCount++;
                        }
                        else
                        {
                            float regroup = Mathf.Clamp01((t - 1.2f) / (FishScatterSeconds - 1.2f));
                            Vector3 curve = Vector3.Cross(swimmer.homeOffset.normalized, Vector3.up) *
                                Mathf.Sin(regroup * Mathf.PI) * 15f;
                            Vector3 goal = school.home + schoolDrift + swimmer.homeOffset;
                            desired = Vector3.Lerp(swimmer.scatterOrigin + swimmer.scatterVelocity * 0.7f, goal, regroup) + curve;
                            float gain = t >= FishScatterSeconds ? 4.5f : 2.2f;
                            float steer = t >= FishScatterSeconds ? 3.8f : 1.9f;
                            swimmer.velocity = Vector3.Lerp(swimmer.velocity, (desired - swimmer.position) * steer, dt * gain);
                            desired = swimmer.position + swimmer.velocity * dt;
                            fishRegroupingCount++;
                        }
                    }
                    else
                    {
                        Vector3 steering = (desired - swimmer.position) * 1.4f;
                        swimmer.velocity = Vector3.Lerp(swimmer.velocity, steering, Mathf.Clamp01(dt * 1.8f));
                        desired = swimmer.position + swimmer.velocity * dt;
                    }
                    Vector3 velocity = swimmer.velocity;
                    swimmer.position = CaveLayout.Constrain(desired, ref velocity, 8f);
                    swimmer.velocity = velocity;
                    Vector3 regroupGoal = school.home + schoolDrift + swimmer.homeOffset;
                    Vector3 goalVelocity = new(Mathf.Cos(song * 0.21f + school.phase) * 1.26f,
                        Mathf.Cos(song * 0.31f + school.phase) * 0.93f, -Mathf.Sin(song * 0.18f + school.phase) * 1.44f);
                    if (wasScattering && swimmer.scatterAge >= FishScatterSeconds &&
                        Vector3.Distance(swimmer.position, regroupGoal) < 0.8f && (swimmer.velocity-goalVelocity).magnitude < 1.5f)
                    {
                        swimmer.scattering = false;
                        swimmer.velocity = Vector3.zero;
                        swimmer.target.hp = 1;
                        fishRegroupingCount = Mathf.Max(0, fishRegroupingCount - 1);
                    }
                    Vector3 forward = swimmer.velocity.sqrMagnitude > 0.4f ? swimmer.velocity.normalized :
                        (school.home + schoolDrift + swimmer.homeOffset - swimmer.position).normalized;
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                    swimmer.rotation = Quaternion.LookRotation(forward, Vector3.up);
                    swimmer.anchor.transform.SetPositionAndRotation(swimmer.position, swimmer.rotation);
                    swimmer.target.position = swimmer.position;
                    swimmer.target.visual.transform.SetPositionAndRotation(swimmer.position, swimmer.rotation);
                    if (!swimmer.scattering && swimmer.target.hp < 1)
                        swimmer.target.hp = 1;
                    matter.SetGroup(swimmer.group, Matrix4x4.TRS(swimmer.position, swimmer.rotation, Vector3.one),
                        swimmer.scattering ? MatterPhase.Scatter : MatterPhase.Form, swimmer.scatterAge,
                        swimmer.scattering ? 1.5f : 0.25f, swimmer.scatterOrigin, swimmer.scattering ? 18f : 0);
                }
            }
        }

        void TickWhale(float song, float dt)
        {
            if (whaleReleased)
            {
                float releaseAge = Mathf.Max(0, song - whaleReleaseAt);
                MatterPhase releasedPhase = releaseAge >= JellySettleSeconds ? MatterPhase.Settled : MatterPhase.Transfer;
                matter.SetGroup(whaleGroup, whaleReleaseMatrix, releasedPhase, releaseAge,
                    releasedPhase == MatterPhase.Settled ? 0.35f : 2.5f, whalePosition, 0);
                matter.SetGroup(ambientGroup, Matrix4x4.identity, MatterPhase.Form, 0, 0.6f, whalePosition, 0);
                whaleVelocity = Vector3.zero;
                return;
            }
            Vector3 previous = whalePosition;
            EvaluateWhalePose(song, out whalePosition, out whaleRotation);
            whaleVelocity = whalePoseStarted && dt > 0 ? Vector3.ClampMagnitude((whalePosition - previous) / dt, 80f) : Vector3.zero;
            whalePoseStarted = true;
            whaleRoot.transform.SetPositionAndRotation(whalePosition, whaleRotation);
            whaleRoot.transform.localScale = Vector3.one * 1.8f;
            float phase = song * 0.045f;
            whaleTurn = Mathf.Abs(Mathf.Sin(phase));
            WhaleSurfaceActivity = Mathf.Clamp01((whalePosition.y - (CaveLayout.HorizonSurfaceY - 42f)) / 42f) *
                Mathf.Clamp01(whaleVelocity.magnitude / 18f);
            float tailStroke = Mathf.Sin(song * 0.78f);
            matter.SetGroup(whaleGroup, Matrix4x4.TRS(whalePosition, whaleRotation, Vector3.one * 1.8f),
                MatterPhase.Form, 0, 0.10f + Mathf.Abs(tailStroke) * 0.08f + whalePulse * 0.10f, whalePosition, whaleVelocity.magnitude * 0.35f);
            matter.SetGroup(ambientGroup, Matrix4x4.identity,
                MatterPhase.Form, 0, 0.6f, whalePosition, 0);
            matter.SetWhaleMotion(whaleVelocity, WhaleSurfaceActivity, whaleTurn);
        }

        public static void EvaluateWhalePose(float song, out Vector3 position, out Quaternion rotation)
        {
            float phase = song * 0.045f;
            Vector3 center = CaveLayout.Rooms[2].Center;
            position = center + new Vector3(330f * Mathf.Sin(phase),
                -35f + 115f * Mathf.Sin(phase * 2f + 0.35f), -400f * Mathf.Cos(phase));
            Vector3 tangent = new(330f * 0.045f * Mathf.Cos(phase),
                230f * 0.045f * Mathf.Cos(phase * 2f + 0.35f), 400f * 0.045f * Mathf.Sin(phase));
            float bank = Mathf.Sin(phase) * 8f;
            rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(0, 0, bank);
        }

        public static Vector3 DeformWhaleLocal(Vector3 local, float song)
        {
            Vector3 form = local;
            float tail = Mathf.Pow(Mathf.Clamp01((-form.z - 8f) / 74f), 1.7f);
            local.y += Mathf.Sin(song * 0.78f + form.z * 0.047f) * tail * 5.5f;
            local.x += Mathf.Sin(song * 0.61f + form.z * 0.052f) * tail * 2.2f;
            float span = Mathf.Clamp01((Mathf.Abs(form.x) - 9f) / 36f);
            float finRegion = Mathf.Clamp01((18f - form.z) / 30f) * span;
            float finPhase = song * 1.18f + span * 1.6f;
            local.y += Mathf.Sin(finPhase) * finRegion * 6.5f;
            local.z += Mathf.Cos(finPhase) * finRegion * 2.1f;
            return local;
        }

        void TickWhaleTargets(float song)
        {
            for (int i = 0; i < whaleTargets.Count; i++)
            {
                Vector3 pos = whaleRoot.transform.TransformPoint(WhaleTargetLocal(i, song));
                LockTarget target = whaleTargets[i];
                target.position = pos;
                target.visual.transform.SetPositionAndRotation(pos, whaleRotation);
                float patchGlow = litResonators.Contains(i) ? whaleOrganHeat[i] : 0.28f;
                whaleOrganPatches[i] = new Vector4(pos.x, pos.y, pos.z, patchGlow);
                if (!litResonators.Contains(i) && whaleRoundReadyAt < 0) target.hp = 1;
                else target.hp = target.reserved;
            }
            matter?.SetWhalePatches(whaleOrganPatches);
            if (whaleRoundReadyAt == -2f && !WhaleShotsOutstanding())
            {
                float beat = (float)Score.BeatSeconds;
                whaleRoundReadyAt = Mathf.Ceil((song + beat * 2f) / beat) * beat;
            }
            else if (whaleRoundReadyAt >= 0 && song >= whaleRoundReadyAt)
            {
                whaleRoundReadyAt = -1;
                whaleRound++;
                litResonators.Clear();
                for (int i = 0; i < whaleTargets.Count; i++)
                {
                    whaleTargets[i].hp = 1 + whaleTargets[i].reserved;
                    whaleOrganHeat[i] = 0f;
                    whaleOrganPatches[i].w = 0f;
                }
                matter?.SetWhalePatches(whaleOrganPatches);
            }
        }

        bool WhaleShotsOutstanding()
        {
            for (int i = 0; i < whaleTargets.Count; i++) if (whaleTargets[i].reserved > 0) return true;
            return false;
        }

        void OnDestroy()
        {
            matter?.Dispose();
            foreach (Jelly jelly in jellies) if (jelly.anchor) Destroy(jelly.anchor);
            foreach (Fish swimmer in fish) if (swimmer.anchor) Destroy(swimmer.anchor);
            foreach (GameObject anchor in whaleAnchors) if (anchor) Destroy(anchor);
            if (whaleRoot) Destroy(whaleRoot);
        }
    }
}
