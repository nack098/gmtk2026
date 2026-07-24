Shader "Custom/TerrainTriplanar"
{
    Properties
    {
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
        // Extended Range to allow ultra-small micro-blocks down to 0.001!
        _BlockSize ("Voxel / Grid Block Size", Range(0.001, 5.0)) = 0.05
        _RandomTilt ("Random Facet Noise Intensity", Range(0.0, 2.0)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            TEXTURE2D(_TopTex);  SAMPLER(sampler_TopTex);
            TEXTURE2D(_SideTex); SAMPLER(sampler_SideTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _TopScale;
                float _SideScale;
                float _BumpScale;
                float _BlendSharpness;
                float _BlockSize;
                float _RandomTilt;
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
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 worldPos = input.positionWS;

                // 1. Quantized Coordinates for Voxel/Block UV Lookups
                float3 blockyWorldPos = floor(worldPos / _BlockSize) * _BlockSize;

                // 2. Flat Triangle Face Normal
                float3 dX = ddx(worldPos);
                float3 dY = ddy(worldPos);
                float3 flatNormal = normalize(cross(dY, dX));

                // 3. Add Facet Block Tilt
                float3 cellRandom = Hash33(blockyWorldPos);
                float3 baseChaoticNormal = normalize(flatNormal + cellRandom * _RandomTilt);

                // 4. Triplanar Blend Weights based on Base Normal
                float3 blendWeights = pow(abs(baseChaoticNormal), _BlendSharpness);
                blendWeights /= max(blendWeights.x + blendWeights.y + blendWeights.z, 0.0001);

                // 5. Sample and Blend Normal Maps across 3 Axes (Triplanar Normal Mapping)
                float3 bumpX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, blockyWorldPos.zy * _SideScale), _BumpScale);
                float3 bumpY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, blockyWorldPos.xz * _TopScale), _BumpScale);
                float3 bumpZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, blockyWorldPos.xy * _SideScale), _BumpScale);

                // Reconstruct World Space Tangent Normals for each axis projection
                float3 worldBumpX = float3(0.0, bumpX.y, bumpX.x);
                float3 worldBumpY = float3(bumpY.x, 0.0, bumpY.y);
                float3 worldBumpZ = float3(bumpZ.x, bumpZ.y, 0.0);

                // Combine normal map offsets with base chaotic facet normal
                float3 perturbedNormal = normalize(baseChaoticNormal + 
                                                   worldBumpX * blendWeights.x + 
                                                   worldBumpY * blendWeights.y + 
                                                   worldBumpZ * blendWeights.z);

                float3 N = perturbedNormal;

                // 6. Sample Albedo Textures
                float3 xProjection = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, blockyWorldPos.zy * _SideScale).rgb;
                float3 zProjection = SAMPLE_TEXTURE2D(_SideTex, sampler_SideTex, blockyWorldPos.xy * _SideScale).rgb;
                float3 yProjection = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, blockyWorldPos.xz * _TopScale).rgb;

                float3 blendedAlbedo = (xProjection * blendWeights.x + 
                                       yProjection * blendWeights.y + 
                                       zProjection * blendWeights.z) * _BaseColor.rgb;

                // 7. Lighting with Perturbed Trash Normals
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(N, mainLight.direction));
                float3 lighting = mainLight.color * NdotL + SampleSH(N);

                return float4(blendedAlbedo * lighting, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Diffuse"
}