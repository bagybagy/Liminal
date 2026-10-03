#ifndef LIMINAL_MATTER_FLOW
#define LIMINAL_MATTER_FLOW
float4 _MatterDeathTimings;
float4 _MatterDeathStyle;

float4 MatterDeathEnvelope(float age, bool large)
{
    float charge=max(.05,_MatterDeathTimings.x);
    float duration=max(.2,large?_MatterDeathTimings.z:_MatterDeathTimings.y);
    float loose=smoothstep(0,charge,age);
    float build=smoothstep(charge,charge+duration,age);
    float fade=1-smoothstep(charge+duration,charge+duration+max(.1,_MatterDeathTimings.w),age);
    return float4(loose,loose*lerp(.06,1,build),loose*lerp(.35,1,build)*fade,fade);
}

// A divergence-free, two-scale current keeps adjacent particles moving in eddies rather than random rays.
float3 MatterCurrent(float3 position, float age, float seed)
{
    float3 p=position*0.32+seed*3.7;
    float3 coarse=float3(-cos(p.z*1.37+age*.8),-cos(p.x*1.19-age*.65),-cos(p.y*1.51+age*.7));
    float3 fine=float3(-cos(p.z*2.79-age*1.1),-cos(p.x*2.42+age*.9),-cos(p.y*3.07-age*1.2));
    return coarse+fine*.28;
}

float3 MatterFlowDelta(float3 position, float age, float seed)
{
    return MatterCurrent(position,age,seed)-MatterCurrent(position,0,seed);
}
#endif
