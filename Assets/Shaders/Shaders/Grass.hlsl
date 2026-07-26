#ifndef GRASS_INCLUDED
#define GRASS_INCLUDED

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

struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    uint instanceID   : SV_InstanceID;
};

struct Varyings {
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float2 uv         : TEXCOORD2;
    float  variation  : TEXCOORD3;
};

float3x3 GetInstanceTransformMatrix(float3 targetNormal, float yawDegrees)
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

Varyings vert(Attributes input) {
    Varyings output;
    
    GrassData instance = _DataBuffer[input.instanceID];
    
    // 1. Pivot Offset Handling
    float3 posOS = input.positionOS.xyz;
    if (_IsPlaneMesh > 0.5)
    {
        posOS.y += 0.5; 
    }
    
    // 2. Scale & Surface Alignment
    float3 scaledOS = posOS * float3(instance.scale.x, instance.scale.y, instance.scale.x);
    float3x3 rotMat = GetInstanceTransformMatrix(instance.normal, instance.rotation.y);
    float3 rotatedOS = mul(rotMat, scaledOS);
    
    float3 worldPos = rotatedOS + instance.position;
    
    // 3. Object-Space Wind Displacement
    float meshHeight = max(0.001, _MeshHeight);
    float heightFactor = saturate(posOS.y / meshHeight);
    
    float wave = sin(_Time.y * 0.5 + (worldPos.x + worldPos.z) * 0.3);
    float3 windOffset = float3(wave * 0.05, 0.0, wave * 0.025) * heightFactor;
    
    worldPos += windOffset;
    
    output.positionWS = worldPos;
    output.positionCS = TransformWorldToHClip(worldPos);
    
    // 4. Normal Blending
    float3 realMeshNormal = normalize(mul(rotMat, input.normalOS));
    float3 upTerrainNormal = instance.normal;
    output.normalWS = normalize(lerp(upTerrainNormal, realMeshNormal, saturate(_NormalNormalBlend)));
    
    output.uv = input.uv;
    output.variation = 0.85 + 0.3 * frac(instance.rotation.y * 0.01);
    
    return output;
}

float4 frag(Varyings input) : SV_Target {
    float3 rootColor = (length(_BaseColor.rgb) > 0.001) ? _BaseColor.rgb : float3(0.08, 0.3, 0.08);
    float3 tipColor  = (length(_TipColor.rgb)  > 0.001) ? _TipColor.rgb  : float3(0.4,  0.85, 0.25);
    
    float3 grassColor = lerp(rootColor, tipColor, saturate(input.uv.y)) * input.variation;
    
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    
    float3 N = normalize(input.normalWS);
    float3 L = normalize(mainLight.direction);
    
    float NdotL = saturate(dot(N, L) * 0.5 + 0.5);
    float3 lightColor = mainLight.color * (NdotL * mainLight.shadowAttenuation);
    
    float3 ambient = float3(0.2, 0.25, 0.2) * grassColor;
    float3 finalColor = ambient + (grassColor * lightColor);
    
    return float4(finalColor, 1.0);
}

#endif