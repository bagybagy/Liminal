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
                    float2 delta = p - origin.xz - motion.xy * motion.z * age * 0.06;
                    float radius = length(delta);
                    float rippleRadius = 3 + age * (4 + motion.z * 0.1);
                    float ripple = sin(radius * 0.34 - age * 2.4)
                        * exp(-abs(radius - rippleRadius) * 0.18) * exp(-age * 0.62);
                    float side = delta.x * motion.y - delta.y * motion.x;
                    float backward = max(0, -dot(delta, motion.xy));
                    float wakeWidth = 2.8 + backward * 0.018;
                    float wakeGate = smoothstep(4, 15, backward) * exp(-backward * 0.012) * exp(-age * 0.24);
                    float shoulder = exp(-pow((abs(side) - backward * 0.22) / wakeWidth, 2)) * wakeGate;
                    float channel = exp(-pow(side / (wakeWidth * 0.42), 2)) * wakeGate;
                    float impact = smoothstep(0.85, 1.3, motion.w);
                    sum += (ripple * (0.12 + impact * 0.15) + shoulder * 0.42 - channel * 0.14) * motion.w;
                }
                return clamp(sum, -0.75, 0.85);
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
                    float2 delta = p - origin.xz - motion.xy * motion.z * age * 0.06;
                    float radius = length(delta);
                    float impact = smoothstep(0.85, 1.3, motion.w);
                    float ringRadius = 3 + age * (4 + motion.z * 0.1);
                    float phase = radius * 0.34 - age * 2.4;
                    float ringEnvelope = exp(-abs(radius - ringRadius) * 0.28) * exp(-age * 0.62);
                    float2 radial = delta / max(radius, 0.001);
                    float2 local = float2(dot(radial, motion.xy), radial.x * motion.y - radial.y * motion.x);
                    float cos2 = local.x * local.x - local.y * local.y;
                    float sin2 = 2 * local.x * local.y;
                    float arc = cos2 * cos2 - sin2 * sin2;
                    float breakup = smoothstep(-0.15, 0.5, arc);
                    float ring = age < 2.4 ? pow(saturate(0.5 + 0.5 * sin(phase)), 10) * ringEnvelope * breakup * impact * 0.44 : 0;
                    float side = delta.x * motion.y - delta.y * motion.x;
                    float backward = max(0, -dot(delta, motion.xy));
                    float wakeWidth = 2.8 + backward * 0.018;
                    float wakeGate = smoothstep(4, 15, backward) * exp(-backward * 0.012) * exp(-age * 0.24);
                    float arm = exp(-pow((abs(side) - backward * 0.22) / wakeWidth, 2)) * wakeGate;
                    float pulse = smoothstep(-0.15, 0.55, sin(backward * 0.16 + i * 2.1));
                    float channel = exp(-pow(side / (wakeWidth * 0.42), 2)) * wakeGate;
                    sum += ring + arm * pulse * motion.w * 0.32 - channel * motion.w * 0.14;
                }
                return clamp(sum, -0.4, 0.7);
            }

            float Height(float2 p)
            {
                float t = _Song;
                float baseWave = sin(dot(p, float2(0.014, 0.007)) + t * 0.48) * 0.7
                    + sin(dot(p, float2(-0.008, 0.018)) - t * 0.34) * 0.44
                    + sin(dot(p, float2(0.036, -0.025)) + t * 0.82) * 0.12;
                return baseWave + EventWave(p);
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
                float specular = pow(saturate(dot(faceNormal, halfVector)), 32) * 0.18;
                float2 p = i.positionWS.xz;
                float2 warp = float2(
                    sin(p.y * 0.019 + p.x * 0.009 + _Song * 0.18),
                    sin(p.x * 0.017 - p.y * 0.013 - _Song * 0.15)) * 19;
                float2 shimmerP = p + warp;
                float shimmerA = sin(dot(shimmerP, float2(0.073, 0.041)) + _Song * 0.55);
                float shimmerB = sin(dot(shimmerP, float2(-0.048, 0.089)) - _Song * 0.37
                    + sin(dot(p, float2(0.021, 0.026)) + _Song * 0.23) * 1.3);
                float crest = pow(saturate(0.5 + 0.5 * (shimmerA * 0.66 + shimmerB * 0.34)), 20);
                float eventSignal = EventCrest(p);
                float eventCrest = saturate(eventSignal);
                float wakeShadow = saturate(-eventSignal * 2.2);
                float narrowSpecular = specular * crest;
                float3 deep = float3(0.002, 0.012, 0.017);
                float3 cyan = float3(0.028, 0.32, 0.35);
                float3 pearl = float3(0.48, 0.78, 0.74);
                float luminous = saturate(crest * 0.55 + eventCrest * 0.62 + narrowSpecular * 0.35);
                float3 color = lerp(deep, cyan, saturate(crest * 0.42 + eventCrest * 0.42));
                color = lerp(color, pearl, saturate(crest * 0.18 + eventCrest * 0.28 + narrowSpecular * 0.45));
                color = lerp(color, deep * 0.72, wakeShadow * 0.2);
                float alpha = min(0.1, 0.018 + fresnel * 0.025 + crest * 0.042 + eventCrest * 0.045 + narrowSpecular * 0.03);
                return half4(color * (1 + luminous * 0.12), alpha);
            }
            ENDHLSL
        }
    }
}
