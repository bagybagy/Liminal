Shader "Liminal/Pufferfish Particles"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 1.9
        _DeathProgress ("Dissolution", Range(0,1)) = 0
        _HitAge ("Hit age", Float) = 10
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
            #pragma target 4.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/MatterFlow.hlsl"
            #include "../Shaders/SharpMatter.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Gain, _DeathProgress, _HitAge;
            float _DeathAge;
            float4 _FlowVelocity;
            CBUFFER_END
            float _Song, _Pulse, _Reduced;

            struct Input
            {
                float4 positionOS : POSITION;
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

            float Hash(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453);
            }

            Vary Vert(Input input)
            {
                Vary output;
                UNITY_SETUP_INSTANCE_ID(input);
                float progress = saturate(_DeathProgress);
                float4 transition=MatterDeathEnvelope(_DeathAge,false);
                float spread=transition.y*_MatterDeathStyle.x;
                float3 p = input.positionOS.xyz;
                float size = input.uv.z;
                p.y += sin(p.x * 0.8 + p.z * 0.6 + _Song * 0.8) * input.data.y;

                float seed = Hash(input.data.x + input.positionOS.x * 13.7);
                float3 radial = normalize(p + float3(0.0001, 0.0002, 0.0003));
                p = p * (1.0 + spread * 1.25) + radial * spread * (0.12 + seed * 0.34);
                p += MatterFlowDelta(input.positionOS.xyz,_DeathAge,input.data.x)*spread*.8;
                p += _FlowVelocity.xyz*(1-exp(-progress*2))*0.3;
                size *= 1.0 + progress * 0.48;

                float3 worldPosition = TransformObjectToWorld(p);
                size *= length(GetObjectToWorldMatrix()[0].xyz);
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 cameraPosition = _WorldSpaceCameraPos;
                float distanceToCamera = length(cameraPosition - worldPosition);
                size *= max(1.0, distanceToCamera * 0.006);
                bool legacy=MatterIsLegacy();
                if(!legacy) size = MatterGrainRadius(size,MatterPixelWorld(worldPosition),seed,2.2);
                worldPosition += (cameraRight * input.uv.x + cameraUp * input.uv.y) * size;

                float deathFade = transition.w;
                float grainLight = legacy ? 0.9 + 0.1 * sin(_Song * 1.7 + input.data.x * 31.0) :
                    MatterGrainLight(seed,_Song,saturate(input.color.a));
                float pulse = 1.0 + _Pulse * 0.18 * (1.0 - _Reduced);
                float3 color = input.color.rgb * _Tint.rgb * _Gain * grainLight * pulse * deathFade;
                float flash=exp(-_HitAge*6);
                color=lerp(color,float3(.05,.85,2.2)*deathFade,flash*.4);
                float glow = 1.0 + transition.z*_MatterDeathStyle.y+flash*.22;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = input.uv.xy;
                output.color = float4(color * glow * exp(-distanceToCamera * 0.0018), 1.0);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                return output;
            }

            half4 Frag(Vary input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if(MatterIsLegacy()) {
                    float radius=dot(input.uv,input.uv);clip(1.0-radius);
                    float glow=exp(-radius*5.0)*0.38+exp(-radius*24.0)*1.55;
                    return half4(input.color.rgb*glow,1.0);
                }
                return half4(input.color.rgb * MatterSharpCore(input.uv), 1.0);
            }
            ENDHLSL
        }
    }
}
