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
        _CavePlayerPosition ("Player Position", Vector) = (0, 0, 0, 0)
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
                float4 _CavePlayerPosition;
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
                float slowTime = _CaveSong * 0.006;
                float3 strokeWarp = float3(
                    Noise3(p * 0.0048 + normal * 0.16 + float3(11.3, 5.1, slowTime)) - 0.5,
                    Noise3(p * 0.0045 + normal * 0.19 + float3(31.7, -slowTime * 0.7, 7.9)) - 0.5,
                    Noise3(p * 0.0051 + normal * 0.14 + float3(-slowTime * 0.5, 53.2, 19.4)) - 0.5);
                float strokeField = Noise3(p * 0.013 + normal * (0.18 + warp * 0.35) + strokeWarp * 1.35 +
                    float3(slowTime, -slowTime * 0.65, slowTime * 0.42));
                float strokeFracture = Noise3(p * 0.032 + normal * 0.27 +
                    float3(-slowTime * 0.8, slowTime * 0.36, 37.1));
                float contour = (1.0 - smoothstep(0.006, 0.021, abs(strokeField - 0.515))) *
                    smoothstep(0.43, 0.65, strokeFracture);
                float proximity = 1.0 - smoothstep(85.0, 260.0, length(p - _CavePlayerPosition.xyz));
                float depthLayer = smoothstep(90.0, 520.0, length(_WorldSpaceCameraPos - p));
                float rock = 0.008 + depthLayer * 0.005 + _CaveReveal * (0.024 + grain * 0.014);
                float3 baseTint = input.color.rgb * _BaseTint.rgb;
                float3 color = baseTint * rock * reliefShade * (0.94 + _CavePulse * 0.10);
                color += baseTint * vein * (0.020 + _CaveReveal * 0.11) * reliefShade;
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
                float waveLayer = saturate(length(localGlow) * 0.06);
                float contourEnergy = 0.024 + proximity * 0.024 + _CavePulse * 0.012 + waveLayer * 0.014;
                color += baseTint * contour * contourEnergy * reliefShade;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
