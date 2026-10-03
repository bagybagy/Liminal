using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class WhaleImpactSplash : MonoBehaviour
    {
        public const float Lifetime = 6f;
        const int DesktopParticles = 98304;
        const int VrParticles = 65536;
        Material material;
        float startedAt = -1f, song, scale = 1f;
        Vector3 forward = Vector3.forward;

        [Range(.2f, 2f)] public float glow = .85f;
        public bool Ready => material != null;
        public bool Active => Ready && startedAt >= 0f && Age < Lifetime;
        public float Age => startedAt < 0f ? -1f : Mathf.Max(0f, song - startedAt);
        public Vector3 Origin { get; private set; }
        public Vector3 LastVelocity { get; private set; }
        public int ImpactCount { get; private set; }
        public int ParticleCount => UnityEngine.XR.XRSettings.enabled ? VrParticles : DesktopParticles;
        public float PeakHeight => 164f * scale * scale;

        public void Initialize()
        {
            Shader shader = Resources.Load<Shader>("WhaleImpactSplash");
            if (!shader) throw new InvalidOperationException("Whale landing splash shader is missing.");
            material = new Material(shader) { name = "Whale landing / coherent water curtain" };
            ResetSplash();
            RenderPipelineManager.beginCameraRendering += Render;
        }

        public bool Burst(Vector3 origin, Vector3 velocity, float musicTime, float strength)
        {
            if (!Ready || (Active && musicTime - startedAt < .45f)) return false;
            Origin = origin;
            LastVelocity = velocity;
            forward = new Vector3(velocity.x, 0f, velocity.z);
            forward = forward.sqrMagnitude > .001f ? forward.normalized : Vector3.forward;
            // The downward impact matters independently of the outgoing surface wave.
            scale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(20f, -velocity.y) / 55f) *
                Mathf.Lerp(.9f, 1.1f, Mathf.InverseLerp(3f, 8f, strength)), .85f, 1.2f);
            startedAt = song = musicTime;
            ImpactCount++;
            material.SetVector("_ImpactOrigin", Origin);
            material.SetVector("_ImpactForward", forward);
            material.SetFloat("_ImpactScale", scale);
            Tick(musicTime);
            return true;
        }

        public void Tick(float musicTime)
        {
            if (startedAt >= 0f && musicTime < song - .001f) ResetSplash();
            song = musicTime;
            if (!material) return;
            material.SetFloat("_ImpactAge", Age);
            material.SetFloat("_Gain", glow);
        }

        public void ResetSplash()
        {
            startedAt = -1f;
            song = 0f;
            ImpactCount = 0;
            if (material) material.SetFloat("_ImpactAge", -1f);
        }

        // Keep this trajectory in lockstep with WhaleSplashMotion.hlsl for the runtime proof.
        public Vector3 EvaluateParticle(int id, float age)
        {
            float a = Random((uint)id * 7u + 11u), b = Random((uint)id * 13u + 19u);
            float c = Random((uint)id * 17u + 31u), d = Random((uint)id * 23u + 47u);
            float angle = a * Mathf.PI * 2f;
            float core = c < .45f ? 1f : 0f;
            float layer = Mathf.Lerp(.82f + d * .26f, Mathf.Sqrt(d) * .65f, core);
            float launch = .04f + b * .18f;
            float t = Mathf.Max(0f, age - launch);
            float lobe = .78f + .15f * Mathf.Sin(angle * 3f + .8f) + .07f * Mathf.Cos(angle * 7f);
            lobe = Mathf.Lerp(lobe, .92f + .06f * Mathf.Cos(angle * 2f + .4f), core);
            float speedY = Mathf.Lerp(90f, 185f, Mathf.Pow(b, .65f)) * lobe * scale;
            float speedOut = Mathf.Lerp(27f, 88f, b) * Mathf.Lerp(1f, .36f, core) * scale;
            Vector3 local = new Vector3(Mathf.Cos(angle) * (34f * layer + speedOut * t),
                Mathf.Max(0f, speedY * t - 52.5f * t * t),
                Mathf.Sin(angle) * (78f * layer + speedOut * t * .85f));
            local.x *= .88f + .12f * Mathf.Sin(angle * 2f + .5f);
            local.z += t * 12f;
            float returned = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.2f, 5.8f, t));
            float breakup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 2.2f, t));
            Vector3 flow = new Vector3(
                Mathf.Sin(local.z * .035f + t * 1.1f) - Mathf.Cos(local.y * .041f + t * .7f),
                Mathf.Sin(local.x * .039f - t * .9f) - Mathf.Cos(local.z * .029f + t * .5f),
                Mathf.Sin(local.y * .043f + t * .8f) - Mathf.Cos(local.x * .031f - t * .6f));
            local += flow * (breakup * (5f + d * 10f) * (1f - returned) *
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 22f, local.y)));
            local.y = Mathf.Max(0f, local.y) * (1f - returned);
            Vector3 side = new Vector3(forward.z, 0f, -forward.x);
            return Origin + side * local.x + Vector3.up * local.y + forward * local.z;
        }

        static float Random(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7feb352du;
                value ^= value >> 15; value *= 0x846ca68bu;
                value ^= value >> 16;
            }
            return (value & 0x00ffffffu) / 16777216f;
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!Active || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
            int count = ParticleCount;
            material.SetFloat("_DensityGain", DesktopParticles / (float)count);
            var settings = new RenderParams(material) { camera = camera,
                worldBounds = new Bounds(Origin + Vector3.up * 170f, new Vector3(1500f, 800f, 1500f)),
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderPrimitives(settings, MeshTopology.Triangles, count * 6);
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            if (material) Destroy(material);
        }
    }
}
