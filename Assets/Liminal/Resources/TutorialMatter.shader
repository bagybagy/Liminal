Shader "Liminal/Tutorial Matter"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 2.6
        _MoteRadius ("Mote Radius", Float) = 1.5
        [HideInInspector] _Dispersing ("Dispersing", Float) = 0
        [HideInInspector] _DisperseAt ("Disperse At", Float) = 0
        [HideInInspector] _MoteCenter ("Mote Center", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+8" "RenderPipeline"="UniversalPipeline" }
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
                float4 _Tint;
                float _Gain;
                float _MoteRadius;
                float _Dispersing;
                float _DisperseAt;
                float4 _MoteCenter;
            CBUFFER_END

            float _TutorialSong;
            float _TutorialPulse;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 anchor = TransformObjectToWorld(input.positionOS);
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 cameraForward = UNITY_MATRIX_V[2].xyz;
                float elapsed = max(0.0, _TutorialSong - _DisperseAt);
                float dissolve = _Dispersing * smoothstep(0.0, 0.9, elapsed);
                float seed = input.data.x;
                float phase = seed * 6.2831853 + elapsed * (0.31 + seed * 0.08);
                float orbit = _MoteRadius * (0.18 + 0.82 * frac(seed * 7.173));
                float3 mote = _MoteCenter.xyz
                    + cameraRight * cos(phase) * orbit
                    + cameraUp * sin(phase) * orbit * 0.62
                    + cameraForward * sin(phase * 0.71 + seed * 13.0) * _MoteRadius * 0.16;
                float3 world = lerp(anchor, mote, dissolve);
                world += dissolve * cameraRight * sin(phase * 0.43 + seed * 8.0) * 0.045;

                float2 quad = input.uv.xy;
                float pointSize = input.uv.z * (1.0 - dissolve * 0.32);
                float depth = max(0.01, -TransformWorldToView(world).z);
                float readableSize = 1.4 * 2.0 * depth /
                    (max(abs(UNITY_MATRIX_P[1][1]), 0.01) * max(_ScreenParams.y, 1.0));
                pointSize = max(pointSize, readableSize * (1.0 - dissolve * 0.45));
                float3 positionWS = world + (cameraRight * quad.x + cameraUp * quad.y) * pointSize;
                float pulse = 1.0 + _TutorialPulse * 0.12;
                float distanceFade = exp(-length(_WorldSpaceCameraPos - world) * 0.0007);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = quad;
                output.color = input.color.rgb * _Tint.rgb * (_Gain * pulse * distanceFade);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = dot(input.uv, input.uv);
                clip(1.0 - radius);
                float crispCore = exp(-radius * 8.0) * 1.45;
                float restrainedHalo = exp(-radius * 4.0) * 0.07;
                return half4(input.color * (crispCore + restrainedHalo), 1.0);
            }
            ENDHLSL
        }
    }
}
