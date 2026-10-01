using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class WhaleInheritance : MonoBehaviour
    {
        const int MaxSummons = 20;
        const int MaxGyres = 2;
        const int MaxGyreSpawns = 4;
        const int GyrePoints = 8;
        const int HitsPerSpearPoint = 3;
        const int CurtainShots = 5;
        const int MaxOwnedShots = 16;
        const float SummonStartAge = 0.35f;
        const float SummonInterval = 0.23f;
        const float GyreFirstAgeOffset = 1.4f;
        const float GyreSpacing = 3.6f;
        const float CurtainFirstAgeOffset = 2.8f;
        const float CurtainInterval = 6.2f;
        static readonly Color Blue = new(0.08f, 0.58f, 1f);
        static readonly Color Cyan = new(0.23f, 0.94f, 1f);
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static MaterialPropertyBlock MarkerProperties;

        sealed class Summon
        {
            public int index;
            public GameObject visual;
            public LockTarget target;
            public bool ray, spawned, defeated;
            public float phase;
        }

        sealed class SpearGyre
        {
            public int index;
            public GameObject spear;
            public GameObject ring;
            public readonly GameObject[] markers = new GameObject[GyrePoints];
            public readonly LockTarget[] targets = new LockTarget[GyrePoints];
            public readonly int[] hits = new int[GyrePoints];
            public bool spawned, retired, cooldownApplied;
            public float phase;
        }

        readonly Summon[] summons = new Summon[MaxSummons];
        readonly SpearGyre[] gyres = new SpearGyre[MaxGyres];
        readonly LockTarget[] summonTargetArray = new LockTarget[MaxSummons];
        readonly LockTarget[] spearTargetArray = new LockTarget[MaxGyres * GyrePoints];
        IReadOnlyList<LockTarget> summonTargetView, spearTargetView;
        Encounter combat;
        ParticleWorld world;
        Flight flight;
        DolphinEncounter dolphins;
        Mesh fishMesh, rayMesh, spearMesh, gyreMesh;
        BossId configuredMask, frozenMask;
        bool initialized, loadoutFrozen, pressureSpawningSuppressed;
        int summonCount, summonHits, gyresSpawned, spearHits, shotsSpawned, curtainsFired, curtainSequence;
        float nextSummonAge, nextGyreAge, nextCurtainAge;

        public BossId GrowthMask => loadoutFrozen ? frozenMask : configuredMask;
        public bool LoadoutFrozen => loadoutFrozen;
        public int SummonCount => summonCount;
        public int SummonsAlive
        {
            get
            {
                int count = 0;
                foreach (Summon summon in summons)
                    if (summon != null && summon.spawned && !summon.defeated) count++;
                return count;
            }
        }
        public int SummonHits => summonHits;
        public IReadOnlyList<LockTarget> SummonTargets => summonTargetView ?? Array.Empty<LockTarget>();
        public IReadOnlyList<LockTarget> SpearLockTargets => spearTargetView ?? Array.Empty<LockTarget>();
        public int SpearTargets
        {
            get
            {
                int count = 0;
                foreach (SpearGyre gyre in gyres)
                    if (gyre != null && IsGyreDeviceActive(gyre)) count += GyrePoints;
                return count;
            }
        }
        public int SpearHits => spearHits;
        public int ActiveGyres
        {
            get
            {
                int count = 0;
                foreach (SpearGyre gyre in gyres)
                    if (gyre != null && IsGyreDeviceActive(gyre)) count++;
                return count;
            }
        }
        public int RingShots => dolphins ? dolphins.BubbleRingShots : 0;
        public int ShotsSpawned => shotsSpawned;
        public int ShotsLive => combat ? combat.LivePressureShots(this) : 0;
        public int ShotsResolved => Mathf.Max(0, shotsSpawned - ShotsLive);
        public int CurtainsFired => curtainsFired;

        public void Initialize(Encounter encounter, ParticleWorld particleWorld, Flight pilot, DolphinEncounter school)
        {
            if (initialized) throw new InvalidOperationException("WhaleInheritance can only be initialized once.");
            MarkerProperties ??= new MaterialPropertyBlock();
            combat = encounter;
            world = particleWorld;
            flight = pilot;
            dolphins = school;
            if (!combat || !world || !flight || !dolphins || !world.NodeMesh || !world.NodeMaterial)
                throw new InvalidOperationException("WhaleInheritance requires combat, particles, flight, and dolphins.");

            fishMesh = BuildFishMesh();
            rayMesh = BuildRayMesh();
            spearMesh = BuildSpearMesh();
            gyreMesh = BuildGyreMesh();
            summonTargetView = Array.AsReadOnly(summonTargetArray);
            spearTargetView = Array.AsReadOnly(spearTargetArray);
            for (int i = 0; i < MaxSummons; i++)
            {
                Summon summon = new() { index = i, ray = i % 4 == 0, phase = i * 2.3999632f };
                summon.visual = PointCloud.Place(summon.ray ? "Whale inheritance / ray " + i :
                    "Whale inheritance / fish " + i, summon.ray ? rayMesh : fishMesh, world.NodeMaterial, transform);
                summon.visual.transform.localScale = Vector3.one * (summon.ray ? 2.5f : 1.8f);
                summon.visual.SetActive(false);
                summons[i] = summon;
                RegisterSummon(summon);
            }

            for (int i = 0; i < MaxGyres; i++)
            {
                SpearGyre gyre = new() { index = i, phase = i * Mathf.PI * 0.5f };
                gyre.spear = PointCloud.Place("Indra spear / blue gyre " + (i + 1), spearMesh,
                    world.NodeMaterial, transform);
                gyre.spear.SetActive(false);
                gyre.ring = PointCloud.Place("Indra spear / gyre " + (i + 1) + " / ring", gyreMesh,
                    world.NodeMaterial, transform);
                gyre.ring.SetActive(false);
                for (int point = 0; point < GyrePoints; point++)
                {
                    int gyreIndex = i, pointIndex = point;
                    gyre.markers[point] = PointCloud.Place("Indra spear / gyre " + (i + 1) +
                        " lock " + (point + 1), world.NodeMesh, world.NodeMaterial, transform);
                    gyre.markers[point].transform.localScale = Vector3.one * 1.45f;
                    SetMarkerTint(gyre.markers[point], Color.Lerp(Blue, Cyan, (point & 1) * 0.36f));
                    gyre.markers[point].SetActive(false);
                    gyre.targets[point] = combat.RegisterEnvironment(gyre.markers[point],
                        (target, song) => OnSpearHit(gyreIndex, pointIndex, target, song));
                    gyre.targets[point].hp = 0;
                    gyre.targets[point].acquireRange = 190f;
                    spearTargetArray[i * GyrePoints + point] = gyre.targets[point];
                }
                gyres[i] = gyre;
            }
            for (int i = 0; i < MaxSummons; i++) summonTargetArray[i] = summons[i].target;
            initialized = true;
        }

        public void ConfigureInheritance(BossId mask)
        {
            if (loadoutFrozen) return;
            configuredMask = mask & RunProgress.OptionalBosses;
            if (dolphins) dolphins.ConfigureInheritance(configuredMask);
        }

        public void Reset()
        {
            configuredMask = frozenMask = BossId.None;
            loadoutFrozen = false;
            pressureSpawningSuppressed = false;
            summonCount = summonHits = gyresSpawned = spearHits = shotsSpawned = curtainsFired = curtainSequence = 0;
            nextCurtainAge = 0f;
            nextSummonAge = nextGyreAge = 0f;
            if (dolphins) dolphins.ResetInheritance();
            if (!initialized) return;
            EnsureRegisteredTargets();
            foreach (Summon summon in summons)
            {
                summon.spawned = summon.defeated = false;
                summon.target.hp = summon.target.reserved = 0;
                summon.visual.SetActive(false);
            }
            foreach (SpearGyre gyre in gyres)
            {
                gyre.spawned = gyre.retired = false;
                gyre.cooldownApplied = false;
                Array.Clear(gyre.hits, 0, gyre.hits.Length);
                gyre.spear.SetActive(false);
                gyre.ring.SetActive(false);
                for (int i = 0; i < GyrePoints; i++)
                {
                    gyre.targets[i].hp = gyre.targets[i].reserved = 0;
                    gyre.markers[i].SetActive(false);
                }
            }
        }

        public void Tick(float song, float arrivalAge, float peakTime, bool arrivalTriggered,
            bool whaleReleased, bool recalling, bool whaleComplete, Vector3 whalePosition, Quaternion whaleRotation)
        {
            if (!initialized || !combat) return;
            if (arrivalTriggered && !loadoutFrozen)
            {
                frozenMask = configuredMask;
                loadoutFrozen = true;
                nextSummonAge = SummonStartAge;
                nextGyreAge = peakTime + GyreFirstAgeOffset;
                nextCurtainAge = peakTime + CurtainFirstAgeOffset;
                dolphins.ConfigureInheritance(frozenMask);
            }
            if (!arrivalTriggered) return;

            bool roomActive = !combat.ExplorationMode || combat.ActiveRoom == 2;
            bool mayMaintain = !whaleReleased && !recalling && !whaleComplete && !combat.Ended && !combat.Peaceful;
            bool targetsActive = mayMaintain && roomActive;
            bool canAttack = targetsActive && CaveLayout.NearestRoom(flight.Position) == 2 &&
                arrivalAge >= peakTime;
            if (!canAttack)
            {
                if (!pressureSpawningSuppressed && combat.LivePressureShots(this) > 0)
                    combat.ClearPressureShots(this);
                pressureSpawningSuppressed = true;
            }
            else pressureSpawningSuppressed = false;

            UpdateSummons(song, whalePosition, whaleRotation, arrivalAge, targetsActive);
            UpdateGyres(song, arrivalAge, targetsActive, whalePosition, whaleRotation);
            if (canAttack) FireCurtain(song, arrivalAge, whaleRotation);
        }

        void UpdateSummons(float song, Vector3 whalePosition, Quaternion whaleRotation, float arrivalAge, bool active)
        {
            bool inherited = (GrowthMask & BossId.Serpent) != 0;
            if (active && inherited && summonCount < MaxSummons && arrivalAge >= nextSummonAge)
            {
                SpawnSummon(summons[summonCount]);
                nextSummonAge = arrivalAge + SummonInterval;
            }

            for (int i = 0; i < summons.Length; i++)
            {
                Summon summon = summons[i];
                if (!summon.spawned)
                {
                    summon.target.hp = summon.target.reserved;
                    summon.visual.SetActive(summon.target.reserved > 0);
                    continue;
                }
                float angle = song * (summon.ray ? 0.29f : 0.42f) + summon.phase;
                Vector3 local = new(Mathf.Cos(angle) * (22f + i % 5 * 2.4f),
                    Mathf.Sin(angle * 1.31f) * (6f + i % 3), Mathf.Sin(angle) * (26f + i % 4 * 2f));
                Vector3 position = whalePosition + whaleRotation * local;
                summon.target.position = position;
                summon.visual.transform.SetPositionAndRotation(position, whaleRotation * Quaternion.Euler(0f,
                    Mathf.Sin(angle) * 12f, Mathf.Sin(angle * 0.7f) * 8f));
                int remaining = summon.defeated ? 0 : 1;
                summon.target.hp = active ? Mathf.Max(remaining, summon.target.reserved) : summon.target.reserved;
                summon.visual.SetActive(summon.target.hp > 0 || summon.target.reserved > 0);
            }
        }

        void SpawnSummon(Summon summon)
        {
            summon.spawned = true;
            summon.defeated = false;
            summon.target.hp = 1;
            summon.target.reserved = 0;
            summon.visual.SetActive(true);
            summonCount++;
        }

        void OnSummonHit(Summon summon, LockTarget target, float song)
        {
            if (summon.defeated) return;
            summonHits++;
            if (target.hp <= 0) summon.defeated = true;
            target.hp = Mathf.Max(summon.defeated ? 0 : 1, target.reserved);
            world.BurstAt(target.position, song, summon.ray ? Cyan : Blue, 0.85f);
        }

        void UpdateGyres(float song, float arrivalAge, bool active, Vector3 whalePosition, Quaternion whaleRotation)
        {
            foreach (SpearGyre gyre in gyres)
            {
                if (!gyre.spawned || gyre.cooldownApplied || !GyreComplete(gyre)) continue;
                gyre.cooldownApplied = true;
                nextGyreAge = Mathf.Max(nextGyreAge, arrivalAge + 12f);
            }

            if ((GrowthMask & BossId.Submarine) != 0 && active && gyresSpawned < MaxGyreSpawns && ActiveGyres < MaxGyres)
            {
                int slot = FindGyreSlot();
                if (slot >= 0 && arrivalAge >= nextGyreAge)
                {
                    SpawnGyre(gyres[slot]);
                    nextGyreAge = arrivalAge + GyreSpacing;
                }
            }

            for (int g = 0; g < gyres.Length; g++)
            {
                SpearGyre gyre = gyres[g];
                if (!gyre.spawned) continue;
                bool hasReservation = false;
                for (int i = 0; i < GyrePoints; i++)
                    hasReservation |= gyre.targets[i].reserved > 0;
                bool complete = GyreComplete(gyre);
                if (complete && !hasReservation) gyre.retired = true;

                if (gyre.retired || complete)
                {
                    gyre.spear.SetActive(false);
                    gyre.ring.SetActive(false);
                    for (int i = 0; i < GyrePoints; i++)
                    {
                        gyre.targets[i].hp = gyre.targets[i].reserved;
                        gyre.markers[i].SetActive(gyre.targets[i].reserved > 0);
                    }
                    continue;
                }

                Vector3 spearOffset = new(30f + g * 12f, -8f + g * 3f, -26f - g * 18f);
                Quaternion spearRotation = whaleRotation * Quaternion.Euler(-4f, -9f + g * 13f, 0f);
                gyre.spear.transform.SetPositionAndRotation(whalePosition + whaleRotation * spearOffset, spearRotation);
                gyre.spear.SetActive(true);
                Transform spearTransform = gyre.spear.transform;
                Vector3 tip = spearTransform.TransformPoint(new Vector3(0f, 0f, 51f));
                Vector3 center = tip + spearTransform.up * 7f;
                float spin = song * (0.48f + g * 0.08f) + gyre.phase;
                Quaternion pointRotation = spearTransform.rotation * Quaternion.AngleAxis(spin * Mathf.Rad2Deg, Vector3.forward);
                gyre.ring.transform.SetPositionAndRotation(center, pointRotation);
                gyre.ring.SetActive(true);
                for (int i = 0; i < GyrePoints; i++)
                {
                    Vector3 local = new(Mathf.Cos(i * Mathf.PI * 2f / GyrePoints) * 12f,
                        Mathf.Sin(i * Mathf.PI * 2f / GyrePoints) * 12f, 0f);
                    LockTarget target = gyre.targets[i];
                    target.position = center + pointRotation * local;
                    gyre.markers[i].transform.SetPositionAndRotation(target.position, pointRotation);
                    int remaining = Mathf.Max(0, HitsPerSpearPoint - gyre.hits[i]);
                    target.hp = active ? Mathf.Max(remaining, target.reserved) : target.reserved;
                    gyre.markers[i].SetActive(target.hp > 0 || target.reserved > 0);
                }
            }
        }

        void SpawnGyre(SpearGyre gyre)
        {
            gyre.spawned = true;
            gyre.retired = false;
            gyre.cooldownApplied = false;
            Array.Clear(gyre.hits, 0, gyre.hits.Length);
            gyre.spear.SetActive(true);
            gyre.ring.SetActive(true);
            for (int i = 0; i < GyrePoints; i++)
            {
                gyre.targets[i].hp = HitsPerSpearPoint;
                gyre.targets[i].reserved = 0;
                gyre.markers[i].SetActive(true);
            }
            gyresSpawned++;
        }

        void OnSpearHit(int gyreIndex, int pointIndex, LockTarget target, float song)
        {
            SpearGyre gyre = gyres[gyreIndex];
            if (!gyre.spawned || gyre.retired || gyre.hits[pointIndex] >= HitsPerSpearPoint) return;
            gyre.hits[pointIndex]++;
            spearHits++;
            target.hp = Mathf.Max(HitsPerSpearPoint - gyre.hits[pointIndex], target.reserved);
            world.BurstAt(target.position, song, Cyan, 1.15f);
        }

        void FireCurtain(float song, float arrivalAge, Quaternion whaleRotation)
        {
            if ((GrowthMask & BossId.Submarine) == 0 || arrivalAge < nextCurtainAge) return;
            int activeCount = ActiveGyres;
            if (activeCount == 0) return;
            if (combat.LivePressureShots(this) + CurtainShots > MaxOwnedShots ||
                !combat.CanRegisterPressureShots(CurtainShots))
            {
                nextCurtainAge = arrivalAge + 0.25f;
                return;
            }

            int deviceIndex = curtainSequence % activeCount;
            SpearGyre firingGyre = null;
            for (int i = 0; i < gyres.Length; i++)
            {
                if (!IsGyreDeviceActive(gyres[i])) continue;
                if (deviceIndex-- == 0)
                {
                    firingGyre = gyres[i];
                    break;
                }
            }
            if (firingGyre == null || !firingGyre.spear.activeSelf) return;
            Transform device = firingGyre.spear.transform;
            Vector3 origin = device.TransformPoint(new Vector3(0f, 0f, 51f));
            Vector3 toPlayer = flight.Position - origin;
            float travelTime = Mathf.Clamp(toPlayer.magnitude / 31f, 0f, 3.5f);
            Vector3 intercept = flight.Position + flight.Velocity * travelTime;
            Vector3 forward = (intercept - origin).sqrMagnitude > 0.001f
                ? (intercept - origin).normalized : device.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.94f ? whaleRotation * Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(Vector3.forward, forward);
            right.Normalize();
            up = Vector3.Cross(forward, right).normalized;
            float angle = (curtainSequence & 1) == 0 ? 9f : 6f;
            float vertical = (curtainSequence & 1) == 0 ? 3f : 11f;
            for (int i = 0; i < CurtainShots; i++)
            {
                float lane = i - (CurtainShots - 1) * 0.5f;
                Vector3 direction = Quaternion.AngleAxis(lane * angle, up) *
                    Quaternion.AngleAxis((lane * vertical) + ((curtainSequence & 1) == 0 ? 0f : 5f), right) * forward;
                if (combat.RegisterPressureShot(origin, direction, song, Blue, this, 31f, 0.9f,
                    lifetimeOverride: 8f) == null) break;
                shotsSpawned++;
            }
            curtainsFired++;
            curtainSequence++;
            float rate = dolphins ? dolphins.AttackRateMultiplier : 1f;
            nextCurtainAge = arrivalAge + CurtainInterval / rate;
            world.BurstAt(origin, song, Blue, 0.65f);
        }

        void EnsureRegisteredTargets()
        {
            foreach (Summon summon in summons)
                if (summon.target == null || !combat.Targets.Contains(summon.target)) RegisterSummon(summon);
            for (int g = 0; g < gyres.Length; g++)
            {
                SpearGyre gyre = gyres[g];
                for (int i = 0; i < GyrePoints; i++)
                {
                    if (gyre.targets[i] == null || !combat.Targets.Contains(gyre.targets[i]))
                    {
                        int gyreIndex = g, pointIndex = i;
                        gyre.targets[i] = combat.RegisterEnvironment(gyre.markers[i],
                            (target, song) => OnSpearHit(gyreIndex, pointIndex, target, song));
                        gyre.targets[i].hp = 0;
                        gyre.targets[i].acquireRange = 190f;
                    }
                    spearTargetArray[g * GyrePoints + i] = gyre.targets[i];
                }
            }
        }

        void RegisterSummon(Summon summon)
        {
            summon.target = combat.RegisterEnvironment(summon.visual, (target, song) => OnSummonHit(summon, target, song));
            summon.target.hp = 0;
            summon.target.acquireRange = 170f;
            summonTargetArray[summon.index] = summon.target;
        }

        int FindGyreSlot()
        {
            for (int i = 0; i < gyres.Length; i++)
                if (!gyres[i].spawned || gyres[i].retired) return i;
            return -1;
        }

        static bool GyreComplete(SpearGyre gyre)
        {
            for (int i = 0; i < GyrePoints; i++)
                if (gyre.hits[i] < HitsPerSpearPoint) return false;
            return true;
        }

        static bool IsGyreDeviceActive(SpearGyre gyre) =>
            gyre.spawned && !gyre.retired && !GyreComplete(gyre);

        static Mesh BuildFishMesh()
        {
            PointCloud cloud = new();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                float z = 1.55f - t * 3.1f;
                float width = Mathf.Sin((0.12f + t * 0.76f) * Mathf.PI) * 0.52f;
                for (int j = 0; j < 8; j++)
                {
                    float angle = j * Mathf.PI * 2f / 8f;
                    float y = Mathf.Sin(angle) * width * 0.55f;
                    float x = Mathf.Cos(angle) * width;
                    cloud.Add(new Vector3(x, y, z), 0.12f,
                        Color.Lerp(new Color(0.34f, 0.92f, 1f), new Color(0.76f, 1f, 1f), t), t);
                }
            }
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                float z = -0.72f - t * 1.15f;
                float spread = Mathf.Sin(t * Mathf.PI) * 0.7f;
                cloud.Add(new Vector3(-spread, 0f, z), 0.11f, Cyan, t);
                cloud.Add(new Vector3(spread, 0f, z), 0.11f, Cyan, t);
            }
            cloud.Add(new Vector3(0f, 0.16f, 0.72f), 0.09f, Color.white);
            return cloud.Build("Whale inheritance / small fish", 8f);
        }

        static Mesh BuildRayMesh()
        {
            PointCloud cloud = new();
            for (int i = 0; i <= 20; i++)
            {
                float x = i / 10f - 1f;
                float edge = 1f - Mathf.Abs(x);
                float chord = edge * 1.7f;
                for (int j = 0; j <= 6; j++)
                {
                    float t = j / 6f * 2f - 1f;
                    Vector3 p = new(x * 1.55f, t * 0.11f, edge * 0.8f - Mathf.Abs(t) * chord * 0.58f);
                    cloud.Add(p, 0.13f, Color.Lerp(Blue, new Color(0.5f, 0.96f, 1f), edge), (x + 1f) * 0.5f);
                }
            }
            for (int i = 0; i < 10; i++)
                cloud.Add(new Vector3(0f, 0f, -0.1f - i * 0.2f), 0.075f, Cyan, i / 10f);
            return cloud.Build("Whale inheritance / small ray", 8f);
        }

        static Mesh BuildGyreMesh()
        {
            PointCloud cloud = new();
            for (int i = 0; i < 144; i++)
            {
                float a = i * Mathf.PI * 2f / 144f;
                float radius = 12f + Mathf.Sin(a * 8f) * 0.38f;
                float z = Mathf.Sin(a * 8f) * 0.3f;
                cloud.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z),
                    0.22f + (i % 3) * 0.045f, Color.Lerp(Blue, Cyan, (i % 12) / 11f), i / 144f);
            }
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                cloud.Add(new Vector3(Mathf.Cos(a) * 8.7f, Mathf.Sin(a) * 8.7f, 0f),
                    0.10f, new Color(0.12f, 0.48f, 1f), i / 48f);
            }
            return cloud.Build("Indra spear / gyre matter", 40f);
        }

        static Mesh BuildSpearMesh()
        {
            PointCloud cloud = new();
            for (int i = 0; i <= 78; i++)
            {
                float t = i / 78f;
                float z = -48f + t * 76f;
                float radius = 0.62f + Mathf.Sin(t * Mathf.PI * 5f) * 0.12f;
                for (int j = 0; j < 5; j++)
                {
                    float a = j * Mathf.PI * 2f / 5f + t * 7f;
                    cloud.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z),
                        0.16f, Color.Lerp(new Color(0.13f, 0.52f, 0.88f), Cyan, t));
                }
            }
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f;
                float width = Mathf.Sin(t * Mathf.PI) * (5.2f + Mathf.Sin(t * Mathf.PI) * 4.2f);
                float z = 19f + t * 32f;
                for (int j = 0; j <= 12; j++)
                {
                    float across = j / 6f - 1f;
                    float y = (1f - across * across) * 1.35f;
                    Color color = Mathf.Abs(across) > 0.82f ? Cyan :
                        Color.Lerp(new Color(0.18f, 0.66f, 1f), new Color(0.82f, 1f, 1f), 1f - Mathf.Abs(across));
                    cloud.Add(new Vector3(across * width, y, z), 0.14f, color, t);
                }
                cloud.Add(new Vector3(0f, 1.45f, z), 0.16f, Color.white, t);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i <= 28; i++)
                {
                    float t = i / 28f;
                    float width = Mathf.Sin(t * Mathf.PI) * 8.8f;
                    cloud.Add(new Vector3(side * width, 0.15f, 19f + t * 32f), 0.18f, Cyan, t);
                }
            return cloud.Build("Indra spear / leaf head and staff", 150f);
        }

        static void SetMarkerTint(GameObject marker, Color color)
        {
            Renderer renderer = marker ? marker.GetComponent<Renderer>() : null;
            if (!renderer) return;
            renderer.GetPropertyBlock(MarkerProperties);
            MarkerProperties.SetColor(TintId, color);
            renderer.SetPropertyBlock(MarkerProperties);
        }

        void OnDestroy()
        {
            if (combat) combat.ClearPressureShots(this);
            if (fishMesh) Destroy(fishMesh);
            if (rayMesh) Destroy(rayMesh);
            if (spearMesh) Destroy(spearMesh);
            if (gyreMesh) Destroy(gyreMesh);
        }
    }
}
