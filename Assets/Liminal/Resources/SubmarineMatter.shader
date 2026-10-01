Shader "Liminal/Submarine Matter"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 1
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
            float4 _Tint;
            float _Gain;
            float4x4 _SubmarineToWorld;
            float4x4 _FleetToWorld;
            float4x4 _GiantToWorld;
            float4x4 _ReefToWorld;
            float _FormFrom;
            float _FormTo;
            float _Morph;
            float _BeatPosition;
            float _Song;
            float _Reduced;
            CBUFFER_END

            struct Input
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 data : TEXCOORD1;
                float3 fleetPoint : TEXCOORD2;
                float3 giantPoint : TEXCOORD3;
                float3 reefPoint : TEXCOORD4;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float3 RotateZ(float3 p, float angle)
            {
                float s = sin(angle), c = cos(angle);
                return float3(c * p.x - s * p.y, s * p.x + c * p.y, p.z);
            }

            float3 GiantPose(float3 position, float joint)
            {
                if (joint < 0.5) return position;
                bool left = joint < 2.5;
                float side = left ? -1.0 : 1.0;
                float phase = _BeatPosition * 0.47 + (left ? 0.0 : 3.14159265);
                float shoulderAngle = 0.15 + sin(phase) * 0.19;
                float3 shoulder = float3(side * 22.0, -183.0, 0.0);
                float3 posed = shoulder + RotateZ(position - shoulder, shoulderAngle);
                if ((joint > 1.5 && joint < 2.5) || (joint > 3.5 && joint < 4.5))
                {
                    float3 elbow = float3(side * 35.0, -209.0, 0.0);
                    float3 posedElbow = shoulder + RotateZ(elbow - shoulder, shoulderAngle);
                    float bend = 0.18 + sin(phase + 1.1) * 0.12;
                    posed = posedElbow + RotateZ(posed - posedElbow, side * bend);
                }
                return posed;
            }

            float3 FormPosition(float form, Input input)
            {
                if (form < 0.5)
                    return mul(_SubmarineToWorld, float4(input.positionOS, 1.0)).xyz;
                if (form < 1.5)
                    return mul(_FleetToWorld, float4(input.fleetPoint, 1.0)).xyz;
                if (form < 2.5)
                {
                    float3 giant = GiantPose(input.giantPoint, input.data.z);
                    if (_FormFrom < 1.5 && _FormTo > 1.5)
                        giant.y -= 44.0 * (1.0 - smoothstep(0.0, 1.0, _Morph));
                    return mul(_GiantToWorld, float4(giant, 1.0)).xyz;
                }
                return mul(_ReefToWorld, float4(input.reefPoint, 1.0)).xyz;
            }

            float Hash(float seed, float salt)
            {
                return frac(sin(seed * 127.1 + salt * 311.7) * 43758.5453);
            }

            float3 Palette(float form, float accent)
            {
                float steelMix = smoothstep(0.80, 0.95, accent);
                float goldMix = smoothstep(0.96, 0.999, accent);
                float redLevel = lerp(0.48, 1.05, smoothstep(0.02, 0.84, accent));
                float3 red = float3(redLevel, 0.018, 0.022);
                float3 cyanSteel = float3(0.035, 0.58, 0.74);
                float3 gold = float3(1.18, 0.43, 0.075);

                if (form < 0.5)
                    return lerp(lerp(red, cyanSteel, steelMix * 0.72), gold, goldMix * 0.9);
                if (form < 1.5)
                    return lerp(lerp(cyanSteel, red, steelMix * 0.72), gold, goldMix * 0.88);
                if (form < 2.5)
                    return lerp(lerp(red * 0.92, cyanSteel, steelMix * 0.68), gold, goldMix * 0.92);
                return lerp(cyanSteel * 0.88, gold, smoothstep(0.86, 0.995, accent) * 0.82);
            }

            Varyings Vert(Input input)
            {
                Varyings output;
                float3 from = FormPosition(_FormFrom, input);
                float3 to = FormPosition(_FormTo, input);
                float morph = smoothstep(0.0, 1.0, _Morph);
                float3 world = lerp(from, to, morph);
                float flow = sin(_Song * 1.6 + input.data.x * 31.0) * sin(morph * 3.14159265);
                world += float3(flow * 0.52, flow * 0.31, -flow * 0.43);

                float distanceToCamera = length(_WorldSpaceCameraPos - world);
                float viewDepth = abs(mul(UNITY_MATRIX_V, float4(world, 1.0)).z);
                float projectionScale = max(0.001, abs(UNITY_MATRIX_P._m11));
                float worldPerPixel = 2.0 * max(0.01, viewDepth) /
                    (max(1.0, _ScreenParams.y) * projectionScale);
                float glint = smoothstep(0.90, 0.99, input.data.w);
                float minimumRadius = lerp(1.4, 2.3, glint) * worldPerPixel;
                float size = max(input.uv.z, minimumRadius);
                float pulse = 0.92 + 0.08 * (1.0 - _Reduced) * (0.5 + 0.5 * sin(_BeatPosition * 6.2831853));
                float3 fromColor = Palette(_FormFrom, input.data.y);
                float3 toColor = Palette(_FormTo, input.data.y);
                float3 palette = lerp(fromColor, toColor, morph);
                float flare = 1.0 + (0.12 + input.data.w * 0.22) * (0.5 + 0.5 * sin(_BeatPosition * 6.2831853 + input.data.x * 19.0));
                float fade = exp(-distanceToCamera * 0.00165);

                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                world += (cameraRight * input.uv.x + cameraUp * input.uv.y) * size;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.color = float4(palette * input.color.rgb * _Tint.rgb * _Gain * pulse * flare * fade, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float glow = exp(-radius * 5.0) * 0.32 + exp(-radius * 24.0) * 1.58;
                return half4(input.color.rgb * glow, 1.0);
            }
            ENDHLSL
        }
    }
}
