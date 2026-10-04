Shader "Liminal/Bioluminescent Membrane"
{
    Properties { _Gain ("Radiance", Float) = 1 }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Spine.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Gain;
            CBUFFER_END
            float _Song,_Evolution,_Dissolve,_Pulse,_Reduced,_Released,_ReleaseBlend;
            float4 _LiminalComparisonOffset;
            struct Input {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 data:TEXCOORD1;};
            struct Vary {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 param:TEXCOORD2;};
            Vary Vert(Input i)
            {
                float u=i.uv.x,a=i.uv.y*6.2831853,radial=1;
                float3 c,forward,side,up;float width;
                SpineFrame(u,c,forward,side,up,width);
                if(i.data.x>.5) {
                    a=i.data.x<1.5?1.5708:i.data.x<2.5?.15:2.99;
                    a+=sin(u*25-_Song*1.1)*i.data.y*.12;
                    radial+=i.data.y*(1.25+pow(sin(u*22),2)*.7)*sin(u*3.14159)*(1+_Evolution*.45);
                }
                float3 normal=side*cos(a)+up*sin(a);
                float breath=1+.022*sin(u*35-_Song*2);
                float3 p=c+normal*width*radial*breath+_LiminalComparisonOffset.xyz;
                Vary o;o.positionCS=TransformWorldToHClip(p);o.positionWS=p;o.normalWS=normal;
                o.param=float4(u,a,i.data.x,i.data.y);return o;
            }
            half4 Frag(Vary i):SV_Target
            {
                float facing=abs(dot(normalize(i.normalWS),normalize(_WorldSpaceCameraPos-i.positionWS)));
                float rim=pow(1-facing,2.6);
                float u=i.param.x,a=i.param.y;
                float vein=pow(.5+.5*sin(u*820+a*4+sin(a*9)*1.2-_Song*2),28);
                float ribs=pow(.5+.5*cos(u*300),48);
                float flow=pow(saturate(sin(u*25-_Song*1.8)),8);
                float3 teal=float3(.09,.56,.57),gold=float3(.9,.47,.10);
                float3 col=lerp(teal,gold,_Evolution*.72+ribs*.2);
                col+=float3(.19,.56,.51)*flow*.35;
                bool fin=i.param.z>.5;
                float opacity=fin?.025+vein*.17+pow(i.param.w,8)*.11:.018+rim*.29+vein*.055+ribs*.04;
                if(fin) opacity*=1-i.param.w*.6;
                opacity*=(1-_Dissolve)*(1+_Pulse*.12*(1-_Reduced));
                opacity*=1-saturate(_ReleaseBlend);
                return half4(col*_Gain,opacity);
            }
            ENDHLSL
        }
    }
}
