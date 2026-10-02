using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class ParticleCredits : IDisposable
    {
        public const float LineInterval = 4f;
        public const float GatherDuration = 2.2f;
        public const float ScrollDuration = 18f;
        public const float ScrollDistance = 108f;
        const float ScatterDuration = 2f;
        const float ReturnDuration = 1.8f;
        const float LineDuration = GatherDuration + ScrollDuration + ScatterDuration + ReturnDuration;
        const int SlotCount = 6;

        readonly struct CreditLine
        {
            public readonly string Text;
            public readonly float Cell, Size, Gain;
            public readonly Color Color;
            public readonly bool Heading;

            public CreditLine(string text, bool heading = false, bool featured = false)
            {
                Text = text;
                Heading = heading;
                Cell = featured ? 1.7f : heading ? 0.98f : 1.15f;
                Size = featured ? 0.60f : heading ? 0.35f : 0.41f;
                Gain = featured ? 5.2f : heading ? 3.5f : 4f;
                Color = featured ? new Color(1f, 0.79f, 0.35f) :
                    heading ? new Color(0.38f, 0.87f, 1f) : new Color(0.82f, 1f, 0.94f);
            }
        }

        static readonly CreditLine[] Lines = {
            new("LIMINAL", true, true),
            new("ABYSSAL CHOIR", true),
            new("CREATED BY", true),
            new("tete", false, true),
            new("MUSIC", true),
            new("TidalMemory"),
            new("ENGINE / RENDERING", true),
            new("Unity 6 / URP"),
            new("Compute shader"),
            new("Graphics buffer"),
            new("AUDIO / PCVR", true),
            new("DSP scheduled audio"),
            new("OpenXR"),
            new("PRODUCTION WORKERS", true),
            new("Peirce / Gauss / Curie"),
            new("Meitner / Noether / Kuhn"),
            new("Ohm / Fermat / Halley"),
            new("Tesla"),
            new("THANK YOU FOR PLAYING", true)
        };

        readonly List<Mesh> lineMeshes = new();
        readonly MeshFilter[] meshFilters = new MeshFilter[SlotCount];
        readonly MeshRenderer[] meshRenderers = new MeshRenderer[SlotCount];
        readonly MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[SlotCount];
        readonly int[] slotLines = new int[SlotCount];
        Material material;
        GameObject root;
        bool[] seen;
        float elapsed;
        bool disposed;

        public static int LineCount => Lines.Length;
        public static float Duration => (LineCount - 1) * LineInterval + LineDuration;
        public static string GetLineText(int index) => Lines[index].Text;
        public static Color GetLineColor(int index) => Lines[index].Color;
        public static bool IsHeading(int index) => Lines[index].Heading;
        public bool Active { get; private set; }
        public bool Completed { get; private set; }
        public int CycleCount { get; private set; }
        public int StableParticleCount { get; private set; }
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
            for (int i = 0; i < SlotCount; i++)
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
            for (int i = 0; i < SlotCount; i++) slotLines[i] = -1;
            root.SetActive(true);
            UpdateSlots();
        }

        public void Tick(float song, float dt)
        {
            if (disposed || !root || (!Active && !Completed)) return;
            elapsed += Mathf.Clamp(dt, 0f, 0.1f);
            VisibleLineCount = CycleCount = 0;
            for (int i = 0; i < LineCount; i++)
            {
                float age = elapsed - i * LineInterval;
                if (age >= GatherDuration && !seen[i]) { seen[i] = true; SeenLines++; }
                if (age >= GatherDuration && age < GatherDuration + ScrollDuration) VisibleLineCount++;
                if (age >= LineDuration) CycleCount++;
            }
            if (Active && elapsed >= Duration) { Active = false; Completed = true; }
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

            StableParticleCount = largest * SlotCount;
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
            // Each pool returns to the same ambient positions before taking its next row.
            for (int slot = 0; slot < SlotCount; slot++)
            {
                int cycle = Mathf.Clamp(Mathf.FloorToInt((elapsed - slot * LineInterval) / LineDuration),
                    0, (LineCount - 1 - slot) / SlotCount);
                int index = slot + cycle * SlotCount;
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
                meshRenderers[slot].SetPropertyBlock(block);
            }
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
