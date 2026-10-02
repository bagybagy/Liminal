using UnityEngine;

namespace Liminal
{
    public sealed class DolphinEncounter : MonoBehaviour
    {
        public const int Capacity = 6;
        public const int PointsPerDolphin = 8;
        const int MaxConcurrentShots = 16;
        const int ShotsPerBurst = 3;
        const float MaximumSpeed = 80f;
        const float MaximumAcceleration = 90f;
        const float PressureSpeed = 28f;
        const float DolphinScale = 1.5f;
        static readonly Vector3[] LockPointLocalPositions = {
            new(0f, -0.3f, 9.2f),
            new(-1.55f, 0.15f, 5.8f), new(1.55f, 0.15f, 5.8f),
            new(-1.3f, -0.7f, 1.4f), new(1.3f, -0.7f, 1.4f),
            new(-0.82f, -0.1f, -5.8f), new(0.82f, -0.1f, -5.8f),
            new(0f, -0.05f, -9.1f)
        };
        static readonly string[] LockPointNames = {
            "front", "front flank port", "front flank starboard",
            "fin base port", "fin base starboard", "rear flank port",
            "rear flank starboard", "back"
        };

        public struct DolphinPose
        {
            public bool Active, Retired;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Age;
            public Vector3 BirthPosition;
            public Quaternion BirthRotation;
            public float Speed;
            public Vector3 Velocity;
        }

        sealed class Slot
        {
            public DolphinPose pose;
            public readonly GameObject[] markers = new GameObject[PointsPerDolphin];
            public readonly LockTarget[] targets = new LockTarget[PointsPerDolphin];
            public readonly System.Collections.ObjectModel.ReadOnlyCollection<LockTarget> targetView;
            public Vector3 velocity;
            public bool sprinting;
            public bool aimable;
            public float nextFireAge, phase;
            public int hits;
            public bool spawned;

            public Slot()
            {
                targetView = System.Array.AsReadOnly(targets);
            }
        }

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static MaterialPropertyBlock MarkerProperties;
        static readonly Color MarkerTint = new(0.2f, 0.72f, 1f);
        static readonly Color BlueShot = new(0.04f, 0.43f, 1f);
        static readonly Color OrangeShot = new(1f, 0.38f, 0.08f);
        readonly Slot[] slots = new Slot[Capacity];
        readonly int[] candidateSlots = new int[Capacity];
        readonly float[] candidateWeights = new float[Capacity];
        Encounter combat;
        ParticleWorld world;
        Flight flight;
        bool initialized, released, pressureSpawningSuppressed;
        int shotSequence;
        int nextBurstSlot;
        int actionSequence, optionalCount;
        bool inheritedBubbleRings;
        float schoolAge, nextSchoolBurstAge;

        public float AttackRateMultiplier => Mathf.Min(2f, 1f + optionalCount / 3f);
        public int BubbleRingShots { get; private set; }
        public int ShotsSpawned { get; private set; }
        public int NormalBursts { get; private set; }

        public void Initialize(Encounter combat, ParticleWorld world, Flight flight)
        {
            MarkerProperties ??= new MaterialPropertyBlock();
            if (initialized)
            {
                if (this.combat != combat || this.world != world || this.flight != flight)
                    throw new System.InvalidOperationException("DolphinEncounter cannot be rebound after initialization.");
                ResetSchool();
                return;
            }
            this.combat = combat;
            this.world = world;
            this.flight = flight;
            if (!combat || !world || !flight || !world.NodeMesh || !world.NodeMaterial)
                throw new System.InvalidOperationException("DolphinEncounter requires initialized combat, particles, and flight.");

            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] == null) slots[i] = new Slot();
                Slot slot = slots[i];
                for (int point = 0; point < PointsPerDolphin; point++)
                {
                    if (!slot.markers[point])
                    {
                        string markerName = "Dolphin lock marker " + i;
                        if (point > 0) markerName += " " + LockPointNames[point];
                        slot.markers[point] = PointCloud.Place(markerName, world.NodeMesh, world.NodeMaterial, transform);
                        slot.markers[point].transform.localScale = Vector3.one * 0.72f;
                        SetMarkerTint(slot.markers[point], MarkerTint);
                    }
                    if (slot.targets[point] != null) continue;
                    int index = i, pointIndex = point;
                    slot.targets[point] = combat.RegisterDolphinTarget(slot.markers[point],
                        (target, song) => OnDolphinHit(index, pointIndex, target, song));
                }
            }
            initialized = true;
            released = false;
            pressureSpawningSuppressed = false;
            combat.RegisterDolphinSchool(this);
        }

        public void ResetSchool()
        {
            if (combat) combat.ClearPressureShots(this);
            released = false;
            pressureSpawningSuppressed = false;
            shotSequence = 0;
            actionSequence = 0;
            schoolAge = 0f;
            nextSchoolBurstAge = 2f;
            nextBurstSlot = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot == null) continue;
                slot.pose = default;
                slot.velocity = Vector3.zero;
                slot.sprinting = false;
                slot.aimable = false;
                slot.nextFireAge = slot.phase = 0f;
                slot.hits = 0;
                slot.spawned = false;
                for (int point = 0; point < PointsPerDolphin; point++)
                {
                    LockTarget target = slot.targets[point];
                    if (target != null)
                    {
                        if (combat) combat.ResetDolphinTarget(target);
                        target.hp = 0;
                        target.reserved = 0;
                        target.position = Vector3.zero;
                    }
                    if (slot.markers[point]) slot.markers[point].SetActive(false);
                }
            }
        }

        public void ConfigureInheritance(BossId mask)
        {
            mask &= RunProgress.OptionalBosses;
            optionalCount = RunProgress.Count(mask);
            inheritedBubbleRings = (mask & BossId.Hermit) != 0;
        }

        public void ResetInheritance()
        {
            ConfigureInheritance(BossId.None);
            BubbleRingShots = ShotsSpawned = NormalBursts = 0;
        }

        public bool TrySpawn(int slotIndex, Vector3 origin, Quaternion rotation, float song)
        {
            if (!initialized || released || !combat || combat.Ended || combat.Peaceful ||
                slotIndex < 0 || slotIndex >= Capacity)
                return false;
            Slot slot = slots[slotIndex];
            if (slot.spawned) return false;
            for (int point = 0; point < PointsPerDolphin; point++)
                if (slot.targets[point] == null || !slot.markers[point]) return false;

            float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            rotation = norm > 0.000001f ? rotation.normalized : Quaternion.identity;
            slot.spawned = true;
            slot.hits = 0;
            slot.phase = slotIndex * (Mathf.PI * 2f / Capacity);
            slot.nextFireAge = 2f;
            slot.velocity = rotation * Vector3.forward * 18f;
            slot.sprinting = false;
            slot.aimable = false;
            slot.pose = new DolphinPose {
                Active = true,
                Retired = false,
                Position = origin,
                Rotation = rotation,
                Age = 0f,
                BirthPosition = origin,
                BirthRotation = rotation,
                Speed = slot.velocity.magnitude,
                Velocity = slot.velocity
            };
            for (int point = 0; point < PointsPerDolphin; point++)
            {
                LockTarget target = slot.targets[point];
                target.hp = 1;
                target.reserved = 0;
                target.position = origin + rotation * (LockPointLocalPositions[point] * DolphinScale);
                slot.markers[point].transform.SetPositionAndRotation(target.position, rotation);
                slot.markers[point].SetActive(true);
            }
            return true;
        }

        public void Tick(float song, float dt, bool whaleReleased, bool whaleComplete = false, bool recalling = false)
        {
            if (!initialized || !combat || !world || !flight) return;
            if (whaleReleased && !released)
            {
                BeginRecall();
                return;
            }
            if (released)
            {
                return;
            }
            if (whaleComplete || recalling || combat.Ended || combat.Peaceful)
            {
                StopUnreservedPressureShots();
                return;
            }
            if ((combat.ExplorationMode && combat.ActiveRoom != 2) ||
                CaveLayout.NearestRoom(flight.Position) != 2)
            {
                StopUnreservedPressureShots();
                return;
            }
            pressureSpawningSuppressed = false;

            dt = Mathf.Clamp(dt, 0f, 0.1f);
            schoolAge += dt;
            for (int i = 0; i < Capacity; i++)
            {
                Slot slot = slots[i];
                if (!slot.spawned || slot.pose.Retired) continue;
                float age = slot.pose.Age + dt;
                slot.pose.Age = age;
                Swim(i, slot, age, dt, song);
            }
            FireBurstIfReady(song);
        }

        void StopUnreservedPressureShots()
        {
            if (pressureSpawningSuppressed) return;
            pressureSpawningSuppressed = true;
            if (combat && combat.LivePressureShots(this) > 0)
                combat.ClearPressureShots(this);
        }

        public DolphinPose PoseAt(int slot)
        {
            return slot >= 0 && slot < Capacity && slots[slot] != null ? slots[slot].pose : default;
        }

        public static float VerticalTargetHeight(int slot, float age)
        {
            if (slot < 0 || slot >= Capacity) throw new System.ArgumentOutOfRangeException(nameof(slot));
            age = Mathf.Max(0f, age);
            float phase = slot * (Mathf.PI * 2f / Capacity);
            float arcCycle = Mathf.Repeat(age + slot * 4f, 30f);
            float arcRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 18f, arcCycle));
            float arcFall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 25f, arcCycle));
            float breachArc = arcRise * arcFall * 13f;
            return Mathf.Sin(age * 0.19f + phase * 1.7f) * 5.2f +
                Mathf.Sin(age * 0.085f + phase) * 2.5f + breachArc;
        }

        public System.Collections.Generic.IReadOnlyList<LockTarget> TargetsAt(int slot)
        {
            return slot >= 0 && slot < Capacity && slots[slot] != null
                ? slots[slot].targetView
                : System.Array.Empty<LockTarget>();
        }

        public void BeginRecall()
        {
            if (!initialized || released) return;
            released = true;
            RetireAttackers();
        }

        void Swim(int index, Slot slot, float age, float dt, float song)
        {
            Quaternion frame = flight.transform.rotation;
            Vector3 planeUp = Vector3.up;
            Vector3 planeRight = Vector3.right;
            Vector3 planeForward = Vector3.forward;
            Vector3 relativePosition = slot.pose.Position - flight.Position;
            Vector3 planarOffset = Vector3.ProjectOnPlane(relativePosition, planeUp);
            float planarDistance = planarOffset.magnitude;
            float desiredRadius = 48f + index * 3.5f + Mathf.Sin(age * 0.16f + slot.phase) * 4.5f;
            Vector3 radial = planarDistance > 0.1f
                ? planarOffset / planarDistance
                : planeRight * Mathf.Cos(slot.phase) + planeForward * Mathf.Sin(slot.phase);
            Vector3 tangent = Vector3.Cross(planeUp, radial).normalized;
            Vector3 relativeVelocity = slot.velocity - flight.Velocity;
            float radialVelocity = Vector3.Dot(relativeVelocity, radial);
            float radialSpeed = Mathf.Clamp((desiredRadius - planarDistance) * 1.8f - radialVelocity * 0.9f, -28f, 24f);

            float cycle = Mathf.Repeat(age - 2f + index * 1.2f, 14f);
            float sprintIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.1f, 2.65f, cycle));
            float sprintOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.95f, 5.5f, cycle));
            float sprintBlend = age >= 2f ? sprintIn * sprintOut : 0f;
            sprintBlend *= 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(8f, 25f, Mathf.Abs(planarDistance - desiredRadius)));

            float slowSpeed = Mathf.Lerp(12f, 26f, 0.5f + 0.5f * Mathf.Sin(age * 0.27f + slot.phase));
            float curvedSprintLimit = Mathf.Sqrt(MaximumAcceleration * Mathf.Max(planarDistance, desiredRadius * 0.7f) * 0.82f);
            float fastSpeed = Mathf.Min(72f, curvedSprintLimit);
            float tangentSpeed = Mathf.Lerp(slowSpeed, fastSpeed, sprintBlend);

            float targetHeight = VerticalTargetHeight(index, age);
            float height = Vector3.Dot(relativePosition, planeUp);
            float verticalSpeed = Mathf.Clamp((targetHeight - height) * 1.4f - Vector3.Dot(relativeVelocity, planeUp) * 0.8f, -12f, 12f);
            Vector3 baseVelocity = flight.Velocity + radial * radialSpeed + planeUp * verticalSpeed;
            if (baseVelocity.sqrMagnitude >= MaximumSpeed * MaximumSpeed)
            {
                baseVelocity = Vector3.ClampMagnitude(baseVelocity, MaximumSpeed);
                tangentSpeed = 0f;
            }
            else
            {
                float tangentDot = Vector3.Dot(baseVelocity, tangent);
                float remaining = MaximumSpeed * MaximumSpeed - baseVelocity.sqrMagnitude;
                float tangentLimit = -tangentDot + Mathf.Sqrt(tangentDot * tangentDot + remaining);
                tangentSpeed = Mathf.Min(tangentSpeed, Mathf.Max(0f, tangentLimit));
            }
            Vector3 desiredVelocity = baseVelocity + tangent * tangentSpeed;
            slot.sprinting = sprintBlend > 0.02f;

            Vector3 neighborCenter = Vector3.zero;
            Vector3 separation = Vector3.zero;
            int neighbors = 0;
            for (int j = 0; j < Capacity; j++)
            {
                if (j == index || !slots[j].spawned || !slots[j].pose.Active) continue;
                Vector3 offset = slot.pose.Position - slots[j].pose.Position;
                float distanceSquared = offset.sqrMagnitude;
                neighborCenter += slots[j].pose.Position;
                neighbors++;
                if (distanceSquared > 0.01f && distanceSquared < 22f * 22f)
                    separation += offset / distanceSquared;
            }

            Vector3 acceleration = (desiredVelocity - slot.velocity) * 1.8f + separation * 95f;
            // Turning feed-forward preserves the sprint instead of drifting outwards and braking it away.
            float actualTangentSpeed = Vector3.Dot(relativeVelocity, tangent);
            acceleration -= radial * (actualTangentSpeed * actualTangentSpeed / Mathf.Max(16f, planarDistance));
            if (neighbors > 0) acceleration += (neighborCenter / neighbors - slot.pose.Position) * 0.008f;
            acceleration = Vector3.ClampMagnitude(acceleration, MaximumAcceleration);
            slot.velocity = Vector3.ClampMagnitude(slot.velocity + acceleration * dt, MaximumSpeed);
            Vector3 next = slot.pose.Position + slot.velocity * dt;
            if (world.Caverns) next = CaveLayout.Constrain(next, ref slot.velocity, 8f);

            Vector3 heading = slot.velocity.sqrMagnitude > 0.01f ? slot.velocity.normalized : slot.pose.Rotation * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
            float bank = Mathf.Clamp(-Vector3.Dot(acceleration, right) * 0.85f, -34f, 34f);
            Vector3 up = Mathf.Abs(Vector3.Dot(heading, Vector3.up)) > 0.98f ? frame * Vector3.forward : Vector3.up;
            Quaternion orientation = Quaternion.LookRotation(heading, up) *
                Quaternion.Euler(Mathf.Sin(age * 0.62f + slot.phase) * 5f, 0f, bank);
            slot.pose.Position = next;
            slot.pose.Rotation = orientation;
            slot.pose.Speed = slot.velocity.magnitude;
            slot.pose.Velocity = slot.velocity;
            slot.aimable = !slot.sprinting && (slot.velocity - flight.Velocity).magnitude <= 36f;
            for (int point = 0; point < PointsPerDolphin; point++)
            {
                LockTarget target = slot.targets[point];
                Vector3 local = DolphinForm.Deform(LockPointLocalPositions[point], song);
                target.position = next + orientation * (local * DolphinScale);
                slot.markers[point].transform.SetPositionAndRotation(target.position, orientation);
                slot.markers[point].SetActive(target.hp > 0 && !slot.pose.Retired && !combat.Ended);
            }
        }

        void FireBurstIfReady(float song)
        {
            if (combat.Ended || combat.Peaceful ||
                (combat.ExplorationMode && combat.ActiveRoom != 2)) return;
            if (schoolAge < nextSchoolBurstAge) return;

            int candidateCount = 0;
            float totalWeight = 0f;
            for (int index = 0; index < Capacity; index++)
            {
                Slot candidate = slots[index];
                if (!candidate.spawned || candidate.pose.Retired || !candidate.aimable ||
                    candidate.pose.Age < candidate.nextFireAge ||
                    (flight.Position - candidate.pose.Position).sqrMagnitude > 105f * 105f) continue;
                float weight = IsInView(candidate.pose.Position) ? 1.2f : 1f;
                candidateSlots[candidateCount] = index;
                candidateWeights[candidateCount] = weight;
                candidateCount++;
                totalWeight += weight;
            }
            if (candidateCount == 0) return;

            float selection = StableUnit(actionSequence * 37 + shotSequence * 11 + nextBurstSlot) * totalWeight;
            int selected = candidateSlots[candidateCount - 1];
            for (int i = 0; i < candidateCount; i++)
            {
                selection -= candidateWeights[i];
                if (selection < 0f) { selected = candidateSlots[i]; break; }
            }
            Slot slot = slots[selected];
            Vector3 origin = slot.pose.Position + slot.pose.Rotation * new Vector3(0f, 0.25f, 0.6f);
            bool fireRing = inheritedBubbleRings && actionSequence % 3 == 2;
            int desiredShots = fireRing ? 1 : ShotsPerBurst;
            if (combat.LivePressureShots(this) + desiredShots > MaxConcurrentShots ||
                !combat.CanRegisterPressureShots(desiredShots))
            {
                nextSchoolBurstAge = schoolAge + 0.15f;
                return;
            }

            Vector3 directionToPlayer = AimDirection(origin, shotSequence);
            Vector3 spreadAxis = Vector3.ProjectOnPlane(Vector3.up, directionToPlayer);
            if (spreadAxis.sqrMagnitude < 0.001f)
                spreadAxis = Vector3.ProjectOnPlane(Vector3.right, directionToPlayer);
            spreadAxis.Normalize();
            int fired = 0;
            if (fireRing)
            {
                Vector3 direction = Quaternion.AngleAxis(-3f, spreadAxis) * directionToPlayer;
                if (combat.RegisterPressureShot(origin, direction, song, new Color(0.18f, 0.76f, 1f),
                    this, PressureSpeed * 0.78f, 9.5f, true) != null)
                {
                    shotSequence++;
                    ShotsSpawned++;
                    BubbleRingShots++;
                    fired++;
                }
            }
            else
            {
                for (int burstIndex = 0; burstIndex < ShotsPerBurst; burstIndex++)
                {
                    int sequence = shotSequence;
                    Vector3 direction = AimDirection(origin, sequence);
                    float spread = (burstIndex - 1) * 12f + Mathf.Sin((sequence + 1) * 1.31f) * 2f;
                    direction = Quaternion.AngleAxis(spread, spreadAxis) * direction;
                    Color color = (sequence & 1) == 0 ? BlueShot : OrangeShot;
                    if (combat.RegisterPressureShot(origin, direction, song, color, this, PressureSpeed) == null)
                        break;
                    shotSequence++;
                    ShotsSpawned++;
                    fired++;
                }
            }

            if (fired == 0)
            {
                nextSchoolBurstAge = schoolAge + 0.15f;
                return;
            }
            nextBurstSlot = (selected + 1) % Capacity;
            float actorCooldown = 2.2f + 1.2f * (0.5f + 0.5f * Mathf.Sin((shotSequence + selected) * 1.71f));
            slot.nextFireAge = slot.pose.Age + actorCooldown / AttackRateMultiplier * (fireRing ? 1.18f : 1f);
            float stagger = 1f + 0.5f * (0.5f + 0.5f * Mathf.Sin((shotSequence + selected) * 1.37f));
            nextSchoolBurstAge = schoolAge + stagger / AttackRateMultiplier;
            actionSequence++;
            if (!fireRing) NormalBursts++;
        }

        bool IsInView(Vector3 position)
        {
            Camera camera = flight.View;
            if (!camera) return false;
            Vector3 viewport = camera.WorldToViewportPoint(position);
            return viewport.z >= camera.nearClipPlane && viewport.z <= camera.farClipPlane &&
                viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        static float StableUnit(int value)
        {
            unchecked
            {
                uint x = (uint)value + 0x9e3779b9u;
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0x00ffffffu) / 16777216f;
            }
        }

        Vector3 AimDirection(Vector3 origin, int sequence)
        {
            Vector3 relative = flight.Position - origin;
            Vector3 direct = relative.sqrMagnitude > 0.001f ? relative.normalized : flight.transform.forward;
            Vector3 velocity = flight.Velocity;
            float a = Vector3.Dot(velocity, velocity) - PressureSpeed * PressureSpeed;
            float b = 2f * Vector3.Dot(relative, velocity);
            float c = relative.sqrMagnitude;
            float time = c > 0.001f ? Mathf.Sqrt(c) / PressureSpeed : 0f;
            float discriminant = b * b - 4f * a * c;
            if (Mathf.Abs(a) < 0.001f)
            {
                if (Mathf.Abs(b) > 0.001f && -c / b > 0f) time = -c / b;
            }
            else if (discriminant >= 0f)
            {
                float root = Mathf.Sqrt(discriminant);
                float first = (-b - root) / (2f * a);
                float second = (-b + root) / (2f * a);
                if (first > 0f) time = first;
                if (second > 0f && (time <= 0f || second < time)) time = second;
            }
            Vector3 intercept = flight.Position + velocity * Mathf.Clamp(time, 0f, 4f);
            Vector3 predicted = intercept - origin;
            if (predicted.sqrMagnitude < 0.001f) return direct;
            float lead = 0.20f + 0.10f * (0.5f + 0.5f * Mathf.Sin((sequence + 1) * 2.399963f));
            return Vector3.Slerp(direct, predicted.normalized, lead).normalized;
        }

        void OnDolphinHit(int index, int pointIndex, LockTarget target, float song)
        {
            Slot slot = slots[index];
            if (!slot.spawned || slot.pose.Retired) return;
            slot.hits++;
            if (target.hp <= 0 && slot.markers[pointIndex]) slot.markers[pointIndex].SetActive(false);
            if (slot.hits >= PointsPerDolphin) Retire(slot);
        }

        void RetireAttackers()
        {
            for (int i = 0; i < Capacity; i++)
            {
                Slot slot = slots[i];
                if (slot.spawned && !slot.pose.Retired) Retire(slot);
            }
            combat.ClearPressureShots(this);
        }

        void Retire(Slot slot)
        {
            if (slot.pose.Retired) return;
            slot.pose.Active = false;
            slot.pose.Retired = true;
            for (int point = 0; point < PointsPerDolphin; point++)
            {
                LockTarget target = slot.targets[point];
                target.hp = Mathf.Max(0, target.reserved);
                if (slot.markers[point]) slot.markers[point].SetActive(target.reserved > 0);
                combat.Locks.Remove(target);
            }
        }

        static void SetMarkerTint(GameObject marker, Color color)
        {
            Renderer renderer = marker.GetComponent<Renderer>();
            if (!renderer) return;
            renderer.GetPropertyBlock(MarkerProperties);
            MarkerProperties.SetColor(TintId, color);
            renderer.SetPropertyBlock(MarkerProperties);
        }

        void OnDestroy()
        {
            if (combat)
            {
                combat.ClearPressureShots(this);
                combat.UnregisterDolphinSchool(this);
                foreach (Slot slot in slots)
                    if (slot != null)
                        foreach (LockTarget target in slot.targets)
                            combat.UnregisterDolphinTarget(target);
            }
        }
    }
}
