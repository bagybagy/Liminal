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

            struct MatterParticle
            {
                float4 positionAge;
                float4 velocityEnergy;
                float4 colorSize;
                float4 identityState;
            };
            StructuredBuffer<MatterParticle> _Particles;
            CBUFFER_START(UnityPerMaterial)
            float _Gain;
            CBUFFER_END

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 color : COLOR; };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                uint particleIndex = vertexID / 6;
                uint corner = vertexID % 6;
                float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                float2 uv = corners[corner];
                MatterParticle particle = _Particles[particleIndex];
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
                float size = max(particle.colorSize.w, distanceToCamera * 0.00018);
                float stretch = 1.8 + saturate(speed / 18.0) * 5.0;
                float3 world = position + across * uv.x * size + along * uv.y * size * stretch;
                float impact = saturate(particle.velocityEnergy.w);
                float energy = 1.0 + impact * 2.3;
                float3 warm = float3(1.0, 0.82, 0.55);
                float3 pearl = lerp(particle.colorSize.rgb, warm, impact * 0.82);
                if (particle.identityState.z > 2.5)
                    pearl = lerp(pearl, float3(0.38,0.95,0.78),0.28) * 1.6;
                float afterglow = saturate(particle.identityState.w);
                pearl = lerp(pearl, float3(0.12, 0.68, 0.92), afterglow * 0.36);
                Varyings output;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = uv;
                output.color = pearl * energy * _Gain * exp(-distanceToCamera * 0.00125);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float core = exp(-radius * 25.0) * 1.55;
                float halo = exp(-radius * 4.8) * 0.42;
                return half4(input.color * (core + halo), 1);
            }
            ENDHLSL
        }
    }
}
