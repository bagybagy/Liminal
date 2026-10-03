using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class WhaleArrival : MonoBehaviour
    {
        const int ParticleCount = 12000;
        const float ChargeSeconds = 3.1f;
        const float BreachSeconds = 3.8f;
        const float JoinSeconds = 3.5f;
        const float SlamTime = ChargeSeconds + BreachSeconds;
        const float CruiseOffset = 13.777778f;
        const float RiseDistance = 320f;
        const float RiseStartSpeed = 24f;
        Material vortex;
        HorizonWater water;
        float startedAt = -1f, song;
        bool slammed;
        Vector3 impactPosition, impactVelocity;

        public Vector3 Origin { get; private set; }
        public bool Triggered => startedAt >= 0f;
        public float Age => Triggered ? Mathf.Max(0f, song - startedAt) : -1f;
        public bool Complete => Age >= SlamTime + JoinSeconds;
        public float CruiseTime => CruiseOffset + (Complete ? Age - SlamTime - JoinSeconds : 0f);
        public bool Slammed => slammed;
        public float ImpactAge { get; private set; }
        public float PeakTime => ChargeSeconds;
        public bool PeakReached => Triggered && Age >= ChargeSeconds;
        public float Formation => Triggered ? Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0f, ChargeSeconds, Age)) : 0f;
        public float Visibility => Triggered ? Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(ChargeSeconds, ChargeSeconds + 0.16f, Age)) : 0f;

        public void Initialize()
        {
            Shader shader = Resources.Load<Shader>("WhaleArrival");
            if (!shader) throw new InvalidOperationException("Whale arrival shader is missing.");
            vortex = new Material(shader) { name = "Blue gyre / whale arrival" };
            Origin = CaveLayout.Rooms[2].Center + new Vector3(0f, 0f, -260f);
            Origin = new Vector3(Origin.x, CaveLayout.HorizonSurfaceY - 28f, Origin.z);
            CalculateLanding();
            ResetArrival();
            vortex.SetVector("_Origin", Origin);
            vortex.SetFloat("_Age", -1f);
            vortex.SetFloat("_Song", 0f);
            vortex.SetFloat("_PeakTime", ChargeSeconds);
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
            if (!slammed && Age >= ImpactAge)
            {
                slammed = true;
                if (water) water.MajorImpact(impactPosition, impactVelocity, startedAt + ImpactAge, 4.5f);
            }
            vortex.SetVector("_Origin", Origin);
            vortex.SetFloat("_Song", song);
            vortex.SetFloat("_Age", Age);
        }

        void CalculateLanding()
        {
            Vector3 p0 = Origin - Vector3.up * 75f;
            Vector3 p1 = Origin + new Vector3(15f, 185f, 15f);
            Vector3 p2 = Origin + new Vector3(150f, 155f, -45f);
            Vector3 p3 = Origin + new Vector3(170f, -14f, -85f);
            // First contact of the descending belly, rather than the end of the jump animation.
            float low = .65f, high = 1f;
            for (int i = 0; i < 20; i++)
            {
                float u = (low + high) * .5f;
                if (Cubic(p0, p1, p2, p3, u).y - 20f > CaveLayout.HorizonSurfaceY) low = u;
                else high = u;
            }
            float contact = (low + high) * .5f;
            ImpactAge = ChargeSeconds + BreachSeconds * contact;
            impactPosition = Cubic(p0, p1, p2, p3, contact);
            impactPosition.y = CaveLayout.HorizonSurfaceY;
            impactVelocity = CubicTangent(p0, p1, p2, p3, contact) / BreachSeconds;
        }

        public void Pose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 peak = Origin - Vector3.up * 75f;
            Vector3 breachControl1 = Origin + new Vector3(15f, 185f, 15f);
            Vector3 breachVelocity = 3f * (breachControl1 - peak) / BreachSeconds;
            if (!Triggered || Age < ChargeSeconds)
            {
                Vector3 start = peak - Vector3.up * RiseDistance;
                Vector3 control1 = start + Vector3.up * (RiseStartSpeed * ChargeSeconds / 3f);
                Vector3 control2 = peak - breachVelocity * (ChargeSeconds / 3f);
                float rise = Triggered ? Mathf.Clamp01(Age / ChargeSeconds) : 0f;
                position = Cubic(start, control1, control2, peak, rise);
                Vector3 riseTangent = CubicTangent(start, control1, control2, peak, rise) / ChargeSeconds;
                rotation = Quaternion.LookRotation(riseTangent.normalized, Vector3.forward);
                return;
            }
            float u = Mathf.Clamp01((Age - ChargeSeconds) / BreachSeconds);
            Vector3 p0 = peak;
            Vector3 p1 = breachControl1;
            Vector3 p2 = Origin + new Vector3(150f, 155f, -45f);
            Vector3 p3 = Origin + new Vector3(170f, -14f, -85f);
            float v = 1f - u;
            position = Cubic(p0, p1, p2, p3, u);
            Vector3 tangent = CubicTangent(p0, p1, p2, p3, u);
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

        static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float v = 1f - u;
            return v * v * v * p0 + 3f * v * v * u * p1 +
                3f * v * u * u * p2 + u * u * u * p3;
        }

        static Vector3 CubicTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float v = 1f - u;
            return 3f * v * v * (p1 - p0) + 6f * v * u * (p2 - p1) +
                3f * u * u * (p3 - p2);
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
