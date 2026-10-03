using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class LeviathanVfx : MonoBehaviour
    {
        public const int SimulatedParticles = 262144;
        const int ResonanceEventCapacity = 16;
        const int OrganCapacity = 16;
        readonly Vector4[] spineSamples = new Vector4[Anatomy.SpineSamples];
        readonly List<Vector4> resonanceEvents = new(ResonanceEventCapacity);
        readonly Vector4[] resonanceEventData = new Vector4[ResonanceEventCapacity];
        readonly Vector4[] organStateData = new Vector4[OrganCapacity];
        GraphicsBuffer particles, spine;
        GraphicsBuffer resonanceBuffer, organStateBuffer;
        ComputeShader simulation;
        Material lightMaterial, skinMaterial;
        Mesh skinMesh;
        int initializeKernel, simulateKernel;
        float previousSong = -1;
        float previousProgress;
        float resonanceClock;
        float releaseClock;
        float releaseSong;
        bool resonanceEnabled;
        bool released;
        bool releasePosePending;
        bool simulationResetPending = true;
        public int SimulationSteps { get; private set; }
        public int InitializationCount { get; private set; }
        public bool Ready => particles != null;
        public bool Released => released;
        public bool ReleaseSettled => released && releaseClock >= 12f;
        public int ParticleCount => Ready ? SimulatedParticles : 0;
        public int OrganStateCount => OrganCapacity;
        public float OrganHeat(int index) => index >= 0 && index < OrganCapacity ? organStateData[index].y : 0;

        public void Initialize(ComputeShader compute, Material light, Material membrane)
        {
            if (!SystemInfo.supportsComputeShaders || !compute || !light || !membrane)
                throw new InvalidOperationException("Leviathan VFX requires compute shader support and production materials.");
            simulation = Instantiate(compute);
            lightMaterial = new Material(light);
            skinMaterial = new Material(membrane);
            particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SimulatedParticles, 64);
            spine = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Anatomy.SpineSamples, 16);
            resonanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ResonanceEventCapacity, 16);
            organStateBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OrganCapacity, 16);
            initializeKernel = simulation.FindKernel("Initialize");
            simulateKernel = simulation.FindKernel("Simulate");
            foreach (int kernel in new[] { initializeKernel, simulateKernel }) {
                simulation.SetBuffer(kernel, "_Particles", particles);
                simulation.SetBuffer(kernel, "_Spine", spine);
            }
            lightMaterial.SetBuffer("_Particles", particles);
            lightMaterial.SetBuffer("_Spine", spine);
            lightMaterial.SetBuffer("_ResonanceEvents", resonanceBuffer);
            lightMaterial.SetBuffer("_OrganStates", organStateBuffer);
            skinMaterial.SetBuffer("_Spine", spine);
            simulation.SetBuffer(simulateKernel, "_ResonanceEvents", resonanceBuffer);
            simulation.SetInt("_Count", SimulatedParticles);
            simulation.SetInt("_ResonanceEventCount", 0);
            lightMaterial.SetInt("_ResonanceEventCount", 0);
            lightMaterial.SetInt("_OrganStateCount", OrganCapacity);
            Array.Clear(organStateData, 0, organStateData.Length);
            organStateBuffer.SetData(organStateData);
            simulation.SetVector("_SanctumCenter", CaveLayout.Rooms[1].Center);
            Vector3 roomRadius = CaveLayout.Rooms[1].Radius;
            simulation.SetVector("_SanctumRadius", new Vector3(roomRadius.x * 0.62f, roomRadius.y, roomRadius.z * 0.60f));
            skinMesh = CreateMembrane();
            PointCloud.Place("Leviathan / translucent living membrane", skinMesh, skinMaterial, transform);
            Tick(0, 0, 0);
            RenderPipelineManager.beginCameraRendering += Render;
        }

        public void Tick(float song, float evolution, float dissolve)
        {
            if (!Ready) return;
            ParticleTransitionSettings.Current.ApplyCompute(simulation);
            float delta = song - previousSong;
            resonanceClock = song;
            if (released) releaseClock = Mathf.Max(0, song - releaseSong);
            bool reset = simulationResetPending || (!resonanceEnabled && !released && (previousSong < 0 || delta < -0.001f || delta > 0.5f));
            if (!reset && !released && delta < 1f / 120f) return;
            if (!released || previousSong < 0 || releasePosePending) {
                Anatomy.WriteSpine(spineSamples, released ? releaseSong : song);
                spine.SetData(spineSamples);
                releasePosePending = false;
            }
            simulation.SetFloat("_Song", released ? releaseSong : song);
            simulation.SetFloat("_Evolution", evolution);
            simulation.SetFloat("_Dissolve", dissolve);
            simulation.SetFloat("_Pulse", Score.Pulse(song));
            simulation.SetVector("_HeadVelocity", (Anatomy.Head(song + 0.02f) - Anatomy.Head(song - 0.02f)) / 0.04f);
            simulation.SetFloat("_ResonanceClock", resonanceClock);
            simulation.SetFloat("_Released", released ? 1 : 0);
            simulation.SetFloat("_ReleaseAge",releaseClock);
            simulation.SetFloat("_ReleaseBlend", released ? Mathf.SmoothStep(0, 1, releaseClock / 12f) : 0);
            lightMaterial.SetFloat("_ResonanceClock", resonanceClock);
            lightMaterial.SetFloat("_Released", released ? 1 : 0);
            lightMaterial.SetFloat("_ReleaseAge",releaseClock);
            skinMaterial.SetFloat("_Released", released ? 1 : 0);
            skinMaterial.SetFloat("_ReleaseBlend", released ? Mathf.SmoothStep(0, 1, releaseClock / 12f) : 0);
            UploadResonanceEvents();
            organStateBuffer.SetData(organStateData);
            if (reset) {
                simulation.Dispatch(initializeKernel, SimulatedParticles / 128, 1, 1);
                simulationResetPending = false;
                InitializationCount++;
            } else {
                float advanced = resonanceEnabled || released ? Mathf.Clamp(delta, 0, 0.3f) : Mathf.Min(delta, 0.05f);
                int steps = resonanceEnabled || released ? Mathf.Clamp(Mathf.CeilToInt(advanced / 0.05f), 1, 6) : 1;
                float stepDelta = advanced / steps;
                for (int step = 0; step < steps; step++) {
                    float stepSong = song - advanced + (step + 1) * stepDelta;
                    if (resonanceEnabled && !released) {
                        Anatomy.WriteSpine(spineSamples, stepSong);
                        spine.SetData(spineSamples);
                    }
                    simulation.SetFloat("_Delta", stepDelta);
                    simulation.SetFloat("_Song", released ? releaseSong : stepSong);
                    simulation.Dispatch(simulateKernel, SimulatedParticles / 128, 1, 1);
                }
                SimulationSteps++;
            }
            previousSong = song;
        }

        public void SetResonance(float progress, bool completed, float song)
        {
            resonanceEnabled = true;
            resonanceClock = song;
            progress = Mathf.Clamp01(progress);
            previousProgress = Mathf.Max(previousProgress, progress);
            if (completed && !released) {
                released = true;
                releaseSong = song;
                releaseClock = 0;
                releasePosePending = true;
            }
        }

        public void SetOrganState(int index, float u, float warmth, float song, bool active)
        {
            if (index < 0 || index >= OrganCapacity) return;
            organStateData[index] = new Vector4(Mathf.Clamp01(u), Mathf.Clamp01(warmth), song, active ? 1 : 0);
            if (warmth > 0) {
                if (resonanceEvents.Count == ResonanceEventCapacity) resonanceEvents.RemoveAt(0);
                resonanceEvents.Add(new Vector4(Mathf.Clamp01(u), song, 0, 0));
            }
        }

        void UploadResonanceEvents()
        {
            resonanceEvents.RemoveAll(e => resonanceClock - e.y > 3.5f);
            Array.Clear(resonanceEventData, 0, resonanceEventData.Length);
            for (int i = 0; i < resonanceEvents.Count; i++) resonanceEventData[i] = resonanceEvents[i];
            resonanceBuffer.SetData(resonanceEventData);
            simulation.SetInt("_ResonanceEventCount", resonanceEvents.Count);
            lightMaterial.SetInt("_ResonanceEventCount", resonanceEvents.Count);
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

        public void ResetSimulation()
        {
            previousSong = -1; SimulationSteps = 0; previousProgress = 0; simulationResetPending = true;
            resonanceClock = 0; releaseClock = 0; releaseSong = 0; releasePosePending = false;
            resonanceEnabled = false; released = false; resonanceEvents.Clear();
            Array.Clear(organStateData, 0, organStateData.Length);
            if (organStateBuffer != null) organStateBuffer.SetData(organStateData);
        }

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
            particles?.Dispose(); spine?.Dispose(); resonanceBuffer?.Dispose(); organStateBuffer?.Dispose();
            particles = null; spine = null; resonanceBuffer = null; organStateBuffer = null;
            if (simulation) Destroy(simulation);
            if (lightMaterial) Destroy(lightMaterial);
            if (skinMaterial) Destroy(skinMaterial);
            if (skinMesh) Destroy(skinMesh);
        }
    }
}
