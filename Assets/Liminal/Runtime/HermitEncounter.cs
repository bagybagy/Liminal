using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class HermitEncounter : MonoBehaviour
    {
        public const int SmallHitsPerCrab = 4;
        public const int BossHitGoal = 48;
        const int EarlyRearmPointCount = 3;
        const float RoomRadiusX = 380f;
        const float RoomRadiusY = 160f;
        const float RoomRadiusZ = 410f;
        const float SmallAcquireRange = 105f;
        const float BossAcquireRange = 150f;
        const float PressureSpeed = 24f;
        const float PressurePodSpeed = 14f;
        const float PressurePodMinimumDistance = 28f;
        const int MaxOwnedPressureShots = 12;
        const float MergeDuration = 4f;
        const float RefugeDuration = 7f;

        enum FormState { Crawling = 0, Scattered = 1, Merging = 2, Giant = 2, Refuge = 3 }

        sealed class Crab
        {
            public GameObject visual;
            public Transform root;
            public Renderer renderer;
            public readonly LockTarget[] targets = new LockTarget[SmallHitsPerCrab];
            public Vector3 spawnPosition;
            public Vector3 position;
            public Vector3 spawnHeading;
            public Vector3 heading;
            public Vector3 desiredHeading;
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
            public int hitMask;
            public int hitsTaken;
            public int reefSourceState;
            public float reefSourceElapsed;
            public readonly Vector3[] feet = new Vector3[6];
            public readonly Vector3[] previousFeet = new Vector3[6];
            public readonly Vector3[] swingStarts = new Vector3[6];
            public readonly Vector3[] swingEnds = new Vector3[6];
            public readonly float[] footPhases = new float[6];
        }

        Vector3 roomCenter => CaveLayout.Rooms[3].Center;
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
        int[] footPropertyIds;
        Mesh swarmMesh;
        Mesh markerMesh;
        Mesh shoalMesh;
        Material matterMaterial;
        GameObject shoalObject;
        Renderer shoalRenderer;
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
        bool retireTargets;
        float previousBeat;
        float reefBeat;
        string status = "DORMANT";

        public IReadOnlyList<LockTarget> SmallTargets => smallTargetView ?? (IReadOnlyList<LockTarget>)Array.Empty<LockTarget>();
        public IReadOnlyList<LockTarget> BossTargets => bossTargetView ?? (IReadOnlyList<LockTarget>)Array.Empty<LockTarget>();
        public bool Complete => complete;
        public bool Merging => merging;
        public int SmallDefeated => smallDefeated;
        public int BossHits => bossHits;
        public int ParticleCount => HermitGeometry.SwarmSize * HermitGeometry.ParticlesPerCrab;
        public float Progress => Mathf.Clamp01((Mathf.Min(smallDefeated, HermitGeometry.MergeSourceCount) + bossHits) /
            (float)(HermitGeometry.MergeSourceCount + BossHitGoal));
        public float MergeProgress => mergeProgress;
        public string Status => status;
        public Vector3 BossPosition => bossPosition;
        public int InitializationCount { get; private set; }
        public bool MergeSettled => mergeStarted && !merging;
        public bool RefugeSettled => complete;
        public float ReefProgress => refugeProgress;
        public int ReefParticleGroups => refugeStarted ? crabs.Length : 0;
        public int ReefParticleCount => refugeStarted ? ParticleCount : 0;
        public float ReefScale => HermitGeometry.ReefScale;
        public int ReefFishCount => HermitGeometry.ReefFishCount;
        public bool ReefFishActive => complete && shoalObject && shoalObject.activeSelf;
        public int SmallBubbleShots { get; private set; }
        public int PressurePods { get; private set; }
        public int GiantBubbleRings { get; private set; }
        public int PlantedFeet { get; private set; }
        public float MaxStanceFootDrift { get; private set; }
        public int RemainingCombatants
        {
            get
            {
                if (!initialized || refugeStarted) return 0;
                int count = bossActive ? 1 : 0;
                for (int i = 0; i < crabs.Length; i++)
                    if (!crabs[i].defeated) count++;
                return count;
            }
        }

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
            smallTargets = new List<LockTarget>(HermitGeometry.SwarmSize * SmallHitsPerCrab);
            bossTargets = new List<LockTarget>(HermitGeometry.BossPointCount);
            smallTargetView = smallTargets.AsReadOnly();
            bossTargetView = bossTargets.AsReadOnly();
            properties = new MaterialPropertyBlock();
            footPropertyIds = new int[6];
            for (int i = 0; i < footPropertyIds.Length; i++)
                footPropertyIds[i] = Shader.PropertyToID("_Foot" + i);

            Shader shader = Resources.Load<Shader>("HermitMatter");
            if (!shader)
                throw new InvalidOperationException("Missing Resources/HermitMatter.shader.");

            matterMaterial = new Material(shader) { name = "Hermit encounter matter" };
            matterMaterial.SetFloat("_Gain", 1.75f);
            matterMaterial.SetColor("_Tint", Color.white);
            swarmMesh = HermitGeometry.BuildSwarmMesh();
            markerMesh = HermitGeometry.BuildMarkerMesh();
            shoalMesh = HermitGeometry.BuildReefShoalMesh();

            Vector3 center = roomCenter;
            bossPosition = new Vector3(center.x,
                HermitGeometry.FloorHeight(center, new Vector3(RoomRadiusX, RoomRadiusY, RoomRadiusZ), center.x, center.z, 0.18f),
                center.z);
            bossRotation = Quaternion.Euler(0f, 180f, 0f);
            matterMaterial.SetVector("_BossRoot", bossPosition);
            matterMaterial.SetVector("_RefugeRoot", bossPosition);
            matterMaterial.SetVector("_BossRight", bossRotation * Vector3.right);
            matterMaterial.SetVector("_BossForward", bossRotation * Vector3.forward);

            BuildCrabs();
            BuildBossMarkers();
            BuildReefShoal();
            initialized = true;
            ResetEncounter();
        }

        public void Tick(float song, float dt, bool roomActive)
        {
            if (!initialized || !combat || !world || !flight || !music || !matterMaterial)
                return;

            dt = Mathf.Clamp(dt, 0f, 0.1f);
            float beatPosition = (float)AuthoredScore.BeatPosition(song);
            float beatStep = Mathf.Clamp(beatPosition - previousBeat, 0f, 0.3f);
            previousBeat = beatPosition;
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

            // Cancel reservations outside the hit callback: Encounter is iterating
            // its scheduled-shot list while delivering that callback.
            if (retireTargets)
            {
                RetireCombatTargets();
                retireTargets = false;
            }
            PlantedFeet = 0;
            for (int i = 0; i < crabs.Length; i++)
                if (crabs[i].state == FormState.Crawling)
                    Crawl(crabs[i], dt, beatPosition, beatStep);

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
                    if (shoalObject)
                        shoalObject.SetActive(true);
                    RefreshStatus();
                }
            }

            SuppressPeacefulTargets();
            UpdateTargetPositions(beatPosition);
            UpdateGroupProperties();
            UpdateShoalProperties(song, beatPosition);
            CheckBossRearm();

            int beat = Mathf.FloorToInt(beatPosition);
            if (attackBeat == int.MinValue)
                attackBeat = beat;
            else if (beat != attackBeat)
            {
                attackBeat = beat;
                if (roomActive && !combat.Ended && !combat.Peaceful && !complete && !refugeStarted)
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
            RetireCombatTargets();
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
            if (shoalObject)
                shoalObject.SetActive(false);
            previousRoomActive = false;
            offenseStopped = false;
            retireTargets = false;
            previousBeat = (float)AuthoredScore.BeatPosition(music.Time);
            reefBeat = 0f;
            SmallBubbleShots = 0;
            PressurePods = 0;
            GiantBubbleRings = 0;
            PlantedFeet = 0;
            MaxStanceFootDrift = 0f;
            fallenOrder.Clear();
            Array.Clear(mergeGroups, 0, mergeGroups.Length);

            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                crab.position = crab.spawnPosition;
                crab.heading = crab.spawnHeading;
                crab.desiredHeading = crab.spawnHeading;
                crab.deathSong = 0f;
                crab.deathBeat = 0f;
                crab.turnCountdown = 0.65f + Mathf.Repeat(crab.turnSeed * 0.73f, 1.35f);
                crab.turnCount = 0;
                crab.mergeSlot = -1;
                crab.state = FormState.Crawling;
                crab.defeated = false;
                crab.hitMask = 0;
                crab.hitsTaken = 0;
                crab.reefSourceState = 0;
                crab.reefSourceElapsed = 0f;
                crab.root.localScale = Vector3.one * crab.scale;
                crab.root.SetPositionAndRotation(crab.position, Quaternion.LookRotation(crab.heading, Vector3.up));
                ResetFeet(crab);
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
            matterMaterial.SetFloat("_ReefBeat", 0f);
            matterMaterial.SetFloat("_Song", (float)music.Time);
            matterMaterial.SetFloat("_Beat", previousBeat);
            UpdateTargetPositions(previousBeat);
            UpdateGroupProperties();
            UpdateShoalProperties((float)music.Time, previousBeat);
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
                float scale = 1.65f + (float)random.NextDouble() * 0.25f;
                float turnSeed = (float)random.NextDouble() * 91.73f;
                var crab = new Crab {
                    visual = visual,
                    root = visual.transform,
                    renderer = renderer,
                    spawnPosition = position,
                    position = position,
                    spawnHeading = heading,
                    heading = heading,
                    desiredHeading = heading,
                    speed = 1.6f + (float)random.NextDouble() * 0.35f,
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

        void BuildReefShoal()
        {
            shoalObject = new GameObject("Hermit reef shoaling fish");
            shoalObject.transform.SetParent(transform, false);
            shoalObject.transform.SetPositionAndRotation(bossPosition, Quaternion.identity);
            shoalObject.AddComponent<MeshFilter>().sharedMesh = shoalMesh;
            shoalRenderer = shoalObject.AddComponent<MeshRenderer>();
            shoalRenderer.sharedMaterial = matterMaterial;
            shoalRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shoalRenderer.receiveShadows = false;
            shoalObject.SetActive(false);
        }

        void EnsureEnvironmentTargets()
        {
            smallTargets.Clear();
            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                for (int point = 0; point < crab.targets.Length; point++)
                {
                    int crabIndex = i;
                    int pointIndex = point;
                    LockTarget target = crab.targets[point];
                    if (target == null || !combat.Targets.Contains(target))
                        target = crab.targets[point] = combat.RegisterEnvironment(crab.visual,
                            (hitTarget, song) => OnSmallHit(crabIndex, pointIndex, hitTarget, song));
                    target.acquireRange = SmallAcquireRange;
                    target.hp = 1;
                    target.reserved = 0;
                    target.position = crab.root.TransformPoint(HermitGeometry.SmallTargetLocalPosition(point));
                    target.visual = crab.visual;
                    combat.Locks.Remove(target);
                    smallTargets.Add(target);
                }
                crab.visual.SetActive(true);
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

        void Crawl(Crab crab, float dt, float beat, float beatStep)
        {
            if (dt <= 0f)
                return;

            crab.turnCountdown -= dt;
            if (crab.turnCountdown <= 0f)
            {
                float hash = Mathf.Sin((crab.turnCount + 1) * 7.31f + crab.turnSeed * 2.17f) * 43758.5453f;
                float fraction = hash - Mathf.Floor(hash);
                float angle = (fraction - 0.5f) * 190f;
                crab.desiredHeading = Quaternion.AngleAxis(angle, Vector3.up) * crab.heading;
                crab.desiredHeading.y = 0f;
                crab.desiredHeading.Normalize();
                crab.turnCountdown = 0.7f + fraction * 1.55f;
                crab.turnCount++;
            }

            crab.heading = Vector3.RotateTowards(crab.heading, crab.desiredHeading, dt * 1.05f, 0f).normalized;
            Vector3 next = crab.position + crab.heading * (crab.speed * crab.scale * beatStep);
            float nx = (next.x - roomCenter.x) / RoomRadiusX;
            float nz = (next.z - roomCenter.z) / RoomRadiusZ;
            if (nx * nx + nz * nz > 0.36f)
            {
                Vector3 normal = new Vector3(nx / RoomRadiusX, 0f, nz / RoomRadiusZ).normalized;
                crab.heading = Vector3.Reflect(crab.heading, normal).normalized;
                crab.desiredHeading = crab.heading;
                next = crab.position + crab.heading * (crab.speed * crab.scale * beatStep);
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
            UpdateFeet(crab, beat);
        }

        void ResetFeet(Crab crab)
        {
            for (int leg = 0; leg < 6; leg++)
            {
                Vector3 foot = crab.root.TransformPoint(HermitGeometry.RestFoot(leg));
                foot.y = HermitGeometry.FloorHeight(roomCenter,
                    new Vector3(RoomRadiusX, RoomRadiusY, RoomRadiusZ), foot.x, foot.z, 0.08f * crab.scale);
                crab.feet[leg] = crab.swingStarts[leg] = crab.swingEnds[leg] = foot;
                crab.previousFeet[leg] = foot;
                crab.footPhases[leg] = -1f;
            }
        }

        void UpdateFeet(Crab crab, float beat)
        {
            for (int leg = 0; leg < 6; leg++)
            {
                float phase = Mathf.Repeat(beat + crab.gaitOffset + leg % 2 * 0.5f, 1f);
                float oldPhase = crab.footPhases[leg];
                if (phase >= 0.62f)
                {
                    if (oldPhase < 0.62f || phase < oldPhase)
                    {
                        crab.swingStarts[leg] = crab.feet[leg];
                        Vector3 landing = crab.root.TransformPoint(HermitGeometry.RestFoot(leg)) +
                            crab.heading * (crab.speed * crab.scale * (1f - phase + 0.31f));
                        landing.y = HermitGeometry.FloorHeight(roomCenter,
                            new Vector3(RoomRadiusX, RoomRadiusY, RoomRadiusZ), landing.x, landing.z, 0.08f * crab.scale);
                        crab.swingEnds[leg] = landing;
                    }
                    float swing = (phase - 0.62f) / 0.38f;
                    crab.feet[leg] = Vector3.Lerp(crab.swingStarts[leg], crab.swingEnds[leg],
                        Mathf.SmoothStep(0f, 1f, swing)) + Vector3.up * (Mathf.Sin(swing * Mathf.PI) * 1.05f * crab.scale);
                }
                else
                {
                    if (oldPhase >= 0.62f)
                        crab.feet[leg] = crab.swingEnds[leg];
                    PlantedFeet++;
                    if (oldPhase >= 0f && oldPhase < 0.62f && phase >= oldPhase)
                        MaxStanceFootDrift = Mathf.Max(MaxStanceFootDrift,
                            Vector3.Distance(crab.feet[leg], crab.previousFeet[leg]));
                }
                crab.previousFeet[leg] = crab.feet[leg];
                crab.footPhases[leg] = phase;
            }
        }

        void OnSmallHit(int index, int pointIndex, LockTarget target, float song)
        {
            Crab crab = crabs[index];
            if (crab.defeated || refugeStarted || pointIndex < 0 || pointIndex >= SmallHitsPerCrab)
                return;
            int pointMask = 1 << pointIndex;
            if ((crab.hitMask & pointMask) != 0)
                return;

            crab.hitMask |= pointMask;
            crab.hitsTaken++;
            world.BurstAt(target.position, song, new Color(0.18f, 0.83f, 0.76f), 0.8f);
            if (crab.hitsTaken < SmallHitsPerCrab)
                return;

            crab.defeated = true;
            crab.state = FormState.Scattered;
            crab.deathSong = song;
            crab.deathBeat = (float)AuthoredScore.BeatPosition(song);
            smallDefeated++;
            fallenOrder.Add(index);

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
                for (int point = 0; point < support.targets.Length; point++)
                {
                    LockTarget target = support.targets[point];
                    if (target.hp > 0 || target.reserved > 0)
                        target.hp = target.reserved > 0 ? target.reserved : 1;
                    target.position = support.root.TransformPoint(HermitGeometry.SmallTargetLocalPosition(point));
                    combat.Locks.Remove(target);
                }
            }
            RefreshStatus();
        }

        void OnBossHit(int index, LockTarget target, float song)
        {
            if (!bossActive || refugeStarted)
                return;
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
            music.BossRelease(song, 1);
            refugeStartSong = song;
            refugeProgress = 0f;
            reefBeat = (float)AuthoredScore.BeatPosition(song);
            matterMaterial.SetFloat("_ReefBeat", reefBeat);
            merging = false;
            bossActive = false;
            offenseStopped = true;
            combat.ClearPressureShots(this);
            retireTargets = true;
            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                target.hp = 0;
                target.visual.SetActive(false);
                combat.Locks.Remove(target);
            }
            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                crab.reefSourceState = (int)crab.state;
                crab.reefSourceElapsed = Mathf.Max(0f, song - crab.deathSong);
                if (crab.state == FormState.Crawling)
                    crab.deathBeat = reefBeat;
                crab.state = FormState.Refuge;
                for (int point = 0; point < crab.targets.Length; point++)
                {
                    crab.targets[point].hp = 0;
                    combat.Locks.Remove(crab.targets[point]);
                }
                crab.visual.SetActive(true);
            }
            foreach (LockTarget target in combat.Targets)
                if (target.isPressureShot && target.pressureOwner == this)
                {
                    target.hp = 0;
                    if (target.visual) target.visual.SetActive(false);
                }
            UpdateGroupProperties();
            RefreshStatus();
        }

        void RetireCombatTargets()
        {
            for (int i = 0; i < crabs.Length; i++)
                for (int point = 0; point < crabs[i].targets.Length; point++)
                    combat.ResetDolphinTarget(crabs[i].targets[point]);
            for (int i = 0; i < bossSlots.Length; i++)
                combat.ResetDolphinTarget(bossSlots[i]);
            for (int i = combat.Targets.Count - 1; i >= 0; i--)
            {
                LockTarget target = combat.Targets[i];
                if (target.isPressureShot && target.pressureOwner == this)
                    combat.ResetDolphinTarget(target);
            }
            combat.ClearPressureShots(this);
        }

        void UpdateTargetPositions(float beat)
        {
            for (int i = 0; i < crabs.Length; i++)
            {
                Crab crab = crabs[i];
                for (int point = 0; point < crab.targets.Length; point++)
                    if (crab.targets[point] != null)
                        crab.targets[point].position = crab.root.TransformPoint(HermitGeometry.SmallTargetLocalPosition(point));
            }

            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                Vector3 position = bossPosition + bossRotation * HermitGeometry.BossTargetLocalPosition(i, beat);
                target.position = position;
                bossMarkers[i].transform.SetPositionAndRotation(position, bossRotation);
                bool show = bossActive && !combat.Peaceful && target.hp > 0;
                if (bossMarkers[i].activeSelf != show)
                    bossMarkers[i].SetActive(show);
            }
        }

        void SuppressPeacefulTargets()
        {
            if (!combat.Peaceful)
                return;
            for (int i = 0; i < crabs.Length; i++)
                for (int point = 0; point < crabs[i].targets.Length; point++)
                {
                    LockTarget target = crabs[i].targets[point];
                    if (target == null)
                        continue;
                    target.hp = target.reserved;
                    combat.Locks.Remove(target);
                }
            for (int i = 0; i < bossSlots.Length; i++)
            {
                LockTarget target = bossSlots[i];
                target.hp = target.reserved;
                combat.Locks.Remove(target);
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
                properties.SetFloat("_CrabId", i);
                properties.SetFloat("_RefugeSourceState", crab.reefSourceState);
                properties.SetFloat("_RefugeSourceElapsed", crab.reefSourceElapsed);
                for (int leg = 0; leg < 6; leg++)
                    properties.SetVector(footPropertyIds[leg], crab.root.InverseTransformPoint(crab.feet[leg]));
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

        void UpdateShoalProperties(float song, float beat)
        {
            if (!shoalRenderer)
                return;
            properties.Clear();
            properties.SetFloat("_State", 4f);
            properties.SetFloat("_Song", song);
            properties.SetFloat("_Beat", beat);
            properties.SetColor("_Tint", Color.white);
            properties.SetFloat("_Gain", 1.75f);
            shoalRenderer.SetPropertyBlock(properties);
        }

        void CheckBossRearm()
        {
            if (!bossActive || refugeStarted || combat.Peaceful || bossHits >= BossHitGoal)
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
                bossMarkers[i].SetActive(true);
            }
        }

        void FreezeSmallTargets()
        {
            for (int i = 0; i < crabs.Length; i++)
            {
                for (int point = 0; point < crabs[i].targets.Length; point++)
                {
                    LockTarget target = crabs[i].targets[point];
                    if (target == null)
                        continue;
                    target.hp = target.reserved;
                    combat.Locks.Remove(target);
                }
            }
        }

        void FireSmallPulse(float song, int beat)
        {
            int start = (smallShotSequence * 7) % crabs.Length;
            if (smallShotSequence % 3 == 2)
            {
                FirePressurePod(song, start);
                smallShotSequence++;
                return;
            }

            int shooters = 2 + ((smallShotSequence + beat / 2) & 1);
            int fired = 0;
            for (int offset = 0; offset < crabs.Length && fired < shooters; offset++)
            {
                Crab crab = crabs[(start + offset) % crabs.Length];
                if (crab.state != FormState.Crawling || crab.defeated)
                    continue;
                Vector3 origin = crab.root.TransformPoint(new Vector3(0f, 0.92f, 3.15f));
                if (Vector3.Distance(origin, flight.Position) < 12f)
                    continue;
                int sequence = smallShotSequence * 3 + fired;
                Vector3 direction = WeakAim(origin, PressureSpeed);
                float yaw = ((sequence % 3) - 1) * 7f;
                float pitch = ((sequence & 1) == 0 ? -1f : 1f) * 2f;
                direction = ApplySpread(direction, yaw, pitch);
                if (!RegisterPressure(origin, direction, song,
                    (sequence & 1) == 0 ? new Color(0.09f, 0.75f, 0.82f) : new Color(0.85f, 0.58f, 0.2f),
                    PressureSpeed, radius: 1.2f, bubbleRing: false))
                    break;
                fired++;
            }
            smallShotSequence++;
        }

        void FirePressurePod(float song, int start)
        {
            if (bossActive || merging)
                return;

            for (int offset = 0; offset < crabs.Length; offset++)
            {
                Crab crab = crabs[(start + offset) % crabs.Length];
                if (crab.state != FormState.Crawling || crab.defeated)
                    continue;
                Vector3 origin = crab.root.TransformPoint(new Vector3(0f, 0.92f, 3.15f));
                if (Vector3.Distance(origin, flight.Position) < PressurePodMinimumDistance)
                    continue;

                Vector3 direction = WeakAim(origin, PressurePodSpeed);
                float flank = (smallShotSequence & 1) == 0 ? -24f : 24f;
                direction = ApplySpread(direction, flank, (smallShotSequence & 1) == 0 ? -2f : 2f);
                Color color = (smallShotSequence & 1) == 0
                    ? new Color(0.09f, 0.75f, 0.82f)
                    : new Color(0.85f, 0.58f, 0.2f);
                RegisterPressurePod(origin, direction, song, color);
                return;
            }
        }

        void FireBossPulse(float song)
        {
            int mode = bossPulseSequence++ & 1;
            if (mode != 0)
            {
                FireSupportPulse(song, bossPulseSequence);
                return;
            }
            int side = (bossPulseSequence & 2) == 0 ? -1 : 1;
            Vector3 local = HermitGeometry.BossClawOrigin(side, (float)AuthoredScore.BeatPosition(song));
            Vector3 origin = bossPosition + bossRotation * local;
            // One hollow ring follows a fixed, launch-time arc over the predicted player position.
            if (Vector3.Distance(origin, flight.Position) < 28f)
                return;
            const float duration = 5f;
            Vector3 aimPoint = flight.Position + flight.Velocity * (duration * 0.35f);
            BuildBallisticArc(origin, aimPoint, duration, out Vector3 velocity, out Vector3 acceleration);
            RegisterPressure(origin, velocity.normalized, song,
                new Color(0.18f, 0.82f, 0.77f), velocity.magnitude, radius: 9f, bubbleRing: true,
                acceleration: acceleration, lifetimeOverride: duration);
        }

        void FireSupportPulse(float song, int pulse)
        {
            int shooters = 2;
            int fired = 0;
            int start = (pulse * 7) % crabs.Length;
            for (int offset = 0; offset < crabs.Length && fired < shooters; offset++)
            {
                Crab crab = crabs[(start + offset) % crabs.Length];
                if (crab.state != FormState.Crawling || crab.defeated || !HasUnreservedSmallTarget(crab))
                    continue;
                Vector3 origin = crab.root.TransformPoint(new Vector3(0f, 0.92f, 3.15f));
                if (Vector3.Distance(origin, flight.Position) < 12f)
                    continue;
                Vector3 direction = WeakAim(origin, PressureSpeed);
                // Leave the giant ring's center open instead of filling it with
                // simultaneous support shots. The small bubbles flank the lane.
                float yaw = (fired == 0 ? -1f : 1f) * 16f;
                float pitch = (fired & 1) == 0 ? -1.5f : 1.5f;
                direction = ApplySpread(direction, yaw, pitch);
                Color color = ((pulse + fired) & 1) == 0
                    ? new Color(0.09f, 0.75f, 0.82f)
                    : new Color(0.85f, 0.58f, 0.2f);
                if (!RegisterPressure(origin, direction, song, color, PressureSpeed, radius: 1.2f, bubbleRing: false))
                    break;
                fired++;
            }
        }

        static bool HasUnreservedSmallTarget(Crab crab)
        {
            for (int i = 0; i < crab.targets.Length; i++)
                if (crab.targets[i] != null && crab.targets[i].hp - crab.targets[i].reserved > 0)
                    return true;
            return false;
        }

        static void BuildBallisticArc(Vector3 origin, Vector3 target, float duration,
            out Vector3 initialVelocity, out Vector3 acceleration)
        {
            float apexY = Mathf.Max(target.y + 27f, origin.y + 0.01f);
            float launchRise = apexY - origin.y;
            float landingRise = apexY - target.y;
            float timeToApex = duration / (1f + Mathf.Sqrt(landingRise / launchRise));
            float verticalAcceleration = -2f * launchRise / (timeToApex * timeToApex);
            Vector3 horizontalVelocity = new Vector3(target.x - origin.x, 0f, target.z - origin.z) / duration;
            initialVelocity = horizontalVelocity + Vector3.up * (-verticalAcceleration * timeToApex);
            acceleration = Vector3.up * verticalAcceleration;
        }

        bool RegisterPressure(Vector3 origin, Vector3 direction, float song, Color color, float speed,
            float radius = 1.2f, bool bubbleRing = false, Vector3? acceleration = null,
            float? lifetimeOverride = null)
        {
            if (combat.Peaceful || combat.PressureSlotUsage(this) + 1 > MaxOwnedPressureShots ||
                !combat.CanRegisterPressureShots(1))
                return false;
            if (combat.RegisterPressureShot(origin, direction, song, color, owner: this, speed: speed,
                radius: radius, bubbleRing: bubbleRing, acceleration: acceleration,
                lifetimeOverride: lifetimeOverride) == null)
                return false;
            if (bubbleRing) GiantBubbleRings++;
            else SmallBubbleShots++;
            return true;
        }

        bool RegisterPressurePod(Vector3 origin, Vector3 direction, float song, Color color)
        {
            if (combat.Peaceful || combat.PressureSlotUsage(this) + 4 > MaxOwnedPressureShots ||
                !combat.CanRegisterPressureShots(4))
                return false;
            if (combat.RegisterPressurePod(origin, direction, song, color,
                owner: this, speed: PressurePodSpeed) == null)
                return false;
            PressurePods++;
            return true;
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
                    if (crab == null)
                        continue;
                    bool visualReleased = false;
                    for (int point = 0; point < crab.targets.Length; point++)
                    {
                        LockTarget target = crab.targets[point];
                        if (!combat || target == null || !combat.Targets.Contains(target))
                            continue;
                        target.visual = visualReleased ? null : crab.visual;
                        combat.UnregisterEnvironment(target);
                        visualReleased = true;
                    }
                    if (!visualReleased && crab.visual)
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
            if (shoalObject)
                Destroy(shoalObject);
            if (shoalMesh)
                Destroy(shoalMesh);
            if (matterMaterial)
                Destroy(matterMaterial);
        }
    }
}
