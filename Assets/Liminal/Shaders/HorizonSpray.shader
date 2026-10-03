Shader "Liminal/Horizon Spray"
{
    Properties { _Pearl ("Pearl", Color) = (0.60, 0.96, 0.88, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+8" "RenderPipeline"="UniversalPipeline" }
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
                float4 _Pearl;
                float _Song;
                float _WhaleVisibility;
                int _WaterEventCount;
                int _SprayBeadsPerEvent;
            CBUFFER_END
            StructuredBuffer<float4> _WaterEvents;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float kind : TEXCOORD1;
                float4 color : COLOR;
            };

            float Hash(float value)
            {
                return frac(sin(value * 12.9898 + 78.233) * 43758.5453);
            }

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                uint verticesPerEvent = (uint)_SprayBeadsPerEvent * 6u;
                uint eventIndex = vertexID / verticesPerEvent;
                uint within = vertexID % verticesPerEvent;
                uint bead = within / 6u;
                uint corner = within % 6u;
                float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                float2 uv = corners[corner];

                if (eventIndex >= (uint)_WaterEventCount)
                {
                    o.positionCS = TransformWorldToHClip(float3(0, -10000, 0));
                    o.uv = uv;
                    o.kind = 0;
                    o.color = 0;
                    return o;
                }

                float4 origin = _WaterEvents[eventIndex * 2u];
                float4 motion = _WaterEvents[eventIndex * 2u + 1u];
                float eventSeed = origin.w * 31.31 + origin.x * 0.173 + origin.z * 0.271;
                float seed = Hash((float)(eventIndex * 29u + bead * 71u + 13u) + eventSeed);
                float seedB = Hash((float)(eventIndex * 97u + bead * 31u + 47u) + eventSeed * 1.31);
                float age = _Song - origin.w;
                float valid = step(0, age) * step(age, 14) * step(0.0001, motion.w);
                float major = step(3, motion.w);
                valid *= 1 - major;
                float whaleVisibility = _WhaleVisibility;
                float impact = smoothstep(0.85, 1.3, motion.w);
                float eventEnergy = saturate(motion.w / 1.65);
                float2 direction = motion.xy;
                if (dot(direction, direction) < 0.001) direction = float2(0, 1);
                float2 side = float2(-direction.y, direction.x);
                float3 center = origin.xyz;
                float3 longAxis = float3(0, 1, 0);
                float2 extent = 0;
                float3 color = 0;
                float intensity = 0;
                float kind = 1;

                if (bead < 80u)
                {
                    kind = 0;
                    float lane = ((float)bead + seed * 0.35) / 80.0;
                    float sector = floor(lane * 4.0);
                    float sectorU = frac(lane * 4.0);
                    float angle = sector * 1.5707963 + 0.18 + sectorU * 0.92;
                    float2 radial = float2(cos(angle), sin(angle));
                    float radius = lerp(3.5 + seedB * 4.2, 6.0 + seedB * 12.0, major);
                    float launch = seed * 0.12;
                    float t = max(age - launch, 0);
                    float horizontalSpeed = lerp(5.5 + seedB * 5.0 + motion.z * 0.08,
                        8.0 + seedB * 10.0 + motion.z * 0.03, major);
                    float verticalSpeed = lerp(7.0 + seed * 5.0, 17.0 + seed * 11.0, major);
                    float3 velocity = float3(radial.x, 0, radial.y) * horizontalSpeed
                        + float3(direction.x, 0, direction.y) * (motion.z * 0.12)
                        + float3(0, verticalSpeed, 0);
                    float crownLife = lerp(0.62 + seedB * 0.22, 1.35 + seedB * 0.85, major);
                    float3 start = origin.xyz + float3(radial.x * radius, 0.24, radial.y * radius);
                    center = start + velocity * t + float3(0, -4.905 * t * t, 0);
                    longAxis = normalize(velocity + float3(0, -9.81 * t, 0));
                    extent = float2(lerp(0.18 + seedB * 0.18, 0.24 + seedB * 0.28, major),
                        lerp(0.9 + seed * 1.05, 1.15 + seed * 1.35, major));
                    float fade = smoothstep(0, lerp(0.07, 0.11, major), t)
                        * (1 - smoothstep(crownLife * 0.62, crownLife, t));
                    intensity = impact * eventEnergy * (0.90 + seedB * 0.65) * fade * valid * whaleVisibility;
                    float3 baseColor = lerp(float3(0.10, 0.40, 0.42), float3(0.025, 0.42, 0.64), major);
                    float pearlMix = lerp(0.38 + seed * 0.28, 0.08 + seed * 0.12, major);
                    color = lerp(baseColor, _Pearl.rgb, pearlMix);
                }
                else if (bead < 240u)
                {
                    float angle = seed * 6.2831853;
                    float2 radial = float2(cos(angle), sin(angle));
                    float2 launchDirection = normalize(radial * 0.84 + direction * 0.16);
                    float horizontalSpeed = 3.0 + seedB * 7.0 + impact * 2.0;
                    float verticalSpeed = 7.0 + seedB * 7.0 + impact * 2.0 + major * (9.0 + seedB * 5.0);
                    float startHeight = 0.25 + seed * 0.35;
                    float launch = seed * 0.3;
                    float t = age - launch;
                    float gravity = 9.81;
                    float returnTime = (verticalSpeed + sqrt(verticalSpeed * verticalSpeed + 2 * gravity * startHeight)) / gravity;
                    float2 horizontalVelocity = launchDirection * horizontalSpeed + direction * (motion.z * 0.12);
                    float3 start = origin.xyz + float3(radial.x * (1.2 + seedB * 2.0), startHeight, radial.y * (1.2 + seedB * 2.0));
                    center = start + float3(horizontalVelocity.x * max(t, 0), verticalSpeed * max(t, 0) - 0.5 * gravity * max(t, 0) * max(t, 0), horizontalVelocity.y * max(t, 0));
                    float fade = smoothstep(0, 0.06, t) * (1 - smoothstep(returnTime - 0.1, returnTime, t));
                    float selected = lerp(lerp(1 - step(0.14, seed), 1, impact), step(0.36, seed), major);
                    float eventWeight = lerp(0.16, 1.0, impact) * eventEnergy;
                    intensity = selected * eventWeight * (1.0 + seedB * 1.25) * fade
                        * step(0, t) * step(t, returnTime) * valid * whaleVisibility;
                    extent = (0.16 + seedB * 0.2) * float2(1, 1);
                    float3 baseColor = lerp(float3(0.12, 0.50, 0.50), float3(0.025, 0.42, 0.66), major);
                    float pearlMix = lerp(0.48 + seedB * 0.32, 0.08 + seedB * 0.16, major);
                    color = lerp(baseColor, _Pearl.rgb, pearlMix);
                }
                else if (bead < 304u)
                {
                    kind = 2;
                    float launch = seed * 0.48;
                    float t = max(age - launch, 0);
                    float life = 1.6 + seedB * 1.4;
                    float2 drift = direction * (1.5 + motion.z * 0.08) + side * ((seed - 0.5) * 2.8);
                    float2 startOffset = side * ((seedB - 0.5) * 8.0) + direction * ((seed - 0.5) * 4.0);
                    float rise = (0.55 + seedB * 0.8) * t + 0.22 * sin(t * 2.1 + seedB * 6.2831853);
                    center = origin.xyz + float3(startOffset.x + drift.x * t, 0.75 + seedB * 1.2 + rise, startOffset.y + drift.y * t);
                    float fade = smoothstep(0, 0.16, t) * exp(-t * 0.58) * (1 - smoothstep(life * 0.55, life, t));
                    float selected = lerp(1 - step(0.28, seed), 1, impact);
                    intensity = selected * eventEnergy * (0.08 + seedB * 0.045) * fade * valid * whaleVisibility;
                    float size = 1.05 + seedB * 1.15;
                    extent = float2(size * 1.25, size);
                    color = float3(0.10, 0.28, 0.27);
                }
                else
                {
                    kind = 3;
                    float angle = seedB * 6.2831853;
                    float2 radial = float2(cos(angle), sin(angle));
                    float verticalSpeed = 8.0 + seedB * 5.0;
                    float startHeight = 0.3 + seed * 0.4;
                    float launch = 0.03 + seedB * 0.18;
                    float t = age - launch;
                    float gravity = 9.81;
                    float returnTime = (verticalSpeed + sqrt(verticalSpeed * verticalSpeed + 2 * gravity * startHeight)) / gravity;
                    float horizontalSpeed = 3.0 + seed * 4.0;
                    float2 horizontalVelocity = radial * horizontalSpeed + direction * (motion.z * 0.08);
                    center = origin.xyz + float3(horizontalVelocity.x * max(t, 0), startHeight + verticalSpeed * max(t, 0) - 0.5 * gravity * max(t, 0) * max(t, 0), horizontalVelocity.y * max(t, 0));
                    float window = 0.12 + seedB * 0.05;
                    float sparkle = saturate(1 - abs(t - returnTime) / window) * step(0, t) * step(t, returnTime);
                    float rare = step(0.91, seed) * impact;
                    intensity = rare * sparkle * eventEnergy * valid * whaleVisibility * 1.15;
                    float size = 0.12 + seedB * 0.1;
                    extent = float2(size, size);
                    color = lerp(_Pearl.rgb, float3(1, 1, 0.94), 0.62);
                }

                float3 viewDirection = normalize(_WorldSpaceCameraPos - center);
                float pixelWorld = max(0.0001, abs(TransformWorldToHClip(center).w) * 2.0 /
                    (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y));
                if (kind > 0.5 && kind < 1.5) extent = max(extent, pixelWorld * (1.7 + seedB * 1.4));
                float3 right = cross(viewDirection, normalize(longAxis));
                if (dot(right, right) < 0.0001)
                {
                    float3 reference = abs(viewDirection.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0);
                    right = cross(viewDirection, reference);
                }
                right = normalize(right);
                float3 up = normalize(cross(right, viewDirection));
                float3 position = center + right * (uv.x * extent.x) + up * (uv.y * extent.y);
                o.positionCS = TransformWorldToHClip(position);
                o.uv = uv;
                o.kind = kind;
                o.color = float4(color * intensity, intensity);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv;
                float weight;
                if (i.kind < 0.5)
                {
                    float taper = 0.28 + 0.72 * (1 - p.y * p.y);
                    float across = exp(-p.x * p.x * 5.0 / max(0.1, taper));
                    float along = 1 - smoothstep(0.62, 1, abs(p.y));
                    weight = across * along;
                }
                else if (i.kind < 1.5)
                {
                    float radius = dot(p, p);
                    clip(1 - radius);
                    weight = exp(-radius * 3.2);
                }
                else if (i.kind < 2.5)
                {
                    float radius = dot(p, p);
                    weight = exp(-radius * 2.6);
                }
                else
                {
                    float radius = dot(p, p);
                    float rays = exp(-p.x * p.x * 75 - p.y * p.y * 2.0)
                        + exp(-p.y * p.y * 75 - p.x * p.x * 2.0);
                    weight = exp(-radius * 12) + rays * 0.18;
                }
                return half4(i.color.rgb * weight, i.color.a * weight);
            }
            ENDHLSL
        }
    }
}
