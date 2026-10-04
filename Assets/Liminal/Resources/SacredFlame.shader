Shader "Liminal/Sacred Flame Matter"
{
    Properties
    {
        _Gain ("Radiance", Float) = 0.88
        _Formation ("Formation", Range(0,1)) = 0
        _Beat ("Authored Music Beat", Float) = 0
        _PixelFloor ("Pixel Radius Floor", Range(0,1)) = 0.44
        _ColorRate ("Palette Cycles Per Beat", Float) = 0.125
        _FlameWidth ("Flame Width", Float) = 6
        _FlameHeight ("Flame Height", Float) = 24.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+8" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/SharpMatter.hlsl"

            #define FLAME_TAU 6.28318530718

            CBUFFER_START(UnityPerMaterial)
                float _Gain;
                float _Formation;
                float _Beat;
                float _PixelFloor;
                float _ColorRate;
                float _FlameWidth;
                float _FlameHeight;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 color : COLOR;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 FlamePalette(float phase)
            {
                float3 azure = float3(0.025, 0.30, 0.96);
                float3 cyan = float3(0.015, 0.84, 0.98);
                float3 amethyst = float3(0.55, 0.075, 0.94);
                float3 gold = float3(0.98, 0.34, 0.025);
                float segment = frac(phase) * 4.0;
                float blend = smoothstep(0.0, 1.0, frac(segment));
                if (segment < 1.0) return lerp(azure, cyan, blend);
                if (segment < 2.0) return lerp(cyan, amethyst, blend);
                if (segment < 3.0) return lerp(amethyst, gold, blend);
                return lerp(gold, azure, blend);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float seed = input.data.x;
                float motion = saturate(input.data.y);
                float beatPhase = _Beat * FLAME_TAU;
                float lifetime = lerp(2.8, 6.1, motion);
                float birthOffset = frac(seed * 91.73 + motion * 17.19);
                float age = frac(birthOffset + _Beat / lifetime);
                float formation = saturate((_Formation - seed * 0.24) / 0.76);
                formation = smoothstep(0.0, 1.0, formation);

                float pulse = 0.5 + 0.5 * cos(beatPhase - seed * FLAME_TAU * 0.13);
                float strand = floor(seed * 5.0);
                float strandPhase = strand * 2.399963;
                float swirl = seed * FLAME_TAU + age * (8.2 + motion * 4.4) + beatPhase * 0.025;
                float curl = sin(age * FLAME_TAU * 2.1 + seed * FLAME_TAU * 3.0 + beatPhase * 0.11);
                float radius = _FlameWidth * pow(1.0 - age, 0.85) + sin(age * 3.14159265) * 1.1;
                radius *= sqrt(motion) * (0.84 + pulse * 0.16);
                float2 bend = float2(sin(_Beat * 0.65 + age * 4.2),
                    cos(_Beat * 0.49 + age * 3.7)) * pow(age, 1.7) * 3.2;
                float fork = sin(age * 3.14159265) * age * 3.6;
                bend += float2(cos(strandPhase), sin(strandPhase)) * fork;
                float tongueHeight = _FlameHeight + 3.2 * sin(_Beat * 0.53 + strandPhase);
                float3 flowLocal = input.positionOS + float3(
                    cos(swirl + curl * 0.32) * radius + bend.x + sin(age * FLAME_TAU + seed * 8.0) * age * 0.55,
                    age * tongueHeight - 7.0,
                    sin(swirl + curl * 0.32) * radius + bend.y + cos(age * FLAME_TAU * 0.8 + seed * 6.0) * age * 0.42);
                float3 gather = input.positionOS + float3(
                    (frac(seed * 17.173) - 0.5) * 0.72,
                    0.35 + motion * 1.25,
                    (frac(seed * 91.713) - 0.5) * 0.72);
                float3 world = TransformObjectToWorld(lerp(gather, flowLocal, formation));

                float2 quad = input.uv.xy;
                float depth = max(0.01, -TransformWorldToView(world).z);
                float pixelWorld = 2.0 * depth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                float variation = frac(seed * 73.197 + input.uv.z * 29.31);
                float nominalSize = input.uv.z * lerp(0.82, 1.18, variation);
                float size;
                if (MatterIsLegacy())
                    size = max(nominalSize, min(pixelWorld * _PixelFloor, 0.42));
                else
                    size = MatterGrainRadius(nominalSize, MatterPixelWorld(world), seed, 1.25);

                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = world + (cameraRight * quad.x + cameraUp * quad.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = quad;

                float alive = smoothstep(0.0, 0.065, age) * (1.0 - smoothstep(0.88, 1.0, age));
                float palettePhase = _Beat * _ColorRate + seed * 0.035 + age * 0.025 + motion * 0.012;
                float3 palette = FlamePalette(palettePhase);
                float radiance = 0.58 + pulse * 0.30 + sin(seed * FLAME_TAU + beatPhase) * 0.055;
                float sparkleWave = pow(saturate(0.5 + 0.5 * sin(
                    beatPhase * (2.0 + motion * 4.0) + seed * FLAME_TAU)), 12.0);
                float sparkle = step(0.78, frac(seed * 71.31 + motion * 13.7)) * sparkleWave;
                float3 sparkleColor = FlamePalette(palettePhase + 0.035);
                output.color = (palette * radiance + sparkleColor * sparkle * 0.38) * _Gain;
                output.alpha = input.color.a * alive * formation;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.alpha - 0.015);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = MatterIsLegacy() ?
                    exp(-radius * 6.4) * 0.88 + exp(-radius * 2.6) * 0.08 :
                    MatterSharpCore(input.uv);
                return half4(input.color * core * input.alpha, 1.0);
            }
            ENDHLSL
        }
    }
}
