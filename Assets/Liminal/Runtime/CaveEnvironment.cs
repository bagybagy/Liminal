using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class CaveEnvironment : MonoBehaviour
    {
        const int LightSlots = 8;
        const float LightLifetime = 10f;
        const float TunnelRadius = CaveLayout.PassageRadius + 9f;
        static readonly int LightArrayId = Shader.PropertyToID("_CaveLights");
        static readonly int PulseId = Shader.PropertyToID("_CavePulse");
        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly Vector4[] lights = new Vector4[LightSlots];
        readonly float[] lightUntil = new float[LightSlots];
        readonly float[] lightStrength = new float[LightSlots];
        readonly System.Random random = new(264991);
        ParticleWorld world;
        Material particleMaterial, surfaceMaterial;
        Mesh surfaceMesh;
        float clock;
        int lightCursor, particleCount;

        public int ParticleCount => particleCount;

        float R(float min = 0f, float max = 1f) => min + (float)random.NextDouble() * (max - min);

        public void Initialize(ParticleWorld owner)
        {
            world = owner;
            particleMaterial = new Material(owner.particleTemplate);
            particleMaterial.SetFloat("_Mode", 0f);
            particleMaterial.SetFloat("_Gain", 1.25f);
            particleMaterial.SetFloat("_CaveWaveEnergy", 0f);
            materials.Add(particleMaterial);
            Material surfaceTemplate = owner.cavernSurface;
            if (!surfaceTemplate) {
                Shader fallback = Shader.Find("Liminal/Cavern Surface");
                if (!fallback) throw new InvalidOperationException("Liminal/Cavern Surface shader is missing.");
                surfaceMaterial = new Material(fallback);
            } else surfaceMaterial = new Material(surfaceTemplate);
            materials.Add(surfaceMaterial);

            var structural = new PointCloud();
            BuildShell(structural);
            BuildLuminousStrata(structural);
            BuildPassages(structural);
            BuildRockFormations(structural);
            BuildRoomArchitecture(structural);
            BuildSuspendedMatter(structural);
            particleCount = structural.Count;
            PointCloud.Place("Cavern strata and reef", Mesh(structural, "Cavern particles"), particleMaterial, transform);
            var shell = new GameObject("Inward cavern walls");
            shell.transform.SetParent(transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = surfaceMesh;
            var renderer = shell.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surfaceMaterial.SetVectorArray(LightArrayId, lights);
        }

        Mesh Mesh(PointCloud points, string name)
        {
            Mesh mesh = points.Build(name, 2400f);
            meshes.Add(mesh);
            return mesh;
        }

        void BuildShell(PointCloud particles)
        {
            var vertices = new List<Vector3>(3 * 73 * 145);
            var colors = new List<Color>(3 * 73 * 145);
            var triangles = new List<int>(3 * 72 * 144 * 6);
            for (int roomIndex = 0; roomIndex < CaveLayout.Rooms.Length; roomIndex++) {
                var room = CaveLayout.Rooms[roomIndex];
                int first = vertices.Count;
                const int latitudes = 72, longitudes = 144;
                for (int lat = 0; lat <= latitudes; lat++) {
                    float v = lat / (float)latitudes;
                    float phi = Mathf.PI * v;
                    for (int lon = 0; lon <= longitudes; lon++) {
                        float theta = Mathf.PI * 2f * lon / longitudes;
                        Vector3 unit = new(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                        float relief = ShellRelief(theta, phi, roomIndex);
                        Vector3 p = room.Center + Vector3.Scale(unit, room.Radius * (1.018f + relief));
                        vertices.Add(p);
                        float strata = 0.5f + 0.5f * Mathf.Sin(phi * 13f + Mathf.Sin(theta * 6f) * 2f + Mathf.Sin(theta * 17f - phi * 8f) * 0.35f);
                        colors.Add(Color.Lerp(room.Color, room.Accent, 0.08f + strata * 0.06f) * (0.10f + strata * 0.025f));
                        if (lat < 1 || lat >= latitudes) continue;
                        if (lon == 0) continue;
                    }
                }
                for (int lat = 1; lat < latitudes; lat++) for (int lon = 0; lon < longitudes; lon++) {
                    int a = first + lat * (longitudes + 1) + lon;
                    int b = a + longitudes + 1;
                    int c = b + 1, d = a + 1;
                    Vector3 center1 = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                    Vector3 center2 = (vertices[a] + vertices[c] + vertices[d]) / 3f;
                    if (!CaveLayout.InPassage(center1, -8f)) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
                    if (!CaveLayout.InPassage(center2, -8f)) { triangles.Add(a); triangles.Add(c); triangles.Add(d); }
                }

                // Mineral seams are concentrated in broken bands, with occasional isolated glints.
                for (int lat = 1; lat < latitudes; lat++) for (int lon = 0; lon < longitudes; lon++) {
                    int index = first + lat * (longitudes + 1) + lon;
                    Vector3 p = vertices[index];
                    if (CaveLayout.InPassage(p, -8f)) continue;
                    float band = Mathf.Pow(Mathf.Abs(Mathf.Sin(lat * 0.105f + Mathf.Sin(lon * 0.037f) * 1.7f)), 5f);
                    float fleck = Mathf.Pow(Mathf.Abs(Mathf.Sin(lat * 0.51f + lon * 0.13f)), 12f);
                    float chance = Mathf.Lerp(0.012f, 0.24f, band) + fleck * 0.14f;
                    if (R() > chance) continue;
                    Color col = Color.Lerp(room.Color, room.Accent, R(0.10f, 0.48f));
                    particles.Add(p, R(0.04f, 0.075f), col * R(0.10f, 0.25f), R(), 0.035f);
                }
            }
            surfaceMesh = new Mesh { name = "Three chamber shell", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            surfaceMesh.SetVertices(vertices);
            surfaceMesh.SetColors(colors);
            surfaceMesh.SetTriangles(triangles, 0);
            surfaceMesh.RecalculateNormals();
            surfaceMesh.bounds = new Bounds(new Vector3(0, 0, 100), new Vector3(1000, 1000, 1900));
            meshes.Add(surfaceMesh);
        }

        static float ShellRelief(float theta, float phi, int room)
        {
            return 0.018f
                + 0.016f * (0.5f + 0.5f * Mathf.Sin(theta * 5f + phi * 3f + room))
                + 0.014f * (0.5f + 0.5f * Mathf.Sin(theta * 11f - phi * 7f + Mathf.Sin(phi * 5f)))
                + 0.010f * (0.5f + 0.5f * Mathf.Sin(theta * 19f + phi * 13f + room * 2f));
        }

        void BuildLuminousStrata(PointCloud particles)
        {
            for (int index = 0; index < CaveLayout.Rooms.Length; index++) {
                var room = CaveLayout.Rooms[index];
                for (int i = 0; i < 72000; i++) {
                    float y = R(-1, 1), theta = R(0, Mathf.PI * 2), phi = Mathf.Acos(y);
                    float radial = Mathf.Sqrt(1 - y * y);
                    Vector3 unit = new(radial * Mathf.Cos(theta), y, radial * Mathf.Sin(theta));
                    Vector3 at = room.Center + Vector3.Scale(unit, room.Radius * (1.006f + ShellRelief(theta, phi, index)));
                    if (CaveLayout.InPassage(at, -9)) continue;
                    float vein = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(phi * 31 + Mathf.Sin(theta * 7) * 3), 8);
                    float colony = Mathf.PerlinNoise(theta * 3.4f + index * 13, phi * 5.2f);
                    float radiance = (0.12f + vein * 0.9f) * Mathf.Lerp(0.3f, 1.3f, colony);
                    Color color = Color.Lerp(room.Color, room.Accent, vein * 0.65f);
                    particles.Add(at, R(0.09f, 0.18f), color * radiance, R(), 0.02f);
                }
                // A lower, rippled seabed gives a readable horizon below the open swimming volume.
                for (int i = 0; i < 40000; i++) {
                    float x = R(-0.83f, 0.83f), z = R(-0.83f, 0.83f);
                    if (x * x + z * z > 0.69f) continue;
                    float floor = -Mathf.Sqrt(1 - x * x - z * z) * room.Radius.y + 4;
                    floor += Mathf.Sin(x * 18 + z * 8) * 1.5f;
                    Vector3 at = room.Center + new Vector3(x * room.Radius.x, floor, z * room.Radius.z);
                    if (CaveLayout.InPassage(at, -4)) continue;
                    float ripple = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(x * 94 + Mathf.Sin(z * 19) * 3), 4);
                    particles.Add(at, R(0.08f, 0.15f), Color.Lerp(room.Color, room.Accent, ripple * 0.6f) * (0.2f + ripple * 0.7f), R(), 0.14f);
                }
            }
        }

        void BuildPassages(PointCloud particles)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            foreach (var route in CaveLayout.Passages) {
                for (int segment = 1; segment < route.Length; segment++) {
                    Vector3 start = route[segment - 1], end = route[segment];
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(start, end) / 12f));
                    Vector3 forward = (end - start).normalized;
                    Vector3 side = Vector3.Cross(forward, Vector3.up).normalized;
                    if (side.sqrMagnitude < 0.1f) side = Vector3.right;
                    Vector3 up = Vector3.Cross(side, forward).normalized;
                    for (int step = 0; step < steps; step++) {
                        float f = (step + 0.5f) / steps;
                        Vector3 center = Vector3.Lerp(start, end, f);
                        for (int radial = 0; radial < 24; radial++) {
                            float angle = radial * Mathf.PI * 2f / 24f;
                            Vector3 p = center + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * (TunnelRadius - 2f);
                            if (IsBuriedInRoom(p) || InsideOtherPassage(p, route, segment - 1)) continue;
                            Color col = Color.Lerp(CaveLayout.Rooms[1].Color, CaveLayout.Rooms[2].Color, segment / (float)route.Length);
                            float trail = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(angle * 5f + f * 13f)), 9f);
                            if (trail > 0.56f) particles.Add(p, 0.06f, col * R(0.12f, 0.25f), R(), 0.025f);
                        }
                    }
                    int begin = verts.Count;
                    for (int step = 0; step <= steps; step++) {
                        Vector3 center = Vector3.Lerp(start, end, step / (float)steps);
                        for (int radial = 0; radial <= 24; radial++) {
                            float angle = radial * Mathf.PI * 2f / 24f;
                            verts.Add(center + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * TunnelRadius);
                            cols.Add(Color.Lerp(CaveLayout.Rooms[1].Color, CaveLayout.Rooms[2].Color, segment / (float)route.Length) * 0.11f);
                        }
                    }
                    for (int step = 0; step < steps; step++) {
                        for (int radial = 0; radial < 24; radial++) {
                            int a = begin + step * 25 + radial, b = a + 25;
                            AddTunnelTriangle(verts, tris, a, b, b + 1, route, segment - 1);
                            AddTunnelTriangle(verts, tris, a, b + 1, a + 1, route, segment - 1);
                        }
                    }
                }
            }
            if (verts.Count == 0) return;
            var tube = new Mesh { name = "Passage tunnel shell", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            tube.SetVertices(verts); tube.SetColors(cols); tube.SetTriangles(tris, 0); tube.RecalculateNormals();
            tube.bounds = new Bounds(new Vector3(50, 0, 50), new Vector3(800, 800, 1400));
            meshes.Add(tube);
            var obj = new GameObject("Passage tunnel walls");
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = tube;
            obj.AddComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }

        void BuildRockFormations(PointCloud particles)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int roomIndex = 0; roomIndex < CaveLayout.Rooms.Length; roomIndex++) {
                var room = CaveLayout.Rooms[roomIndex];
                Vector3 route = roomIndex < 2
                    ? CaveLayout.Passages[roomIndex][0] - room.Center
                    : room.Center - CaveLayout.Passages[1][^1];
                route.y = 0f;
                route.Normalize();
                for (int formation = 0; formation < 4; formation++) {
                    float angle = formation * Mathf.PI * 0.5f + roomIndex * 0.37f;
                    Vector3 horizontal = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    if (Vector3.Dot(horizontal, route) > 0.70f) continue;
                    bool stalactite = formation == 2;
                    Vector3 root = room.Center + new Vector3(horizontal.x * room.Radius.x * 0.61f,
                        room.Radius.y * (stalactite ? 0.79f : -0.76f), horizontal.z * room.Radius.z * 0.61f);
                    float length = room.Radius.y * R(stalactite ? 0.20f : 0.27f, stalactite ? 0.34f : 0.43f);
                    float radius = Mathf.Min(room.Radius.x, room.Radius.z) * R(0.028f, 0.044f);
                    AddSpire(vertices, colors, triangles, particles, room, root, horizontal, length, radius, stalactite, formation);
                }
            }
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = "Dark tapered cavern spires", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0, 0, 100), new Vector3(1000, 1000, 1900));
            meshes.Add(mesh);
            var obj = new GameObject("Pillars and hanging stone");
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }

        void AddSpire(List<Vector3> vertices, List<Color> colors, List<int> triangles, PointCloud particles,
            CaveLayout.Chamber room, Vector3 root, Vector3 lean, float length, float radius, bool hangs, int seed)
        {
            const int rings = 9, sides = 14;
            int first = vertices.Count;
            float sign = hangs ? -1f : 1f;
            for (int ring = 0; ring <= rings; ring++) {
                float t = ring / (float)rings;
                float taper = Mathf.Pow(1f - t, 0.78f) * (0.92f + 0.09f * Mathf.Sin(t * 17f + seed));
                Vector3 center = root + Vector3.up * (sign * length * t) + lean * (Mathf.Sin(t * 4.1f + seed) * t * 5f);
                for (int side = 0; side < sides; side++) {
                    float angle = side * Mathf.PI * 2f / sides;
                    float lobe = 1f + 0.16f * Mathf.Sin(angle * 3f + t * 8f + seed) + 0.08f * Mathf.Sin(angle * 5f - t * 11f);
                    Vector3 offset = new(Mathf.Cos(angle) * radius * taper * lobe, 0, Mathf.Sin(angle) * radius * taper * lobe);
                    vertices.Add(center + offset);
                    colors.Add(Color.Lerp(room.Color, room.Accent, 0.12f + t * 0.10f) * 0.045f);
                }
            }
            for (int ring = 0; ring < rings; ring++) for (int side = 0; side < sides; side++) {
                int a = first + ring * sides + side;
                int b = first + ring * sides + (side + 1) % sides;
                int c = b + sides, d = a + sides;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
            for (int facet = 0; facet < 3; facet++) for (int ring = 1; ring <= rings; ring++) {
                float t = ring / (float)rings;
                if (R() > 0.72f) continue;
                float angle = facet * Mathf.PI * 2f / 3f + 0.16f;
                float taper = Mathf.Pow(1f - t, 0.78f);
                Vector3 center = root + Vector3.up * (sign * length * t) + lean * (Mathf.Sin(t * 4.1f + seed) * t * 5f);
                Vector3 edge = center + new Vector3(Mathf.Cos(angle) * radius * taper * 1.08f, 0,
                    Mathf.Sin(angle) * radius * taper * 1.08f);
                Color glow = Color.Lerp(room.Color, room.Accent, R(0.22f, 0.72f));
                particles.Add(edge, R(0.045f, 0.072f), glow * R(0.22f, 0.48f), R(), 0.035f);
            }
        }

        void BuildSuspendedMatter(PointCloud particles)
        {
            const int perRoom = 24000;
            for (int roomIndex = 0; roomIndex < CaveLayout.Rooms.Length; roomIndex++) {
                var room = CaveLayout.Rooms[roomIndex];
                for (int i = 0; i < perRoom; i++) {
                    Vector3 normalized;
                    if (i % 4 == 0) {
                        float t = R();
                        float angle = t * Mathf.PI * 2.8f + roomIndex * 1.9f + R(-0.16f, 0.16f);
                        float orbit = 0.36f + 0.16f * Mathf.Sin(t * Mathf.PI * 3f);
                        normalized = new Vector3(Mathf.Cos(angle) * orbit, (t - 0.5f) * 1.05f, Mathf.Sin(angle) * orbit);
                        normalized += new Vector3(R(-0.06f, 0.06f), R(-0.045f, 0.045f), R(-0.06f, 0.06f));
                    } else {
                        float y = R(-1f, 1f);
                        float angle = R(0f, Mathf.PI * 2f);
                        float radial = Mathf.Pow(R(), 1f / 3f) * 0.92f;
                        float planar = Mathf.Sqrt(1f - y * y) * radial;
                        normalized = new Vector3(Mathf.Cos(angle) * planar, y * radial, Mathf.Sin(angle) * planar);
                    }
                    Vector3 at = room.Center + Vector3.Scale(room.Radius, normalized);
                    float glint = R();
                    float energy = glint > 0.997f ? R(0.25f, 0.42f) : R(0.045f, 0.15f);
                    Color tint = Color.Lerp(room.Color, room.Accent, R(0.08f, glint > 0.97f ? 0.75f : 0.34f));
                    particles.Add(at, R(0.021f, glint > 0.98f ? 0.064f : 0.043f), tint * energy, R(), R(0.035f, 0.18f));
                }
            }
        }

        void AddTunnelTriangle(List<Vector3> vertices, List<int> triangles, int a, int b, int c, Vector3[] route, int segment)
        {
            Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            if (IsBuriedInRoom(center) || InsideOtherPassage(center, route, segment)) return;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        bool IsBuriedInRoom(Vector3 point)
        {
            for (int i = 0; i < CaveLayout.Rooms.Length; i++) if (CaveLayout.RoomDistance(i, point) < 0.985f) return true;
            return false;
        }

        bool InsideOtherPassage(Vector3 point, Vector3[] owner, int ownerSegment)
        {
            float radiusSquared = (TunnelRadius - 1f) * (TunnelRadius - 1f);
            foreach (var route in CaveLayout.Passages) for (int i = 1; i < route.Length; i++) {
                if (ReferenceEquals(route, owner) && i - 1 == ownerSegment) continue;
                Vector3 closest = CaveLayout.ClosestOnSegment(point, route[i - 1], route[i]);
                if ((point - closest).sqrMagnitude < radiusSquared) return true;
            }
            return false;
        }

        void BuildRoomArchitecture(PointCloud p)
        {
            for (int roomIndex = 0; roomIndex < CaveLayout.Rooms.Length; roomIndex++) {
                var room = CaveLayout.Rooms[roomIndex];
                Color reef = Color.Lerp(room.Color, room.Accent, 0.65f);
                // Floor-rooted branching coral and upright stone ribs stay around the room perimeter.
                for (int i = 0; i < 22; i++) {
                    float angle = i * 2.399f + roomIndex * 0.8f;
                    float radial = R(0.38f, 0.76f);
                    Vector3 root = room.Center + new Vector3(Mathf.Cos(angle) * room.Radius.x * radial,
                        -room.Radius.y * Mathf.Sqrt(1-radial*radial) + 4, Mathf.Sin(angle) * room.Radius.z * radial);
                    if (CaveLayout.InPassage(root, -12)) continue;
                    Branch(p, root, (Vector3.up + new Vector3(Mathf.Cos(angle) * 0.18f, 0, Mathf.Sin(angle) * 0.18f)).normalized,
                        room.Radius.y * R(0.17f, 0.30f), 4, reef * 0.95f);
                    if (i % 5 == 0) Rib(p, room, angle, i);
                }
                BuildFan(p, room, reef);
                BuildKelp(p, room, reef);
                BuildFloorGuide(p, room, reef);
            }
        }

        void Branch(PointCloud p, Vector3 start, Vector3 direction, float length, int depth, Color color)
        {
            Vector3 bend = new(R(-0.26f, 0.26f), 0, R(-0.26f, 0.26f));
            int count = Mathf.CeilToInt(length * 18f);
            for (int i = 0; i < count; i++) {
                float f = i / (float)count;
                Vector3 at = start + direction * length * f + bend * length * f * f;
                at += new Vector3(Mathf.Cos(i * 2.399f), 0, Mathf.Sin(i * 2.399f)) * depth * 0.055f;
                p.Add(at, R(0.065f, 0.115f), color * R(0.5f, 1f), R(), 0.12f);
            }
            if (depth == 0) return;
            Vector3 tip = start + (direction + bend) * length;
            for (int i = 0; i < 3; i++) Branch(p, tip,
                (direction + new Vector3(R(-0.85f, 0.85f), R(0f, 0.35f), R(-0.85f, 0.85f))).normalized,
                length * R(0.43f, 0.61f), depth - 1, color * 0.83f);
        }

        void Rib(PointCloud p, CaveLayout.Chamber room, float angle, int seed)
        {
            Vector3 origin = room.Center + new Vector3(Mathf.Cos(angle) * room.Radius.x * 0.70f, -room.Radius.y * 0.68f,
                Mathf.Sin(angle) * room.Radius.z * 0.70f);
            float height = room.Radius.y * R(0.48f, 0.76f);
            for (int i = 0; i < 110; i++) {
                float f = i / 109f;
                Vector3 point = origin + Vector3.up * height * f;
                point += new Vector3(Mathf.Sin(f * 6f + seed) * 3.4f, 0, Mathf.Cos(f * 4f + seed) * 3.2f);
                int strands = Mathf.RoundToInt(Mathf.Lerp(3f, 1f, f));
                for (int strand = 0; strand < strands; strand++) {
                    Vector3 at = point + new Vector3(Mathf.Cos(strand * 2.1f) * (1.6f - f), 0, Mathf.Sin(strand * 2.1f) * (1.6f - f));
                    p.Add(at, 0.055f, Color.Lerp(room.Color, room.Accent, f * 0.58f) * (0.24f + f * 0.18f), R(), 0.025f);
                }
            }
        }

        void BuildFan(PointCloud p, CaveLayout.Chamber room, Color color)
        {
            for (int fan = 0; fan < 3; fan++) {
                float angle = fan * Mathf.PI * 0.73f + room.Center.z * 0.003f;
                Vector3 root = room.Center + new Vector3(Mathf.Cos(angle) * room.Radius.x * 0.72f, -room.Radius.y * 0.52f,
                    Mathf.Sin(angle) * room.Radius.z * 0.72f);
                for (int ray = -5; ray <= 5; ray++) {
                    Vector3 dir = (Vector3.up * 0.63f + new Vector3(Mathf.Cos(angle + ray * 0.12f), 0.12f,
                        Mathf.Sin(angle + ray * 0.12f)) * 0.78f).normalized;
                    int dots = 44 - Mathf.Abs(ray) * 3;
                    for (int j = 0; j < dots; j++) {
                        float f = j / (float)dots;
                        Vector3 at = root + dir * (19f * f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 4f;
                        p.Add(at, 0.05f + (1f - f) * 0.025f,
                            Color.Lerp(color, room.Accent, f * 0.42f) * R(0.34f, 0.66f), R(), 0.035f);
                    }
                }
            }
        }

        void BuildKelp(PointCloud p, CaveLayout.Chamber room, Color color)
        {
            Vector3 route = CaveLayout.ForwardWaypoint(room.Center, Array.IndexOf(CaveLayout.Rooms, room));
            Vector3 toward = (route - room.Center).normalized;
            for (int strand = 0; strand < 16; strand++) {
                float angle = strand * 2.399f;
                Vector3 root = room.Center + new Vector3(Mathf.Cos(angle) * room.Radius.x * 0.57f, -room.Radius.y * 0.66f,
                    Mathf.Sin(angle) * room.Radius.z * 0.57f);
                if (Vector3.Dot((root - room.Center).normalized, toward) > 0.65f) continue;
                float length = room.Radius.y * R(0.34f, 0.68f);
                for (int j = 0; j < 115; j++) {
                    float f = j / 114f;
                    Vector3 at = root + Vector3.up * length * f + new Vector3(Mathf.Sin(f * 5f + angle) * (2f + f * 5f), 0,
                        Mathf.Cos(f * 4f + angle) * (2f + f * 4f));
                    Color blade = Color.Lerp(color, room.Accent, f * 0.34f) * R(0.20f, 0.40f);
                    p.Add(at, R(0.035f, 0.058f), blade, R(), 0.16f);
                }
            }
        }

        void BuildFloorGuide(PointCloud p, CaveLayout.Chamber room, Color color)
        {
            Vector3 start = room.Center + Vector3.down * room.Radius.y * 0.70f;
            Vector3 end = CaveLayout.ForwardWaypoint(room.Center, Array.IndexOf(CaveLayout.Rooms, room));
            end.y = Mathf.Lerp(start.y, end.y, 0.32f);
            for (int i = 0; i < 170; i++) {
                float f = i / 169f;
                Vector3 at = Vector3.Lerp(start, end, f) + Vector3.up * (Mathf.Sin(f * 8f) * 2f);
                at += Vector3.right * Mathf.Sin(f * 13f) * (2f + f * 3f);
                float fade = Mathf.Lerp(0.50f, 0.12f, f);
                p.Add(at, 0.052f, Color.Lerp(room.Accent, room.Color, f * 0.55f) * fade, R(), 0.025f);
            }
        }

        public void Tick(float song, float dt, Vector3 player)
        {
            clock = song;
            surfaceMaterial.SetFloat(PulseId, Mathf.Clamp01(Score.Pulse(song)));
            for (int i = 0; i < LightSlots; i++) {
                float remaining = Mathf.Clamp01((lightUntil[i] - song) / LightLifetime);
                lights[i].w = lightStrength[i] * remaining;
                if (remaining <= 0f) { lights[i] = Vector4.zero; lightStrength[i] = 0f; }
            }
            surfaceMaterial.SetVectorArray(LightArrayId, lights);
        }

        public void Illuminate(Vector3 position, float strength)
        {
            int slot = lightCursor++ % LightSlots;
            lightStrength[slot] = Mathf.Clamp(strength, 0f, 8f);
            lights[slot] = new Vector4(position.x, position.y, position.z, lightStrength[slot]);
            lightUntil[slot] = clock + LightLifetime;
            surfaceMaterial.SetVectorArray(LightArrayId, lights);
            particleMaterial.SetVector("_CaveWave", new Vector4(position.x, position.y, position.z, clock));
            particleMaterial.SetFloat("_CaveWaveEnergy", Mathf.Clamp(strength, 0f, 4f));
        }

        public void ResetLighting()
        {
            Array.Clear(lights, 0, lights.Length);
            Array.Clear(lightUntil, 0, lightUntil.Length);
            Array.Clear(lightStrength, 0, lightStrength.Length);
            if (surfaceMaterial) surfaceMaterial.SetVectorArray(LightArrayId, lights);
            if (particleMaterial) particleMaterial.SetFloat("_CaveWaveEnergy", 0f);
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
