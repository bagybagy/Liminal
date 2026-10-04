Shader "Liminal/Luminous"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Gain ("Radiance", Float) = 1
        _Mode ("Geometry", Float) = 0
        _CaveWaveEnergy ("Environment response", Float) = 0
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
            #include "MatterFlow.hlsl"
            #include "SharpMatter.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Gain, _Mode;
            float4 _Burst;
            float4 _BurstVelocity;
            float _BurstLarge;
            float4 _CaveWave;
            float _CaveWaveEnergy;
            CBUFFER_END
            float _Song, _Pulse, _Evolution, _Dissolve, _Reduced;
            float3 Curve(float u)
            {
                float a = (u * 1.48 - 0.55) * PI + sin(_Song * 0.17) * 0.22;
                return float3(sin(a)*26, cos(a*1.8+_Song*0.27)*(6+_Evolution*2) + sin(u*16-_Song*0.65)*1.1 + _Evolution*2,
                    34+cos(a)*9+sin(u*10+_Song*0.3)*(3+_Evolution*3));
            }
            float Width(float u)
            {
                return (0.3 + pow(saturate(sin((u*0.88+0.07)*PI)), 0.65)*2.9) * (1-smoothstep(0.72,1,u)*0.9) *
                    (1+exp(-pow((u-0.047)/0.032,2))*0.7)*(0.12+0.88*smoothstep(0,0.03,u));
            }
            struct Input { float4 positionOS : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; float2 data : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Vary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float core : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float value, float salt)
            {
                return frac(sin(value * 127.1 + salt * 311.7) * 43758.5453);
            }
            Vary Vert(Input input)
            {
                Vary o;
                UNITY_SETUP_INSTANCE_ID(input);
                bool legacy = MatterIsLegacy();
                float3 p = input.positionOS.xyz;
                float size = input.uv.z;
                float grainAccent = saturate(input.color.a);
                float twinkle = legacy ? .85 + .15 * sin(_Song * 1.1 + input.data.x * 31) :
                    MatterGrainLight(input.data.x,_Song,grainAccent);
                float resonance = 0;
                float impactCore = 0;
                if (_Mode > 0.5 && _Mode < 1.5)
                {
                    float u = p.x, angle = p.y;
                    float3 c = Curve(u);
                    float3 forward = normalize(Curve(u+0.002)-Curve(u-0.002));
                    float3 side = normalize(cross(float3(0,1,0), forward));
                    float3 up = normalize(cross(forward, side));
                    float breath = 1 + sin(u*35-_Song*2)*0.045 + _Pulse*0.055;
                    float radius = Width(u)*p.z*breath*(1+_Evolution*input.data.y*0.8);
                    p = c + (side*cos(angle)+up*sin(angle))*radius;
                    if (input.data.y > 0.5)
                        p += up*sin(u*28-_Song*2.1)*input.data.y*0.55;
                    float facing = dot(normalize(side*cos(angle)+up*sin(angle)),normalize(_WorldSpaceCameraPos-p));
                    twinkle *= 0.42 + 0.58*saturate(facing*0.7+0.3);
                    float spread = _Dissolve*(_Dissolve*40 + 1);
                    p += float3(sin(input.data.x*317), cos(input.data.x*417), sin(input.data.x*719))*spread;
                    twinkle *= (1-_Dissolve*0.85) * (1 + _Pulse*0.35*(1-_Reduced));
                }
                else if (_Mode > 1.5)
                {
                    float age = max(0,_Song-_Burst.w);
                    float4 death = MatterDeathEnvelope(age,_BurstLarge>.5);
                    impactCore = 1.0 - step(0.0, input.data.y);
                    if (impactCore > 0.5) {
                        p = _Burst.xyz + p;
                    } else {
                        float group = floor(input.data.y + 0.5);
                        float angle = group * 2.39996323 + (Hash(group + 1.0, 1.0) - 0.5) * 0.24;
                        float elevation = lerp(-0.22, 0.78, frac(group * 0.38196601 + 0.17));
                        elevation += (Hash(group + 1.0, 2.0) - 0.5) * 0.12;
                        float horizontal = sqrt(max(0.05, 1.0 - elevation * elevation));
                        float3 axis = normalize(float3(cos(angle) * horizontal, elevation, sin(angle) * horizontal));
                        float3 side = normalize(cross(float3(0, 1, 0), axis));
                        float3 normal = normalize(cross(axis, side));
                        float phase = Hash(group + 1.0, 3.0) * 6.2831853;
                        float turn = age * (2.35 + Hash(group + 1.0, 4.0) * 0.8);
                        float travel = death.y * (1.8 + Hash(group + 1.0, 5.0) * 2.5);
                        float3 curl = side * (sin(turn + phase) - sin(phase)) * 0.32 +
                            normal * (cos(phase) - cos(turn + phase)) * 0.25;
                        p = _Burst.xyz + p + axis * travel + curl*death.y;
                        p += MatterFlowDelta(p*.45,age,group*.037)*death.y*2.0*_MatterDeathStyle.x;
                        p += _BurstVelocity.xyz*(1.0-exp(-age*1.4))/1.4;
                        p += axis*(age*age*exp(-age*2.0))*.7;
                        p += side * sin(age * 4.2 + input.data.x * 6.2831853) * 0.045 * saturate(age * 2.0);
                    }
                    float grainFade = death.w*(.7+death.z*_MatterDeathStyle.y);
                    float coreFade = 0.16 * exp(-age * 1.25) + 0.88 * exp(-age * 12.0);
                    twinkle = lerp(grainFade, coreFade, impactCore);
                    if (!legacy)
                        twinkle *= MatterGrainLight(input.data.x,_Song,max(grainAccent,saturate(death.z)));
                    size *= 1.0 + min(age * 0.16, 0.24);
                }
                else
                {
                    p.y += sin(p.x*0.06+p.z*0.04+_Song*0.16)*input.data.y;
                    p = TransformObjectToWorld(p);
                    twinkle *= 0.92 + 0.08*_Pulse*(1-_Reduced);
                    if (_CaveWaveEnergy > 0)
                    {
                        float age = max(0, _Song - _CaveWave.w);
                        float3 delta = p - _CaveWave.xyz;
                        float radius = length(delta);
                        float ring = exp(-pow((radius-age*32)/7,2)) * exp(-age*0.18);
                        float flash = exp(-radius/85-age*1.2);
                        resonance = (ring*2+flash*3)*_CaveWaveEnergy;
                        p += delta/max(radius,1)*ring*_CaveWaveEnergy*0.65;
                    }
                }
                float3 toCamera = _WorldSpaceCameraPos-p;
                float distance = length(toCamera);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 upCam = UNITY_MATRIX_V[1].xyz;
                size *= max(1, distance * 0.006);
                if (_Mode > 1.5 && impactCore > 0.5) {
                    float screenHeight = legacy ? _ScreenParams.y : _ScaledScreenParams.y;
                    size = max(size, distance * 2.5 / (max(screenHeight, 1.0) * max(abs(UNITY_MATRIX_P[1][1]), 0.01)));
                }
                if (!legacy) {
                    float maxGrainPixels = _Mode > 1.5 ? 1.8 : 2.2;
                    size = MatterGrainRadius(size,MatterPixelWorld(p),input.data.x,maxGrainPixels);
                }
                p += (right*input.uv.x + upCam*input.uv.y)*size;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = input.uv.xy;
                float3 col = input.color.rgb;
                col = lerp(col, float3(0.78,0.95,1), saturate(resonance*0.18));
                twinkle *= 1+resonance;
                if (_Mode > 0.5 && _Mode < 1.5)
                    col = lerp(col, col.gbr*float3(1.8,0.7,0.3)+float3(0.24,0.04,0), _Evolution*0.72);
                if (_Mode > 1.5) {
                    float tintLevel = max(max(_Tint.r, _Tint.g), _Tint.b);
                    float3 tintHue = _Tint.rgb / max(tintLevel, 0.0001);
                    float warm = saturate((max(tintHue.r-tintHue.b, (tintHue.r-tintHue.g)*0.55)-0.04)*1.7);
                    float cyan = saturate((tintHue.g-tintHue.r)*1.1);
                    float3 electricHue = lerp(float3(0.035,0.22,1.0),float3(0.015,0.76,1.0),cyan);
                    float localLevel = max(max(col.r,col.g),col.b);
                    float3 grainColor = lerp(electricHue*tintLevel*localLevel, col*_Tint.rgb, warm);
                    float coreWhite = (1.0-smoothstep(0.0,0.08,max(0.0,_Song-_Burst.w)))*0.48;
                    float3 coreColor = lerp(grainColor,float3(1,1,1),coreWhite);
                    o.color = float4(lerp(grainColor, coreColor, impactCore) * _Gain * twinkle * exp(-distance * 0.0018), 1);
                } else {
                    o.color = float4(col*_Tint.rgb*_Gain*twinkle*exp(-distance*0.0018),1);
                }
                o.core = impactCore;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                return o;
            }
            half4 Frag(Vary i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = dot(i.uv,i.uv);
                bool legacy = MatterIsLegacy();
                if (legacy) {
                    clip(1-r);
                    if (_Mode > 1.5) {
                        float grainGlow = exp(-r * 4.2) * 0.58 + exp(-r * 22.0) * 0.8;
                        float coreGlow = exp(-r * 3.6) * 0.78 + exp(-r * 20.0) * 1.32;
                        return half4(i.color.rgb * lerp(grainGlow, coreGlow, i.core), 1);
                    }
                    float legacyGlow = exp(-r * 5) * 0.35 + exp(-r * 24) * 1.65;
                    return half4(i.color.rgb * legacyGlow,1);
                }
                float sharpCore = MatterSharpCore(i.uv);
                if (_Mode > 1.5) {
                    float grainGlow = sharpCore * 0.72;
                    float coreGlow = sharpCore * 1.10;
                    return half4(i.color.rgb * lerp(grainGlow, coreGlow, i.core), 1);
                }
                float glow = sharpCore * 1.05;
                return half4(i.color.rgb*glow,1);
            }
            ENDHLSL
        }
    }
}
