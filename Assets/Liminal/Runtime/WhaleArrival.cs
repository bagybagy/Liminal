using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class WhaleArrival : MonoBehaviour
    {
        const int ParticleCount = 12000;
        const float ChargeSeconds = 1.5f;
        const float BreachSeconds = 3.8f;
        const float JoinSeconds = 3.5f;
        const float SlamTime = ChargeSeconds + BreachSeconds;
        const float CruiseOffset = 13.777778f;
        Material vortex;
        HorizonWater water;
        float startedAt = -1f, song;
        bool slammed;

        public Vector3 Origin { get; private set; }
        public bool Triggered => startedAt >= 0f;
        public float Age => Triggered ? Mathf.Max(0f, song - startedAt) : -1f;
        public bool Complete => Age >= SlamTime + JoinSeconds;
        public float CruiseTime => CruiseOffset + (Complete ? Age - SlamTime - JoinSeconds : 0f);
        public bool Slammed => slammed;
        public float Visibility => Triggered ? Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(ChargeSeconds, ChargeSeconds + 0.7f, Age)) : 0f;

        public void Initialize()
        {
            Shader shader = Resources.Load<Shader>("WhaleArrival");
            if (!shader) throw new InvalidOperationException("Whale arrival shader is missing.");
            vortex = new Material(shader) { name = "Blue gyre / whale arrival" };
            Origin = CaveLayout.Rooms[2].Center + new Vector3(0f, 0f, -260f);
            Origin = new Vector3(Origin.x, CaveLayout.HorizonSurfaceY - 28f, Origin.z);
            ResetArrival();
            vortex.SetVector("_Origin", Origin);
            vortex.SetFloat("_Age", -1f);
            vortex.SetFloat("_Song", 0f);
            RenderPipelineManager.beginCameraRendering += Render;
        }

        public void ResetArrival()
        {
            startedAt = -1f;
            song = 0f;
            slammed = false;
        }

        public void Tick(float musicTime, Vector3 player)
        {
            song = musicTime;
            if (!Triggered && Vector3.Distance(player, Origin) <= 190f) startedAt = song;
            if (!water) water = GetComponent<HorizonWater>();
            if (!slammed && Age >= SlamTime)
            {
                slammed = true;
                if (water) water.MajorImpact(Origin + new Vector3(170f, -14f, -85f),
                    new Vector3(34f, -55f, 12f), song, 4.5f);
            }
            vortex.SetVector("_Origin", Origin);
            vortex.SetFloat("_Song", song);
            vortex.SetFloat("_Age", Age);
        }

        public void Pose(out Vector3 position, out Quaternion rotation)
        {
            if (Age < ChargeSeconds)
            {
                position = Origin - Vector3.up * 100f;
                rotation = Quaternion.LookRotation(new Vector3(0.45f, 1f, 0.08f), Vector3.up);
                return;
            }
            float u = Mathf.Clamp01((Age - ChargeSeconds) / BreachSeconds);
            Vector3 p0 = Origin - Vector3.up * 75f;
            Vector3 p1 = Origin + new Vector3(15f, 185f, 15f);
            Vector3 p2 = Origin + new Vector3(150f, 155f, -45f);
            Vector3 p3 = Origin + new Vector3(170f, -14f, -85f);
            float v = 1f - u;
            position = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
            Vector3 tangent = 3f * v * v * (p1 - p0) + 6f * v * u * (p2 - p1) + 3f * u * u * (p3 - p2);
            rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) *
                Quaternion.Euler(0f, 0f, Mathf.Sin(u * Mathf.PI) * -12f);
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(new Vector3(0.94f, 0.02f, 0.34f), Vector3.up),
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 0.98f, u)));
            if (Age <= SlamTime) return;
            float join = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SlamTime, SlamTime + JoinSeconds, Age));
            MarineLife.EvaluateWhalePose(CruiseOffset, out Vector3 cruise, out Quaternion cruiseRotation);
            position = Vector3.Lerp(p3, cruise, join) - Vector3.up * (Mathf.Sin(join * Mathf.PI) * 42f);
            rotation = Quaternion.Slerp(rotation, cruiseRotation, join);
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!vortex || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
            var settings = new RenderParams(vortex) { camera = camera, worldBounds = CaveLayout.WorldBounds,
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderPrimitives(settings, MeshTopology.Triangles, ParticleCount * 6);
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            if (vortex) Destroy(vortex);
        }
    }
}
