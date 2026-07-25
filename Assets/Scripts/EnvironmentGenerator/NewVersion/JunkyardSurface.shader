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

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            // Universal Render Pipeline Keywords for Shadows
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

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<ScrapInstanceData> _DataBuffer;
            #endif

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 positionWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
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

            void setup()
            {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                uint globalIndex = unity_InstanceID + _BaseInstanceOffset;
                ScrapInstanceData data = _DataBuffer[globalIndex];

                float2 scale = max(float2(1e-4, 1e-4), data.scale);
                float3 N = length(data.normal) > 0.1 ? normalize(data.normal) : float3(0, 1, 0);

                float3 groundPos = data.position + (N * (_PivotOffset * scale.y));
                float3x3 R = GetNormalRotationMatrix(N, data.rotation);

                unity_ObjectToWorld = float4x4(
                    R._m00 * scale.x, R._m01 * scale.y, R._m02 * scale.x, groundPos.x,
                    R._m10 * scale.x, R._m11 * scale.y, R._m12 * scale.x, groundPos.y,
                    R._m20 * scale.x, R._m21 * scale.y, R._m22 * scale.x, groundPos.z,
                    0,                 0,                 0,                 1.0
                );

                float3x3 invR = transpose(R);
                float invSx = 1.0 / scale.x;
                float invSy = 1.0 / scale.y;

                unity_WorldToObject = float4x4(
                    invR._m00 * invSx, invR._m01 * invSy, invR._m02 * invSx, 0,
                    invR._m10 * invSx, invR._m11 * invSy, invR._m12 * invSx, 0,
                    invR._m20 * invSx, invR._m21 * invSy, invR._m22 * invSx, 0,
                    0,                 0,                 0,                 1.0
                );

                unity_WorldToObject._14 = -dot(unity_WorldToObject._11_12_13, groundPos);
                unity_WorldToObject._24 = -dot(unity_WorldToObject._21_22_23, groundPos);
                unity_WorldToObject._34 = -dot(unity_WorldToObject._31_32_33, groundPos);
            #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    setup();
                #endif

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Get World Normals and View Direction
                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Get Main Light with Shadow Attenuation
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 L = normalize(mainLight.direction);

                // 1. Quantized Cel Diffuse Step
                float rawNdotL = dot(N, L);
                float halfLambert = rawNdotL * 0.5 + 0.5; // Map from [-1, 1] to [0, 1]
                
                // Combine light intensity with URP Shadow Map attenuation
                float lightIntensity = halfLambert * mainLight.shadowAttenuation;
                
                // Smoothstep transition for clean, anti-aliased cel bands
                float celStep = smoothstep(_StepThreshold - _StepSmoothing, _StepThreshold + _StepSmoothing, lightIntensity);
                
                half3 litColor = texColor.rgb * _BaseColor.rgb * mainLight.color;
                half3 shadowColor = litColor * _ShadowTint.rgb;
                half3 finalDiffuse = lerp(shadowColor, litColor, celStep);

                // 2. Stylized Cel Specular (Blinn-Phong Half-Vector)
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float specIntensity = pow(NdotH, 1.0 / max(0.001, _SpecularSize));
                float celSpecular = smoothstep(0.5 - _SpecularSmoothness, 0.5 + _SpecularSmoothness, specIntensity);
                half3 finalSpecular = celSpecular * _SpecularColor.rgb * mainLight.color * celStep;

                // 3. Crisp Rim Light
                float NdotV = 1.0 - saturate(dot(N, V));
                float rimIntensity = pow(NdotV, _RimPower) * celStep; // Only apply rim in lit areas
                float celRim = smoothstep(_RimThreshold - 0.05, _RimThreshold + 0.05, rimIntensity);
                half3 finalRim = celRim * _RimColor.rgb * mainLight.color;

                // Final Composition
                half3 finalColor = finalDiffuse + finalSpecular + finalRim;

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}