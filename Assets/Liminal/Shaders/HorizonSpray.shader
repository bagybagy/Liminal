Shader "Liminal/Horizon Spray"
{
    Properties { _Pearl ("Pearl", Color) = (0.60, 0.96, 0.88, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+8" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Pearl;
                float _Song;
                int _WaterEventCount;
            CBUFFER_END
            StructuredBuffer<float4> _WaterEvents;

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                uint verticesPerEvent = 256 * 6;
                uint eventIndex = vertexID / verticesPerEvent;
                uint within = vertexID % verticesPerEvent;
                uint bead = within / 6;
                uint corner = within % 6;
                float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                float2 uv = corners[corner];
                float4 origin = _WaterEvents[eventIndex * 2];
                float4 motion = _WaterEvents[eventIndex * 2 + 1];
                float seed = frac(sin((eventIndex * 29 + bead * 71 + 13) * 12.9898) * 43758.5453);
                float age = _Song - origin.w;
                float launch = seed * 0.92;
                float t = age - launch;
                float angle = seed * 6.2831853;
                float3 side = float3(-motion.y, 0, motion.x);
                float spread = (seed * 2 - 1) * (20 + seed * 15);
                float speed = 4.5 + seed * 8.0 + motion.z * 0.08;
                float3 start = origin.xyz + side * spread + float3(cos(angle) * 1.6, 0, sin(angle) * 1.6);
                float verticalSpeed = 12 + seed * 12;
                float flightTime = 2 * verticalSpeed / 9.8;
                float3 velocity = float3(motion.x, 0, motion.y) * (speed * 0.38)
                    + side * (cos(angle) * speed * 0.26) + float3(0, verticalSpeed, 0);
                float3 position = start + velocity * max(0, t) + float3(0, -4.9 * t * t, 0);
                float3 travel = normalize(velocity + float3(0, -9.8 * max(0, t), 0));
                float3 right = normalize(cross(travel, normalize(_WorldSpaceCameraPos - position)) + float3(0.0001,0,0));
                float size = (0.11 + seed * 0.14) * (1 - saturate(t / flightTime) * 0.35);
                float3 pos = position + right * uv.x * size + travel * uv.y * size * (1.4 + speed * 0.045);
                o.positionCS = TransformWorldToHClip(pos);
                o.uv = uv;
                float life = step(0, t) * step(t, flightTime);
                float3 color = lerp(float3(0.10, 0.65, 0.63), _Pearl.rgb, seed * 0.78);
                color = lerp(color, float3(1, 0.56, 0.20), step(0.91, seed) * 0.52);
                o.color = float4(color * (0.55 + seed * 0.72) * life * motion.w, life);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv;
                clip(1 - dot(p, p));
                float core = exp(-dot(p, p) * 4.5);
                return half4(i.color.rgb * core, i.color.a * core);
            }
            ENDHLSL
        }
    }
}
