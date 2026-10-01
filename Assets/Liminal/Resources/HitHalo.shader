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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _HitData,_HitColor;
            float _HitSong,_HitReduced;
            CBUFFER_END
            struct Input { float3 positionOS:POSITION;float4 uv:TEXCOORD0;float2 data:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 glow:COLOR;float active:TEXCOORD1; };
            Varyings Vert(Input v)
            {
                Varyings o;
                float strength=max(0,_HitColor.a),seed=v.data.x;
                float elapsed=_HitSong-_HitData.w;
                float duration=lerp(.55,.85,saturate((strength-.35)/1.65));
                float life=saturate(elapsed/duration);
                float depth=max(.01,-TransformWorldToView(_HitData.xyz).z);
                float pixelRadius=lerp(clamp(_ScreenParams.y*.055,44,90),clamp(_ScreenParams.y*.09,64,112),saturate((strength-.35)/1.65));
                float pixelWorld=2*depth/(max(abs(UNITY_MATRIX_P[1][1]),.01)*max(_ScreenParams.y,1));
                float outer=lerp(.22,.97*lerp(1,.72,_HitReduced),smoothstep(0,.96,life));
                float inner=lerp(.11,.74*lerp(1,.72,_HitReduced),saturate((life-.08)/.92));
                float radius=v.data.y<.2?outer:inner;
                if(v.data.y>.7) radius=lerp(.18,.95,life)*(.7+.3*seed);
                radius+=(seed-.5)*(.026+life*.032);
                float angle=atan2(v.positionOS.y,v.positionOS.x)+elapsed*(seed-.5)*lerp(.75,.2,_HitReduced);
                float2 offset=float2(cos(angle),sin(angle))*radius*pixelRadius;
                float size=max(1.05,v.uv.z*pixelRadius)*(1-.42*life);
                float3 world=_HitData.xyz+(UNITY_MATRIX_V[0].xyz*(offset.x+v.uv.x*size)+
                    UNITY_MATRIX_V[1].xyz*(offset.y+v.uv.y*size))*pixelWorld;
                o.positionCS=TransformWorldToHClip(world);o.uv=v.uv.xy;
                o.active=step(0,elapsed)*step(elapsed,duration);
                float3 incoming=max(_HitColor.rgb,0);
                float3 hue=incoming/max(max(incoming.r,incoming.g),max(incoming.b,.0001));
                float warm=saturate((max(hue.r-hue.b,(hue.r-hue.g)*.55)-.04)*1.7);
                float cyan=saturate((hue.g-hue.r)*1.1);
                float3 blue=lerp(float3(.035,.22,1),float3(.015,.76,1),cyan);
                float fade=1-smoothstep(.68,1,life);
                float scintillation=.7+.6*pow(saturate(sin(seed*37+elapsed*13)),8);
                o.glow=lerp(blue,hue,warm)*fade*scintillation*2.2*max(.7,exp(-depth*.0006));
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.active-.5);
                float r2=dot(i.uv,i.uv);clip(1-r2);
                return half4(i.glow*(exp(-r2*5)+.14*exp(-r2*2)),1);
            }
            ENDHLSL
        }
    }
}
