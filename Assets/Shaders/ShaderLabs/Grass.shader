Shader "Takayama/Grass"
{
    Properties
    {
        _BaseColor ("Grass Root Color", Color) = (0.05, 0.25, 0.05, 1.0)
        _TipColor  ("Grass Tip Color", Color)  = (0.3, 0.8, 0.2, 1.0)
        _IsPlaneMesh ("Is Plane Mesh", Float) = 0.0
        _MeshHeight ("Mesh Height", Float) = 1.0
        _NormalNormalBlend ("Normal Blend", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        // ----------------------------------------------------
        // PASS 1: FORWARD LIT PASS
        // ----------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_CASCADE _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON

            #include "../Shaders/Grass.hlsl"
            ENDHLSL
        }

        // ----------------------------------------------------
        // PASS 2: SHADOW CASTER PASS
        // ----------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertShadow 
            #pragma fragment fragShadow

            #pragma multi_compile_shadowcaster

            #include "../Shaders/GrassShadows.hlsl"
            ENDHLSL
        }

        // ----------------------------------------------------
        // PASS 3: DEPTH ONLY PASS
        // ----------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            #include "../Shaders/GrassShadows.hlsl"
            ENDHLSL
        }
    }
}