#ifndef GRASS_SHADOWS_INCLUDED
#define GRASS_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassCommon.hlsl"

StructuredBuffer<GrassData> _DataBuffer;

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _TipColor;
    float _IsPlaneMesh;
    float _MeshHeight;
    float _NormalNormalBlend;
CBUFFER_END

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
    
    // 1. Pivot Offset Handling (Plane Shift)
    float3 posOS = input.positionOS.xyz;
    if (_IsPlaneMesh > 0.5)
    {
        posOS.y += 0.5; 
    }

    // 2. Scale & Surface Alignment
    float3 scaledOS = posOS * float3(instance.scale.x, instance.scale.y, instance.scale.x);
    float3x3 rotMat = GetInstanceTransformMatrixShadow(instance.normal, instance.rotation.y);
    float3 rotatedOS = mul(rotMat, scaledOS);
    float3 worldPos = rotatedOS + instance.position;

    // 3. Identical Wind Calculation as Main Pass
    float meshHeight = max(0.001, _MeshHeight);
    float heightFactor = saturate(posOS.y / meshHeight);
    
    float wave = sin(_Time.y * 0.5 + (worldPos.x + worldPos.z) * 0.3);
    float3 windOffset = float3(wave * 0.05, 0.0, wave * 0.025) * heightFactor;
    worldPos += windOffset;

    // 4. Transform to Shadow Clip Space (Fixed cross-version URP Shadow Bias)
    float3 normalWS = normalize(mul(rotMat, input.normalOS));
    
    Light mainLight = GetMainLight();
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, normalWS, mainLight.direction));

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