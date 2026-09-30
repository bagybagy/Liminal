using System;
using UnityEngine;

namespace Liminal
{
    public static class WhaleAnatomy
    {
        public const int ParticleCount = 106124;
        public const int TargetCount = 48;
        const float Tau = 6.28318530718f;
        static readonly Color Skin = new Color(0.025f, 0.075f, 0.12f);
        static readonly Color Sea = new Color(0.045f, 0.23f, 0.29f);
        static readonly Color Silver = new Color(0.38f, 0.55f, 0.57f);
        static readonly Color Light = new Color(0.10f, 0.69f, 0.73f);

        // Rest-space geometry only. Group transforms and transfer destinations belong to the caller.
        public static void Append(Action<Vector3, float, Color, float, float> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            int identity = 0;
            SampleSurface(emit, ref identity, 72000, (u, v) => Body(-78f + 154f * u, v * Tau), 0);
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side;
                SampleSurface(emit, ref identity, 8000, (u, v) => Flipper(u, v * Tau, s), 1);
                SampleSurface(emit, ref identity, 4000, (u, v) => Fluke(u, v * Tau, s), 2);
            }

            // Eight longitudinal paths, not a circumferential wire grid.
            for (int line = 0; line < 8; line++)
            {
                int route = line;
                Curve(emit, ref identity, 420, t => {
                    float z = -70f + 140f * t;
                    float angle = (route + 0.5f) * Tau / 8f + 0.055f * Mathf.Sin(z * 0.037f + route);
                    return Body(z, angle);
                }, Light * 0.65f, 0.072f, 2f);
            }
            for (int pleat = 0; pleat < 13; pleat++)
            {
                float spread = (pleat - 6) / 6f;
                Curve(emit, ref identity, 260, t => {
                    float z = -2f + 74f * t;
                    float angle = -Tau * 0.25f + spread * (0.13f + 0.64f * Mathf.Sin(t * Mathf.PI));
                    return Body(z, angle);
                }, Silver * 0.82f, 0.070f, 3f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side;
                Curve(emit, ref identity, 420, t => {
                    float z = 36f + 39.7f * t;
                    float angle = s > 0 ? -0.12f - 0.12f * t : Mathf.PI + 0.12f + 0.12f * t;
                    return Body(z, angle);
                }, Silver, 0.085f, 2f);
                for (int edge = 0; edge < 2; edge++)
                {
                    float angle = edge * Mathf.PI;
                    Curve(emit, ref identity, 300, t => Flipper(t, angle, s), Light * 0.8f, 0.075f, 2f);
                    Curve(emit, ref identity, 240, t => Fluke(t, angle, s), Light * 0.8f, 0.075f, 2f);
                }
                for (int i = 0; i < 192; i++)
                {
                    float a = i * 2.39996323f;
                    float r = Mathf.Sqrt((i + 0.5f) / 192f);
                    float z = 43f + 0.65f * r * Mathf.Cos(a);
                    float angle = (s > 0 ? -0.13f : Mathf.PI + 0.13f) + 0.041f * r * Mathf.Sin(a);
                    Vector3 p = Body(z, angle);
                    Color c = r < 0.55f ? new Color(0.055f, 0.18f, 0.20f) : new Color(0.63f, 0.90f, 0.86f);
                    Emit(emit, ref identity, p + Normal(p) * 0.12f, 0.105f, c, 4f);
                }
            }
        }

        // Monotone Hermite trunk with an elliptical, broad, rounded head cap.
        public static Vector2 Radius(float z)
        {
            if (z <= -78f || z >= 76f) return Vector2.zero;
            if (z < -62f) return new Vector2(
                Profile(z, -78f, -62f, 0f, 4.3f, 0.30f, 0.10f),
                Profile(z, -78f, -62f, 0f, 4.2f, 0.32f, 0.10f));
            if (z < -30f) return new Vector2(
                Profile(z, -62f, -30f, 4.3f, 11.5f, 0.10f, 0.30f),
                Profile(z, -62f, -30f, 4.2f, 10.5f, 0.10f, 0.20f));
            if (z < 8f) return new Vector2(
                Profile(z, -30f, 8f, 11.5f, 19f, 0.30f, 0.10f),
                Profile(z, -30f, 8f, 10.5f, 15.8f, 0.20f, 0.06f));
            if (z < 26f) return new Vector2(
                Profile(z, 8f, 26f, 19f, 20f, 0.10f, 0f),
                Profile(z, 8f, 26f, 15.8f, 16f, 0.06f, 0f));
            if (z < 42f) return new Vector2(20f, Profile(z, 26f, 42f, 16f, 14f, 0f, 0f));
            float t = (z - 42f) / 34f;
            float cap = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            return new Vector2(20f * cap, 14f * cap);
        }

        static float Profile(float z, float a, float b, float r0, float r1, float d0, float d1)
        {
            float t = Mathf.Clamp01((z - a) / (b - a));
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * r0 + (t3 - 2f * t2 + t) * (b - a) * d0
                + (-2f * t3 + 3f * t2) * r1 + (t3 - t2) * (b - a) * d1;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float CenterY(float z) => 0.65f * Smooth(-70f, -30f, z) - 1.5f * Smooth(30f, 70f, z);

        static Vector3 Body(float z, float angle)
        {
            Vector2 r = Radius(z);
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            float hump = 4.6f * Mathf.Exp(-(z + 29f) * (z + 29f) / 95f) * Mathf.Pow(Mathf.Max(0f, s), 12f);
            // A fuller lower jaw and a lower, flatter crown break rotational symmetry.
            float y = CenterY(z) + r.y * s * (1f - 0.10f * s) + hump;
            return new Vector3(r.x * c, y, z);
        }

        static Vector3 Flipper(float u, float angle, int side)
        {
            u = Mathf.Clamp01(u);
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            float end = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * u * u));
            float arc = Mathf.Sin(u * Mathf.PI);
            float chord = (10.5f - 4f * u) * end;
            float thickness = (2.3f - 1.4f * u) * end;
            float scallop = 0.60f * arc * arc * Mathf.Cos(u * Mathf.PI * 9f) * Mathf.Pow(Mathf.Max(0f, c), 8f);
            return new Vector3(side * (15f + 38f * u),
                -6f - 7f * u + 2.5f * arc + thickness * s + 0.65f * arc * c * c,
                18f - 36f * u + 6f * arc + chord * c + scallop);
        }

        static Vector3 Fluke(float u, float angle, int side)
        {
            u = Mathf.Clamp01(u);
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            float end = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * u * u));
            float arc = Mathf.Sin(u * Mathf.PI);
            float chord = (3.5f + 7f * arc) * end;
            float thickness = (1.5f - 1.1f * u) * end;
            float scallop = 0.25f * arc * arc * Mathf.Cos(u * Mathf.PI * 11f) * Mathf.Pow(Mathf.Max(0f, -c), 8f);
            return new Vector3(side * 30f * u, 0.35f + 1.8f * arc + thickness * s,
                -71f - 2f * u + chord * c + scallop);
        }

        // Mirror this operation order in WhaleAnatomy.hlsl; all seeds and targets use this same field.
        public static Vector3 Deform(Vector3 p, float song)
        {
            Vector3 form = p;
            float phase = song * 0.42f;
            float tail = Smooth(-8f, 78f, -form.z);
            float wave = Mathf.Sin(phase - tail * 0.65f);
            float pitch = 0.20f * tail * wave;
            float cp = Mathf.Cos(pitch), sp = Mathf.Sin(pitch);
            p.y = form.y * cp + 8f * tail * tail * wave;
            p.z = form.z - form.y * sp;
            p.x += 0.65f * tail * tail * Mathf.Sin(phase * 0.5f - tail * 0.4f);
            float span = Smooth(17f, 53f, Mathf.Abs(form.x));
            float fin = span * Smooth(-54f, -43f, form.z) * (1f - Smooth(30f, 40f, form.z));
            float bank = Mathf.Sin(song * 0.16f);
            float side = form.x < 0f ? -1f : 1f;
            p.y += fin * (3.8f * Mathf.Sin(phase - 1.1f) - side * 3.2f * bank);
            p.z += fin * 1.1f * Mathf.Cos(phase - 1.1f);
            float roll = 0.035f * bank;
            float cr = Mathf.Cos(roll), sr = Mathf.Sin(roll);
            return new Vector3(p.x * cr - p.y * sr, p.x * sr + p.y * cr, p.z);
        }

        public static Vector3 TargetLocal(int index, float song)
        {
            if (index < 0 || index >= TargetCount) throw new ArgumentOutOfRangeException(nameof(index));
            int ring = index / 4, quadrant = index % 4;
            int side = quadrant == 0 ? 1 : -1;
            bool lateral = quadrant == 0 || quadrant == 2;
            Vector3 p;
            if (ring == 0 && lateral) p = Fluke(0.62f, Tau * 0.25f, side);
            else if ((ring == 5 || ring == 6) && lateral)
                p = Flipper(ring == 5 ? 0.65f : 0.32f, Tau * 0.25f, side);
            else p = Body(-68f + 136f * ring / 11f, quadrant * Tau * 0.25f);
            return Deform(p + Normal(p) * 0.12f, song);
        }

        static int SurfaceFrame(Vector3 p, out float u, out float angle, out int side)
        {
            side = p.x < 0f ? -1 : 1;
            float x = Mathf.Abs(p.x);
            Vector2 r = Radius(p.z);
            if (p.z < -59f && x > r.x + 0.2f)
            {
                u = Mathf.Clamp01(x / 30f);
                float arc = Mathf.Sin(u * Mathf.PI);
                float end = Mathf.Sqrt(Mathf.Max(0.0001f, 1f - u * u * u * u));
                angle = Mathf.Atan2((p.y - 0.35f - 1.8f * arc) / ((1.5f - 1.1f * u) * end),
                    (p.z + 71f + 2f * u) / ((3.5f + 7f * arc) * end));
                return 2;
            }
            if (p.z > -54f && p.z < 32f && x > r.x + 0.2f)
            {
                u = Mathf.Clamp01((x - 15f) / 38f);
                float arc = Mathf.Sin(u * Mathf.PI);
                float end = Mathf.Sqrt(Mathf.Max(0.0001f, 1f - u * u * u * u));
                float c = Mathf.Clamp((p.z - 18f + 36f * u - 6f * arc) / ((10.5f - 4f * u) * end), -1f, 1f);
                // Invert the rounded section; the small scallop only affects its leading margin.
                for (int i = 0; i < 2; i++)
                {
                    float scallop = 0.60f * arc * arc * Mathf.Cos(u * Mathf.PI * 9f) * Mathf.Pow(Mathf.Max(0f, c), 8f);
                    c = Mathf.Clamp((p.z - 18f + 36f * u - 6f * arc - scallop) / ((10.5f - 4f * u) * end), -1f, 1f);
                }
                angle = Mathf.Atan2((p.y + 6f + 7f * u - 2.5f * arc - 0.65f * arc * c * c)
                    / ((2.3f - 1.4f * u) * end), c);
                return 1;
            }
            u = p.z;
            float y = (p.y - CenterY(p.z)) / Mathf.Max(0.0001f, r.y);
            float s = Mathf.Clamp(y, -1f, 1f);
            for (int i = 0; i < 4; i++)
            {
                float k = 4.6f * Mathf.Exp(-(p.z + 29f) * (p.z + 29f) / 95f) / Mathf.Max(0.0001f, r.y);
                float positive = Mathf.Max(0f, s);
                float f = s - 0.10f * s * s + k * Mathf.Pow(positive, 12f) - y;
                float derivative = 1f - 0.20f * s + 12f * k * Mathf.Pow(positive, 11f);
                s = Mathf.Clamp(s - f / derivative, -1f, 1f);
            }
            angle = Mathf.Atan2(s, p.x / Mathf.Max(0.0001f, r.x));
            return 0;
        }

        static void Frame(Vector3 p, out Vector3 along, out Vector3 around, out int surface, out int side)
        {
            float u, angle;
            surface = SurfaceFrame(p, out u, out angle, out side);
            if (surface == 0)
            {
                along = Body(Mathf.Min(75.999f, u + 0.025f), angle) - Body(Mathf.Max(-77.999f, u - 0.025f), angle);
                around = Body(u, angle + 0.002f) - Body(u, angle - 0.002f);
            }
            else if (surface == 1)
            {
                along = Flipper(u + 0.0005f, angle, side) - Flipper(u - 0.0005f, angle, side);
                around = Flipper(u, angle + 0.002f, side) - Flipper(u, angle - 0.002f, side);
            }
            else
            {
                along = Fluke(u + 0.0005f, angle, side) - Fluke(u - 0.0005f, angle, side);
                around = Fluke(u, angle + 0.002f, side) - Fluke(u, angle - 0.002f, side);
            }
        }

        public static Vector3 Normal(Vector3 p)
        {
            Vector3 along, around;
            int surface, side;
            Frame(p, out along, out around, out surface, out side);
            Vector3 n = surface == 0 ? Vector3.Cross(around, along) : Vector3.Cross(along, around) * side;
            return n.sqrMagnitude > 0.0000000001f ? n.normalized : Vector3.up;
        }

        public static Vector3 Tangent(Vector3 p)
        {
            Vector3 along, around;
            int surface, side;
            Frame(p, out along, out around, out surface, out side);
            return along.sqrMagnitude > 0.0000000001f ? along.normalized : Vector3.forward;
        }

        // Invert an area CDF of the actual asymmetric surface, including its cap slopes.
        // Independent hashed positions within each stratum avoid visible rings or lattice streaks.
        static void SampleSurface(Action<Vector3, float, Color, float, float> emit, ref int identity,
            int count, Func<float, float, Vector3> surface, int region)
        {
            const int rows = 128, columns = 64;
            var area = new float[rows * columns + 1];
            for (int cell = 0; cell < rows * columns; cell++)
            {
                int row = cell / columns, column = cell % columns;
                float u0 = row / (float)rows, u1 = (row + 1f) / rows;
                float v0 = column / (float)columns, v1 = (column + 1f) / columns;
                Vector3 a = surface(u0, v0), b = surface(u1, v0);
                Vector3 c = surface(u0, v1), d = surface(u1, v1);
                float weight = (Vector3.Cross(b - a, c - a).magnitude + Vector3.Cross(c - d, b - d).magnitude) * 0.5f;
                area[cell + 1] = area[cell] + weight;
            }
            int offset = identity;
            for (int i = 0; i < count; i++)
            {
                int id = offset + i;
                float quantile = (i + Hash(id + 17017)) / count * area[rows * columns];
                int lo = 0, hi = rows * columns;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (area[mid] < quantile) lo = mid;
                    else hi = mid;
                }
                float u = (lo / columns + Hash(id + 31013)) / rows;
                float v = (lo % columns + Hash(id + 71023)) / columns;
                Vector3 p = surface(u, v);
                float belly = region == 0 ? Smooth(-0.15f, 0.82f, -Mathf.Sin(v * Tau)) : Smooth(-0.1f, 0.8f, -Mathf.Sin(v * Tau));
                float flow = 0.5f + 0.5f * Mathf.Sin(p.z * 0.044f + v * Tau * 2f);
                Color color = Color.Lerp(Skin, Sea, 0.18f + flow * 0.25f);
                color = Color.Lerp(color, Silver, belly * (region == 0 ? 0.48f : 0.85f));
                Emit(emit, ref identity, p, 0.055f + 0.024f * Hash(id + 91019), color, 0f);
            }
        }

        // Arc-length samples keep continuous contour brightness through the rounded caps and tips.
        static void Curve(Action<Vector3, float, Color, float, float> emit, ref int identity,
            int count, Func<float, Vector3> path, Color color, float radius, float role)
        {
            const int steps = 512;
            var length = new float[steps + 1];
            Vector3 previous = path(0f);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = path(i / (float)steps);
                length[i] = length[i - 1] + Vector3.Distance(previous, p);
                previous = p;
            }
            int segment = 0;
            for (int i = 0; i < count; i++)
            {
                float distance = (i + 0.5f) / count * length[steps];
                while (segment < steps - 1 && length[segment + 1] < distance) segment++;
                float t = (segment + Mathf.InverseLerp(length[segment], length[segment + 1], distance)) / steps;
                Vector3 p = path(t);
                float fade = 0.40f + 0.60f * Mathf.Sin(t * Mathf.PI);
                Emit(emit, ref identity, p + Normal(p) * 0.065f, radius, color * fade, role);
            }
        }

        static void Emit(Action<Vector3, float, Color, float, float> emit, ref int identity,
            Vector3 p, float radius, Color color, float role)
        {
            color.a = 1f;
            emit(p, radius, color, Hash(identity + 172009), role);
            identity++;
        }

        static float Hash(int value)
        {
            unchecked
            {
                uint h = (uint)value;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0x00ffffffu) / 16777216f;
            }
        }
    }
}
