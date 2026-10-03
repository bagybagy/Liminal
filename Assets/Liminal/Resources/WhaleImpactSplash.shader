Shader "Liminal/Whale Impact Splash"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+9" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/WhaleSplashMotion.hlsl"
            float3 _ImpactOrigin, _ImpactForward;
            float _ImpactAge, _ImpactScale, _Gain, _DensityGain;
            struct Attributes { uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float streak : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                uint id = input.vertexID / 6u;
                float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1),
                    float2(-1,-1), float2(1,1), float2(1,-1) };
                float2 uv = corners[input.vertexID % 6u];
                float4 seed;
                float3 velocity;
                float3 local = SplashTrajectory(id, _ImpactAge, _ImpactScale, seed, velocity);
                float3 side = float3(_ImpactForward.z, 0, -_ImpactForward.x);
                float3 center = _ImpactOrigin + side * local.x + float3(0, local.y, 0) + _ImpactForward * local.z;
                float t = max(0, _ImpactAge - .04 - seed.y * .18);
                float breakup = smoothstep(.45, 2.2, t);
                float rising = smoothstep(0, .12, t);
                float returned = 1 - smoothstep(4.4, 5.9, _ImpactAge);
                float afloat = smoothstep(0, 12, local.y);
                float fade = rising * returned * lerp(.14, 1, afloat) * step(0, _ImpactAge);
                // Dense sheets become separated droplets; the same particle IDs survive the change.
                float sheet = 1 - smoothstep(.6, 2.5, t);
                float core = 1 - step(.45, seed.z);
                float streak = step(.87, seed.z);
                float radius = lerp(.32 + seed.w * .40, .9 + seed.w * 1.15, sheet);
                radius *= lerp(1, 1.2, core);
                float pixelWorld = max(.0001, abs(TransformWorldToHClip(center).w) * 2 /
                    (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y));
                radius = max(radius, pixelWorld * .65);
                float length = lerp(radius, 1.4 + seed.w * 2.6, streak * breakup * afloat);
                float3 direction = normalize(side * velocity.x + float3(0, velocity.y, 0) +
                    _ImpactForward * velocity.z + float3(0, .001, 0));
                float3 view = normalize(_WorldSpaceCameraPos - center);
                float3 right = cross(view, direction);
                if (dot(right, right) < .0001) right = UNITY_MATRIX_V[0].xyz;
                right = normalize(right);
                float3 up = normalize(cross(right, view));
                float3 position = center + right * (uv.x * radius) + up * (uv.y * length);
                o.positionCS = TransformWorldToHClip(position);
                o.uv = uv;
                o.streak = streak;
                float crest = pow(saturate(local.y / max(1, 185 * _ImpactScale)), 1.5);
                float glint = pow(saturate(.5 + .5 * sin(seed.x * 91 + t * 7 + seed.y * 17)), 14);
                float pearl = saturate(crest * .32 + glint * .42);
                float3 color = lerp(float3(.009, .24, .76), float3(.34, .88, 1.0), pearl);
                float energy = lerp(.13, .32, breakup) + glint * .85 + crest * .22;
                // Keep the mass blue and reserve bright peaks for droplets, not a white Bloom slab.
                o.color = float4(color * (energy * fade * _Gain * _DensityGain), fade);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = dot(i.uv, i.uv);
                clip(1 - r);
                float weight = lerp(exp(-r * 5.5), exp(-i.uv.x * i.uv.x * 7) *
                    (1 - smoothstep(.65, 1, abs(i.uv.y))), i.streak);
                return half4(i.color.rgb * weight, i.color.a * weight);
            }
            ENDHLSL
        }
    }
}
