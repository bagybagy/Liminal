Shader "Liminal/Atlantis Matter"
{
    Properties
    {
        _Gain ("Radiance", Float) = 3
        _Formation ("Formation", Range(0,1)) = 0
        _LayerKind ("Layer Kind", Float) = 0
        _Song ("Song Time", Float) = 0
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

            CBUFFER_START(UnityPerMaterial)
                float _Gain;
                float _Formation;
                float _LayerKind;
                float _Song;
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

            float SchoolSpeed(float school)
            {
                return 0.115 + fmod(school, 3.0) * 0.017;
            }

            float3 SchoolCenter(float school, float phase)
            {
                float laneAngle = school * 0.8975979;
                float radius = 218.0 + fmod(school, 3.0) * 24.0;
                float orbitX = 22.0 + fmod(school, 2.0) * 8.0;
                float orbitZ = 18.0 + fmod(school + 1.0, 3.0) * 5.0;
                float altitude = 82.0 + fmod(school, 4.0) * 14.0;
                return float3(cos(laneAngle) * radius + cos(phase) * orbitX,
                    altitude + sin(phase * 0.67 + school * 0.37) * 3.0,
                    sin(laneAngle) * radius + sin(phase) * orbitZ);
            }

            float SchoolHeading(float school, float phase)
            {
                float speed = SchoolSpeed(school);
                float orbitX = 22.0 + fmod(school, 2.0) * 8.0;
                float orbitZ = 18.0 + fmod(school + 1.0, 3.0) * 5.0;
                return atan2(-sin(phase) * orbitX * speed,
                    cos(phase) * orbitZ * speed);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float seed = input.data.x;
                float3 anchor = TransformObjectToWorld(input.positionOS);
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float formation = saturate((_Formation - seed * 0.28) / 0.72);
                formation = smoothstep(0.0, 1.0, formation);
                float3 gather = center + float3(
                    (frac(seed * 17.173) - 0.5) * 46.0,
                    -44.0 + frac(seed * 3.713) * 21.0,
                    (frac(seed * 91.713) - 0.5) * 32.0);
                float3 world;

                if (_LayerKind > 0.5 && _LayerKind < 1.5)
                {
                    float school = floor(input.data.y + 0.5);
                    float phaseOffset = school * 2.3999632;
                    float phase = phaseOffset + _Song * SchoolSpeed(school);
                    float3 localAnchor = TransformWorldToObject(anchor);
                    float3 startCenter = SchoolCenter(school, phaseOffset);
                    float3 currentCenter = SchoolCenter(school, phase);
                    float heading = SchoolHeading(school, phase);
                    float startHeading = SchoolHeading(school, phaseOffset);
                    anchor = TransformObjectToWorld(currentCenter +
                        RotateY(localAnchor - startCenter, heading - startHeading));
                    world = lerp(gather, anchor, formation);
                }
                else
                {
                    world = lerp(gather, anchor, formation);
                    if (_LayerKind > 1.5)
                    {
                        float craft = floor(input.data.y + 0.5);
                        float phase = _Song * 0.19 + craft * 2.0943951;
                        world += float3(sin(phase) * 2.4, sin(phase * 0.63) * 1.2,
                            cos(phase) * 2.4);
                    }
                    else
                    {
                        world.y += sin(_Song * 0.27 + seed * 6.2831853) * 0.045 * formation;
                    }
                }

                float2 quad = input.uv.xy;
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float depth = max(0.01, -TransformWorldToView(world).z);
                float pixelWorld = 2.0 * depth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                float minimumRadius=_LayerKind<.5?.6:1.0;
                float size = max(input.uv.z, pixelWorld * minimumRadius);
                float3 positionWS = world + (cameraRight * quad.x + cameraUp * quad.y) * size;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = quad;
                float schoolRadiance = _LayerKind > 0.5 && _LayerKind < 1.5 ? 0.48 : 1.0;
                float coverage=min(1.0,input.uv.z*input.uv.z/max(size*size,.00001));
                output.color = input.color.rgb * _Gain * schoolRadiance * (0.58 + formation * 0.42)*
                    lerp(coverage,1.0,_LayerKind>.5?1.0:.25);
                output.alpha = input.color.a * formation;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.alpha - 0.015);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 8.0) * 1.5;
                float halo = exp(-radius * 3.2) * 0.18;
                return half4(input.color * (core + halo), 1.0);
            }
            ENDHLSL
        }
    }
}
