using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class AtlantisFinale : MonoBehaviour
    {
        const float FormationDuration = 18f;
        const int AuxiliaryLayerCount = 4;

        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly List<GameObject> optionalLayers = new();
        readonly Mesh[] desktopMeshes = new Mesh[AuxiliaryLayerCount];
        readonly Mesh[] vrMeshes = new Mesh[AuxiliaryLayerCount];
        readonly MeshFilter[] layerFilters = new MeshFilter[AuxiliaryLayerCount];
        static readonly float[] DesktopPixelFloors = { .56f, .56f, .50f, .54f };
        static readonly float[] VrPixelFloors = { .42f, .44f, .38f, .42f };
        ParticleWorld world;
        MarineLife marineLife;
        Flight flight;
        SacredFlame sacredFlame;
        Transform cityRoot;
        bool initialized, creditsStarted, densityModeInitialized;
        float elapsed;

        public bool Active { get; private set; }
        public BossId Mask { get; private set; }
        public float Formation { get; private set; }
        public int LayerCount => Active ? 1 + RunProgress.Count(Mask) : 0;
        public Bounds DestinationBounds { get; private set; }
        public Bounds SerpentPoseBounds { get; private set; }
        public ParticleCredits Credits { get; private set; }
        public bool UsesReducedDensity { get; private set; }
        public int WhaleBasePointCount => WhaleAnatomy.ParticleCount;
        public int DesktopAuxiliaryPointCount => AtlantisGeometry.ExpectedAuxiliaryPointCount(false);
        public int VrAuxiliaryPointCount => AtlantisGeometry.ExpectedAuxiliaryPointCount(true);
        public int DesktopPointCount => WhaleBasePointCount + AtlantisGeometry.ExpectedAuxiliaryPointCount(false);
        public int VrPointCount => WhaleBasePointCount + AtlantisGeometry.ExpectedAuxiliaryPointCount(true);
        public int ExpectedPointCount => UsesReducedDensity ? VrPointCount : DesktopPointCount;
        public int SerpentSchoolCount => AtlantisGeometry.SerpentSchoolCount;
        public int ActiveAuxiliaryPointCount => Active ? ContinentPointCount +
            FlamePointCount +
            (((Mask & BossId.Hermit) != 0) ? PalacePointCount : 0) +
            (((Mask & BossId.Serpent) != 0) ? SerpentPointCount : 0) +
            (((Mask & BossId.Submarine) != 0) ? SubmarinePointCount : 0) : 0;
        public int ActivePointCount => Active ? WhaleBasePointCount + ActiveAuxiliaryPointCount : 0;
        public int AuxiliaryPointCount { get; private set; }
        public int ContinentPointCount { get; private set; }
        public int PalacePointCount { get; private set; }
        public int SerpentPointCount { get; private set; }
        public int SubmarinePointCount { get; private set; }
        public int SerpentFishCount { get; private set; }
        public int FlamePointCount => sacredFlame ? sacredFlame.PointCount : 0;
        public Vector3 FlameCenter => sacredFlame ? sacredFlame.Center : Vector3.zero;
        public Color FlamePaletteColorDiagnostic => sacredFlame ?
            sacredFlame.PaletteColorDiagnostic : Color.clear;

        public void Initialize(ParticleWorld particleWorld, MarineLife marine, RunProgress runProgress)
        {
            if (initialized) throw new System.InvalidOperationException("Atlantis finale can only be initialized once.");
            if (!particleWorld || !marine || runProgress == null)
                throw new System.ArgumentException("Atlantis finale requires the particle world, marine life, and run progress.");

            world = particleWorld;
            marineLife = marine;
            Experience experience = marine.GetComponent<Experience>();
            flight = experience != null ? experience.Flight : null;
            if (!flight)
                throw new System.InvalidOperationException("Atlantis finale requires the initialized Flight controller.");
            DestinationBounds = AtlantisGeometry.DestinationBounds;
            SerpentPoseBounds = AtlantisGeometry.SerpentPoseBounds;

            Shader cityShader = Resources.Load<Shader>("AtlantisMatter");
            Shader flameShader = Resources.Load<Shader>("SacredFlame");
            Shader creditsShader = Resources.Load<Shader>("CreditsMatter");
            if (!cityShader || !flameShader || !creditsShader)
                throw new System.InvalidOperationException("Atlantis particle shaders were not found in Resources.");

            var root = new GameObject("Atlantis / persistent whale matter");
            root.transform.SetParent(world.transform, false);
            root.transform.position = AtlantisGeometry.CityOrigin;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            cityRoot = root.transform;

            BuildLayer("Atlantis / fractured continent", 0,
                AtlantisGeometry.BuildContinent(false), AtlantisGeometry.BuildContinent(true),
                cityShader, 3f, 0.65f, false);
            BuildLayer("Atlantis / Hermit restored temple", 1,
                AtlantisGeometry.BuildPalace(false), AtlantisGeometry.BuildPalace(true),
                cityShader, 0f, 1.85f, true);
            BuildLayer("Atlantis / traveling serpent schools", 2,
                AtlantisGeometry.BuildSerpentSchools(false), AtlantisGeometry.BuildSerpentSchools(true),
                cityShader, 1f, 1.0f, true);
            BuildLayer("Atlantis / retro stage and observation craft", 3,
                AtlantisGeometry.BuildSubmarines(false), AtlantisGeometry.BuildSubmarines(true),
                cityShader, 2f, 1.05f, true);

            var flameRoot = new GameObject("Atlantis / central altar sacred flame");
            flameRoot.transform.SetParent(cityRoot, false);
            sacredFlame = flameRoot.AddComponent<SacredFlame>();
            sacredFlame.Initialize(flameShader, false);

            ValidatePointBudget(false);
            ValidatePointBudget(true);
            RefreshDensityMode();

            Credits = new ParticleCredits();
            Credits.Initialize(cityRoot, creditsShader, new Vector3(210f, 64f, -20f));
            root.SetActive(false);

            marineLife.PrepareFinale(SampleWhaleDestination);
            initialized = true;
        }

        public void Begin(BossId mask, float song)
        {
            if (!initialized) throw new System.InvalidOperationException("Initialize Atlantis finale before beginning it.");
            RefreshDensityMode();

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
            if (!initialized) return;
            RefreshDensityMode();
            if (!Active) return;
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

        void BuildLayer(string name, int layerIndex, Mesh desktopMesh, Mesh vrMesh,
            Shader shader, float kind, float gain, bool optional)
        {
            desktopMeshes[layerIndex] = desktopMesh;
            vrMeshes[layerIndex] = vrMesh;
            meshes.Add(desktopMesh);
            meshes.Add(vrMesh);
            var material = new Material(shader) { name = name + " material" };
            material.SetFloat("_LayerKind", kind);
            material.SetFloat("_Gain", gain);
            material.SetFloat("_PixelFloor", DesktopPixelFloors[layerIndex]);
            material.SetFloat("_Formation", 0f);
            materials.Add(material);
            GameObject layer = PointCloud.Place(name, desktopMesh, material, cityRoot);
            layerFilters[layerIndex] = layer.GetComponent<MeshFilter>();
            if (optional)
            {
                layer.SetActive(false);
                optionalLayers.Add(layer);
            }
        }

        void RefreshDensityMode()
        {
            bool useVr = flight.VrEnabled;
            if (densityModeInitialized && UsesReducedDensity == useVr) return;

            UsesReducedDensity = useVr;
            for (int i = 0; i < AuxiliaryLayerCount; i++)
            {
                layerFilters[i].sharedMesh = useVr ? vrMeshes[i] : desktopMeshes[i];
                materials[i].SetFloat("_PixelFloor", useVr ? VrPixelFloors[i] : DesktopPixelFloors[i]);
            }

            ContinentPointCount = PointCount(useVr ? vrMeshes[0] : desktopMeshes[0]);
            PalacePointCount = PointCount(useVr ? vrMeshes[1] : desktopMeshes[1]);
            SerpentPointCount = PointCount(useVr ? vrMeshes[2] : desktopMeshes[2]);
            SubmarinePointCount = PointCount(useVr ? vrMeshes[3] : desktopMeshes[3]);
            SerpentFishCount = AtlantisGeometry.FishCount(useVr);
            AuxiliaryPointCount = CountAuxiliaryPoints(useVr);
            sacredFlame?.SetDensityMode(useVr);
            densityModeInitialized = true;
        }

        void ValidatePointBudget(bool useVr)
        {
            int pointCount = CountAuxiliaryPoints(useVr);
            int expectedCount = AtlantisGeometry.ExpectedAuxiliaryPointCount(useVr);
            int budget = useVr ? AtlantisGeometry.VrAuxiliaryPointBudget : AtlantisGeometry.AuxiliaryPointBudget;
            if (pointCount != expectedCount)
                throw new System.InvalidOperationException("Atlantis point diagnostics do not match generated mesh counts: " +
                    pointCount + " generated, " + expectedCount + " expected (VR=" + useVr + ").");
            if (pointCount > budget)
                throw new System.InvalidOperationException("Atlantis layers exceed their auxiliary point budget.");
        }

        int CountAuxiliaryPoints(bool useVr)
        {
            int pointCount = 0;
            Mesh[] lodMeshes = useVr ? vrMeshes : desktopMeshes;
            foreach (Mesh mesh in lodMeshes) pointCount += PointCount(mesh);
            return pointCount + (sacredFlame ? sacredFlame.PointCountForDensity(useVr) : 0);
        }

        static int PointCount(Mesh mesh)
        {
            return mesh ? mesh.vertexCount / 4 : 0;
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
            float beat = (float)AuthoredScore.BeatPosition(song);
            foreach (Material material in materials)
            {
                material.SetFloat("_Formation", formation);
                material.SetFloat("_Song", song);
                material.SetFloat("_Beat", beat);
            }
            sacredFlame?.SetProgress(formation, beat);
        }

        void OnDestroy()
        {
            Credits?.Dispose();
            sacredFlame?.Dispose();
            sacredFlame = null;
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
