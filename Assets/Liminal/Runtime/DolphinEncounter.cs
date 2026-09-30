using UnityEngine;

namespace Liminal
{
    public sealed class DolphinEncounter : MonoBehaviour
    {
        public const int Capacity = 6;
        const int HitsToRetire = 3;
        const int MaxConcurrentShots = 3;
        const float MaximumSpeed = 80f;
        const float MaximumAcceleration = 90f;
        const float PressureSpeed = 48f;

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
            public GameObject marker;
            public LockTarget target;
            public Vector3 velocity;
            public bool sprinting;
            public bool aimable;
            public float nextFireAge, phase;
            public int hits;
            public bool spawned;
        }

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly MaterialPropertyBlock MarkerProperties = new();
        static readonly Color MarkerTint = new(0.2f, 0.72f, 1f);
        static readonly Color BlueShot = new(0.04f, 0.43f, 1f);
        static readonly Color OrangeShot = new(1f, 0.38f, 0.08f);
        readonly Slot[] slots = new Slot[Capacity];
        Encounter combat;
        ParticleWorld world;
        Flight flight;
        bool initialized, released;
        int shotSequence;

        public void Initialize(Encounter combat, ParticleWorld world, Flight flight)
        {
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
                if (!slot.marker)
                {
                    slot.marker = PointCloud.Place("Dolphin lock marker " + i, world.NodeMesh, world.NodeMaterial, transform);
                    slot.marker.transform.localScale = Vector3.one * 0.72f;
                    SetMarkerTint(slot.marker, MarkerTint);
                }
                int index = i;
                slot.target = combat.RegisterDolphinTarget(slot.marker, (target, song) => OnDolphinHit(index, target, song));
            }
            initialized = true;
            released = false;
            combat.RegisterDolphinSchool(this);
        }

        public void ResetSchool()
        {
            if (combat) combat.ClearPressureShots(this);
            released = false;
            shotSequence = 0;
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
                if (slot.target != null)
                {
                    if (combat) combat.ResetDolphinTarget(slot.target);
                    slot.target.hp = 0;
                    slot.target.reserved = 0;
                    slot.target.position = Vector3.zero;
                }
                if (slot.marker) slot.marker.SetActive(false);
            }
        }

        public bool TrySpawn(int slotIndex, Vector3 origin, Quaternion rotation, float song)
        {
            if (!initialized || released || !combat || combat.Ended || slotIndex < 0 || slotIndex >= Capacity)
                return false;
            Slot slot = slots[slotIndex];
            if (slot.spawned || slot.target == null || !slot.marker) return false;

            float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            rotation = norm > 0.000001f ? rotation.normalized : Quaternion.identity;
            slot.spawned = true;
            slot.hits = 0;
            slot.phase = slotIndex * (Mathf.PI * 2f / Capacity);
            slot.nextFireAge = 2f + slotIndex * 0.27f;
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
            slot.target.hp = HitsToRetire;
            slot.target.reserved = 0;
            slot.target.position = origin;
            slot.marker.transform.SetPositionAndRotation(origin, rotation);
            slot.marker.SetActive(true);
            return true;
        }

        public void Tick(float song, float dt, bool whaleReleased)
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

            dt = Mathf.Clamp(dt, 0f, 0.1f);
            for (int i = 0; i < Capacity; i++)
            {
                Slot slot = slots[i];
                if (!slot.spawned || slot.pose.Retired) continue;
                float age = slot.pose.Age + dt;
                slot.pose.Age = age;
                Swim(i, slot, age, dt);
                FireIfReady(i, slot, age, song);
            }
        }

        public DolphinPose PoseAt(int slot)
        {
            return slot >= 0 && slot < Capacity && slots[slot] != null ? slots[slot].pose : default;
        }

        public void BeginRecall()
        {
            if (!initialized || released) return;
            released = true;
            RetireAttackers();
        }

        void Swim(int index, Slot slot, float age, float dt)
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

            float targetHeight = Mathf.Sin(age * 0.72f + slot.phase * 1.7f) * 7f +
                Mathf.Sin(age * 0.31f + slot.phase) * 3f;
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
            slot.target.position = next;
            slot.marker.transform.SetPositionAndRotation(next, orientation);
            slot.marker.SetActive(slot.target.hp > 0 && !combat.Ended);
        }

        void FireIfReady(int index, Slot slot, float age, float song)
        {
            if (!slot.aimable || age < slot.nextFireAge || (flight.Position - slot.pose.Position).sqrMagnitude > 105f * 105f ||
                combat.LivePressureShots(this) >= MaxConcurrentShots) return;

            Vector3 direction = AimDirection(slot.pose.Position);
            Color color = (shotSequence++ & 1) == 0 ? BlueShot : OrangeShot;
            if (combat.RegisterPressureShot(slot.pose.Position, direction, song, color, this) == null)
            {
                slot.nextFireAge = age + 0.5f;
                return;
            }
            float stagger = 3.15f + 0.55f * (0.5f + 0.5f * Mathf.Sin(age * 0.17f + index * 1.9f));
            slot.nextFireAge = age + stagger;
        }

        Vector3 AimDirection(Vector3 origin)
        {
            Vector3 relative = flight.Position - origin;
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
            Vector3 direction = intercept - origin;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : flight.transform.forward;
        }

        void OnDolphinHit(int index, LockTarget target, float song)
        {
            Slot slot = slots[index];
            if (!slot.spawned || slot.pose.Retired) return;
            slot.hits++;
            if (slot.hits >= HitsToRetire || target.hp <= 0) Retire(slot);
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
            slot.pose.Active = false;
            slot.pose.Retired = true;
            slot.target.hp = 0;
            slot.marker.SetActive(false);
            combat.Locks.Remove(slot.target);
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
                    if (slot != null) combat.UnregisterDolphinTarget(slot.target);
            }
        }
    }
}
