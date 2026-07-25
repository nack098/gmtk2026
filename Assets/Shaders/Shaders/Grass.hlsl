#ifndef GRASS_INCLUDED
#define GRASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassCommon.hlsl"

StructuredBuffer<GrassData> _DataBuffer;

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _TipColor;
    
    // Toggle & Adjustments for Mesh vs. Plane
    float _IsPlaneMesh;       // Set to 1.0 if using Unity's default Plane/Quad, 0.0 for 3D Mesh
    float _MeshHeight;        // Total unscaled height of the mesh (default ~1.0)
    float _NormalNormalBlend; // Blend between actual mesh normals and aligned up normals (0.0 to 1.0)
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

// Construct Alignment Matrix from Surface Normal + Yaw
float3x3 GetInstanceTransformMatrix(float3 targetNormal, float yawDegrees)
{
    float rad = yawDegrees * 0.0174532925;
    float s = sin(rad);
    float c = cos(rad);
    float3x3 yawMatrix = float3x3(
        c, 0, s,
        0, 1, 0,
       -s, 0, c
    );

    float3 up = normalize(targetNormal);
    float3 defaultUp = float3(0.0, 1.0, 0.0);

    if (abs(dot(up, defaultUp)) > 0.999)
    {
        return yawMatrix;
    }

    float3 axis = normalize(cross(defaultUp, up));
    float angle = acos(dot(defaultUp, up));
    float sa = sin(angle);
    float ca = cos(angle);

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
    
    // 1. Pivot Offset Handling (Planes have centered pivots, 3D meshes are usually base-pivoted)
    float3 posOS = input.positionOS.xyz;
    if (_IsPlaneMesh > 0.5)
    {
        // Shift centered quad/plane upward so the bottom rests on ground (Y = 0)
        posOS.y += 0.5; 
    }
    
    // 2. Scale object-space mesh
    float3 scaledOS = posOS * float3(instance.scale.x, instance.scale.y, instance.scale.x);
    
    // 3. Transform by surface normal & yaw
    float3x3 rotMat = GetInstanceTransformMatrix(instance.normal, instance.rotation.y);
    float3 rotatedOS = mul(rotMat, scaledOS);
    
    // 4. Base World Position
    float3 worldPos = rotatedOS + instance.position;
    
    // 5. Smart Wind Masking (Uses Object-Space Height instead of unreliable UVs!)
    float meshHeight = max(0.001, _MeshHeight);
    float heightFactor = saturate(posOS.y / meshHeight); // 0.0 at root, 1.0 at tip
    
    float windSpeed = 0.5;
    float windStrength = 0.05;
    float windFrequency = 0.3;
    
    float wave = sin(_Time.y * windSpeed + (worldPos.x + worldPos.z) * windFrequency);
    float3 windOffset = float3(wave * windStrength, 0.0, wave * windStrength * 0.5) * heightFactor;
    
    worldPos += windOffset;
    
    output.positionWS = worldPos;
    output.positionCS = TransformWorldToHClip(worldPos);
    
    // 6. Normal Blending for Lush Shading (Blends geometric normals with up-vectors)
    float3 realMeshNormal = normalize(mul(rotMat, input.normalOS));
    float3 upTerrainNormal = instance.normal;
    
    // Blend real mesh normals with top normal so 3D geometry doesn't look dark or patchy
    output.normalWS = normalize(lerp(upTerrainNormal, realMeshNormal, saturate(_NormalNormalBlend)));
    
    output.uv = input.uv;
    
    // Per-blade subtle variation
    output.variation = 0.85 + 0.3 * frac(instance.rotation.y * 0.01);
    
    return output;
}

float4 frag(Varyings input) : SV_Target {
    float3 rootColor = (length(_BaseColor.rgb) > 0.001) ? _BaseColor.rgb : float3(0.08, 0.3, 0.08);
    float3 tipColor  = (length(_TipColor.rgb)  > 0.001) ? _TipColor.rgb  : float3(0.4,  0.85, 0.25);
    
    // Gradient blending using UV Y
    float3 grassColor = lerp(rootColor, tipColor, saturate(input.uv.y));
    grassColor *= input.variation;
    
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    
    float3 N = normalize(input.normalWS);
    float3 L = normalize(mainLight.direction);
    
    // Soft Half-Lambert Shading for foliage
    float NdotL = saturate(dot(N, L) * 0.5 + 0.5);
    float3 lightColor = mainLight.color * (NdotL * mainLight.shadowAttenuation);
    
    float3 ambient = float3(0.2, 0.25, 0.2) * grassColor;
    float3 finalColor = ambient + (grassColor * lightColor);
    
    return float4(finalColor, 1.0);
}

#endif
