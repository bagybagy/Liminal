Shader "Liminal/Hit Halo"
{
    Properties
    {
        _HitData("Hit Position And Time",Vector)=(0,0,0,-1)
        _HitColor("Hit Color And Strength",Vector)=(1,1,1,1)
        _HitSong("Song Time",Float)=0
        _HitReduced("Reduced Motion",Float)=0
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/SharpMatter.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _HitData,_HitColor;
            float _HitSong,_HitReduced;
            CBUFFER_END
            struct Input { float3 positionOS:POSITION;float4 uv:TEXCOORD0;float2 data:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 glow:COLOR;float active:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Input v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float strength=max(0,_HitColor.a),seed=v.data.x;
                float elapsed=_HitSong-_HitData.w;
                float duration=lerp(.55,.85,saturate((strength-.35)/1.65));
                float life=saturate(elapsed/duration);
                float depth=max(.01,-TransformWorldToView(_HitData.xyz).z);
                float pixelRadius=lerp(clamp(_ScaledScreenParams.y*.055,44,90),clamp(_ScaledScreenParams.y*.09,64,112),saturate((strength-.35)/1.65));
                float pixelWorld=2*depth/(max(abs(UNITY_MATRIX_P[1][1]),.01)*max(_ScaledScreenParams.y,1));
                float outer=lerp(.22,.97*lerp(1,.72,_HitReduced),smoothstep(0,.96,life));
                float inner=lerp(.11,.74*lerp(1,.72,_HitReduced),saturate((life-.08)/.92));
                float radius=v.data.y<.2?outer:inner;
                if(v.data.y>.7) radius=lerp(.18,.95,life)*(.7+.3*seed);
                radius+=(seed-.5)*(.026+life*.032);
                float angle=atan2(v.positionOS.y,v.positionOS.x)+elapsed*(seed-.5)*lerp(.75,.2,_HitReduced);
                float2 offset=float2(cos(angle),sin(angle))*radius*pixelRadius;
                float size=max(1.05,v.uv.z*pixelRadius)*(1-.42*life);
                float3 particleCenter=_HitData.xyz+(UNITY_MATRIX_V[0].xyz*offset.x+
                    UNITY_MATRIX_V[1].xyz*offset.y)*pixelWorld;
                float grainPixelWorld=MatterPixelWorld(particleCenter);
                float grainSize=MatterGrainRadius(size*grainPixelWorld,grainPixelWorld,seed,1.8);
                float3 world=particleCenter+(UNITY_MATRIX_V[0].xyz*v.uv.x+
                    UNITY_MATRIX_V[1].xyz*v.uv.y)*grainSize;
                o.positionCS=TransformWorldToHClip(world);o.uv=v.uv.xy;
                o.active=step(0,elapsed)*step(elapsed,duration);
                float3 incoming=max(_HitColor.rgb,0);
                float3 hue=incoming/max(max(incoming.r,incoming.g),max(incoming.b,.0001));
                float warm=saturate((max(hue.r-hue.b,(hue.r-hue.g)*.55)-.04)*1.7);
                float cyan=saturate((hue.g-hue.r)*1.1);
                float3 blue=lerp(float3(.035,.22,1),float3(.015,.76,1),cyan);
                float fade=1-smoothstep(.68,1,life);
                float grainLight=MatterGrainLight(seed,_HitSong,saturate(strength/2.0));
                o.glow=lerp(blue,hue,warm)*fade*grainLight*2.2*max(.7,exp(-depth*.0006));
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(i.active-.5);
                return half4(i.glow*MatterSharpCore(i.uv)*.6,1);
            }
            ENDHLSL
        }
    }
}
