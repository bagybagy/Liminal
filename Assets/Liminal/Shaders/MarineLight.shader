Shader "Liminal/Marine Light"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 1
        _MarineMode ("Marine form", Float) = 0
        _Activation ("Resonance", Float) = 0
        _Scatter ("School response", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SharpMatter.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Gain, _MarineMode, _Activation, _Scatter;
            CBUFFER_END
            float _MarineSong;
            struct Input
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Vary
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Vary Vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Vary o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                bool legacy = MatterIsLegacy();
                float3 p=v.positionOS;
                float t=_MarineSong;
                float pulse=0.82+0.18*sin(t*1.35+v.data.x*17);
                if(_MarineMode>0.5 && _MarineMode<1.5)
                {
                    float bell=saturate((p.y+0.4)*0.8);
                    if(p.y<0.0) {
                        float u=saturate(-p.y/24.0);
                        p.x+=sin(t*1.3+v.data.x*2.7+u*5.0)*u*0.72;
                        p.z+=cos(t*1.1+v.data.x*2.1+u*4.0)*u*0.72;
                    }
                    p.y+=bell*sin(t*2.0+v.data.x*3.0)*0.10;
                    pulse*=1.0+_Activation*(0.8+0.25*sin(t*4.2));
                }
                else if(_MarineMode>1.5 && _MarineMode<2.5)
                {
                    p.y+=sin(t*8.0+v.data.x*3.0)*v.data.y*0.16;
                    p.x+=sin(t*4.5+v.data.x*2.0)*v.data.y*0.10;
                    float3 scatter=frac(sin(float3(v.data.x*127.1,v.data.x*311.7,v.data.x*74.7))*43758.5453)*2.0-1.0;
                    p+=scatter*_Scatter*3.5;
                    pulse*=1.0+_Activation*1.5;
                }
                else if(_MarineMode>2.5)
                {
                    float tail=pow(saturate((-p.z-8.0)/74.0),1.7);
                    p.y+=sin(t*0.78+p.z*0.047)*tail*5.5;
                    p.x+=sin(t*0.61+p.z*0.052)*tail*2.2;
                    float fin=saturate((abs(p.x)-9.0)/28.0)*saturate((p.z-1.0)/15.0);
                    p.y+=sin(t*0.82+v.data.x*0.7)*fin*0.9;
                    pulse*=1.0+_Activation*0.45;
                }
                float3 world=TransformObjectToWorld(p);
                float distance=length(_WorldSpaceCameraPos-world);
                float size=v.uv.z*max(1.0,distance*0.0038);
                if(!legacy) size=MatterGrainRadius(size,MatterPixelWorld(world),v.data.x,1.3);
                float grainGain=legacy?1.0:1.0+(MatterGrainLight(v.data.x,t,saturate(v.data.y))-1.0)*0.14;
                float3 right=UNITY_MATRIX_V[0].xyz, up=UNITY_MATRIX_V[1].xyz;
                world+=(right*v.uv.x+up*v.uv.y)*size;
                o.positionCS=TransformWorldToHClip(world);
                o.uv=v.uv.xy;
                float3 color=lerp(v.color.rgb,v.color.rgb*float3(0.58,1.28,1.52),_Activation*0.72);
                o.color=float4(color*_Tint.rgb*_Gain*pulse*exp(-distance*0.0015)*grainGain,1);
                return o;
            }
            half4 Frag(Vary i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r=dot(i.uv,i.uv);
                clip(1-r);
                if(MatterIsLegacy()) {
                    float glow=exp(-r*5.5)*0.34+exp(-r*25.0)*1.55;
                    return half4(i.color.rgb*glow,1);
                }
                return half4(i.color.rgb*MatterSharpCore(i.uv)*0.884,1);
            }
            ENDHLSL
        }
    }
}
