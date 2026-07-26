Shader "Custom/TerrainTriplanar"
{
    Properties
    {
        [Header(Heightmap Displacement)]
        _HeightMap ("Heightmap Texture (RFloat)", 2D) = "black" {}
        _HeightScale ("Height Scale", Float) = 15.0

        [Header(Base Tint)]
        _BaseColor ("Base Tint Color", Color) = (0.6, 0.55, 0.5, 1.0)

        [Header(Top Texture (Flat Surface))]
        _TopTex ("Top Albedo (RGB)", 2D) = "white" {}
        _TopScale ("Top Texture Scale", Float) = 0.1

        [Header(Side Texture (Cliffs))]
        _SideTex ("Side Albedo (RGB)", 2D) = "white" {}
        _SideScale ("Side Texture Scale", Float) = 0.1

        [Header(Trash Normal Perturbation)]
        _BumpMap ("Debris Normal Map (AG / Unpacked)", 2D) = "bump" {}
        _BumpScale ("Debris Surface Roughness/Strength", Range(0.0, 3.0)) = 1.2

        [Header(Trash Heap Stylization)]
        _BlendSharpness ("Cliff Blend Sharpness", Range(1.0, 16.0)) = 6.0
        _BlockSize ("Voxel / Grid Block Size", Range(0.001, 5.0)) = 0.05
        _RandomTilt ("Random Facet Noise Intensity", Range(0.0, 2.0)) = 0.8

        [Header(Cel Shading Controls)]
        _ShadowColor ("Shadow Color Tint", Color) = (0.2, 0.25, 0.35, 1.0)
        _CelThreshold ("Cel Shadow Threshold", Range(0.0, 1.0)) = 0.35
        _CelSmoothing ("Cel Shadow Anti-Aliasing Edge", Range(0.001, 0.2)) = 0.02
        _SpecularThreshold ("Toon Specular Threshold", Range(0.5, 1.0)) = 0.95
        _SpecularColor ("Toon Specular Color", Color) = (0.8, 0.8, 0.8, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // =================================================================
        // PASS 1: FORWARD LIT (Color + Cel Shading)
        // =================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);
            TEXTURE2D(_TopTex);    SAMPLER(sampler_TopTex);
            TEXTURE2D(_SideTex);   SAMPLER(sampler_SideTex);
            TEXTURE2D(_BumpMap);   SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _HeightMap_TexelSize;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _SpecularColor;
                float _HeightScale;
                float _TopScale;
                float _SideScale;
                float _BumpScale;
                float _BlendSharpness;
                float _BlockSize;
                float _RandomTilt;
                float _CelThreshold;
                float _CelSmoothing;
                float _SpecularThreshold;
            CBUFFER_END

            // Fast 3D Pseudo-Random Noise Generator
            float3 Hash33(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yzx + 19.19);
                return frac((p.xxy + p.yzz) * p.zyx) * 2.0 - 1.0;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 1. Sample Heightmap at Mesh UVs (0..1)
                float height = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, input.uv, 0).r;
                
                // 2. Displace Vertex Position along local Y axis
                float3 displacedPosOS = input.positionOS.xyz;
                displacedPosOS.y += height * _HeightScale;

                // 3. Compute World Space Coordinates
                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedPosOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 worldPos = input.positionWS;

                // 1. Quantized Coordinates for Voxel/Block UV Lookups
                float3 blockyWorldPos = floor(worldPos / _BlockSize) * _BlockSize;

                // 2. Flat Triangle Face Normal (Recomputed dynamically from displaced positions!)
                float3 dX = ddx(worldPos);
                float3 dY = ddy(worldPos);
                float3 flatNormal = normalize(cross(dY, dX));

                // 3. Add Facet Block Tilt
                float3 cellRandom = Hash33(blockyWorldPos);
                float3 baseChaoticNormal = normalize(flatNormal + cellRandom * _RandomTilt);

                // 4. Triplanar Blend Weights based on Base Normal
                float3 blendWeights = pow(abs(baseChaoticNormal), _BlendSharpness);
                blendWeights /= max(blendWeights.x + blendWeights.y + blendWeights.z, 0.0001);

                // 5. Sample and Blend Normal Maps across 3 Axes
                float3 bumpX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, blockyWorldPos.zy * _SideScale), _BumpScale);
                float3 bumpY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, blockyWorldPos.xz * _TopScale), _BumpScale);
                float3 bumpZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, blockyWorldPos.xy * _SideScale), _BumpScale);

                float3 worldBumpX = float3(0.0, bumpX.y, bumpX.x);
                float3 worldBumpY = float3(bumpY.x, 0.0, bumpY.y);
                float3 worldBumpZ = float3(bumpZ.x, bumpZ.y, 0.0);

                float3 N = normalize(baseChaoticNormal + 
                                     worldBumpX * blendWeights.x + 
                                     worldBumpY * blendWeights.y + 
                                     worldBumpZ * blendWeights.z);

                // 6. Sample Albedo Textures
                float3 xProjection = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, blockyWorldPos.zy * _SideScale).rgb;
                float3 zProjection = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, blockyWorldPos.xy * _SideScale).rgb;
                float3 yProjection = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, blockyWorldPos.xz * _TopScale).rgb;

                float3 blendedAlbedo = (xProjection * blendWeights.x + 
                                       yProjection * blendWeights.y + 
                                       zProjection * blendWeights.z) * _BaseColor.rgb;

                // 7. CEL SHADING CALCULATION
                Light mainLight = GetMainLight();
                float rawNdotL = dot(N, mainLight.direction);
                
                // Remap NdotL into 0..1 range and apply quantized smoothstep transition for cel bands
                float NdotL = saturate(rawNdotL * 0.5 + 0.5); 
                float celRamp = smoothstep(_CelThreshold - _CelSmoothing, _CelThreshold + _CelSmoothing, NdotL);

                // Blend between Shadow Color and Lit Color based on Cel Ramp
                float3 celDiffuse = lerp(_ShadowColor.rgb, mainLight.color, celRamp);

                // Sharp Toon Specular Highlight
                float3 V = GetWorldSpaceNormalizeViewDir(worldPos);
                float3 H = normalize(mainLight.direction + V);
                float NdotH = saturate(dot(N, H));
                float toonSpecular = smoothstep(_SpecularThreshold - 0.01, _SpecularThreshold + 0.01, NdotH) * celRamp;

                // Combine Ambient SH + Cel Lighting + Toon Specular
                float3 finalLighting = (SampleSH(N) + celDiffuse) * blendedAlbedo + (toonSpecular * _SpecularColor.rgb);

                return float4(finalLighting, 1.0);
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 2: DEPTH ONLY (CRITICAL FOR Hi-Z CULLING PREPASS!)
        // =================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                float _HeightScale;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // Displace vertex in depth pass so _CameraDepthTexture matches full 3D terrain!
                float height = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, input.uv, 0).r;
                float3 displacedPosOS = input.positionOS.xyz;
                displacedPosOS.y += height * _HeightScale;

                output.positionCS = TransformObjectToHClip(displacedPosOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 3: SHADOW CASTER (Unity 6 URP Compatible)
        // =================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                float _HeightScale;
            CBUFFER_END

            float3 _LightDirection;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 1. Displace vertex height for shadow mapping
                float height = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, input.uv, 0).r;
                float3 displacedPosOS = input.positionOS.xyz;
                displacedPosOS.y += height * _HeightScale;

                // 2. Transform position & normal to World Space
                float3 positionWS = TransformObjectToWorld(displacedPosOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // 3. Apply URP shadow bias and project to Clip Space
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}