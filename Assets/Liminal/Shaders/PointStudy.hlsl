#ifndef LIMINAL_POINT_STUDY_INCLUDED
#define LIMINAL_POINT_STUDY_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Spine.hlsl"

StructuredBuffer<float4> _Samples;
CBUFFER_START(UnityPerMaterial)
float4 _Anchor;
float _Song, _Shape, _Gain, _Flow, _SurfaceArea, _ActiveCount, _Visibility;
CBUFFER_END

float StudyHash(uint v)
{
    v ^= v >> 16; v *= 0x7feb352du;
    v ^= v >> 15; v *= 0x846ca68bu;
    v ^= v >> 16;
    return (v & 0xffffffu) / 16777216.0;
}

float3 JellySample(float4 p, float id)
{
    float t = _Song * _Flow;
    float u = saturate(p.x + .016 * sin(t * .7 + id * 17));
    float angle = p.y + t * .24;
    float3 localPosition;
    if (p.z < .5) {
        float radius = sin(u * 1.5707963) * 4.6;
        float fluting = 1 + .035 * cos(angle * 30 + t * .45);
        localPosition = float3(cos(angle) * radius * fluting, .25 + cos(u * 1.5707963) * 3.5,
            sin(angle) * radius * fluting);
    } else if (p.z < 1.5) {
        float strand = floor(p.w * 16);
        float a = strand * .3926991 + t * .11;
        float length = 8 + 9 * frac(strand * .6180339);
        float wave = u * 11 - t * .68 + strand * 1.7;
        float radius = 3.45 + sin(wave) * u * 1.05;
        float spread = (id - .5) * .14;
        localPosition = float3(cos(a + spread) * radius + sin(wave * .67) * u * .7,
            .25 - u * length, sin(a + spread) * radius + cos(wave * .71) * u * .7);
    } else {
        float radius = pow(p.w, .3333333) * 1.7;
        float y = u * 2 - 1;
        float across = sqrt(saturate(1 - y * y));
        localPosition = float3(cos(angle) * across, y * 1.8, sin(angle) * across) * radius;
    }
    return _Anchor.xyz + localPosition * (1 + .045 * sin(t * 1.1));
}

float3 SerpentSample(float4 p, uint index)
{
    float t = _Song * _Flow;
    float u = clamp(p.x + .012 * sin(t * .55 + p.x * 19), .0001, .9999);
    float angle = p.y + t * .47;
    float3 c, forward, side, up; float width;
    SpineFrame(u, c, forward, side, up, width);
    if (index < 6144) {
        u = .037 + p.x * .019;
        SpineFrame(u, c, forward, side, up, width);
        angle = (index & 1) == 0 ? .23 : 2.9116;
        angle += (p.y - 3.1415926) * .045;
        return c + (side * cos(angle) + up * sin(angle)) * width * 1.015;
    }
    float radial = p.w < .5 ? 1 : p.w < 1.5 ? 1 + p.z * .58 : p.z * .9;
    if (p.w > .5 && p.w < 1.5) {
        angle = floor(p.y / 2.0943951) * 2.0943951 + .12 * sin(u * 24 - t);
    }
    float ripple = sin(u * 70 + angle * 5 - t * 1.3) * .023;
    return c + (side * cos(angle) + up * sin(angle)) * width * (radial + ripple);
}

struct StudyAttributes {
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct StudyVaryings {
    float4 positionCS : SV_POSITION;
    float3 light : TEXCOORD0;
#if defined(STUDY_QUAD)
    float2 uv : TEXCOORD1;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

StudyVaryings StudyVertex(StudyAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    StudyVaryings output = (StudyVaryings)0;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
#if defined(STUDY_QUAD)
    uint index = input.vertexID / 6;
#else
    uint index = input.vertexID;
#endif
    float id = StudyHash(index * 11 + 31);
    float4 seedData = _Samples[index];
    float3 position = _Shape < .5 ? JellySample(seedData, id) : SerpentSample(seedData, index);
    float4 clipPosition = TransformWorldToHClip(position);
    float distance = max(.01, -TransformWorldToView(position).z);
    // Compensate projected overlap, not geometry size. Dense distant points must not form a white sheet.
    float pixelWorld = 2 * distance / (max(.01, abs(UNITY_MATRIX_P[1][1])) * _ScaledScreenParams.y);
    float coverage = min(1, _SurfaceArea / max(1, _ActiveCount) / max(.00001, pixelWorld * pixelWorld));
    float shimmer = .28 + .9 * pow(.5 + .5 * sin(_Song * (5 + id * 12) + id * 127), 2);
    float spark = pow(saturate(sin(_Song * (4 + id * 9) + id * 391)), 16) * 2.5;
    if (_Shape > .5) {
        // Dense anatomy needs quiet matter between rare sharp sparks, not uniformly emissive skin.
        shimmer *= .055;
        spark *= 2.4 * step(.94, StudyHash(index * 17 + 83));
        float3 c, forward, side, up; float width;
        SpineFrame(seedData.x,c,forward,side,up,width);
        float axial=abs(dot(forward,normalize(_WorldSpaceCameraPos-position)));
        coverage*=lerp(1,.20,pow(axial,6));
    }
    float drift = .5 + .5 * sin(seedData.x * 12 - _Song * .65 + seedData.y * .3);
    float3 blue = float3(.07, .34, 1);
    float3 cyan = float3(.06, .88, .74);
    float3 violet = float3(.65, .09, .62);
    float3 color = lerp(blue, cyan, smoothstep(.12, .82, drift));
    color = lerp(color, violet, smoothstep(.74, 1, id) * .72);
    if (id > .975) color = lerp(color, float3(1, .52, .12), .65);
    output.light = color * coverage * (shimmer * min(_Gain,4) + spark * _Gain) * _Visibility;
#if defined(STUDY_QUAD)
    uint corner = input.vertexID % 6;
    float2 uv = corner == 0 ? float2(-1,-1) : corner == 1 ? float2(1,-1) :
        corner == 2 || corner == 3 ? float2(1,1) : corner == 4 ? float2(-1,1) : float2(-1,-1);
    float radius = .75 + id * .3;
    clipPosition.xy += uv * radius * 2 / _ScaledScreenParams.xy * clipPosition.w;
    output.uv = uv;
    output.light *= .75;
#endif
    output.positionCS = clipPosition;
    return output;
}

float4 StudyFragment(StudyVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(STUDY_QUAD)
    float radiusSquared = dot(input.uv, input.uv);
    clip(1 - radiusSquared);
    return float4(input.light * exp(-radiusSquared * 5), 1);
#else
    return float4(input.light, 1);
#endif
}
#endif
