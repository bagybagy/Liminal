Shader "Liminal/Atlantis Matter"
{
    Properties
    {
        _Gain ("Radiance", Float) = 3
        _Formation ("Formation", Range(0,1)) = 0
        _LayerKind ("Layer Kind", Float) = 0
        _Song ("Song Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+7" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Gain;
                float _Formation;
                float _LayerKind;
                float _Song;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 color : COLOR;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateY(float3 position, float angle)
            {
                float sine = sin(angle);
                float cosine = cos(angle);
                return float3(cosine * position.x + sine * position.z, position.y,
                    -sine * position.x + cosine * position.z);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float seed = input.data.x;
                float3 anchor = TransformObjectToWorld(input.positionOS);
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float formation = saturate((_Formation - seed * 0.28) / 0.72);
                formation = smoothstep(0.0, 1.0, formation);
                float3 gather = center + float3(
                    (frac(seed * 17.173) - 0.5) * 46.0,
                    -44.0 + frac(seed * 3.713) * 21.0,
                    (frac(seed * 91.713) - 0.5) * 32.0);
                float3 world = lerp(gather, anchor, formation);

                if (_LayerKind > 0.5 && _LayerKind < 1.5)
                {
                    float school = floor(input.data.y + 0.5);
                    float speed = 0.075 + fmod(school, 3.0) * 0.012;
                    float orbit = _Song * speed;
                    float3 local = TransformWorldToObject(world);
                    world = TransformObjectToWorld(RotateY(local, orbit));
                }
                else if (_LayerKind > 1.5)
                {
                    float craft = floor(input.data.y + 0.5);
                    float phase = _Song * 0.19 + craft * 2.0943951;
                    world += float3(sin(phase) * 2.4, sin(phase * 0.63) * 1.2,
                        cos(phase) * 2.4);
                }
                else
                {
                    world.y += sin(_Song * 0.27 + seed * 6.2831853) * 0.045 * formation;
                }

                float2 quad = input.uv.xy;
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float depth = max(0.01, -TransformWorldToView(world).z);
                float pixelWorld = 2.0 * depth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                float size = max(input.uv.z, pixelWorld * 1.2);
                float3 positionWS = world + (cameraRight * quad.x + cameraUp * quad.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = quad;
                output.color = input.color.rgb * _Gain * (0.58 + formation * 0.42);
                output.alpha = input.color.a * formation;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.alpha - 0.015);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 8.0) * 1.5;
                float halo = exp(-radius * 3.2) * 0.18;
                return half4(input.color * (core + halo), 1.0);
            }
            ENDHLSL
        }
    }
}
