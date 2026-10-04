using UnityEngine;

namespace Liminal
{
    public sealed class AtlantisFauna
    {
        public const int DesktopPointBudget = 50000;
        public const int VrPointBudget = 20000;
        public const int DesktopFishCount = 1568;
        public const int VrFishCount = 1120;
        public const int DesktopPufferfishCount = 28;
        public const int VrPufferfishCount = 14;
        public const int DesktopHermitCrabCount = 48;
        public const int VrHermitCrabCount = 24;

        const float Tau = Mathf.PI * 2f;
        static readonly int GainId = Shader.PropertyToID("_Gain");
        static readonly int FormationId = Shader.PropertyToID("_Formation");
        static readonly int LayerKindId = Shader.PropertyToID("_LayerKind");
        static readonly int SongId = Shader.PropertyToID("_Song");
        static readonly int BeatId = Shader.PropertyToID("_Beat");
        static readonly int PixelFloorId = Shader.PropertyToID("_PixelFloor");

        static readonly Color Cobalt = new(0.09f, 0.38f, 0.78f);
        static readonly Color Aqua = new(0.12f, 0.78f, 0.72f);
        static readonly Color Pearl = new(0.58f, 0.88f, 1f);
        static readonly Color PufferCoral = new(0.76f, 0.48f, 0.22f);
        static readonly Color CrabShell = new(0.72f, 0.43f, 0.15f);

        readonly Mesh[] shoalMeshes = new Mesh[2];
        readonly Mesh[] benthicMeshes = new Mesh[2];
        MeshFilter shoalFilter;
        MeshFilter benthicFilter;
        Material shoalMaterial;
        Material benthicMaterial;
        GameObject root;
        bool reducedDensity;
        bool disposed;

        public bool Active { get; private set; }
        public int DesktopPointCount { get; private set; }
        public int VrPointCount { get; private set; }
        public int FishCount => reducedDensity ? VrFishCount : DesktopFishCount;
        public int PufferfishCount => reducedDensity ? VrPufferfishCount : DesktopPufferfishCount;
        public int HermitCrabCount => reducedDensity ? VrHermitCrabCount : DesktopHermitCrabCount;
        public int ActiveFishCount => Active ? FishCount : 0;
        public int ActiveFaunaCount => Active ? FishCount + PufferfishCount + HermitCrabCount : 0;
        public int ActivePointCount => Active ? (reducedDensity ? VrPointCount : DesktopPointCount) : 0;
        public string BudgetReport { get; private set; } = "not initialized";

        public void Initialize(Transform parent, Shader shader, bool useVr)
        {
            if (root || disposed)
                throw new System.InvalidOperationException("Atlantis fauna can only be initialized once.");
            if (!parent || !shader)
                throw new System.ArgumentException("Atlantis fauna requires its parent and particle shader.");

            shoalMeshes[0] = BuildShoals(false);
            shoalMeshes[1] = BuildShoals(true);
            benthicMeshes[0] = BuildHermitCrabs(false);
            benthicMeshes[1] = BuildHermitCrabs(true);
            DesktopPointCount = PointCount(shoalMeshes[0]) + PointCount(benthicMeshes[0]);
            VrPointCount = PointCount(shoalMeshes[1]) + PointCount(benthicMeshes[1]);
            ValidateBudget(DesktopPointCount, DesktopPointBudget, "desktop");
            ValidateBudget(VrPointCount, VrPointBudget, "VR");
            BudgetReport = "Desktop " + DesktopPointCount + "/" + DesktopPointBudget +
                " points; VR " + VrPointCount + "/" + VrPointBudget + " points";

            root = new GameObject("Atlantis / all-boss peaceful fauna");
            root.transform.SetParent(parent, false);
            shoalMaterial = CreateMaterial(shader, "Atlantis / celebratory shoals", 1f, 2.3f);
            benthicMaterial = CreateMaterial(shader, "Atlantis / walking hermit crabs", 4f, 1.85f);
            shoalFilter = PointCloud.Place("Atlantis / expanded fish and puffer schools",
                shoalMeshes[0], shoalMaterial, root.transform).GetComponent<MeshFilter>();
            benthicFilter = PointCloud.Place("Atlantis / peaceful seabed hermit crabs",
                benthicMeshes[0], benthicMaterial, root.transform).GetComponent<MeshFilter>();
            SetDensityMode(useVr);
            SetProgress(0f, 0f, 0f);
            root.SetActive(false);
        }

        public void Begin(bool enabled)
        {
            Active = enabled && root != null;
            if (root) root.SetActive(Active);
        }

        public void SetDensityMode(bool useVr)
        {
            if (!shoalFilter || !benthicFilter) return;
            reducedDensity = useVr;
            shoalFilter.sharedMesh = shoalMeshes[useVr ? 1 : 0];
            benthicFilter.sharedMesh = benthicMeshes[useVr ? 1 : 0];
            shoalMaterial.SetFloat(PixelFloorId, useVr ? 0.66f : 0.75f);
            benthicMaterial.SetFloat(PixelFloorId, useVr ? 0.62f : 0.72f);
        }

        public void SetProgress(float formation, float song, float beat)
        {
            if (!shoalMaterial || !benthicMaterial) return;
            SetMaterialProgress(shoalMaterial, formation, song, beat);
            SetMaterialProgress(benthicMaterial, formation, song, beat);
        }

        public void Reset()
        {
            Active = false;
            if (root) root.SetActive(false);
            SetProgress(0f, 0f, 0f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Reset();
            if (shoalFilter) shoalFilter.sharedMesh = null;
            if (benthicFilter) benthicFilter.sharedMesh = null;
            DestroyResource(root);
            root = null;
            for (int i = 0; i < shoalMeshes.Length; i++)
            {
                DestroyResource(shoalMeshes[i]);
                DestroyResource(benthicMeshes[i]);
                shoalMeshes[i] = null;
                benthicMeshes[i] = null;
            }
            DestroyResource(shoalMaterial);
            DestroyResource(benthicMaterial);
            shoalMaterial = null;
            benthicMaterial = null;
            shoalFilter = null;
            benthicFilter = null;
        }

        static Material CreateMaterial(Shader shader, string name, float kind, float gain)
        {
            var material = new Material(shader) { name = name };
            material.SetFloat(GainId, gain);
            material.SetFloat(LayerKindId, kind);
            material.SetFloat(FormationId, 0f);
            material.SetFloat(SongId, 0f);
            material.SetFloat(BeatId, 0f);
            return material;
        }

        static void SetMaterialProgress(Material material, float formation, float song, float beat)
        {
            material.SetFloat(FormationId, Mathf.Clamp01(formation));
            material.SetFloat(SongId, song);
            material.SetFloat(BeatId, beat);
        }

        static Mesh BuildShoals(bool useVr)
        {
            var cloud = new PointCloud();
            uint identity = 0x4f1bbcdcu;
            int fishPerSchool = (useVr ? VrFishCount : DesktopFishCount) / AtlantisGeometry.SerpentSchoolCount;
            int branchSize = fishPerSchool / 2;
            for (int school = 0; school < AtlantisGeometry.SerpentSchoolCount; school++)
            {
                AtlantisGeometry.SchoolPose pose = AtlantisGeometry.EvaluateSchoolPose(school, 0f);
                Quaternion rotation = Quaternion.Euler(0f, pose.HeadingRadians * Mathf.Rad2Deg, 0f);
                Vector3 forward = rotation * Vector3.forward;
                Vector3 side = rotation * Vector3.right;
                for (int fish = 0; fish < fishPerSchool; fish++)
                {
                    int branch = fish / branchSize;
                    int member = fish % branchSize;
                    float along = ((member + 0.5f) / branchSize - 0.5f) * 92f;
                    float lane = (member % 7 - 3) * 1.05f + Mathf.Sin(member * 0.37f + school) * 0.7f;
                    float branchOffset = branch == 0 ? -30f : 30f;
                    float height = Mathf.Sin(member * 0.19f + school * 0.83f + branch) * 1.1f;
                    Vector3 center = pose.Center + forward * along + side * (branchOffset + lane) +
                        Vector3.up * height;
                    AddSchoolFish(cloud, center, forward, side, school, fish, useVr, ref identity);
                }

                int puffersPerSchool = useVr ? 2 : 4;
                for (int puffer = 0; puffer < puffersPerSchool; puffer++)
                {
                    float along = (puffer - (puffersPerSchool - 1) * 0.5f) * 18f;
                    float lane = puffer % 2 == 0 ? 24f : -24f;
                    Vector3 center = pose.Center + forward * along + side * lane + Vector3.down * 6f;
                    AddPufferfish(cloud, center, rotation, school, useVr, ref identity);
                }
            }
            return cloud.Build("Atlantis / expanded fish and puffer schools", 720f);
        }

        static Mesh BuildHermitCrabs(bool useVr)
        {
            var cloud = new PointCloud();
            uint identity = 0xb5297a4du;
            int count = useVr ? VrHermitCrabCount : DesktopHermitCrabCount;
            for (int crab = 0; crab < count; crab++)
            {
                float angle = crab * 2.3999632f;
                Quaternion rotation = Quaternion.Euler(0f,
                    (angle + Mathf.Sin(crab * 1.71f) * 0.45f) * Mathf.Rad2Deg, 0f);
                AddHermitCrab(cloud, HermitCenter(crab, useVr), rotation, crab + 1, useVr, ref identity);
            }
            return cloud.Build("Atlantis / walking hermit crabs", 720f);
        }

        static void AddSchoolFish(PointCloud cloud, Vector3 center, Vector3 forward,
            Vector3 side, int school, int fish, bool useVr, ref uint identity)
        {
            Color body = fish % 13 == 0 ? new Color(0.48f, 0.72f, 0.64f) :
                Color.Lerp(Cobalt, Aqua, Hash01((uint)(fish * 17 + school * 101)) * 0.72f);
            int segments = useVr ? 3 : 4;
            for (int segment = 0; segment < segments; segment++)
            {
                float t = segment / (float)(segments - 1);
                float z = Mathf.Lerp(1.9f, -1.5f, t);
                float width = 0.14f + Mathf.Sin(t * Mathf.PI) * 0.54f;
                AddPoint(cloud, center + forward * z, 0.28f, body, school, ref identity);
                AddPoint(cloud, center + forward * z + side * width, 0.25f, body, school, ref identity);
                AddPoint(cloud, center + forward * z - side * width, 0.25f, body, school, ref identity);
            }
            AddPoint(cloud, center - forward * 1.55f, 0.23f, Aqua, school, ref identity);
            AddPoint(cloud, center - forward * 2.3f + side * 0.7f, 0.25f, Aqua, school, ref identity);
            AddPoint(cloud, center - forward * 2.3f - side * 0.7f, 0.25f, Aqua, school, ref identity);
            if (!useVr) AddPoint(cloud, center - forward * 1.95f, 0.23f, Aqua, school, ref identity);
            AddPoint(cloud, center + forward * 1.7f + Vector3.up * 0.18f, 0.24f, Pearl, school, ref identity);
        }

        static void AddPufferfish(PointCloud cloud, Vector3 center, Quaternion rotation,
            int school, bool useVr, ref uint identity)
        {
            int rings = useVr ? 4 : 6;
            for (int ring = 0; ring < rings; ring++)
            {
                float latitude = Mathf.Lerp(-1.05f, 1.05f, ring / (float)(rings - 1));
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Tau / 16f + ring * 0.12f;
                    float radius = 2.5f * Mathf.Cos(latitude) + (i % 4 == 0 ? .3f : 0f);
                    Vector3 local = new(Mathf.Cos(angle) * radius, Mathf.Sin(latitude) * 2.5f,
                        Mathf.Sin(angle) * radius);
                    Color color = (i + ring) % 5 == 0 ? Pearl :
                        Color.Lerp(PufferCoral, Aqua, ((i * 3 + ring) % 9) * 0.075f);
                    AddPoint(cloud, center + rotation * local, 0.26f, color, school, ref identity);
                }
            }
        }

        internal static Vector3 HermitCenter(int crab, bool useVr)
        {
            int count = useVr ? VrHermitCrabCount : DesktopHermitCrabCount;
            float radius = Mathf.Lerp(106f, 254f, Mathf.Sqrt((crab + 0.5f) / count));
            float angle = crab * 2.3999632f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(x, AtlantisGeometry.GroundHeight(x, z) + .7f, z);
        }

        static void AddHermitCrab(PointCloud cloud, Vector3 center, Quaternion rotation,
            int motionId, bool useVr, ref uint identity)
        {
            Color shell = Color.Lerp(CrabShell, Aqua, (motionId % 5) * 0.045f);
            int shellPoints = useVr ? 72 : 192;
            for (int point = 0; point < shellPoints; point++)
            {
                float t = Mathf.Pow((point + .5f) / shellPoints, .62f);
                Vector3 local = HermitGeometry.ShellSurface(t, point * 2.3999632f) * .85f;
                AddPoint(cloud, center + rotation * local, .25f,
                    point % 11 == 0 ? Pearl : shell, motionId, ref identity);
            }
            int legPoints = useVr ? 10 : 20;
            for (int leg = 0; leg < 6; leg++)
            {
                for (int point = 0; point < legPoints; point++)
                {
                    float t = point / (float)(legPoints - 1);
                    Vector3 local = HermitGeometry.LegSurface(leg, t, HermitGeometry.RestFoot(leg)) * .85f;
                    AddPoint(cloud, center + rotation * local, .24f,
                        point % 7 == 0 ? Pearl : Aqua,
                        LegMotionId(motionId, leg, t < .52f), ref identity);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                int clawPoints = useVr ? 12 : 32;
                for (int point = 0; point < clawPoints; point++)
                {
                    float t = (point / 2) / (float)(clawPoints / 2 - 1);
                    Vector3 local = Vector3.Lerp(new Vector3(side * 1.45f, .93f, 1.4f),
                        new Vector3(side * 3.95f, 1.1f + (point % 2 == 0 ? .2f : -.2f), 3.8f), t) * .85f;
                    AddPoint(cloud, center + rotation * local, .25f, Aqua, motionId, ref identity);
                }
                int eyePoints = useVr ? 6 : 12;
                for (int point = 0; point < eyePoints; point++)
                {
                    float t = point / (float)(eyePoints - 1);
                    Vector3 local = new(side * (.62f + .08f * t), 1.15f + 1.12f * t, 2.1f + .55f * t);
                    AddPoint(cloud, center + rotation * local * .85f, .23f,
                        point == eyePoints - 1 ? Pearl : Aqua, motionId, ref identity);
                }
            }
        }

        static void AddPoint(PointCloud cloud, Vector3 position, float size, Color color,
            float motionId, ref uint identity)
        {
            uint id = identity++;
            float seed = Hash01(id + 0x9e3779b9u);
            float sizeVariation = 0.88f + Hash01(id + 0x85ebca6bu) * 0.24f;
            cloud.Add(position, size * sizeVariation, color, seed, motionId);
        }

        static float LegMotionId(int motionId, int legIndex, bool knee)
        {
            int roleCode = 1 + legIndex * 2 + (knee ? 0 : 1);
            return motionId + roleCode / 16f;
        }

        static float Hash01(uint value) => (Hash(value) & 0x00ffffffu) / 16777216f;

        static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }

        static int PointCount(Mesh mesh) => mesh ? mesh.vertexCount / 4 : 0;

        static void ValidateBudget(int pointCount, int budget, string label)
        {
            if (pointCount > budget)
                throw new System.InvalidOperationException("Atlantis " + label +
                    " celebration fauna exceeds its point budget: " + pointCount + "/" + budget + ".");
        }

        static void DestroyResource(Object resource)
        {
            if (resource) UnityEngine.Object.Destroy(resource);
        }
    }
}
