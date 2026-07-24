Shader "Custom/StylizedPostProcess"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source Texture", 2D) = "white" {}
        
        [Header(Bilateral Surface Blur)]
        _SpatialSigma ("Spatial Blur Sigma", Range(0.1, 5.0)) = 1.0
        _ColorSigma ("Color Edge Threshold (Sigma)", Range(0.01, 1.0)) = 0.15
        _DepthSigma ("Depth Boundary Sharpness (Sigma)", Range(0.001, 0.1)) = 0.01
        
        [Header(Bloom Threshold)]
        _BloomThreshold ("Bloom Threshold (Luminance)", Range(0.0, 1.0)) = 0.5
        _BloomIntensity ("Bloom Intensity", Range(0.0, 5.0)) = 1.5

        [Header(Monochrome and Tinting)]
        _Saturation ("Saturation", Range(0.0, 1.0)) = 1.0
        _ColorTint ("Color Tint", Color) = (1, 1, 1, 1)

        [Header(Vignette)]
        _VignetteIntensity ("Vignette Intensity", Range(0.0, 2.0)) = 0.5
        _VignetteSmoothness ("Vignette Smoothness", Range(0.01, 1.0)) = 0.4

        [Header(Color Balance)]
        _Shadows ("Shadows Tint", Color) = (1, 1, 1, 1)
        _Midtones ("Midtones Tint", Color) = (1, 1, 1, 1)
        _Highlights ("Highlights Tint", Color) = (1, 1, 1, 1)

        [Header(Color Correction)]
        _Exposure ("Exposure", Range(-3.0, 3.0)) = 0.0
        _Contrast ("Contrast", Range(0.5, 2.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Cull Off 
        ZWrite Off 
        ZTest Always

        Pass
        {
            Name "StylizedBilateralPostProcessPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // URP Core Headers & Blit Utilities
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Includes CBUFFER and bilateral functions in correct order
            #include "BilateralBloom.hlsl"

            float4 Frag(Varyings input) : SV_Target
            {
                float3 blurredColor = FragSurfaceBlur(input).rgb;
                return float4(blurredColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}