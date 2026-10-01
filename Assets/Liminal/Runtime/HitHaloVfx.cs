using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class HitHaloVfx : MonoBehaviour
    {
        const int SlotCount = 32;
        const float MinLifetime = 0.55f;
        const float MaxLifetime = 0.85f;

        static readonly int HitDataId = Shader.PropertyToID("_HitData");
        static readonly int HitColorId = Shader.PropertyToID("_HitColor");
        static readonly int HitSongId = Shader.PropertyToID("_HitSong");
        static readonly int HitReducedId = Shader.PropertyToID("_HitReduced");

        sealed class Slot
        {
            public GameObject visual;
            public Renderer renderer;
            public MaterialPropertyBlock properties;
            public float until;
            public bool active;
        }

        readonly Slot[] slots = new Slot[SlotCount];
        Mesh quadMesh;
        Material material;
        int cursor;
        bool initialized;
        bool disposed;

        public void Initialize(Material source = null)
        {
            if (initialized) throw new InvalidOperationException("HitHaloVfx is already initialized.");
            if (disposed) throw new ObjectDisposedException(nameof(HitHaloVfx));

            if (source) {
                material = new Material(source);
            } else {
                Shader shader = Resources.Load<Shader>("HitHalo");
                if (!shader) shader = Shader.Find("Liminal/Hit Halo");
                if (!shader) throw new InvalidOperationException("Liminal/Hit Halo shader is missing.");
                material = new Material(shader);
            }
            material.name = "Hit halo material";

            var cloud=new PointCloud();
            for(int i=0;i<384;i++) {
                float seed=Mathf.Repeat(i*.61803399f,1);
                float angle=i*2.39996323f;
                float band=i<240?0:i<352?.4f:.8f;
                cloud.Add(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0),
                    .014f+seed*.012f,Color.white,seed,band);
            }
            quadMesh=cloud.Build("Hit halo / 384 drifting grains",2048);

            for (int i = 0; i < slots.Length; i++) {
                var visual = new GameObject("Hit halo " + i);
                visual.transform.SetParent(transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = quadMesh;
                Renderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var properties = new MaterialPropertyBlock();
                properties.SetVector(HitDataId, new Vector4(0, 0, 0, -10));
                properties.SetVector(HitColorId, Vector4.zero);
                renderer.SetPropertyBlock(properties);
                visual.SetActive(false);
                slots[i] = new Slot { visual = visual, renderer = renderer, properties = properties };
            }
            initialized = true;
        }

        public void Spawn(Vector3 position, float song, Color color, float strength)
        {
            if (!initialized || disposed || strength <= 0f) return;

            Slot slot = slots[cursor];
            cursor = (cursor + 1) % slots.Length;
            float normalizedStrength = Mathf.InverseLerp(0.35f, 2f, strength);
            slot.until = song + Mathf.Lerp(MinLifetime, MaxLifetime, normalizedStrength);
            slot.properties.SetVector(HitDataId, new Vector4(position.x, position.y, position.z, song));
            slot.properties.SetVector(HitColorId, new Vector4(color.r, color.g, color.b, strength));
            slot.renderer.SetPropertyBlock(slot.properties);
            slot.visual.transform.position = position;
            slot.active = true;
            slot.visual.SetActive(true);
        }

        public void Tick(float song, bool reduced)
        {
            if (!initialized || disposed) return;
            material.SetFloat(HitSongId, song);
            material.SetFloat(HitReducedId, reduced ? 1f : 0f);
            for (int i = 0; i < slots.Length; i++) {
                Slot slot = slots[i];
                if (!slot.active || song < slot.until) continue;
                slot.active = false;
                slot.visual.SetActive(false);
            }
        }

        public void ResetEffects()
        {
            if (!initialized || disposed) return;
            cursor = 0;
            for (int i = 0; i < slots.Length; i++) {
                slots[i].active = false;
                slots[i].visual.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int i = 0; i < slots.Length; i++) {
                Slot slot = slots[i];
                if (slot == null || !slot.visual) continue;
                MeshFilter filter = slot.visual.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh == quadMesh) filter.sharedMesh = null;
                Destroy(slot.visual);
                slot.visual = null;
            }
            if (quadMesh) Destroy(quadMesh);
            if (material) Destroy(material);
            quadMesh = null;
            material = null;
            initialized = false;
        }

        void OnDestroy() => Dispose();
    }
}
