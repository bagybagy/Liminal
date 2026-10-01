Shader "Liminal/Pressure Bubble"
{
    Properties { _Tint("Tint",Color)=(.08,.7,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            CBUFFER_END
            float _Song;
            struct Input { float3 positionOS:POSITION;float4 uv:TEXCOORD0;float2 data:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float glow:TEXCOORD1; };
            Varyings Vert(Input v)
            {
                Varyings o;
                float phase=v.data.x*6.2831853;
                float3 local=v.positionOS*(1+.028*sin(_Song*2.3+phase));
                float3 center=TransformObjectToWorld(local);
                float scale=length(GetObjectToWorldMatrix()[0].xyz);
                float size=max(v.uv.z*scale,.065);
                float3 pos=center+(UNITY_MATRIX_V[0].xyz*v.uv.x+UNITY_MATRIX_V[1].xyz*v.uv.y)*size;
                o.positionCS=TransformWorldToHClip(pos);o.uv=v.uv.xy;
                o.glow=.7+.5*pow(saturate(sin(phase+_Song*3)),6);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float r2=dot(i.uv,i.uv);clip(1-r2);
                return half4(_Tint.rgb*exp(-r2*5)*i.glow*2.4,1);
            }
            ENDHLSL
        }
    }
}
