Shader "Liminal/Point Study Native"
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
            #include "../Shaders/PointStudy.hlsl"
            ENDHLSL
        }
    }
}
