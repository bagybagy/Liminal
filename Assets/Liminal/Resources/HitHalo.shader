Shader "Liminal/Hit Halo"
{
    Properties
    {
        _HitData ("Hit Position And Time", Vector) = (0,0,0,-1)
        _HitColor ("Hit Color And Strength", Vector) = (1,1,1,1)
        _HitSong ("Song Time", Float) = 0
        _HitReduced ("Reduced Motion", Float) = 0
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
            float4 _HitData;
            float4 _HitColor;
            float _HitSong;
            float _HitReduced;
            CBUFFER_END

            struct Input
            {
                float3 positionOS : POSITION;
                float4 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float elapsed : TEXCOORD1;
                float life : TEXCOORD2;
                float active : TEXCOORD3;
                float phase : TEXCOORD4;
                float strength : TEXCOORD5;
                float3 palette : COLOR;
                float viewDepth : TEXCOORD6;
            };

            Varyings Vert(Input input)
            {
                Varyings output;
                float strength = max(0.0, _HitColor.a);
                float duration = lerp(0.55, 0.85, saturate((strength - 0.35) / 1.65));
                float elapsed = _HitSong - _HitData.w;
                float active = step(0.0, elapsed) * step(elapsed, duration);
                float3 center = _HitData.xyz;
                float viewDepth = max(0.01, -TransformWorldToView(center).z);
                float pixelRadius = lerp(28.0, 70.0, saturate((strength - 0.35) / 1.65));
                float radiusWorld = pixelRadius * 2.0 * viewDepth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 world = center + (cameraRight * input.uv.x + cameraUp * input.uv.y) * radiusWorld;

                float3 incoming = max(_HitColor.rgb, 0.0);
                float tintLevel = max(max(incoming.r, incoming.g), incoming.b);
                float3 tintHue = incoming / max(tintLevel, 0.0001);
                float warm = saturate((max(tintHue.r - tintHue.b, (tintHue.r - tintHue.g) * 0.55) - 0.04) * 1.7);
                float cyan = saturate((tintHue.g - tintHue.r) * 1.1);
                float3 electricHue = lerp(float3(0.035, 0.22, 1.0), float3(0.015, 0.76, 1.0), cyan);
                float intensity = lerp(0.78, 1.08, saturate(strength / 2.0));

                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.elapsed = elapsed;
                output.life = saturate(elapsed / duration);
                output.active = active;
                output.phase = frac(sin(dot(center, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 6.2831853;
                output.strength = strength;
                output.palette = lerp(electricHue, tintHue, warm) * intensity;
                output.viewDepth = viewDepth;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);
                float radiusSquared = dot(input.uv, input.uv);
                clip(1.0 - radiusSquared);
                float radius = sqrt(radiusSquared);
                float life = input.life;
                float fade = 1.0 - smoothstep(0.78, 1.0, life);
                float expansion = lerp(1.0, 0.72, _HitReduced);

                float outerRadius = lerp(0.22, 0.97 * expansion, smoothstep(0.0, 0.96, life));
                float innerRadius = lerp(0.11, 0.74 * expansion, saturate((life - 0.08) / 0.92));
                float outerDistance = (radius - outerRadius) / 0.026;
                float innerDistance = (radius - innerRadius) / 0.031;
                float angle = atan2(input.uv.y, input.uv.x);
                float arc = 0.72 + 0.28 * saturate(0.5 + 0.5 * sin(angle * 7.0 + input.phase + life * 5.0));
                float outerRing = exp(-outerDistance * outerDistance) * arc * 1.25;
                float innerRing = exp(-innerDistance * innerDistance) * 0.82;

                float glintRadius = lerp(0.92, 0.28, smoothstep(0.04, 0.88, life));
                float glintOffset = sin(angle * 5.0 + input.phase) * 0.022;
                float glintDistance = (radius - glintRadius - glintOffset) / 0.034;
                float glintAngles = pow(saturate(0.5 + 0.5 * cos(angle * 7.0 + input.phase + life * 2.4)), 15.0);
                float glints = exp(-glintDistance * glintDistance) * glintAngles *
                    (1.0 - smoothstep(0.66, 0.94, life)) * lerp(1.0, 0.4, _HitReduced);

                float disc = exp(-radiusSquared * 9.0) * (0.68 * exp(-input.elapsed * 5.2) + 0.16 * fade);
                float impact = exp(-radiusSquared * 4.0) * exp(-input.elapsed * 17.0) * 0.32;
                float whiteCore = exp(-radiusSquared * 520.0) *
                    (1.0 - smoothstep(0.0, 0.08, input.elapsed)) * 0.9;
                float intensity = lerp(1.0, 0.76, _HitReduced) * lerp(0.78, 1.15, saturate(input.strength / 2.0));
                float distanceFade = exp(-input.viewDepth * 0.0018);
                float3 color = input.palette * (disc + impact + outerRing + innerRing + glints) + whiteCore;
                return half4(color * fade * intensity * distanceFade, 1.0);
            }
            ENDHLSL
        }
    }
}
