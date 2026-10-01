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
        _CrabId ("Persistent Crab ID", Float) = 0
        _MergeSlot ("Merge Slot", Float) = -1
        _MergeProgress ("Merge Progress", Float) = 0
        _RefugeProgress ("Refuge Progress", Float) = 0
        _RefugeSourceState ("Reef Source Form", Float) = 0
        _RefugeSourceElapsed ("Reef Source Scatter Age", Float) = 0
        _ReefBeat ("Reef Frozen Beat", Float) = 0
        _BossRoot ("Giant Root", Vector) = (0,0,0,0)
        _BossRight ("Giant Right", Vector) = (1,0,0,0)
        _BossForward ("Giant Forward", Vector) = (0,0,1,0)
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
            float _Gain, _Song, _Beat, _State;
            float _DeathSong, _DeathBeat, _GaitOffset, _CrabId;
            float _MergeSlot, _MergeProgress, _RefugeProgress;
            float _RefugeSourceState, _RefugeSourceElapsed, _ReefBeat;
            float4 _BossRoot, _BossRight, _BossForward, _RefugeRoot;
            float4 _Foot0, _Foot1, _Foot2, _Foot3, _Foot4, _Foot5;
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
            static const float GIANT_SCALE = 7.5;

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

            // These normalized formulas match HermitGeometry, including target
            // attachments. Only the small feet are supplied as world-planted poses.
            float3 ShellSurface(float t, float v)
            {
                float angle = -(1.0 - t) * TAU * 2.65;
                float radius = 0.08 + 2.15 * pow(t, 1.25);
                float tube = 0.12 + 1.28 * pow(t, 1.35);
                tube += 0.10 * pow(0.5 + 0.5 * cos(v * 9.0 + t * 12.0), 8.0);
                float radial = radius + cos(v) * tube;
                return float3(cos(angle) * radial,
                    9.5 - 6.7 * t + sin(v) * tube * 1.4, -1.35 + sin(angle) * radial);
            }

            float3 LegRoot(float leg)
            {
                float side = leg < 3.0 ? -1.0 : 1.0;
                return float3(side * 1.35, 1.12, (1.0 - fmod(leg, 3.0)) * 1.25);
            }

            float3 RestFoot(float leg)
            {
                float side = leg < 3.0 ? -1.0 : 1.0;
                float fore = 1.0 - fmod(leg, 3.0);
                return float3(side * (4.35 - abs(fore) * 0.2), 0.08, fore * 2.65);
            }

            float3 LegSurface(float leg, float t, float3 foot)
            {
                float3 root = LegRoot(leg);
                float3 knee = lerp(root, foot, 0.52);
                knee.y = 2.75 + max(0.0, foot.y - 0.08) * 0.42;
                return t < 0.52 ? lerp(root, knee, t / 0.52)
                    : lerp(knee, foot, (t - 0.52) / 0.48);
            }

            float3 GiantFoot(float leg, float beat)
            {
                float3 foot = RestFoot(leg);
                float phase = frac(beat + fmod(leg, 2.0) * 0.5);
                if (phase >= 0.62)
                {
                    float swing = (phase - 0.62) / 0.38;
                    foot.y += sin(swing * PI) * 0.85;
                    foot.z += sin(swing * TAU) * 0.45;
                }
                return foot;
            }

            float3 ClawFlex(float side, float beat)
            {
                float flex = sin((beat + (side > 0.0 ? 0.25 : 0.0)) * PI) * 0.12;
                return float3(0.0, flex, flex * 0.5);
            }

            float3 PlantedFoot(float leg)
            {
                if (leg < 0.5) return _Foot0.xyz;
                if (leg < 1.5) return _Foot1.xyz;
                if (leg < 2.5) return _Foot2.xyz;
                if (leg < 3.5) return _Foot3.xyz;
                if (leg < 4.5) return _Foot4.xyz;
                return _Foot5.xyz;
            }

            float3 SmallPose(Input input, float beat)
            {
                float3 p = input.positionOS.xyz;
                float part = input.data.y;
                if (part > 1.5 && part < 2.5)
                {
                    float leg = input.extra.x;
                    float t = input.extra.y;
                    p += LegSurface(leg, t, PlantedFoot(leg)) - LegSurface(leg, t, RestFoot(leg));
                }
                else if (part > 2.5 && part < 3.5)
                    p += ClawFlex(p.x < 0.0 ? -1.0 : 1.0, beat + _GaitOffset);
                return p;
            }

            float3 BossWorld(float3 local)
            {
                return _BossRoot.xyz + _BossRight.xyz * local.x
                    + float3(0.0, local.y, 0.0) + _BossForward.xyz * local.z;
            }

            float3 GiantPoint(Input input, float beat, float id)
            {
                float3 p = input.positionOS.xyz;
                float part = input.data.y;
                if (part < 0.5)
                {
                    // Each of the eight original groups fills the same shell with
                    // interleaved samples, rather than eight coincident copies.
                    float t = input.extra.x;
                    if (t < 0.98)
                        t = saturate(t + (Hash(id, 7.0) - 0.5) * 0.003);
                    p = ShellSurface(t, input.extra.y + _MergeSlot * (TAU / 8.0));
                }
                else if (part > 1.5 && part < 2.5)
                {
                    float leg = input.extra.x;
                    float t = input.extra.y;
                    p += LegSurface(leg, t, GiantFoot(leg, beat)) - LegSurface(leg, t, RestFoot(leg));
                }
                else if (part > 2.5 && part < 3.5)
                    p += ClawFlex(p.x < 0.0 ? -1.0 : 1.0, beat);
                if (part > 0.5)
                    p += UnitSphere(Hash(id, 8.0), Hash(id, 9.0), Hash(id, 10.0)) * 0.035;
                return BossWorld(p * GIANT_SCALE);
            }

            float3 ScatteredPoint(float3 source, float id, float seed, float elapsed)
            {
                float a = Hash(id, 14.0) * TAU;
                float y = Hash(id, 15.0) * 2.0 - 1.0;
                float ring = sqrt(max(0.0, 1.0 - y * y));
                float3 drift = float3(cos(a) * ring, y * 0.8, sin(a) * ring);
                float distance = min(elapsed * (1.6 + seed * 1.5), 22.0) * (0.45 + Hash(id, 16.0));
                float sway = (sin(elapsed * 0.37 + seed * TAU) - sin(seed * TAU)) * 0.65;
                float sway2 = (cos(elapsed * 0.29 + seed * 19.0) - cos(seed * 19.0)) * 0.48;
                return source + drift * distance + float3(sway, sway2, -sway * 0.55);
            }

            float3 RefugePoint(float id, out float part, out float accent)
            {
                float selector = Hash(id, 21.0);
                float a = Hash(id, 22.0), b = Hash(id, 23.0);
                float c = Hash(id, 24.0), d = Hash(id, 25.0);
                float3 p;
                if (selector < 0.24)
                {
                    // A substantial continuous foundation, not a flat spiral.
                    float3 rock = UnitSphere(a, b, c);
                    p = float3(rock.x * 29.0, 0.9 + abs(rock.y) * 5.0, rock.z * 25.0);
                    part = 2.0;
                    accent = d;
                }
                else if (selector < 0.62)
                {
                    // Six thick open barrel vaults joined into the reef base.
                    float module = floor(a * 6.0);
                    float angle = module * (TAU / 6.0);
                    float arch = b * PI;
                    float r = 6.0 + (c - 0.5) * 1.4;
                    float depth = 15.0 + (d - 0.5) * 10.0;
                    float tangent = cos(arch) * r;
                    p = float3(cos(angle) * depth - sin(angle) * tangent,
                        2.4 + sin(arch) * r * 1.65,
                        sin(angle) * depth + cos(angle) * tangent);
                    part = 0.0;
                    accent = b;
                }
                else if (selector < 0.84)
                {
                    // Tubular coral crowns with stable branch shapes and volume.
                    float branch = floor(a * 14.0);
                    float angle = branch * (TAU / 14.0);
                    float t = b;
                    float radius = 14.0 + t * 9.0;
                    float sway = sin(t * 4.0 + branch) * t * 2.2;
                    float tube = (0.65 + (1.0 - t) * 0.9) * sqrt(d);
                    float crossAngle = c * TAU;
                    float lateral = sway + cos(crossAngle) * tube;
                    p = float3(cos(angle) * radius - sin(angle) * lateral,
                        3.0 + t * (16.0 + Hash(branch, 26.0) * 9.0) + sin(crossAngle) * tube,
                        sin(angle) * radius + cos(angle) * lateral);
                    part = 1.0;
                    accent = t;
                }
                else
                {
                    // The giant shell becomes the central shelter and ties the
                    // original marine silhouette into the artificial refuge.
                    p = ShellSurface(pow(a, 0.62), b * TAU) * 3.0;
                    part = 0.0;
                    accent = a;
                }
                return p;
            }

            float3 GiantColor(float part, float accent)
            {
                if (part < 0.5)
                    return lerp(float3(0.07, 0.36, 0.35), float3(0.2, 0.64, 0.45), accent);
                if (part < 1.5)
                    return float3(0.95, 0.69, 0.28);
                if (part < 2.5)
                    return float3(0.08, 0.58, 0.59);
                if (part < 3.5)
                    return float3(0.12, 0.64, 0.51);
                if (part < 4.5)
                    return float3(0.025, 0.29, 0.34);
                return float3(0.95, 0.72, 0.28);
            }

            float3 RefugeColor(float part, float accent, float id)
            {
                if (part < 0.5)
                    return lerp(float3(0.08, 0.54, 0.51), float3(0.88, 0.67, 0.33), Hash(id, 32.0) * 0.65);
                if (part < 1.5)
                    return lerp(float3(0.17, 0.47, 0.43), float3(0.82, 0.35, 0.48), accent * 0.75);
                return lerp(float3(0.025, 0.27, 0.31), float3(0.16, 0.51, 0.43), accent);
            }

            Varyings Vert(Input input)
            {
                Varyings output;
                float3 world;
                float3 color = input.color.rgb;
                float id = _CrabId * 3200.0 + input.data.x;
                float matterScale = 1.0;
                float3 giantColor = GiantColor(input.data.y, saturate(input.extra.x));
                if (input.data.y < 0.5 && input.extra.x >= 0.98)
                    giantColor = float3(0.88, 0.65, 0.31);
                if (_State < 0.5)
                {
                    world = TransformObjectToWorld(SmallPose(input, _Beat));
                }
                else if (_State < 1.5)
                {
                    float3 source = TransformObjectToWorld(SmallPose(input, _DeathBeat));
                    world = ScatteredPoint(source, id, input.data.z, max(0.0, _Song - _DeathSong));
                }
                else if (_State < 2.5)
                {
                    float3 source = TransformObjectToWorld(SmallPose(input, _DeathBeat));
                    float3 scattered = ScatteredPoint(source, id, input.data.z, max(0.0, _Song - _DeathSong));
                    float blend = smoothstep(0.0, 1.0, _MergeProgress);
                    world = lerp(scattered, GiantPoint(input, _Beat, id), blend);
                    matterScale = lerp(1.0, GIANT_SCALE, blend);
                    color = lerp(color, giantColor, blend);
                }
                else if (_State < 3.5)
                {
                    float3 source;
                    float sourceScale = 1.0;
                    float3 sourceColor = color;
                    if (_RefugeSourceState > 1.5)
                    {
                        source = GiantPoint(input, _ReefBeat, id);
                        sourceScale = GIANT_SCALE;
                        sourceColor = giantColor;
                    }
                    else
                    {
                        source = TransformObjectToWorld(SmallPose(input, _DeathBeat));
                        if (_RefugeSourceState > 0.5)
                            source = ScatteredPoint(source, id, input.data.z, _RefugeSourceElapsed);
                    }
                    float refugePart, refugeAccent;
                    float3 refuge = _RefugeRoot.xyz + RefugePoint(id, refugePart, refugeAccent);
                    float delay = Hash(_CrabId, 41.0) * 0.10;
                    float blend = smoothstep(0.16 + delay, 1.0, _RefugeProgress);
                    float disperse = smoothstep(0.0, 0.18, _RefugeProgress);
                    float3 drift = UnitSphere(Hash(id, 42.0), Hash(id, 43.0), 1.0);
                    float3 stream = float3(-drift.z, 0.6 + abs(drift.y), drift.x);
                    world = lerp(source + drift * (3.8 * disperse), refuge, blend)
                        + stream * (sin(blend * PI) * 8.0);
                    matterScale = lerp(sourceScale, 2.4, blend);
                    color = lerp(sourceColor, RefugeColor(refugePart, refugeAccent, id), blend);
                }
                else
                    world = TransformObjectToWorld(input.positionOS.xyz);

                float distanceToCamera = length(_WorldSpaceCameraPos - world);
                float rootScale = length(unity_ObjectToWorld._m00_m10_m20);
                // Small animals retain their size when scaled for readability;
                // merged matter is in world space and must not inherit that scale.
                if (_State < 1.5)
                    matterScale *= rootScale;
                else if (_State < 2.5)
                    matterScale = lerp(rootScale, GIANT_SCALE, smoothstep(0.0, 1.0, _MergeProgress));
                else if (_State < 3.5 && _RefugeSourceState < 1.5)
                    matterScale = lerp(rootScale, 2.4,
                        smoothstep(0.16 + Hash(_CrabId, 41.0) * 0.10, 1.0, _RefugeProgress));
                float size = input.uv.z * matterScale * max(1.0, distanceToCamera * 0.0028);
                float glintMask = step(0.992, Hash(id, 47.0));
                float4 centerClip = TransformWorldToHClip(world);
                float pixelsPerWorldUnit = abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5 /
                    max(abs(centerClip.w), 0.001);
                size = max(size, lerp(1.15, 1.9, glintMask) / max(pixelsPerWorldUnit, 0.001));
                world += (UNITY_MATRIX_V[0].xyz * input.uv.x + UNITY_MATRIX_V[1].xyz * input.uv.y) * size;
                float pulse = 0.96 + 0.04 * sin(_Beat * TAU + input.data.z * TAU);
                float distanceFade = exp(-distanceToCamera * 0.0014);
                color *= _Tint.rgb * _Gain * 2.4 * pulse * distanceFade;
                color *= 1.0 + glintMask * max(0.0, sin(_Song * 3.6 + input.data.z * 29.0)) * 0.5;

                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.color = float4(color, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 15.0) * 1.35;
                float halo = exp(-radius * 5.5) * 0.22;
                return half4(input.color.rgb * (core + halo), 1.0);
            }
            ENDHLSL
        }
    }
}
