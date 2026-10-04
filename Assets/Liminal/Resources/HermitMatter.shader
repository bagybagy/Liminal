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
        _ParticlesPerCrab ("Matter Samples Per Crab", Float) = 4800
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
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/MatterFlow.hlsl"
            #include "../Shaders/SharpMatter.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Gain, _Song, _Beat, _State;
            float _DeathSong, _DeathBeat, _GaitOffset, _CrabId, _ParticlesPerCrab;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            static const float TAU = 6.28318530718;
            static const float GIANT_SCALE = 11.25;
            static const float REEF_SCALE = 2.5;
            static const float REEF_FISH_COUNT = 16.0;

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
                float4 death = MatterDeathEnvelope(elapsed, false);
                float a = Hash(id, 14.0) * TAU;
                float y = Hash(id, 15.0) * 2.0 - 1.0;
                float ring = sqrt(max(0.0, 1.0 - y * y));
                float3 drift = float3(cos(a) * ring, y * 0.8, sin(a) * ring);
                float distance = min(elapsed * (1.6 + seed * 1.5), 22.0) * (0.45 + Hash(id, 16.0))*death.y;
                float sway = (sin(elapsed * 0.37 + seed * TAU) - sin(seed * TAU)) * 0.65;
                float sway2 = (cos(elapsed * 0.29 + seed * 19.0) - cos(seed * 19.0)) * 0.48;
                return source + drift * distance + float3(sway, sway2, -sway * 0.55)*death.y+
                    MatterFlowDelta(source*.2,elapsed,seed)*death.y*3.0*_MatterDeathStyle.x;
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
                else if (selector < 0.66)
                {
                    // One dominant shell whorl sweeps from a broad foot into an open arch.
                    float t = b;
                    float angle = -PI * 0.72 + t * TAU * 1.42;
                    float radius = 7.0 + 28.0 * t;
                    float angleRate = TAU * 1.42;
                    float height = 4.0 + sin(t * PI) * 28.0 + t * 3.0;
                    float heightRate = 28.0 * PI * cos(t * PI) + 3.0;
                    float3 tangent = normalize(float3(
                        28.0 * cos(angle) - radius * angleRate * sin(angle),
                        heightRate,
                        28.0 * sin(angle) + radius * angleRate * cos(angle)));
                    float3 side = normalize(float3(-tangent.z, 0.0, tangent.x));
                    float3 normal = normalize(cross(tangent, side));
                    float tube = (2.7 + (1.0 - t) * 1.4) * sqrt(d);
                    float crossAngle = c * TAU;
                    float3 center = float3(cos(angle) * radius, height, sin(angle) * radius);
                    p = center + (side * cos(crossAngle) + normal * sin(crossAngle)) * tube;
                    part = 0.0;
                    accent = t;
                }
                else if (selector < 0.82)
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
                    part = 3.0;
                    accent = a;
                }
                return p;
            }

            float3 ShoalPoint(float3 local, float fishId, float song)
            {
                float seed = Hash(fishId, 51.0);
                float phase = fishId * (TAU / REEF_FISH_COUNT) + seed * TAU +
                    song * (0.12 + Hash(fishId, 52.0) * 0.08);
                float radius = 47.0 + Hash(fishId, 53.0) * 22.0;
                float height = 13.0 + Hash(fishId, 54.0) * 18.0 +
                    sin(song * 0.21 + fishId * 1.71) * 2.2;
                float fishScale = 1.0 + Hash(fishId, 55.0) * 0.55;
                float3 radial = float3(cos(phase), 0.0, sin(phase));
                float3 tangent = float3(-sin(phase), 0.0, cos(phase));
                return radial * (radius + local.x * fishScale) +
                    float3(0.0, height + local.y * fishScale, 0.0) + tangent * (local.z * fishScale);
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
                    return lerp(float3(0.08, 0.66, 0.61), float3(0.96, 0.72, 0.34),
                        saturate(accent * 0.55 + Hash(id, 32.0) * 0.35));
                if (part < 1.5)
                    return lerp(float3(0.18, 0.54, 0.5), float3(0.96, 0.39, 0.43), accent * 0.78);
                if (part < 2.5)
                    return lerp(float3(0.035, 0.34, 0.37), float3(0.18, 0.58, 0.49), accent);
                return lerp(float3(0.86, 0.62, 0.31), float3(1.0, 0.85, 0.54), Hash(id, 32.0) * 0.7);
            }

            Varyings Vert(Input input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world;
                float3 color = input.color.rgb;
                float id = _CrabId * _ParticlesPerCrab + input.data.x;
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
                    world += MatterFlowDelta(source*.2,_MergeProgress*4.0,input.data.z)*sin(blend*PI)*3.0;
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
                    float3 refuge = _RefugeRoot.xyz + RefugePoint(id, refugePart, refugeAccent) * REEF_SCALE;
                    float delay = Hash(_CrabId, 41.0) * 0.10;
                    float blend = smoothstep(0.16 + delay, 1.0, _RefugeProgress);
                    float4 death = MatterDeathEnvelope(_RefugeProgress*7.0, true);
                    float disperse = death.y;
                    float3 drift = UnitSphere(Hash(id, 42.0), Hash(id, 43.0), 1.0);
                    float3 stream = float3(-drift.z, 0.6 + abs(drift.y), drift.x);
                    world = lerp(source + drift * (3.8 * disperse), refuge, blend)
                        + stream * (sin(blend * PI) * 8.0);
                    world += MatterFlowDelta(source*.15,_RefugeProgress*7.0,input.data.z)*
                        max(sin(blend*PI),death.y)*(1.0-blend)*7.0*_MatterDeathStyle.x;
                    matterScale = lerp(sourceScale, 2.4, blend);
                    color = lerp(sourceColor, RefugeColor(refugePart, refugeAccent, id), blend);
                }
                else if (_State < 4.5)
                {
                    id = input.data.y * 32.0 + input.data.x;
                    world = TransformObjectToWorld(ShoalPoint(input.positionOS.xyz, input.data.y, _Song));
                    color = input.color.rgb * (0.92 + 0.08 * sin(_Song * 2.1 + input.data.z * TAU));
                }
                else
                    world = TransformObjectToWorld(input.positionOS.xyz);

                float grainAccent = saturate(max(max(color.r, color.g), color.b));
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
                float giantWeight = _State > 1.5 && _State < 2.5 ? smoothstep(0, 1, _MergeProgress) :
                    _State > 2.5 && _State < 3.5 && _RefugeSourceState > 1.5 ?
                    1 - smoothstep(0.16 + Hash(_CrabId, 41.0) * 0.10, 1, _RefugeProgress) : 0;
                // Enlarge the animal with more samples, not overlapping metre-wide billboards.
                size = lerp(size, min(size, .32 + glintMask * .08), giantWeight);
                float pixelWorld = MatterPixelWorld(world);
                float minimumPixels = lerp(lerp(1.15, 1.9, glintMask), lerp(.72, 1.15, glintMask), giantWeight);
                if (MatterIsLegacy()) {
                    float4 centerClip = TransformWorldToHClip(world);
                    float pixelsPerWorldUnit = abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5 /
                        max(abs(centerClip.w), 0.001);
                    size = max(size, minimumPixels / max(pixelsPerWorldUnit, 0.001));
                } else size = MatterGrainRadius(max(size, pixelWorld * minimumPixels), pixelWorld, id, 1.3);
                world += (UNITY_MATRIX_V[0].xyz * input.uv.x + UNITY_MATRIX_V[1].xyz * input.uv.y) * size;
                float pulse = 0.96 + 0.04 * sin(_Beat * TAU + input.data.z * TAU);
                float distanceFade = exp(-distanceToCamera * 0.0014);
                color *= _Tint.rgb * _Gain * 2.4 * pulse * distanceFade;
                float sampleGain = _State < 3.5 ? 3200.0 / max(1, _ParticlesPerCrab) : 1;
                color *= sampleGain * lerp(1, .7, giantWeight);
                float transitionLight=_State>.5 && _State<1.5?MatterDeathEnvelope(max(0,_Song-_DeathSong),false).z:
                    _State<2.5 && _State>1.5?sin(saturate(_MergeProgress)*PI):
                    _State<3.5 && _State>2.5?MatterDeathEnvelope(_RefugeProgress*7.0,true).z:0;
                color *= 1.0+transitionLight*_MatterDeathStyle.y;
                color *= 1.0 + glintMask * max(0.0, sin(_Song * 3.6 + input.data.z * 29.0)) * 0.5;
                if (!MatterIsLegacy())
                    color *= 1.0 + (MatterGrainLight(id, _Song, grainAccent) - 1.0) * 0.14;

                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.color = float4(color, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                if (MatterIsLegacy()) {
                    float core = exp(-radius * 15.0) * 1.35;
                    float halo = exp(-radius * 5.5) * 0.22;
                    return half4(input.color.rgb * (core + halo), 1.0);
                }
                return half4(input.color.rgb * MatterSharpCore(input.uv) * 0.725, 1.0);
            }
            ENDHLSL
        }
    }
}
