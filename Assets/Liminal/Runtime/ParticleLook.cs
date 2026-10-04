using UnityEngine;

namespace Liminal
{
    public sealed class ParticleLook : MonoBehaviour
    {
        [Range(.25f, 2f)] public float bodyRadiance = 1f;
        [Range(0, 12f)] public float sparkRadiance = 3f;
        [Range(0, 2f)] public float surfaceFlow = 2f;
        [Range(.75f, 1.5f)] public float grainFootprint = 1f;
        static readonly int LookId = Shader.PropertyToID("_LiminalGrainLook");
        public static Vector4 Current => Shader.GetGlobalVector(LookId);
        Vector4 previous;

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void Update()
        {
            Vector4 next = new(bodyRadiance, sparkRadiance, surfaceFlow, grainFootprint);
            if (next != previous) Apply();
        }

        public void Apply()
        {
            previous = new Vector4(bodyRadiance, sparkRadiance, surfaceFlow, grainFootprint);
            Shader.SetGlobalVector(LookId, previous);
        }
    }
}
