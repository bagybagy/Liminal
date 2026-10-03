Shader "Liminal/Atlantis Matter"
{
    Properties
    {
        _Gain ("Radiance", Float) = 1
        _Formation ("Formation", Range(0,1)) = 0
        _LayerKind ("Layer Kind", Float) = 0
        _Song ("Song Time", Float) = 0
        _Beat ("Authored Music Beat", Float) = 0
        _PixelFloor ("Pixel Radius Floor", Range(0,1)) = 0.54
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+7" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define ATLANTIS_TAU 6.28318530718

            CBUFFER_START(UnityPerMaterial)
                float _Gain;
                float _Formation;
                float _LayerKind;
                float _Song;
                float _Beat;
                float _PixelFloor;
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

            float3 RotateY(float3 position, float angle)
            {
                float sine = sin(angle);
                float cosine = cos(angle);
                return float3(cosine * position.x + sine * position.z, position.y,
                    -sine * position.x + cosine * position.z);
            }

            float SmoothStep01(float edge0, float edge1, float value)
            {
                return smoothstep(edge0, edge1, value);
            }

            float AtlantisGroundHeight(float x, float z)
            {
                float nx = x / 700.0;
                float nz = z / 720.0;
                float floorOffset = 300.0 * (1.0 - sqrt(max(0.0, 1.0 - nx * nx - nz * nz)));
                float radius = sqrt(x * x + z * z);
                float shelves = SmoothStep01(82.0, 92.0, radius) * 1.7 +
                    SmoothStep01(142.0, 158.0, radius) * 2.5 +
                    SmoothStep01(204.0, 222.0, radius) * 3.2;
                float rolling = 0.8 * sin(x * 0.027 + sin(z * 0.019) * 1.7) +
                    0.6 * cos(z * 0.036 - x * 0.014) +
                    0.4 * sin((x + z) * 0.07);
                return floorOffset + 18.5 + shelves + rolling;
            }

            float SchoolSpeed(float school)
            {
                return 0.038 + fmod(school, 3.0) * 0.004;
            }

            float3 SchoolCenter(float school, float phase)
            {
                float radialLane = (school - 3.0) * 2.7;
                float radius = 164.0 + 19.0 * sin(phase * 3.0 + school * 0.47) +
                    7.0 * sin(phase * 5.0 - school * 0.31) + radialLane;
                float angle = phase + 0.08 * sin(phase * 2.0 + school * 0.61) +
                    0.03 * sin(phase * 4.0 - school * 0.23);
                float x = radius * cos(angle);
                float z = radius * sin(angle);
                float y = AtlantisGroundHeight(x, z) + 14.5 +
                    sin(phase * 0.67 + school * 0.37) * 0.8;
                return float3(x, y, z);
            }

            float SchoolHeading(float school, float phase)
            {
                float speed = SchoolSpeed(school);
                float radialLane = (school - 3.0) * 2.7;
                float radius = 164.0 + 19.0 * sin(phase * 3.0 + school * 0.47) +
                    7.0 * sin(phase * 5.0 - school * 0.31) + radialLane;
                float angle = phase + 0.08 * sin(phase * 2.0 + school * 0.61) +
                    0.03 * sin(phase * 4.0 - school * 0.23);
                float radialVelocity = 57.0 * cos(phase * 3.0 + school * 0.47) +
                    35.0 * cos(phase * 5.0 - school * 0.31);
                float angularVelocity = 1.0 + 0.16 * cos(phase * 2.0 + school * 0.61) +
                    0.12 * cos(phase * 4.0 - school * 0.23);
                float velocityX = (radialVelocity * cos(angle) - radius * sin(angle) * angularVelocity) * speed;
                float velocityZ = (radialVelocity * sin(angle) + radius * cos(angle) * angularVelocity) * speed;
                return atan2(velocityX, velocityZ);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float seed = input.data.x;
                float3 anchor = TransformObjectToWorld(input.positionOS);
                float3 localAnchor = TransformWorldToObject(anchor);
                float3 objectOrigin = TransformObjectToWorld(float3(0, 0, 0));
                float formation = saturate((_Formation - seed * 0.28) / 0.72);
                formation = smoothstep(0.0, 1.0, formation);
                float3 gather = objectOrigin + float3(
                    (frac(seed * 17.173) - 0.5) * 46.0,
                    -44.0 + frac(seed * 3.713) * 21.0,
                    (frac(seed * 91.713) - 0.5) * 32.0);
                float3 world;

                if (_LayerKind > 0.5 && _LayerKind < 1.5)
                {
                    float school = floor(input.data.y + 0.5);
                    float phaseOffset = school * 2.3999632;
                    float phase = phaseOffset + _Song * SchoolSpeed(school);
                    float3 startCenter = SchoolCenter(school, phaseOffset);
                    float3 currentCenter = SchoolCenter(school, phase);
                    float heading = SchoolHeading(school, phase);
                    float startHeading = SchoolHeading(school, phaseOffset);
                    float originalTerrainOffset = localAnchor.y -
                        AtlantisGroundHeight(localAnchor.x, localAnchor.z);
                    float currentCenterHeight = currentCenter.y -
                        AtlantisGroundHeight(currentCenter.x, currentCenter.z);
                    float startCenterHeight = startCenter.y -
                        AtlantisGroundHeight(startCenter.x, startCenter.z);
                    float3 movedLocal = currentCenter +
                        RotateY(localAnchor - startCenter, heading - startHeading);
                    movedLocal.y = AtlantisGroundHeight(movedLocal.x, movedLocal.z) +
                        originalTerrainOffset + currentCenterHeight - startCenterHeight;
                    float current = _Song * 0.41 + seed * ATLANTIS_TAU;
                    movedLocal += float3(sin(current) * 0.14,
                        sin(current * 0.73) * 0.10, cos(current * 0.89) * 0.14);
                    anchor = TransformObjectToWorld(movedLocal);
                    world = lerp(gather, anchor, formation);
                }
                else
                {
                    world = lerp(gather, anchor, formation);
                    if (_LayerKind > 1.5 && _LayerKind < 2.5)
                    {
                        float craft = floor(input.data.y + 0.5);
                        float phase = _Song * 0.075 + craft * (ATLANTIS_TAU * 0.25);
                        float drift = craft < 0.5 ? 1.5 : 4.1;
                        world += float3(sin(phase) * drift,
                            sin(phase * 0.67) * (craft < 0.5 ? 0.32 : 0.72),
                            cos(phase) * drift * 0.72);
                    }
                }

                float2 quad = input.uv.xy;
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float depth = max(0.01, -TransformWorldToView(world).z);
                float pixelWorld = 2.0 * depth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                float variation = frac(seed * 73.197 + input.uv.z * 29.31);
                float role = (_LayerKind < 0.5 || _LayerKind > 2.5) ?
                    floor(input.data.y + 0.5) : -1.0;
                float roleContour = step(0.5, role) * (1.0 - step(1.5, role));
                float roleGlint = step(1.5, role);
                float sizeTier = lerp(0.78, 1.22, variation) + roleContour * 0.07 + roleGlint * 0.12;
                float nominalSize = input.uv.z * sizeTier;
                float size = max(nominalSize, min(pixelWorld * _PixelFloor, 0.42));
                float3 positionWS = world +
                    (cameraRight * quad.x + cameraUp * quad.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = quad;

                float traveling = 0.5 + 0.5 * sin(_Song * 0.32 - anchor.x * 0.019 + anchor.z * 0.014);
                float diffuse = 0.52 + variation * 0.17;
                float contour = roleContour * (0.73 + traveling * 0.21);
                float pulse = saturate(0.48 + 0.52 * sin(_Song * (0.58 + variation * 0.32) + seed * 19.7));
                float seededGlint = step(0.9975, frac(seed * 127.13 + input.uv.z * 71.7)) * pulse * 0.36;
                float authoredGlint = roleGlint * (0.45 + pulse * 0.35);
                float schoolRadiance = (_LayerKind > 0.5 && _LayerKind < 1.5) ? 0.78 : 1.0;
                float radiance = max(diffuse, contour) + max(seededGlint, authoredGlint);
                radiance *= schoolRadiance * (0.72 + formation * 0.28) * (.96 + .04 * cos(_Beat * 1.5707963));
                float coverage = saturate(input.uv.z * input.uv.z / max(size * size, 0.00001));
                output.color = input.color.rgb * _Gain * radiance * lerp(coverage, 1.0, 0.38);
                float3 glintColor = lerp(float3(0.35, 0.67, 0.80), float3(0.78, 0.60, 0.34),
                    frac(seed * 17.17));
                output.color = lerp(output.color, glintColor * _Gain * 0.72,
                    saturate(max(seededGlint, authoredGlint) * 0.36));
                output.alpha = input.color.a * formation;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.alpha - 0.015);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 6.4) * 0.88;
                float halo = exp(-radius * 2.6) * 0.08;
                return half4(input.color * (core + halo), 1.0);
            }
            ENDHLSL
        }
    }
}
