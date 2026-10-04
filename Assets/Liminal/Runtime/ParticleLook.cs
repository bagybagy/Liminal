using UnityEngine;

namespace Liminal
{
    public sealed class ParticleLook : MonoBehaviour
    {
        public enum QuadStyle { SharpQuad, OriginalQuad }

        [Range(.25f, 2f)] public float bodyRadiance = 1f;
        [Range(0, 12f)] public float sparkRadiance = 3f;
        [Range(0, 2f)] public float surfaceFlow = 2f;
        [Range(.75f, 1.5f)] public float grainFootprint = 1f;
        static readonly int LookId = Shader.PropertyToID("_LiminalGrainLook");
        static readonly int QuadStyleId = Shader.PropertyToID("_LiminalQuadStyle");
        public static Vector4 Current => Shader.GetGlobalVector(LookId);
        public static bool SideBySide { get; private set; }
        public QuadStyle quadStyle;
        public bool sideBySide;
        public bool IsLegacy => quadStyle == QuadStyle.OriginalQuad;
        Vector4 previous;

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void OnDisable() => SideBySide = false;
        void Update()
        {
            Vector4 next = new(bodyRadiance, sparkRadiance, surfaceFlow, grainFootprint);
            if (next != previous) Apply();
        }

        public void Apply()
        {
            previous = new Vector4(bodyRadiance, sparkRadiance, surfaceFlow, grainFootprint);
            Shader.SetGlobalVector(LookId, previous);
            Shader.SetGlobalFloat(QuadStyleId, IsLegacy ? 1f : 0f);
            SideBySide = sideBySide;
        }

        public void SetStyle(bool legacy)
        {
            quadStyle = legacy ? QuadStyle.OriginalQuad : QuadStyle.SharpQuad;
            Shader.SetGlobalFloat(QuadStyleId, legacy ? 1f : 0f);
        }

        public void SetSideBySide(bool enabled)
        {
            sideBySide = enabled;
            SideBySide = enabled;
        }
    }
}
