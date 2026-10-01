using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class HermitEncounter : MonoBehaviour
    {
        const int BossHitGoal = 32;
        const int EarlyRearmPointCount = 3;
        const float RoomRadiusX = 380f;
        const float RoomRadiusY = 160f;
        const float RoomRadiusZ = 410f;
        const float SmallAcquireRange = 105f;
        const float BossAcquireRange = 150f;
        const float PressureSpeed = 23.5f;
        const int MaxOwnedPressureShots = 12;
        const float MergeDuration = 4f;
        const float RefugeDuration = 4f;

        enum FormState { Crawling = 0, Scattered = 1, Merging = 2, Giant = 2, Refuge = 3, Settled = 4 }

        sealed class Crab
        {
            public GameObject visual;
            public Transform root;
            public Renderer renderer;
            public LockTarget target;
            public Vector3 spawnPosition;
            public Vector3 position;
            public Vector3 spawnHeading;
            public Vector3 heading;
            public Color tint;
            public float speed;
            public float scale;
            public float gaitOffset;
            public float turnCountdown;
            public float turnSeed;
            public float deathSong;
            public float deathBeat;
            public int turnCount;
            public int mergeSlot = -1;
            public FormState state;
            public bool defeated;
        }

        readonly Vector3 roomCenter = new Vector3(-120f, -840f, 2290f);
        Crab[] crabs;
        int[] mergeGroups;
        GameObject[] bossMarkers;
        Renderer[] bossRenderers;
        LockTarget[] bossSlots;
        List<int> fallenOrder;
        List<LockTarget> smallTargets;
        List<LockTarget> bossTargets;
        ReadOnlyCollection<LockTarget> smallTargetView;
        ReadOnlyCollection<LockTarget> bossTargetView;
        MaterialPropertyBlock properties;
        Mesh swarmMesh;
        Mesh markerMesh;
        Material matterMaterial;
        Encounter combat;
        ParticleWorld world;
        Flight flight;
        MusicTransport music;

        Vector3 bossPosition;
        Quaternion bossRotation;
        int smallDefeated;
        int bossHits;
        int attackBeat = int.MinValue;
        int smallShotSequence;
        int bossPulseSequence;
        float mergeStartSong;
        float refugeStartSong;
        float mergeProgress;
        float refugeProgress;
        bool initialized;
        bool mergeStarted;
        bool merging;
        bool bossActive;
        bool refugeStarted;
        bool complete;
        bool previousRoomActive;
        bool offenseStopped;
        string status = "DORMANT";

        public IReadOnlyList<LockTarget> SmallTargets => smallTargetView ?? (IReadOnlyList<LockTarget>)Array.Empty<LockTarget>();
        public IReadOnlyList<LockTarget> BossTargets => bossTargetView ?? (IReadOnlyList<LockTarget>)Array.Empty<LockTarget>();
        public bool Complete => complete;
        public bool Merging => merging;
        public int SmallDefeated => smallDefeated;
        public int BossHits => bossHits;
        public int ParticleCount => HermitGeometry.SwarmSize * HermitGeometry.ParticlesPerCrab;
        public float Progress => Mathf.Clamp01((Mathf.Min(smallDefeated, HermitGeometry.MergeSourceCount) + bossHits) / 40f);
        public float MergeProgress => mergeProgress;
        public string Status => status;
        public Vector3 BossPosition => bossPosition;
        public int InitializationCount { get; private set; }
        public bool MergeSettled => mergeStarted && !merging;
        public bool RefugeSettled => complete;

        public void Initialize(Encounter combat, ParticleWorld world, Flight flight, MusicTransport music)
        {
            if (initialized)
            {
                if (this.combat != combat || this.world != world || this.flight != flight || this.music != music)
                    throw new InvalidOperationException("HermitEncounter cannot be rebound after initialization.");
                InitializationCount++;
                ResetEncounter();
                return;
            }

            if (!combat || !world || !flight || !music)
                throw new InvalidOperationException("HermitEncounter requires initialized combat, particles, flight, and music.");

            InitializationCount++;
            this.combat = combat;
            this.world = world;
            this.flight = flight;
            this.music = music;
            crabs = new Crab[HermitGeometry.SwarmSize];
            mergeGroups = new int[HermitGeometry.MergeSourceCount];
            bossMarkers = new GameObject[HermitGeometry.BossPointCount];
            bossRenderers = new Renderer[HermitGeometry.BossPointCount];
            bossSlots = new LockTarget[HermitGeometry.BossPointCount];
            fallenOrder = new List<int>(HermitGeometry.SwarmSize);
            smallTargets = new List<LockTarget>(HermitGeometry.SwarmSize);
            bossTargets = new List<LockTarget>(HermitGeometry.BossPointCount);
            smallTargetView = smallTargets.AsReadOnly();
            bossTargetView = bossTargets.AsReadOnly();
            properties = new MaterialPropertyBlock();

            Shader shader = Resources.Load<Shader>("HermitMatter");
            if (!shader)
                throw new InvalidOperationException("Missing Resources/HermitMatter.shader.");

            matterMaterial = new Material(shader) { name = "Hermit encounter matter" };
            matterMaterial.SetFloat("_Gain", 1.75f);
            matterMaterial.SetColor("_Tint", Color.white);
            swarmMesh = HermitGeometry.BuildSwarmMesh();
            markerMesh = HermitGeometry.BuildMarkerMesh();

            Vector3 center = roomCenter;
            bossPosition = new Vector3(center.x,
                HermitGeometry.FloorHeight(center, new Vector3(RoomRadiusX, RoomRadiusY, RoomRadiusZ), center.x, center.z, 0.18f),
                center.z);
            bossRotation = Quaternion.Euler(0f, 180f, 0f);
            matterMaterial.SetVector("_BossRoot", bossPosition);
            matterMaterial.SetVector("_RefugeRoot", bossPosition);

            BuildCrabs();
            BuildBossMarkers();
            initialized = true;
            ResetEncounter();
        }

        public void Tick(float song, float dt, bool roomActive)
        {
            if (!initialized || !combat || !world || !flight || !music || !matterMaterial)
                return;

            dt = Mathf.Clamp(dt, 0f, 0.1f);
            float beatPosition = (float)AuthoredScore.BeatPosition(song);
            matterMaterial.SetFloat("_Song", song);
            matterMaterial.SetFloat("_Beat", beatPosition);

            if (previousRoomActive && !roomActive)
                combat.ClearPressureShots(this);
            previousRoomActive = roomActive;

            if (combat.Ended && !offenseStopped)
            {
                combat.ClearPressureShots(this);
                offenseStopped = true;
            }

            for (int i = 0; i < crabs.Length; i++)
                if (crabs[i].state == FormState.Crawling)
                    Crawl(crabs[i], dt);

            if (merging)
            {
                mergeProgress = Mathf.Clamp01((song - mergeStartSong) / MergeDuration);
                if (mergeProgress >= 1f)
                    FinishMerge();
            }

            if (refugeStarted && !complete)
            {
                refugeProgress = Mathf.Clamp01((song - refugeStartSong) / RefugeDuration);
                if (refugeProgress >= 1f)
                {
                    complete = true;
                    RefreshStatus();
                }
            }

            UpdateTargetPositions(beatPosition);
            UpdateGroupProperties();
            CheckBossRearm();

            int beat = Mathf.FloorToInt(beatPosition);
            if (attackBeat == int.MinValue)
                attackBeat = beat;
            else if (beat != attackBeat)
            {
                attackBeat = beat;
                if (roomActive && !combat.Ended && !complete && !refugeStarted)
                {
                    if (!mergeStarted && (beat & 1) == 0)
                        FireSmallPulse(song, beat);
                    else if (bossActive && !merging)
                        FireBossPulse(song);
                }
            }
        }

        public void ResetEncounter()
        {
            if (!initialized || !combat)
                return;

            combat.ClearPressureShots(this);
            smallDefeated = 0;
            bossHits = 0;
            attackBeat = int.MinValue;
            smallShotSequence = 0;
            bossPulseSequence = 0;
            mergeStartSong = 0f;
            refugeStartSong = 0f;
            mergeProgress = 0f;
            refugeProgress = 0f;
            mergeStarted = false;
            merging = false;
            bossActive = false;
            refugeStarted = false;
            complete = false;
            previousRoomActive = false;
            offenseStopped = false;
            fallenOrder.Clear();
            Array.Clear(mergeGroups, 0, mergeGroups.Length);

            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                crab.position = crab.spawnPosition;
                crab.heading = crab.spawnHeading;
                crab.deathSong = 0f;
                crab.deathBeat = 0f;
                crab.turnCountdown = 0.65f + Mathf.Repeat(crab.turnSeed * 0.73f, 1.35f);
                crab.turnCount = 0;
                crab.mergeSlot = -1;
                crab.state = FormState.Crawling;
                crab.defeated = false;
                crab.root.localScale = Vector3.one * crab.scale;
                crab.root.SetPositionAndRotation(crab.position, Quaternion.LookRotation(crab.heading, Vector3.up));
                crab.visual.SetActive(true);
            }

            EnsureEnvironmentTargets();
            for (int i = 0; i < HermitGeometry.BossPointCount; i++)
            {
                LockTarget target = bossSlots[i];
                target.hp = 0;
                target.reserved = 0;
                target.acquireRange = BossAcquireRange;
                target.position = bossPosition + bossRotation * HermitGeometry.BossTargetLocalPosition(i, 0f);
                combat.Locks.Remove(target);
                bossMarkers[i].transform.SetPositionAndRotation(target.position, bossRotation);
                bossMarkers[i].SetActive(false);
            }

            matterMaterial.SetVector("_BossRoot", bossPosition);
            matterMaterial.SetVector("_RefugeRoot", bossPosition);
            UpdateTargetPositions(0f);
            UpdateGroupProperties();
            RefreshStatus();
        }

        void BuildCrabs()
        {
            var random = new System.Random(341827);
            for (int i = 0; i < crabs.Length; i++)
            {
                var visual = new GameObject("Hermit crab " + (i + 1));
                visual.transform.SetParent(transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = swarmMesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = matterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float nx, nz;
                if (i < 4)
                {
                    nx = -0.12f + i * 0.08f;
                    nz = -0.57f + (i - 1.5f) * 0.008f;
                }
                else
                {
                    int index = i - 4;
                    float radius = 0.2f + 0.4f * Mathf.Sqrt((index + 0.5f) / 20f);
                    float angle = index * 2.39996323f + 0.47f;
                    nx = Mathf.Cos(angle) * radius;
                    nz = Mathf.Sin(angle) * radius;
                }

                float headingAngle = (float)random.NextDouble() * 360f;
                Vector3 heading = Quaternion.Euler(0f, headingAngle, 0f) * Vector3.forward;
                float x = roomCenter.x + nx * RoomRadiusX;
                float z = roomCenter.z + nz * RoomRadiusZ;
                Vector3 position = new Vector3(x,
                    HermitGeometry.FloorHeight(roomCenter, new Vector3(RoomRadiusX, RoomRadiusY, RoomRadiusZ), x, z, 0.18f), z);
                float scale = 0.9f + (float)random.NextDouble() * 0.2f;
                float turnSeed = (float)random.NextDouble() * 91.73f;
                var crab = new Crab {
                    visual = visual,
                    root = visual.transform,
                    renderer = renderer,
                    spawnPosition = position,
                    position = position,
                    spawnHeading = heading,
                    heading = heading,
                    speed = 6f + (float)random.NextDouble() * 6f,
                    scale = scale,
                    gaitOffset = GaitOffset(i),
                    turnSeed = turnSeed,
                    turnCountdown = 0.65f + Mathf.Repeat(turnSeed * 0.73f, 1.35f),
                    tint = Color.Lerp(new Color(0.9f, 1f, 0.96f), new Color(1f, 0.9f, 0.75f), Mathf.Repeat(i * 0.618034f, 1f) * 0.12f),
                    state = FormState.Crawling
                };
                crab.root.localScale = Vector3.one * scale;
                crab.root.SetPositionAndRotation(position, Quaternion.LookRotation(heading, Vector3.up));
                crabs[i] = crab;
            }
        }

        void BuildBossMarkers()
        {
            for (int i = 0; i < bossMarkers.Length; i++)
            {
                var marker = new GameObject("Hermit lock point " + (i + 1));
                marker.transform.SetParent(transform, false);
                marker.AddComponent<MeshFilter>().sharedMesh = markerMesh;
                var renderer = marker.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = matterMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                marker.transform.localScale = Vector3.one * 0.9f;
                marker.SetActive(false);
                bossMarkers[i] = marker;
                bossRenderers[i] = renderer;
            }
        }

        void EnsureEnvironmentTargets()
        {
            smallTargets.Clear();
            for (int i = 0; i < crabs.Length; i++)
            {
                int index = i;
                Crab crab = crabs[i];
                if (crab.target == null || !combat.Targets.Contains(crab.target))
                    crab.target = combat.RegisterEnvironment(crab.visual, (target, song) => OnSmallHit(index, target, song));
                crab.target.acquireRange = SmallAcquireRange;
                crab.target.hp = 1;
                crab.target.reserved = 0;
                crab.target.position = crab.position + Vector3.up * (2.55f * crab.scale);
                crab.target.visual = crab.visual;
                crab.visual.SetActive(true);
                combat.Locks.Remove(crab.target);
                smallTargets.Add(crab.target);
            }

            bossTargets.Clear();
            for (int i = 0; i < bossSlots.Length; i++)
            {
                int index = i;
                GameObject marker = bossMarkers[i];
                if (bossSlots[i] == null || !combat.Targets.Contains(bossSlots[i]))
                    bossSlots[i] = combat.RegisterEnvironment(marker, (target, song) => OnBossHit(index, target, song));
                LockTarget target = bossSlots[i];
                target.acquireRange = BossAcquireRange;
                target.hp = 0;
                target.reserved = 0;
                target.position = bossPosition + bossRotation * HermitGeometry.BossTargetLocalPosition(i, 0f);
                target.visual = marker;
                marker.SetActive(false);
                combat.Locks.Remove(target);
                bossTargets.Add(target);
            }
        }

        void Crawl(Crab crab, float dt)
        {
            if (dt <= 0f)
                return;

            crab.turnCountdown -= dt;
            if (crab.turnCountdown <= 0f)
            {
                float hash = Mathf.Sin((crab.turnCount + 1) * 7.31f + crab.turnSeed * 2.17f) * 43758.5453f;
                float fraction = hash - Mathf.Floor(hash);
                float angle = (fraction - 0.5f) * 190f;
                crab.heading = Quaternion.AngleAxis(angle, Vector3.up) * crab.heading;
                crab.heading.y = 0f;
                crab.heading.Normalize();
                crab.turnCountdown = 0.7f + fraction * 1.55f;
                crab.turnCount++;
            }

            Vector3 next = crab.position + crab.heading * (crab.speed * dt);
            float nx = (next.x - roomCenter.x) / RoomRadiusX;
            float nz = (next.z - roomCenter.z) / RoomRadiusZ;
            if (nx * nx + nz * nz > 0.36f)
            {
                Vector3 normal = new Vector3(nx / RoomRadiusX, 0f, nz / RoomRadiusZ).normalized;
                crab.heading = Vector3.Reflect(crab.heading, normal).normalized;
                next = crab.position + crab.heading * (crab.speed * dt);
                nx = (next.x - roomCenter.x) / RoomRadiusX;
                nz = (next.z - roomCenter.z) / RoomRadiusZ;
                float radius = Mathf.Sqrt(nx * nx + nz * nz);
                if (radius > 0.6f)
                {
                    nx *= 0.599f / radius;
                    nz *= 0.599f / radius;
                    next.x = roomCenter.x + nx * RoomRadiusX;
                    next.z = roomCenter.z + nz * RoomRadiusZ;
                }
            }

            next.y = HermitGeometry.FloorHeight(roomCenter,
                new Vector3(RoomRadiusX, RoomRadiusY, RoomRadiusZ), next.x, next.z, 0.18f);
            crab.position = next;
            crab.root.SetPositionAndRotation(next, Quaternion.LookRotation(crab.heading, Vector3.up));
        }

        void OnSmallHit(int index, LockTarget target, float song)
        {
            Crab crab = crabs[index];
            if (crab.defeated)
                return;

            crab.defeated = true;
            crab.state = FormState.Scattered;
            crab.deathSong = song;
            crab.deathBeat = (float)AuthoredScore.BeatPosition(song);
            smallDefeated++;
            fallenOrder.Add(index);
            world.BurstAt(target.position, song, new Color(0.18f, 0.83f, 0.76f), 0.8f);

            if (smallDefeated == HermitGeometry.MergeSourceCount && !mergeStarted)
                BeginMerge(song);
            RefreshStatus();
        }

        void BeginMerge(float song)
        {
            mergeStarted = true;
            merging = true;
            mergeStartSong = song;
            mergeProgress = 0f;
            int slot = 0;
            for (int i = 0; i < fallenOrder.Count && slot < mergeGroups.Length; i++)
            {
                int groupIndex = fallenOrder[i];
                Crab crab = crabs[groupIndex];
                crab.mergeSlot = slot;
                crab.state = FormState.Merging;
                mergeGroups[slot++] = groupIndex;
            }
            bossActive = false;
            FreezeSmallTargets();
            for (int i = 0; i < bossSlots.Length; i++)
            {
                bossSlots[i].hp = 0;
                bossSlots[i].reserved = 0;
                bossMarkers[i].SetActive(false);
            }
            RefreshStatus();
        }

        void FinishMerge()
        {
            merging = false;
            mergeProgress = 1f;
            bossActive = true;
            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                target.hp = 1;
                target.reserved = 0;
                target.acquireRange = BossAcquireRange;
                bossMarkers[i].SetActive(true);
            }
            for (int i = 0; i < mergeGroups.Length; i++)
                crabs[mergeGroups[i]].state = FormState.Giant;
            for (int i = 0; i < crabs.Length; i++)
            {
                Crab support = crabs[i];
                if (support.defeated)
                    continue;
                support.visual.SetActive(true);
                support.target.hp = support.target.reserved > 0 ? support.target.reserved : 1;
                support.target.position = support.position + Vector3.up * (2.55f * support.scale);
                combat.Locks.Remove(support.target);
            }
            RefreshStatus();
        }

        void OnBossHit(int index, LockTarget target, float song)
        {
            if (bossHits < BossHitGoal)
                bossHits++;
            world.BurstAt(target.position, song, (index & 1) == 0
                ? new Color(0.16f, 0.82f, 0.76f)
                : new Color(1f, 0.65f, 0.22f), 1.1f);

            if (target.hp <= 0)
            {
                target.visual.SetActive(false);
                combat.Locks.Remove(target);
            }

            RefreshStatus();
            if (bossHits >= BossHitGoal && !refugeStarted)
                BeginRefuge(song);
        }

        void BeginRefuge(float song)
        {
            refugeStarted = true;
            refugeStartSong = song;
            refugeProgress = 0f;
            merging = false;
            bossActive = false;
            offenseStopped = true;
            combat.ClearPressureShots(this);
            FreezeSmallTargets();

            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                target.hp = target.reserved;
                if (target.reserved == 0)
                {
                    target.visual.SetActive(false);
                    combat.Locks.Remove(target);
                }
            }
            for (int i = 0; i < mergeGroups.Length; i++)
                crabs[mergeGroups[i]].state = FormState.Refuge;
            for (int i = 0; i < crabs.Length; i++)
                if (crabs[i].state == FormState.Crawling)
                    crabs[i].state = FormState.Settled;
            RefreshStatus();
        }

        void UpdateTargetPositions(float beat)
        {
            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                if (crab.target != null)
                    crab.target.position = crab.position + crab.root.rotation * Vector3.up * (2.55f * crab.scale);
            }

            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                Vector3 position = bossPosition + bossRotation * HermitGeometry.BossTargetLocalPosition(i, beat);
                target.position = position;
                bossMarkers[i].transform.SetPositionAndRotation(position, bossRotation);
                bool show = (bossActive && target.hp > 0) || (refugeStarted && target.reserved > 0);
                if (bossMarkers[i].activeSelf != show)
                    bossMarkers[i].SetActive(show);
            }
        }

        void UpdateGroupProperties()
        {
            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                properties.Clear();
                properties.SetFloat("_State", (float)crab.state);
                properties.SetFloat("_DeathSong", crab.deathSong);
                properties.SetFloat("_DeathBeat", crab.deathBeat);
                properties.SetFloat("_GaitOffset", crab.gaitOffset);
                properties.SetFloat("_MergeSlot", crab.mergeSlot);
                properties.SetFloat("_MergeProgress", mergeProgress);
                properties.SetFloat("_RefugeProgress", refugeProgress);
                properties.SetColor("_Tint", crab.tint);
                properties.SetFloat("_Gain", 1.75f);
                crab.renderer.SetPropertyBlock(properties);
            }

            for (int i = 0; i < bossMarkers.Length; i++)
            {
                properties.Clear();
                properties.SetFloat("_State", 5f);
                properties.SetFloat("_DeathSong", 0f);
                properties.SetFloat("_GaitOffset", 0f);
                properties.SetFloat("_MergeSlot", 0f);
                properties.SetFloat("_MergeProgress", 0f);
                properties.SetFloat("_RefugeProgress", 0f);
                properties.SetColor("_Tint", Color.white);
                properties.SetFloat("_Gain", 2.1f);
                bossRenderers[i].SetPropertyBlock(properties);
            }
        }

        void CheckBossRearm()
        {
            if (!bossActive || refugeStarted || bossHits >= BossHitGoal)
                return;

            int unreservedPoints = 0;
            bool pending = false;
            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                if (target.hp - target.reserved > 0)
                    unreservedPoints++;
                pending |= target.reserved > 0;
            }

            if (unreservedPoints > EarlyRearmPointCount)
                return;
            if (pending)
                return;

            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                target.hp = 1;
                target.reserved = 0;
                target.position = bossPosition + bossRotation * HermitGeometry.BossTargetLocalPosition(i, 0f);
                bossMarkers[i].SetActive(true);
            }
        }

        void FreezeSmallTargets()
        {
            for (int i = 0; i < crabs.Length; i++)
            {
                LockTarget target = crabs[i].target;
                if (target == null)
                    continue;
                target.hp = target.reserved;
                combat.Locks.Remove(target);
            }
        }

        void FireSmallPulse(float song, int beat)
        {
            int shooters = 2 + ((smallShotSequence + beat / 2) & 1);
            int fired = 0;
            int start = (smallShotSequence * 7) % crabs.Length;
            for (int offset = 0; offset < crabs.Length && fired < shooters; offset++)
            {
                Crab crab = crabs[(start + offset) % crabs.Length];
                if (crab.state != FormState.Crawling || crab.defeated)
                    continue;
                Vector3 origin = crab.root.TransformPoint(new Vector3(0f, 0.92f, 3.15f));
                int sequence = smallShotSequence * 3 + fired;
                Vector3 direction = WeakAim(origin, PressureSpeed);
                float yaw = ((sequence % 3) - 1) * 7f;
                float pitch = ((sequence & 1) == 0 ? -1f : 1f) * 2f;
                direction = ApplySpread(direction, yaw, pitch);
                if (!RegisterPressure(origin, direction, song,
                    (sequence & 1) == 0 ? new Color(0.09f, 0.75f, 0.82f) : new Color(0.85f, 0.58f, 0.2f), PressureSpeed))
                    break;
                fired++;
            }
            smallShotSequence++;
        }

        void FireBossPulse(float song)
        {
            int mode = bossPulseSequence++ & 1;
            FireSupportPulse(song, bossPulseSequence);
            if (mode == 0)
            {
                int side = (bossPulseSequence & 2) == 0 ? -1 : 1;
                Vector3 local = HermitGeometry.BossClawOrigin(side, (float)AuthoredScore.BeatPosition(song));
                Vector3 origin = bossPosition + bossRotation * local;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 direction = WeakAim(origin, PressureSpeed + 1f);
                    direction = ApplySpread(direction, (i - 1) * 9f, (i - 1) * 2.5f);
                    if (!RegisterPressure(origin, direction, song,
                        i == 1 ? new Color(1f, 0.67f, 0.22f) : new Color(0.12f, 0.78f, 0.77f), PressureSpeed + 1f))
                        return;
                }
            }
            else
            {
                Vector3 origin = bossPosition + bossRotation * new Vector3(0f, 8f, 14f);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 direction = WeakAim(origin, PressureSpeed + 1f);
                    direction = ApplySpread(direction, (i - 2) * 14f, (i % 2 == 0 ? -3f : 3f));
                    if (!RegisterPressure(origin, direction, song,
                        (i & 1) == 0 ? new Color(0.1f, 0.74f, 0.79f) : new Color(0.88f, 0.56f, 0.19f), PressureSpeed + 1f))
                        return;
                }
            }
        }

        void FireSupportPulse(float song, int pulse)
        {
            int shooters = 2 + (pulse & 1);
            int fired = 0;
            int start = (pulse * 7) % crabs.Length;
            for (int offset = 0; offset < crabs.Length && fired < shooters; offset++)
            {
                Crab crab = crabs[(start + offset) % crabs.Length];
                if (crab.state != FormState.Crawling || crab.defeated || crab.target.hp - crab.target.reserved <= 0)
                    continue;
                Vector3 origin = crab.root.TransformPoint(new Vector3(0f, 0.92f, 3.15f));
                Vector3 direction = WeakAim(origin, PressureSpeed);
                float yaw = (fired - (shooters - 1) * 0.5f) * 6.5f;
                float pitch = (fired & 1) == 0 ? -1.5f : 1.5f;
                direction = ApplySpread(direction, yaw, pitch);
                Color color = ((pulse + fired) & 1) == 0
                    ? new Color(0.09f, 0.75f, 0.82f)
                    : new Color(0.85f, 0.58f, 0.2f);
                if (!RegisterPressure(origin, direction, song, color, PressureSpeed))
                    break;
                fired++;
            }
        }

        bool RegisterPressure(Vector3 origin, Vector3 direction, float song, Color color, float speed)
        {
            if (combat.LivePressureShots(this) >= MaxOwnedPressureShots || !combat.CanRegisterPressureShots(1))
                return false;
            return combat.RegisterPressureShot(origin, direction, song, color, this, speed) != null;
        }

        Vector3 WeakAim(Vector3 origin, float speed)
        {
            Vector3 direct = flight.Position - origin;
            if (direct.sqrMagnitude < 0.001f)
                direct = flight.transform.forward;
            direct.Normalize();
            float travel = Mathf.Clamp(Vector3.Distance(origin, flight.Position) / speed, 0f, 1.2f);
            Vector3 predicted = flight.Position + flight.Velocity * travel * 0.45f - origin;
            if (predicted.sqrMagnitude < 0.001f)
                return direct;
            return Vector3.Slerp(direct, predicted.normalized, 0.2f).normalized;
        }

        static Vector3 ApplySpread(Vector3 direction, float yaw, float pitch)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();
            direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
            return (Quaternion.AngleAxis(pitch, right) * direction).normalized;
        }

        void RefreshStatus()
        {
            if (complete)
                status = "TIDAL SHELL REFUGE";
            else if (refugeStarted)
                status = "CORAL REFUGE SETTLING";
            else if (mergeStarted && merging)
                status = "HERMIT SWARM GATHERING";
            else if (mergeStarted)
                status = "GIANT HERMIT " + bossHits + "/" + BossHitGoal;
            else
                status = "HERMIT SWARM " + smallDefeated + "/" + HermitGeometry.MergeSourceCount;
        }

        static float GaitOffset(int index)
        {
            switch (index & 3)
            {
                case 1: return 0.5f;
                case 2: return 0.25f;
                case 3: return 0.125f;
                default: return 0f;
            }
        }

        void OnDestroy()
        {
            if (combat)
                combat.ClearPressureShots(this);

            if (crabs != null)
                foreach (Crab crab in crabs)
                {
                    if (crab == null || !crab.visual)
                        continue;
                    bool registered = combat && crab.target != null && combat.Targets.Contains(crab.target);
                    if (registered)
                        combat.UnregisterEnvironment(crab.target);
                    else
                        Destroy(crab.visual);
                }
            if (bossMarkers != null)
                for (int i = 0; i < bossMarkers.Length; i++)
                {
                    GameObject marker = bossMarkers[i];
                    if (!marker)
                        continue;
                    bool registered = combat && bossSlots != null && bossSlots[i] != null && combat.Targets.Contains(bossSlots[i]);
                    if (registered)
                        combat.UnregisterEnvironment(bossSlots[i]);
                    else
                        Destroy(marker);
                }
            if (swarmMesh)
                Destroy(swarmMesh);
            if (markerMesh)
                Destroy(markerMesh);
            if (matterMaterial)
                Destroy(matterMaterial);
        }
    }
}
