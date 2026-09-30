Shader "Liminal/Defeated Marine Forms"
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
            float4x4 _SourceLocalToWorld;
            float4 _TargetRoot;
            float4 _ColonyRight;
            float4 _ColonyForward;
            float4 _RoomColor;
            float4 _RoomAccent;
            float4 _SourceTint;
            float _SourceGain;
            float _DeathSong;
            float _Elapsed;
            CBUFFER_END
            float _Song, _Pulse, _Reduced;

            struct Input
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float visible : TEXCOORD1;
                float4 field : TEXCOORD2;
            };

            float Hash(float value, float salt)
            {
                return frac(sin(value * 127.1 + salt * 311.7) * 43758.5453);
            }

            float3 ColonyPoint(float seed, out float visible, out float accent)
            {
                float kind = Hash(seed, 1.0);
                float a = Hash(seed, 2.0);
                float b = Hash(seed, 3.0);
                float c = Hash(seed, 4.0);
                float d = Hash(seed, 5.0);
                float angle = b * 6.2831853;
                float3 offset = 0;
                visible = 1;
                accent = 0.16 + Hash(seed, 6.0) * 0.25;

                if (kind < 0.10)
                {
                    float radius = 0.22 + c * 0.52;
                    offset = float3(cos(angle) * radius, 0.35 + d * 10.5, sin(angle) * radius);
                    accent = 0.10;
                }
                else if (kind < 0.43)
                {
                    float plate = floor(a * 3.0);
                    float radius = 0.55 + sqrt(c) * 4.5;
                    offset = float3(cos(angle) * radius, 1.3 + plate * 2.25 + sin(angle * 4.0) * 0.14,
                        sin(angle) * radius);
                    accent = 0.18 + plate * 0.08;
                }
                else if (kind < 0.67)
                {
                    float tube = floor(a * 5.0);
                    float tubeAngle = tube * 1.2566371;
                    float height = 0.65 + c * 8.2;
                    float radius = 0.78 + 0.09 * sin(angle * 3.0 + height * 0.4);
                    float centerRadius = 2.25;
                    float x = cos(tubeAngle) * centerRadius + cos(angle + tubeAngle) * radius;
                    float z = sin(tubeAngle) * centerRadius + sin(angle + tubeAngle) * radius;
                    offset = float3(x, height, z);
                    float pores = abs(sin(angle * 5.0)) * abs(sin(height * 1.55 + angle * 0.8));
                    if (height > 1.1 && height < 7.5 && pores < 0.10) offset.y = 8.25;
                    accent = height > 7.2 ? 0.45 : 0.20;
                }
                else if (kind < 0.83)
                {
                    float tentacle = floor(a * 18.0);
                    float t = c;
                    float tentacleAngle = tentacle * 0.349066;
                    float reach = 0.32 + t * (1.1 + d * 1.2);
                    float sway = sin(t * 3.5 + tentacleAngle) * t * 0.65;
                    offset = float3(cos(tentacleAngle) * (reach + sway), 0.25 + t * (4.5 + d * 3.2),
                        sin(tentacleAngle) * (reach + sway));
                    accent = 0.18 + t * 0.30;
                }
                else if (kind < 0.92)
                {
                    float ray = floor(a * 17.0) - 8.0;
                    float t = c;
                    offset = float3(ray * (0.33 + t * 0.43), t * 11.5,
                        sin(t * 3.6 + ray * 0.22) * 0.48);
                    accent = 0.22 + t * 0.24;
                }
                else
                {
                    float blade = floor(a * 7.0);
                    float t = c;
                    float x = (blade - 3.0) * 0.63 + sin(t * 4.1 + blade) * (0.42 + t * 0.95);
                    float z = cos(t * 3.0 + blade * 0.8) * (0.35 + t * 1.2) + (d - 0.5) * 0.75;
                    offset = float3(x, 0.25 + t * 14.0, z);
                    accent = 0.12 + t * 0.24;
                }

                return _TargetRoot.xyz + _ColonyRight.xyz * offset.x + float3(0, offset.y, 0) +
                    _ColonyForward.xyz * offset.z;
            }

            Varyings Vert(Input input)
            {
                Varyings output;
                float3 local = input.positionOS.xyz;
                local.y += sin(local.x * 0.06 + local.z * 0.04 + _DeathSong * 0.16) * input.data.y;
                float3 source = mul(_SourceLocalToWorld, float4(local, 1)).xyz;

                float visible, accent;
                float3 target = ColonyPoint(input.data.x, visible, accent);
                float elapsed = max(0, _Elapsed);
                float span = saturate(abs(local.x) / 2.7);
                float depth = saturate(abs(local.z) / 1.4);
                float edge = saturate(smoothstep(0.22, 0.86, span) * 0.72 + smoothstep(0.2, 0.94, depth) * 0.28);
                float bandX = floor(span * 4.0);
                float bandZ = floor((local.z + 1.4) * 1.42);
                float subgroup = bandX + bandZ * 4.0 + (local.x < 0.0 ? 24.0 : 0.0);
                float sideSign = local.x < 0.0 ? -1.0 : 1.0;
                float3 peelAxis = normalize(float3(sideSign * (0.42 + Hash(subgroup, 21.0) * 0.48),
                    -0.12 + Hash(subgroup, 22.0) * 0.72, (Hash(subgroup, 23.0) - 0.5) * 0.9));
                float3 curlSide = normalize(cross(float3(0, 1, 0), peelAxis));
                float3 curlNormal = normalize(cross(peelAxis, curlSide));
                float phase = Hash(subgroup, 24.0) * 6.2831853;
                float turn = max(0.0, elapsed - 0.38) * (2.5 + Hash(subgroup, 25.0) * 1.1);
                float3 curl = curlSide * (sin(turn + phase) - sin(phase)) * (0.18 + edge * 0.46) +
                    curlNormal * (cos(phase) - cos(turn + phase)) * (0.14 + edge * 0.34);
                float releaseStart = 0.40 + (1.0 - edge) * 0.18;
                float scatter = smoothstep(releaseStart, releaseStart + 1.0, elapsed);
                float settle = smoothstep(1.6, 7.4, elapsed);
                float current = elapsed * (1.0 - saturate(elapsed / 2.0));
                float spread = 0.8 + edge * 2.0 + Hash(input.data.x, 10.0) * 1.15;
                float3 dispersed = source + peelAxis * (scatter * spread) + curl * scatter +
                    (_ColonyRight.xyz * 0.42 + _ColonyForward.xyz * 0.28) * current * scatter;
                float3 world = lerp(dispersed, target, settle);
                float kelp = step(0.92, Hash(input.data.x, 1.0));
                world += _ColonyRight.xyz * sin(_Song * 0.47 + input.data.x * 19.0) * kelp * settle * 0.10;

                float distanceToCamera = length(_WorldSpaceCameraPos - world);
                float fieldWeight = smoothstep(0.52, 1.17, elapsed) * (1.0 - settle);
                float size = input.uv.z * max(1.0, distanceToCamera * 0.006) * (1.0 + settle) * (1.0 + fieldWeight * 3.0);
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                world += (cameraRight * input.uv.x + cameraUp * input.uv.y) * size;

                float3 rawSourceColor = input.color.rgb * _SourceTint.rgb * _SourceGain;
                float sourceLevel = max(max(rawSourceColor.r, rawSourceColor.g), rawSourceColor.b);
                float3 sourceHue = rawSourceColor / max(sourceLevel, 0.0001);
                float warm = saturate((max(sourceHue.r-sourceHue.b, (sourceHue.r-sourceHue.g)*0.55)-0.04)*1.7);
                float cyan = saturate((sourceHue.g-sourceHue.r)*1.1);
                float3 electricHue = lerp(float3(0.035,0.22,1.0),float3(0.015,0.76,1.0),cyan);
                float3 startColor = lerp(electricHue * sourceLevel, rawSourceColor, warm);
                float charge = smoothstep(0.0, 0.48, elapsed);
                float flare = charge * exp(-max(0.0, elapsed - 0.46) * 1.15);
                startColor *= 1.0 + flare * 0.32;
                float3 formTint = lerp(_RoomColor.rgb, _RoomAccent.rgb, accent);
                float3 formColor = formTint * (0.92 + Hash(input.data.x, 11.0) * 0.28);
                float pulse = lerp(0.92 + 0.08 * _Pulse * (1.0 - _Reduced), 1.0 + _Pulse * 0.055 * (1.0 - _Reduced), settle);
                float distanceFade = exp(-distanceToCamera * 0.0018);

                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.color = float4(lerp(startColor, formColor, settle) * _Tint.rgb * _Gain * pulse * distanceFade, 1);
                output.field = float4(formColor * _Tint.rgb * _Gain * pulse * distanceFade, fieldWeight * 0.58);
                output.visible = lerp(1.0, visible, settle);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.visible - 0.5);
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float glow = exp(-radius * 5.0) * 0.35 + exp(-radius * 24.0) * 1.65;
                float fieldGlow = exp(-radius * 3.6) * 0.42 + exp(-radius * 10.0) * 0.3;
                return half4(input.color.rgb * glow + input.field.rgb * input.field.a * fieldGlow, 1);
            }
            ENDHLSL
        }
    }
}
