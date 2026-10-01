Shader "Liminal/Hermit Matter"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 1.75
        _Song ("Song Time", Float) = 0
        _Beat ("Authored Beat", Float) = 0
        _State ("Form State", Float) = 0
        _DeathSong ("Scatter Start", Float) = 0
        _DeathBeat ("Scatter Beat", Float) = 0
        _GaitOffset ("Gait Offset", Float) = 0
        _MergeSlot ("Merge Slot", Float) = -1
        _MergeProgress ("Merge Progress", Float) = 0
        _RefugeProgress ("Refuge Progress", Float) = 0
        _BossRoot ("Giant Root", Vector) = (0,0,0,0)
        _RefugeRoot ("Refuge Root", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
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
            float4 _Tint;
            float _Gain;
            float _Song;
            float _Beat;
            float _State;
            float _DeathSong;
            float _DeathBeat;
            float _GaitOffset;
            float _MergeSlot;
            float _MergeProgress;
            float _RefugeProgress;
            float4 _BossRoot;
            float4 _RefugeRoot;
            CBUFFER_END

            struct Input
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 data : TEXCOORD1;
                float4 extra : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            static const float TAU = 6.28318530718;

            float Hash(float value, float salt)
            {
                return frac(sin(value * 127.1 + salt * 311.7) * 43758.5453);
            }

            float3 UnitSphere(float a, float b, float c)
            {
                float y = b * 2.0 - 1.0;
                float angle = a * TAU;
                float radius = pow(c, 1.0 / 3.0);
                float ring = sqrt(max(0.0, 1.0 - y * y));
                return float3(cos(angle) * ring, y, sin(angle) * ring) * radius;
            }

            float3 SmallPose(float3 p, float part, float legIndex, float legT, float beat)
            {
                if (part < 1.5 || part > 2.5)
                    return p;

                float alternate = fmod(legIndex, 2.0) * 0.5;
                float phase = frac(beat + _GaitOffset + alternate);
                float stride;
                float lift;
                if (phase < 0.62)
                {
                    stride = 0.7 - (phase / 0.62) * 1.4;
                    lift = 0.0;
                }
                else
                {
                    float swing = (phase - 0.62) / 0.38;
                    stride = -0.7 + swing * 1.4;
                    lift = sin(swing * 3.14159265) * 0.82;
                }

                float distal = smoothstep(0.24, 1.0, legT);
                p.z += stride * distal;
                p.y += lift * distal;
                p.x += sin(phase * TAU + legIndex) * 0.13 * legT;
                p.x += sin(phase * TAU + legIndex * 0.7) * sin(legT * 3.14159265) * 0.11;
                return p;
            }

            float3 ScatteredPoint(float3 source, float id, float seed, float elapsed)
            {
                float a = Hash(id, 14.0) * TAU;
                float y = Hash(id, 15.0) * 2.0 - 1.0;
                float ring = sqrt(max(0.0, 1.0 - y * y));
                float3 drift = float3(cos(a) * ring, y * 0.8, sin(a) * ring);
                float distance = min(elapsed * (1.6 + seed * 1.5), 22.0) * (0.45 + Hash(id, 16.0));
                float sway = sin(elapsed * 0.37 + seed * TAU) * 0.65;
                float sway2 = cos(elapsed * 0.29 + seed * 19.0) * 0.48;
                return source + drift * distance + float3(sway, sway2, -sway * 0.55);
            }

            float3 GiantPoint(float id, out float part, out float accent)
            {
                float selector = Hash(id, 1.0);
                float a = Hash(id, 2.0);
                float b = Hash(id, 3.0);
                float c = Hash(id, 4.0);
                float d = Hash(id, 5.0);
                float3 p;

                if (selector < 0.60)
                {
                    float radius = sqrt(a) * 0.985;
                    float angle = b * TAU;
                    float x = cos(angle) * radius * 27.0;
                    float z = -5.0 + sin(angle) * radius * 18.0;
                    float q = x * x / (27.0 * 27.0) + (z + 5.0) * (z + 5.0) / (18.0 * 18.0);
                    p = float3(x, 1.6 + 22.0 * sqrt(max(0.0, 1.0 - q)), z);
                    part = 0.0;
                    accent = radius;
                }
                else if (selector < 0.645)
                {
                    float t = a;
                    float angle = t * 8.0 * 3.14159265;
                    float radius = 0.25 + t * 15.0;
                    float x = 0.5 + cos(angle) * radius;
                    float z = -5.0 + sin(angle) * radius * 0.73;
                    float q = x * x / (27.0 * 27.0) + (z + 5.0) * (z + 5.0) / (18.0 * 18.0);
                    p = float3(x, 1.9 + 22.0 * sqrt(max(0.0, 1.0 - q)), z);
                    part = 1.0;
                    accent = t;
                }
                else if (selector < 0.835)
                {
                    float leg = floor(a * 6.0);
                    float side = leg < 3.0 ? -1.0 : 1.0;
                    float localLeg = fmod(leg, 3.0);
                    float zBase = localLeg < 0.5 ? 13.0 : localLeg < 1.5 ? 0.0 : -13.0;
                    float t = b;
                    float3 root = float3(side * 12.0, 5.2, zBase * 0.75);
                    float3 knee = float3(side * 23.0, 2.4, zBase + (localLeg < 0.5 ? 3.0 : localLeg > 1.5 ? -2.7 : 0.0));
                    float3 foot = float3(side * 34.5, 0.35, zBase + (localLeg < 0.5 ? 4.6 : localLeg > 1.5 ? -4.0 : 0.0));
                    p = t < 0.56 ? lerp(root, knee, t / 0.56) : lerp(knee, foot, (t - 0.56) / 0.44);
                    float phase = frac(_Beat + _GaitOffset + fmod(leg, 2.0) * 0.5);
                    float stride;
                    float lift;
                    if (phase < 0.62)
                    {
                        stride = 1.65 - (phase / 0.62) * 3.3;
                        lift = 0.0;
                    }
                    else
                    {
                        float swing = (phase - 0.62) / 0.38;
                        stride = -1.65 + swing * 3.3;
                        lift = sin(swing * 3.14159265) * 2.0;
                    }
                    float distal = smoothstep(0.24, 1.0, t);
                    p.z += stride * distal;
                    p.y += lift * distal;
                    p.x += sin(phase * TAU + leg) * 0.45 * t;
                    part = 2.0;
                    accent = t;
                }
                else if (selector < 0.945)
                {
                    float side = a < 0.5 ? -1.0 : 1.0;
                    float scale = side > 0.0 ? 1.24 : 0.84;
                    float clawFlex = sin((_Beat + _GaitOffset + (side > 0.0 ? 0.25 : 0.0)) * 3.14159265) * 0.8;
                    if (b < 0.7)
                    {
                        float3 sphere = UnitSphere(c, d, Hash(id, 8.0));
                        p = float3(side * 30.0, 4.0, 16.5 + clawFlex) + sphere * float3(3.1, 2.8, 3.6) * scale;
                    }
                    else
                    {
                        float finger = floor(c * 2.0);
                        float t = d;
                        float inner = finger < 0.5 ? -1.0 : 1.0;
                        p = float3(side * (30.2 + (0.7 + t * 1.2) * scale),
                            3.7 + inner * (0.65 + 0.35 * sin(t * 3.14159265)),
                            18.7 + t * 6.2 * scale + clawFlex);
                    }
                    part = 3.0;
                    accent = side > 0.0 ? 1.0 : 0.0;
                }
                else if (selector < 0.985)
                {
                    float3 sphere = UnitSphere(a, b, c);
                    p = float3(sphere.x * 14.0, 3.1 + sphere.y * 4.0, 8.3 + sphere.z * 11.0);
                    part = 4.0;
                    accent = d;
                }
                else
                {
                    float side = a < 0.5 ? -1.0 : 1.0;
                    if (b < 0.64)
                    {
                        float t = c;
                        p = float3(side * (4.0 + t * 1.2), 5.5 + t * 9.3, 21.0 + t * 2.0);
                    }
                    else
                    {
                        p = float3(side * 5.2, 15.0, 23.2) + UnitSphere(c, d, Hash(id, 12.0)) * 1.5;
                    }
                    part = 5.0;
                    accent = 1.0;
                }

                return p;
            }

            float3 RefugePoint(float id, out float part, out float accent)
            {
                float selector = Hash(id, 21.0);
                float a = Hash(id, 22.0);
                float b = Hash(id, 23.0);
                float c = Hash(id, 24.0);
                float d = Hash(id, 25.0);
                float3 p;

                if (selector < 0.48)
                {
                    float t = a;
                    float angle = t * 6.4 * 3.14159265;
                    float radius = 0.65 + t * 25.0;
                    float cross = (b - 0.5) * 0.85;
                    p = float3(cos(angle) * radius - sin(angle) * cross,
                        0.55 + (c - 0.5) * 0.55,
                        -2.0 + sin(angle) * radius + cos(angle) * cross);
                    part = 0.0;
                    accent = t;
                }
                else if (selector < 0.77)
                {
                    float branch = floor(a * 12.0);
                    float t = b;
                    float angle = branch * (TAU / 12.0);
                    float branchScale = 0.72 + Hash(id, 26.0) * 0.42;
                    float radius = t * 21.0 * branchScale;
                    float sway = sin(t * 5.0 + branch) * t * 1.25;
                    p = float3(cos(angle) * radius - sin(angle) * sway,
                        0.65 + t * (8.0 + c * 7.0),
                        sin(angle) * radius + cos(angle) * sway);
                    part = 1.0;
                    accent = t;
                }
                else
                {
                    float radius = sqrt(a) * 23.0;
                    float angle = b * TAU;
                    float x = cos(angle) * radius;
                    float z = sin(angle) * radius * 0.78;
                    p = float3(x, 0.6 + (1.0 - radius / 23.0) * 2.3 + (c - 0.5) * 0.75, z);
                    part = 2.0;
                    accent = d;
                }

                return p;
            }

            float3 GiantColor(float part, float accent, float id)
            {
                float3 teal = float3(0.018, 0.34, 0.36);
                float3 seaGreen = float3(0.035, 0.62, 0.46);
                float3 gold = float3(0.92, 0.58, 0.16);
                if (part < 0.5)
                    return lerp(teal, seaGreen, accent * 0.66);
                if (part < 1.5)
                    return lerp(float3(0.64, 0.36, 0.1), float3(1.0, 0.73, 0.24), accent * 0.72 + 0.28);
                if (part < 2.5)
                    return lerp(float3(0.025, 0.36, 0.4), float3(0.08, 0.72, 0.67), accent * 0.64);
                if (part < 3.5)
                    return accent > 0.5 ? lerp(seaGreen, gold, 0.34) : lerp(teal, seaGreen, 0.42);
                if (part < 4.5)
                    return float3(0.025, 0.27, 0.31);
                return lerp(float3(0.05, 0.65, 0.6), gold, 0.72 + Hash(id, 31.0) * 0.24);
            }

            float3 RefugeColor(float part, float accent, float id)
            {
                if (part < 0.5)
                {
                    float glint = smoothstep(0.76, 0.98, Hash(id, 32.0));
                    return lerp(float3(0.035, 0.48, 0.48), float3(0.9, 0.6, 0.19), glint * 0.55);
                }
                if (part < 1.5)
                    return lerp(float3(0.025, 0.42, 0.4), float3(0.22, 0.76, 0.56), accent * 0.72);
                return lerp(float3(0.025, 0.27, 0.31), float3(0.08, 0.51, 0.43), accent * 0.55);
            }

            Varyings Vert(Input input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float3 source;
                float3 world;
                float3 color = input.color.rgb;
                float id = input.data.x;
                float matterScale = 1.0;

                if (_State < 0.5)
                {
                    p = SmallPose(p, input.data.y, input.extra.x, input.extra.y, _Beat);
                    world = TransformObjectToWorld(p);
                }
                else if (_State < 1.5)
                {
                    p = SmallPose(p, input.data.y, input.extra.x, input.extra.y, _DeathBeat);
                    source = TransformObjectToWorld(p);
                    float elapsed = max(0.0, _Song - _DeathSong);
                    world = ScatteredPoint(source, id, input.data.z, elapsed);
                }
                else if (_State < 2.5)
                {
                    p = SmallPose(p, input.data.y, input.extra.x, input.extra.y, _DeathBeat);
                    source = TransformObjectToWorld(p);
                    float elapsed = max(0.0, _Song - _DeathSong);
                    float3 scattered = ScatteredPoint(source, id, input.data.z, elapsed);
                    float globalId = _MergeSlot * 3200.0 + id;
                    float giantPart, giantAccent;
                    float3 giant = _BossRoot.xyz + GiantPoint(globalId, giantPart, giantAccent);
                    float blend = smoothstep(0.0, 1.0, _MergeProgress);
                    world = lerp(scattered, giant, blend);
                    matterScale = lerp(1.0, 7.5, blend);
                    color = lerp(color, GiantColor(giantPart, giantAccent, globalId), blend);
                }
                else if (_State < 3.5)
                {
                    float globalId = _MergeSlot * 3200.0 + id;
                    float giantPart, giantAccent;
                    float refugePart, refugeAccent;
                    float3 giant = _BossRoot.xyz + GiantPoint(globalId, giantPart, giantAccent);
                    float3 refuge = _RefugeRoot.xyz + RefugePoint(globalId, refugePart, refugeAccent);
                    float blend = smoothstep(0.0, 1.0, _RefugeProgress);
                    world = lerp(giant, refuge, blend);
                    matterScale = lerp(7.5, 6.3, blend);
                    color = lerp(GiantColor(giantPart, giantAccent, globalId),
                        RefugeColor(refugePart, refugeAccent, globalId), blend);
                }
                else
                {
                    world = TransformObjectToWorld(p);
                }

                float distanceToCamera = length(_WorldSpaceCameraPos - world);
                float size = input.uv.z * matterScale * max(1.0, distanceToCamera * 0.0028);
                float glintMask = step(0.992, Hash(id, 47.0));
                float4 centerClip = TransformWorldToHClip(world);
                float pixelsPerWorldUnit = abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5 /
                    max(abs(centerClip.w), 0.001);
                float minimumRadiusPixels = lerp(1.4, 2.3, glintMask);
                size = max(size, minimumRadiusPixels / max(pixelsPerWorldUnit, 0.001));
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                world += (cameraRight * input.uv.x + cameraUp * input.uv.y) * size;

                float pulse = 0.94 + 0.06 * (0.5 + 0.5 * sin(_Beat * TAU + input.data.z * TAU));
                float sparkle = glintMask *
                    (0.45 + 0.55 * sin(_Song * 3.6 + input.data.z * 29.0));
                float distanceFade = exp(-distanceToCamera * 0.0014);
                color *= _Tint.rgb * _Gain * 3.5 * pulse * distanceFade;
                color *= 1.0 + max(0.0, sparkle) * 0.72;

                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.color = float4(color, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 22.0) * 1.55;
                float halo = exp(-radius * 4.5) * 0.34;
                return half4(input.color.rgb * (core + halo), 1.0);
            }
            ENDHLSL
        }
    }
}
