using UnityEngine;

namespace Liminal
{
    public sealed class SacredFlame : MonoBehaviour
    {
        public const int DesktopParticleCount = 9000;
        public const int VrParticleCount = 4500;

        [Range(0.025f, 0.5f)] public float paletteCyclesPerBeat = 0.125f;
        [Range(2f, 10f)] public float flameWidth = 6f;
        [Range(12f, 32f)] public float flameHeight = 24.5f;

        Mesh desktopMesh;
        Mesh vrMesh;
        Material material;
        MeshFilter meshFilter;
        bool reducedDensity;
        bool disposed;

        public int PointCount => PointCountForDensity(reducedDensity);
        public Vector3 Center { get; private set; }
        public Color PaletteColorDiagnostic { get; private set; }

        public int PointCountForDensity(bool useVr) =>
            CountMeshPoints(useVr ? vrMesh : desktopMesh);

        public void Initialize(Shader shader, bool useVr)
        {
            if (desktopMesh || material)
                throw new System.InvalidOperationException("Sacred flame can only be initialized once.");
            if (!shader)
                throw new System.ArgumentNullException(nameof(shader));

            float ground = AtlantisGeometry.GroundHeight(0f, 0f);
            float centerY = ground + 5.45f + 7f;
            Center = transform.TransformPoint(new Vector3(0f, centerY, 0f));
            desktopMesh = BuildMesh("Atlantis / sacred flame desktop particles",
                DesktopParticleCount, centerY);
            vrMesh = BuildMesh("Atlantis / sacred flame VR particles",
                VrParticleCount, centerY);
            material = new Material(shader) { name = "Atlantis / sacred flame material" };
            material.SetFloat("_Gain", 0.88f);
            material.SetFloat("_Formation", 0f);
            material.SetFloat("_Beat", 0f);
            material.SetFloat("_PixelFloor", 0.44f);
            ApplyShape();

            GameObject particles = PointCloud.Place("Sacred flame / living embers",
                desktopMesh, material, transform);
            meshFilter = particles.GetComponent<MeshFilter>();
            SetDensityMode(useVr);
            SetProgress(0f, 0f);
        }

        public void SetDensityMode(bool useVr)
        {
            if (!meshFilter) return;
            reducedDensity = useVr;
            meshFilter.sharedMesh = useVr ? vrMesh : desktopMesh;
            material.SetFloat("_PixelFloor", useVr ? 0.34f : 0.44f);
        }

        public void SetProgress(float formation, float authoredBeat)
        {
            if (!material) return;
            float beat = Mathf.Max(0f, authoredBeat);
            material.SetFloat("_Formation", Mathf.Clamp01(formation));
            material.SetFloat("_Beat", beat);
            PaletteColorDiagnostic = EvaluatePalette(beat * paletteCyclesPerBeat + 0.036f);
        }

        void OnValidate() => ApplyShape();

        void ApplyShape()
        {
            if (!material) return;
            material.SetFloat("_ColorRate", paletteCyclesPerBeat);
            material.SetFloat("_FlameWidth", flameWidth);
            material.SetFloat("_FlameHeight", flameHeight);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (meshFilter) meshFilter.sharedMesh = null;
            DestroyResource(desktopMesh);
            DestroyResource(vrMesh);
            DestroyResource(material);
            desktopMesh = null;
            vrMesh = null;
            material = null;
            meshFilter = null;
        }

        void OnDestroy() => Dispose();

        static Mesh BuildMesh(string name, int count, float centerY)
        {
            var cloud = new PointCloud();
            for (int i = 0; i < count; i++)
            {
                uint id = (uint)i;
                float seed = Hash01(id + 0x9e3779b9u);
                float motion = Hash01(id + 0x85ebca6bu);
                float size = Mathf.Lerp(0.105f, 0.19f,
                    Hash01(id + 0xc2b2ae35u));
                cloud.Add(new Vector3(0f, centerY, 0f), size, Color.white, seed, motion);
            }
            return cloud.Build(name, 720f);
        }

        static Color EvaluatePalette(float phase)
        {
            Color azure = new(0.025f, 0.30f, 0.96f);
            Color cyan = new(0.015f, 0.84f, 0.98f);
            Color amethyst = new(0.55f, 0.075f, 0.94f);
            Color gold = new(0.98f, 0.34f, 0.025f);
            float segment = Mathf.Repeat(phase, 1f) * 4f;
            int index = Mathf.FloorToInt(segment);
            float blend = Mathf.SmoothStep(0f, 1f, segment - index);
            return index switch
            {
                0 => Color.Lerp(azure, cyan, blend),
                1 => Color.Lerp(cyan, amethyst, blend),
                2 => Color.Lerp(amethyst, gold, blend),
                _ => Color.Lerp(gold, azure, blend)
            };
        }

        static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }

        static float Hash01(uint value) => (Hash(value) & 0x00ffffffu) / 16777216f;

        static void DestroyResource(Object resource)
        {
            if (resource) UnityEngine.Object.Destroy(resource);
        }

        static int CountMeshPoints(Mesh mesh) => mesh ? mesh.vertexCount / 4 : 0;
    }
}
