using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    // Visual-only comparison: stable samples, identical motion, different rasterization.
    public sealed class PointStudy : MonoBehaviour
    {
        public enum RenderMode { NativePoint, SharpQuad }
        const int JellyBudget = 1706;
        const int SerpentBudget = LeviathanVfx.SimulatedParticles;
        const int StudyLayer = 30;
        readonly Vector4[] spineData = new Vector4[Anatomy.SpineSamples];
        GraphicsBuffer jellySamples, serpentSamples, spine;
        Material jellyPoint, jellyQuad, serpentPoint, serpentQuad;
        Vector3 jellyAnchor, serpentAnchor;
        Vector3 jellyRoot;
        float currentSong;
        bool ready;

        public bool Visible { get; private set; } = true;
        public RenderMode Mode { get; private set; } = RenderMode.NativePoint;
        public int DensityMultiplier { get; private set; } = 3;
        public float Gain { get; private set; } = 4f;
        public float Flow { get; private set; } = 1f;
        public Vector3 JellyPosition { get; private set; }
        public Vector3 SerpentPosition { get; private set; }
        public int JellyPointCount => JellyBudget * DensityMultiplier;
        public int SerpentPointCount => SerpentBudget * DensityMultiplier;
        public MeshTopology NativeTopology => Mode == RenderMode.NativePoint ? MeshTopology.Points : MeshTopology.Triangles;

        public void Initialize(Experience owner)
        {
            Shader native = Resources.Load<Shader>("PointStudyNative");
            Shader sharp = Resources.Load<Shader>("PointStudySharp");
            if (!native || !sharp || !native.isSupported || !sharp.isSupported)
                throw new InvalidOperationException("Point study shaders are missing or unsupported.");

            jellyAnchor = CaveLayout.Spawn + new Vector3(25, 12, 32);
            serpentAnchor = CaveLayout.Rooms[1].Center + new Vector3(-40, 38, -8);
            spine = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Anatomy.SpineSamples, 16);
            jellySamples = Samples(JellyBudget * 3, true);
            serpentSamples = Samples(SerpentBudget * 3, false);
            jellyPoint = MakeMaterial(native, jellySamples, 0, 450f);
            jellyQuad = MakeMaterial(sharp, jellySamples, 0, 450f);
            serpentPoint = MakeMaterial(native, serpentSamples, 1, 1850f);
            serpentQuad = MakeMaterial(sharp, serpentSamples, 1, 1850f);
            ready = true;
            Tick(0, 0);
            RenderPipelineManager.beginCameraRendering += Render;
        }

        Material MakeMaterial(Shader shader, GraphicsBuffer samples, float shape, float surfaceArea)
        {
            var material = new Material(shader) { name = "Point study / " + (shape == 0 ? "jelly" : "serpent") };
            material.SetBuffer("_Samples", samples);
            material.SetBuffer("_Spine", spine);
            material.SetFloat("_Shape", shape);
            material.SetFloat("_SurfaceArea", surfaceArea);
            return material;
        }

        static GraphicsBuffer Samples(int count, bool jelly)
        {
            var data = new Vector4[count];
            for (uint i = 0; i < count; i++) {
                float a = Hash(i * 7 + 11), b = Hash(i * 7 + 12);
                float c = Hash(i * 7 + 13), d = Hash(i * 7 + 14);
                // Prefixes are representative, so 1x/3x toggles preserve identities.
                if (jelly) {
                    float part = c < 630f / JellyBudget ? 0 : c < 1526f / JellyBudget ? 1 : 2;
                    data[i] = new Vector4(a, b * Mathf.PI * 2, part, d);
                } else {
                    float part = c < .60f ? 0 : c < .80f ? 1 : 2;
                    data[i] = new Vector4(a, b * Mathf.PI * 2, Mathf.Sqrt(d), part);
                }
            }
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 16);
            buffer.SetData(data);
            return buffer;
        }

        static float Hash(uint value)
        {
            value ^= value >> 16; value *= 0x7feb352du;
            value ^= value >> 15; value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777216f;
        }

        public void SetVisible(bool value) => Visible = value;
        public void SetMode(RenderMode value) => Mode = value;
        public void SetDensity(int value) { DensityMultiplier = value >= 3 ? 3 : 1; UpdateSettings(); }
        public void SetGain(float value) { Gain = Mathf.Clamp(value, .25f, 12f); UpdateSettings(); }
        public void SetFlow(float value) { Flow = Mathf.Clamp(value, 0, 2f); UpdateSettings(); }

        public void Tick(float song, float dt)
        {
            if (!ready) return;
            float phase = song * .55f + 18f;
            Anatomy.WriteSpine(spineData, phase);
            Vector3 sourceCenter = CaveLayout.Rooms[1].Center;
            for (int i = 0; i < spineData.Length; i++) {
                Vector4 sample = spineData[i];
                Vector3 point = serpentAnchor + (new Vector3(sample.x, sample.y, sample.z) - sourceCenter) * .9f;
                spineData[i] = new Vector4(point.x, point.y, point.z, sample.w * .9f);
            }
            spine.SetData(spineData);
            currentSong = song;
            jellyRoot = jellyAnchor + new Vector3(Mathf.Sin(song * .19f) * .65f, Mathf.Sin(song * .32f) * .7f, 0);
            JellyPosition = jellyRoot + Vector3.down * 4f;
            SerpentPosition = serpentAnchor + (Anatomy.Center(.43f, phase) - sourceCenter) * .9f;
            UpdateSettings();
        }

        void UpdateSettings()
        {
            if (!ready) return;
            Configure(jellyPoint, jellyRoot, currentSong, JellyPointCount);
            Configure(jellyQuad, jellyRoot, currentSong, JellyPointCount);
            Configure(serpentPoint, serpentAnchor, currentSong, SerpentPointCount);
            Configure(serpentQuad, serpentAnchor, currentSong, SerpentPointCount);
        }

        void Configure(Material material, Vector3 root, float song, int count)
        {
            material.SetVector("_Anchor", root);
            material.SetFloat("_Song", song);
            material.SetFloat("_Gain", Gain);
            material.SetFloat("_Flow", Flow);
            material.SetFloat("_ActiveCount", count);
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!ready || !Visible || !isActiveAndEnabled || (camera.cullingMask & (1 << StudyLayer)) == 0 ||
                camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            bool native = Mode == RenderMode.NativePoint;
            Draw(camera, native ? jellyPoint : jellyQuad, JellyPointCount,
                new Bounds(jellyAnchor, Vector3.one * 70), JellyPosition, 190f, 300f);
            Draw(camera, native ? serpentPoint : serpentQuad, SerpentPointCount,
                new Bounds(CaveLayout.Rooms[1].Center, CaveLayout.Rooms[1].Radius * 2f), CaveLayout.Rooms[1].Center, 220f, 360f);
        }

        void Draw(Camera camera, Material material, int count, Bounds bounds, Vector3 center, float fadeStart, float fadeEnd)
        {
            float distance = Vector3.Distance(camera.transform.position, center);
            if (distance >= fadeEnd) return;
            material.SetFloat("_Visibility", 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fadeStart, fadeEnd, distance)));
            var settings = new RenderParams(material) {
                camera = camera, layer = StudyLayer, worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false
            };
            bool native = Mode == RenderMode.NativePoint;
            Graphics.RenderPrimitives(settings, native ? MeshTopology.Points : MeshTopology.Triangles, native ? count : count * 6);
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            jellySamples?.Dispose(); serpentSamples?.Dispose(); spine?.Dispose();
            if (jellyPoint) Destroy(jellyPoint);
            if (jellyQuad) Destroy(jellyQuad);
            if (serpentPoint) Destroy(serpentPoint);
            if (serpentQuad) Destroy(serpentQuad);
        }
    }
}
