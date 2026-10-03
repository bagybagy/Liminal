using UnityEngine;

namespace Liminal
{
    public static class WhaleCombatMotion
    {
        public const float MotionStretch = 1f;
        public const float Duration = 22f;
        public const float ApexHeight = 85.5f;
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

        public readonly struct Sequence
        {
            public readonly float Prep, Launch, Apex, Submerged, LaunchSpeed, PrepHeight;
            public Sequence(float startHeight)
            {
                Prep = 2.5f;
                PrepHeight = Mathf.Min(startHeight - 12f, CaveLayout.HorizonSurfaceY - 28f);
                float rise = CaveLayout.HorizonSurfaceY - PrepHeight;
                LaunchSpeed = Mathf.Clamp(40f + rise * .105f, 46f, 64f);
                Launch = Prep + rise * 2f / LaunchSpeed;
                Apex = Launch + 2f * ApexHeight / LaunchSpeed;
                Submerged = Apex + 4.8f;
            }
        }

        public static Sequence Describe(float startCruise)
        {
            MarineLife.EvaluateWhalePose(startCruise, out Vector3 start, out _);
            return new Sequence(start.y);
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
            Sequence sequence = new Sequence(start.y);
            // Ease the cruise orbit inward during the leap, without relocating to a separate anchor.
            Vector3 position = ordinary, velocity = ordinaryVelocity;
            float inward = Smooth(sequence.Prep, sequence.Apex, age);
            float outward = Smooth(sequence.Submerged, Duration, age);
            float envelope = inward * (1 - outward);
            float envelopeSpeed = SmoothSpeed(sequence.Prep, sequence.Apex, age) * (1 - outward) -
                inward * SmoothSpeed(sequence.Submerged, Duration, age);
            Vector3 radial = Vector3.ProjectOnPlane(ordinary - CaveLayout.Rooms[2].Center, Vector3.up);
            position -= radial * (.35f * envelope);
            velocity -= Vector3.ProjectOnPlane(ordinaryVelocity, Vector3.up) * (.35f * envelope) +
                radial * (.35f * envelopeSpeed);
            Height(startCruise, age, sequence, out position.y, out velocity.y);
            Vector3 heading = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (heading.sqrMagnitude < .001f) heading = Vector3.ProjectOnPlane(startVelocity, Vector3.up);
            float pitch = Mathf.Atan2(velocity.y, heading.magnitude) * Mathf.Rad2Deg;
            float roll;
            Vector4 gesture;
            bool canImpact;
            if (kind == Action.BackBreach)
            {
                float landing = Smooth(sequence.Launch, sequence.Apex, age) *
                    (1 - Smooth(sequence.Submerged, Duration - .8f, age));
                pitch = Mathf.Lerp(pitch, -14f, landing);
                float frontUnwind = Smooth(sequence.Apex + 2.5f, Duration - 2f, age);
                float tailUnwind = Smooth(sequence.Apex + 3.4f, Duration - .7f, age);
                roll = 108f * Smooth(sequence.Launch - .3f, sequence.Apex + 1.3f, age) * (1 - frontUnwind);
                float thrust = .28f * Smooth(sequence.Prep, sequence.Launch - .7f, age) *
                    (1 - Smooth(sequence.Launch - .7f, sequence.Launch + .3f, age));
                // Post-impact follow-through stays on the longitudinal roll axis, not a new sagittal bend.
                gesture = new Vector4(thrust, 0,
                    roll / 108f + (frontUnwind - tailUnwind) * .7f,
                    .35f * Smooth(sequence.Launch - .8f, sequence.Apex, age) *
                    (1 - Smooth(sequence.Apex + 2f, sequence.Submerged + 1f, age)));
            }
            else
            {
                roll = -12f * Mathf.Sin(Smooth(sequence.Launch, Duration - 1f, age) * Mathf.PI);
                float recover = Smooth(sequence.Submerged, Duration - .8f, age);
                pitch = Mathf.Lerp(pitch, 0, recover);
                gesture = new Vector4(0, 0, roll / 108f,
                    .18f * Smooth(sequence.Launch, sequence.Apex, age) *
                    (1 - Smooth(sequence.Apex + 2f, sequence.Submerged + 1f, age)));
            }
            canImpact = age > sequence.Apex && age < sequence.Submerged;
            Quaternion rotation = Quaternion.LookRotation(heading.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0, roll);
            float join = Smooth(0, .8f, age) * (1 - Smooth(Duration - 1.5f, Duration, age));
            rotation = Quaternion.Slerp(ordinaryRotation, rotation, join);
            gesture *= join;
            return new Pose(position, velocity, rotation, gesture, kind, index, age, canImpact);
        }

        static void Height(float cruise, float age, Sequence s, out float height, out float speed)
        {
            float surface = CaveLayout.HorizonSurfaceY;
            if (age >= s.Prep && age <= s.Launch)
            {
                float duration = s.Launch - s.Prep;
                float t = Mathf.Clamp01((age - s.Prep) / duration);
                float t2 = t * t, t4 = t2 * t2;
                // Integral of smootherstep: ascent velocity rises monotonically until the surface.
                height = s.PrepHeight + s.LaunchSpeed * duration * t4 * (2.5f + t * (-3f + t));
                speed = s.LaunchSpeed * t * t2 * (10f + t * (-15f + 6f * t));
                return;
            }
            MarineLife.EvaluateWhalePose(cruise, out Vector3 start, out _, out Vector3 startVelocity, out Vector3 startAcceleration);
            MarineLife.EvaluateWhalePose(cruise + Duration, out Vector3 finish, out _, out Vector3 finishVelocity, out Vector3 finishAcceleration);
            Key a, b;
            float aa, ab;
            float gravity = -s.LaunchSpeed / (s.Apex - s.Launch);
            if (age < s.Prep)
            {
                a = HeightKey(0, start.y, startVelocity.y);
                b = HeightKey(s.Prep, s.PrepHeight, 0);
                aa = startAcceleration.y; ab = 0;
            }
            else if (age < s.Apex)
            {
                a = HeightKey(s.Launch, surface, s.LaunchSpeed);
                b = HeightKey(s.Apex, surface + ApexHeight, 0);
                aa = 0; ab = gravity;
            }
            else if (age < s.Submerged)
            {
                a = HeightKey(s.Apex, surface + ApexHeight, 0);
                b = HeightKey(s.Submerged, surface - 55f, -28f);
                aa = gravity; ab = 7f;
            }
            else
            {
                a = HeightKey(s.Submerged, surface - 55f, -28f);
                b = HeightKey(Duration, finish.y, finishVelocity.y);
                aa = 7f; ab = finishAcceleration.y;
            }
            Quintic(a, b, Vector3.up * aa, Vector3.up * ab, age, out Vector3 p, out Vector3 v);
            height = p.y; speed = v.y;
        }

        static Key HeightKey(float time, float height, float speed) =>
            new Key(time, Vector3.up * height, Vector3.up * speed);

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

        static float SmoothSpeed(float start, float end, float age)
        {
            float t = Mathf.InverseLerp(start, end, age);
            return 30f * t * t * (1 - t) * (1 - t) / (end - start);
        }
    }
}
