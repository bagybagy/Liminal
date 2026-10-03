Shader "Liminal/Point Study Sharp Quad"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex StudyVertex
            #pragma fragment StudyFragment
            #pragma multi_compile_instancing
            #define STUDY_QUAD 1
            #include "../Shaders/PointStudy.hlsl"
            ENDHLSL
        }
    }
}
