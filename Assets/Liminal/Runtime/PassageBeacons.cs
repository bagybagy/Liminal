using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    [DisallowMultipleComponent]
    public sealed class PassageBeacons : MonoBehaviour
    {
        static readonly Vector2[] Corners = { new(-1,-1), new(-1,1), new(1,1), new(1,-1) };
        static readonly int BeatId = Shader.PropertyToID("_BeatPosition");
        static readonly int FlowId = Shader.PropertyToID("_FlowEnabled");
        static readonly int GainId = Shader.PropertyToID("_Gain");
        Mesh arcMesh, currentMesh;
        Material arcMaterial, currentMaterial;
        CorridorShoals shoals;
        Camera playerCamera;
        bool initialized;
        int particleCount;

        public int ParticleCount => particleCount;
        public int PortalCount => CaveLayout.Passages.Length * 2;
        public int ShoalCount => shoals ? shoals.ShoalCount : 0;
        public int FishCount => shoals ? shoals.FishCount : 0;
        public IReadOnlyList<Vector3> ShoalPositions => shoals ? shoals.Positions : System.Array.Empty<Vector3>();

        public void ResetShoals()
        {
            if (shoals) shoals.ResetShoals();
        }

        public void Initialize(ParticleWorld world = null, Encounter combat = null)
        {
            if (!initialized)
            {
                Shader shader = Resources.Load<Shader>("PassageBeacon");
                if (!shader) { Debug.LogError("Missing Resources/PassageBeacon shader.", this); return; }

                arcMesh = BuildWreaths(out int wreathCount);
                currentMesh = BuildCurrents(out int currentCount);
                Bounds bounds = CaveLayout.WorldBounds;
                bounds.Expand(160f);
                arcMesh.bounds = currentMesh.bounds = bounds;
                arcMaterial = MakeMaterial(shader, false);
                currentMaterial = MakeMaterial(shader, true);
                PointCloud.Place("Passage beacon wreaths", arcMesh, arcMaterial, transform);
                PointCloud.Place("Passage currents", currentMesh, currentMaterial, transform);
                particleCount = wreathCount + currentCount;
                initialized = true;
                Tick(0f);
            }

            if (world)
            {
                if (!shoals) shoals = gameObject.AddComponent<CorridorShoals>();
                shoals.Initialize(world, combat);
                if (!playerCamera) playerCamera = Camera.main;
            }
        }

        public void Tick(float song)
        {
            if (!initialized) return;
            float beat = (float)AuthoredScore.BeatPosition(song);
            arcMaterial.SetFloat(BeatId, beat);
            currentMaterial.SetFloat(BeatId, beat);
            if (shoals)
            {
                if (!playerCamera) playerCamera = Camera.main;
                if (playerCamera) shoals.Tick(song, Time.deltaTime, playerCamera.transform.position, true);
                else shoals.Tick(song, Time.deltaTime, Vector3.zero, false);
            }
        }

        void OnDestroy()
        {
            if (arcMesh) Destroy(arcMesh);
            if (currentMesh) Destroy(currentMesh);
            if (arcMaterial) Destroy(arcMaterial);
            if (currentMaterial) Destroy(currentMaterial);
        }

        static Material MakeMaterial(Shader shader, bool flow)
        {
            var material = new Material(shader);
            material.name = flow ? "Passage current material" : "Passage beacon material";
            material.SetFloat(FlowId, flow ? 1f : 0f);
            material.SetFloat(GainId, 2.8f);
            return material;
        }

        static Mesh BuildWreaths(out int count)
        {
            var cloud = new WreathCloud();
            for (int passage = 0; passage < CaveLayout.Passages.Length; passage++)
            for (int end = 0; end < 2; end++)
            {
                bool forward = end == 0;
                CaveLayout.GetPortal(passage, forward, out Vector3 mouth, out Vector3 direction);
                Vector3 axis = direction.normalized;
                Vector3 right = Vector3.Cross(axis, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(axis, Vector3.right).normalized;
                Vector3 up = Vector3.Cross(right, axis).normalized;
                const float meanRadius = 30f;
                int samples = Mathf.CeilToInt(2f * Mathf.PI * meanRadius / 1.12f);
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    float seed = Hash(passage * 991 + end * 173 + i, 53);
                    float radius = meanRadius + 0.92f * Mathf.Sin(angle * 3f + passage * 0.8f + end) +
                        0.44f * Mathf.Sin(angle * 7f - passage * 0.61f) + (seed - 0.5f) * 0.26f;
                    bool pearl = i % 67 == (passage * 13 + end * 7) % 67;
                    Color tint = pearl ? new Color(0.72f, 0.92f, 0.78f) :
                        forward ? new Color(0.20f, 0.80f, 0.70f) : new Color(0.16f, 0.65f, 0.62f);
                    float size = 0.62f + seed * 0.38f;
                    cloud.Add(mouth, right, up, angle, radius, seed, size, tint * (0.82f + seed * 0.22f));
                }

                for (int tuft = 0; tuft < 5; tuft++)
                {
                    float anchor = (tuft + 0.23f * (passage + end)) * Mathf.PI * 2f / 5f;
                    float seed = Hash(passage * 71 + end * 19 + tuft, 101);
                    for (int sample = 0; sample < 9; sample++)
                    {
                        float t = sample / 8f;
                        float angle = anchor + (t - 0.5f) * (0.23f + 0.06f * seed) +
                            Mathf.Sin(t * Mathf.PI) * 0.055f * Mathf.Sin(seed * 6.283f);
                        float radius = meanRadius + 0.8f + t * (1.4f + seed * 1.8f) +
                            0.35f * Mathf.Sin(angle * 5f + seed * 6f);
                        float fleck = Hash(tuft * 31 + sample, passage * 17 + end);
                        Color tint = Color.Lerp(new Color(0.14f, 0.66f, 0.61f),
                            new Color(0.52f, 0.82f, 0.65f), fleck * 0.55f);
                        cloud.Add(mouth, right, up, angle, radius, seed + t * 0.19f,
                            0.34f + fleck * 0.30f, tint * (0.46f + 0.20f * (1f - t)));
                    }
                }
            }
            count = cloud.Count;
            return cloud.Build();
        }

        static Mesh BuildCurrents(out int count)
        {
            var cloud = new CurrentCloud();
            const int samples = 180;
            for (int passage = 0; passage < CaveLayout.Passages.Length; passage++)
            {
                CaveLayout.GetPortal(passage, true, out Vector3 source, out _);
                CaveLayout.GetPortal(passage, false, out Vector3 destination, out _);
                Vector3[] route = CaveLayout.Passages[passage];
                Vector3[] path = {
                    Vector3.Lerp(CaveLayout.Rooms[passage].Center, source, 0.5f),
                    source, route[1], route[2], destination
                };
                for (int i = 0; i < samples; i++)
                {
                    float seed = (i + 0.5f) / samples;
                    bool guide = i % 47 == passage * 7 % 47;
                    Color tint = guide ? new Color(0.92f, 0.62f, 0.24f) : new Color(0.32f, 0.92f, 0.82f);
                    cloud.Add(path, seed, 0.0065f + 0.0015f * Hash(i, passage),
                        Hash(i, passage + 19) * Mathf.PI * 2f, 1.3f + 2.1f * Hash(i, passage + 41), tint);
                }
            }
            count = cloud.Count;
            return cloud.Build();
        }

        static float Hash(int x, int salt) => Mathf.Repeat(Mathf.Sin(x * 127.1f + salt * 311.7f) * 43758.5453f, 1f);

        sealed class WreathCloud
        {
            readonly List<Vector3> vertices = new();
            readonly List<Vector4> uv0 = new(), data = new();
            readonly List<Vector4>[] frame = { new(), new(), new() };
            readonly List<Color> colors = new();
            readonly List<int> triangles = new();
            public int Count => vertices.Count / 4;

            public void Add(Vector3 center, Vector3 right, Vector3 up, float angle, float radius,
                float seed, float size, Color tint)
            {
                int first = vertices.Count;
                Vector3 position = center + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
                Vector4 c = new(center.x, center.y, center.z, 0f);
                Vector4 r = new(right.x, right.y, right.z, 0f);
                Vector4 u = new(up.x, up.y, up.z, 0f);
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices.Add(position);
                    uv0.Add(new Vector4(Corners[corner].x, Corners[corner].y, size, seed));
                    data.Add(new Vector4(angle, radius, seed, Mathf.Clamp01(size)));
                    colors.Add(tint);
                    frame[0].Add(c); frame[1].Add(r); frame[2].Add(u);
                }
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }

            public Mesh Build()
            {
                var mesh = new Mesh { name = "Suspended passage wreaths", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv0); mesh.SetUVs(1, data);
                for (int i = 0; i < frame.Length; i++) mesh.SetUVs(i + 2, frame[i]);
                mesh.SetTriangles(triangles, 0); mesh.UploadMeshData(true);
                return mesh;
            }
        }

        sealed class CurrentCloud
        {
            readonly List<Vector3> vertices = new();
            readonly List<Vector4> uv0 = new(), data = new();
            readonly List<Vector4>[] knots = { new(), new(), new(), new(), new() };
            readonly List<Color> colors = new();
            readonly List<int> triangles = new();
            public int Count => vertices.Count / 4;

            public void Add(Vector3[] path, float seed, float rate, float phase, float offset, Color tint)
            {
                int first = vertices.Count;
                Vector4[] p = {
                    new(path[0].x,path[0].y,path[0].z,0), new(path[1].x,path[1].y,path[1].z,0),
                    new(path[2].x,path[2].y,path[2].z,0), new(path[3].x,path[3].y,path[3].z,0),
                    new(path[4].x,path[4].y,path[4].z,0)
                };
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices.Add(path[0]); uv0.Add(new Vector4(Corners[corner].x, Corners[corner].y, 0f, 0f));
                    data.Add(new Vector4(seed, rate, phase, offset)); colors.Add(tint);
                    for (int k = 0; k < 5; k++) knots[k].Add(p[k]);
                }
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }

            public Mesh Build()
            {
                var mesh = new Mesh { name = "Passage currents", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv0); mesh.SetUVs(1, data);
                for (int k = 0; k < 5; k++) mesh.SetUVs(k + 2, knots[k]);
                mesh.SetTriangles(triangles, 0); mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
