#ifndef LIMINAL_MATTER_FLOW
#define LIMINAL_MATTER_FLOW

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
