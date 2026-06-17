#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _SHADOWS_SOFT

#ifndef GETMAINLIGHT_INCLUDED
#define GETMAINLIGHT_INCLUDED

void GetMainLightData_float(float3 WorldPos, out float3 Direction, out float3 Color, out float ShadowAtten)
{
#if defined(SHADERGRAPH_PREVIEW) || !defined(UNIVERSAL_LIGHTING_INCLUDED)
    Direction   = float3(0.5, 0.5, 0);
    Color       = float3(1, 1, 1);
    ShadowAtten = 1.0;
#else
    #if defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    #else
        float4 shadowCoord = float4(0, 0, 0, 0);
    #endif

    Light mainLight = GetMainLight(shadowCoord);
    Direction   = mainLight.direction;
    Color       = mainLight.color;
    ShadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
#endif
}

#endif
