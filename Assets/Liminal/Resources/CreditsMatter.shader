Shader "Liminal/Credits Matter"
{
    Properties
    {
        _Gain ("Radiance", Float) = 3
        [HideInInspector] _CreditsActive ("Credits Active", Float) = 0
        [HideInInspector] _AmbientOnly ("Ambient Afterglow", Float) = 0
        [HideInInspector] _CreditsCycle ("Line Cycle", Float) = 0
        [HideInInspector] _CreditsElapsed ("Elapsed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+8" "RenderPipeline"="UniversalPipeline" }
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
                float _CreditsActive;
                float _AmbientOnly;
                float _CreditsCycle;
                float _CreditsElapsed;
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

            float3 AmbientPosition(float3 center, float seed)
            {
                float phase = _CreditsElapsed * (0.22 + frac(seed * 7.13) * 0.16) + seed * 6.2831853;
                return center + float3(
                    (frac(seed * 19.17) - 0.5) * 88.0 + sin(phase) * 3.4,
                    -27.0 + cos(phase * 0.73) * 7.5,
                    (frac(seed * 53.71) - 0.5) * 10.0 + sin(phase * 0.61) * 4.0);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float seed = input.data.x;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float3 target = TransformObjectToWorld(input.positionOS) - float3(0, 16, 0);
                float3 ambient = AmbientPosition(center, seed);
                float3 world = ambient;
                float glow = 0.15;

                bool glyph = input.color.a > 0.5;
                if (_CreditsActive > 0.5 && glyph)
                {
                    float cycle = _CreditsCycle;
                    if (cycle < 2.2)
                    {
                        world = lerp(ambient, target, smoothstep(0.0, 2.2, cycle));
                        glow = 0.82;
                    }
                    else if (cycle < 5.0)
                    {
                        world = target;
                        glow = 1.0;
                    }
                    else if (cycle < 8.0)
                    {
                        float rise = smoothstep(5.0, 8.0, cycle);
                        world = target + float3(sin(seed * 41.0 + cycle) * 0.5, rise * 32.0, 0.0);
                        glow = 1.0 - rise * 0.16;
                    }
                    else if (cycle < 9.5)
                    {
                        float dissolve = smoothstep(8.0, 9.5, cycle);
                        float3 scatter = float3(
                            sin(seed * 71.0) * dissolve * 13.0,
                            32.0 + dissolve * (10.0 + frac(seed * 5.31) * 12.0),
                            cos(seed * 29.0) * dissolve * 11.0);
                        world = target + scatter;
                        glow = 1.0 - dissolve;
                    }
                    else
                    {
                        float returnToDrift = smoothstep(9.5, 12.0, cycle);
                        float3 above = target + float3(sin(seed * 71.0) * 13.0,
                            42.0 + frac(seed * 5.31) * 12.0, cos(seed * 29.0) * 11.0);
                        world = lerp(above, ambient, returnToDrift);
                        glow = lerp(0.0, 0.18, returnToDrift);
                    }
                }

                float alpha = _AmbientOnly > 0.5 ? 0.18 : _CreditsActive;
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float depth = max(0.01, -TransformWorldToView(world).z);
                float pixelWorld = 2.0 * depth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                float size = max(input.uv.z, pixelWorld * 1.25);
                float3 positionWS = world + (cameraRight * input.uv.x + cameraUp * input.uv.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv.xy;
                output.color = input.color.rgb * _Gain * glow;
                output.alpha = alpha * glow;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.alpha - 0.005);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 8.0) * 1.55;
                float halo = exp(-radius * 3.0) * 0.14;
                return half4(input.color * (core + halo), 1.0);
            }
            ENDHLSL
        }
    }
}
