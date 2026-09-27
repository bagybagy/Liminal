Shader "Liminal/Cavern Surface"
{
    Properties
    {
        _BaseTint ("Cavern Tint", Color) = (1, 1, 1, 1)
        _CavePulse ("Ambient Pulse", Float) = 0
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
                float _CavePulse;
            CBUFFER_END
            float4 _CaveLights[8];

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
                float strata = sin(p.y * 0.28 + sin(p.x * 0.055) * 2.7 + sin(p.z * 0.08));
                float rock = 0.009 + 0.007 * saturate(strata * 0.5 + 0.5);
                float3 base = input.color.rgb * _BaseTint.rgb * rock;
                float3 illumination = 0;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float3 delta = input.positionWS - _CaveLights[i].xyz;
                    float distanceToLight = length(delta);
                    float falloff = saturate(1.0 - distanceToLight / 92.0);
                    falloff *= falloff;
                    illumination += _CaveLights[i].w * falloff * float3(0.82, 0.94, 1.0);
                }
                float pulse = 1.0 + _CavePulse * 0.045;
                float caustic = pow(saturate(sin(p.x * 0.13 + p.z * 0.17 + _Time.y * 0.22)
                    * sin(p.z * 0.11 - p.y * 0.09 - _Time.y * 0.18)), 6.0);
                float3 color = base * pulse + base * illumination * (12.0 + caustic * 14.0);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
