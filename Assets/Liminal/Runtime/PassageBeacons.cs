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
        bool initialized;
        int particleCount;

        public int ParticleCount => particleCount;
        public int PortalCount => CaveLayout.Passages.Length * 2;

        public void Initialize()
        {
            if (initialized) return;
            Shader shader = Resources.Load<Shader>("PassageBeacon");
            if (!shader) { Debug.LogError("Missing Resources/PassageBeacon shader.", this); return; }

            var arcs = new PointCloud();
            BuildArcs(arcs);
            arcMesh = arcs.Build("Passage beacon arcs");
            currentMesh = BuildCurrents(out int currentCount);
            Bounds bounds = CaveLayout.WorldBounds;
            bounds.Expand(160f);
            arcMesh.bounds = currentMesh.bounds = bounds;
            arcMaterial = MakeMaterial(shader, false);
            currentMaterial = MakeMaterial(shader, true);
            PointCloud.Place("Passage beacon arcs", arcMesh, arcMaterial, transform);
            PointCloud.Place("Passage currents", currentMesh, currentMaterial, transform);
            particleCount = arcs.Count + currentCount;
            initialized = true;
            Tick(0f);
        }

        public void Tick(float song)
        {
            if (!initialized) return;
            float beat = (float)AuthoredScore.BeatPosition(song);
            arcMaterial.SetFloat(BeatId, beat);
            currentMaterial.SetFloat(BeatId, beat);
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

        static void BuildArcs(PointCloud arcs)
        {
            for (int passage = 0; passage < CaveLayout.Passages.Length; passage++)
            for (int end = 0; end < 2; end++)
            {
                bool forward = end == 0;
                CaveLayout.GetPortal(passage, forward, out Vector3 mouth, out Vector3 direction);
                Vector3 axis = direction.normalized;
                Vector3 right = Vector3.Cross(axis, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(axis, Vector3.right).normalized;
                Vector3 up = Vector3.Cross(right, axis).normalized;
                for (int arc = 0; arc < 3; arc++)
                {
                    float radius = 27.5f + arc * 2.25f;
                    float start = 0.22f + arc * 2.16f + (forward ? passage * 0.13f : 0.57f);
                    const float sweep = 4.55f;
                    int samples = Mathf.CeilToInt(radius * sweep / 1.85f);
                    for (int i = 0; i <= samples; i++)
                    {
                        float t = i / (float)samples;
                        float angle = start + sweep * t;
                        float seed = Hash(passage * 97 + end * 29 + arc * 11 + i, 7);
                        float wobble = 1f + 0.018f * Mathf.Sin(angle * 3f + arc * 1.7f + passage);
                        Vector3 at = mouth + right * (Mathf.Cos(angle) * radius * wobble) +
                                     up * (Mathf.Sin(angle) * radius * (1f + 0.012f * Mathf.Sin(angle * 2f))) +
                                     axis * (0.35f * Mathf.Sin(angle * 2f + arc));
                        bool gold = forward && i % 19 == (arc * 5 + 3) % 19;
                        Color tint = gold ? new Color(1f, 0.58f, 0.2f) :
                            forward ? new Color(0.28f, 0.96f, 0.86f) : new Color(0.19f, 0.62f, 0.56f);
                        arcs.Add(at, 0f, tint * (0.86f + 0.14f * Hash(i, passage + end)), seed, gold ? 1f : 0f);
                    }
                }
            }
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
