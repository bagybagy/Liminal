Shader "Liminal/Persistent Matter"
{
    Properties
    {
        _Gain ("Radiance", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
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
            #include "WhaleAnatomy.hlsl"

            struct MatterParticle
            {
                float4 positionAge;
                float4 velocityEnergy;
                float4 colorSize;
                float4 identityState;
            };
            StructuredBuffer<MatterParticle> _Particles;
            struct MatterSeed { float4 form; float4 destination; float4 color; float4 traits; };
            struct MatterGroup { float4x4 localToWorld; float4 state; float4 impulse; };
            StructuredBuffer<MatterSeed> _Seeds;
            StructuredBuffer<MatterGroup> _Groups;
            StructuredBuffer<float4> _GroupVelocities;
            StructuredBuffer<float4> _AlternateForms;
            StructuredBuffer<float4> _SurfaceFrames;
            CBUFFER_START(UnityPerMaterial)
            float _Gain;
            float _WhaleVisibility;
            int _WhaleGroup;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 color : COLOR;
                float sparkle : TEXCOORD1;
                float isWhale : TEXCOORD2;
                float contour : TEXCOORD3;
                float isAmbient : TEXCOORD4;
                float visibility : TEXCOORD5;
            };

            uint MarineHash(uint value)
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return value;
            }

            float Hash01(uint value)
            {
                return (MarineHash(value) & 0x00ffffffu) * (1.0 / 16777216.0);
            }

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                uint particleIndex = vertexID / 6;
                uint corner = vertexID % 6;
                float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                float2 uv = corners[corner];
                MatterParticle particle = _Particles[particleIndex];
                MatterSeed seed = _Seeds[particleIndex];
                float3 position = particle.positionAge.xyz;
                float3 velocity = particle.velocityEnergy.xyz;
                float speed = length(velocity);
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float2 motion = float2(dot(velocity, cameraRight), dot(velocity, cameraUp));
                float motionLength = length(motion);
                motion = motionLength > 0.001 ? motion / motionLength : float2(0, 1);
                float3 along = cameraRight * motion.x + cameraUp * motion.y;
                float3 across = cameraRight * -motion.y + cameraUp * motion.x;
                float distanceToCamera = length(_WorldSpaceCameraPos - position);
                float pixelWorld = max(0.0001, abs(TransformWorldToHClip(position).w) * 2.0 /
                    (abs(UNITY_MATRIX_P._m11) * _ScreenParams.y));
                float size = max(particle.colorSize.w, pixelWorld * 1.3);
                float stretch = 1.8 + saturate(speed / 18.0) * 5.0;
                bool isDolphin = seed.traits.y > 3.5 && _GroupVelocities[(uint)particle.identityState.y].w > 0.5;
                bool isWhale = seed.traits.y > 2.5 && !isDolphin;
                bool isAmbient = seed.traits.y < 0.5;
                if (isAmbient) {
                    size = max(particle.colorSize.w, pixelWorld * (2.0 + Hash01(particleIndex + 313u) * 1.8));
                    stretch = 1.0;
                }
                float glintHash = Hash01((uint)particle.identityState.x + 17u);
                float contour = isWhale ? step(1.5, seed.traits.w) : 0.0;
                float sparkle = isWhale ? smoothstep(0.92, 1.0, glintHash) *
                    pow(0.5 + 0.5 * sin(particle.positionAge.w * 2.4 + seed.form.z * 0.16 + seed.traits.z * 13.0), 12.0) : 0.0;
                float silhouette = 1.0;
                if (isWhale) {
                    stretch = 1.0;
                    size = max(particle.colorSize.w, pixelWorld * (1.8 + sparkle * 2.6));
                    MatterGroup group = _Groups[(uint)particle.identityState.y];
                    if (particle.identityState.z < 2.5) {
                        float3 normalOS = _SurfaceFrames[particleIndex * 2].xyz;
                        float3 normalWS = normalize(mul((float3x3)group.localToWorld, normalOS));
                        float facing = dot(normalWS, normalize(_WorldSpaceCameraPos - position));
                        float rim = pow(1.0 - abs(facing), 3.0);
                        silhouette = (0.16 + smoothstep(-0.12, 0.24, facing) * 0.84) * (0.65 + rim * 1.15);
                        if (contour > 0.5 && particle.identityState.z < 0.5) {
                            float3 tangent = normalize(mul((float3x3)group.localToWorld, _SurfaceFrames[particleIndex * 2 + 1].xyz));
                            float2 screenTangent = float2(dot(tangent,cameraRight),dot(tangent,cameraUp));
                            screenTangent = normalize(screenTangent + float2(0.0001,0));
                            along = cameraRight * screenTangent.x + cameraUp * screenTangent.y;
                            across = cameraRight * -screenTangent.y + cameraUp * screenTangent.x;
                            size = max(particle.colorSize.w, pixelWorld * 1.35);
                            stretch = 2.1;
                        }
                    }
                    if (particle.identityState.z > 1.5 && particle.identityState.z < 2.5) {
                        size = max(particle.colorSize.w, pixelWorld * (1.15 + sparkle * 1.4));
                        silhouette = lerp(silhouette, 0.85, smoothstep(0.4, 1.4, group.state.y));
                        silhouette *= lerp(0.24, 0.55, smoothstep(1.6, 4.0, group.state.y));
                    }
                    if (particle.identityState.z > 4.5) {
                        stretch = 1.5 + saturate(speed / 60.0) * 5.0;
                        size = max(particle.colorSize.w, pixelWorld * 1.15);
                        silhouette = 1.0;
                    }
                }
                if (isDolphin) {
                    size = max(particle.colorSize.w, pixelWorld * 1.35);
                    stretch = 1.0;
                    silhouette = 1.0;
                }
                bool city = seed.traits.y > 2.5 && particle.identityState.z > 2.5 && particle.identityState.z < 3.5;
                if (city) {
                    size = max(particle.colorSize.w * 0.7, pixelWorld * 1.0);
                    stretch = 1.0;
                    silhouette = 0.48;
                    sparkle = contour = 0.0;
                }
                float3 world = position + across * uv.x * size + along * uv.y * size * stretch;
                float impact = saturate(particle.velocityEnergy.w);
                float energy = 1.0 + impact * 2.3;
                float3 pearl = lerp(particle.colorSize.rgb, float3(0.025,0.70,1.8), impact * (isWhale ? 0.15 : 0.28));
                float patchHeat = isWhale && particle.identityState.z < 0.5
                    ? saturate(particle.identityState.w) : 0.0;
                if (isWhale)
                {
                    float hitState = saturate((patchHeat - 0.28) / 0.72);
                    float3 energizedAnchor = float3(2.2, 0.12, 0.72);
                    pearl = lerp(pearl, energizedAnchor, hitState * 0.82);
                    energy += hitState * 1.6;
                }
                if (city) {
                    pearl = particle.colorSize.rgb;
                    energy = 1.0;
                }
                else if (particle.identityState.z > 2.5 && particle.identityState.z < 3.5)
                    pearl = lerp(pearl, float3(0.38,0.95,0.78),0.28) * 1.6;
                float afterglow = saturate(particle.identityState.w);
                if (isWhale) afterglow = 0.0;
                pearl = lerp(pearl, float3(0.12, 0.68, 0.92), afterglow * 0.36);
                if (isAmbient) {
                    float selected = smoothstep(0.78, 0.90, Hash01(particleIndex + 127u));
                    float habitat = pow(0.5 + 0.5 * sin(position.x * 0.026 + position.y * 0.019 +
                        sin(position.z * 0.023) * 2.0), 2.0);
                    pearl = lerp(particle.colorSize.rgb, float3(0.08,0.65,0.80), afterglow * 0.5);
                    energy = (0.45 + afterglow * 1.6) * max(selected, afterglow * 0.65) * (0.2 + habitat * 0.8);
                }
                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = uv;
                output.color = pearl * energy * _Gain * silhouette * exp(-distanceToCamera * 0.00065);
                output.sparkle = sparkle;
                output.isWhale = isWhale || isDolphin ? 1.0 : 0.0;
                output.contour = contour;
                output.isAmbient = isAmbient ? 1.0 : 0.0;
                output.visibility = isWhale ? _WhaleVisibility : 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                clip(input.visibility - 0.0001);
                clip(1.0 - radius);
                if (input.isAmbient > 0.5) {
                    float ring = exp(-pow(sqrt(radius) - 0.5, 2.0) * 36.0) * 0.24;
                    float mist = exp(-radius * 2.0) * 0.22;
                    return half4(input.color * (ring + mist), 1);
                }
                if (input.isWhale > 0.5)
                {
                    float core = exp(-radius * 6.0) * 1.25;
                    float halo = exp(-radius * 2.4) * 0.08;
                    float glintCore = exp(-radius * 9.0) * input.sparkle * 5.0;
                    float glintHalo = exp(-radius * 2.0) * input.sparkle * 0.20;
                    float3 glintColor = lerp(input.color, float3(0.78, 0.96, 1.2), 0.68);
                    return half4((input.color * (core + halo) + glintColor * (glintCore + glintHalo)) * input.visibility, 1);
                }
                float core = exp(-radius * 9.0) * 1.55;
                float halo = exp(-radius * 3.5) * 0.25;
                return half4(input.color * (core + halo), 1);
            }
            ENDHLSL
        }
    }
}
