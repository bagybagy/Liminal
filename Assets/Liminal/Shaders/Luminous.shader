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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Gain, _Mode;
            float4 _Burst;
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
            struct Input { float4 positionOS : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; float2 data : TEXCOORD1; };
            struct Vary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Vary Vert(Input input)
            {
                Vary o;
                float3 p = input.positionOS.xyz;
                float size = input.uv.z;
                float twinkle = 0.85 + 0.15*sin(_Song*1.1+input.data.x*31);
                float resonance = 0;
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
                    p = _Burst.xyz + p * age * 9;
                    twinkle *= exp(-age*2.3);
                    size *= 1+age*1.5;
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
                p += (right*input.uv.x + upCam*input.uv.y)*size;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = input.uv.xy;
                float3 col = input.color.rgb;
                col = lerp(col, float3(0.78,0.95,1), saturate(resonance*0.18));
                twinkle *= 1+resonance;
                if (_Mode > 0.5 && _Mode < 1.5)
                    col = lerp(col, col.gbr*float3(1.8,0.7,0.3)+float3(0.24,0.04,0), _Evolution*0.72);
                o.color = float4(col*_Tint.rgb*_Gain*twinkle*exp(-distance*0.0018),1);
                return o;
            }
            half4 Frag(Vary i) : SV_Target
            {
                float r = dot(i.uv,i.uv);
                clip(1-r);
                float glow = exp(-r*5)*0.35 + exp(-r*24)*1.65;
                return half4(i.color.rgb*glow,1);
            }
            ENDHLSL
        }
    }
}
