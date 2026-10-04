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

        static readonly Color Cobalt = new(0.055f, 0.18f, 0.34f);
        static readonly Color Aqua = new(0.09f, 0.39f, 0.42f);
        static readonly Color Pearl = new(0.50f, 0.68f, 0.70f);
        static readonly Color PufferCoral = new(0.34f, 0.25f, 0.16f);
        static readonly Color CrabShell = new(0.30f, 0.23f, 0.16f);

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
            shoalMaterial = CreateMaterial(shader, "Atlantis / celebratory shoals", 1f, 0.56f);
            benthicMaterial = CreateMaterial(shader, "Atlantis / walking hermit crabs", 4f, 0.48f);
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
            shoalMaterial.SetFloat(PixelFloorId, useVr ? 0.40f : 0.48f);
            benthicMaterial.SetFloat(PixelFloorId, useVr ? 0.38f : 0.44f);
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
                    AddSchoolFish(cloud, center, forward, side, school, fish, ref identity);
                }

                int puffersPerSchool = useVr ? 2 : 4;
                for (int puffer = 0; puffer < puffersPerSchool; puffer++)
                {
                    float along = (puffer - (puffersPerSchool - 1) * 0.5f) * 18f;
                    float lane = puffer % 2 == 0 ? 24f : -24f;
                    Vector3 center = pose.Center + forward * along + side * lane + Vector3.down * 6f;
                    AddPufferfish(cloud, center, rotation, school, ref identity);
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
                float u = (crab + 0.5f) / count;
                float angle = crab * 2.3999632f;
                float radius = Mathf.Lerp(106f, 254f, Mathf.Sqrt(u));
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                Vector3 center = new(x, AtlantisGeometry.GroundHeight(x, z) + 0.14f, z);
                Quaternion rotation = Quaternion.Euler(0f,
                    angle * Mathf.Rad2Deg + Mathf.Sin(crab * 1.71f) * 0.45f, 0f);
                AddHermitCrab(cloud, center, rotation, crab + 1, ref identity);
            }
            return cloud.Build("Atlantis / walking hermit crabs", 720f);
        }

        static void AddSchoolFish(PointCloud cloud, Vector3 center, Vector3 forward,
            Vector3 side, int school, int fish, ref uint identity)
        {
            Color body = fish % 13 == 0 ? new Color(0.22f, 0.29f, 0.28f) :
                Color.Lerp(Cobalt, Aqua, Hash01((uint)(fish * 17 + school * 101)) * 0.72f);
            AddPoint(cloud, center + forward * 0.82f, 0.13f, Pearl, school, ref identity);
            AddPoint(cloud, center + forward * 0.22f, 0.16f, body, school, ref identity);
            AddPoint(cloud, center - forward * 0.30f, 0.145f, body, school, ref identity);
            AddPoint(cloud, center + forward * 0.20f + side * 0.30f, 0.13f, body, school, ref identity);
            AddPoint(cloud, center + forward * 0.20f - side * 0.30f, 0.13f, body, school, ref identity);
            AddPoint(cloud, center - forward * 0.48f + side * 0.16f, 0.12f, body, school, ref identity);
            AddPoint(cloud, center - forward * 0.48f - side * 0.16f, 0.12f, body, school, ref identity);
            AddPoint(cloud, center - forward * 0.72f + side * 0.34f, 0.105f, Aqua, school, ref identity);
            AddPoint(cloud, center - forward * 0.72f - side * 0.34f, 0.105f, Aqua, school, ref identity);
            AddPoint(cloud, center + Vector3.up * 0.18f, 0.10f, Pearl, school, ref identity);
            AddPoint(cloud, center - forward * 0.22f - Vector3.up * 0.14f, 0.10f, body, school, ref identity);
        }

        static void AddPufferfish(PointCloud cloud, Vector3 center, Quaternion rotation,
            int school, ref uint identity)
        {
            for (int ring = 0; ring < 3; ring++)
            {
                float y = (ring - 1) * 0.52f;
                float radius = ring == 1 ? 0.98f : 0.78f;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Tau / 8f;
                    Vector3 local = new(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                    Color color = (i + ring) % 5 == 0 ? Pearl :
                        Color.Lerp(PufferCoral, Aqua, ((i * 3 + ring) % 9) * 0.075f);
                    AddPoint(cloud, center + rotation * local, 0.15f, color, school, ref identity);
                }
            }

            for (int spike = 0; spike < 8; spike++)
            {
                float angle = spike * Tau / 8f + 0.18f;
                Vector3 local = new(Mathf.Cos(angle) * 1.12f, 0.12f, Mathf.Sin(angle) * 1.12f);
                AddPoint(cloud, center + rotation * local, 0.11f, Pearl, school, ref identity);
            }
            AddPoint(cloud, center + rotation * new Vector3(-0.27f, 0.28f, 0.82f),
                0.12f, Pearl, school, ref identity);
            AddPoint(cloud, center + rotation * new Vector3(0.27f, 0.28f, 0.82f),
                0.12f, Pearl, school, ref identity);
            AddPoint(cloud, center + rotation * new Vector3(0f, -0.14f, 1.02f),
                0.10f, PufferCoral, school, ref identity);
            AddPoint(cloud, center - rotation * Vector3.forward * 1.12f,
                0.14f, Aqua, school, ref identity);
        }

        static void AddHermitCrab(PointCloud cloud, Vector3 center, Quaternion rotation,
            int motionId, ref uint identity)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 side = rotation * Vector3.right;
            Vector3 up = Vector3.up;
            Color shell = Color.Lerp(CrabShell, Aqua, (motionId % 5) * 0.045f);
            AddPoint(cloud, center + up * 0.40f, 0.18f, shell, motionId, ref identity);
            AddPoint(cloud, center + forward * 0.16f + up * 0.42f, 0.16f, shell, motionId, ref identity);
            AddPoint(cloud, center - forward * 0.16f + up * 0.38f, 0.15f, shell, motionId, ref identity);
            AddPoint(cloud, center + side * 0.28f + up * 0.35f, 0.14f, shell, motionId, ref identity);
            AddPoint(cloud, center - side * 0.28f + up * 0.35f, 0.14f, shell, motionId, ref identity);

            for (int crabSide = -1; crabSide <= 1; crabSide += 2)
            {
                for (int leg = 0; leg < 3; leg++)
                {
                    float z = (leg - 1) * 0.34f;
                    Vector3 rootPoint = center + side * (crabSide * 0.30f) + forward * z + up * 0.20f;
                    Vector3 knee = center + side * (crabSide * 0.72f) +
                        forward * (z + 0.13f) + up * 0.10f;
                    Vector3 foot = center + side * (crabSide * 0.99f) +
                        forward * (z - 0.08f) + up * 0.03f;
                    AddPoint(cloud, rootPoint, 0.11f, shell, motionId, ref identity);
                    int legIndex = crabSide < 0 ? leg : leg + 3;
                    AddPoint(cloud, knee, 0.10f, shell,
                        LegMotionId(motionId, legIndex, true), ref identity);
                    AddPoint(cloud, foot, 0.095f, Pearl,
                        LegMotionId(motionId, legIndex, false), ref identity);
                }

                Vector3 clawRoot = center + side * (crabSide * 0.38f) + forward * 0.38f + up * 0.19f;
                Vector3 clawTip = center + side * (crabSide * 0.62f) + forward * 0.67f + up * 0.22f;
                AddPoint(cloud, clawRoot, 0.12f, shell, motionId, ref identity);
                AddPoint(cloud, clawTip, 0.13f, Pearl, motionId, ref identity);
                AddPoint(cloud, clawTip + side * (crabSide * 0.13f), 0.105f, shell, motionId, ref identity);

                Vector3 eyeStalk = center + side * (crabSide * 0.15f) + forward * 0.38f + up * 0.46f;
                AddPoint(cloud, eyeStalk, 0.09f, shell, motionId, ref identity);
                AddPoint(cloud, eyeStalk + up * 0.12f, 0.085f, Pearl, motionId, ref identity);
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
