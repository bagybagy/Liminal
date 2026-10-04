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
        const int WhaleRingCount = PersistentMatter.WhaleRingCount;
        const int WhaleTargetsPerRing = PersistentMatter.WhaleTargetsPerRing;
        public const int WhaleOrganCount = PersistentMatter.WhalePatchCount;
        public const int WhaleDamageGoal = 96;
        const int AmbientCount = 16000;
        const float FishScatterSeconds = 8f;
        const float JellySettleSeconds = 10f;
        const float WhaleRecallSeconds = 4f;
        const float WhaleSurfaceColorFadeSeconds = 2f;
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
        readonly List<Vector4> alternateForms = new();
        readonly List<Matrix4x4> initialMatrices = new();
        static readonly int[] DolphinOrgans = { 8, 10, 16, 18, 32, 34 };
        readonly int[] dolphinGroups = new int[DolphinEncounter.Capacity];
        readonly bool[] dolphinBorn = new bool[DolphinEncounter.Capacity];
        readonly Matrix4x4[] dolphinBirthMatrices = new Matrix4x4[DolphinEncounter.Capacity];
        readonly Matrix4x4[] dolphinReleaseMatrices = new Matrix4x4[DolphinEncounter.Capacity];
        readonly Vector3[] dolphinPrevious = new Vector3[DolphinEncounter.Capacity];
        readonly float[] dolphinRetiredAt = new float[DolphinEncounter.Capacity];
        WhaleArrival arrival;
        DolphinEncounter dolphins;
        WhaleInheritance inheritance;
        MusicTransport music;
        PersistentMatter matter;
        ParticleWorld world;
        Encounter combat;
        GameObject whaleRoot;
        Vector3 whalePosition, whaleVelocity, previousPlayer;
        Quaternion whaleRotation = Quaternion.identity;
        int whaleGroup, ambientGroup, fishScatteringCount, fishRegroupingCount;
        float whalePulse, whaleReleaseAt, whaleRecallStartAt = -1f, whaleRecallStartedAt = -1f;
        float whaleRecallFinishAt = -1f, whaleRecallProgress, whaleTurn, whaleSurfaceColorBlend, jellyImpactAge = -100;
        int whaleDamage, whaleRound = 1;
        Vector3 jellyImpactPoint;
        int jellyImpactIndex = -1;
        bool whaleReleased, whaleRegenerating, whaleRecallStarted, whaleReleaseSoundPlayed;
        int whaleRegenerations, dolphinRecallCount;
        bool whalePoseStarted, playerPoseStarted;
        bool whalePeakEffectsPlayed;
        Matrix4x4 whaleReleaseMatrix;
        float whaleCombatStartedAt = -1f;
        public WhaleCombatMotion.Pose CombatPose { get; private set; }
        public Vector4 WhaleGesture => whaleReleased ? Vector4.zero : CombatPose.Gesture;
        public float CombatFirstActionAt => whaleCombatStartedAt;
        public Vector3 WhaleHullContact => WhaleCombatMotion.ContactPoint(whalePosition, whaleRotation,
            lastWhalePoseSong, WhaleGesture);
        float lastWhalePoseSong;
        float lastWhaleHitSong = -1f;
        bool lastWhaleHitValidity;
        string lastWhaleHitCondition = "No resonator hit this run";

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
        public int RemainingWhaleTargets => whaleDamage >= WhaleDamageGoal ? 0 :
            Mathf.Max(0, WhaleOrganCount - litResonators.Count);
        public bool WhaleRegenerating => whaleRegenerating;
        public float WhaleSurfaceColorBlend => whaleSurfaceColorBlend;
        public float LastWhaleHitSong => lastWhaleHitSong;
        public bool LastWhaleHitValidity => lastWhaleHitValidity;
        public string LastWhaleHitCondition => lastWhaleHitCondition;
        public float WhaleRecallProgress => whaleRecallProgress;
        public int WhaleRegenerations => whaleRegenerations;
        public int DolphinRecallCount => dolphinRecallCount;
        public bool WhaleWaveReady => WhaleEntranceComplete && !whaleReleased && !whaleRegenerating &&
            whaleDamage < WhaleDamageGoal;
        public bool WhaleVisible => WhaleVisibility > 0.001f;
        public float WhaleVisibility => arrival ? arrival.Visibility : 0f;
        public bool WhaleEntranceComplete => arrival && arrival.Complete;
        public WhaleArrival Arrival => arrival;
        public DolphinEncounter Dolphins => dolphins;
        public WhaleInheritance Inheritance => inheritance;
        public BossId GrowthMask => inheritance ? inheritance.GrowthMask : BossId.None;
        public int SummonCount => inheritance ? inheritance.SummonCount : 0;
        public int SummonsAlive => inheritance ? inheritance.SummonsAlive : 0;
        public int SpearTargets => inheritance ? inheritance.SpearTargets : 0;
        public IReadOnlyList<LockTarget> SummonTargets => inheritance ? inheritance.SummonTargets : Array.Empty<LockTarget>();
        public IReadOnlyList<LockTarget> SpearLockTargets => inheritance ? inheritance.SpearLockTargets : Array.Empty<LockTarget>();
        public int SpearHits => inheritance ? inheritance.SpearHits : 0;
        public int RingShots => inheritance ? inheritance.RingShots : 0;
        public int InheritedShotsSpawned => inheritance ? inheritance.ShotsSpawned : 0;
        public int InheritedShotsLive => inheritance ? inheritance.ShotsLive : 0;
        public int InheritedShotsResolved => inheritance ? inheritance.ShotsResolved : 0;
        public int InheritedCurtainsFired => inheritance ? inheritance.CurtainsFired : 0;
        public int DolphinParticleCount { get; private set; }
        public int DolphinGroupAt(int index) => dolphinGroups[index];
        public int SpawnedDolphins
        {
            get { int count = 0; foreach (bool born in dolphinBorn) if (born) count++; return count; }
        }
        public float WhaleOrganHeat(int index) => whaleOrganHeat[index];
        public bool WhaleReleased => whaleReleased;
        public Vector3 WhalePosition => whalePosition;
        public Vector3 WhaleVelocity => whaleVelocity;
        public float WhaleSurfaceActivity { get; private set; }
        public Color WhaleLightColor { get; private set; } = new(0.05f, 0.65f, 1f);
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
            music = GetComponent<Experience>().Music;
            EvaluateWhalePose(0, out whalePosition, out whaleRotation);
            if (!world.matterSimulation || !world.matterLight)
                throw new InvalidOperationException("MarineLife requires ParticleWorld.matterSimulation and matterLight.");
            BuildJellies();
            BuildSchools();
            BuildWhaleSeeds();
            BuildAmbientSeeds();
            matter = new PersistentMatter();
            matter.Initialize(world.matterSimulation, world.matterLight, seeds, initialMatrices.Count, alternateForms);
            matter.SetWhaleGroup(whaleGroup);
            matter.SetComparisonJellyGroup(FirstJellyGroup);
            whaleRoot = new GameObject("THE HORIZON WHALE / transform");
            whaleRoot.transform.SetParent(transform, false);
            BuildWhaleTargets();
            arrival = gameObject.AddComponent<WhaleArrival>();
            arrival.Initialize();
            dolphins = gameObject.AddComponent<DolphinEncounter>();
            dolphins.Initialize(combat, world, GetComponent<Experience>().Flight);
            inheritance = gameObject.AddComponent<WhaleInheritance>();
            inheritance.Initialize(combat, world, GetComponent<Experience>().Flight, dolphins);
            ResetLife();
        }

        public void ConfigureInheritance(BossId mask)
        {
            if (inheritance) inheritance.ConfigureInheritance(mask);
        }

        public void PrepareFinale(Func<int, Vector3> sampler)
        {
            if (sampler == null) throw new ArgumentNullException(nameof(sampler));
            matter.SetWhaleDestinations(sampler);
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
                for (int j = 0; j < 63; j++)
                {
                    float identity = index * 1987 + r * 63 + j;
                    float u = (j + .5f) / 63f, radius = Mathf.Sin(u * Mathf.PI * .5f) * 4.6f;
                    float angle = a + (JellyHash(identity) - .5f) * Mathf.PI * 2 / ribs;
                    Vector3 p = new(Mathf.Cos(angle) * radius, .25f + Mathf.Cos(u * Mathf.PI * .5f) * 3.5f, Mathf.Sin(angle) * radius);
                    AddSeed(group, p, .024f, JellyColor(identity, u),
                        JellyKind, identity * .0137f, u, JellyGarden(index, r, u));
                }
            }
            for (int t = 0; t < 16 + index % 5; t++)
            {
                float a = t * Mathf.PI * 2 / (16 + index % 5), length = 8f + (t * 17 % 9) * 1.15f;
                for (int j = 0; j < 168; j++)
                {
                    float u = (j + .5f) / 168f, sway = Mathf.Sin(u * 5.2f + t * 1.7f) * u * 1.25f;
                    Vector3 p = new(Mathf.Cos(a) * (2.2f + u * 0.6f) + Mathf.Cos(a + Mathf.PI / 2) * sway,
                        -0.15f - u * length, Mathf.Sin(a) * (2.2f + u * 0.6f) + Mathf.Sin(a + Mathf.PI / 2) * sway);
                    float identity = index * 3763 + t * 168 + j + 7919;
                    p += new Vector3(JellyHash(identity) - .5f, 0, JellyHash(identity + 19) - .5f) * .10f;
                    AddSeed(group, p, Mathf.Lerp(.024f, .012f, u), JellyColor(identity, u) * (1 - u * .20f),
                        JellyKind, identity * .0137f, 1 + u, JellyGarden(index, ribs + t, u));
                }
            }
            for (int j = 0; j < 540; j++)
            {
                float u = JellyHash(j + index * 540), a = j * 2.399f;
                float y = .2f + u * 3.7f, radius = Mathf.Sqrt(JellyHash(j + 571)) * 1.8f;
                Vector3 p = new(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                AddSeed(group, p, .020f, JellyColor(j + 12971, u) * .65f, JellyKind,
                    j * .137f, 2 + u, JellyGarden(index, ribs + 40 + j / 180, (j % 180) / 179f));
            }
        }

        static float JellyHash(float value) => Mathf.Repeat(Mathf.Sin(value * 127.1f + 311.7f) * 43758.5453f, 1f);

        static Color JellyColor(float identity, float u)
        {
            float hue = JellyHash(identity + 83);
            Color color = Color.Lerp(new Color(.07f, .34f, 1f), new Color(.06f, .88f, .74f), u);
            if (hue > .86f) color = Color.Lerp(color, new Color(.65f, .09f, .62f), .65f);
            if (hue > .98f) color = Color.Lerp(color, new Color(1f, .52f, .12f), .7f);
            return color;
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
            var matrix = Matrix4x4.TRS(whalePosition, whaleRotation, Vector3.one * 1.8f);
            whaleGroup = NewGroup(matrix);
            for (int i = 0; i < DolphinEncounter.Capacity; i++) dolphinGroups[i] = NewGroup(matrix);
            int first = seeds.Count;
            WhaleAnatomy.Append((position, radius, color, seed, role) =>
                AddSeed(whaleGroup, position, radius, color, WhaleKind, seed, role, WhaleGarden(seeds.Count, 0)));
            int[] counts = new int[DolphinEncounter.Capacity];
            for (int i = first; i < seeds.Count; i++)
            {
                MatterSeed item = seeds[i];
                if (item.traits.w > 1.5f) continue;
                Vector3 point = item.form;
                int closest = -1;
                float distance = 15f * 15f;
                for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
                {
                    Vector3 patch = WhaleTargetLocal(DolphinOrgans[slot], 0f);
                    float d = (point - patch).sqrMagnitude;
                    if (d < distance) { distance = d; closest = slot; }
                }
                if (closest < 0) continue;
                item.traits.x = dolphinGroups[closest];
                item.traits.y = 4f;
                seeds[i] = item;
                counts[closest]++;
            }
            int[] ordinal = new int[DolphinEncounter.Capacity];
            for (int i = first; i < seeds.Count; i++)
            {
                for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
                {
                    if ((int)seeds[i].traits.x != dolphinGroups[slot]) continue;
                    alternateForms[i] = DolphinForm.Sample(ordinal[slot]++, counts[slot]);
                    DolphinParticleCount += 1;
                    break;
                }
            }
            ambientGroup = NewGroup(Matrix4x4.identity);
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
            alternateForms.Add(Vector4.zero);
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

        Vector3 WhaleTargetLocal(int index, float song) => WhaleAnatomy.TargetLocal(index, song, WhaleGesture);

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
            if (world.Caverns) world.Caverns.Illuminate(target.position, jelly.hits == 1 ? 1.8f : 1.2f, Aqua);
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
            world.BurstAt(origin, song, Blue, 0.75f);
        }

        void HitWhale(int index, LockTarget target, float song)
        {
            if (whaleReleased)
            {
                lastWhaleHitValidity = false;
                lastWhaleHitCondition = "Whale already released";
                return;
            }
            if (whaleRecallStarted)
            {
                lastWhaleHitValidity = false;
                lastWhaleHitCondition = "Whale recall active";
                return;
            }
            if (litResonators.Contains(index))
            {
                lastWhaleHitValidity = false;
                lastWhaleHitCondition = "Resonator already lit";
                return;
            }
            if (!WhaleEntranceComplete)
            {
                lastWhaleHitValidity = false;
                lastWhaleHitCondition = "Whale entrance incomplete";
                return;
            }
            lastWhaleHitValidity = true;
            lastWhaleHitSong = song;
            lastWhaleHitCondition = "Valid resonator hit";
            litResonators.Add(index);
            if (whaleDamage < WhaleDamageGoal) whaleDamage++;
            whaleOrganHeat[index] = 1f;
            target.hp = target.reserved;
            whalePulse = 1.6f;
            Color hitColor = index % 11 == 0 ? new Color(1f, 0.52f, 0.10f) : new Color(0.035f, 0.62f, 1f);
            if (world.Caverns) world.Caverns.Illuminate(target.position, 1.45f, hitColor);
            world.BurstAt(target.position, song, hitColor, 1.25f);
            for (int slot = 0; whaleDamage < WhaleDamageGoal && slot < DolphinOrgans.Length; slot++)
            {
                if (index != DolphinOrgans[slot] || dolphinBorn[slot]) continue;
                if (!dolphins.TrySpawn(slot, target.position, whaleRotation, song)) continue;
                dolphinBorn[slot] = true;
                dolphinBirthMatrices[slot] = Matrix4x4.TRS(whalePosition, whaleRotation, Vector3.one * 1.8f);
                dolphinPrevious[slot] = target.position;
            }
            if (whaleDamage >= WhaleDamageGoal)
            {
                FreezeWhaleTargets();
                FreezeDolphinTargets();
                if (!whaleReleaseSoundPlayed)
                {
                    whaleReleaseSoundPlayed = true;
                    music.BossRelease(song, 2);
                }
            }
            if (litResonators.Count >= WhaleOrganCount - 4 && whaleDamage < WhaleDamageGoal)
                BeginWhaleRegeneration();
        }

        void ReleaseWhale(float song)
        {
            if (whaleReleased) return;
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
            arrival.ResetArrival();
            matter.SetWhaleArrival(arrival.Origin, 0f);
            dolphins.ResetSchool();
            inheritance.Reset();
            for (int i = 0; i < DolphinEncounter.Capacity; i++)
            {
                dolphinBorn[i] = false;
                dolphinRetiredAt[i] = -1f;
                matter.SetDolphinState(dolphinGroups[i], Matrix4x4.identity, Vector3.zero, false);
            }
            litJellies.Clear(); depletedJellies.Clear(); litResonators.Clear();
            Array.Clear(whaleOrganHeat, 0, whaleOrganHeat.Length);
            Array.Clear(whaleOrganPatches, 0, whaleOrganPatches.Length);
            whaleDamage = 0; whaleRound = 1;
            lastWhaleHitSong = -1f;
            lastWhaleHitValidity = false;
            lastWhaleHitCondition = "No resonator hit this run";
            whaleReleaseSoundPlayed = false;
            whaleRegenerating = whaleRecallStarted = false;
            whaleRecallStartAt = whaleRecallStartedAt = whaleRecallFinishAt = -1f;
            whaleRecallProgress = 0f;
            whaleRegenerations = dolphinRecallCount = 0;
            FishResponses = 0; SettledJellies = 0; whaleReleased = false;
            jellyImpactIndex = -1; jellyImpactAge = -100; whalePulse = 0; whaleReleaseAt = 0;
            fishScatteringCount = fishRegroupingCount = 0;
            whaleVelocity = Vector3.zero;
            whaleCombatStartedAt = -1f;
            CombatPose = default;
            lastWhalePoseSong = 0f;
            WhaleSurfaceActivity = 0f;
            whaleSurfaceColorBlend = 0f;
            whaleTurn = 0f;
            EvaluateWhalePose(0, out whalePosition, out whaleRotation);
            whaleRoot.transform.SetPositionAndRotation(whalePosition, whaleRotation);
            whaleRoot.transform.localScale = Vector3.one * 1.8f;
            previousPlayer = CaveLayout.Spawn;
            whalePoseStarted = playerPoseStarted = false;
            whalePeakEffectsPlayed = false;
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
                target.hp = 0; target.reserved = 0;
                target.visual.SetActive(false);
            }
            for (int i = 0; i < initialMatrices.Count; i++)
                matter.SetGroup(i, initialMatrices[i], MatterPhase.Form, 0, 0, Vector3.zero, 0);
            matter.SetCurrent(whalePosition, Vector3.zero, 180f, 0);
            matter.SetWhaleMotion(Vector3.zero, 0f, 0f);
            matter.SetWhaleGesture(Vector4.zero, Vector3.zero);
            matter.SetWhaleVisibility(0f);
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
            arrival.Tick(song, player);
            matter.SetWhaleArrival(arrival.Origin, arrival.Formation);
            TickWhale(song, dt);
            if (arrival.PeakReached && !whalePeakEffectsPlayed)
            {
                whalePeakEffectsPlayed = true;
                if (world.Caverns) world.Caverns.Illuminate(whalePosition, 5f, Blue);
                world.BurstAt(whalePosition, song, Blue, 4f);
            }
            inheritance.Tick(song, arrival.Age, arrival.PeakTime, arrival.Triggered, whaleReleased,
                whaleRegenerating || whaleRecallStarted, whaleDamage >= WhaleDamageGoal, whalePosition, whaleRotation);
            TickWhaleTargets(song);
            TickDolphins(song, dt);
            matter.SetWhaleVisibility(WhaleVisibility);
            Vector3 currentVelocity = whaleVelocity + whaleRotation * Vector3.forward * (whalePulse * 9f);
            float currentEnergy = WhaleVisibility * (0.28f + whalePulse * 1.8f + (whaleReleased ? 0.7f : 0));
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
            lastWhalePoseSong = song;
            if (whaleReleased)
            {
                float releaseAge = Mathf.Max(0, song - whaleReleaseAt);
                MatterPhase releasedPhase = releaseAge >= JellySettleSeconds ? MatterPhase.Settled : MatterPhase.Transfer;
                matter.SetGroup(whaleGroup, whaleReleaseMatrix, releasedPhase, releaseAge,
                    releasedPhase == MatterPhase.Settled ? 0.35f : 2.5f, whalePosition, 0);
                matter.SetGroup(ambientGroup, Matrix4x4.identity, MatterPhase.Form, 0, 0.6f, whalePosition, 0);
                whaleVelocity = Vector3.zero;
                matter.SetWhaleGesture(Vector4.zero, Vector3.zero);
                return;
            }
            Vector3 previous = whalePosition;
            Quaternion previousRotation = whaleRotation;
            if (arrival.Complete) {
                if (whaleCombatStartedAt < 0f) whaleCombatStartedAt = (float)AuthoredScore.Next(song + 8f, 0, true);
                CombatPose = WhaleCombatMotion.Evaluate(arrival.CruiseTime, song, whaleCombatStartedAt);
                whalePosition = CombatPose.Position;
                // A massive swimmer turns into its trajectory rather than snapping to its instantaneous tangent.
                whaleRotation = Quaternion.RotateTowards(previousRotation, CombatPose.Rotation, 95f * dt);
            }
            else { CombatPose = default; arrival.Pose(out whalePosition, out whaleRotation); }
            whaleVelocity = whalePoseStarted && dt > 0 ? Vector3.ClampMagnitude((whalePosition - previous) / dt, 200f) : Vector3.zero;
            whalePoseStarted = true;
            Vector3 angularVelocity = Vector3.zero;
            if (arrival.Complete && dt > 0f) {
                Quaternion delta = whaleRotation * Quaternion.Inverse(previousRotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (Mathf.Abs(angle) > .0001f && !float.IsNaN(axis.x)) angularVelocity = axis * (angle * Mathf.Deg2Rad / dt);
            }
            matter.SetWhaleGesture(WhaleGesture, Vector3.ClampMagnitude(angularVelocity, 2f));
            whaleRoot.transform.SetPositionAndRotation(whalePosition, whaleRotation);
            whaleRoot.transform.localScale = Vector3.one * 1.8f;
            float phase = song * 0.045f;
            whaleTurn = Mathf.Abs(Mathf.Sin(phase));
            whaleSurfaceColorBlend = WhaleEntranceComplete
                ? Mathf.MoveTowards(whaleSurfaceColorBlend, 1f, dt / WhaleSurfaceColorFadeSeconds)
                : 0f;
            WhaleSurfaceActivity = Mathf.Clamp01((whalePosition.y - (CaveLayout.HorizonSurfaceY - 42f)) / 42f) *
                Mathf.Clamp01(whaleVelocity.magnitude / 18f) * whaleSurfaceColorBlend;
            float dive = Mathf.Clamp01(-whaleVelocity.y / 16f);
            WhaleLightColor = Color.Lerp(new Color(.04f,.85f,1f),new Color(1f,.12f,.62f),
                Mathf.SmoothStep(0,1,Mathf.Clamp01(whaleTurn*.70f+dive*.65f)));
            WhaleLightColor = Color.Lerp(WhaleLightColor,new Color(1f,.60f,.16f),WhaleSurfaceActivity);
            float tailStroke = Mathf.Sin(song * 0.78f);
            matter.SetGroup(whaleGroup, Matrix4x4.TRS(whalePosition, whaleRotation, Vector3.one * 1.8f),
                MatterPhase.Form, 0, 0.10f + Mathf.Abs(tailStroke) * 0.08f + whalePulse * 0.10f, whalePosition, whaleVelocity.magnitude * 0.35f);
            matter.SetGroup(ambientGroup, Matrix4x4.identity,
                MatterPhase.Form, 0, 0.6f, whalePosition, 0);
            if (!WhaleEntranceComplete) WhaleLightColor = new Color(0.025f, 0.7f, 1f);
            matter.SetWhaleMotion(whaleVelocity, WhaleSurfaceActivity, whaleTurn, !WhaleEntranceComplete);
        }

        void TickDolphins(float song, float dt)
        {
            dolphins.Tick(song, dt, whaleReleased, whaleDamage >= WhaleDamageGoal,
                whaleRegenerating || whaleRecallStarted);
            var whaleMatrix = Matrix4x4.TRS(whalePosition, whaleRotation, Vector3.one * 1.8f);
            if (whaleRegenerating && whaleRecallStarted)
            {
                float age = Mathf.Max(0f, song - whaleRecallStartedAt);
                whaleRecallProgress = Mathf.Clamp01(age / WhaleRecallSeconds);
                for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
                {
                    int group = dolphinGroups[slot];
                    matter.SetDolphinState(group, dolphinBirthMatrices[slot], Vector3.zero, false);
                    matter.SetGroup(group, whaleMatrix, MatterPhase.Recall, age, 2f, whalePosition, 1f);
                }
                return;
            }
            for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
            {
                int group = dolphinGroups[slot];
                if (!dolphinBorn[slot])
                {
                    float releaseAge = whaleReleased ? Mathf.Max(0f, song - whaleReleaseAt) : 0f;
                    MatterPhase phase = !whaleReleased ? MatterPhase.Form :
                        releaseAge >= JellySettleSeconds ? MatterPhase.Settled : MatterPhase.Transfer;
                    matter.SetGroup(group, whaleReleased ? whaleReleaseMatrix : whaleMatrix, phase, releaseAge,
                        whaleReleased ? 2.5f : 0.16f, whalePosition, 0f);
                    continue;
                }
                var pose = dolphins.PoseAt(slot);
                Vector3 velocity = dt > 0f ? Vector3.ClampMagnitude((pose.Position - dolphinPrevious[slot]) / dt, 80f) : Vector3.zero;
                dolphinPrevious[slot] = pose.Position;
                matter.SetDolphinState(group, dolphinBirthMatrices[slot], velocity, true);
                var matrix = Matrix4x4.TRS(pose.Position, pose.Rotation, Vector3.one * 1.5f);
                if (!pose.Retired)
                {
                    matter.SetGroup(group, matrix, MatterPhase.Dolphin, pose.Age, 0.2f,
                        pose.BirthPosition, 0f);
                    continue;
                }
                if (dolphinRetiredAt[slot] < 0f)
                {
                    dolphinRetiredAt[slot] = song;
                    dolphinReleaseMatrices[slot] = matrix;
                }
                float age = Mathf.Max(0f, song - dolphinRetiredAt[slot]);
                matter.SetGroup(group, dolphinReleaseMatrices[slot],
                    age >= JellySettleSeconds ? MatterPhase.Settled : MatterPhase.Transfer, age, 1.4f,
                    pose.Position, 1f);
            }
        }

        public static void EvaluateWhalePose(float song, out Vector3 position, out Quaternion rotation)
            => EvaluateWhalePose(song, out position, out rotation, out _, out _);

        internal static void EvaluateWhalePose(float song, out Vector3 position, out Quaternion rotation,
            out Vector3 velocity, out Vector3 acceleration)
        {
            float phase = song * 0.045f;
            Vector3 center = CaveLayout.Rooms[2].Center;
            position = center + new Vector3(330f * Mathf.Sin(phase),
                -35f + 115f * Mathf.Sin(phase * 2f + 0.35f), -400f * Mathf.Cos(phase));
            Vector3 tangent = new(330f * 0.045f * Mathf.Cos(phase),
                230f * 0.045f * Mathf.Cos(phase * 2f + 0.35f), 400f * 0.045f * Mathf.Sin(phase));
            velocity = tangent;
            acceleration = new Vector3(-330f * .045f * .045f * Mathf.Sin(phase),
                -460f * .045f * .045f * Mathf.Sin(phase * 2f + .35f), 400f * .045f * .045f * Mathf.Cos(phase));
            float bank = Mathf.Sin(phase) * 8f;
            rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(0, 0, bank);
        }

        public static Vector3 DeformWhaleLocal(Vector3 local, float song) => WhaleAnatomy.Deform(local, song);

        void TickWhaleTargets(float song)
        {
            TickWhaleRegeneration(song);
            for (int i = 0; i < whaleTargets.Count; i++)
            {
                Vector3 pos = whaleRoot.transform.TransformPoint(WhaleTargetLocal(i, song));
                LockTarget target = whaleTargets[i];
                target.position = pos;
                target.visual.transform.SetPositionAndRotation(pos, whaleRotation);
                float patchGlow = litResonators.Contains(i) ? whaleOrganHeat[i] : 0.28f;
                whaleOrganPatches[i] = new Vector4(pos.x, pos.y, pos.z, patchGlow);
                target.visual.SetActive(WhaleEntranceComplete && !whaleReleased);
                if (WhaleWaveReady && !litResonators.Contains(i)) target.hp = 1;
                else target.hp = target.reserved;
            }
            matter?.SetWhalePatches(whaleOrganPatches);
            if (whaleDamage >= WhaleDamageGoal && !whaleReleased && !WhaleOrDolphinShotsOutstanding())
            {
                ReleaseWhale(song);
            }
        }

        void BeginWhaleRegeneration()
        {
            if (whaleRegenerating || whaleReleased) return;
            whaleRegenerating = true;
            FreezeWhaleTargets();
            FreezeDolphinTargets();
        }

        void FreezeWhaleTargets()
        {
            foreach (LockTarget target in whaleTargets) target.hp = target.reserved;
        }

        void FreezeDolphinTargets()
        {
            foreach (LockTarget target in combat.Targets)
                if (target.kind == TargetKind.Dolphin) target.hp = target.reserved;
        }

        bool WhaleOrDolphinShotsOutstanding()
        {
            foreach (LockTarget target in whaleTargets)
                if (target.reserved > 0) return true;
            foreach (LockTarget target in combat.Targets)
                if (target.kind == TargetKind.Dolphin && target.reserved > 0) return true;
            return false;
        }

        void TickWhaleRegeneration(float song)
        {
            if (!whaleRegenerating) return;
            FreezeWhaleTargets();
            FreezeDolphinTargets();
            if (WhaleOrDolphinShotsOutstanding()) return;
            if (whaleDamage >= WhaleDamageGoal)
            {
                whaleRegenerating = false;
                whaleRecallProgress = 0f;
                ReleaseWhale(song);
                return;
            }

            if (whaleRecallStartAt < 0f)
            {
                whaleRecallStartAt = (float)AuthoredScore.Next(song, .02, true);
            }
            if (!whaleRecallStarted && song >= whaleRecallStartAt)
                BeginWhaleRecall();
            if (whaleRecallStarted)
            {
                whaleRecallProgress = Mathf.Clamp01(Mathf.Max(0f, song - whaleRecallStartedAt) / WhaleRecallSeconds);
                if (song >= whaleRecallFinishAt) FinishWhaleRegeneration();
            }
        }

        void BeginWhaleRecall()
        {
            whaleRecallStarted = true;
            whaleRecallStartedAt = whaleRecallStartAt;
            whaleRecallProgress = 0f;
            whaleRecallFinishAt = (float)AuthoredScore.Next(whaleRecallStartedAt + WhaleRecallSeconds, 0, true);
            for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
                if (dolphinBorn[slot]) dolphinRecallCount++;
            for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
                matter.SetDolphinState(dolphinGroups[slot], dolphinBirthMatrices[slot], Vector3.zero, false);
            dolphins.BeginRecall();
        }

        void FinishWhaleRegeneration()
        {
            dolphins.ResetSchool();
            litResonators.Clear();
            Array.Clear(whaleOrganHeat, 0, whaleOrganHeat.Length);
            Array.Clear(whaleOrganPatches, 0, whaleOrganPatches.Length);
            Array.Clear(dolphinBorn, 0, dolphinBorn.Length);
            Array.Clear(dolphinBirthMatrices, 0, dolphinBirthMatrices.Length);
            Array.Clear(dolphinReleaseMatrices, 0, dolphinReleaseMatrices.Length);
            for (int slot = 0; slot < DolphinEncounter.Capacity; slot++)
            {
                dolphinRetiredAt[slot] = -1f;
                matter.SetDolphinState(dolphinGroups[slot], Matrix4x4.identity, Vector3.zero, false);
            }
            for (int i = 0; i < whaleTargets.Count; i++)
            {
                whaleTargets[i].hp = 1;
                whaleOrganPatches[i].w = 0f;
            }
            matter.SetWhalePatches(whaleOrganPatches);
            whaleRound++;
            whaleRegenerations++;
            whaleRegenerating = whaleRecallStarted = false;
            whaleRecallStartAt = whaleRecallStartedAt = whaleRecallFinishAt = -1f;
            whaleRecallProgress = 0f;
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
