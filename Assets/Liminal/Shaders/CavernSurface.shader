Shader "Liminal/Cavern Surface"
{
    Properties
    {
        _BaseTint ("Cavern Tint", Color) = (1, 1, 1, 1)
        _CavePulse ("Ambient Pulse", Float) = 0
        _CaveReveal ("Authored Room Reveal", Float) = 0
        _CaveSweep ("Formation Sweep", Float) = 0
        _CaveBind ("Formation Binding", Float) = 0
        _CaveSong ("Authored Song Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTint;
                float _CavePulse, _CaveReveal, _CaveSweep, _CaveBind, _CaveSong;
            CBUFFER_END
            float4 _CaveLights[10];
            float4 _CaveLightColors[10];

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3(float3 p)
            {
                float3 cell = floor(p);
                float3 blend = frac(p);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float a = lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), blend.x);
                float b = lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), blend.x);
                float c = lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), blend.x);
                float d = lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + float3(1, 1, 1)), blend.x);
                return lerp(lerp(a, b, blend.y), lerp(c, d, blend.y), blend.z);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 color : COLOR;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float3 normal = normalize(input.normalWS);
                float warp = Noise3(p * 0.0065 + 17.3) - 0.5;
                float3 veinPosition = p * 0.020 + normal * (0.22 + warp * 0.28) + warp * 1.8;
                float veinField = Noise3(veinPosition);
                float fracture = Noise3(p * 0.038 + normal * 0.35 + 41.7);
                float vein = 1.0 - smoothstep(0.003, 0.015, abs(veinField - 0.51));
                vein *= smoothstep(0.48, 0.68, fracture);
                float sweepField = Noise3(p * 0.008 + normal * 0.10 + 63.1);
                float sweepDistance = abs(frac(sweepField - _CaveSweep + 0.5) - 0.5);
                float edgePulse = exp(-sweepDistance * sweepDistance * 240.0) * _CaveReveal *
                    (vein * 0.72 + smoothstep(0.54, 0.70, fracture) * 0.12);
                float reliefShade = 0.76 + 0.24 * saturate(dot(abs(normal), normalize(float3(0.34, 0.82, 0.46))));
                float grain = Noise3(p * 0.012 + normal * 0.18 + 89.4);
                float rock = 0.006 + _CaveReveal * (0.022 + grain * 0.014);
                float3 baseTint = input.color.rgb * _BaseTint.rgb;
                float3 color = baseTint * rock * reliefShade * (0.96 + _CavePulse * 0.08);
                color += baseTint * vein * (0.018 + _CaveReveal * 0.13) * reliefShade;
                float3 localGlow = 0;
                [unroll]
                for (int i = 0; i < 10; i++)
                {
                    float3 delta = input.positionWS - _CaveLights[i].xyz;
                    float distanceToLight = length(delta);
                    float radius = max(1.0, _CaveLightColors[i].a);
                    float falloff = 1.0 - smoothstep(radius * 0.25, radius, distanceToLight);
                    localGlow += _CaveLightColors[i].rgb * (_CaveLights[i].w * falloff * falloff);
                }
                color += localGlow * (0.010 + vein * 0.055 + edgePulse * 0.14);
                color += baseTint * edgePulse * 0.20;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
