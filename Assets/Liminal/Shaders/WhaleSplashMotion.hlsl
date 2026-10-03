#ifndef LIMINAL_WHALE_SPLASH_MOTION
#define LIMINAL_WHALE_SPLASH_MOTION

float SplashRandom(uint value)
{
    value ^= value >> 16; value *= 0x7feb352du;
    value ^= value >> 15; value *= 0x846ca68bu;
    value ^= value >> 16;
    return (value & 0x00ffffffu) / 16777216.0;
}

float3 SplashTrajectory(uint id, float age, float scale, out float4 seed, out float3 velocity)
{
    seed = float4(SplashRandom(id * 7u + 11u), SplashRandom(id * 13u + 19u),
        SplashRandom(id * 17u + 31u), SplashRandom(id * 23u + 47u));
    float angle = seed.x * 6.2831853;
    float core = 1 - step(.45, seed.z);
    float layer = lerp(.82 + seed.w * .26, sqrt(seed.w) * .65, core);
    float launch = .04 + seed.y * .18;
    float t = max(0, age - launch);
    float lobe = .78 + .15 * sin(angle * 3 + .8) + .07 * cos(angle * 7);
    lobe = lerp(lobe, .92 + .06 * cos(angle * 2 + .4), core);
    float speedY = lerp(90, 185, pow(seed.y, .65)) * lobe * scale;
    float speedOut = lerp(27, 88, seed.y) * lerp(1, .36, core) * scale;
    float3 local = float3(cos(angle) * (34 * layer + speedOut * t),
        max(0, speedY * t - 52.5 * t * t), sin(angle) * (78 * layer + speedOut * t * .85));
    float asymmetry = .88 + .12 * sin(angle * 2 + .5);
    local.x *= asymmetry;
    local.z += t * 12;
    velocity = float3(cos(angle) * speedOut * asymmetry, speedY - 105 * t,
        sin(angle) * speedOut * .85 + 12);
    float returned = smoothstep(4.2, 5.8, t);
    float breakup = smoothstep(.45, 2.2, t);
    // Divergence-free trigonometric field: each component is independent of its own axis.
    float3 flow = float3(sin(local.z * .035 + t * 1.1) - cos(local.y * .041 + t * .7),
        sin(local.x * .039 - t * .9) - cos(local.z * .029 + t * .5),
        sin(local.y * .043 + t * .8) - cos(local.x * .031 - t * .6));
    local += flow * (breakup * (5 + seed.w * 10) * (1 - returned) * smoothstep(0, 22, local.y));
    local.y = max(0, local.y) * (1 - returned);
    return local;
}
#endif
