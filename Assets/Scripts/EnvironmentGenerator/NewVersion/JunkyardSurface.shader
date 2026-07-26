Shader "Custom/GpuJunkInstancedShader_WebGPU"
{
    Properties
    {
        [Header(Base Texture)]
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)

        [Header(Cel Lighting)]
        _ShadowTint ("Shadow Tint Color", Color) = (0.15, 0.15, 0.25, 1.0)
        _StepThreshold ("Shadow Step Threshold", Range(0, 1)) = 0.35
        _StepSmoothing ("Step Smoothness/Feather", Range(0.001, 0.2)) = 0.02
        
        [Header(Specular Highlight)]
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize ("Specular Size", Range(0.01, 0.5)) = 0.05
        _SpecularSmoothness ("Specular Feather", Range(0.001, 0.1)) = 0.01

        [Header(Rim Lighting)]
        _RimColor ("Rim Light Color", Color) = (0.8, 0.8, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimThreshold ("Rim Cutoff", Range(0, 1)) = 0.5

        [Header(Transform Adjustments)]
        _PivotOffset ("Pivot Ground Offset", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // =================================================================
        // PASS 1: FORWARD LIT (CEL SHADED)
        // =================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off 

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct ScrapInstanceData
            {
                float id;
                float3 rotation;  // Pitch, Yaw, Roll
                float2 scale;     
                float3 position;  
                float3 normal;    
                float pad;
            };

            StructuredBuffer<ScrapInstanceData> _DataBuffer;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                uint instanceID     : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half _StepThreshold;
                half _StepSmoothing;

                half4 _SpecularColor;
                half _SpecularSize;
                half _SpecularSmoothness;

                half4 _RimColor;
                half _RimPower;
                half _RimThreshold;

                float _PivotOffset;
                uint _BaseInstanceOffset;
            CBUFFER_END

            float3x3 GetNormalRotationMatrix(float3 N, float3 rotEuler)
            {
                float3 up = normalize(N);
                float3 approxForward = abs(up.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 right = normalize(cross(approxForward, up));
                float3 forward = cross(up, right);

                float3 rad = rotEuler * 0.01745329251;
                
                float cx = cos(rad.x), sx = sin(rad.x); 
                float cy = cos(rad.y), sy = sin(rad.y); 
                float cz = cos(rad.z), sz = sin(rad.z); 

                float3 localR = float3(cy * cz, cy * sz, -sy);
                float3 localU = float3(sx * sy * cz - cx * sz, sx * sy * sz + cx * cz, sx * cy);
                float3 localF = float3(cx * sy * cz + sx * sz, cx * sy * sz - sx * cz, cx * cy);

                float3 worldRight   = right * localR.x + up * localR.y + forward * localR.z;
                float3 worldUp      = right * localU.x + up * localU.y + forward * localU.z;
                float3 worldForward = right * localF.x + up * localF.y + forward * localF.z;

                return float3x3(
                    worldRight.x, worldUp.x, worldForward.x,
                    worldRight.y, worldUp.y, worldForward.y,
                    worldRight.z, worldUp.z, worldForward.z
                );
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                uint globalIndex = input.instanceID + _BaseInstanceOffset;
                ScrapInstanceData data = _DataBuffer[globalIndex];

                float2 scale = max(float2(1e-4, 1e-4), data.scale);
                float3 N = length(data.normal) > 0.1 ? normalize(data.normal) : float3(0, 1, 0);

                float3x3 R = GetNormalRotationMatrix(N, data.rotation);
                float3 scaledOS = input.positionOS.xyz * float3(scale.x, scale.y, scale.x);
                float3 rotatedOS = mul(R, scaledOS);
                
                float3 groundPos = data.position + (N * (_PivotOffset * scale.y));
                float3 worldPos = rotatedOS + groundPos;

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = normalize(mul(R, input.normalOS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 L = normalize(mainLight.direction);

                // Cel Diffuse Step
                float rawNdotL = dot(N, L);
                float halfLambert = rawNdotL * 0.5 + 0.5;
                float lightIntensity = halfLambert * mainLight.shadowAttenuation;
                float celStep = smoothstep(_StepThreshold - _StepSmoothing, _StepThreshold + _StepSmoothing, lightIntensity);
                
                half3 litColor = texColor.rgb * _BaseColor.rgb * mainLight.color;
                half3 shadowColor = litColor * _ShadowTint.rgb;
                half3 finalDiffuse = lerp(shadowColor, litColor, celStep);

                // Cel Specular
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float specIntensity = pow(NdotH, 1.0 / max(0.001, _SpecularSize));
                float celSpecular = smoothstep(0.5 - _SpecularSmoothness, 0.5 + _SpecularSmoothness, specIntensity);
                half3 finalSpecular = celSpecular * _SpecularColor.rgb * mainLight.color * celStep;

                // Cel Rim Light
                float NdotV = 1.0 - saturate(dot(N, V));
                float rimIntensity = pow(NdotV, _RimPower) * celStep;
                float celRim = smoothstep(_RimThreshold - 0.05, _RimThreshold + 0.05, rimIntensity);
                half3 finalRim = celRim * _RimColor.rgb * mainLight.color;

                half3 finalColor = finalDiffuse + finalSpecular + finalRim;
                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 2: SHADOW CASTER
        // =================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct ScrapInstanceData
            {
                float id;
                float3 rotation;
                float2 scale;     
                float3 position;  
                float3 normal;    
                float pad;
            };

            StructuredBuffer<ScrapInstanceData> _DataBuffer;

            CBUFFER_START(UnityPerMaterial)
                float _PivotOffset;
                uint _BaseInstanceOffset;
            CBUFFER_END

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

            float3x3 GetNormalRotationMatrix(float3 N, float3 rotEuler)
            {
                float3 up = normalize(N);
                float3 approxForward = abs(up.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 right = normalize(cross(approxForward, up));
                float3 forward = cross(up, right);

                float3 rad = rotEuler * 0.01745329251;
                float cx = cos(rad.x), sx = sin(rad.x); 
                float cy = cos(rad.y), sy = sin(rad.y); 
                float cz = cos(rad.z), sz = sin(rad.z); 

                float3 localR = float3(cy * cz, cy * sz, -sy);
                float3 localU = float3(sx * sy * cz - cx * sz, sx * sy * sz + cx * cz, sx * cy);
                float3 localF = float3(cx * sy * cz + sx * sz, cx * sy * sz - sx * cz, cx * cy);

                return float3x3(
                    right.x * localR.x + up.x * localR.y + forward.x * localR.z, right.x * localU.x + up.x * localU.y + forward.x * localU.z, right.x * localF.x + up.x * localF.y + forward.x * localF.z,
                    right.y * localR.x + up.y * localR.y + forward.y * localR.z, right.y * localU.x + up.y * localU.y + forward.y * localU.z, right.y * localF.x + up.y * localF.y + forward.y * localF.z,
                    right.z * localR.x + up.z * localR.y + forward.z * localR.z, right.z * localU.x + up.z * localU.y + forward.z * localU.z, right.z * localF.x + up.z * localF.y + forward.z * localF.z
                );
            }

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                uint globalIndex = input.instanceID + _BaseInstanceOffset;
                ScrapInstanceData data = _DataBuffer[globalIndex];

                float2 scale = max(float2(1e-4, 1e-4), data.scale);
                float3 N = length(data.normal) > 0.1 ? normalize(data.normal) : float3(0, 1, 0);

                float3x3 R = GetNormalRotationMatrix(N, data.rotation);
                float3 scaledOS = input.positionOS.xyz * float3(scale.x, scale.y, scale.x);
                float3 rotatedOS = mul(R, scaledOS);
                
                float3 groundPos = data.position + (N * (_PivotOffset * scale.y));
                float3 worldPos = rotatedOS + groundPos;
                float3 normalWS = normalize(mul(R, input.normalOS));

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
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}