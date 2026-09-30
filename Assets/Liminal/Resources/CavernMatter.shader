Shader "Liminal/Cavern Matter"
{
    Properties
    {
        _Tint ("Matter Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 2.6
        [HideInInspector] _CaveSong ("Authored Song Time", Float) = 0
        [HideInInspector] _CaveReveal ("Authored Room Reveal", Float) = 0
        [HideInInspector] _CaveSweep ("Formation Sweep", Float) = 0
        [HideInInspector] _CaveBind ("Formation Binding", Float) = 0
        [HideInInspector] _CaveWave ("Light Wave", Vector) = (0,0,0,0)
        [HideInInspector] _CaveWaveEnergy ("Light Wave Energy", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Gain;
                float _CaveSong, _CaveReveal, _CaveSweep, _CaveBind;
                float4 _CaveWave;
                float _CaveWaveEnergy;
            CBUFFER_END
            float4 _CaveLights[10];
            float4 _CaveLightColors[10];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 anchor = TransformObjectToWorld(input.positionOS.xyz);
                float3 p = anchor;
                float seed = input.data.x;
                float layer = input.data.y;
                float volumeLayer = 1.0 - step(0.35, layer);
                float formationLayer = step(0.35, layer) * (1.0 - step(0.72, layer));
                float mineralLayer = step(0.72, layer);
                float habitat = pow(saturate(0.5 + 0.5 * sin(anchor.x * 0.028 + anchor.y * 0.021 +
                    sin(anchor.z * 0.018) * 2.4)), 3.0);
                float distanceToCamera = length(_WorldSpaceCameraPos - anchor);
                float flowFade = smoothstep(20.0, 125.0, distanceToCamera);
                float3 phase = float3(seed * 31.7, seed * 53.1, seed * 79.3);
                float3 drift = float3(
                    sin(_CaveSong * 0.21 + phase.x + anchor.z * 0.008),
                    sin(_CaveSong * 0.16 + phase.y + anchor.x * 0.006),
                    cos(_CaveSong * 0.19 + phase.z + anchor.y * 0.007));
                float formationDrift = formationLayer * (1.0 - _CaveBind) * 1.45;
                float mineralDrift = mineralLayer * (1.0 - _CaveBind) * 0.62;
                p += drift * flowFade * (volumeLayer * 2.15 + formationDrift + mineralDrift);

                float sparkle = smoothstep(0.94, 0.99, frac(seed * 1.618 + anchor.x * 0.00013));
                float sweepCoord = frac(dot(anchor, float3(0.0017, 0.0031, 0.0011)) + seed * 0.11);
                float sweepDistance = abs(frac(sweepCoord - _CaveSweep + 0.5) - 0.5);
                float edgeSweep = exp(-sweepDistance * sweepDistance * 1800.0) * _CaveReveal;

                float volumeSelection = smoothstep(0.72, 0.85, seed);
                float volumeEnergy = (0.075 + sparkle * 0.45 + _CaveReveal * 0.08) * volumeSelection;
                float formationEnergy = (0.025 + _CaveReveal * (0.14 + sparkle * 0.24) + edgeSweep * 0.72) * (0.16 + habitat * 0.84);
                float mineralEnergy = (0.012 + _CaveReveal * (0.08 + sparkle * 0.18) + edgeSweep * 1.75) * (0.12 + habitat * 0.88);
                float energy = volumeLayer * volumeEnergy + formationLayer * formationEnergy + mineralLayer * mineralEnergy;

                float3 localGlow = 0;
                [unroll]
                for (int i = 0; i < 10; i++)
                {
                    float3 delta = p - _CaveLights[i].xyz;
                    float radius = max(1.0, _CaveLightColors[i].a);
                    float falloff = 1.0 - smoothstep(radius * 0.2, radius, length(delta));
                    localGlow += _CaveLightColors[i].rgb * (_CaveLights[i].w * falloff * falloff);
                }

                float3 color = input.color.rgb * _Tint.rgb * (_Gain * energy);
                color += localGlow * (0.012 + edgeSweep * 0.10) * (0.12 + habitat * 0.88);
                color *= lerp(1.0, volumeSelection, volumeLayer);
                if (_CaveWaveEnergy > 0.001)
                {
                    float age = max(0.0, _CaveSong - _CaveWave.w);
                    float waveRadius = length(p - _CaveWave.xyz);
                    float ring = exp(-pow((waveRadius - age * 30.0) / 9.0, 2.0)) * exp(-age * 0.55);
                    color += float3(0.32, 0.72, 0.94) * (ring * _CaveWaveEnergy * 0.12);
                }

                float projectionScale = max(1.0, abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y * 0.5);
                float nativeRadiusPixels = input.uv.z * projectionScale / max(1.0, distanceToCamera);
                float maxRadiusPixels = lerp(1.12, 1.65, max(sparkle, edgeSweep));
                float minRadiusPixels = volumeLayer > 0.5 ? 1.10 : 0.95;
                float radiusPixels = clamp(nativeRadiusPixels, minRadiusPixels, maxRadiusPixels);
                float worldRadius = radiusPixels * distanceToCamera / projectionScale;
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                p += (right * input.uv.x + up * input.uv.y) * worldRadius;

                output.positionCS = TransformWorldToHClip(p);
                output.uv = input.uv.xy;
                output.color = float4(color, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = dot(input.uv, input.uv);
                clip(1.0 - r);
                float soft = exp(-r * 3.0) * 0.48 + exp(-r * 15.0) * 0.42 + exp(-r * 38.0) * 0.15;
                float crisp = exp(-r * 4.0) * 0.14 + exp(-r * 8.0) * 0.82;
                float crispness = saturate(_CaveBind * 0.7 + _CaveReveal * 0.32);
                return half4(input.color.rgb * lerp(soft, crisp, crispness), 1.0);
            }
            ENDHLSL
        }
    }
}
