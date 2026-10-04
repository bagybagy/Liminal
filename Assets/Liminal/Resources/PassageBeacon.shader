Shader "Liminal/Passage Beacon"
{
    Properties
    {
        _Gain ("Radiance", Float) = 2.8
        _FlowEnabled ("Flow Enabled", Float) = 0
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
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/SharpMatter.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Gain;
            float _FlowEnabled;
            float _BeatPosition;
            CBUFFER_END

            struct Input
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 data : TEXCOORD1;
                float3 path0 : TEXCOORD2;
                float3 path1 : TEXCOORD3;
                float3 path2 : TEXCOORD4;
                float3 path3 : TEXCOORD5;
                float3 path4 : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 radiance : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 PathPosition(float3 a, float3 b, float3 c, float3 d, float3 e, float t, out float3 tangent)
            {
                float4 lengths = float4(length(b-a), length(c-b), length(d-c), length(e-d));
                float total = max(0.001, lengths.x + lengths.y + lengths.z + lengths.w);
                float at = saturate(t) * total;
                if (at < lengths.x) { tangent = normalize(b-a); return lerp(a,b,at/max(0.001,lengths.x)); }
                at -= lengths.x;
                if (at < lengths.y) { tangent = normalize(c-b); return lerp(b,c,at/max(0.001,lengths.y)); }
                at -= lengths.y;
                if (at < lengths.z) { tangent = normalize(d-c); return lerp(c,d,at/max(0.001,lengths.z)); }
                at -= lengths.z;
                tangent = normalize(e-d);
                return lerp(d,e,at/max(0.001,lengths.w));
            }

            Varyings Vert(Input input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world;
                float brightness = 1.0;
                if (_FlowEnabled > 0.5)
                {
                    float phase = frac(input.data.x + _BeatPosition * input.data.y);
                    float3 tangent;
                    float3 local = PathPosition(input.path0,input.path1,input.path2,input.path3,input.path4,phase,tangent);
                    world = TransformObjectToWorld(local);
                    float3 axis = normalize(TransformObjectToWorldDir(tangent));
                    float3 side = cross(axis,float3(0,1,0));
                    if (dot(side,side) < 0.01) side = cross(axis,float3(1,0,0));
                    side = normalize(side);
                    float3 rise = normalize(cross(side,axis));
                    world += side * sin(phase * 6.2831853 + input.data.z) * input.data.w;
                    world += rise * cos(phase * 6.2831853 + input.data.z) * input.data.w * 0.28;
                    brightness = smoothstep(0.0,0.035,phase) * (1.0-smoothstep(0.965,1.0,phase));
                }
                else world = TransformObjectToWorld(input.positionOS);

                bool legacy=MatterIsLegacy();
                float radius;
                if (legacy) {
                    float viewDepth = abs(mul(UNITY_MATRIX_V,float4(world,1.0)).z);
                    float projectionScale = max(0.001,abs(UNITY_MATRIX_P._m11));
                    float worldPerPixel = 2.0 * max(0.01,viewDepth) /
                        (max(1.0,_ScreenParams.y) * projectionScale);
                    radius = max(input.uv.z,1.3 * worldPerPixel) * (1.0 + 0.18 * saturate(input.data.y));
                } else {
                    float pixelWorld = MatterPixelWorld(world);
                    float physicalRadius = input.uv.z * (1.0 + 0.18 * saturate(input.data.y));
                    radius = MatterGrainRadius(physicalRadius, pixelWorld, input.data.x, 1.3);
                }
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                world += (cameraRight * input.uv.x + cameraUp * input.uv.y) * radius;
                float pulse = 0.94 + 0.06 * (0.5 + 0.5 * sin(_BeatPosition * 6.2831853));
                float grainGain = legacy ? 1.0 : 1.0 +
                    (MatterGrainLight(input.data.x, _BeatPosition, saturate(input.data.y)) - 1.0) * 0.14;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.radiance = input.color.rgb * _Gain * pulse * brightness * grainGain;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float r = dot(input.uv,input.uv);
                clip(1.0-r);
                if (MatterIsLegacy()) {
                    float glow = exp(-r*5.0)*0.34 + exp(-r*22.0)*1.18;
                    return half4(input.radiance * glow,1.0);
                }
                return half4(input.radiance * MatterSharpCore(input.uv) * 0.707, 1.0);
            }
            ENDHLSL
        }
    }
}
