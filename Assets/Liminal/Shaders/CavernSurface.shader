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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float strata = 0.5 + 0.5 * sin(p.y * 0.28 + sin(p.x * 0.055) * 2.7 + sin(p.z * 0.08));
                float ridge = pow(saturate(strata), 3.0);
                float sweepCoord = frac(p.x * 0.0017 + p.y * 0.0029 + p.z * 0.0011);
                float sweepDistance = abs(frac(sweepCoord - _CaveSweep + 0.5) - 0.5);
                float sweepLine = exp(-sweepDistance * sweepDistance * 2200.0) * ridge;
                float rock = 0.008 + _CaveReveal * (0.045 + ridge * 0.105 + sweepLine * 0.16);
                float3 baseTint = input.color.rgb * _BaseTint.rgb;
                float3 color = baseTint * rock * (0.96 + _CavePulse * 0.08);
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
                float caustic = pow(saturate(sin(p.x * 0.13 + p.z * 0.17 + _CaveSong * 0.22)
                    * sin(p.z * 0.11 - p.y * 0.09 - _CaveSong * 0.18)), 6.0);
                color += localGlow * (0.018 + ridge * 0.045 + sweepLine * (0.035 + caustic * 0.06));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
