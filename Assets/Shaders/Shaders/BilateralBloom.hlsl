#ifndef BILATERAL_BLOOM_INCLUDED
#define BILATERAL_BLOOM_INCLUDED

// --- Declare All Material Uniforms ---
CBUFFER_START(UnityPerMaterial)
    // 1. Bilateral & Bloom Controls
    float _SpatialSigma;
    float _ColorSigma;
    float _DepthSigma;
    float _BloomThreshold;
    float _BloomIntensity;

    // 2. Saturation & Tinting
    float _Saturation;
    float4 _ColorTint;

    // 3. Vignette Controls
    float _VignetteIntensity;
    float _VignetteSmoothness;

    // 4. Color Balance
    float4 _Shadows;
    float4 _Midtones;
    float4 _Highlights;

    // 5. Exposure & Contrast
    float _Exposure;
    float _Contrast;
CBUFFER_END

// --- Math & Color Utilities ---

// Luminance calculation in Rec.709 color space
float GetLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

// Extract bright pixels for HDR bloom
float3 ExtractBrightPass(float3 color, float threshold)
{
    float lum = GetLuminance(color);
    float brightFactor = smoothstep(threshold, threshold + 0.2, lum);
    return color * brightFactor;
}

// Bilateral Weight Calculator (Spatial, Color, and Depth distance)
float CalculateBilateralWeight(float3 centerColor, float3 sampleColor, float centerDepth, float sampleDepth, float2 offset)
{
    float spatialDistSq = dot(offset, offset);
    float spatialWeight = exp(-spatialDistSq / (2.0 * _SpatialSigma * _SpatialSigma));

    float3 colorDiff = centerColor - sampleColor;
    float colorDistSq = dot(colorDiff, colorDiff);
    float colorWeight = exp(-colorDistSq / (2.0 * _ColorSigma * _ColorSigma));

    float depthDiff = abs(centerDepth - sampleDepth);
    float depthWeight = exp(-depthDiff / (2.0 * _DepthSigma * _DepthSigma));

    return spatialWeight * colorWeight * depthWeight;
}

// Standard ACES Film Tonemapper (Squeezes HDR [0, ∞) -> LDR [0, 1])
float3 ACESTonemap(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

// --- Main Fragment Pass ---
float4 FragSurfaceBlur(Varyings input) : SV_Target
{
    float2 uv = input.texcoord;

    // Sample raw HDR scene color and depth
    float3 centerColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
    float centerDepth = Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);

    // ==========================================
    // STEP 1: Exposure Adjust (HDR Space)
    // ==========================================
    centerColor *= exp2(_Exposure);

    // ==========================================
    // STEP 2: 5x5 Bilateral Surface Blur / Bloom
    // ==========================================
    float3 accumulatedBloom = 0.0;
    float totalWeight = 0.0;

    [unroll]
    for (int x = -2; x <= 2; ++x)
    {
        [unroll]
        for (int y = -2; y <= 2; ++y)
        {
            float2 offset = float2(x, y);
            float2 sampleUV = uv + offset * _BlitTexture_TexelSize.xy;

            float3 rawSampleColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV).rgb;
            rawSampleColor *= exp2(_Exposure);

            float3 brightSampleColor = ExtractBrightPass(rawSampleColor, _BloomThreshold);
            float sampleDepth = Linear01Depth(SampleSceneDepth(sampleUV), _ZBufferParams);

            float weight = CalculateBilateralWeight(centerColor, rawSampleColor, centerDepth, sampleDepth, offset);

            accumulatedBloom += brightSampleColor * weight;
            totalWeight += weight;
        }
    }

    float3 blurredGlow = (accumulatedBloom / max(totalWeight, 0.00001)) * _BloomIntensity;
    float3 hdrColor = centerColor + blurredGlow;

    // ==========================================
    // STEP 3: Monochrome & Saturation (HDR Space)
    // ==========================================
    float luma = GetLuminance(hdrColor);
    float3 monochromeColor = float3(luma, luma, luma);
    float3 desaturatedColor = lerp(monochromeColor, hdrColor, _Saturation);

    // ==========================================
    // STEP 4: Color Tint & 3-Way Color Balance
    // ==========================================
    float3 tintedColor = desaturatedColor * _ColorTint.rgb;

    // Split HDR color into Shadows, Midtones, and Highlights based on luma
    float shadowWeight = saturate(1.0 - (luma * 2.0));
    float highlightWeight = saturate((luma - 0.5) * 2.0);
    float midtoneWeight = saturate(1.0 - shadowWeight - highlightWeight);

    float3 colorBalanced = (tintedColor * _Shadows.rgb * shadowWeight) +
                           (tintedColor * _Midtones.rgb * midtoneWeight) +
                           (tintedColor * _Highlights.rgb * highlightWeight);

    // Contrast adjustment
    colorBalanced = saturate((colorBalanced - 0.5) * _Contrast + 0.5);

    // ==========================================
    // STEP 5: ACES Tonemapping (HDR -> LDR)
    // ==========================================
    float3 ldrColor = ACESTonemap(colorBalanced);

    // ==========================================
    // STEP 6: Vignette (LDR Display Space)
    // ==========================================
    float2 coord = (uv - 0.5) * 2.0;
    float dist = length(coord);
    float vignette = smoothstep(_VignetteIntensity, _VignetteIntensity - _VignetteSmoothness, dist);
    
    float3 finalDisplayColor = ldrColor * vignette;

    return float4(finalDisplayColor, 1.0);
}

#endif