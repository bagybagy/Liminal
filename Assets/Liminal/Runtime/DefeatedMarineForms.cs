using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class DefeatedMarineForms : MonoBehaviour
    {
        public const int DefaultCapacity = 96;

        static readonly int SourceMatrixId = Shader.PropertyToID("_SourceLocalToWorld");
        static readonly int TargetRootId = Shader.PropertyToID("_TargetRoot");
        static readonly int ColonyRightId = Shader.PropertyToID("_ColonyRight");
        static readonly int ColonyForwardId = Shader.PropertyToID("_ColonyForward");
        static readonly int RoomColorId = Shader.PropertyToID("_RoomColor");
        static readonly int RoomAccentId = Shader.PropertyToID("_RoomAccent");
        static readonly int SourceTintId = Shader.PropertyToID("_SourceTint");
        static readonly int SourceGainId = Shader.PropertyToID("_SourceGain");
        static readonly int DeathSongId = Shader.PropertyToID("_DeathSong");
        static readonly int ElapsedId = Shader.PropertyToID("_Elapsed");
        static readonly int TransferDurationId = Shader.PropertyToID("_TransferDuration");
        static readonly int SourceVelocityId = Shader.PropertyToID("_SourceVelocity");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int GainId = Shader.PropertyToID("_Gain");
        const float GoldenAngle = 2.39996323f;

        sealed class Colony
        {
            public GameObject visual;
            public Renderer renderer;
            public Material sourceMaterial;
            public MaterialPropertyBlock sourceProperties;
            public Bounds sourceBounds;
            public Matrix4x4 sourceMatrix;
            public Vector3 root, velocity;
            public Color roomColor, roomAccent, sourceTint;
            public float sourceGain, deathSong, transferDuration;
        }

        readonly List<Colony> colonies = new();
        readonly HashSet<GameObject> reservations = new();
        MaterialPropertyBlock properties;
        Mesh sourceMesh;
        Material fallbackSourceMaterial, colonyMaterial;
        bool initialized;

        public int Capacity { get; private set; } = DefaultCapacity;
        public int Count => colonies.Count;
        public int ReservedCount => reservations.Count;
        public int AvailableCapacity => Mathf.Max(0, Capacity - Count - ReservedCount);
        public Vector3 RootAt(int index) => colonies[index].root;
        public bool IsSettled(int index, float songTime) => songTime - colonies[index].deathSong >= colonies[index].transferDuration;
        public float TransferDurationAt(int index) => colonies[index].transferDuration;
        public static float TransferSeconds(float distance) => 2f+Mathf.Max(8f,distance/8f);

        public void Initialize(Mesh rayMesh, Material rayMaterial, int capacity = DefaultCapacity)
        {
            properties = new MaterialPropertyBlock();
            if (initialized) throw new InvalidOperationException("DefeatedMarineForms is already initialized.");
            if (!rayMesh) throw new ArgumentNullException(nameof(rayMesh));

            Shader shader = Resources.Load<Shader>("DefeatedMarineForms");
            if (!shader) shader = Shader.Find("Liminal/Defeated Marine Forms");
            if (!shader) throw new InvalidOperationException("Liminal/Defeated Marine Forms shader is missing.");

            sourceMesh = rayMesh;
            fallbackSourceMaterial = rayMaterial;
            Capacity = Mathf.Clamp(capacity, 0, DefaultCapacity);
            colonyMaterial = new Material(shader) { name = "Defeated marine colonies" };
            initialized = true;
        }

        public bool CanReserve(int amount = 1) => initialized && amount >= 0 && amount <= AvailableCapacity;

        public bool TryReserve(GameObject rayVisual)
        {
            if (!initialized || !rayVisual || reservations.Contains(rayVisual) || AvailableCapacity == 0)
                return false;
            MeshFilter filter = rayVisual.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh || filter.sharedMesh != sourceMesh || !rayVisual.GetComponent<Renderer>())
                return false;
            return reservations.Add(rayVisual);
        }

        public bool Release(GameObject rayVisual)
        {
            return rayVisual && reservations.Remove(rayVisual);
        }

        public bool TryAdopt(GameObject rayVisual, float songTime, int roomIndex, Vector3 velocity=default)
        {
            if (!initialized || !rayVisual || roomIndex < 0 || roomIndex >= CaveLayout.Rooms.Length)
                return false;

            MeshFilter filter = rayVisual.GetComponent<MeshFilter>();
            Renderer renderer = rayVisual.GetComponent<Renderer>();
            if (!filter || filter.sharedMesh != sourceMesh || !renderer)
                return false;

            bool reserved = reservations.Contains(rayVisual);
            if (!reserved && AvailableCapacity == 0)
                return false;

            int seed = StableSeed(rayVisual.transform.position, songTime, roomIndex);
            if (!TryFindRoot(rayVisual.transform.position, roomIndex, seed, out Vector3 root))
                return false;

            Material originalMaterial = renderer.sharedMaterial ? renderer.sharedMaterial : fallbackSourceMaterial;
            var sourceProperties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(sourceProperties);
            Color sourceTint = originalMaterial && originalMaterial.HasProperty(TintId)
                ? originalMaterial.GetColor(TintId) : Color.white;
            float sourceGain = originalMaterial && originalMaterial.HasProperty(GainId)
                ? originalMaterial.GetFloat(GainId) : 1f;
            Vector3 sourcePosition = rayVisual.transform.position;
            Bounds sourceBounds = renderer.bounds;
            var bounds = new Bounds(sourcePosition, Vector3.zero);
            bounds.Encapsulate(root);
            bounds.Expand(36f);

            var room = CaveLayout.Rooms[roomIndex];
            var colony = new Colony {
                visual = rayVisual,
                renderer = renderer,
                sourceMaterial = originalMaterial,
                sourceProperties = sourceProperties,
                sourceBounds = sourceBounds,
                sourceMatrix = rayVisual.transform.localToWorldMatrix,
                root = root,
                roomColor = room.Color,
                roomAccent = room.Accent,
                sourceTint = sourceTint,
                sourceGain = sourceGain,
                deathSong = songTime,
                velocity = Vector3.ClampMagnitude(velocity,8f),
                transferDuration = TransferSeconds(Vector3.Distance(sourcePosition,root))
            };

            renderer.sharedMaterial = colonyMaterial;
            renderer.bounds = bounds;
            ApplyProperties(colony, 0f);
            colonies.Add(colony);
            if (reserved) reservations.Remove(rayVisual);
            return true;
        }

        public void Tick(float songTime)
        {
            if (!initialized) return;
            for (int i = 0; i < colonies.Count; i++) {
                Colony colony = colonies[i];
                if (!colony.visual || !colony.renderer) continue;
                ApplyProperties(colony, Mathf.Max(0f, songTime - colony.deathSong));
            }
        }

        void ApplyProperties(Colony colony, float elapsed)
        {
            properties.Clear();
            properties.SetMatrix(SourceMatrixId, colony.sourceMatrix);
            properties.SetVector(TargetRootId, colony.root);
            properties.SetVector(ColonyRightId, Vector3.right);
            properties.SetVector(ColonyForwardId, Vector3.forward);
            properties.SetColor(RoomColorId, colony.roomColor);
            properties.SetColor(RoomAccentId, colony.roomAccent);
            properties.SetColor(SourceTintId, colony.sourceTint);
            properties.SetFloat(SourceGainId, colony.sourceGain);
            properties.SetFloat(DeathSongId, colony.deathSong);
            properties.SetFloat(ElapsedId, elapsed);
            properties.SetFloat(TransferDurationId, colony.transferDuration);
            properties.SetVector(SourceVelocityId, colony.velocity);
            colony.renderer.SetPropertyBlock(properties);
        }

        static int StableSeed(Vector3 position, float songTime, int roomIndex)
        {
            return Mathf.RoundToInt(position.x * 17f + position.y * 11f + position.z * 31f + songTime * 97f) + roomIndex * 7919;
        }

        static bool TryFindRoot(Vector3 source, int roomIndex, int seed, out Vector3 root)
        {
            var room = CaveLayout.Rooms[roomIndex];
            Vector2 normalized = new((source.x - room.Center.x) / room.Radius.x,
                (source.z - room.Center.z) / room.Radius.z);
            float baseAngle = normalized.sqrMagnitude > 0.0025f
                ? Mathf.Atan2(normalized.y, normalized.x)
                : (Mathf.Abs(seed % 10000) / 10000f) * Mathf.PI * 2f;
            float baseRadius = Mathf.Clamp(normalized.magnitude, 0.56f, 0.78f);
            Vector3 passage = CaveLayout.ForwardWaypoint(room.Center, roomIndex) - room.Center;
            passage.y = 0f;
            if (passage.sqrMagnitude > 0.001f) passage.Normalize();

            for (int attempt = 0; attempt < 72; attempt++) {
                float angle = baseAngle + attempt * GoldenAngle + (seed % 29) * 0.013f;
                int radialStep = (attempt * 7 + Mathf.Abs(seed % 9)) % 9;
                float radial = Mathf.Clamp(baseRadius + (radialStep - 4) * 0.014f, 0.53f, 0.82f);
                Vector3 horizontal = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Vector3.Dot(horizontal, passage) > 0.52f) continue;
                float normalizedRadiusSquared = radial * radial;
                float floor = -Mathf.Sqrt(1f - normalizedRadiusSquared) * room.Radius.y + 2f;
                Vector3 candidate = room.Center + new Vector3(horizontal.x * radial * room.Radius.x, floor,
                    horizontal.z * radial * room.Radius.z);
                if (CaveLayout.InPassage(candidate, -10f)) continue;
                root = candidate;
                return true;
            }

            root = default;
            return false;
        }

        public void Reset()
        {
            for (int i = colonies.Count - 1; i >= 0; i--) {
                Colony colony = colonies[i];
                if (colony.renderer) {
                    colony.renderer.sharedMaterial = colony.sourceMaterial;
                    colony.renderer.SetPropertyBlock(colony.sourceProperties);
                    colony.renderer.bounds = colony.sourceBounds;
                }
                if (colony.visual) Destroy(colony.visual);
            }
            colonies.Clear();
            reservations.Clear();
        }

        public void Dispose()
        {
            Reset();
            if (colonyMaterial) Destroy(colonyMaterial);
            colonyMaterial = null;
            sourceMesh = null;
            fallbackSourceMaterial = null;
            initialized = false;
        }

        void OnDestroy() => Dispose();
    }
}
