#ifndef GRASS_SHADOWS_INCLUDED
#define GRASS_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "GrassCommon.hlsl"

StructuredBuffer<GrassData> _CulledInstancesBuffer;

float3 _LightDirection;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    uint instanceID   : SV_InstanceID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
};

float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
{
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
    
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    
    return positionCS;
}

Varyings vertShadow(Attributes input)
{
    Varyings output;

    GrassData instance = _CulledInstancesBuffer[input.instanceID];
    
    float3 positionWS = input.positionOS.xyz + instance.position;
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    
    output.positionCS = GetShadowPositionHClip(positionWS, normalWS);
    
    return output;
}

float4 fragShadow(Varyings input) : SV_Target
{
    return 0;
}

#endif