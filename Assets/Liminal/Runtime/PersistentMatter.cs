using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class PersistentMatter : IDisposable
    {
        const int Threads = 128;
        public const int WhaleRingCount = 12;
        public const int WhaleTargetsPerRing = 4;
        public const int WhalePatchCount = WhaleRingCount * WhaleTargetsPerRing;
        MatterGroup[] groupData;
        MatterSeed[] seedData;
        int[] whaleSeedIndices = Array.Empty<int>();
        GraphicsBuffer seeds, groups, particles, whalePatchBuffer, alternateForms, birthMatrices, groupVelocities, surfaceFrames;
        Matrix4x4[] birthData;
        Vector4[] groupVelocityData;
        ComputeShader simulation;
        Material drawMaterial;
        int initializeKernel, simulateKernel;
        bool initialized, disposed, whaleDestinationsSet;
        float previousSong = float.NaN;
        Vector3 currentCenter, currentVelocity, playerPosition, playerVelocity;
        Vector3 whaleVelocity, whaleAngularVelocity;
        Vector4 whaleGesture;
        Vector4[] whalePatches = new Vector4[WhalePatchCount];
        float whaleSurfaceActivity, whaleTurn;
        int whaleGroup = -1;
        float currentRadius = 1f, currentEnergy;
        float whaleVisibility;
        bool whaleArriving;
        Vector3 arrivalOrigin;
        float whaleFormation;
        int comparisonJellyGroup = -1;
        readonly MaterialPropertyBlock primaryLook = new();
        readonly MaterialPropertyBlock companionLook = new();
        public static Vector3 JellyComparisonOffset => Vector3.right * 28f;
        public static Vector3 WhaleComparisonOffset => Vector3.right * 280f;

        public int ParticleCount { get; private set; }
        public int InitializationCount { get; private set; }
        public int SimulationSteps { get; private set; }
        public bool Ready => !disposed && particles != null && seeds != null && groups != null && whalePatchBuffer != null;

        public PersistentMatter() { }

        public void SetComparisonJellyGroup(int group) => comparisonJellyGroup = group;

        public void Initialize(ComputeShader compute, Material material, IReadOnlyList<MatterSeed> matterSeeds, int groupCount,
            IReadOnlyList<Vector4> dolphinForms = null)
        {
            if (Ready || disposed) throw new InvalidOperationException("Persistent matter can only be initialized once per instance.");
            if (!SystemInfo.supportsComputeShaders)
                throw new InvalidOperationException("Persistent matter requires compute shader support.");
            if (!compute || !material || matterSeeds == null || matterSeeds.Count == 0 || groupCount <= 0)
                throw new ArgumentException("Persistent matter requires a compute shader, material, seeds, and at least one group.");

            ParticleCount = matterSeeds.Count;
            groupData = new MatterGroup[groupCount];
            birthData = new Matrix4x4[groupCount];
            groupVelocityData = new Vector4[groupCount];
            for (int i = 0; i < groupData.Length; i++) groupData[i].localToWorld = Matrix4x4.identity;
            seedData = new MatterSeed[ParticleCount];
            var whaleSeeds = new List<int>();
            for (int i = 0; i < ParticleCount; i++)
            {
                seedData[i] = matterSeeds[i];
                int group = Mathf.RoundToInt(seedData[i].traits.x);
                if (group < 0 || group >= groupCount)
                    throw new ArgumentOutOfRangeException(nameof(matterSeeds), "Every seed group must be inside groupCount.");
                if (seedData[i].traits.y > 2.5f) whaleSeeds.Add(i);
            }
            whaleSeedIndices = whaleSeeds.ToArray();

            simulation = UnityEngine.Object.Instantiate(compute);
            ParticleTransitionSettings.Current.ApplyCompute(simulation);
            simulation.name = compute.name + " (Persistent Matter)";
            drawMaterial = new Material(material) { name = material.name + " (Persistent Matter)" };
            seeds = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 64);
            groups = new GraphicsBuffer(GraphicsBuffer.Target.Structured, groupCount, 96);
            particles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 64);
            whalePatchBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, WhalePatchCount, 16);
            alternateForms = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount, 16);
            surfaceFrames = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ParticleCount * 2, 16);
            birthMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, groupCount, 64);
            groupVelocities = new GraphicsBuffer(GraphicsBuffer.Target.Structured, groupCount, 16);
            var alternateData = new Vector4[ParticleCount];
            if (dolphinForms != null)
            {
                if (dolphinForms.Count != ParticleCount) throw new ArgumentException("Alternate forms must match the particle count.");
                for (int i = 0; i < ParticleCount; i++) alternateData[i] = dolphinForms[i];
            }
            alternateForms.SetData(alternateData);
            var frames = new Vector4[ParticleCount * 2];
            for (int i = 0; i < ParticleCount; i++)
            {
                if (seedData[i].traits.y < 2.5f) continue;
                Vector3 point = seedData[i].form;
                frames[i * 2] = WhaleAnatomy.Normal(point);
                frames[i * 2 + 1] = WhaleAnatomy.Tangent(point);
            }
            surfaceFrames.SetData(frames);
            birthMatrices.SetData(birthData);
            groupVelocities.SetData(groupVelocityData);
            seeds.SetData(seedData);
            simulation.SetVector("_AtlantisOrigin", AtlantisGeometry.CityOrigin);
            whalePatchBuffer.SetData(whalePatches);

            initializeKernel = simulation.FindKernel("Initialize");
            simulateKernel = simulation.FindKernel("Simulate");
            simulation.SetBuffer(initializeKernel, "_Seeds", seeds);
            simulation.SetBuffer(initializeKernel, "_Groups", groups);
            simulation.SetBuffer(initializeKernel, "_Particles", particles);
            simulation.SetBuffer(initializeKernel, "_WhalePatches", whalePatchBuffer);
            simulation.SetBuffer(initializeKernel, "_AlternateForms", alternateForms);
            simulation.SetBuffer(initializeKernel, "_BirthMatrices", birthMatrices);
            simulation.SetBuffer(initializeKernel, "_GroupVelocities", groupVelocities);
            simulation.SetBuffer(initializeKernel, "_SurfaceFrames", surfaceFrames);
            simulation.SetBuffer(simulateKernel, "_Seeds", seeds);
            simulation.SetBuffer(simulateKernel, "_Groups", groups);
            simulation.SetBuffer(simulateKernel, "_Particles", particles);
            simulation.SetBuffer(simulateKernel, "_WhalePatches", whalePatchBuffer);
            simulation.SetBuffer(simulateKernel, "_AlternateForms", alternateForms);
            simulation.SetBuffer(simulateKernel, "_BirthMatrices", birthMatrices);
            simulation.SetBuffer(simulateKernel, "_GroupVelocities", groupVelocities);
            simulation.SetBuffer(simulateKernel, "_SurfaceFrames", surfaceFrames);
            simulation.SetInt("_Count", ParticleCount);
            drawMaterial.SetBuffer("_Particles", particles);
            drawMaterial.SetBuffer("_Seeds", seeds);
            drawMaterial.SetBuffer("_Groups", groups);
            drawMaterial.SetBuffer("_GroupVelocities", groupVelocities);
            drawMaterial.SetBuffer("_AlternateForms", alternateForms);
            drawMaterial.SetBuffer("_SurfaceFrames", surfaceFrames);
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

        public void SetWhaleMotion(Vector3 velocity, float surfaceActivity, float turn, bool arriving = false)
        {
            whaleVelocity = velocity;
            whaleSurfaceActivity = Mathf.Clamp01(surfaceActivity);
            whaleTurn = Mathf.Clamp01(turn);
            whaleArriving = arriving;
        }

        public void SetWhaleGesture(Vector4 gesture, Vector3 angularVelocity)
        { whaleGesture = gesture; whaleAngularVelocity = angularVelocity; }

        public void SetWhaleVisibility(float visibility)
        {
            whaleVisibility = Mathf.Clamp01(visibility);
            if (drawMaterial) drawMaterial.SetFloat("_WhaleVisibility", whaleVisibility);
        }

        public void SetWhaleArrival(Vector3 origin, float formation)
        {
            arrivalOrigin = origin;
            whaleFormation = Mathf.Clamp01(formation);
        }

        public void SetDolphinState(int group, Matrix4x4 birth, Vector3 velocity, bool born)
        {
            birthData[group] = birth;
            groupVelocityData[group] = new Vector4(velocity.x, velocity.y, velocity.z, born ? 1f : 0f);
        }

        public void SetWhaleGroup(int groupIndex)
        {
            if (groupData == null || groupIndex < 0 || groupIndex >= groupData.Length)
                throw new ArgumentOutOfRangeException(nameof(groupIndex));
            whaleGroup = groupIndex;
            if (simulation) simulation.SetInt("_WhaleGroup", whaleGroup);
            if (drawMaterial) drawMaterial.SetInt("_WhaleGroup", whaleGroup);
        }

        public void SetWhalePatches(Vector4[] patches)
        {
            if (patches == null || patches.Length != whalePatches.Length)
                throw new ArgumentException("Whale resonator patches must contain exactly 48 entries.", nameof(patches));
            Array.Copy(patches, whalePatches, whalePatches.Length);
            if (whalePatchBuffer != null) whalePatchBuffer.SetData(whalePatches);
        }

        public void SetWhaleDestinations(Func<int, Vector3> sampler)
        {
            if (!Ready) throw new InvalidOperationException("Persistent matter must be initialized before whale destinations are set.");
            if (sampler == null) throw new ArgumentNullException(nameof(sampler));
            if (whaleDestinationsSet) return;

            for (int i = 0; i < whaleSeedIndices.Length; i++)
            {
                int particleId = whaleSeedIndices[i];
                MatterSeed seed = seedData[particleId];
                Vector3 destination = sampler(particleId);
                seed.destination = new Vector4(destination.x, destination.y, destination.z, seed.destination.w);
                seedData[particleId] = seed;
            }
            seeds.SetData(seedData);
            whaleDestinationsSet = true;
        }

        public void Tick(float song, float dt)
        {
            if (!Ready) return;
            ParticleTransitionSettings.Current.ApplyCompute(simulation);
            if (!float.IsNaN(previousSong) && song < previousSong - 0.001f)
                initialized = false;

            groups.SetData(groupData);
            birthMatrices.SetData(birthData);
            groupVelocities.SetData(groupVelocityData);
            simulation.SetFloat("_Song", song);
            simulation.SetVector("_LiminalGrainLook", ParticleLook.Current);
            simulation.SetFloat("_AuthoredBeat", (float)AuthoredScore.BeatPosition(song));
            simulation.SetVector("_Current", new Vector4(currentCenter.x, currentCenter.y, currentCenter.z, currentRadius));
            simulation.SetVector("_CurrentVelocity", currentVelocity);
            simulation.SetFloat("_CurrentEnergy", currentEnergy);
            simulation.SetVector("_Player", playerPosition);
            simulation.SetVector("_PlayerVelocity", playerVelocity);
            simulation.SetVector("_WhaleVelocity", whaleVelocity);
            simulation.SetVector("_WhaleGesture", whaleGesture);
            simulation.SetVector("_WhaleAngularVelocity", whaleAngularVelocity);
            simulation.SetFloat("_WhaleSurfaceActivity", whaleSurfaceActivity);
            simulation.SetFloat("_WhaleTurn", whaleTurn);
            simulation.SetFloat("_WhaleArrival", whaleArriving ? 1f : 0f);
            simulation.SetVector("_ArrivalOrigin", arrivalOrigin);
            simulation.SetFloat("_WhaleFormation", whaleFormation);

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
                worldBounds = CaveLayout.WorldBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            primaryLook.SetFloat("_LiminalComparisonPass", ParticleLook.SideBySide ? 1f : 0f);
            primaryLook.SetVector("_LiminalComparisonOffset", Vector4.zero);
            settings.matProps = primaryLook;
            Graphics.RenderPrimitives(settings, MeshTopology.Triangles, ParticleCount * 6);
            if (!ParticleLook.SideBySide) return;
            int room = CaveLayout.NearestRoom(camera.transform.position);
            if (room != 0 && room != 2) return;
            if (room == 0 && comparisonJellyGroup < 0) return;
            Vector3 offset = room == 0 ? JellyComparisonOffset : WhaleComparisonOffset;
            companionLook.SetFloat("_LiminalComparisonPass", 2f);
            companionLook.SetVector("_LiminalComparisonOffset", offset);
            companionLook.SetFloat("_LiminalComparisonGroup", room == 0 ? comparisonJellyGroup : -2f);
            settings.matProps = companionLook;
            Bounds bounds = CaveLayout.WorldBounds;
            bounds.Encapsulate(bounds.max + offset);
            settings.worldBounds = bounds;
            Graphics.RenderPrimitives(settings, MeshTopology.Triangles, ParticleCount * 6);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RenderPipelineManager.beginCameraRendering -= Render;
            seeds?.Dispose(); groups?.Dispose(); particles?.Dispose(); whalePatchBuffer?.Dispose();
            alternateForms?.Dispose(); birthMatrices?.Dispose(); groupVelocities?.Dispose();
            surfaceFrames?.Dispose();
            seeds = null; groups = null; particles = null; whalePatchBuffer = null;
            seedData = null;
            whaleSeedIndices = Array.Empty<int>();
            if (simulation) UnityEngine.Object.Destroy(simulation);
            if (drawMaterial) UnityEngine.Object.Destroy(drawMaterial);
        }
    }
}
