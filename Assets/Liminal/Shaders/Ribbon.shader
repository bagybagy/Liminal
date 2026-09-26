Shader "Liminal/Ribbon"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            CBUFFER_END
            struct In { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Out { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Out Vert(In i) { Out o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.color=i.color*_Tint; o.uv=i.uv; return o; }
            half4 Frag(Out i):SV_Target { float edge=pow(saturate(1-abs(i.uv.y*2-1)),1.6); return half4(i.color.rgb, i.color.a*edge); }
            ENDHLSL
        }
    }
}
