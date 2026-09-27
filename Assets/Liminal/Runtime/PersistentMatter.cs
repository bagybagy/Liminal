using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class PersistentMatter : IDisposable
    {
        const int Threads = 128;
        MatterGroup[] groupData;
        GraphicsBuffer seeds, groups, particles;
        ComputeShader simulation;
        Material drawMaterial;
        int initializeKernel, simulateKernel;
        bool initialized, disposed;
        float previousSong = float.NaN;
        Vector3 currentCenter, currentVelocity, playerPosition, playerVelocity;
        float currentRadius = 1f, currentEnergy;

        public int ParticleCount { get; private set; }
        public int InitializationCount { get; private set; }
        public int SimulationSteps { get; private set; }
        public bool Ready => !disposed && particles != null && seeds != null && groups != null;

        public PersistentMatter() { }

        public void Initialize(ComputeShader compute, Material material, IReadOnlyList<MatterSeed> matterSeeds, int groupCount)
        {
            if (Ready || disposed) throw new InvalidOperationException("Persistent matter can only be initialized once per instance.");
            if (!SystemInfo.supportsComputeShaders)
                throw new InvalidOperationException("Persistent matter requires compute shader support.");
            if (!compute || !material || matterSeeds == null || matterSeeds.Count == 0 || groupCount <= 0)
                throw new ArgumentException("Persistent matter requires a compute shader, material, seeds, and at least one group.");

            ParticleCount = matterSeeds.Count;
            groupData = new MatterGroup[groupCount];
            for (int i = 0; i < groupData.Length; i++) groupData[i].localToWorld = Matrix4x4.identity;
            var seedData = new MatterSeed[ParticleCount];
            for (int i = 0; i < ParticleCount; i++)
            {
                seedData[i] = matterSeeds[i];
                int group = Mathf.RoundToInt(seedData[i].traits.x);
                if (group < 0 || group >= groupCount)
                    throw new ArgumentOutOfRangeException(nameof(matterSeeds), "Every seed group must be inside groupCount.");
            }

            simulation = UnityEngine.Object.Instantiate(compute);
            simulation.name = compute.name + " (Persistent Matter)";
            drawMaterial = new Material(material) { name = material.name + " (Persistent Matter)" };
            seeds = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 64);
            groups = new GraphicsBuffer(GraphicsBuffer.Target.Structured, groupCount, 96);
            particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 64);
            seeds.SetData(seedData);

            initializeKernel = simulation.FindKernel("Initialize");
            simulateKernel = simulation.FindKernel("Simulate");
            simulation.SetBuffer(initializeKernel, "_Seeds", seeds);
            simulation.SetBuffer(initializeKernel, "_Groups", groups);
            simulation.SetBuffer(initializeKernel, "_Particles", particles);
            simulation.SetBuffer(simulateKernel, "_Seeds", seeds);
            simulation.SetBuffer(simulateKernel, "_Groups", groups);
            simulation.SetBuffer(simulateKernel, "_Particles", particles);
            simulation.SetInt("_Count", ParticleCount);
            drawMaterial.SetBuffer("_Particles", particles);
            RenderPipelineManager.beginCameraRendering += Render;
        }

        public void SetGroup(int index, Matrix4x4 transform, MatterPhase phase, float phaseAge, float energy,
            Vector3 impulsePoint, float impulseStrength)
        {
            if (index < 0 || index >= groupData.Length) throw new ArgumentOutOfRangeException(nameof(index));
            groupData[index].localToWorld = transform;
            groupData[index].state = new Vector4((float)phase, Mathf.Max(0f, phaseAge), Mathf.Max(0f, energy), 0f);
            groupData[index].impulse = new Vector4(impulsePoint.x, impulsePoint.y, impulsePoint.z, impulseStrength);
        }

        public void SetCurrent(Vector3 center, Vector3 velocity, float radius, float energy)
        {
            currentCenter = center;
            currentVelocity = velocity;
            currentRadius = Mathf.Max(0.01f, radius);
            currentEnergy = Mathf.Max(0f, energy);
        }

        public void SetPlayer(Vector3 position, Vector3 velocity)
        {
            playerPosition = position;
            playerVelocity = velocity;
        }

        public void Tick(float song, float dt)
        {
            if (!Ready) return;
            if (!float.IsNaN(previousSong) && song < previousSong - 0.001f)
                initialized = false;

            groups.SetData(groupData);
            simulation.SetFloat("_Song", song);
            simulation.SetVector("_Current", new Vector4(currentCenter.x, currentCenter.y, currentCenter.z, currentRadius));
            simulation.SetVector("_CurrentVelocity", currentVelocity);
            simulation.SetFloat("_CurrentEnergy", currentEnergy);
            simulation.SetVector("_Player", playerPosition);
            simulation.SetVector("_PlayerVelocity", playerVelocity);

            if (!initialized)
            {
                simulation.Dispatch(initializeKernel, Mathf.CeilToInt(ParticleCount / (float)Threads), 1, 1);
                initialized = true;
                InitializationCount++;
            }

            float remaining = Mathf.Clamp(dt, 0f, 0.1f);
            const float maxStep = 1f / 60f;
            while (remaining > 0.00001f)
            {
                float step = Mathf.Min(remaining, maxStep);
                simulation.SetFloat("_Delta", step);
                simulation.Dispatch(simulateKernel, Mathf.CeilToInt(ParticleCount / (float)Threads), 1, 1);
                SimulationSteps++;
                remaining -= step;
            }
            previousSong = song;
        }

        public void ResetSimulation()
        {
            if (disposed) return;
            initialized = false;
            previousSong = float.NaN;
        }

        public MatterParticle[] Readback()
        {
            if (!Ready || !initialized) return Array.Empty<MatterParticle>();
            var result = new MatterParticle[ParticleCount];
            particles.GetData(result);
            return result;
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!Ready || !initialized || camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            var settings = new RenderParams(drawMaterial)
            {
                camera = camera,
                worldBounds = new Bounds(new Vector3(0f, 0f, 180f), new Vector3(1200f, 1200f, 1800f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderPrimitives(settings, MeshTopology.Triangles, ParticleCount * 6);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RenderPipelineManager.beginCameraRendering -= Render;
            seeds?.Dispose(); groups?.Dispose(); particles?.Dispose();
            seeds = null; groups = null; particles = null;
            if (simulation) UnityEngine.Object.Destroy(simulation);
            if (drawMaterial) UnityEngine.Object.Destroy(drawMaterial);
        }
    }
}
