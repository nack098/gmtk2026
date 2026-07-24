#ifndef GRASS_INCLUDED
#define GRASS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "GrassCommon.hlsl"

StructuredBuffer<GrassData> _DataBuffer;

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
};

Varyings vert(Attributes input) {
    Varyings output;
    GrassData instance = _DataBuffer[input.instanceID];
    float3 worldPos = input.positionOS.xyz + instance.position;
    
    float windSpeed = 2.0;
    float windStrength = 0.3;
    float windFrequency = 0.5;
    
    float wave = sin(_Time.y * windSpeed + (worldPos.x + worldPos.z) * windFrequency);
    float3 windOffset = float3(wave * windStrength, 0.0, wave * windStrength * 0.5) * input.uv.y;
    
    worldPos += windOffset;
    
    output.positionWS = worldPos; // FIX: Assigned positionWS!
    output.positionCS = TransformWorldToHClip(worldPos);
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    output.uv         = input.uv;
    
    return output;
}

float4 frag(Varyings input) : SV_Target {
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    
    float3 N = normalize(input.normalWS);
    float3 L = normalize(mainLight.direction);
    float NdotL = saturate(dot(N, L));
    
    float3 lightColor = mainLight.color * (NdotL * mainLight.shadowAttenuation);
    
    float3 rootColor = float3(0.05, 0.25, 0.05);
    float3 tipColor  = float3(0.3, 0.8, 0.2);
    float3 grassColor = lerp(rootColor, tipColor, input.uv.y);
    
    float3 ambient = float3(0.1, 0.15, 0.1) * grassColor;
    float3 finalColor = ambient + (grassColor * lightColor);
    
    return float4(finalColor, 1.0);
}

#endif