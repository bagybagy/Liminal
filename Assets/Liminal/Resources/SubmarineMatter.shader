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
            float _SpearCharge;
            CBUFFER_END

            struct Input
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 data : TEXCOORD1;
                float4 fleetPoint : TEXCOORD2;
                float3 giantPoint : TEXCOORD3;
                float4 reefPoint : TEXCOORD4;
                float4 giantFlowCenter : TEXCOORD5;
                float4 giantFlowTangent : TEXCOORD6;
                float4 hullNormals : TEXCOORD7;
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
                float shoulderAngle = left ? 0.06 + sin(phase) * 0.16 : -0.04 + sin(phase) * 0.07;
                float3 shoulder = float3(side * 26.0, -178.0, 0.0);
                float3 posed = shoulder + RotateZ(position - shoulder, shoulderAngle);
                if ((joint > 1.5 && joint < 2.5) || (joint > 3.5 && joint < 4.5))
                {
                    float3 elbow = float3(side * 43.0, -204.0, 0.0);
                    float3 posedElbow = shoulder + RotateZ(elbow - shoulder, shoulderAngle);
                    float bend = left ? 0.12 + sin(phase + 1.1) * 0.12 : 0.06 + sin(phase + 1.1) * 0.06;
                    posed = posedElbow + RotateZ(posed - posedElbow, side * bend);
                }
                return posed;
            }

            float3 FormPosition(float form, Input input)
            {
                if (form < 0.5)
                    return mul(_SubmarineToWorld, float4(input.positionOS, 1.0)).xyz;
                if (form < 1.5)
                    return mul(_FleetToWorld, float4(input.fleetPoint.xyz, 1.0)).xyz;
                if (form < 2.5)
                {
                    float flowAngle = _Song * input.giantFlowCenter.w * lerp(1.0, 0.45, _Reduced);
                    float3 flowing = input.giantFlowCenter.xyz +
                        (input.giantPoint - input.giantFlowCenter.xyz) * cos(flowAngle) +
                        input.giantFlowTangent.xyz * sin(flowAngle);
                    float3 giant = GiantPose(flowing, input.data.z);
                    if (_FormFrom < 1.5 && _FormTo > 1.5)
                        giant.y -= 44.0 * (1.0 - smoothstep(0.0, 1.0, _Morph));
                    return mul(_GiantToWorld, float4(giant, 1.0)).xyz;
                }
                return mul(_ReefToWorld, float4(input.reefPoint.xyz, 1.0)).xyz;
            }

            float FormAccent(float form, Input input)
            {
                if (form < 0.5) return input.data.y;
                if (form < 1.5) return input.fleetPoint.w;
                if (form < 2.5) return input.giantFlowTangent.w;
                return input.reefPoint.w;
            }

            float3 DecodeNormal(float2 oct)
            {
                float3 normal = float3(oct, 1.0 - abs(oct.x) - abs(oct.y));
                float fold = saturate(-normal.z);
                normal.xy += float2(normal.x >= 0.0 ? -fold : fold, normal.y >= 0.0 ? -fold : fold);
                return normalize(normal);
            }

            float HullLight(float form, Input input, float3 world)
            {
                if (form > 1.5) return 1.0;
                float3 normal = DecodeNormal(form < 0.5 ? input.hullNormals.xy : input.hullNormals.zw);
                if (form < 0.5) normal = mul((float3x3)_SubmarineToWorld, normal);
                else normal = mul((float3x3)_FleetToWorld, normal);
                normal = normalize(normal);
                float3 view = normalize(_WorldSpaceCameraPos - world + float3(0.0, 0.0001, 0.0));
                float facing = dot(normal, view);
                float diffuse = 0.4 + 0.6 * saturate(dot(normal, normalize(float3(-0.45, 0.7, 0.55))));
                float rim = 0.18 * pow(saturate(1.0 - abs(facing)), 3.0);
                float light = (diffuse + rim) * (0.22 + 0.78 * saturate(facing));
                float accent = FormAccent(form, input);
                float emission = form < 0.5 ? smoothstep(0.94, 0.96, accent) : smoothstep(0.57, 0.65, accent);
                return lerp(light, 1.0, emission);
            }

            float3 Palette(float form, float accent)
            {
                float3 cyanSteel = float3(0.035, 0.58, 0.74);
                float3 gold = float3(1.18, 0.43, 0.075);

                if (form < 0.5)
                {
                    float3 steel = lerp(float3(0.24, 0.025, 0.04), float3(0.50, 0.075, 0.065),
                        smoothstep(0.14, 0.54, accent));
                    float3 brass = float3(0.95, 0.59, 0.19);
                    float3 plate = lerp(steel, brass, smoothstep(0.61, 0.69, accent));
                    plate = lerp(plate, float3(0.68, 0.77, 0.68), smoothstep(0.84, 0.9, accent));
                    return lerp(plate, float3(1.28, 0.022, 0.04), smoothstep(0.94, 0.97, accent));
                }
                if (form < 1.5)
                {
                    float3 steel = lerp(float3(0.18, 0.3, 0.43), float3(0.55, 0.67, 0.74),
                        smoothstep(0.12, 0.53, accent));
                    float3 plate = lerp(steel, float3(0.06, 0.93, 1.12), smoothstep(0.58, 0.72, accent));
                    plate = lerp(plate, float3(1.15, 0.07, 0.2), smoothstep(0.8, 0.86, accent));
                    return lerp(plate, float3(0.72, 0.66, 1.18), smoothstep(0.94, 0.98, accent));
                }
                if (form < 2.5)
                {
                    float3 enamel = float3(0.36, 0.64, 0.59);
                    float3 bronze = float3(0.69, 0.30, 0.10);
                    float3 porcelain = float3(0.83, 0.88, 0.77);
                    float3 plate = lerp(bronze, enamel, smoothstep(0.24, 0.72, accent));
                    plate = lerp(plate, porcelain, smoothstep(0.75, 0.86, accent));
                    plate = lerp(plate, float3(0.15, 0.87, 1.18), smoothstep(0.87, 0.92, accent));
                    return lerp(plate, gold, smoothstep(0.94, 0.985, accent));
                }
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
                float fromAccent = FormAccent(_FormFrom, input);
                float toAccent = FormAccent(_FormTo, input);
                float3 fromColor = Palette(_FormFrom, fromAccent);
                float3 toColor = Palette(_FormTo, toAccent);
                float3 palette = lerp(fromColor, toColor, morph);
                float hullLight = lerp(HullLight(_FormFrom, input, from), HullLight(_FormTo, input, to), morph);
                float giantWeight = lerp(_FormFrom > 1.5 && _FormFrom < 2.5 ? 1.0 : 0.0,
                    _FormTo > 1.5 && _FormTo < 2.5 ? 1.0 : 0.0, morph);
                float tip = input.data.z > 3.5 ? smoothstep(-128.0, -100.0, input.giantPoint.y) : 0.0;
                palette += float3(0.23, 0.77, 1.2) * tip * _SpearCharge * giantWeight;
                float flare = 1.0 + (0.12 + input.data.w * 0.22) * (0.5 + 0.5 * sin(_BeatPosition * 6.2831853 + input.data.x * 19.0));
                float fade = exp(-distanceToCamera * 0.00165);

                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                world += (cameraRight * input.uv.x + cameraUp * input.uv.y) * size;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.color = float4(palette * input.color.rgb * _Tint.rgb * _Gain * pulse * flare * fade * hullLight, 1.0);
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
