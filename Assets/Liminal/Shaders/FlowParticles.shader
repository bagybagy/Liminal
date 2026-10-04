Shader "Liminal/Advected Light"
{
    Properties { _Gain ("Radiance", Float) = 1 }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Spine.hlsl"
            #include "MatterFlow.hlsl"
            #include "SharpMatter.hlsl"
            StructuredBuffer<FlowParticle> _Particles;
            CBUFFER_START(UnityPerMaterial)
            float _Gain;
            CBUFFER_END
            float _Song,_Pulse,_Evolution,_Dissolve,_Reduced,_ResonanceClock,_Released;
            float _ReleaseAge;
            uint _ResonanceEventCount;
            StructuredBuffer<float4> _ResonanceEvents;
            uint _OrganStateCount;
            StructuredBuffer<float4> _OrganStates;
            struct Vary {
                float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float fish:TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            struct Attributes { uint vertexID:SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Vary Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Vary o=(Vary)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                uint vertexID=input.vertexID;
                uint index=vertexID/6, corner=vertexID%6;
                float2 quad[6]={float2(-1,-1),float2(-1,1),float2(1,1),float2(-1,-1),float2(1,1),float2(1,-1)};
                float2 uv=quad[corner];
                FlowParticle p=_Particles[index];
                float3 comparisonOffset = _LiminalComparisonPass > 0.5 ? _LiminalComparisonOffset.xyz : 0.0;
                p.position += comparisonOffset;
                bool wake=p.anatomy.w>2.5;
                float3 c,forward,side,up;float width;
                SpineFrame(p.anatomy.x,c,forward,side,up,width);
                c += comparisonOffset;
                float3 cameraRight=UNITY_MATRIX_V[0].xyz, cameraUp=UNITY_MATRIX_V[1].xyz;
                float3 travel=(wake || _Released>.5)?p.velocity:forward;
                float2 screenMotion=float2(dot(travel,cameraRight),dot(travel,cameraUp));
                screenMotion=normalize(screenMotion+float2(.0001,.0001));
                float3 along=cameraRight*screenMotion.x+cameraUp*screenMotion.y;
                float3 across=cameraRight*(-screenMotion.y)+cameraUp*screenMotion.x;
                float distance=length(_WorldSpaceCameraPos-p.position);
                bool fish=_Released>.5 && index%8==0;
                float size=fish?lerp(.14,.26,p.seed):_Released>.5?.018:(wake?.028:.016)*lerp(.75,1.7,p.seed);
                bool legacy=MatterIsLegacy();
                if(legacy) size=max(size,distance*.00028);
                else if(!fish) size=MatterGrainRadius(size,MatterPixelWorld(p.position),p.seed*73,1.25);
                float stretch=fish?3.6:_Released>.5?1.2:legacy?(wake?2.5:lerp(2,5,p.seed)):(wake?1.8:lerp(1,2.1,p.seed));
                if(p.anatomy.w>1.5 && !wake && _Released<.5) size*=1.4;
                float3 pos=p.position+across*uv.x*size+along*uv.y*size*stretch;
                float angle=p.anatomy.y;
                if(p.anatomy.w>=0 && p.anatomy.w<.5 && _Released<.5)
                    angle+=_Song*.20*MatterLook().z;
                float3 normal=side*cos(angle)+up*sin(angle);
                float rim=pow(1-abs(dot(normal,normalize(_WorldSpaceCameraPos-p.position))),1.5);
                float accent=pow(saturate(sin(p.anatomy.x*111-_Song*1.8+p.seed*1.3)),16);
                float3 teal=float3(.025,.38,.94), pearl=float3(.08,.93,.75), gold=float3(1,.54,.13);
                if(legacy) { teal=float3(.025,.52,.64);pearl=float3(.42,.93,.75); }
                float3 color=lerp(teal,pearl,p.seed*.8);
                if(!legacy && p.seed>.86) color=lerp(color,float3(.60,.12,.72),.45);
                color=lerp(color,gold,(p.anatomy.w>.5 && p.anatomy.w<1.5?.52:accent*.5)+_Evolution*.4);
                float localHeat=0;
                [loop] for(uint organIndex=0;organIndex<_OrganStateCount;organIndex++) {
                    float4 organ=_OrganStates[organIndex];
                    if(organ.w<.5) continue;
                    float influence=exp(-pow((p.anatomy.x-organ.x)/.023,2))*organ.y;
                    localHeat=max(localHeat,influence);
                }
                color=lerp(color,float3(1,.20,.045),localHeat*.9);
                float resonanceGlow=0;
                [loop] for(uint eventIndex=0;eventIndex<_ResonanceEventCount;eventIndex++) {
                    float4 eventData=_ResonanceEvents[eventIndex];
                    float eventAge=_ResonanceClock-eventData.y;
                    if(eventAge<0 || eventAge>3.5) continue;
                    float center=saturate(eventData.x+eventAge*.025);
                    resonanceGlow+=exp(-pow((p.anatomy.x-center)/.065,2))*exp(-eventAge*.72);
                }
                float life=wake && _Released<.5?sin(saturate(p.age/p.life)*3.14159):1;
                float brightness=wake?.20:p.anatomy.w>1.5?(legacy?.10:.18):1.0+rim*.8;
                if(!legacy && p.anatomy.w>=0 && p.anatomy.w<.5 && _Released<.5) {
                    float axial=abs(dot(forward,normalize(_WorldSpaceCameraPos-p.position)));
                    brightness*=lerp(1,.22,pow(axial,6));
                }
                brightness+=resonanceGlow*2.3;
                if(_Released>.5) {
                    color=lerp(float3(.10,.55,.67),float3(.65,.94,.78),p.seed);
                    if(p.seed>.91) color=lerp(color,gold,.7);
                    brightness=fish?1.3:.05;
                    brightness *= 1+MatterDeathEnvelope(_ReleaseAge,true).z*_MatterDeathStyle.y;
                }
                if(p.anatomy.w<-.5 && _Released<.5) { color=gold;brightness=2.8; }
                float pulse=1+_Pulse*.24*(1-_Reduced);
                o.positionCS=TransformWorldToHClip(pos);o.uv=uv;
                o.fish=fish?1:0;
                float dissolve=_Released>.5?0:_Dissolve;
                float grains=fish || legacy?1:MatterGrainLight(p.seed*73,_Song+_ReleaseAge,rim);
                o.color=float4(color*brightness*life*_Gain*pulse*grains*(1-dissolve)*exp(-distance*.0016),1);
                return o;
            }
            half4 Frag(Vary i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(i.fish>.5) {
                    float y=i.uv.y;
                    float bodyWidth=.62*sqrt(saturate(1-pow((y-.13)/.85,2)));
                    float tailWidth=y<-.50 ? (-y-.50)*1.15 : 0;
                    float width=max(bodyWidth,tailWidth);
                    clip(width-abs(i.uv.x));
                    float silver=.40+.90*exp(-abs(i.uv.x)*7);
                    return half4(i.color.rgb*silver,1);
                }
                if(MatterIsLegacy()) {
                    float r=dot(i.uv,i.uv);
                    clip(1-r);
                    float light=exp(-r*5)*.55+exp(-r*24)*1.45;
                    return half4(i.color.rgb*light,1);
                }
                return half4(i.color.rgb*MatterSharpCore(i.uv),1);
            }
            ENDHLSL
        }
    }
}
