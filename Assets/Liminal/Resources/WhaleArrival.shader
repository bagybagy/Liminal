Shader "Liminal/Whale Arrival"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+6" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float3 _Origin;
            float _Song, _Age, _PeakTime;
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 color : COLOR; };
            float Hash(float v) { return frac(sin(v * 127.1 + 311.7) * 43758.5453); }
            Varyings Vert(uint id : SV_VertexID)
            {
                uint particleID = id / 6;
                float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                float2 uv = corners[id % 6];
                float strand = particleID % 12;
                float u = (particleID / 12 + Hash(particleID + 9)) / 1000.0;
                float charge = _Age < 0 ? 0 : smoothstep(0, _PeakTime, _Age);
                float turn = strand * 6.2831853 / 12 + u * 8.0 - _Song * (0.24 + charge * 0.55);
                float radius = (8 + pow(u, 1.25) * 92) * lerp(1, 0.30, charge);
                float3 local = float3(cos(turn) * radius, -pow(1-u, 2) * 58, sin(turn) * radius);
                local.y += sin(turn * 2 + u * 4 - _Song * 0.6) * u * 4;
                local += float3(Hash(particleID+23)-0.5,Hash(particleID+41)-0.5,Hash(particleID+59)-0.5) * (0.5 + u * 0.8);
                float spread = smoothstep(_PeakTime, _PeakTime + 0.65, _Age);
                float releaseAge = max(0, _Age - _PeakTime);
                float waveRadius = radius + releaseAge * (35 + u * 24);
                float3 wave = float3(cos(turn) * waveRadius, 28 + sin(turn * 3) * 2, sin(turn) * waveRadius);
                local = lerp(local, wave, spread);
                float settled = smoothstep(11, 20, _Age);
                float3 resting = float3(cos(turn) * (62 + u * 65), -90 + u * 55, sin(turn) * (62 + u * 65));
                local = lerp(local, resting, settled);
                float3 position = _Origin + local;
                float pixelWorld = max(0.0001, abs(TransformWorldToHClip(position).w) * 2 /
                    (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y));
                float glint = pow(0.5 + 0.5 * sin(u * 27 - _Song * 2.3 + strand * 0.7), 10);
                float flash = exp(-pow((_Age - _PeakTime) / 0.14, 2));
                float size = max(0.16, pixelWorld * (1.0 + glint * 0.8 + flash * 2.4));
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                Varyings o;
                o.positionCS = TransformWorldToHClip(position + (right * uv.x + up * uv.y) * size);
                o.uv = uv;
                float luminous = (0.22 + glint * 0.9) * (1 + charge * 2.6 + flash * 12) * lerp(1, 0.06, settled);
                o.color = lerp(float3(0.012,0.24,1.0),float3(0.025,1.45,2.6),saturate(glint + flash * 0.7)) * luminous;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float r = dot(i.uv, i.uv);
                clip(1-r);
                return half4(i.color * (exp(-r * 7) + exp(-r * 2.5) * 0.08),1);
            }
            ENDHLSL
        }
    }
}
