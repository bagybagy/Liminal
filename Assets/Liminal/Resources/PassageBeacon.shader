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
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 radiance : COLOR;
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
                Varyings output;
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
                else
                {
                    float seed = input.data.z;
                    float beat = _BeatPosition * 6.2831853;
                    float eddy = _BeatPosition * 0.31 + seed * 6.2831853;
                    float angle = input.data.x + _BeatPosition * 0.005;
                    angle += sin(eddy * 0.43 + input.data.x * 2.0) * 0.009;
                    float ringRadius = input.data.y +
                        sin(input.data.x * 3.0 + seed * 6.1 + eddy * 0.24) * 0.46 +
                        sin(input.data.x * 8.0 - eddy * 0.51 + seed * 4.7) * 0.29 +
                        sin(input.data.x * 14.0 + eddy * 0.37) * 0.12;
                    float3 center = TransformObjectToWorld(input.path0);
                    float3 right = normalize(TransformObjectToWorldDir(input.path1));
                    float3 up = normalize(TransformObjectToWorldDir(input.path2));
                    float3 axis = normalize(cross(right, up));
                    world = center + right * (cos(angle) * ringRadius) + up * (sin(angle) * ringRadius);
                    world += axis * (sin(angle * 3.0 + eddy * 0.67) * 0.39 +
                        sin(angle * 7.0 - eddy * 0.42 + seed * 8.0) * 0.21);
                    float breath = 0.5 + 0.5 * sin(beat + seed * 6.2831853);
                    brightness = 0.70 + 0.20 * breath + 0.10 * (0.5 + 0.5 * sin(angle * 4.0 - eddy));
                }

                float viewDepth = abs(mul(UNITY_MATRIX_V,float4(world,1.0)).z);
                float projectionScale = max(0.001,abs(UNITY_MATRIX_P._m11));
                float worldPerPixel = 2.0 * max(0.01,viewDepth) / (max(1.0,_ScreenParams.y) * projectionScale);
                float radiusVariation = _FlowEnabled > 0.5 ? saturate(input.data.y) : saturate(input.data.w);
                float radius = max(input.uv.z,2.0 * worldPerPixel) * (1.0 + 0.14 * radiusVariation);
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                world += (cameraRight * input.uv.x + cameraUp * input.uv.y) * radius;
                float pulse = 0.94 + 0.06 * (0.5 + 0.5 * sin(_BeatPosition * 6.2831853));
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv.xy;
                output.radiance = input.color.rgb * _Gain * pulse * brightness;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = dot(input.uv,input.uv);
                clip(1.0-r);
                float glow = exp(-r*5.0)*0.34 + exp(-r*22.0)*1.18;
                return half4(input.radiance * glow,1.0);
            }
            ENDHLSL
        }
    }
}
