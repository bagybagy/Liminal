Shader "Liminal/Advected Light"
{
    Properties { _Gain ("Radiance", Float) = 1 }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Spine.hlsl"
            StructuredBuffer<FlowParticle> _Particles;
            CBUFFER_START(UnityPerMaterial)
            float _Gain;
            CBUFFER_END
            float _Song,_Pulse,_Evolution,_Dissolve,_Reduced;
            struct Vary {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            Vary Vert(uint vertexID:SV_VertexID)
            {
                uint index=vertexID/6, corner=vertexID%6;
                float2 quad[6]={float2(-1,-1),float2(-1,1),float2(1,1),float2(-1,-1),float2(1,1),float2(1,-1)};
                float2 uv=quad[corner];
                FlowParticle p=_Particles[index];
                bool wake=p.anatomy.w>2.5;
                float3 c,forward,side,up;float width;
                SpineFrame(p.anatomy.x,c,forward,side,up,width);
                float3 cameraRight=UNITY_MATRIX_V[0].xyz, cameraUp=UNITY_MATRIX_V[1].xyz;
                float3 travel=wake?p.velocity:forward;
                float2 screenMotion=float2(dot(travel,cameraRight),dot(travel,cameraUp));
                screenMotion=normalize(screenMotion+float2(.0001,.0001));
                float3 along=cameraRight*screenMotion.x+cameraUp*screenMotion.y;
                float3 across=cameraRight*(-screenMotion.y)+cameraUp*screenMotion.x;
                float distance=length(_WorldSpaceCameraPos-p.position);
                float size=(wake?.028:.016)*lerp(.75,1.7,p.seed);
                size=max(size,distance*.00028);
                float stretch=wake?2.5:lerp(2,5,p.seed);
                if(p.anatomy.w>1.5 && !wake) size*=1.4;
                float3 pos=p.position+across*uv.x*size+along*uv.y*size*stretch;
                float3 normal=side*cos(p.anatomy.y)+up*sin(p.anatomy.y);
                float rim=pow(1-abs(dot(normal,normalize(_WorldSpaceCameraPos-p.position))),1.5);
                float accent=pow(saturate(sin(p.anatomy.x*111-_Song*1.8+p.seed*1.3)),16);
                float3 teal=float3(.025,.52,.64), pearl=float3(.42,.93,.75), gold=float3(1,.54,.13);
                float3 color=lerp(teal,pearl,p.seed*.8);
                color=lerp(color,gold,(p.anatomy.w>.5 && p.anatomy.w<1.5?.52:accent*.5)+_Evolution*.4);
                float life=wake?sin(saturate(p.age/p.life)*3.14159):1;
                float brightness=wake?.20:p.anatomy.w>1.5?.10:1.0+rim*.8;
                if(p.anatomy.w<-.5) { color=gold;brightness=2.8; }
                float pulse=1+_Pulse*.24*(1-_Reduced);
                Vary o;
                o.positionCS=TransformWorldToHClip(pos);o.uv=uv;
                o.color=float4(color*brightness*life*_Gain*pulse*(1-_Dissolve)*exp(-distance*.0016),1);
                return o;
            }
            half4 Frag(Vary i):SV_Target
            {
                float r=dot(i.uv,i.uv);
                clip(1-r);
                float light=exp(-r*5)*.55+exp(-r*24)*1.45;
                return half4(i.color.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
