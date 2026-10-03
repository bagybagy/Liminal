using UnityEngine;

namespace Liminal
{
    public static class WhaleCombatMotion
    {
        const float AuthoredDuration = 20f;
        public const float MotionStretch = 1.2f;
        public const float Duration = AuthoredDuration * MotionStretch;
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
            float actualAge = age;
            age /= MotionStretch;
            MarineLife.EvaluateWhalePose(startCruise, out Vector3 start, out _);
            Vector3 startVelocity = CruiseVelocity(startCruise);
            Vector3 direction = Vector3.ProjectOnPlane(startVelocity, Vector3.up).normalized;
            Vector3 anchor = Vector3.Lerp(start, CaveLayout.Rooms[2].Center, .52f);
            anchor.y = CaveLayout.HorizonSurfaceY;
            int count = kind == Action.BackBreach ? 7 : 6;
            Key from = GetKey(0, kind, startCruise, start, startVelocity, anchor, direction);
            Key to = from;
            int segment = 0;
            for (int i = 1; i < count; i++)
            {
                to = GetKey(i, kind, startCruise, start, startVelocity, anchor, direction);
                if (age <= to.Time) { segment = i - 1; break; }
                from = to;
            }
            Vector3 accelerationFrom = KeyAcceleration(segment, count, kind, startCruise,
                start, startVelocity, anchor, direction);
            Vector3 accelerationTo = KeyAcceleration(segment + 1, count, kind, startCruise,
                start, startVelocity, anchor, direction);
            Quintic(from, to, accelerationFrom, accelerationTo, age, out Vector3 position, out Vector3 velocity);
            velocity /= MotionStretch;
            Vector3 heading = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (heading.sqrMagnitude < .001f) heading = direction;
            float pitch = Mathf.Atan2(velocity.y, heading.magnitude) * Mathf.Rad2Deg;
            float roll;
            Vector4 gesture;
            bool canImpact;
            if (kind == Action.BackBreach)
            {
                float landing = Smooth(5.8f, 7.4f, age) * (1 - Smooth(8.4f, 13.2f, age));
                pitch = Mathf.Lerp(pitch, -14f, landing);
                float frontUnwind = Smooth(8.6f, 15.8f, age);
                float tailUnwind = Smooth(10f, 18.2f, age);
                roll = 112f * Smooth(4.2f, 7.1f, age) * (1 - frontUnwind);
                float thrust = Smooth(.3f, 2.5f, age) * (1 - Smooth(5.1f, 6.3f, age));
                float recoveryStroke = Smooth(10.4f, 13f, age) * (1 - Smooth(17f, 19.5f, age));
                float followThrough = Smooth(7.3f, 9.6f, age) * (1 - Smooth(12f, 16.5f, age));
                float arch = .8f * Mathf.Sin(Smooth(2.8f, 6.5f, age) * Mathf.PI) - landing * .35f + followThrough * .35f;
                // Fins regain control first; rear torsion persists while the body unwinds.
                gesture = new Vector4(thrust + recoveryStroke * .42f, arch,
                    roll / 112f + (frontUnwind - tailUnwind) * 1.5f,
                    .7f * Smooth(3.1f, 5.9f, age) * (1 - Smooth(8.1f, 12.8f, age)));
                canImpact = age > 6.3f && age < 12.5f;
            }
            else
            {
                roll = -16f * Mathf.Sin(Smooth(3f, 15f, age) * Mathf.PI);
                float thrust = Smooth(1.8f, 3.8f, age) * (1 - Smooth(5.1f, 7.1f, age));
                float recoveryStroke = Smooth(9.3f, 12.3f, age) * (1 - Smooth(17f, 19.5f, age));
                gesture = new Vector4(thrust + recoveryStroke * .35f,
                    -.65f * Smooth(5.1f, 6.4f, age) * (1 - Smooth(9.3f, 16f, age)),
                    -.25f * Mathf.Sin(Smooth(3f, 17.5f, age) * Mathf.PI), thrust * .5f);
                canImpact = age > 5.1f && age < 11.5f;
            }
            Quaternion rotation = Quaternion.LookRotation(heading.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0, roll);
            float join = Smooth(0, .8f, age) * (1 - Smooth(AuthoredDuration - 1.5f, AuthoredDuration, age));
            rotation = Quaternion.Slerp(ordinaryRotation, rotation, join);
            gesture *= join;
            return new Pose(position, velocity, rotation, gesture, kind, index, actualAge, canImpact);
        }

        static Key GetKey(int index, Action kind, float cruise, Vector3 start, Vector3 startVelocity,
            Vector3 anchor, Vector3 direction)
        {
            if (index == 0) return new Key(0, start, startVelocity * MotionStretch);
            if (kind == Action.BackBreach)
            {
                switch (index)
                {
                    case 1: return new Key(2.5f, anchor + direction * 50 - Vector3.up * 132, direction * 18 - Vector3.up * 5);
                    case 2: return new Key(4.2f, anchor + direction * 90 - Vector3.up * 28, direction * 30 + Vector3.up * 90);
                    case 3: return new Key(6.3f, anchor + direction * 135 + Vector3.up * 94, direction * 30);
                    case 4: return new Key(8.2f, anchor + direction * 170 - Vector3.up * 44, direction * 30 - Vector3.up * 54);
                    case 5: return new Key(12.5f, anchor + direction * 225 - Vector3.up * 112 +
                        Vector3.Cross(Vector3.up, direction) * 22, direction * 15 - Vector3.up * 7 +
                        Vector3.Cross(Vector3.up, direction) * 5);
                }
            }
            else
            {
                switch (index)
                {
                    case 1: return new Key(3f, anchor + direction * 55 + Vector3.up * 40, direction * 19 + Vector3.up * 8);
                    case 2: return new Key(5.1f, anchor + direction * 105 + Vector3.up * 104, direction * 32);
                    case 3: return new Key(7.1f, anchor + direction * 170 - Vector3.up * 60, direction * 25 - Vector3.up * 90);
                    case 4: return new Key(11.5f, anchor + direction * 220 - Vector3.up * 120 +
                        Vector3.Cross(Vector3.up, direction) * 18, direction * 15 - Vector3.up * 2 +
                        Vector3.Cross(Vector3.up, direction) * 4);
                }
            }
            MarineLife.EvaluateWhalePose(cruise + Duration, out Vector3 finish, out _);
            return new Key(AuthoredDuration, finish, CruiseVelocity(cruise + Duration) * MotionStretch);
        }

        static Vector3 KeyAcceleration(int index, int count, Action kind, float cruise, Vector3 start,
            Vector3 startVelocity, Vector3 anchor, Vector3 direction)
        {
            if (index == 0) return CruiseAcceleration(cruise) * (MotionStretch * MotionStretch);
            if (index == count - 1) return CruiseAcceleration(cruise + Duration) * (MotionStretch * MotionStretch);
            Key before = GetKey(index - 1, kind, cruise, start, startVelocity, anchor, direction);
            Key after = GetKey(index + 1, kind, cruise, start, startVelocity, anchor, direction);
            return (after.Velocity - before.Velocity) / (after.Time - before.Time);
        }

        // Matching acceleration at each knot avoids a new force impulse at every segment boundary.
        static void Quintic(Key a, Key b, Vector3 accelerationA, Vector3 accelerationB, float age,
            out Vector3 position, out Vector3 velocity)
        {
            float duration = b.Time - a.Time;
            float t = Mathf.Clamp01((age - a.Time) / duration);
            Vector3 c0 = a.Position, c1 = a.Velocity * duration, c2 = accelerationA * (.5f * duration * duration);
            Vector3 d = b.Position - c0 - c1 - c2;
            Vector3 v = b.Velocity * duration - c1 - c2 * 2;
            Vector3 acc = accelerationB * (duration * duration) - c2 * 2;
            Vector3 c3 = d * 10 - v * 4 + acc * .5f;
            Vector3 c4 = d * -15 + v * 7 - acc;
            Vector3 c5 = d * 6 - v * 3 + acc * .5f;
            position = c0 + t * (c1 + t * (c2 + t * (c3 + t * (c4 + t * c5))));
            velocity = (c1 + t * (c2 * 2 + t * (c3 * 3 + t * (c4 * 4 + t * c5 * 5)))) / duration;
        }

        static Vector3 CruiseAcceleration(float cruise)
        {
            MarineLife.EvaluateWhalePose(cruise, out _, out _, out _, out Vector3 acceleration);
            return acceleration;
        }

        static Vector3 CruiseVelocity(float cruise)
        {
            MarineLife.EvaluateWhalePose(cruise, out _, out _, out Vector3 velocity, out _);
            return velocity;
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

        static float Smooth(float start, float end, float age)
        {
            float t = Mathf.InverseLerp(start, end, age);
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
    }
}
