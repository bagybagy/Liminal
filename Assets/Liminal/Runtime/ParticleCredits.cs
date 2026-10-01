using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class ParticleCredits : IDisposable
    {
        const float LineDuration = 12f;
        const float GatherDuration = 2.2f;

        readonly struct CreditLine
        {
            public readonly string Text;
            public readonly float Cell, Size, Gain;
            public readonly Color Color;

            public CreditLine(string text, float cell, float size, float gain, Color color)
            {
                Text = text;
                Cell = cell;
                Size = size;
                Gain = gain;
                Color = color;
            }
        }

        static readonly CreditLine[] Lines = {
            new("Created by tete", 0.68f, 0.29f, 5.2f, new Color(1f, 0.79f, 0.35f)),
            new("Unity 6 / URP", 0.58f, 0.20f, 3.4f, new Color(0.75f, 1f, 0.92f)),
            new("Compute shader / Graphics buffer", 0.43f, 0.19f, 3.2f, new Color(0.36f, 0.91f, 0.85f)),
            new("DSP scheduled audio / OpenXR", 0.46f, 0.19f, 3.3f, new Color(0.49f, 0.78f, 1f)),
            new("Peirce / Gauss / Curie", 0.50f, 0.19f, 3.0f, new Color(0.82f, 0.96f, 0.88f)),
            new("Meitner / Noether / Kuhn", 0.47f, 0.19f, 3.0f, new Color(0.68f, 0.91f, 1f)),
            new("Ohm / Fermat / Halley", 0.50f, 0.19f, 3.1f, new Color(1f, 0.75f, 0.41f)),
            new("Tesla", 0.72f, 0.24f, 3.5f, new Color(0.70f, 1f, 0.89f))
        };

        readonly List<Mesh> lineMeshes = new();
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Material material;
        GameObject root;
        bool[] seen;
        float lineElapsed, elapsed;
        int lineIndex;
        bool disposed;

        public bool Active { get; private set; }
        public bool Completed { get; private set; }
        public int CycleCount { get; private set; }
        public int StableParticleCount { get; private set; }
        public int SeenLines { get; private set; }

        public void Initialize(Transform parent, Shader shader, Vector3 localPosition)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ParticleCredits));
            if (root) throw new InvalidOperationException("Particle credits can only be initialized once.");
            if (!parent || !shader) throw new ArgumentException("Particle credits require a parent and shader.");

            root = new GameObject("Atlantis credits / world-fixed glyphs");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            meshFilter = root.AddComponent<MeshFilter>();
            meshRenderer = root.AddComponent<MeshRenderer>();
            material = new Material(shader) { name = "Atlantis credits matter" };
            material.SetFloat("_Gain", 3.2f);
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            BuildLineMeshes();
            root.SetActive(false);
        }

        public void Begin(float song)
        {
            if (disposed || !root) throw new InvalidOperationException("Particle credits are not initialized.");
            Active = true;
            Completed = false;
            CycleCount = 0;
            SeenLines = 0;
            lineIndex = 0;
            lineElapsed = 0f;
            elapsed = 0f;
            Array.Clear(seen, 0, seen.Length);
            root.SetActive(true);
            SelectLine(0);
            UpdateMaterial();
        }

        public void Tick(float song, float dt)
        {
            if (disposed || !root || (!Active && !Completed)) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            elapsed += dt;

            if (Active)
            {
                lineElapsed += dt;
                if (!seen[lineIndex] && lineElapsed >= GatherDuration)
                {
                    seen[lineIndex] = true;
                    SeenLines++;
                }

                while (lineElapsed >= LineDuration && Active)
                {
                    lineElapsed -= LineDuration;
                    CycleCount++;
                    if (lineIndex + 1 >= Lines.Length)
                    {
                        Active = false;
                        Completed = true;
                        lineElapsed = LineDuration;
                        break;
                    }

                    lineIndex++;
                    SelectLine(lineIndex);
                    if (!seen[lineIndex] && lineElapsed >= GatherDuration)
                    {
                        seen[lineIndex] = true;
                        SeenLines++;
                    }
                }
            }

            UpdateMaterial();
        }

        public void Reset()
        {
            Active = false;
            Completed = false;
            CycleCount = 0;
            SeenLines = 0;
            lineIndex = 0;
            lineElapsed = 0f;
            elapsed = 0f;
            if (seen != null) Array.Clear(seen, 0, seen.Length);
            if (root) root.SetActive(false);
            if (material)
            {
                material.SetFloat("_CreditsActive", 0f);
                material.SetFloat("_AmbientOnly", 0f);
                material.SetFloat("_CreditsCycle", 0f);
                material.SetFloat("_CreditsElapsed", 0f);
            }
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

            StableParticleCount = largest;
            seen = new bool[Lines.Length];
            for (int i = 0; i < clouds.Length; i++)
            {
                PointCloud cloud = clouds[i];
                while (cloud.Count < StableParticleCount) {
                    // Glyphs use this same ID seed; unused points continue drifting between lines.
                    float seed = Mathf.Repeat(317 * 0.173f + cloud.Count * 0.6180339f, 1f);
                    cloud.Add(Vector3.zero, 0.19f, new Color(0.36f, 0.91f, 0.85f, 0f), seed);
                }
                lineMeshes.Add(cloud.Build("Atlantis credit glyphs / " + i, 800f));
            }
        }

        void SelectLine(int index)
        {
            meshFilter.sharedMesh = lineMeshes[index];
            material.SetFloat("_Gain", Lines[index].Gain);
        }

        void UpdateMaterial()
        {
            if (!material) return;
            material.SetFloat("_CreditsActive", Active ? 1f : 0f);
            material.SetFloat("_AmbientOnly", Completed ? 1f : 0f);
            material.SetFloat("_CreditsCycle", lineElapsed);
            material.SetFloat("_CreditsElapsed", elapsed);
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
            meshFilter = null;
            meshRenderer = null;
            material = null;
            root = null;
        }
    }
}
