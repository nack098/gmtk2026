#ifndef GRASS_SHADOWS_INCLUDED
#define GRASS_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "GrassCommon.hlsl"

StructuredBuffer<GrassData> _DataBuffer;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    uint instanceID   : SV_InstanceID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
};

float3x3 GetInstanceTransformMatrixShadow(float3 targetNormal, float yawDegrees)
{
    float rad = yawDegrees * 0.0174532925;
    float s = sin(rad), c = cos(rad);
    float3x3 yawMatrix = float3x3(c, 0, s, 0, 1, 0, -s, 0, c);

    float3 up = normalize(targetNormal);
    float3 defaultUp = float3(0.0, 1.0, 0.0);

    if (abs(dot(up, defaultUp)) > 0.999) return yawMatrix;

    float3 axis = normalize(cross(defaultUp, up));
    float angle = acos(dot(defaultUp, up));
    float sa = sin(angle), ca = cos(angle);

    float3x3 alignMatrix = float3x3(
        ca + axis.x * axis.x * (1.0 - ca),          axis.x * axis.y * (1.0 - ca) - axis.z * sa, axis.x * axis.z * (1.0 - ca) + axis.y * sa,
        axis.y * axis.x * (1.0 - ca) + axis.z * sa, ca + axis.y * axis.y * (1.0 - ca),          axis.y * axis.z * (1.0 - ca) - axis.x * sa,
        axis.z * axis.x * (1.0 - ca) - axis.y * sa, axis.z * axis.y * (1.0 - ca) + axis.x * sa, ca + axis.z * axis.z * (1.0 - ca)
    );

    return mul(alignMatrix, yawMatrix);
}

Varyings vertShadow(Attributes input)
{
    Varyings output;

    GrassData instance = _DataBuffer[input.instanceID];
    
    float3 scaledOS = input.positionOS.xyz * float3(instance.scale.x, instance.scale.y, instance.scale.x);
    
    float3x3 rotMat = GetInstanceTransformMatrixShadow(instance.normal, instance.rotation.y);
    float3 rotatedOS = mul(rotMat, scaledOS);
    float3 positionWS = rotatedOS + instance.position;
    
    float3 normalWS = normalize(mul(rotMat, input.normalOS));
    
    float wave = sin(_Time.y * 1.5 + (positionWS.x + positionWS.z) * 0.3);
    positionWS += float3(wave * 0.08, 0.0, wave * 0.04) * input.uv.y;

    float3 lightDir = _MainLightPosition.xyz;
    if (length(lightDir) < 0.001) lightDir = float3(0.0, 1.0, 0.0);
    
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
    
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    
    output.positionCS = positionCS;
    return output;
}

float4 fragShadow(Varyings input) : SV_Target
{
    return 0;
}

#endif
