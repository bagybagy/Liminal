using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class SubmarineEncounter : MonoBehaviour
    {
        const int SubmarinePhase = 0;
        const int FleetPhase = 1;
        const int GiantPhase = 2;
        const int ReefPhase = 3;
        const int TotalGoal = 160;
        const float MorphSeconds = 4f;
        const float FleetWallMargin = 100f;

        sealed class TargetSlot
        {
            public GameObject visual;
            public Renderer renderer;
            public LockTarget target;
            public int phase, index;
        }

        readonly List<LockTarget> publicTargets = new(32);
        Encounter combat;
        ParticleWorld world;
        Flight flight;
        MusicTransport music;
        Mesh matterMesh;
        Material matterMaterial;
        GameObject matterObject;
        TargetSlot[] submarinePool, fleetPool, giantPool;
        Matrix4x4 submarineMatrix, fleetMatrix, giantMatrix, reefMatrix;
        MaterialPropertyBlock markerProperties;
        int submarineMatrixId, fleetMatrixId, giantMatrixId, reefMatrixId;
        int formFromId, formToId, morphId, beatPositionId, songId, reducedId, tintId, spearChargeId;
        int activePoolCount, lastWholeBeat = -1;
        int fromForm, toForm;
        float transitionStartSong, fleetAngle;
        bool initialized, roomIsActive, closing, morphStarted;
        Vector3 submarinePosition, focus;
        Vector3 spearAim;
        int spearPattern = -1;
        float geometryBeat, spearChargeBeat;

        public IReadOnlyList<LockTarget> Targets => publicTargets;
        public bool Complete { get; private set; }
        public bool Transitioning { get; private set; }
        public int Phase { get; private set; }
        public int PhaseHits { get; private set; }
        public int TotalHits { get; private set; }
        public int ParticleCount { get; private set; }
        public int InitializationCount { get; private set; }
        public float Progress => Mathf.Clamp01(TotalHits / (float)TotalGoal);
        public float TransitionProgress { get; private set; }
        public string Status { get; private set; } = "SCARLET ENGINE / SUBMARINE 0/48";
        public Vector3 Focus => focus;
        public int CompletedPhases { get; private set; }
        public Vector3 SubmarinePosition => submarinePosition;
        public Vector3 GiantChestPosition => giantMatrix.MultiplyPoint3x4(new Vector3(0f, -204f, 0f));
        public Vector3 SpearTip => giantMatrix.MultiplyPoint3x4(
            SubmarineGeometry.PoseGiant(SubmarineGeometry.SpearTip, 4, geometryBeat));
        public int SpearShots { get; private set; }
        public int SpearVolleys { get; private set; }
        public int SpearTelegraphs { get; private set; }
        public bool SpearCharging => spearPattern >= 0;

        public void Initialize(Encounter combat, ParticleWorld world, Flight flight, MusicTransport music)
        {
            if (initialized) throw new InvalidOperationException("SubmarineEncounter is already initialized.");
            if (!combat) throw new ArgumentNullException(nameof(combat));
            if (!world) throw new ArgumentNullException(nameof(world));
            if (!flight) throw new ArgumentNullException(nameof(flight));
            if (!music) throw new ArgumentNullException(nameof(music));

            this.combat = combat;
            this.world = world;
            this.flight = flight;
            this.music = music;

            Shader shader = Resources.Load<Shader>("SubmarineMatter");
            if (!shader) shader = Shader.Find("Liminal/Submarine Matter");
            if (!shader) throw new InvalidOperationException("Liminal/Submarine Matter shader is missing.");

            submarineMatrixId = Shader.PropertyToID("_SubmarineToWorld");
            fleetMatrixId = Shader.PropertyToID("_FleetToWorld");
            giantMatrixId = Shader.PropertyToID("_GiantToWorld");
            reefMatrixId = Shader.PropertyToID("_ReefToWorld");
            formFromId = Shader.PropertyToID("_FormFrom");
            formToId = Shader.PropertyToID("_FormTo");
            morphId = Shader.PropertyToID("_Morph");
            beatPositionId = Shader.PropertyToID("_BeatPosition");
            songId = Shader.PropertyToID("_Song");
            reducedId = Shader.PropertyToID("_Reduced");
            tintId = Shader.PropertyToID("_Tint");
            spearChargeId = Shader.PropertyToID("_SpearCharge");
            markerProperties = new MaterialPropertyBlock();

            matterMesh = SubmarineGeometry.Build();
            matterMaterial = new Material(shader) { name = "Scarlet Engine matter" };
            matterMaterial.SetFloat("_Gain", 2.6f);
            matterObject = new GameObject("Scarlet Engine / persistent matter");
            matterObject.transform.position = SubmarineGeometry.RoomCenter;
            matterObject.AddComponent<MeshFilter>().sharedMesh = matterMesh;
            var matterRenderer = matterObject.AddComponent<MeshRenderer>();
            matterRenderer.sharedMaterial = matterMaterial;
            matterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            matterRenderer.receiveShadows = false;
            matterObject.SetActive(roomIsActive);

            submarinePool = CreatePool(SubmarinePhase, SubmarineGeometry.SubmarineTargets);
            fleetPool = CreatePool(FleetPhase, SubmarineGeometry.FleetTargets);
            giantPool = CreatePool(GiantPhase, SubmarineGeometry.GiantTargets);
            ParticleCount = SubmarineGeometry.ParticleCount;
            InitializationCount++;
            initialized = true;
            Phase = SubmarinePhase;
            fromForm = toForm = Phase;
            lastWholeBeat = Mathf.FloorToInt((float)AuthoredScore.BeatPosition(music.Time));
            UpdateGeometry((float)music.Time, 0f, (float)AuthoredScore.BeatPosition(music.Time));
            RegisterPool(submarinePool, submarinePool.Length);
            SetStatus();
        }

        public void Tick(float song, float dt, bool roomActive)
        {
            if (!initialized) return;
            song = Mathf.Max(0f, song);
            dt = Mathf.Max(0f, dt);
            float beatPosition = (float)AuthoredScore.BeatPosition(song);

            bool leavingRoom = roomIsActive && !roomActive;
            roomIsActive = roomActive;
            if (leavingRoom)
            {
                RemoveOwnedLocks();
                combat.ClearPressureShots(this);
                ClearSpearPattern();
            }

            if (matterObject && matterObject.activeSelf != roomActive)
                matterObject.SetActive(roomActive);

            if (SpearCharging && (combat.Ended || combat.Peaceful || beatPosition - spearChargeBeat > 4.5f))
                ClearSpearPattern();
            UpdateGeometry(song, dt, beatPosition);
            UpdateTargetPoses(beatPosition);
            UpdateTargetVisibility();

            if (closing && !morphStarted && !HasReservedTargets())
                StartMorph(song);

            if (morphStarted)
            {
                TransitionProgress = Mathf.Clamp01((song - transitionStartSong) / MorphSeconds);
                matterMaterial.SetFloat(morphId, TransitionProgress);
                if (TransitionProgress >= 1f)
                    FinishMorph(song, beatPosition);
            }

            ProcessBeats(Mathf.FloorToInt(beatPosition), song);
            if (!Transitioning && !Complete && !closing && !combat.Peaceful)
                EvaluateRearm();

            UpdateTargetPoses(beatPosition);
            UpdateTargetVisibility();
        }

        public void ResetEncounter()
        {
            if (!initialized) return;
            combat.ClearPressureShots(this);
            ClearSpearPattern();
            SpearShots = SpearVolleys = SpearTelegraphs = 0;
            UnregisterPool(submarinePool);
            UnregisterPool(fleetPool);
            UnregisterPool(giantPool);

            Complete = false;
            Transitioning = false;
            closing = false;
            morphStarted = false;
            TransitionProgress = 0f;
            Phase = SubmarinePhase;
            PhaseHits = TotalHits = CompletedPhases = 0;
            fleetAngle = 0f;
            fromForm = toForm = Phase;
            transitionStartSong = 0f;
            float song = (float)music.Time;
            float beat = (float)AuthoredScore.BeatPosition(song);
            lastWholeBeat = Mathf.FloorToInt(beat);
            UpdateGeometry(song, 0f, beat);
            if (matterObject) matterObject.SetActive(roomIsActive);
            RegisterPool(submarinePool, submarinePool.Length);
            SetStatus();
        }

        TargetSlot[] CreatePool(int phase, int count)
        {
            var pool = new TargetSlot[count];
            for (int i = 0; i < count; i++)
            {
                var slot = new TargetSlot { phase = phase, index = i };
                EnsureVisual(slot);
                pool[i] = slot;
            }
            return pool;
        }

        void EnsureVisual(TargetSlot slot)
        {
            if (slot.visual) return;
            slot.visual = PointCloud.Place("Scarlet Engine target", world.NodeMesh, world.NodeMaterial, null);
            slot.visual.SetActive(false);
            slot.renderer = slot.visual.GetComponent<Renderer>();
            SetMarkerTint(slot);
        }

        void RegisterPool(TargetSlot[] pool, int count)
        {
            if (pool == null || !combat) return;
            UnregisterPool(pool);
            activePoolCount = Mathf.Clamp(count, 0, pool.Length);
            publicTargets.Clear();
            for (int i = 0; i < activePoolCount; i++)
            {
                TargetSlot slot = pool[i];
                EnsureVisual(slot);
                slot.target = combat.RegisterEnvironment(slot.visual, (target, song) => OnTargetHit(slot, target, song));
                if (slot.target == null) continue;
                slot.target.acquireRange = slot.phase == FleetPhase ? 145f : 155f;
                if (slot.phase == SubmarinePhase) slot.target.acquireRange = 160f;
                if (slot.phase == GiantPhase) slot.target.acquireRange = 330f;
                slot.target.position = TargetPosition(slot, slot.phase == GiantPhase ? geometryBeat : 0f);
                slot.visual.transform.position = slot.target.position;
                slot.visual.transform.localScale = Vector3.one * 1.35f;
                slot.visual.SetActive(roomIsActive);
                publicTargets.Add(slot.target);
            }
        }

        void UnregisterPool(TargetSlot[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                TargetSlot slot = pool[i];
                if (slot.target != null)
                {
                    LockTarget target = slot.target;
                    combat.Locks.Remove(target);
                    bool registered = combat.Targets.Contains(target);
                    if (registered) combat.UnregisterEnvironment(target);
                    target.onHit = null;
                    slot.target = null;
                    if (registered)
                    {
                        slot.visual = null;
                        slot.renderer = null;
                    }
                }
                if (slot.visual) slot.visual.SetActive(false);
            }
            if (pool == CurrentPool())
            {
                publicTargets.Clear();
                activePoolCount = 0;
            }
        }

        void OnTargetHit(TargetSlot slot, LockTarget target, float song)
        {
            if (!initialized || slot.phase != Phase || Complete) return;
            PhaseHits++;
            TotalHits++;
            world.BurstAt(target.position, song, HitColor(Phase), Phase == GiantPhase ? 1.3f : 1f);
            if (PhaseHits >= GoalFor(Phase)) BeginClosing();
            SetStatus();
        }

        void BeginClosing()
        {
            if (closing || Complete) return;
            closing = true;
            ClearSpearPattern();
            if (Phase == GiantPhase) combat.ClearPressureShots(this);
            Transitioning = true;
            TransitionProgress = 0f;
            RemoveOwnedLocks();
            SetPoolVisibility(CurrentPool());
            if (matterMaterial) matterMaterial.SetFloat(morphId, 0f);
            SetStatus();
        }

        void StartMorph(float song)
        {
            UnregisterPool(CurrentPool());
            combat.ClearPressureShots(this);
            ClearSpearPattern();
            fromForm = Phase;
            toForm = Phase == GiantPhase ? ReefPhase : Phase + 1;
            transitionStartSong = song;
            TransitionProgress = 0f;
            morphStarted = true;
            matterMaterial.SetFloat(formFromId, fromForm);
            matterMaterial.SetFloat(formToId, toForm);
            matterMaterial.SetFloat(morphId, 0f);
            SetStatus();
        }

        void FinishMorph(float song, float beatPosition)
        {
            Phase = toForm;
            CompletedPhases = Phase;
            PhaseHits = 0;
            Transitioning = false;
            closing = false;
            morphStarted = false;
            TransitionProgress = 0f;
            fromForm = toForm = Phase;
            matterMaterial.SetFloat(formFromId, Phase);
            matterMaterial.SetFloat(formToId, Phase);
            matterMaterial.SetFloat(morphId, 0f);
            UpdateGeometry(song, 0f, beatPosition);

            if (Phase == ReefPhase)
            {
                Complete = true;
                combat.ClearPressureShots(this);
                SetStatus();
                return;
            }

            TargetSlot[] pool = CurrentPool();
            RegisterPool(pool, pool.Length);
            UpdateTargetPoses(beatPosition);
            UpdateTargetVisibility();
            SetStatus();
        }

        void EvaluateRearm()
        {
            TargetSlot[] pool = CurrentPool();
            if (pool == null) return;
            int available = 0;
            for (int i = 0; i < activePoolCount; i++)
                if (pool[i].target != null)
                    available += Mathf.Max(0, pool[i].target.hp - pool[i].target.reserved);

            int goalRemaining = GoalFor(Phase) - PhaseHits;
            if (available > 3 || available >= goalRemaining || HasReservedTargets()) return;
            RegisterPool(pool, Mathf.Min(pool.Length, goalRemaining));
        }

        bool HasReservedTargets()
        {
            TargetSlot[] pool = CurrentPool();
            if (pool == null) return false;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].target != null && pool[i].target.reserved > 0)
                    return true;
            return false;
        }

        void RemoveOwnedLocks()
        {
            TargetSlot[] pool = CurrentPool();
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].target != null)
                    combat.Locks.Remove(pool[i].target);
        }

        void UpdateGeometry(float song, float dt, float beatPosition)
        {
            geometryBeat = beatPosition;
            Vector3 center = SubmarineGeometry.RoomCenter;
            float orbit = song * 0.15f;
            Vector3 position = center + new Vector3(Mathf.Cos(orbit) * 135f,
                Mathf.Sin(song * 0.071f) * 20f, Mathf.Sin(orbit) * 145f);
            Vector3 tangent = new Vector3(-Mathf.Sin(orbit) * 20.25f,
                Mathf.Cos(song * 0.071f) * 1.42f, Mathf.Cos(orbit) * 21.75f);
            Quaternion submarineRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            submarineMatrix = Matrix4x4.TRS(position, submarineRotation, Vector3.one);
            submarinePosition = position;

            float beatCycle = beatPosition - Mathf.Floor(beatPosition / 16f) * 16f;
            float burstIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 4.8f, beatCycle));
            float burstOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 7f, beatCycle));
            float speedBurst = Mathf.Clamp01(burstIn * burstOut);
            fleetAngle += dt * (0.055f + speedBurst * 0.68f);
            Vector3 fleetCenter = ClampFleetCenter(flight.Position);
            fleetMatrix = Matrix4x4.TRS(fleetCenter, Quaternion.Euler(0f, fleetAngle * Mathf.Rad2Deg, 0f), Vector3.one);
            giantMatrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
            reefMatrix = giantMatrix;

            float shaderFrom = morphStarted ? fromForm : Phase;
            float shaderTo = morphStarted ? toForm : Phase;
            matterMaterial.SetMatrix(submarineMatrixId, submarineMatrix);
            matterMaterial.SetMatrix(fleetMatrixId, fleetMatrix);
            matterMaterial.SetMatrix(giantMatrixId, giantMatrix);
            matterMaterial.SetMatrix(reefMatrixId, reefMatrix);
            matterMaterial.SetFloat(formFromId, shaderFrom);
            matterMaterial.SetFloat(formToId, shaderTo);
            matterMaterial.SetFloat(beatPositionId, beatPosition);
            matterMaterial.SetFloat(songId, song);
            matterMaterial.SetFloat(reducedId, flight.ReducedMotion ? 1f : 0f);
            float charge = SpearCharging ? Mathf.Clamp01((beatPosition - spearChargeBeat) / 2f) : 0f;
            matterMaterial.SetFloat(spearChargeId, charge);

            if (Phase == SubmarinePhase) focus = submarinePosition;
            else if (Phase == FleetPhase) focus = flight.Position;
            else if (Phase == GiantPhase) focus = giantMatrix.MultiplyPoint3x4(new Vector3(0f, -190f, 0f));
            else focus = reefMatrix.MultiplyPoint3x4(new Vector3(0f, -238f, 0f));
        }

        void UpdateTargetPoses(float beatPosition)
        {
            UpdatePoolPoses(submarinePool, beatPosition);
            UpdatePoolPoses(fleetPool, beatPosition);
            UpdatePoolPoses(giantPool, beatPosition);
        }

        void UpdatePoolPoses(TargetSlot[] pool, float beatPosition)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                TargetSlot slot = pool[i];
                Vector3 position = TargetPosition(slot, beatPosition);
                if (slot.target != null) slot.target.position = position;
                if (slot.visual) slot.visual.transform.position = position;
            }
        }

        Vector3 TargetPosition(TargetSlot slot, float beatPosition)
        {
            if (slot.phase == SubmarinePhase)
                return submarineMatrix.MultiplyPoint3x4(SubmarineGeometry.SubmarineTarget(slot.index));
            if (slot.phase == FleetPhase)
                return fleetMatrix.MultiplyPoint3x4(SubmarineGeometry.FleetTarget(slot.index / 8, slot.index % 8));
            Vector3 local = SubmarineGeometry.GiantTarget(slot.index, out int joint);
            return giantMatrix.MultiplyPoint3x4(SubmarineGeometry.PoseGiant(local, joint, beatPosition));
        }

        void UpdateTargetVisibility()
        {
            SetPoolVisibility(submarinePool);
            SetPoolVisibility(fleetPool);
            SetPoolVisibility(giantPool);
        }

        void SetPoolVisibility(TargetSlot[] pool)
        {
            if (pool == null) return;
            bool activePhase = pool == CurrentPool();
            for (int i = 0; i < pool.Length; i++)
            {
                TargetSlot slot = pool[i];
                bool visible = roomIsActive && !combat.Peaceful && activePhase && slot.target != null && slot.target.hp > 0 &&
                    (!closing || slot.target.reserved > 0);
                if (slot.visual && slot.visual.activeSelf != visible)
                    slot.visual.SetActive(visible);
            }
        }

        void ProcessBeats(int currentBeat, float song)
        {
            if (lastWholeBeat < 0)
            {
                lastWholeBeat = currentBeat;
                return;
            }
            if (currentBeat < lastWholeBeat)
            {
                lastWholeBeat = currentBeat;
                ClearSpearPattern();
                if (Phase == GiantPhase) combat.ClearPressureShots(this);
                return;
            }
            int first = Mathf.Max(lastWholeBeat + 1, currentBeat - 32);
            // A skipped frame must not replay a phrase of spear curtains at one origin/time.
            if (Phase == GiantPhase) first = Mathf.Max(first, currentBeat);
            for (int beat = first; beat <= currentBeat; beat++)
                ProcessBeat(beat, song);
            lastWholeBeat = currentBeat;
        }

        void ProcessBeat(int beat, float song)
        {
            if (!roomIsActive || Transitioning || Complete || closing || combat.Ended || combat.Peaceful) return;
            int position = beat % 8;
            if (Phase == SubmarinePhase)
            {
                if (position == 0) FireTorpedoFan(song, beat);
                else if (position == 3 || position == 4 || position == 5 || position == 6)
                    FireSequentialTorpedo(song, beat, position);
            }
            else if (Phase == FleetPhase)
            {
                if (position == 0 || position == 2 || position == 4 || position == 6)
                    FireCraftArc(song, beat, (beat / 2) % 3);
            }
            else if (Phase == GiantPhase)
            {
                int stage = beat % 32;
                if (stage == 0) TelegraphSpear(song, beat, 0);
                else if (stage == 10) TelegraphSpear(song, beat, 1);
                else if (stage == 22) TelegraphSpear(song, beat, 2);
                else if (stage == 2 || stage == 4) FireSpearCurtain(song, beat, 0, stage == 4);
                else if (stage == 12 || stage == 14) FireSpearCurtain(song, beat, 1, stage == 14);
                else if (stage == 24 || stage == 26) FireSpearCurtain(song, beat, 2, stage == 26);
            }
        }

        void FireTorpedoFan(float song, int beat)
        {
            Vector3 aim = LeadPoint();
            for (int i = 0; i < 5; i++)
            {
                int tube = i % 4;
                int side = i % 2 == 0 ? -1 : 1;
                Vector3 origin = submarineMatrix.MultiplyPoint3x4(SubmarineGeometry.SubmarineMuzzle(side, tube));
                Vector3 forward = (aim - origin).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float spread = (i - 2) * 0.075f;
                Vector3 direction = (forward + right * spread + Vector3.up * ((i - 2) * 0.014f)).normalized;
                Launch(origin, direction, song, 22f + (i + beat % 3) * 1.1f, HitColor(SubmarinePhase));
            }
        }

        void FireSequentialTorpedo(float song, int beat, int position)
        {
            int side = (position + beat) % 2 == 0 ? -1 : 1;
            Vector3 origin = submarineMatrix.MultiplyPoint3x4(SubmarineGeometry.SubmarineMuzzle(side, position - 3));
            Vector3 aim = LeadPoint();
            Vector3 direction = (aim - origin).normalized;
            Launch(origin, direction, song, 24f + (beat % 3), HitColor(SubmarinePhase));
        }

        void FireCraftArc(float song, int beat, int craft)
        {
            Vector3 aim = LeadPoint();
            for (int i = 0; i < 3; i++)
            {
                Vector3 origin = fleetMatrix.MultiplyPoint3x4(SubmarineGeometry.FleetTarget(craft, 3 + i));
                Vector3 forward = (aim - origin).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float spread = (i - 1) * 0.09f;
                Vector3 direction = (forward + right * spread).normalized;
                Launch(origin, direction, song, 22f + ((beat + i) % 6), HitColor(FleetPhase));
            }
        }

        void TelegraphSpear(float song, int beat, int pattern)
        {
            spearAim = LeadPoint();
            spearPattern = pattern;
            spearChargeBeat = beat;
            SpearTelegraphs++;
            world.BurstAt(SpearTip, song, new Color(0.18f, 0.68f, 1f), 0.85f);
        }

        void FireSpearCurtain(float song, int beat, int pattern, bool secondLayer)
        {
            if (spearPattern != pattern) return;
            if (beat - spearChargeBeat > 4f)
            {
                ClearSpearPattern();
                return;
            }
            int count = pattern == 1 ? 5 : 7;
            if (combat.LivePressureShots(this) + count > 24 || !combat.CanRegisterPressureShots(count))
            {
                if (secondLayer) ClearSpearPattern();
                return;
            }

            Vector3 origin = SpearTip;
            Vector3 forward = spearAim - origin;
            if (forward.sqrMagnitude < 0.001f) forward = giantMatrix.MultiplyVector(Vector3.forward);
            forward.Normalize();
            Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(reference, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            float layer = secondLayer ? 1f : -1f;
            int lanes = pattern == 0 ? 9 : pattern == 1 ? 7 : 10;
            int fired = 0;
            for (int i = 0; i < lanes; i++)
            {
                float x, y;
                if (pattern == 0)
                {
                    // A broad fan with a persistent central escape lane and one side opening.
                    if (i == 4 || i == ((beat / 32) % 2 == 0 ? 2 : 6)) continue;
                    x = (i - 4) * 0.12f;
                    y = layer * 0.065f;
                }
                else if (pattern == 1)
                {
                    if (i == 3 || i == ((beat / 32) % 2 == 0 ? 1 : 5)) continue;
                    float angle = (i - 3) * Mathf.PI / 6f;
                    x = Mathf.Sin(angle) * 0.38f;
                    y = Mathf.Cos(angle) * 0.22f * layer;
                }
                else
                {
                    // An expanding electric ring, not a filled disk; three sectors stay open.
                    if (i == 0 || i == 1 || i == 5) continue;
                    float angle = i * Mathf.PI * 2f / 10f + layer * 0.12f;
                    x = Mathf.Cos(angle) * 0.32f;
                    y = Mathf.Sin(angle) * 0.32f;
                }
                Vector3 direction = (forward + right * x + up * y).normalized;
                // Fixed, interceptable trajectories. No tracking after the authored warning.
                float speed = 19f + (i % 3) * 1.5f;
                Color electric = new Color(0.24f + (i % 2) * 0.26f, 0.72f, 1f);
                if (combat.RegisterPressureShot(origin, direction, song, electric, this, speed) != null)
                {
                    SpearShots++;
                    fired++;
                }
            }
            if (fired > 0)
            {
                SpearVolleys++;
                world.BurstAt(origin, song, new Color(0.62f, 0.89f, 1f), 1.1f);
            }
            if (secondLayer) ClearSpearPattern();
        }

        void ClearSpearPattern()
        {
            spearPattern = -1;
            spearAim = Vector3.zero;
            spearChargeBeat = 0f;
            if (matterMaterial) matterMaterial.SetFloat(spearChargeId, 0f);
        }

        void Launch(Vector3 origin, Vector3 direction, float song, float speed, Color color)
        {
            if (combat.LivePressureShots(this) >= 16 || !combat.CanRegisterPressureShots(1)) return;
            combat.RegisterPressureShot(origin, direction, song, color, this, Mathf.Clamp(speed, 22f, 28f));
        }

        Vector3 LeadPoint()
        {
            return flight.Position + Vector3.ClampMagnitude(flight.Velocity * 0.42f, 18f);
        }

        static Vector3 ClampFleetCenter(Vector3 position)
        {
            CaveLayout.Chamber room = CaveLayout.Rooms[4];
            Vector3 radius = room.Radius - Vector3.one * FleetWallMargin;
            Vector3 offset = position - room.Center;
            float normalized = Mathf.Sqrt(offset.x * offset.x / (radius.x * radius.x) +
                offset.y * offset.y / (radius.y * radius.y) + offset.z * offset.z / (radius.z * radius.z));
            if (normalized > 1f) offset /= normalized;
            return room.Center + offset;
        }

        TargetSlot[] CurrentPool()
        {
            if (Phase == SubmarinePhase) return submarinePool;
            if (Phase == FleetPhase) return fleetPool;
            if (Phase == GiantPhase) return giantPool;
            return null;
        }

        static int GoalFor(int phase)
        {
            if (phase == SubmarinePhase || phase == FleetPhase) return 48;
            return phase == GiantPhase ? 64 : 0;
        }

        static Color HitColor(int phase)
        {
            if (phase == SubmarinePhase) return new Color(1f, 0.18f, 0.075f);
            if (phase == FleetPhase) return new Color(0.12f, 0.78f, 0.88f);
            return new Color(1f, 0.52f, 0.14f);
        }

        void SetMarkerTint(TargetSlot slot)
        {
            if (slot.renderer == null) return;
            Color color = HitColor(slot.phase);
            markerProperties.Clear();
            markerProperties.SetColor(tintId, color);
            slot.renderer.SetPropertyBlock(markerProperties);
        }

        void SetStatus()
        {
            if (Complete)
            {
                Status = "SCARLET ENGINE / COMPLETE";
                return;
            }
            if (Transitioning)
            {
                Status = Phase == GiantPhase ? "SCARLET ENGINE / ENGINE AWAKENING" : "SCARLET ENGINE / MATTER FORMING";
                return;
            }
            string label = Phase == SubmarinePhase ? "SUBMARINE" : Phase == FleetPhase ? "SUBMERSIBLES" : "POSEIDON / INDRA'S ARROW";
            Status = $"SCARLET ENGINE / {label} {PhaseHits}/{GoalFor(Phase)}";
        }

        void OnDisable()
        {
            if (!initialized || !combat) return;
            roomIsActive = false;
            if (matterObject) matterObject.SetActive(false);
            RemoveOwnedLocks();
            combat.ClearPressureShots(this);
            ClearSpearPattern();
            HidePool(submarinePool);
            HidePool(fleetPool);
            HidePool(giantPool);
        }

        static void HidePool(TargetSlot[] pool)
        {
            if (pool == null) return;
            foreach (TargetSlot slot in pool)
                if (slot.visual) slot.visual.SetActive(false);
        }

        void OnDestroy()
        {
            if (combat)
            {
                combat.ClearPressureShots(this);
                UnregisterPool(submarinePool);
                UnregisterPool(fleetPool);
                UnregisterPool(giantPool);
            }
            DestroyPoolVisuals(submarinePool);
            DestroyPoolVisuals(fleetPool);
            DestroyPoolVisuals(giantPool);
            if (matterObject) Destroy(matterObject);
            if (matterMaterial) Destroy(matterMaterial);
            if (matterMesh) Destroy(matterMesh);
        }

        static void DestroyPoolVisuals(TargetSlot[] pool)
        {
            if (pool == null) return;
            foreach (TargetSlot slot in pool)
                if (slot.visual) Destroy(slot.visual);
        }
    }
}
