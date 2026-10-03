using UnityEngine;

namespace Liminal
{
    [CreateAssetMenu(menuName="Liminal/Particle Transition Settings")]
    public sealed class ParticleTransitionSettings : ScriptableObject
    {
        [Min(.05f)] public float chargeSeconds=.3f;
        [Min(.2f)] public float smallTurbulenceSeconds=1.2f;
        [Min(.2f)] public float largeTurbulenceSeconds=3f;
        [Min(.1f)] public float fadeSeconds=.8f;
        [Range(0,3)] public float peakRadianceBoost=.8f;
        [Range(0,3)] public float turbulenceStrength=1.4f;
        static ParticleTransitionSettings current;
        public static ParticleTransitionSettings Current
        {
            get {
                if(!current) current=Resources.Load<ParticleTransitionSettings>("ParticleTransitions");
                if(!current) current=CreateInstance<ParticleTransitionSettings>();
                return current;
            }
        }
        public Vector4 Timings => new(Mathf.Max(.05f,chargeSeconds),Mathf.Max(.2f,smallTurbulenceSeconds),
            Mathf.Max(.2f,largeTurbulenceSeconds),Mathf.Max(.1f,fadeSeconds));
        public float Duration(bool large=false) => Timings.x+(large?Timings.z:Timings.y)+Timings.w;
        public Vector4 Envelope(float age,bool large=false)
        {
            Vector4 t=Timings;
            float loose=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/t.x));
            float build=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-t.x)/(large?t.z:t.y)));
            float end=t.x+(large?t.z:t.y);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-end)/t.w));
            return new Vector4(loose,loose*Mathf.Lerp(.06f,1,build),loose*Mathf.Lerp(.35f,1,build)*fade,fade);
        }
        public void ApplyGlobals()
        {
            Shader.SetGlobalVector("_MatterDeathTimings",Timings);
            Shader.SetGlobalVector("_MatterDeathStyle",new Vector4(turbulenceStrength,peakRadianceBoost,0,0));
        }
        public void ApplyCompute(ComputeShader compute)
        {
            compute.SetVector("_MatterDeathTimings",Timings);
            compute.SetVector("_MatterDeathStyle",new Vector4(turbulenceStrength,peakRadianceBoost,0,0));
        }
    }
}
