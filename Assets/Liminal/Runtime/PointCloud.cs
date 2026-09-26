using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class PointCloud
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector4> uv = new();
        readonly List<Vector2> data = new();
        readonly List<Color> colors = new();
        readonly List<int> indices = new();
        static readonly Vector2[] Corners = { new(-1,-1), new(-1,1), new(1,1), new(1,-1) };
        public int Count => vertices.Count / 4;
        public void Add(Vector3 center, float size, Color color, float seed = 0, float motion = 0)
        {
            int start = vertices.Count;
            for (int j = 0; j < 4; j++) {
                vertices.Add(center); colors.Add(color);
                uv.Add(new Vector4(Corners[j].x, Corners[j].y, size, 0));
                data.Add(new Vector2(seed, motion));
            }
            indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            indices.Add(start); indices.Add(start+2); indices.Add(start+3);
        }
        public Mesh Build(string name, float bounds = 600)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, data);
            mesh.SetTriangles(indices, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * bounds);
            mesh.UploadMeshData(true);
            return mesh;
        }
        public static GameObject Place(string name, Mesh mesh, Material material, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj;
        }
    }
}
