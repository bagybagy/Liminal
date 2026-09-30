#ifndef LIMINAL_WHALE_ANATOMY_INCLUDED
#define LIMINAL_WHALE_ANATOMY_INCLUDED

float WhaleSmooth(float a, float b, float x)
{
    float t = saturate((x - a) / (b - a));
    return t * t * (3.0 - 2.0 * t);
}

float WhaleProfile(float z, float a, float b, float r0, float r1, float d0, float d1)
{
    float t = saturate((z - a) / (b - a));
    float t2 = t * t, t3 = t2 * t;
    return (2.0 * t3 - 3.0 * t2 + 1.0) * r0 + (t3 - 2.0 * t2 + t) * (b - a) * d0
        + (-2.0 * t3 + 3.0 * t2) * r1 + (t3 - t2) * (b - a) * d1;
}

float2 WhaleRadius(float z)
{
    if (z <= -78.0 || z >= 76.0) return float2(0.0, 0.0);
    if (z < -62.0) return float2(
        WhaleProfile(z, -78.0, -62.0, 0.0, 4.3, 0.30, 0.10),
        WhaleProfile(z, -78.0, -62.0, 0.0, 4.2, 0.32, 0.10));
    if (z < -30.0) return float2(
        WhaleProfile(z, -62.0, -30.0, 4.3, 11.5, 0.10, 0.30),
        WhaleProfile(z, -62.0, -30.0, 4.2, 10.5, 0.10, 0.20));
    if (z < 8.0) return float2(
        WhaleProfile(z, -30.0, 8.0, 11.5, 19.0, 0.30, 0.10),
        WhaleProfile(z, -30.0, 8.0, 10.5, 15.8, 0.20, 0.06));
    if (z < 26.0) return float2(
        WhaleProfile(z, 8.0, 26.0, 19.0, 20.0, 0.10, 0.0),
        WhaleProfile(z, 8.0, 26.0, 15.8, 16.0, 0.06, 0.0));
    if (z < 42.0) return float2(20.0, WhaleProfile(z, 26.0, 42.0, 16.0, 14.0, 0.0, 0.0));
    float t = (z - 42.0) / 34.0;
    float cap = sqrt(max(0.0, 1.0 - t * t));
    return float2(20.0 * cap, 14.0 * cap);
}

float WhaleCenterY(float z)
{
    return 0.65 * WhaleSmooth(-70.0, -30.0, z) - 1.5 * WhaleSmooth(30.0, 70.0, z);
}

float3 WhaleBody(float z, float angle)
{
    float2 r = WhaleRadius(z);
    float s = sin(angle), c = cos(angle);
    float hump = 4.6 * exp(-(z + 29.0) * (z + 29.0) / 95.0) * pow(max(0.0, s), 12.0);
    float y = WhaleCenterY(z) + r.y * s * (1.0 - 0.10 * s) + hump;
    return float3(r.x * c, y, z);
}

float3 WhaleFlipper(float u, float angle, int side)
{
    u = saturate(u);
    float s = sin(angle), c = cos(angle);
    float end = sqrt(max(0.0, 1.0 - u * u * u * u));
    float arc = sin(u * 3.14159265359);
    float chord = (10.5 - 4.0 * u) * end;
    float thickness = (2.3 - 1.4 * u) * end;
    float scallop = 0.60 * arc * arc * cos(u * 3.14159265359 * 9.0) * pow(max(0.0, c), 8.0);
    return float3(side * (15.0 + 38.0 * u),
        -6.0 - 7.0 * u + 2.5 * arc + thickness * s + 0.65 * arc * c * c,
        18.0 - 36.0 * u + 6.0 * arc + chord * c + scallop);
}

float3 WhaleFluke(float u, float angle, int side)
{
    u = saturate(u);
    float s = sin(angle), c = cos(angle);
    float end = sqrt(max(0.0, 1.0 - u * u * u * u));
    float arc = sin(u * 3.14159265359);
    float chord = (3.5 + 7.0 * arc) * end;
    float thickness = (1.5 - 1.1 * u) * end;
    float scallop = 0.25 * arc * arc * cos(u * 3.14159265359 * 11.0) * pow(max(0.0, -c), 8.0);
    return float3(side * 30.0 * u, 0.35 + 1.8 * arc + thickness * s,
        -71.0 - 2.0 * u + chord * c + scallop);
}

// Keep constants, branches and arithmetic order identical to WhaleAnatomy.Deform.
float3 WhaleDeform(float3 p, float song)
{
    float3 form = p;
    float phase = song * 0.42;
    float tail = WhaleSmooth(-8.0, 78.0, -form.z);
    float wave = sin(phase - tail * 0.65);
    float pitch = 0.20 * tail * wave;
    float cp = cos(pitch), sp = sin(pitch);
    p.y = form.y * cp + 8.0 * tail * tail * wave;
    p.z = form.z - form.y * sp;
    p.x += 0.65 * tail * tail * sin(phase * 0.5 - tail * 0.4);
    float span = WhaleSmooth(17.0, 53.0, abs(form.x));
    float fin = span * WhaleSmooth(-54.0, -43.0, form.z) * (1.0 - WhaleSmooth(30.0, 40.0, form.z));
    float bank = sin(song * 0.16);
    float side = form.x < 0.0 ? -1.0 : 1.0;
    p.y += fin * (3.8 * sin(phase - 1.1) - side * 3.2 * bank);
    p.z += fin * 1.1 * cos(phase - 1.1);
    float roll = 0.035 * bank;
    float cr = cos(roll), sr = sin(roll);
    return float3(p.x * cr - p.y * sr, p.x * sr + p.y * cr, p.z);
}

int WhaleSurfaceFrame(float3 p, out float u, out float angle, out int side)
{
    side = p.x < 0.0 ? -1 : 1;
    float x = abs(p.x);
    float2 r = WhaleRadius(p.z);
    if (p.z < -59.0 && x > r.x + 0.2)
    {
        u = saturate(x / 30.0);
        float arc = sin(u * 3.14159265359);
        float end = sqrt(max(0.0001, 1.0 - u * u * u * u));
        angle = atan2((p.y - 0.35 - 1.8 * arc) / ((1.5 - 1.1 * u) * end),
            (p.z + 71.0 + 2.0 * u) / ((3.5 + 7.0 * arc) * end));
        return 2;
    }
    if (p.z > -54.0 && p.z < 32.0 && x > r.x + 0.2)
    {
        u = saturate((x - 15.0) / 38.0);
        float arc = sin(u * 3.14159265359);
        float end = sqrt(max(0.0001, 1.0 - u * u * u * u));
        float c = clamp((p.z - 18.0 + 36.0 * u - 6.0 * arc) / ((10.5 - 4.0 * u) * end), -1.0, 1.0);
        [unroll] for (int i = 0; i < 2; i++)
        {
            float scallop = 0.60 * arc * arc * cos(u * 3.14159265359 * 9.0) * pow(max(0.0, c), 8.0);
            c = clamp((p.z - 18.0 + 36.0 * u - 6.0 * arc - scallop) / ((10.5 - 4.0 * u) * end), -1.0, 1.0);
        }
        angle = atan2((p.y + 6.0 + 7.0 * u - 2.5 * arc - 0.65 * arc * c * c)
            / ((2.3 - 1.4 * u) * end), c);
        return 1;
    }
    u = p.z;
    float y = (p.y - WhaleCenterY(p.z)) / max(0.0001, r.y);
    float s = clamp(y, -1.0, 1.0);
    [unroll] for (int i = 0; i < 4; i++)
    {
        float k = 4.6 * exp(-(p.z + 29.0) * (p.z + 29.0) / 95.0) / max(0.0001, r.y);
        float positive = max(0.0, s);
        float f = s - 0.10 * s * s + k * pow(positive, 12.0) - y;
        float derivative = 1.0 - 0.20 * s + 12.0 * k * pow(positive, 11.0);
        s = clamp(s - f / derivative, -1.0, 1.0);
    }
    angle = atan2(s, p.x / max(0.0001, r.x));
    return 0;
}

void WhaleFrame(float3 p, out float3 along, out float3 around, out int surface, out int side)
{
    float u, angle;
    surface = WhaleSurfaceFrame(p, u, angle, side);
    if (surface == 0)
    {
        along = WhaleBody(min(75.999, u + 0.025), angle) - WhaleBody(max(-77.999, u - 0.025), angle);
        around = WhaleBody(u, angle + 0.002) - WhaleBody(u, angle - 0.002);
    }
    else if (surface == 1)
    {
        along = WhaleFlipper(u + 0.0005, angle, side) - WhaleFlipper(u - 0.0005, angle, side);
        around = WhaleFlipper(u, angle + 0.002, side) - WhaleFlipper(u, angle - 0.002, side);
    }
    else
    {
        along = WhaleFluke(u + 0.0005, angle, side) - WhaleFluke(u - 0.0005, angle, side);
        around = WhaleFluke(u, angle + 0.002, side) - WhaleFluke(u, angle - 0.002, side);
    }
}

float3 WhaleSurfaceNormal(float3 p)
{
    float3 along, around;
    int surface, side;
    WhaleFrame(p, along, around, surface, side);
    float3 n = surface == 0 ? cross(around, along) : cross(along, around) * side;
    return dot(n, n) > 0.0000000001 ? normalize(n) : float3(0.0, 1.0, 0.0);
}

float3 WhaleSurfaceTangent(float3 p)
{
    float3 along, around;
    int surface, side;
    WhaleFrame(p, along, around, surface, side);
    return dot(along, along) > 0.0000000001 ? normalize(along) : float3(0.0, 0.0, 1.0);
}

#endif
