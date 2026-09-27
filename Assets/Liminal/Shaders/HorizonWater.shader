Shader "Liminal/Horizon Water"
{
    Properties { _WaterTint ("Deep water", Color) = (0.002, 0.012, 0.017, 0.035) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterTint;
                float4 _WaterCenter;
                float4 _WaterRadii;
                float _Song;
                int _WaterEventCount;
            CBUFFER_END
            StructuredBuffer<float4> _WaterEvents;

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            float EventWave(float2 p)
            {
                float sum = 0;
                [loop] for (int i = 0; i < _WaterEventCount; i++)
                {
                    float4 origin = _WaterEvents[i * 2];
                    float age = _Song - origin.w;
                    if (age < 0 || age > 14) continue;
                    float4 motion = _WaterEvents[i * 2 + 1];
                    float2 delta = p - origin.xz - motion.xy * motion.z * age * 0.32;
                    float radius = length(delta);
                    float phase = radius * 0.16 - age * (2.1 + motion.z * 0.035);
                    float envelope = exp(-abs(radius - age * (12 + motion.z * 0.35)) * 0.014) * exp(-age * 0.12);
                    float backward = max(0, -dot(delta, motion.xy));
                    float wakeAxis = abs(delta.x * motion.y - delta.y * motion.x);
                    float armDistance = wakeAxis - backward * 0.24;
                    float vWake = exp(-pow(armDistance / 3.5, 2)) * smoothstep(8, 24, backward)
                        * exp(-backward * 0.0033);
                    sum += (sin(phase) * 0.9 + vWake * 1.4) * envelope * motion.w;
                }
                return sum;
            }

            float EventCrest(float2 p)
            {
                float sum = 0;
                [loop] for (int i = 0; i < _WaterEventCount; i++)
                {
                    float4 origin = _WaterEvents[i * 2];
                    float age = _Song - origin.w;
                    if (age < 0 || age > 14) continue;
                    float4 motion = _WaterEvents[i * 2 + 1];
                    float2 delta = p - origin.xz - motion.xy * motion.z * age * 0.32;
                    float radius = length(delta);
                    float phase = radius * 0.16 - age * (2.1 + motion.z * 0.035);
                    float ringEnvelope = exp(-abs(radius - age * (12 + motion.z * 0.35)) * 0.032) * exp(-age * 0.12);
                    float ring = pow(saturate(0.5 + 0.5 * sin(phase)), 20) * ringEnvelope;
                    float backward = max(0, -dot(delta, motion.xy));
                    float wakeAxis = abs(delta.x * motion.y - delta.y * motion.x);
                    float arm = exp(-pow((wakeAxis - backward * 0.24) / 3.2, 2))
                        * smoothstep(8, 24, backward) * exp(-backward * 0.0033) * exp(-age * 0.08);
                    sum += (ring * 0.72 + arm * 0.9) * motion.w;
                }
                return saturate(sum);
            }

            float Height(float2 p)
            {
                float t = _Song;
                float baseWave = sin(dot(p, float2(0.014, 0.007)) + t * 0.48) * 0.7
                    + sin(dot(p, float2(-0.008, 0.018)) - t * 0.34) * 0.44
                    + sin(dot(p, float2(0.036, -0.025)) + t * 0.82) * 0.12;
                return baseWave + EventWave(p) * 1.05;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(input.positionOS);
                float h = Height(p.xz);
                p.y += h;
                float hx = Height(p.xz + float2(2, 0)) - Height(p.xz - float2(2, 0));
                float hz = Height(p.xz + float2(0, 2)) - Height(p.xz - float2(0, 2));
                o.positionWS = p;
                o.normalWS = normalize(float3(-hx * 0.25, 1, -hz * 0.25));
                o.positionCS = TransformWorldToHClip(p);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 normal = normalize(i.normalWS);
                float normalView = dot(normal, view);
                float facing = abs(normalView);
                float fresnel = pow(1 - facing, 3.2);
                float3 faceNormal = normalView < 0 ? -normal : normal;
                float3 halfVector = normalize(view + normalize(float3(-0.34, 0.86, 0.38)));
                float specular = pow(saturate(dot(faceNormal, halfVector)), 32) * 0.28;
                float2 p = i.positionWS.xz;
                float2 warp = float2(
                    sin(p.y * 0.019 + p.x * 0.009 + _Song * 0.18),
                    sin(p.x * 0.017 - p.y * 0.013 - _Song * 0.15)) * 19;
                float2 shimmerP = p + warp;
                float shimmerA = sin(dot(shimmerP, float2(0.073, 0.041)) + _Song * 0.55);
                float shimmerB = sin(dot(shimmerP, float2(-0.048, 0.089)) - _Song * 0.37
                    + sin(dot(p, float2(0.021, 0.026)) + _Song * 0.23) * 1.3);
                float crest = pow(saturate(0.5 + 0.5 * (shimmerA * 0.66 + shimmerB * 0.34)), 20);
                float eventCrest = EventCrest(p);
                float narrowSpecular = specular * crest;
                float3 deep = float3(0.002, 0.012, 0.017);
                float3 cyan = float3(0.055, 0.66, 0.68);
                float3 pearl = float3(0.74, 1.0, 0.91);
                float3 gold = float3(1.0, 0.58, 0.26);
                float luminous = saturate(crest * 0.85 + eventCrest * 0.9 + narrowSpecular * 0.55);
                float3 color = lerp(deep, cyan, crest * 0.72 + eventCrest * 0.48);
                color = lerp(color, pearl, saturate(crest * 0.38 + eventCrest * 0.55 + narrowSpecular));
                color = lerp(color, gold, eventCrest * 0.08);
                float alpha = min(0.16, 0.025 + fresnel * 0.035 + crest * 0.075 + eventCrest * 0.075 + narrowSpecular * 0.055);
                return half4(color * (1 + luminous * 0.24), alpha);
            }
            ENDHLSL
        }
    }
}
