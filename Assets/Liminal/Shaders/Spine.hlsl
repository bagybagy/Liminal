#ifndef LIMINAL_SPINE_INCLUDED
#define LIMINAL_SPINE_INCLUDED
StructuredBuffer<float4> _Spine;
struct FlowParticle
{
    float3 position; float age;
    float3 velocity; float life;
    float3 previous; float seed;
    float4 anatomy;
};
float4 SampleSpine(float u)
{
    float f = saturate(u) * 256;
    uint i = min((uint)f, 255);
    return lerp(_Spine[i], _Spine[i+1], f-i);
}
void SpineFrame(float u, out float3 c, out float3 forward, out float3 side, out float3 up, out float width)
{
    float4 center = SampleSpine(u);
    c = center.xyz; width = center.w;
    forward = normalize(SampleSpine(min(1,u+0.0039)).xyz-SampleSpine(max(0,u-0.0039)).xyz);
    side = normalize(cross(float3(0,1,0),forward));
    up = normalize(cross(forward,side));
}
float3 SkinPosition(float u, float angle, float radial, float evolution)
{
    float3 c,forward,side,up; float width;
    SpineFrame(u,c,forward,side,up,width);
    return c+(side*cos(angle)+up*sin(angle))*width*radial;
}
#endif
