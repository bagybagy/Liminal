using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class LeviathanVfx : MonoBehaviour
    {
        public const int SimulatedParticles = 262144;
        readonly Vector4[] spineSamples = new Vector4[Anatomy.SpineSamples];
        GraphicsBuffer particles, spine;
        ComputeShader simulation;
        Material lightMaterial, skinMaterial;
        Mesh skinMesh;
        int initializeKernel, simulateKernel;
        float previousSong = -1;
        public int SimulationSteps { get; private set; }
        public bool Ready => particles != null;

        public void Initialize(ComputeShader compute, Material light, Material membrane)
        {
            if (!SystemInfo.supportsComputeShaders || !compute || !light || !membrane)
                throw new InvalidOperationException("Leviathan VFX requires compute shader support and production materials.");
            simulation = Instantiate(compute);
            lightMaterial = new Material(light);
            skinMaterial = new Material(membrane);
            particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SimulatedParticles, 64);
            spine = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Anatomy.SpineSamples, 16);
            initializeKernel = simulation.FindKernel("Initialize");
            simulateKernel = simulation.FindKernel("Simulate");
            foreach (int kernel in new[] { initializeKernel, simulateKernel }) {
                simulation.SetBuffer(kernel, "_Particles", particles);
                simulation.SetBuffer(kernel, "_Spine", spine);
            }
            lightMaterial.SetBuffer("_Particles", particles);
            lightMaterial.SetBuffer("_Spine", spine);
            skinMaterial.SetBuffer("_Spine", spine);
            simulation.SetInt("_Count", SimulatedParticles);
            skinMesh = CreateMembrane();
            PointCloud.Place("Leviathan / translucent living membrane", skinMesh, skinMaterial, transform);
            Tick(0, 0, 0);
            RenderPipelineManager.beginCameraRendering += Render;
        }

        public void Tick(float song, float evolution, float dissolve)
        {
            if (!Ready) return;
            float delta = song - previousSong;
            bool reset = previousSong < 0 || delta < -0.001f || delta > 0.5f;
            if (!reset && delta < 1f / 120f) return;
            Anatomy.WriteSpine(spineSamples, song);
            spine.SetData(spineSamples);
            simulation.SetFloat("_Song", song);
            simulation.SetFloat("_Evolution", evolution);
            simulation.SetFloat("_Dissolve", dissolve);
            simulation.SetFloat("_Pulse", Score.Pulse(song));
            simulation.SetVector("_HeadVelocity", (Anatomy.Head(song + 0.02f) - Anatomy.Head(song - 0.02f)) / 0.04f);
            if (reset) {
                simulation.Dispatch(initializeKernel, SimulatedParticles / 128, 1, 1);
            } else {
                simulation.SetFloat("_Delta", Mathf.Min(delta, 0.05f));
                simulation.Dispatch(simulateKernel, SimulatedParticles / 128, 1, 1);
                SimulationSteps++;
            }
            previousSong = song;
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!Ready || camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            var settings = new RenderParams(lightMaterial) {
                camera = camera,
                worldBounds = new Bounds(new Vector3(0, 10, 35), Vector3.one * 700),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderPrimitives(settings, MeshTopology.Triangles, SimulatedParticles * 6);
        }

        public void ResetSimulation() { previousSong = -1; SimulationSteps = 0; }

        static Mesh CreateMembrane()
        {
            const int rings = 256, sides = 96;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var data = new List<Vector2>();
            var triangles = new List<int>();
            for (int surface = 0; surface < 4; surface++) {
                int across = surface == 0 ? sides : 14;
                int start = vertices.Count;
                for (int i = 0; i <= rings; i++) for (int j = 0; j <= across; j++) {
                    float u = i / (float)rings, v = j / (float)across;
                    vertices.Add(new Vector3(u, v, 0));
                    uv.Add(new Vector2(u, v));
                    data.Add(new Vector2(surface, v));
                }
                for (int i = 0; i < rings; i++) for (int j = 0; j < across; j++) {
                    int a = start + i * (across + 1) + j, b = a + across + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = "Leviathan translucent skin", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, data); mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(new Vector3(0, 10, 35), Vector3.one * 700);
            mesh.UploadMeshData(true);
            return mesh;
        }
        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            particles?.Dispose(); spine?.Dispose(); particles = null; spine = null;
            if (simulation) Destroy(simulation);
            if (lightMaterial) Destroy(lightMaterial);
            if (skinMaterial) Destroy(skinMaterial);
            if (skinMesh) Destroy(skinMesh);
        }
    }
}
