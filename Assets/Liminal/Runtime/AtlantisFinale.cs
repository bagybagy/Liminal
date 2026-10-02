using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class AtlantisFinale : MonoBehaviour
    {
        const float FormationDuration = 18f;

        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly List<GameObject> optionalLayers = new();
        ParticleWorld world;
        MarineLife marineLife;
        Transform cityRoot;
        bool initialized, creditsStarted;
        float elapsed;

        public bool Active { get; private set; }
        public BossId Mask { get; private set; }
        public float Formation { get; private set; }
        public int LayerCount => Active ? 1 + RunProgress.Count(Mask) : 0;
        public Bounds DestinationBounds { get; private set; }
        public ParticleCredits Credits { get; private set; }

        public void Initialize(ParticleWorld particleWorld, MarineLife marine, RunProgress runProgress)
        {
            if (initialized) throw new System.InvalidOperationException("Atlantis finale can only be initialized once.");
            if (!particleWorld || !marine || runProgress == null)
                throw new System.ArgumentException("Atlantis finale requires the particle world, marine life, and run progress.");

            world = particleWorld;
            marineLife = marine;
            DestinationBounds = AtlantisGeometry.DestinationBounds;

            Shader cityShader = Resources.Load<Shader>("AtlantisMatter");
            Shader creditsShader = Resources.Load<Shader>("CreditsMatter");
            if (!cityShader || !creditsShader)
                throw new System.InvalidOperationException("Atlantis particle shaders were not found in Resources.");

            var root = new GameObject("Atlantis / persistent whale matter");
            root.transform.SetParent(world.transform, false);
            root.transform.position = AtlantisGeometry.CityOrigin;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            cityRoot = root.transform;

            BuildOptionalLayer("Atlantis / Hermit palace",
                AtlantisGeometry.BuildPalace(), cityShader, 0f, 3.4f);
            BuildOptionalLayer("Atlantis / Serpent shoals",
                AtlantisGeometry.BuildSerpentSchools(), cityShader, 1f, 3.0f);
            BuildOptionalLayer("Atlantis / observation craft",
                AtlantisGeometry.BuildSubmarines(), cityShader, 2f, 3.1f);

            int auxiliaryCount = 0;
            foreach (Mesh mesh in meshes) auxiliaryCount += mesh.vertexCount / 4;
            if (auxiliaryCount > AtlantisGeometry.AuxiliaryPointBudget)
                throw new System.InvalidOperationException("Atlantis optional layers exceed their auxiliary point budget.");

            Credits = new ParticleCredits();
            Credits.Initialize(cityRoot, creditsShader, new Vector3(207f, 88f, 8f));
            root.SetActive(false);

            marineLife.PrepareFinale(SampleWhaleDestination);
            initialized = true;
        }

        public void Begin(BossId mask, float song)
        {
            if (!initialized) throw new System.InvalidOperationException("Initialize Atlantis finale before beginning it.");

            Mask = mask & RunProgress.OptionalBosses;
            elapsed = 0f;
            Formation = 0f;
            Active = true;
            creditsStarted = false;
            Credits.Reset();
            cityRoot.gameObject.SetActive(true);
            SetOptionalLayerVisibility();
            SetFormation(0f, song);
        }

        public void Tick(float song, float dt)
        {
            if (!initialized || !Active) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            elapsed += dt;
            Formation = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / FormationDuration));
            SetFormation(Formation, song);

            if (!creditsStarted && elapsed >= FormationDuration)
            {
                Credits.Begin(song);
                creditsStarted = true;
            }
            if (creditsStarted) Credits.Tick(song, dt);
        }

        public Vector3 SampleWhaleDestination(int index) => AtlantisGeometry.SampleWhaleDestination(index);

        public void ResetFinale()
        {
            Active = false;
            Mask = BossId.None;
            Formation = 0f;
            elapsed = 0f;
            creditsStarted = false;
            Credits?.Reset();
            if (cityRoot) cityRoot.gameObject.SetActive(false);
            SetFormation(0f, 0f);
        }

        void BuildOptionalLayer(string name, Mesh mesh, Shader shader,
            float kind, float gain)
        {
            meshes.Add(mesh);
            var material = new Material(shader) { name = name + " material" };
            material.SetFloat("_LayerKind", kind);
            material.SetFloat("_Gain", gain);
            material.SetFloat("_Formation", 0f);
            materials.Add(material);
            GameObject layer = PointCloud.Place(name, mesh, material, cityRoot);
            layer.SetActive(false);
            optionalLayers.Add(layer);
        }

        void SetOptionalLayerVisibility()
        {
            if (optionalLayers.Count != 3) return;
            optionalLayers[0].SetActive((Mask & BossId.Hermit) != 0);
            optionalLayers[1].SetActive((Mask & BossId.Serpent) != 0);
            optionalLayers[2].SetActive((Mask & BossId.Submarine) != 0);
        }

        void SetFormation(float formation, float song)
        {
            foreach (Material material in materials)
            {
                material.SetFloat("_Formation", formation);
                material.SetFloat("_Song", song);
            }
        }

        void OnDestroy()
        {
            Credits?.Dispose();
            foreach (Mesh mesh in meshes)
                if (mesh) Destroy(mesh);
            foreach (Material material in materials)
                if (material) Destroy(material);
            meshes.Clear();
            materials.Clear();
            optionalLayers.Clear();
            if (cityRoot) Destroy(cityRoot.gameObject);
            cityRoot = null;
        }
    }
}
