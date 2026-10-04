using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class ParticleCredits : IDisposable
    {
        public const float TargetDuration = 180f;
        public const float GatherDuration = 0.25f;
        public const float ScrollDuration = 8f;
        public const float ScrollDistance = 96f;
        public const float ScatterDuration = 1.2f;
        public const float ReturnDuration = 1.8f;
        public const float LineDuration = GatherDuration + ScrollDuration + ScatterDuration + ReturnDuration;

        readonly struct CreditLine
        {
            public readonly string Text, HudText;
            public readonly float Cell, Size, Gain;
            public readonly Color Color;
            public readonly bool Heading;

            public CreditLine(string text, string hudText, float maxCell,
                bool heading = false, bool featured = false)
            {
                Text = text;
                HudText = hudText;
                Heading = heading;
                Cell = Mathf.Min(0.55f,
                    Mathf.Min(maxCell, 150f / Mathf.Max(6f, text.Length * 6f)));
                Size = Cell * (featured ? 0.35f : 0.36f);
                Gain = featured ? 5.2f : heading ? 3.5f : 4f;
                Color = featured ? new Color(1f, 0.79f, 0.35f) :
                    heading ? new Color(0.38f, 0.87f, 1f) : new Color(0.82f, 1f, 0.94f);
            }
        }

        static readonly StaffCreditRoster Roster;
        static readonly CreditLine[] Lines;
        static readonly int PoolSize;

        static ParticleCredits()
        {
            Roster = StaffCreditRoster.Load(Resources.Load<TextAsset>("ProductionCredits"));
            StaffCreditRoster.DisplayLine[] rows = Roster.BuildDisplayLines();
            if (rows.Length < 2)
                throw new InvalidOperationException("Production credits need at least two display lines.");
            Lines = new CreditLine[rows.Length];
            float maxCell = ScrollDistance * Mathf.Max(0f, LineInterval - GatherDuration) /
                ScrollDuration * 0.75f / 7f;
            for (int i = 0; i < rows.Length; i++)
                Lines[i] = new CreditLine(rows[i].ParticleText, rows[i].Text, maxCell,
                    rows[i].Heading, rows[i].Featured);
            PoolSize = Mathf.Max(1, Mathf.CeilToInt(LineDuration / LineInterval));
        }

        readonly List<Mesh> lineMeshes = new();
        readonly MeshFilter[] meshFilters;
        readonly MeshRenderer[] meshRenderers;
        readonly MaterialPropertyBlock[] blocks;
        readonly int[] slotLines;
        Material material;
        GameObject root;
        bool[] seen;
        float elapsed;
        bool disposed;

        public ParticleCredits()
        {
            meshFilters = new MeshFilter[PoolSize];
            meshRenderers = new MeshRenderer[PoolSize];
            blocks = new MaterialPropertyBlock[PoolSize];
            slotLines = new int[PoolSize];
        }

        public static int LineCount => Lines.Length;
        public static int ContributorCount => Roster.ContributorCount;
        public static float LineInterval => (TargetDuration - LineDuration) / (LineCount - 1f);
        public static float Duration => (LineCount - 1) * LineInterval + LineDuration;
        public static int PoolSlotCount => PoolSize;
        public static float PoolCyclePeriod => PoolSize * LineInterval;
        public static int MaximumVisibleLineCount => Mathf.CeilToInt((GatherDuration + ScrollDuration) / LineInterval);
        public static string GetLineText(int index) => Lines[index].HudText;
        public static string GetParticleLineText(int index) => Lines[index].Text;
        public static Color GetLineColor(int index) => Lines[index].Color;
        public static bool IsHeading(int index) => Lines[index].Heading;
        public bool Active { get; private set; }
        public bool Completed { get; private set; }
        public int CycleCount { get; private set; }
        public int StableParticleCount { get; private set; }
        public int PointCount => StableParticleCount;
        public int ParticlesPerLine { get; private set; }
        public int SeenLines { get; private set; }
        public float Elapsed => elapsed;
        public Vector3 Position => root ? root.transform.position : Vector3.zero;
        public int VisibleLineCount { get; private set; }

        public void Initialize(Transform parent, Shader shader, Vector3 localPosition)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ParticleCredits));
            if (root) throw new InvalidOperationException("Particle credits can only be initialized once.");
            if (!parent || !shader) throw new ArgumentException("Particle credits require a parent and shader.");

            root = new GameObject("Atlantis credits / world-fixed staff roll");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            material = new Material(shader) { name = "Atlantis circulating credit matter" };
            BuildLineMeshes();
            for (int i = 0; i < PoolSize; i++)
            {
                var slot = new GameObject("Circulating glyph pool / " + i);
                slot.transform.SetParent(root.transform, false);
                meshFilters[i] = slot.AddComponent<MeshFilter>();
                meshRenderers[i] = slot.AddComponent<MeshRenderer>();
                meshRenderers[i].sharedMaterial = material;
                meshRenderers[i].shadowCastingMode = ShadowCastingMode.Off;
                meshRenderers[i].receiveShadows = false;
                blocks[i] = new MaterialPropertyBlock();
                slotLines[i] = -1;
            }
            root.SetActive(false);
        }

        public void Begin(float song)
        {
            if (disposed || !root) throw new InvalidOperationException("Particle credits are not initialized.");
            Active = true;
            Completed = false;
            CycleCount = SeenLines = VisibleLineCount = 0;
            elapsed = 0f;
            Array.Clear(seen, 0, seen.Length);
            for (int i = 0; i < PoolSize; i++) slotLines[i] = -1;
            root.SetActive(true);
            UpdateSlots();
        }

        public void Tick(float song, float dt)
        {
            if (disposed || !root || (!Active && !Completed)) return;
            elapsed += Mathf.Clamp(dt, 0f, 0.1f);
            bool completedNow = Active && elapsed >= Duration;
            if (completedNow)
            {
                elapsed = Duration;
                Active = false;
                Completed = true;
            }
            VisibleLineCount = CycleCount = 0;
            for (int i = 0; i < LineCount; i++)
            {
                float age = elapsed - i * LineInterval;
                if (age >= GatherDuration && !seen[i]) { seen[i] = true; SeenLines++; }
                if (age >= GatherDuration && age < GatherDuration + ScrollDuration) VisibleLineCount++;
                if (age >= LineDuration) CycleCount++;
            }
            if (completedNow) CycleCount = LineCount;
            UpdateSlots();
        }

        public void Reset()
        {
            Active = false;
            Completed = false;
            CycleCount = SeenLines = VisibleLineCount = 0;
            elapsed = 0f;
            if (seen != null) Array.Clear(seen, 0, seen.Length);
            if (root) root.SetActive(false);
        }

        void BuildLineMeshes()
        {
            var clouds = new PointCloud[Lines.Length];
            int largest = 0;
            for (int i = 0; i < Lines.Length; i++)
            {
                CreditLine line = Lines[i];
                clouds[i] = new PointCloud();
                TutorialGlyphs.AddText(clouds[i], line.Text, Vector2.zero, line.Cell,
                    line.Size, line.Color, 317);
                largest = Mathf.Max(largest, clouds[i].Count);
            }

            ParticlesPerLine = largest;
            StableParticleCount = largest * PoolSize;
            seen = new bool[Lines.Length];
            for (int i = 0; i < clouds.Length; i++)
            {
                PointCloud cloud = clouds[i];
                while (cloud.Count < largest) {
                    // Glyphs use this same ID seed; unused points continue drifting between lines.
                    float seed = Mathf.Repeat(317 * 0.173f + cloud.Count * 0.6180339f, 1f);
                    cloud.Add(Vector3.zero, 0.32f, new Color(0.38f, 0.87f, 1f, 0f), seed);
                }
                lineMeshes.Add(cloud.Build("Atlantis staff-roll glyphs / " + i, 800f));
            }
        }

        void UpdateSlots()
        {
            int latestStarted = Mathf.FloorToInt(elapsed / LineInterval);
            for (int slot = 0; slot < PoolSize; slot++)
            {
                int latestForSlot = latestStarted - PositiveMod(latestStarted - slot, PoolSize);
                int lastForSlot = LineCount - 1 - PositiveMod(LineCount - 1 - slot, PoolSize);
                int index = Mathf.Min(Mathf.Max(slot, latestForSlot), lastForSlot);
                if (slotLines[slot] != index)
                {
                    slotLines[slot] = index;
                    meshFilters[slot].sharedMesh = lineMeshes[index];
                }
                var block = blocks[slot];
                block.SetFloat("_Gain", Lines[index].Gain);
                block.SetFloat("_Slot", slot);
                block.SetFloat("_CreditsActive", Active ? 1f : 0f);
                block.SetFloat("_AmbientOnly", Completed ? 1f : 0f);
                block.SetFloat("_CreditsCycle", elapsed - index * LineInterval);
                block.SetFloat("_CreditsElapsed", elapsed);
                block.SetFloat("_CreditsGatherDuration", GatherDuration);
                block.SetFloat("_CreditsScrollDuration", ScrollDuration);
                block.SetFloat("_CreditsScatterDuration", ScatterDuration);
                block.SetFloat("_CreditsReturnDuration", ReturnDuration);
                block.SetFloat("_CreditsLineDuration", LineDuration);
                block.SetFloat("_CreditsScrollDistance", ScrollDistance);
                meshRenderers[slot].SetPropertyBlock(block);
            }
        }

        static int PositiveMod(int value, int divisor)
        {
            return (value % divisor + divisor) % divisor;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Mesh mesh in lineMeshes)
                if (mesh) UnityEngine.Object.Destroy(mesh);
            lineMeshes.Clear();
            if (material) UnityEngine.Object.Destroy(material);
            if (root) UnityEngine.Object.Destroy(root);
            material = null;
            root = null;
        }
    }
}
