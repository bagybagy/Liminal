#ifndef LIMINAL_ATLANTIS_LIGHT_INCLUDED
#define LIMINAL_ATLANTIS_LIGHT_INCLUDED

static const float AtlantisMatterFlowMeters = 0.42;
static const float AtlantisMatterCoherentFlow = 0.25;
static const float AtlantisMatterIndependentFlow = 0.075;
static const float AtlantisMatterContourFlow = 0.42;
static const float AtlantisMatterRegionalBreath = 0.18;
static const float AtlantisMatterIndependentBreath = 0.32;
static const float AtlantisMatterGlintBoost = 1.15;
static const float AtlantisMatterSizeMin = 0.52;
static const float AtlantisMatterSizeMax = 1.65;

struct AtlantisMatterField
{
    float3 flow;
    float radiance;
    float size;
    float glint;
};

float AtlantisMatterHash(float value)
{
    return frac(sin(value * 127.1 + 311.7) * 43758.5453);
}

AtlantisMatterField EvaluateAtlantisMatterField(float3 localPosition, float seed,
    float song, float authoredBeat, float contour)
{
    seed = frac(seed);
    float seedA = seed * 6.2831853;
    float slowTime = song * 0.19;
    float3 coherentFlow = float3(
        sin(localPosition.z * 0.010 + slowTime + seedA * 0.08),
        sin(localPosition.x * 0.009 - slowTime * 0.72 + seedA * 0.11),
        cos((localPosition.x + localPosition.z) * 0.006 + slowTime * 0.81 + seedA * 0.07));
    float3 individualFlow = float3(
        sin(slowTime * 1.31 + seedA),
        cos(slowTime * 1.17 + seedA * 1.37),
        sin(slowTime * 1.23 - seedA * 0.83));

    AtlantisMatterField field;
    float3 flow = coherentFlow * AtlantisMatterCoherentFlow +
        individualFlow * AtlantisMatterIndependentFlow;
    field.flow = flow * min(1.0, AtlantisMatterFlowMeters / max(length(flow), 0.0001)) *
        lerp(1.0, AtlantisMatterContourFlow, saturate(contour));

    float region = sin(localPosition.x * 0.008 + localPosition.z * 0.006 +
        song * 0.23 + authoredBeat * 0.035);
    float independent = sin(song * 0.71 + seedA * 1.91);
    float beatBreath = cos(authoredBeat * 1.5707963 + seedA * 0.13);
    field.radiance = 0.96 + region * AtlantisMatterRegionalBreath +
        independent * AtlantisMatterIndependentBreath * 0.5 + beatBreath * 0.025;

    float glintPhase = 0.5 + 0.5 * sin(song * 1.13 + seedA * 3.17 +
        authoredBeat * 0.045);
    float glintSelection = step(0.91, AtlantisMatterHash(seed + 47.0));
    field.glint = glintSelection * pow(saturate(glintPhase), 5.0);
    field.size = lerp(AtlantisMatterSizeMin, AtlantisMatterSizeMax,
        AtlantisMatterHash(seed + 113.0)) * (0.92 + independent * 0.08) + field.glint * 0.20;
    return field;
}

#endif
