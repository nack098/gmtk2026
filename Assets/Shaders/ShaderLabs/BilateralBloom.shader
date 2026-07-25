Shader "Custom/StylizedPostProcess"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source Texture", 2D) = "white" {}
        
        [Header(Ambient Occlusion)]
        _AORadius ("AO Radius", Range(0.01, 5.0)) = 0.5
        _AOIntensity ("AO Intensity", Range(0.0, 5.0)) = 2.0
        _AOBias ("AO Bias", Range(0.0001, 0.1)) = 0.01

        [Header(Subsurface Scattering)]
        _SSSColor ("SSS Scatter Color", Color) = (1.0, 0.4, 0.25, 1.0)
        _SSSIntensity ("SSS Intensity", Range(0.0, 5.0)) = 1.2
        _SSSRadius ("SSS Blur Radius", Range(0.1, 10.0)) = 1.5

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

        [Header(Air Dust Atmospheric Fog)]
        _AirDustColor ("Air Dust Fog Color", Color) = (0.1, 0.12, 0.14, 1.0)
        _AirDustDensity ("Air Dust Density", Range(0.0, 0.1)) = 0.02
        _AirDustStartDistance ("Start Distance (Meters)", Range(0.0, 50.0)) = 2.0
        _AirDustNoiseScale ("Dust Drift Noise", Range(0.0, 1.0)) = 0.15

        [Header(Drifting Animated Fog)]
        _AnimatedFogColor ("Fog Color", Color) = (0.8, 0.85, 0.9, 1.0)
        _AnimatedFogSpeed ("Fog Drift Speed (XY)", Vector) = (0.03, 0.01, 0, 0)
        _AnimatedFogIntensity ("Animated Fog Intensity", Range(0.0, 2.0)) = 0.5
        _AnimatedFogScale ("Fog Scale", Range(0.5, 10.0)) = 2.5

        [Header(Lens Dirt and Glass Smudge)]
        _LensDirtTex ("Lens Dirt Texture", 2D) = "black" {}
        _LensDirtIntensity ("Lens Dirt Intensity", Range(0.0, 5.0)) = 1.5
        _RadialBlurStrength ("Lens Edge Smear", Range(0.0, 0.05)) = 0.015

        [Header(Analog TV and Lens Noise)]
        _FilmGrainIntensity ("Film Grain Intensity", Range(0.0, 1.0)) = 0.08
        _LensFogIntensity ("Lens Fog Haze", Range(0.0, 1.0)) = 0.2
        _ScanlineIntensity ("TV Scanline Density", Range(0.0, 1.0)) = 0.1
        _ScanlineCount ("Scanline Frequency", Float) = 400.0
        _JitterIntensity ("Horizontal Jitter / Flicker", Range(0.0, 0.05)) = 0.003
        _JitterSpeed ("Jitter Speed", Float) = 25.0
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

            // Compile depth variants for URP Depth Texture
            #pragma multi_compile_fragment _ _CAMERA_DEPTH

            // URP Core Headers & Blit Utilities
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Includes CBUFFER and functions in correct order
            #include "../Shaders/BilateralBloom.hlsl"

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