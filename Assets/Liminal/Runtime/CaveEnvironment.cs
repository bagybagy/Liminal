using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class CaveEnvironment : MonoBehaviour
    {
        const int LightSlots = 8;
        const int OrganismLightSlots = 2;
        const int ShaderLightSlots = LightSlots + OrganismLightSlots;
        const float LightLifetime = 10f;
        const float TunnelRadius = CaveLayout.PassageRadius + 9f;
        static readonly int LightArrayId = Shader.PropertyToID("_CaveLights");
        static readonly int LightColorsId = Shader.PropertyToID("_CaveLightColors");
        static readonly int PulseId = Shader.PropertyToID("_CavePulse");
        static readonly int CaveSongId = Shader.PropertyToID("_CaveSong");
        static readonly int CaveRevealId = Shader.PropertyToID("_CaveReveal");
        static readonly int CaveSweepId = Shader.PropertyToID("_CaveSweep");
        static readonly int CaveBindId = Shader.PropertyToID("_CaveBind");
        static readonly int CavePlayerPositionId = Shader.PropertyToID("_CavePlayerPosition");
        const float VolumeLayer = 0.15f;
        const float FormationLayer = 0.55f;
        const float MarineLayer = 0.68f;
        const float MineralLayer = 0.85f;
        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly Vector4[] lights = new Vector4[ShaderLightSlots];
        readonly Vector4[] lightColors = new Vector4[ShaderLightSlots];
        readonly float[] lightUntil = new float[LightSlots];
        readonly float[] lightStrength = new float[LightSlots];
        readonly System.Random random = new(264991);
        ParticleWorld world;
        MarineLife marineLife;
        Material particleMaterial, cavernMatterMaterial, surfaceMaterial;
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
            Shader matterShader = Resources.Load<Shader>("CavernMatter");
            if (!matterShader) matterShader = Shader.Find("Liminal/Cavern Matter");
            if (!matterShader) throw new InvalidOperationException("Liminal/Cavern Matter shader is missing from Resources.");
            cavernMatterMaterial = new Material(matterShader);
            cavernMatterMaterial.SetFloat("_Gain", 2.6f);
            materials.Add(cavernMatterMaterial);
            Material surfaceTemplate = owner.cavernSurface;
            if (!surfaceTemplate) {
                Shader fallback = Shader.Find("Liminal/Cavern Surface");
                if (!fallback) throw new InvalidOperationException("Liminal/Cavern Surface shader is missing.");
                surfaceMaterial = new Material(fallback);
            } else surfaceMaterial = new Material(surfaceTemplate);
            materials.Add(surfaceMaterial);

            var structural = new PointCloud();
            var backgroundMatter = new PointCloud();
            BuildShell(backgroundMatter);
            BuildSparseRockLight(backgroundMatter);
            BuildPassages(structural);
            BuildSeabedEcology(backgroundMatter);
            BuildRoomArchitecture(backgroundMatter);
            BuildSuspendedMatter(backgroundMatter);
            particleCount = structural.Count + backgroundMatter.Count;
            PointCloud.Place("Cavern passages", Mesh(structural, "Passage accents"), particleMaterial, transform);
            PointCloud.Place("Cavern drift and mineral grains", Mesh(backgroundMatter, "Cavern matter"), cavernMatterMaterial, transform);
            var shell = new GameObject("Inward cavern walls");
            shell.transform.SetParent(transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = surfaceMesh;
            var renderer = shell.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surfaceMaterial.SetVectorArray(LightArrayId, lights);
            surfaceMaterial.SetVectorArray(LightColorsId, lightColors);
            cavernMatterMaterial.SetVectorArray(LightArrayId, lights);
            cavernMatterMaterial.SetVectorArray(LightColorsId, lightColors);
        }

        Mesh Mesh(PointCloud points, string name)
        {
            Mesh mesh = points.Build(name, 2400f);
            mesh.bounds = CaveLayout.WorldBounds;
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
                        colors.Add(Color.Lerp(room.Color, room.Accent, 0.055f) * 0.15f);
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

                // Sparse mineral flecks break up the dark shell without forming continuous bands.
                for (int lat = 1; lat < latitudes; lat++) for (int lon = 0; lon < longitudes; lon++) {
                    int index = first + lat * (longitudes + 1) + lon;
                    Vector3 p = vertices[index];
                    if (CaveLayout.InPassage(p, -8f)) continue;
                    if (R() > 0.022f) continue;
                    Color col = Color.Lerp(room.Color, room.Accent, R(0.10f, 0.48f));
                    particles.Add(p, R(0.04f, 0.075f), col * R(0.06f, 0.14f), R(), MineralLayer);
                }
            }
            surfaceMesh = new Mesh { name = "Connected chamber shell", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            surfaceMesh.SetVertices(vertices);
            surfaceMesh.SetColors(colors);
            surfaceMesh.SetTriangles(triangles, 0);
            surfaceMesh.RecalculateNormals();
            surfaceMesh.bounds = CaveLayout.WorldBounds;
            meshes.Add(surfaceMesh);
        }

        static float ShellRelief(float theta, float phi, int room)
        {
            return 0.018f
                + 0.016f * (0.5f + 0.5f * Mathf.Sin(theta * 5f + phi * 3f + room))
                + 0.014f * (0.5f + 0.5f * Mathf.Sin(theta * 11f - phi * 7f + Mathf.Sin(phi * 5f)))
                + 0.010f * (0.5f + 0.5f * Mathf.Sin(theta * 19f + phi * 13f + room * 2f));
        }

        void BuildSparseRockLight(PointCloud particles)
        {
            for (int index = 0; index < CaveLayout.Rooms.Length; index++) {
                var room = CaveLayout.Rooms[index];
                for (int i = 0; i < 4800; i++) {
                    float y = R(-1, 1), theta = R(0, Mathf.PI * 2), phi = Mathf.Acos(y);
                    float radial = Mathf.Sqrt(1 - y * y);
                    Vector3 unit = new(radial * Mathf.Cos(theta), y, radial * Mathf.Sin(theta));
                    Vector3 at = room.Center + Vector3.Scale(unit, room.Radius * (1.006f + ShellRelief(theta, phi, index)));
                    if (CaveLayout.InPassage(at, -9)) continue;
                    float patch = Mathf.PerlinNoise((at.x + at.z * 0.31f) * 0.012f + index * 19.7f,
                        (at.y - at.z * 0.23f) * 0.012f);
                    if (patch < 0.70f && R() > 0.025f) continue;
                    Color color = Color.Lerp(room.Color, room.Accent, R(0.18f, 0.62f));
                    particles.Add(at, R(0.07f, 0.13f), color * R(0.035f, 0.095f), R(), FormationLayer);
                }
                // Low-contrast floor flecks keep the seabed legible without a repeated ripple pattern.
                for (int i = 0; i < 2600; i++) {
                    float x = R(-0.83f, 0.83f), z = R(-0.83f, 0.83f);
                    if (x * x + z * z > 0.69f) continue;
                    float floor = -Mathf.Sqrt(1 - x * x - z * z) * room.Radius.y + 4;
                    floor += (Mathf.PerlinNoise(x * 4.4f + index * 7.1f, z * 4.8f) - 0.5f) * 2.4f;
                    Vector3 at = room.Center + new Vector3(x * room.Radius.x, floor, z * room.Radius.z);
                    if (CaveLayout.InPassage(at, -4)) continue;
                    float patch = Mathf.PerlinNoise(x * 12f + index * 13.2f, z * 13.5f);
                    if (patch < 0.56f && R() > 0.06f) continue;
                    Color color = Color.Lerp(room.Color, room.Accent, patch * 0.52f);
                    particles.Add(at, R(0.07f, 0.13f), color * R(0.045f, 0.10f), R(), FormationLayer);
                }
            }
        }

        void BuildPassages(PointCloud particles)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int passage = 0; passage < CaveLayout.Passages.Length; passage++) {
                var route = CaveLayout.Passages[passage];
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
                            Color col = Color.Lerp(CaveLayout.Rooms[CaveLayout.FromRoom(passage)].Color,
                                CaveLayout.Rooms[CaveLayout.ToRoom(passage)].Color, segment / (float)route.Length);
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
                            cols.Add(Color.Lerp(CaveLayout.Rooms[CaveLayout.FromRoom(passage)].Color,
                                CaveLayout.Rooms[CaveLayout.ToRoom(passage)].Color, segment / (float)route.Length) * 0.11f);
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
            tube.bounds = CaveLayout.WorldBounds;
            meshes.Add(tube);
            var obj = new GameObject("Passage tunnel walls");
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = tube;
            obj.AddComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }

        static Vector3 FloorPosition(CaveLayout.Chamber room, float x, float z, float lift = 2f)
        {
            float radiusSquared = Mathf.Clamp(x * x + z * z, 0f, 0.96f);
            float y = -Mathf.Sqrt(1f - radiusSquared) * room.Radius.y + lift;
            return room.Center + new Vector3(x * room.Radius.x, y, z * room.Radius.z);
        }

        static Vector3 PassageDirection(CaveLayout.Chamber room, int roomIndex)
        {
            Vector3 direction = CaveLayout.ForwardWaypoint(room.Center, roomIndex) - room.Center;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        }

        void BuildSeabedEcology(PointCloud particles)
        {
            for (int roomIndex = 0; roomIndex < CaveLayout.Rooms.Length; roomIndex++) {
                var room = CaveLayout.Rooms[roomIndex];
                Vector3 passage = PassageDirection(room, roomIndex);
                float scale = Mathf.Clamp(room.Radius.y / 90f, 0.72f, 1.55f);
                for (int colony = 0; colony < 18; colony++) {
                    float angle = colony * 2.399963f + roomIndex * 0.71f;
                    float radial = R(0.54f, 0.82f);
                    Vector3 horizontal = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    if (Vector3.Dot(horizontal, passage) > 0.56f) continue;
                    Vector3 root = FloorPosition(room, horizontal.x * radial, horizontal.z * radial);
                    if (CaveLayout.InPassage(root, -10f)) continue;
                    int species = (colony + roomIndex) % 3;
                    if (species == 0) BuildPlateCoral(particles, root, room, scale, colony);
                    else if (species == 1) BuildTubeSponges(particles, root, room, scale, colony);
                    else BuildAnemone(particles, root, room, scale, colony);
                }
            }
        }

        void BuildPlateCoral(PointCloud p, Vector3 root, CaveLayout.Chamber room, float scale, int seed)
        {
            float stemHeight = R(5f, 8f) * scale;
            float stemAngle = R(0f, Mathf.PI * 2f);
            Vector3 lean = new(Mathf.Cos(stemAngle), 0f, Mathf.Sin(stemAngle));
            int stemDots = Mathf.CeilToInt(stemHeight * 8f);
            Color stemColor = Color.Lerp(room.Color, room.Accent, 0.16f) * 0.24f;
            for (int i = 0; i < stemDots; i++) {
                float t = i / (float)Mathf.Max(1, stemDots - 1);
                Vector3 at = root + Vector3.up * (stemHeight * t) + lean * (t * t * 0.65f);
                at += new Vector3(Mathf.Cos(i * 2.399f), 0f, Mathf.Sin(i * 2.399f)) * 0.18f;
                p.Add(at, 0.052f, stemColor, R(), FormationLayer);
            }

            int plates = 2 + (seed % 2);
            for (int plate = 0; plate < plates; plate++) {
                float height = stemHeight * (0.48f + plate * 0.24f);
                float radius = R(2.5f, 4.2f) * scale * (1f - plate * 0.12f);
                float phase = R(0f, Mathf.PI * 2f);
                Color color = Color.Lerp(room.Color, room.Accent, 0.22f + plate * 0.10f) * 0.26f;
                const int rim = 44, ribs = 9, ribDots = 13;
                for (int i = 0; i < rim; i++) {
                    float angle = i * Mathf.PI * 2f / rim;
                    float lobe = 1f + 0.08f * Mathf.Sin(angle * 5f + phase);
                    Vector3 at = root + Vector3.up * height + lean * (height / stemHeight * 0.65f);
                    at += new Vector3(Mathf.Cos(angle) * radius * lobe, 0f, Mathf.Sin(angle) * radius * lobe);
                    p.Add(at, 0.075f, color * R(0.78f, 1.08f), R(), FormationLayer);
                }
                for (int ray = 0; ray < ribs; ray++) {
                    float angle = phase + ray * Mathf.PI * 2f / ribs;
                    for (int j = 1; j < ribDots; j++) {
                        float t = j / (float)ribDots;
                        float r = radius * t;
                        Vector3 at = root + Vector3.up * (height + Mathf.Sin(t * Mathf.PI) * 0.18f);
                        at += lean * (height / stemHeight * 0.65f);
                        at += new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                        p.Add(at, 0.052f, color * 0.82f, R(), FormationLayer);
                    }
                }
            }
        }

        void BuildTubeSponges(PointCloud p, Vector3 root, CaveLayout.Chamber room, float scale, int seed)
        {
            int tubes = 3 + seed % 3;
            float groupAngle = R(0f, Mathf.PI * 2f);
            for (int tube = 0; tube < tubes; tube++) {
                float around = groupAngle + tube * Mathf.PI * 2f / tubes;
                float groupRadius = (0.45f + (tube % 2) * 1.15f) * scale;
                Vector3 baseAt = root + new Vector3(Mathf.Cos(around) * groupRadius, 0f, Mathf.Sin(around) * groupRadius);
                float height = R(6f, 11f) * scale;
                float radius = R(0.62f, 0.95f) * scale;
                int rows = Mathf.CeilToInt(height / 0.72f);
                const int sides = 18;
                Color color = Color.Lerp(room.Color, room.Accent, 0.30f + R(0f, 0.14f)) * 0.25f;
                for (int row = 0; row <= rows; row++) {
                    float t = row / (float)rows;
                    float y = height * t;
                    for (int side = 0; side < sides; side++) {
                        // Alternating openings leave a visibly hollow, perforated tube wall.
                        if (row > 1 && row < rows - 1 && side % 6 >= 2 && side % 6 <= 3 && row % 4 >= 1 && row % 4 <= 2)
                            continue;
                        float angle = side * Mathf.PI * 2f / sides;
                        float waviness = 1f + 0.06f * Mathf.Sin(angle * 3f + t * 5f + seed);
                        Vector3 at = baseAt + Vector3.up * y + new Vector3(Mathf.Cos(angle) * radius * waviness, 0f,
                            Mathf.Sin(angle) * radius * waviness);
                        p.Add(at, 0.062f, Color.Lerp(color, room.Accent * 0.22f, t * 0.34f), R(), FormationLayer);
                        if (row == rows) {
                            Vector3 innerLip = baseAt + Vector3.up * (height - 0.16f) +
                                new Vector3(Mathf.Cos(angle) * radius * 0.68f, 0f, Mathf.Sin(angle) * radius * 0.68f);
                            p.Add(innerLip, 0.05f, color * 0.82f, R(), FormationLayer);
                        }
                    }
                }
            }
        }

        void BuildAnemone(PointCloud p, Vector3 root, CaveLayout.Chamber room, float scale, int seed)
        {
            float phase = R(0f, Mathf.PI * 2f);
            Color baseColor = Color.Lerp(room.Color, room.Accent, 0.18f) * 0.20f;
            for (int i = 0; i < 52; i++) {
                float angle = i * 2.399f + phase;
                float radius = Mathf.Sqrt((i + 0.5f) / 52f) * 1.35f * scale;
                Vector3 at = root + new Vector3(Mathf.Cos(angle) * radius, 0.12f + radius * 0.22f,
                    Mathf.Sin(angle) * radius);
                p.Add(at, 0.055f, baseColor, R(), FormationLayer);
            }
            int tentacles = 15 + seed % 6;
            for (int tentacle = 0; tentacle < tentacles; tentacle++) {
                float angle = phase + tentacle * 2.399f;
                float length = R(6f, 11f) * scale;
                float sway = R(0.8f, 2f) * scale;
                int dots = Mathf.CeilToInt(length * 3.4f);
                Color tip = Color.Lerp(room.Color, room.Accent, 0.32f) * 0.34f;
                for (int j = 0; j < dots; j++) {
                    float t = j / (float)dots;
                    float curl = Mathf.Sin(t * 3.1f + angle) * sway * t;
                    Vector3 at = root + Vector3.up * (length * t) +
                        new Vector3(Mathf.Cos(angle) * (0.22f + curl), 0f, Mathf.Sin(angle) * (0.22f + curl));
                    Color color = Color.Lerp(baseColor, tip, t * 0.82f);
                    p.Add(at, Mathf.Lerp(0.047f, 0.065f, t), color, R(), FormationLayer);
                }
            }
        }

        void BuildSuspendedMatter(PointCloud particles)
        {
            const int perRoom = 2400;
            for (int roomIndex = 0; roomIndex < CaveLayout.Rooms.Length; roomIndex++) {
                var room = CaveLayout.Rooms[roomIndex];
                for (int i = 0; i < perRoom; i++) {
                    float y = R(-1f, 1f), angle = R(0f, Mathf.PI * 2f);
                    float radius = Mathf.Pow(R(), 1f / 3f) * 0.92f;
                    float planar = Mathf.Sqrt(1f - y * y) * radius;
                    Vector3 normalized = new(Mathf.Cos(angle) * planar, y * radius, Mathf.Sin(angle) * planar);
                    Vector3 at = room.Center + Vector3.Scale(room.Radius, normalized);
                    float glint = R();
                    float energy = glint > 0.997f ? R(0.20f, 0.36f) : R(0.018f, 0.065f);
                    Color tint = Color.Lerp(room.Color, room.Accent, R(0.08f, glint > 0.97f ? 0.75f : 0.34f));
                    particles.Add(at, R(0.021f, glint > 0.98f ? 0.064f : 0.043f), tint * energy, R(), VolumeLayer);
                }
            }
            BuildSiphonophoreColonies(particles);
        }

        void BuildSiphonophoreColonies(PointCloud particles)
        {
            const int colonyCount = 10;
            CaveLayout.Chamber room = CaveLayout.Rooms[2];
            Vector3 arrivalOrigin = new(room.Center.x, CaveLayout.HorizonSurfaceY - 28f, room.Center.z - 260f);
            for (int colony = 0; colony < colonyCount; colony++) {
                float angle = Mathf.Lerp(0.8f, Mathf.PI * 2f - 0.8f, colony / (float)(colonyCount - 1));
                float orbitOffset = colony == 0 || colony == colonyCount - 1 ? 98f : (colony & 1) == 0 ? 82f : -78f;
                float xRadius = 330f + orbitOffset;
                float zRadius = 400f + orbitOffset;
                float whaleY = -35f + 115f * Mathf.Sin(angle * 2f + 0.35f);
                Vector3 anchor = room.Center + new Vector3(xRadius * Mathf.Sin(angle), whaleY + R(-12f, 12f),
                    -zRadius * Mathf.Cos(angle));
                if ((anchor - arrivalOrigin).sqrMagnitude < 250f * 250f) continue;
                Vector3 tangent = new(xRadius * Mathf.Cos(angle), 230f * Mathf.Cos(angle * 2f + 0.35f),
                    zRadius * Mathf.Sin(angle));
                tangent.Normalize();
                Vector3 radial = new(Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                BuildSiphonophore(particles, anchor, tangent, radial);
            }
        }

        void BuildSiphonophore(PointCloud particles, Vector3 anchor, Vector3 tangent, Vector3 radial)
        {
            float phase = R(0f, Mathf.PI * 2f);
            float turns = R(1.18f, 1.48f);
            float coilRadius = R(20f, 27f);
            float axisLength = R(24f, 34f);
            Color bellTint = Color.Lerp(new Color(0.20f, 0.76f, 0.96f), new Color(0.78f, 0.96f, 1f), R(0.24f, 0.72f));
            Color veilTint = Color.Lerp(bellTint, new Color(0.40f, 0.92f, 0.94f), 0.42f);

            Vector3 CoilPoint(float t)
            {
                float angle = phase + t * Mathf.PI * 2f * turns;
                float radius = Mathf.Lerp(4f, coilRadius, Mathf.SmoothStep(0f, 1f, t));
                float rise = Mathf.Sin(angle) * radius * 0.58f;
                return anchor + radial * (Mathf.Cos(angle) * radius) + Vector3.up * rise +
                    tangent * ((t - 0.5f) * axisLength + Mathf.Sin(angle * 0.55f + phase) * 3.5f);
            }

            const int chainSamples = 420;
            for (int sample = 0; sample < chainSamples; sample++) {
                float t = sample / (float)(chainSamples - 1);
                particles.Add(CoilPoint(t), 0.16f, veilTint * 0.055f, R(), MarineLayer);
            }

            const int bellCount = 24;
            for (int bell = 0; bell < bellCount; bell++) {
                float t = (bell + 0.5f) / bellCount + R(-0.004f, 0.004f);
                Vector3 at = CoilPoint(t);
                Vector3 bellTangent = (CoilPoint(Mathf.Min(1f, t + 0.006f)) -
                    CoilPoint(Mathf.Max(0f, t - 0.006f))).normalized;
                Vector3 across = Vector3.Cross(bellTangent, Vector3.up).normalized;
                if (across.sqrMagnitude < 0.1f) across = radial;
                Vector3 other = Vector3.Cross(bellTangent, across).normalized;
                Color bellColor = bellTint * R(0.16f, 0.27f);
                particles.Add(at, R(0.44f, 0.72f), bellColor, R(), MarineLayer);
                for (int ring = 0; ring < 4; ring++) {
                    float ringAngle = ring * Mathf.PI * 0.5f + phase;
                    Vector3 point = at + (across * Mathf.Cos(ringAngle) + other * Mathf.Sin(ringAngle)) * 0.92f;
                    particles.Add(point, 0.34f, bellColor * 0.72f, R(), MarineLayer);
                }
            }

            const int tentacleCount = 8;
            const int tentacleSamples = 112;
            for (int tentacle = 0; tentacle < tentacleCount; tentacle++) {
                float rootT = 0.13f + tentacle * 0.105f + R(-0.025f, 0.025f);
                Vector3 root = CoilPoint(rootT);
                float directionAngle = phase + tentacle * 2.399963f + R(-0.3f, 0.3f);
                Vector3 side = (radial * Mathf.Cos(directionAngle) + tangent * Mathf.Sin(directionAngle)).normalized;
                float length = R(22f, 43f);
                float curl = R(2.5f, 6.5f);
                float threadPhase = R(0f, Mathf.PI * 2f);
                float brightness = R(0.025f, 0.075f);
                for (int sample = 0; sample < tentacleSamples; sample++) {
                    float t = sample / (float)(tentacleSamples - 1);
                    float sway = Mathf.Sin(t * 4.1f + threadPhase) * curl * t +
                        Mathf.Sin(t * 8.7f + threadPhase * 1.7f) * curl * 0.22f * t;
                    Vector3 at = root + Vector3.down * (length * t) + side * sway +
                        tangent * (Mathf.Sin(t * 2.8f + threadPhase) * 2.2f * t);
                    Color color = veilTint * (brightness * Mathf.Lerp(0.52f, 1f, Mathf.Sin(t * Mathf.PI) * 0.5f + 0.5f));
                    particles.Add(at, Mathf.Lerp(0.075f, 0.21f, t), color, R(), MarineLayer);
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
                Color reef = Color.Lerp(room.Color, room.Accent, 0.25f);
                BuildFan(p, room, reef, roomIndex);
                BuildKelp(p, room, reef, roomIndex);
            }
        }

        void BuildFan(PointCloud p, CaveLayout.Chamber room, Color color, int roomIndex)
        {
            Vector3 passage = PassageDirection(room, roomIndex);
            float scale = Mathf.Clamp(room.Radius.y / 90f, 0.72f, 1.55f);
            int built = 0;
            for (int candidate = 0; candidate < 12 && built < 2; candidate++) {
                float angle = candidate * 2.399963f + roomIndex * 1.31f;
                Vector3 horizontal = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Vector3.Dot(horizontal, passage) > 0.50f) continue;
                float radial = 0.64f + (candidate % 3) * 0.045f;
                Vector3 root = FloorPosition(room, horizontal.x * radial, horizontal.z * radial, 3f);
                if (CaveLayout.InPassage(root, -12f)) continue;
                float fanAngle = angle + Mathf.PI * 0.5f;
                for (int ray = -7; ray <= 7; ray++) {
                    Vector3 direction = new(Mathf.Cos(fanAngle + ray * 0.085f), 0f, Mathf.Sin(fanAngle + ray * 0.085f));
                    float length = (16f + (7 - Mathf.Abs(ray)) * 0.75f) * scale;
                    int dots = Mathf.CeilToInt(length * 3f);
                    for (int j = 0; j < dots; j++) {
                        float f = j / (float)dots;
                        Vector3 at = root + Vector3.up * (length * f) + direction * (f * (3f + (7 - Mathf.Abs(ray)) * 0.7f) * scale);
                        at += Vector3.up * Mathf.Sin(f * Mathf.PI) * 1.4f * scale;
                        Color tint = Color.Lerp(color, room.Accent, 0.22f + f * 0.28f) * 0.25f;
                        p.Add(at, 0.047f + (1f - f) * 0.018f, tint * R(0.82f, 1.08f), R(), FormationLayer);
                    }
                }
                built++;
            }
        }

        void BuildKelp(PointCloud p, CaveLayout.Chamber room, Color color, int roomIndex)
        {
            Vector3 passage = PassageDirection(room, roomIndex);
            float scale = Mathf.Clamp(room.Radius.y / 90f, 0.72f, 1.55f);
            int clumps = 10;
            for (int clump = 0; clump < clumps; clump++) {
                float angle = clump * 2.399963f + roomIndex * 0.93f;
                Vector3 horizontal = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Vector3.Dot(horizontal, passage) > 0.54f) continue;
                float radial = R(0.55f, 0.78f);
                Vector3 root = FloorPosition(room, horizontal.x * radial, horizontal.z * radial, 2.5f);
                if (CaveLayout.InPassage(root, -12f)) continue;
                float rootAngle = R(0f, Mathf.PI * 2f);
                int blades = 3 + clump % 2;
                for (int bladeIndex = 0; bladeIndex < blades; bladeIndex++) {
                    float angleAtRoot = rootAngle + bladeIndex * Mathf.PI * 2f / blades;
                    float length = R(20f, 32f) * scale;
                    int rows = 56;
                    for (int row = 0; row < rows; row++) {
                        float f = row / (float)(rows - 1);
                        float sway = Mathf.Sin(f * 3.8f + angleAtRoot) * (1.4f + f * 5.4f) * scale;
                        Vector3 center = root + Vector3.up * (length * f) +
                            new Vector3(Mathf.Cos(angleAtRoot) * sway, 0f, Mathf.Sin(angleAtRoot) * sway);
                        float width = (0.12f + Mathf.Sin(f * Mathf.PI) * 0.78f) * scale;
                        for (int edge = -1; edge <= 1; edge++) {
                            Vector3 across = new(-Mathf.Sin(angleAtRoot), 0f, Mathf.Cos(angleAtRoot));
                            Vector3 at = center + across * (edge * width * 0.5f);
                            Color bladeColor = Color.Lerp(color * 0.72f, room.Accent, 0.10f + f * 0.18f) * 0.22f;
                            p.Add(at, edge == 0 ? 0.054f : 0.043f, bladeColor * R(0.82f, 1.08f), R(), FormationLayer);
                        }
                    }
                }
            }
        }

        public void Tick(float song, float dt, Vector3 player)
        {
            clock = song;
            float pulse = Mathf.Clamp01(Score.Pulse(song));
            EvaluateReveal(song, out float reveal, out float sweep, out float bind);
            surfaceMaterial.SetFloat(PulseId, pulse);
            surfaceMaterial.SetFloat(CaveSongId, song);
            surfaceMaterial.SetFloat(CaveRevealId, reveal);
            surfaceMaterial.SetFloat(CaveSweepId, sweep);
            surfaceMaterial.SetFloat(CaveBindId, bind);
            surfaceMaterial.SetVector(CavePlayerPositionId, player);
            cavernMatterMaterial.SetFloat(CaveSongId, song);
            cavernMatterMaterial.SetFloat(CaveRevealId, reveal);
            cavernMatterMaterial.SetFloat(CaveSweepId, sweep);
            cavernMatterMaterial.SetFloat(CaveBindId, bind);
            cavernMatterMaterial.SetVector(CavePlayerPositionId, player);
            for (int i = 0; i < LightSlots; i++) {
                float remaining = Mathf.Clamp01((lightUntil[i] - song) / LightLifetime);
                lights[i].w = lightStrength[i] * remaining;
                if (remaining <= 0f) { lights[i] = Vector4.zero; lightStrength[i] = 0f; }
            }
            if (!marineLife) marineLife = world.GetComponent<Experience>()?.Marine;
            if (world.Serpent)
                SetOrganismLight(LightSlots, Anatomy.Head(song), 0.78f, RoomTint(1), 145f);
            else lights[LightSlots] = Vector4.zero;
            float whaleVisibility = marineLife ? Mathf.Clamp01(marineLife.WhaleVisibility) : 0f;
            if (marineLife && marineLife.WhaleVisible && whaleVisibility > 0.001f)
                SetOrganismLight(LightSlots + 1, marineLife.WhalePosition,
                    (marineLife.WhaleReleased ? 1.15f : 0.72f) * whaleVisibility, marineLife.WhaleLightColor, 300f);
            else {
                lights[LightSlots + 1] = Vector4.zero;
                lightColors[LightSlots + 1] = Vector4.zero;
            }
            surfaceMaterial.SetVectorArray(LightArrayId, lights);
            surfaceMaterial.SetVectorArray(LightColorsId, lightColors);
            cavernMatterMaterial.SetVectorArray(LightArrayId, lights);
            cavernMatterMaterial.SetVectorArray(LightColorsId, lightColors);
        }

        public void Illuminate(Vector3 position, float strength)
        {
            Illuminate(position, strength, RoomTint(CaveLayout.NearestRoom(position)));
        }

        public void Illuminate(Vector3 position, float strength, Color color)
        {
            int slot = lightCursor++ % LightSlots;
            lightStrength[slot] = Mathf.Clamp(strength, 0f, 8f);
            lights[slot] = new Vector4(position.x, position.y, position.z, lightStrength[slot]);
            float radius = Mathf.Clamp(CaveLayout.Rooms[CaveLayout.NearestRoom(position)].Radius.y * 0.8f, 90f, 240f);
            lightColors[slot] = new Vector4(color.r, color.g, color.b, radius);
            lightUntil[slot] = clock + LightLifetime;
            surfaceMaterial.SetVectorArray(LightArrayId, lights);
            surfaceMaterial.SetVectorArray(LightColorsId, lightColors);
            cavernMatterMaterial.SetVectorArray(LightArrayId, lights);
            cavernMatterMaterial.SetVectorArray(LightColorsId, lightColors);
            particleMaterial.SetVector("_CaveWave", new Vector4(position.x, position.y, position.z, clock));
            particleMaterial.SetFloat("_CaveWaveEnergy", Mathf.Clamp(strength, 0f, 4f));
            cavernMatterMaterial.SetVector("_CaveWave", new Vector4(position.x, position.y, position.z, clock));
            cavernMatterMaterial.SetFloat("_CaveWaveEnergy", Mathf.Clamp(strength, 0f, 4f));
        }

        public void ResetLighting()
        {
            Array.Clear(lights, 0, lights.Length);
            Array.Clear(lightColors, 0, lightColors.Length);
            Array.Clear(lightUntil, 0, lightUntil.Length);
            Array.Clear(lightStrength, 0, lightStrength.Length);
            if (surfaceMaterial) surfaceMaterial.SetVectorArray(LightArrayId, lights);
            if (surfaceMaterial) surfaceMaterial.SetVectorArray(LightColorsId, lightColors);
            if (cavernMatterMaterial) cavernMatterMaterial.SetVectorArray(LightArrayId, lights);
            if (cavernMatterMaterial) cavernMatterMaterial.SetVectorArray(LightColorsId, lightColors);
            if (particleMaterial) particleMaterial.SetFloat("_CaveWaveEnergy", 0f);
            if (cavernMatterMaterial) cavernMatterMaterial.SetFloat("_CaveWaveEnergy", 0f);
        }

        static Color RoomTint(int roomIndex)
        {
            var room = CaveLayout.Rooms[Mathf.Clamp(roomIndex, 0, CaveLayout.Rooms.Length - 1)];
            return Color.Lerp(room.Color, room.Accent, roomIndex == 0 ? 0.22f : 0.68f);
        }

        void SetOrganismLight(int slot, Vector3 position, float strength, Color color, float radius)
        {
            lights[slot] = new Vector4(position.x, position.y, position.z, strength);
            lightColors[slot] = new Vector4(color.r, color.g, color.b, radius);
        }

        static void EvaluateReveal(float song, out float reveal, out float sweep, out float bind)
        {
            AuthoredScore.Timeline score = AuthoredScore.Data;
            double localSample = AuthoredScore.LocalSample(song);
            int beatIndex = AuthoredScore.Previous(score.beats, localSample);
            int startIndex = beatIndex - beatIndex % 3;
            int startSample = score.beats[startIndex];
            double elapsed = Math.Max(0, localSample - startSample) / score.sampleRate;
            int nextIndex = startIndex + 3;
            double period = nextIndex < score.beats.Length
                ? (score.beats[nextIndex] - startSample) / (double)score.sampleRate
                : (score.sampleCount - startSample + score.beats[0]) / (double)score.sampleRate;
            float periodSeconds = Mathf.Max(0.1f, (float)period);
            float age = (float)elapsed;
            reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.015f, 0.17f, age)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(periodSeconds * 0.46f, periodSeconds * 0.92f, age)));
            sweep = Mathf.Clamp01(age / periodSeconds);
            bind = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.46f, age));
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
