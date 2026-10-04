#ifndef LIMINAL_SHARP_MATTER_INCLUDED
#define LIMINAL_SHARP_MATTER_INCLUDED

// Body energy, rare peak energy, surface current, maximum grain footprint.
float4 _LiminalGrainLook;
float _LiminalQuadStyle;
float _LiminalComparisonPass;
float4 _LiminalComparisonOffset;
float _LiminalComparisonGroup;

bool MatterIsLegacy()
{
    if (_LiminalComparisonPass > 1.5) return true;
    if (_LiminalComparisonPass > 0.5) return false;
    return _LiminalQuadStyle > 0.5;
}

float4 MatterLook()
{
    return _LiminalGrainLook.w > 0 ? _LiminalGrainLook : float4(1, 3, 2, 1);
}

float MatterPixelWorld(float3 center)
{
    return max(.00001, abs(TransformWorldToHClip(center).w) * 2 /
        (max(.01, abs(UNITY_MATRIX_P._m11)) * _ScaledScreenParams.y));
}

float MatterGrainRadius(float physicalRadius, float pixelWorld, float seed, float maxPixels)
{
    float tier = .72 + .40 * frac(seed * 19.731);
    return clamp(physicalRadius, pixelWorld * tier * .90,
        pixelWorld * maxPixels * tier * MatterLook().w);
}

float MatterGrainLight(float seed, float song, float accent)
{
    float id = frac(sin(seed * 127.13 + 19.7) * 43758.5453);
    float shimmer = .65 + .35 * pow(.5 + .5 * sin(song * (5 + id * 8) + seed * 17), 2);
    float candidate = step(.93, frac(sin(seed * 79.41 + 8.9) * 23817.193));
    float peak = candidate * pow(saturate(sin(song * (6 + id * 7) + seed * 31)), 18);
    float4 look = MatterLook();
    return shimmer * look.x + peak * look.y * (1 + saturate(accent));
}

float MatterSharpCore(float2 uv)
{
    float r = dot(uv, uv);
    clip(1 - r);
    return exp(-r * 10) * 2.0 + exp(-r * 5) * .16;
}
#endif
