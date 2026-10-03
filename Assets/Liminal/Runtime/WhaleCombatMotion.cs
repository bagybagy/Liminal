using UnityEngine;

namespace Liminal
{
    public static class WhaleCombatMotion
    {
        public const float Duration = 14f;
        public static float Interval => (AuthoredScore.Data.beats[64] - AuthoredScore.Data.beats[0]) /
            (float)AuthoredScore.Data.sampleRate;
        public enum Action { Cruise, BackBreach, SkySlam }

        public readonly struct Pose
        {
            public readonly Vector3 Position, Velocity;
            public readonly Quaternion Rotation;
            public readonly Vector4 Gesture;
            public readonly Action Kind;
            public readonly int Index;
            public readonly float Age;
            public readonly bool CanImpact;
            public Pose(Vector3 position, Vector3 velocity, Quaternion rotation, Vector4 gesture,
                Action kind, int index, float age, bool canImpact)
            {
                Position = position; Velocity = velocity; Rotation = rotation; Gesture = gesture;
                Kind = kind; Index = index; Age = age; CanImpact = canImpact;
            }
        }

        readonly struct Key
        {
            public readonly float Time;
            public readonly Vector3 Position, Velocity;
            public Key(float time, Vector3 position, Vector3 velocity)
            { Time = time; Position = position; Velocity = velocity; }
        }

        public static Pose Evaluate(float cruise, float song, float firstActionAt)
        {
            MarineLife.EvaluateWhalePose(cruise, out Vector3 ordinary, out Quaternion ordinaryRotation);
            Vector3 ordinaryVelocity = CruiseVelocity(cruise);
            if (firstActionAt < 0 || song < firstActionAt)
                return new Pose(ordinary, ordinaryVelocity, ordinaryRotation, Vector4.zero, Action.Cruise, -1, 0, false);
            float elapsed = song - firstActionAt;
            int index = Mathf.FloorToInt(elapsed / Interval);
            float age = elapsed - index * Interval;
            if (age >= Duration)
                return new Pose(ordinary, ordinaryVelocity, ordinaryRotation, Vector4.zero, Action.Cruise, index, age, false);

            Action kind = (index & 1) == 0 ? Action.BackBreach : Action.SkySlam;
            float startCruise = cruise - age;
            MarineLife.EvaluateWhalePose(startCruise, out Vector3 start, out _);
            Vector3 startVelocity = CruiseVelocity(startCruise);
            Vector3 direction = Vector3.ProjectOnPlane(startVelocity, Vector3.up).normalized;
            Vector3 anchor = Vector3.Lerp(start, CaveLayout.Rooms[2].Center, .52f);
            anchor.y = CaveLayout.HorizonSurfaceY;
            int count = kind == Action.BackBreach ? 7 : 6;
            Key from = GetKey(0, kind, startCruise, start, startVelocity, anchor, direction);
            Key to = from;
            for (int i = 1; i < count; i++)
            {
                to = GetKey(i, kind, startCruise, start, startVelocity, anchor, direction);
                if (age <= to.Time) break;
                from = to;
            }
            Hermite(from, to, age, out Vector3 position, out Vector3 velocity);
            Vector3 heading = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (heading.sqrMagnitude < .001f) heading = direction;
            float pitch = Mathf.Atan2(velocity.y, heading.magnitude) * Mathf.Rad2Deg;
            float roll;
            Vector4 gesture;
            bool canImpact;
            if (kind == Action.BackBreach)
            {
                float landing = Smooth(5.8f, 7.4f, age) * (1 - Smooth(9.8f, 12.3f, age));
                pitch = Mathf.Lerp(pitch, -14f, landing);
                roll = 112f * Smooth(4.2f, 7.1f, age) * (1 - Smooth(9.2f, 13.5f, age));
                float thrust = Smooth(.3f, 2.5f, age) * (1 - Smooth(5.1f, 6.3f, age));
                float arch = .8f * Mathf.Sin(Smooth(2.8f, 6.5f, age) * Mathf.PI) - landing * .35f;
                gesture = new Vector4(thrust, arch, roll / 112f, .7f * Smooth(3.1f, 5.9f, age) * (1 - Smooth(9.2f, 13f, age)));
                canImpact = age > 6.3f && age < 9.8f;
            }
            else
            {
                roll = -16f * Mathf.Sin(Smooth(3f, 10f, age) * Mathf.PI);
                float thrust = Smooth(1.8f, 3.8f, age) * (1 - Smooth(5.1f, 7.1f, age));
                gesture = new Vector4(thrust, -.65f * Smooth(5.1f, 6.4f, age) * (1 - Smooth(8.2f, 11.8f, age)),
                    -.25f * Mathf.Sin(Smooth(3f, 11f, age) * Mathf.PI), thrust * .5f);
                canImpact = age > 5.1f && age < 8.8f;
            }
            Quaternion rotation = Quaternion.LookRotation(heading.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0, roll);
            float join = Smooth(0, .8f, age) * (1 - Smooth(Duration - 1.5f, Duration, age));
            rotation = Quaternion.Slerp(ordinaryRotation, rotation, join);
            gesture *= join;
            return new Pose(position, velocity, rotation, gesture, kind, index, age, canImpact);
        }

        static Key GetKey(int index, Action kind, float cruise, Vector3 start, Vector3 startVelocity,
            Vector3 anchor, Vector3 direction)
        {
            if (index == 0) return new Key(0, start, startVelocity);
            if (kind == Action.BackBreach)
            {
                switch (index)
                {
                    case 1: return new Key(2.5f, anchor + direction * 50 - Vector3.up * 132, direction * 18 - Vector3.up * 5);
                    case 2: return new Key(4.2f, anchor + direction * 90 - Vector3.up * 28, direction * 30 + Vector3.up * 90);
                    case 3: return new Key(6.3f, anchor + direction * 135 + Vector3.up * 94, direction * 30);
                    case 4: return new Key(8.2f, anchor + direction * 170 - Vector3.up * 56, direction * 22 - Vector3.up * 90);
                    case 5: return new Key(9.8f, anchor + direction * 185 - Vector3.up * 140, direction * 7);
                }
            }
            else
            {
                switch (index)
                {
                    case 1: return new Key(3f, anchor + direction * 55 + Vector3.up * 40, direction * 19 + Vector3.up * 8);
                    case 2: return new Key(5.1f, anchor + direction * 105 + Vector3.up * 104, direction * 32);
                    case 3: return new Key(7.1f, anchor + direction * 170 - Vector3.up * 60, direction * 25 - Vector3.up * 90);
                    case 4: return new Key(8.8f, anchor + direction * 185 - Vector3.up * 138, direction * 7);
                }
            }
            MarineLife.EvaluateWhalePose(cruise + Duration, out Vector3 finish, out _);
            return new Key(Duration, finish, CruiseVelocity(cruise + Duration));
        }

        static void Hermite(Key a, Key b, float age, out Vector3 position, out Vector3 velocity)
        {
            float duration = b.Time - a.Time;
            float t = Mathf.Clamp01((age - a.Time) / duration), t2 = t * t, t3 = t2 * t;
            position = (2 * t3 - 3 * t2 + 1) * a.Position + (t3 - 2 * t2 + t) * duration * a.Velocity +
                (-2 * t3 + 3 * t2) * b.Position + (t3 - t2) * duration * b.Velocity;
            velocity = ((6 * t2 - 6 * t) * a.Position + (3 * t2 - 4 * t + 1) * duration * a.Velocity +
                (-6 * t2 + 6 * t) * b.Position + (3 * t2 - 2 * t) * duration * b.Velocity) / duration;
        }

        static Vector3 CruiseVelocity(float cruise)
        {
            MarineLife.EvaluateWhalePose(cruise + .005f, out Vector3 next, out _);
            MarineLife.EvaluateWhalePose(cruise - .005f, out Vector3 before, out _);
            return (next - before) / .01f;
        }

        public static Vector3 ContactPoint(Vector3 position, Quaternion rotation, float song, Vector4 gesture)
        {
            Vector3 contact = position;
            // Central hull rather than a grazing flipper or tail: the heavy body creates the curtain.
            for (int section = 0; section < 5; section++)
            {
                float z = -16f + section * 16f;
                Vector2 radius = WhaleAnatomy.Radius(z);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6;
                    Vector3 local = new(radius.x * Mathf.Cos(angle), radius.y * Mathf.Sin(angle), z);
                    Vector3 world = position + rotation * (WhaleAnatomy.Deform(local, song, gesture) * 1.8f);
                    if (world.y < contact.y) contact = world;
                }
            }
            return contact;
        }

        static float Smooth(float start, float end, float age) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, age));
    }
}
