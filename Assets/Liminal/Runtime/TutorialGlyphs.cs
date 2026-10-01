using UnityEngine;

namespace Liminal
{
    internal static class TutorialGlyphs
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/";
        static readonly string[] Patterns = {
            "01110/10001/10001/11111/10001/10001/10001",
            "11110/10001/10001/11110/10001/10001/11110",
            "01111/10000/10000/10000/10000/10000/01111",
            "11110/10001/10001/10001/10001/10001/11110",
            "11111/10000/10000/11110/10000/10000/11111",
            "11111/10000/10000/11110/10000/10000/10000",
            "01111/10000/10000/10111/10001/10001/01111",
            "10001/10001/10001/11111/10001/10001/10001",
            "11111/00100/00100/00100/00100/00100/11111",
            "00111/00010/00010/00010/10010/10010/01100",
            "10001/10010/10100/11000/10100/10010/10001",
            "10000/10000/10000/10000/10000/10000/11111",
            "10001/11011/10101/10101/10001/10001/10001",
            "10001/11001/10101/10011/10001/10001/10001",
            "01110/10001/10001/10001/10001/10001/01110",
            "11110/10001/10001/11110/10000/10000/10000",
            "01110/10001/10001/10001/10101/10010/01101",
            "11110/10001/10001/11110/10100/10010/10001",
            "01111/10000/10000/01110/00001/00001/11110",
            "11111/00100/00100/00100/00100/00100/00100",
            "10001/10001/10001/10001/10001/10001/01110",
            "10001/10001/10001/10001/10001/01010/00100",
            "10001/10001/10001/10101/10101/10101/01010",
            "10001/10001/01010/00100/01010/10001/10001",
            "10001/10001/01010/00100/00100/00100/00100",
            "11111/00001/00010/00100/01000/10000/11111",
            "01110/10001/10011/10101/11001/10001/01110",
            "00100/01100/00100/00100/00100/00100/01110",
            "01110/10001/00001/00010/00100/01000/11111",
            "11110/00001/00001/01110/00001/00001/11110",
            "00010/00110/01010/10010/11111/00010/00010",
            "11111/10000/10000/11110/00001/00001/11110",
            "01110/10000/10000/11110/10001/10001/01110",
            "11111/00001/00010/00100/01000/01000/01000",
            "01110/10001/10001/01110/10001/10001/01110",
            "01110/10001/10001/01111/00001/00001/01110",
            "00001/00010/00010/00100/01000/01000/10000"
        };

        public static void AddText(PointCloud cloud, string text, Vector2 center, float cell,
            float size, Color color, int seed)
        {
            int width = 0;
            foreach (char value in text) width += value == ' ' ? 4 : 6;
            width = Mathf.Max(0, width - 1);
            float left = center.x - width * cell * 0.5f;
            int cursor = 0, point = 0;
            foreach (char raw in text) {
                char value = char.ToUpperInvariant(raw);
                int patternIndex = Alphabet.IndexOf(value);
                if (patternIndex < 0) {
                    cursor += value == ' ' ? 4 : 6;
                    continue;
                }
                string pattern = Patterns[patternIndex];
                for (int row = 0; row < 7; row++)
                    for (int column = 0; column < 5; column++) {
                        if (pattern[row * 6 + column] != '1') continue;
                        Vector3 position = new(left + (cursor + column) * cell,
                            center.y + (3 - row) * cell, 0f);
                        cloud.Add(position, size, color, Seed(seed, point++));
                    }
                cursor += 6;
            }
        }

        public static void AddArrow(PointCloud cloud, Vector2 center, Vector2 direction,
            float length, float size, Color color, int seed)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            direction.Normalize();
            Vector2 side = new(-direction.y, direction.x);
            Vector2 tip = center + direction * (length * 0.5f);
            Vector2 tail = center - direction * (length * 0.42f);
            int point = 0;
            AddSegment(cloud, tail, tip, 7, size, color, seed, ref point);
            float head = length * 0.27f;
            AddSegment(cloud, tip - direction * head + side * head * 0.72f, tip,
                4, size, color, seed, ref point);
            AddSegment(cloud, tip - direction * head - side * head * 0.72f, tip,
                4, size, color, seed, ref point);
        }

        public static void AddMouse(PointCloud cloud, Vector2 center, float width, float height,
            float size, Color color, Color buttonColor, int seed)
        {
            int point = 0;
            float halfWidth = width * 0.5f, halfHeight = height * 0.5f;
            for (int i = 0; i <= 18; i++) {
                float angle = Mathf.PI * i / 18f;
                AddPoint(cloud, center + new Vector2(Mathf.Cos(angle) * halfWidth,
                    Mathf.Sin(angle) * halfHeight), size, color, seed, ref point);
            }
            AddSegment(cloud, center + new Vector2(-halfWidth, 0f),
                center + new Vector2(-halfWidth, -halfHeight), 5, size, color, seed, ref point);
            AddSegment(cloud, center + new Vector2(halfWidth, 0f),
                center + new Vector2(halfWidth, -halfHeight), 5, size, color, seed, ref point);
            AddSegment(cloud, center + new Vector2(-halfWidth, -halfHeight),
                center + new Vector2(halfWidth, -halfHeight), 7, size, color, seed, ref point);
            AddSegment(cloud, center + new Vector2(0f, 0.02f),
                center + new Vector2(0f, halfHeight * 0.78f), 5, size, color, seed, ref point);
            AddSegment(cloud, center + new Vector2(-halfWidth * 0.92f, 0.02f),
                center + new Vector2(halfWidth * 0.92f, 0.02f), 7, size * 0.78f, color, seed, ref point);
            AddSegment(cloud, center + new Vector2(-width * 0.22f, height * 0.16f),
                center + new Vector2(-width * 0.22f, height * 0.34f), 3,
                size * 0.95f, buttonColor, seed + 31, ref point);
        }

        public static void AddSocket(PointCloud cloud, Vector2 center, float radius,
            float size, Color color, int seed)
        {
            int point = 0;
            const int samples = 28;
            for (int i = 0; i < samples; i++) {
                float angle = Mathf.PI * 2f * i / samples;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                AddPoint(cloud, p, size, color, seed, ref point);
            }
            for (int i = 0; i < 4; i++) {
                float angle = Mathf.PI * 0.5f * i;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                AddSegment(cloud, center + direction * radius * 0.78f,
                    center + direction * radius * 1.22f, 3, size, color, seed + 71, ref point);
            }
            AddPoint(cloud, center, size * 0.82f, color, seed + 103, ref point);
        }

        static void AddSegment(PointCloud cloud, Vector2 from, Vector2 to, int count,
            float size, Color color, int seed, ref int point)
        {
            for (int i = 0; i < count; i++) {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                AddPoint(cloud, Vector2.Lerp(from, to, t), size, color, seed, ref point);
            }
        }

        static void AddPoint(PointCloud cloud, Vector2 position, float size, Color color,
            int seed, ref int point)
        {
            cloud.Add(new Vector3(position.x, position.y, 0f), size, color, Seed(seed, point++));
        }

        static float Seed(int seed, int point)
        {
            return Mathf.Repeat(seed * 0.173f + point * 0.6180339f, 1f);
        }
    }
}
