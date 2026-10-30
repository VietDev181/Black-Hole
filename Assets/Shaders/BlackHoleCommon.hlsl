#ifndef BLACKHOLE_COMMON_INCLUDED
#define BLACKHOLE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Globals pushed every frame / every level by BHShader.cs
float4 _BH_Hole;        // xyz = hole centre, w = radius
float4 _BH_LightDir;    // xyz = direction towards the light
float4 _BH_LightColor;
float4 _BH_AmbSky;
float4 _BH_AmbGround;
float4 _BH_RimColor;
float4 _BH_HoleColor;   // accent colour of the hole
float4 _BH_HoleColor2;  // hot colour of the hole
float4 _BH_FogColor;
float4 _BH_Fog;         // x = near, y = far, z = strength
float4 _BH_Map;         // x = half extent X, y = half extent Z
float4 _BH_Border;

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float Noise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++)
    {
        v += a * Noise2(p);
        p = p * 2.02 + 7.1;
        a *= 0.5;
    }
    return v;
}

float2 Rot2(float2 v, float a)
{
    float s, c;
    sincos(a, s, c);
    return float2(c * v.x - s * v.y, s * v.x + c * v.y);
}

half3 ApplyFog(half3 col, float3 wp)
{
    float d = distance(wp, _WorldSpaceCameraPos);
    float f = saturate((d - _BH_Fog.x) / max(_BH_Fog.y - _BH_Fog.x, 0.01));
    return lerp(col, _BH_FogColor.rgb, f * f * _BH_Fog.z);
}

#endif
