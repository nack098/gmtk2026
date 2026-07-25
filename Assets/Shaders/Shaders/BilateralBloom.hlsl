#ifndef BILATERAL_BLOOM_INCLUDED
#define BILATERAL_BLOOM_INCLUDED

// Lens Dirt Texture Declaration
TEXTURE2D(_LensDirtTex);
SAMPLER(sampler_LensDirtTex);

// --- Declare All Material Uniforms (CBUFFER aligned to 16-byte boundaries) ---
CBUFFER_START(UnityPerMaterial)
    // float4 (16 bytes each)
    float4 _ColorTint;
    float4 _Shadows;
    float4 _Midtones;
    float4 _Highlights;
    float4 _SSSColor;
    float4 _AirDustColor;
    float4 _AnimatedFogColor;
    float4 _AnimatedFogSpeed; // Only XY used, ZW act as padding

    // float groups (16 bytes per chunk of 4)
    float _SpatialSigma;
    float _ColorSigma;
    float _DepthSigma;
    float _BloomThreshold;

    float _BloomIntensity;
    float _Saturation;
    float _VignetteIntensity;
    float _VignetteSmoothness;

    float _Exposure;
    float _Contrast;
    float _AORadius;
    float _AOIntensity;

    float _AOBias;
    float _SSSIntensity;
    float _SSSRadius;
    float _FilmGrainIntensity;

    float _LensFogIntensity;
    float _ScanlineIntensity;
    float _ScanlineCount;
    float _JitterIntensity;

    float _JitterSpeed;
    float _AirDustDensity;
    float _AirDustStartDistance;
    float _AirDustNoiseScale;

    float _LensDirtIntensity;
    float _RadialBlurStrength;
    float _AnimatedFogIntensity;
    float _AnimatedFogScale;
CBUFFER_END

// Pseudo-random noise generator
float PseudoRandom(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

// Animated 2D Hash Noise
float DynamicNoise(float2 uv, float time)
{
    return frac(sin(dot(uv + float2(time, time * 0.5), float2(12.9898, 78.233))) * 43758.5453);
}

// Smooth 2D Value Noise
float ValueNoise(float2 st)
{
    float2 i = floor(st);
    float2 f = frac(st);

    float a = PseudoRandom(i);
    float b = PseudoRandom(i + float2(1.0, 0.0));
    float c = PseudoRandom(i + float2(0.0, 1.0));
    float d = PseudoRandom(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

// 3-Octave Fractional Brownian Motion (fBm) for fluid mist density
float FbmNoise(float2 st)
{
    float value = 0.0;
    float amplitude = 0.5;
    
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        value += amplitude * ValueNoise(st);
        st *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

// Swirling Volumetric Fog using Domain Warping
float CalculateDriftingFog(float2 uv, float time, float scale, float2 speed)
{
    float2 fogUV = uv * scale + time * speed;

    // Domain Warp: Distort UV coordinates with secondary noise
    float2 warpOffset = float2(
        FbmNoise(fogUV + float2(0.0, time * 0.02)),
        FbmNoise(fogUV + float2(5.2, time * 0.015))
    );

    return FbmNoise(fogUV + warpOffset * 1.5);
}

// Screen Space Ambient Occlusion (SSAO) Calculation
float CalculateAO(float2 uv, float centerEyeDepth)
{
    float ao = 0.0;
    float randomAngle = PseudoRandom(uv) * 6.283185;
    
    const int SAMPLE_COUNT = 8;

    [unroll]
    for (int i = 0; i < SAMPLE_COUNT; ++i)
    {
        float angle = randomAngle + (float(i) * (6.283185 / float(SAMPLE_COUNT)));
        float2 dir = float2(cos(angle), sin(angle));
        
        float2 sampleOffset = dir * (_AORadius / max(centerEyeDepth, 0.1)) * _BlitTexture_TexelSize.xy * 100.0;
        float2 sampleUV = uv + sampleOffset;
        
        float sampleEyeDepth = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
        
        float depthDiff = centerEyeDepth - sampleEyeDepth;
        if (depthDiff > _AOBias && depthDiff < _AORadius)
        {
            ao += 1.0 - smoothstep(_AOBias, _AORadius, depthDiff);
        }
    }
    
    ao = 1.0 - ((ao / float(SAMPLE_COUNT)) * _AOIntensity);
    return saturate(ao);
}

// Screen Space Subsurface Scattering (SSSS)
float3 CalculateSSSS(float2 uv, float3 centerColor, float centerEyeDepth)
{
    if (_SSSIntensity <= 0.001) return centerColor;

    const float sampleOffsets[5] = { -2.0, -1.0, 0.0, 1.0, 2.0 };
    
    const float3 sampleWeights[5] = {
        float3(0.05, 0.02, 0.01),
        float3(0.25, 0.20, 0.15),
        float3(0.40, 0.58, 0.68),
        float3(0.25, 0.20, 0.15),
        float3(0.05, 0.02, 0.01)
    };

    float3 sssAccumulation = centerColor * sampleWeights[2];
    float3 totalWeight = sampleWeights[2];

    float2 sssStep = (_SSSRadius / max(centerEyeDepth, 0.1)) * _BlitTexture_TexelSize.xy * 100.0;

    [unroll]
    for (int i = 0; i < 5; ++i)
    {
        if (i == 2) continue;

        float2 sampleUV = uv + float2(sampleOffsets[i], 0.0) * sssStep;
        float sampleEyeDepth = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);

        float depthDiff = abs(centerEyeDepth - sampleEyeDepth);
        float edgeGuard = step(depthDiff, 0.1); 

        float depthWeight = exp(-depthDiff * depthDiff * 50.0) * edgeGuard;

        float3 sampleColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV).rgb;

        sssAccumulation += sampleColor * sampleWeights[i] * depthWeight;
        totalWeight += sampleWeights[i] * depthWeight;
    }

    float3 sssBlurred = sssAccumulation / max(totalWeight, 0.0001);

    return lerp(centerColor, sssBlurred * _SSSColor.rgb, _SSSIntensity * 0.5);
}

// Distance-based Atmospheric Air Dust Fog
float3 ApplyAirDustFog(float3 sceneColor, float2 uv, float eyeDepth, float time)
{
    if (_AirDustDensity <= 0.0001) return sceneColor;

    float fogDistance = max(0.0, eyeDepth - _AirDustStartDistance);

    float dustDrift = DynamicNoise(uv * 5.0 + float2(time * 0.02, time * 0.01), 0.0);
    float noiseModulation = lerp(1.0, 0.7 + dustDrift * 0.6, _AirDustNoiseScale);

    float fogFactor = 1.0 - exp(-fogDistance * _AirDustDensity * noiseModulation);
    fogFactor = saturate(fogFactor);

    return lerp(sceneColor, _AirDustColor.rgb, fogFactor);
}

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

// Bilateral Weight Calculator
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

// ACES Film Tonemapper
float3 ACESTonemap(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

// Main Fragment Pass
float4 FragSurfaceBlur(Varyings input) : SV_Target
{
    float2 uv = input.texcoord;
    float time = _Time.y;

    // STEP 0: TV Scanline Horizontal Line Jitter
    if (_JitterIntensity > 0.0001)
    {
        float lineID = floor(uv.y * _ScanlineCount);
        float jitterNoise = sin(lineID * 12.9898 + floor(time * _JitterSpeed)) * 43758.5453;
        float jitterOffset = (frac(jitterNoise) - 0.5) * 2.0;

        float jitterTrigger = step(0.85, frac(jitterNoise * 0.1)); 
        uv.x += jitterOffset * _JitterIntensity * jitterTrigger;
    }

    // Sample raw HDR scene color
    float3 centerColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;

    // Extract raw scene depth
    float rawDepth = SampleSceneDepth(uv);
    float centerEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
    float centerDepth01 = Linear01Depth(rawDepth, _ZBufferParams);

    // STEP 1: Ambient Occlusion
    float aoFactor = CalculateAO(uv, centerEyeDepth);
    centerColor *= aoFactor;

    // STEP 2: Exposure Adjustment
    centerColor *= exp2(_Exposure);

    // STEP 3: Screen-Space Subsurface Scattering
    centerColor = CalculateSSSS(uv, centerColor, centerEyeDepth);

    // STEP 4: 5x5 Bilateral Surface Blur / Bloom
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
            float sampleDepth01 = Linear01Depth(SampleSceneDepth(sampleUV), _ZBufferParams);

            float weight = CalculateBilateralWeight(centerColor, rawSampleColor, centerDepth01, sampleDepth01, offset);

            accumulatedBloom += brightSampleColor * weight;
            totalWeight += weight;
        }
    }

    float3 blurredGlow = (accumulatedBloom / max(totalWeight, 0.00001)) * _BloomIntensity;
    float3 hdrColor = centerColor + blurredGlow;

    // STEP 5: Real-Time Drifting Fluid Swirl Fog
    if (_AnimatedFogIntensity > 0.001)
    {
        // Calculate fluid domain-warped noise
        float fogDensity = CalculateDriftingFog(uv, time, _AnimatedFogScale, _AnimatedFogSpeed.xy);
        float depthMask = saturate(centerEyeDepth * 0.2);
        float finalFog = smoothstep(0.2, 0.8, fogDensity) * _AnimatedFogIntensity * depthMask;

        // --- Dark Fog / Smoke Blend Logic ---
        // 1. Calculate how dark the fog target color is (0 = Pitch Black, 1 = Pure White)
        float fogLuma = GetLuminance(_AnimatedFogColor.rgb);

        // 2. Light Absorption: Dark fog blocks/absorbs background light instead of adding to it
        float3 absorbedLight = hdrColor * lerp(_AnimatedFogColor.rgb, float3(1, 1, 1), fogLuma);

        // 3. Smoothly blend between light-absorbing dark smoke and bright glowing fog based on fog tint luminance
        float3 darkFogResult = lerp(hdrColor, absorbedLight, finalFog);
        float3 lightFogResult = lerp(hdrColor, hdrColor + _AnimatedFogColor.rgb * finalFog, finalFog);

        hdrColor = lerp(darkFogResult, lightFogResult, fogLuma);
    }

    // STEP 6: Reactive Optical Lens Fog / Bloom Scatter
    if (_LensFogIntensity > 0.001)
    {
        float2 lensUV = (uv - 0.5) * 2.0;
        float lensDist = length(lensUV);
        float radialFog = smoothstep(0.1, 1.4, lensDist);

        float fogLightBleed = GetLuminance(blurredGlow) * 1.5;
        float3 opticalFogColor = float3(0.85, 0.88, 0.92); 
        float totalFogFactor = saturate((radialFog * 0.6 + fogLightBleed * 0.4) * _LensFogIntensity);

        hdrColor = lerp(hdrColor, hdrColor + opticalFogColor * (1.0 + fogLightBleed), totalFogFactor * 0.5);
        hdrColor = lerp(hdrColor, opticalFogColor * GetLuminance(hdrColor), totalFogFactor * 0.25);
    }

    // STEP 7: Lens Dirt & Glass Smudge Masking
    if (_LensDirtIntensity > 0.001)
    {
        float3 lensDirt = SAMPLE_TEXTURE2D(_LensDirtTex, sampler_LensDirtTex, uv).rgb;
        float3 lensGlow = (hdrColor + blurredGlow * 2.0) * lensDirt * _LensDirtIntensity;
        hdrColor += lensGlow;
    }

    // STEP 8: Radial Lens Edge Distortion / Smear
    if (_RadialBlurStrength > 0.0001)
    {
        float2 centerOffset = uv - 0.5;
        float distFromCenter = length(centerOffset);
        
        float2 smearUV = uv + centerOffset * distFromCenter * _RadialBlurStrength;
        float3 smearedColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, smearUV).rgb;
        
        hdrColor = lerp(hdrColor, smearedColor, smoothstep(0.2, 0.8, distFromCenter));
    }

    // STEP 9: Distance-Based Air Dust Atmospheric Fog
    hdrColor = ApplyAirDustFog(hdrColor, uv, centerEyeDepth, time);

    // STEP 10: Monochrome & Saturation
    float luma = GetLuminance(hdrColor);
    float3 monochromeColor = float3(luma, luma, luma);
    float3 desaturatedColor = lerp(monochromeColor, hdrColor, _Saturation);

    // STEP 11: Color Tint & 3-Way Color Balance
    float3 tintedColor = desaturatedColor * _ColorTint.rgb;

    float shadowWeight = saturate(1.0 - (luma * 2.0));
    float highlightWeight = saturate((luma - 0.5) * 2.0);
    float midtoneWeight = saturate(1.0 - shadowWeight - highlightWeight);

    float3 colorBalanced = (tintedColor * _Shadows.rgb * shadowWeight) +
                           (tintedColor * _Midtones.rgb * midtoneWeight) +
                           (tintedColor * _Highlights.rgb * highlightWeight);

    colorBalanced = saturate((colorBalanced - 0.5) * _Contrast + 0.5);

    // STEP 12: ACES Tonemapping (HDR -> LDR)
    float3 ldrColor = ACESTonemap(colorBalanced);

    // STEP 13: Analog TV Scanline Overlay
    if (_ScanlineIntensity > 0.001)
    {
        float scanline = sin(uv.y * _ScanlineCount * 3.14159);
        scanline = saturate(0.5 + 0.5 * scanline);
        ldrColor *= lerp(1.0, scanline, _ScanlineIntensity);
    }

    // STEP 14: Dynamic Film Grain
    if (_FilmGrainIntensity > 0.001)
    {
        float grain = DynamicNoise(uv, time * 15.0);
        float grainLumaWeight = 1.0 - abs(luma - 0.5) * 2.0; 
        ldrColor += (grain - 0.5) * _FilmGrainIntensity * (0.5 + 0.5 * grainLumaWeight);
    }

    // STEP 15: Vignette
    float2 coord = (uv - 0.5) * 2.0;
    float dist = length(coord);
    float vignette = smoothstep(_VignetteIntensity, _VignetteIntensity - _VignetteSmoothness, dist);

    return float4(ldrColor * vignette, 1.0);
}

#endif