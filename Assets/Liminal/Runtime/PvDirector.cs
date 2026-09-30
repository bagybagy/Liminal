using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class PvDirector : MonoBehaviour
    {
        const int FirstFishBeat = 32;
        const int SerpentBeat = 48;
        const int ArrivalBeat = 64;
        const int WhaleBeat = 96;
        const int DolphinBeat = 128;
        const int EndBeat = 160;
        const float LockHoldSeconds = 1.2f;

        static readonly int[] ShotStarts = { 0, FirstFishBeat, SerpentBeat, ArrivalBeat, WhaleBeat, DolphinBeat };
        static readonly int[] WhaleTargetOrder = { 8, 10, 0, 2, 4, 6, 12, 14, 16, 18, 20, 22, 24, 26,
            28, 30, 32, 34, 36, 38, 40, 42, 44, 46, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23,
            25, 27, 29, 31, 33, 35, 37, 39, 41, 43, 45, 47 };

        [Serializable]
        public sealed class StageMarker
        {
            public string shot;
            public int beatIndex;
            public int frameCount;
            public double authoredMusicSeconds;
            public double observedMusicSeconds;
        }

        readonly List<StageMarker> markers = new();
        readonly List<LockTarget> batchTargets = new();
        Experience experience;
        bool captureArmed, completed, failed;
        bool serpentShotReleased, dolphinVolleyReleased, dolphinVolleyComplete;
        int currentStage = -1, captureFrameCount, whaleTargetCursor, dolphinSlot = -1;
        double recordStartDsp, captureStartMusicTime;
        float jellyLockStarted = -1f, nextJellyShotAt, serpentLockStarted = -1f;
        float whaleLockStarted = -1f, batchLockStarted = -1f, pressureLockStarted = -1f;
        LockTarget jellyTarget, serpentTarget, whaleTarget, batchFocus, pressureTarget;
        string failure;

        public bool CaptureStarted => captureArmed;
        public bool Completed => completed;
        public bool Failed => failed;
        public string Failure => failure;
        public int CaptureFrameCount => captureFrameCount;
        public double RecordStartDsp => recordStartDsp;
        public double CaptureStartMusicTime => captureStartMusicTime;
        public float ArrivalFormation { get; private set; }
        public int DolphinFullLocks { get; private set; }
        public IReadOnlyList<StageMarker> Markers => markers;

        public void Initialize(Experience value)
        {
            if (value == null)
            {
                Fail("PvDirector.Initialize received no Experience.");
                return;
            }
            if (experience != null && experience != value)
            {
                Fail("PvDirector cannot be rebound to another Experience.");
                return;
            }
            experience = value;
        }

        public void BeginCapture(double startDsp)
        {
            if (captureArmed || completed || failed) return;
            if (experience == null || !experience.Ready || experience.Music == null || experience.Flight == null ||
                experience.Combat == null || experience.Marine == null)
            {
                Fail("The cavern Experience is not ready for PV capture.");
                return;
            }
            if (!experience.ProofActive || !experience.CavernMode)
            {
                Fail("PV capture requires ProofActive=true and CavernMode=true.");
                return;
            }
            if (Mathf.Abs(Time.timeScale - 1f) > 0.0001f)
            {
                Fail("PV capture requires the normal 1.0 time scale.");
                return;
            }

            recordStartDsp = startDsp;
            captureStartMusicTime = experience.Music.Time;
            captureArmed = true;
        }

        void Update()
        {
            if (!captureArmed || completed || failed) return;
            try
            {
                if (Mathf.Abs(Time.timeScale - 1f) > 0.0001f)
                {
                    Fail("Time.timeScale changed during PV capture.");
                    return;
                }

                captureFrameCount++;
                double song = experience.Music.Time;
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.05f);
                int stage = StageAt(song);
                if (stage != currentStage) BeginStage(stage, song);

                if (song >= BeatTime(EndBeat) + 0.58)
                {
                    if (DolphinFullLocks != DolphinEncounter.PointsPerDolphin)
                    {
                        Fail("The actual dolphin sequence did not reach eight full body locks.");
                        return;
                    }
                    experience.Combat.AbandonLocks();
                    AddMarker("end", EndBeat, song);
                    completed = true;
                    return;
                }

                switch (currentStage)
                {
                    case 0: UpdateGrotto(song, dt); break;
                    case 1: UpdateFish(song, dt); break;
                    case 2: UpdateSerpent(song, dt); break;
                    case 3: UpdateArrival(dt); break;
                    case 4: UpdateWhale(song, dt); break;
                    case 5: UpdateDolphins(song, dt); break;
                }
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
            }
        }

        int StageAt(double song)
        {
            int result = 0;
            for (int i = 1; i < ShotStarts.Length; i++)
                if (song >= BeatTime(ShotStarts[i])) result = i;
                else break;
            return result;
        }

        void BeginStage(int stage, double song)
        {
            experience.Combat.AbandonLocks();
            currentStage = stage;
            AddMarker(ShotName(stage), ShotStarts[stage], song);
            jellyLockStarted = serpentLockStarted = whaleLockStarted = batchLockStarted = -1f;
            jellyTarget = serpentTarget = whaleTarget = batchFocus = null;
            batchTargets.Clear();
            dolphinSlot = -1;
            dolphinVolleyReleased = dolphinVolleyComplete = false;

            Flight flight = experience.Flight;
            MarineLife marine = experience.Marine;
            if (stage == 0)
            {
                flight.SetPose(CaveLayout.Spawn, CaveLayout.SpawnRotation);
                jellyTarget = marine.JellyTargets.Count > 0 ? marine.JellyTargets[0] : null;
                if (jellyTarget != null) FrameAt(jellyTarget.position, new Vector3(0f, 14f, -58f));
                nextJellyShotAt = (float)BeatTime(0);
            }
            else if (stage == 1)
            {
                for (int i = 0; i < marine.FishTargets.Count && batchTargets.Count < 8; i++)
                    if (marine.FishTargets[i].Available) batchTargets.Add(marine.FishTargets[i]);
                FrameAt(Centroid(batchTargets), new Vector3(0f, 7f, -34f));
            }
            else if (stage == 2)
            {
                Vector3 focus = Anatomy.Focus((float)song);
                Vector3 position = focus + new Vector3(35f, 12f, -64f);
                flight.SetPose(Constrain(position), Quaternion.LookRotation(focus - position));
            }
            else if (stage == 3)
            {
                Vector3 origin = marine.Arrival.Origin;
                Vector3 position = origin + new Vector3(0f, 40f, -206f);
                flight.SetPose(Constrain(position), Quaternion.LookRotation(origin - position));
            }
            else if (stage == 4)
            {
                LockTarget focus = marine.WhaleResonatorTargets.Count > 8 ? marine.WhaleResonatorTargets[8] : null;
                Vector3 point = focus != null ? focus.position : marine.WhalePosition;
                Vector3 offset = marine.WhaleRotation * new Vector3(35f, 12f, -92f);
                Vector3 position = point + offset;
                flight.SetPose(Constrain(position), Quaternion.LookRotation(point - position));
            }
            else if (stage == 5)
            {
                int active = FirstActiveDolphin();
                Vector3 point = active >= 0 ? marine.Dolphins.PoseAt(active).Position : marine.WhalePosition;
                FrameAt(point, new Vector3(0f, 18f, -68f));
            }
        }

        void UpdateGrotto(double song, float dt)
        {
            if (jellyTarget == null) return;
            StepToward(jellyTarget.position, dt, 54f);
            Encounter combat = experience.Combat;
            if (combat.HasPending) return;
            if (combat.Locks.Contains(jellyTarget))
            {
                if (song - jellyLockStarted >= LockHoldSeconds)
                {
                    combat.Release();
                    nextJellyShotAt = (float)song + 1f;
                    jellyLockStarted = -1f;
                }
                return;
            }
            if (jellyTarget.hp <= 0 || !jellyTarget.Available || song < nextJellyShotAt) return;
            if (AtReticle(jellyTarget.position))
            {
                combat.AcquireAt(experience.Flight.AimScreenPosition);
                if (combat.Locks.Contains(jellyTarget)) jellyLockStarted = (float)song;
            }
        }

        void UpdateFish(double song, float dt)
        {
            if (batchTargets.Count < 8) return;
            RunLockBatch(batchTargets, 8, song, dt, 54f);
        }

        void UpdateSerpent(double song, float dt)
        {
            Encounter combat = experience.Combat;
            if (serpentShotReleased)
            {
                StepToward(Anatomy.Focus((float)song), dt, 72f);
                return;
            }
            if (serpentTarget == null || (!serpentTarget.Available && !combat.Locks.Contains(serpentTarget)))
                serpentTarget = NearestSerpentTarget();
            if (serpentTarget == null)
            {
                StepToward(Anatomy.Focus((float)song), dt, 72f);
                return;
            }

            StepToward(serpentTarget.position, dt, 72f);
            if (combat.HasPending) return;
            if (combat.Locks.Contains(serpentTarget))
            {
                if (song - serpentLockStarted >= LockHoldSeconds)
                {
                    combat.Release();
                    serpentShotReleased = true;
                    serpentLockStarted = -1f;
                }
                return;
            }
            if (serpentTarget.Available && AtReticle(serpentTarget.position))
            {
                combat.AcquireAt(experience.Flight.AimScreenPosition);
                if (combat.Locks.Contains(serpentTarget)) serpentLockStarted = (float)song;
            }
        }

        void UpdateArrival(float dt)
        {
            WhaleArrival arrival = experience.Marine.Arrival;
            Vector3 target = arrival.Triggered ? experience.Marine.WhalePosition : arrival.Origin;
            float followDistance = arrival.Triggered ? 158f : 158f;
            StepToward(target, dt, followDistance);
            ArrivalFormation = Mathf.Max(ArrivalFormation, arrival.Formation);
        }

        void UpdateWhale(double song, float dt)
        {
            MarineLife marine = experience.Marine;
            if (!marine.WhaleEntranceComplete)
            {
                StepToward(marine.WhalePosition, dt, 150f);
                return;
            }

            Encounter combat = experience.Combat;
            if (whaleTarget == null || (!whaleTarget.Available && !combat.Locks.Contains(whaleTarget)))
                whaleTarget = NextWhaleTarget();
            if (whaleTarget == null)
            {
                StepToward(marine.WhalePosition, dt, 92f);
                return;
            }

            StepToward(whaleTarget.position, dt, 82f);
            if (combat.HasPending) return;
            if (combat.Locks.Contains(whaleTarget))
            {
                if (song - whaleLockStarted >= LockHoldSeconds)
                {
                    combat.Release();
                    whaleLockStarted = -1f;
                    whaleTargetCursor++;
                }
                return;
            }
            if (whaleTarget.Available && AtReticle(whaleTarget.position))
            {
                combat.AcquireAt(experience.Flight.AimScreenPosition);
                if (combat.Locks.Contains(whaleTarget)) whaleLockStarted = (float)song;
            }
        }

        void UpdateDolphins(double song, float dt)
        {
            if (dolphinVolleyReleased && experience.Combat.HasPending)
            {
                StepToward(batchFocus != null ? batchFocus.position : experience.Marine.WhalePosition, dt, 58f);
                return;
            }
            if (dolphinVolleyReleased)
            {
                DolphinEncounter.DolphinPose pose = experience.Marine.Dolphins.PoseAt(dolphinSlot);
                if (pose.Retired || !pose.Active)
                {
                    dolphinVolleyComplete = true;
                    batchTargets.Clear();
                    batchFocus = null;
                    dolphinVolleyReleased = false;
                    dolphinSlot = -1;
                }
                else
                {
                    bool anyAvailable = false;
                    foreach (LockTarget target in batchTargets) anyAvailable |= target.Available;
                    if (!anyAvailable)
                    {
                        batchTargets.Clear();
                        batchFocus = null;
                        dolphinVolleyReleased = false;
                        dolphinSlot = -1;
                    }
                }
            }

            if (dolphinVolleyComplete && TryInterceptPressureShot(song, dt, 105f)) return;

            if (dolphinSlot < 0)
            {
                dolphinSlot = FirstActiveDolphin();
                if (dolphinSlot >= 0)
                {
                    batchTargets.Clear();
                    foreach (LockTarget target in experience.Marine.Dolphins.TargetsAt(dolphinSlot))
                        batchTargets.Add(target);
                    batchLockStarted = -1f;
                    batchFocus = null;
                    FrameAt(experience.Marine.Dolphins.PoseAt(dolphinSlot).Position, new Vector3(0f, 18f, -68f));
                }
            }

            if (dolphinSlot >= 0 && batchTargets.Count >= DolphinEncounter.PointsPerDolphin && !dolphinVolleyReleased)
            {
                if (RunLockBatch(batchTargets, DolphinEncounter.PointsPerDolphin, song, dt, 48f))
                {
                    dolphinVolleyReleased = true;
                    dolphinVolleyComplete = false;
                }
                return;
            }

            if (TryInterceptPressureShot(song, dt, 105f)) return;
            StepToward(experience.Marine.WhalePosition, dt, 92f);
        }

        bool RunLockBatch(IReadOnlyList<LockTarget> targets, int desiredCount, double song, float dt, float followDistance)
        {
            Encounter combat = experience.Combat;
            if (combat.HasPending)
            {
                if (batchFocus != null) StepToward(batchFocus.position, dt, followDistance);
                return false;
            }

            int locked = CountLocked(targets);
            if (locked >= desiredCount)
            {
                if (currentStage == 5 && DolphinFullLocks < desiredCount)
                {
                    DolphinFullLocks = locked;
                    AddMarker("dolphin-full-lock", DolphinBeat, song);
                }
                if (batchLockStarted < 0f) batchLockStarted = (float)song;
                if (batchFocus != null) StepToward(batchFocus.position, dt, followDistance);
                if (batchLockStarted >= 0f && song - batchLockStarted >= LockHoldSeconds)
                {
                    combat.Release();
                    batchLockStarted = -1f;
                    return true;
                }
                return false;
            }

            LockTarget next = NextBatchTarget(targets);
            if (next == null)
            {
                if (batchFocus != null) StepToward(batchFocus.position, dt, followDistance);
                return false;
            }
            batchFocus = next;
            StepToward(next.position, dt, followDistance);
            if (!AtReticle(next.position)) return false;

            int before = combat.Locks.Count;
            combat.AcquireAt(experience.Flight.View.WorldToScreenPoint(next.position));
            if (combat.Locks.Count > before)
            {
                LockTarget acquired = combat.Locks[combat.Locks.Count - 1];
                bool wanted = false;
                foreach (LockTarget target in targets) wanted |= target == acquired;
                if (!wanted)
                {
                    combat.Locks.Remove(acquired);
                    return false;
                }
            }
            return false;
        }

        bool TryInterceptPressureShot(double song, float dt, float maxDistance)
        {
            Encounter combat = experience.Combat;
            if (combat.HasPending)
            {
                if (pressureTarget != null) StepToward(pressureTarget.position, dt, 30f);
                return pressureTarget != null;
            }
            if (pressureTarget == null || (!pressureTarget.Available && !combat.Locks.Contains(pressureTarget)))
            {
                pressureTarget = NearestPressureShot(maxDistance);
                pressureLockStarted = -1f;
            }
            if (pressureTarget == null || Vector3.Distance(experience.Flight.Position, pressureTarget.position) > maxDistance)
                return false;

            StepToward(pressureTarget.position, dt, 30f);
            if (combat.Locks.Contains(pressureTarget))
            {
                if (song - pressureLockStarted >= 0.24f)
                {
                    combat.Release();
                    pressureTarget = null;
                    pressureLockStarted = -1f;
                }
                return true;
            }
            if (pressureTarget.Available && AtReticle(pressureTarget.position))
            {
                combat.AcquireAt(experience.Flight.AimScreenPosition);
                if (combat.Locks.Contains(pressureTarget)) pressureLockStarted = (float)song;
            }
            return true;
        }

        LockTarget NearestSerpentTarget()
        {
            LockTarget best = null;
            float bestDistance = float.MaxValue;
            foreach (LockTarget target in experience.Combat.Targets)
            {
                if ((target.kind != TargetKind.Organ && target.kind != TargetKind.Ray) || !target.Available) continue;
                float distance = Vector3.Distance(experience.Flight.Position, target.position);
                if (distance < bestDistance) { best = target; bestDistance = distance; }
            }
            return best;
        }

        LockTarget NextWhaleTarget()
        {
            IReadOnlyList<LockTarget> targets = experience.Marine.WhaleResonatorTargets;
            for (int offset = 0; offset < WhaleTargetOrder.Length; offset++)
            {
                int orderIndex = (whaleTargetCursor + offset) % WhaleTargetOrder.Length;
                int targetIndex = WhaleTargetOrder[orderIndex];
                if (targetIndex < targets.Count && targets[targetIndex].Available)
                {
                    whaleTargetCursor = orderIndex;
                    return targets[targetIndex];
                }
            }
            return null;
        }

        int FirstActiveDolphin()
        {
            for (int i = 0; i < DolphinEncounter.Capacity; i++)
            {
                DolphinEncounter.DolphinPose pose = experience.Marine.Dolphins.PoseAt(i);
                if (pose.Active && !pose.Retired) return i;
            }
            return -1;
        }

        LockTarget NearestPressureShot(float maxDistance)
        {
            LockTarget best = null;
            float bestDistance = maxDistance;
            foreach (LockTarget target in experience.Combat.Targets)
            {
                if (!target.isPressureShot || !target.Available || target.reserved > 0) continue;
                float distance = Vector3.Distance(experience.Flight.Position, target.position);
                if (distance < bestDistance) { bestDistance = distance; best = target; }
            }
            return best;
        }

        LockTarget NextBatchTarget(IReadOnlyList<LockTarget> targets)
        {
            foreach (LockTarget target in targets)
                if (target != null && target.Available && !experience.Combat.Locks.Contains(target)) return target;
            return null;
        }

        int CountLocked(IReadOnlyList<LockTarget> targets)
        {
            int count = 0;
            foreach (LockTarget target in targets)
                if (target != null && experience.Combat.Locks.Contains(target)) count++;
            return count;
        }

        void StepToward(Vector3 target, float dt, float followDistance)
        {
            Flight flight = experience.Flight;
            Vector3 direction = target - flight.Position;
            float distance = direction.magnitude;
            if (distance < 0.001f)
            {
                flight.Step((float)experience.Music.Time, dt, Vector3.zero, Vector2.zero, false);
                return;
            }

            direction = (target - flight.View.transform.position).normalized;
            float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float desiredPitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            float currentYaw = flight.transform.eulerAngles.y;
            float currentPitch = -Mathf.DeltaAngle(0f, flight.transform.eulerAngles.x);
            float yawStep = Mathf.Clamp(Mathf.DeltaAngle(currentYaw, desiredYaw), -100f * dt, 100f * dt);
            float pitchStep = Mathf.Clamp(Mathf.DeltaAngle(currentPitch, desiredPitch), -75f * dt, 75f * dt);
            Vector2 look = new(yawStep / 2.1f, pitchStep / 1.7f);

            float forward = distance > followDistance + 8f ? 1f : distance < followDistance - 8f ? -0.55f : 0f;
            Vector3 move = new(0f, 0f, forward);
            ApplyEvasion(ref move);
            flight.Step((float)experience.Music.Time, dt, move, look, false);
        }

        void ApplyEvasion(ref Vector3 move)
        {
            LockTarget threat = NearestPressureShot(72f);
            if (threat == null) return;
            Vector3 away = experience.Flight.Position - threat.position;
            if (Vector3.Dot(threat.direction, away) <= 0f) return;
            Vector3 side = Vector3.Cross(Vector3.up, threat.direction).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            Vector3 local = Quaternion.Inverse(experience.Flight.transform.rotation) * side;
            move.x += (threat.id % 2 == 0 ? 1f : -1f) * local.x * 0.8f;
            move.y += local.y * 0.55f;
            move.z += local.z * 0.55f;
            move = Vector3.ClampMagnitude(move, 1f);
        }

        bool AtReticle(Vector3 point)
        {
            Vector3 screen = experience.Flight.View.WorldToScreenPoint(point);
            return screen.z > 0f && Vector2.Distance(screen, experience.Flight.AimScreenPosition) <=
                experience.Combat.LockRadiusPixels * 0.65f;
        }

        void FrameAt(Vector3 focus, Vector3 offset)
        {
            Vector3 position = Constrain(focus + offset);
            experience.Flight.SetPose(position, Quaternion.LookRotation(focus - position));
        }

        static Vector3 Constrain(Vector3 position)
        {
            Vector3 velocity = Vector3.zero;
            return CaveLayout.Constrain(position, ref velocity);
        }

        static Vector3 Centroid(IReadOnlyList<LockTarget> targets)
        {
            if (targets == null || targets.Count == 0) return CaveLayout.Rooms[0].Center;
            Vector3 center = Vector3.zero;
            foreach (LockTarget target in targets) center += target.position;
            return center / targets.Count;
        }

        void AddMarker(string shot, int beat, double observedSong)
        {
            markers.Add(new StageMarker {
                shot = shot,
                beatIndex = beat,
                frameCount = captureFrameCount,
                authoredMusicSeconds = BeatTime(beat),
                observedMusicSeconds = observedSong
            });
        }

        static string ShotName(int stage)
        {
            switch (stage)
            {
                case 0: return "grotto";
                case 1: return "fish-school";
                case 2: return "serpent";
                case 3: return "whale-arrival";
                case 4: return "whale-chase";
                default: return "dolphin-combat";
            }
        }

        static double BeatTime(int beatIndex)
        {
            AuthoredScore.Timeline timeline = AuthoredScore.Data;
            if (beatIndex < 0 || beatIndex >= timeline.beats.Length)
                throw new InvalidOperationException("Authored score is missing beat " + beatIndex + ".");
            return timeline.beats[beatIndex] / (double)timeline.sampleRate;
        }

        void Fail(string message)
        {
            failed = true;
            failure = message;
            Debug.LogError("LIMINAL_PV_DIRECTOR_ERROR " + message +
                " fired=" + experience.Combat.Fired + " hits=" + experience.Combat.Hits +
                " dolphins=" + experience.Marine.SpawnedDolphins + " maxLocks=" + experience.Combat.MaxLocks);
        }
    }
}
