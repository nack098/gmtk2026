Shader "Custom/URP_IndirectVolumetricClouds"
{
    Properties
    {
        _CloudScale ("Cloud Base Scale", Float) = 0.015
        _CloudDetailScale ("Detail Wisp Scale", Float) = 0.06
        _CloudThreshold ("Cloud Coverage Cutoff", Range(0, 1)) = 0.38
        _CloudDensityMultiplier ("Cloud Density", Float) = 12.0
        
        _SkyColor ("Ambient / Shadow Tint", Color) = (0.1, 0.15, 0.25, 1.0)
        _ForwardScattering ("Silver Lining (Phase)", Range(0, 0.95)) = 0.75
        
        [Header(Wind Animation)]
        _WindDirection ("Wind Dir (XY) & Speed (Z)", Vector) = (1.0, 0.2, 0.03, 0.0)

        _MaxSteps ("Max Raymarch Steps", Range(16, 64)) = 32
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+2000"
            "RenderPipeline" = "UniversalPipeline"
            "DisableBatching" = "True"
        }
        
        Cull Off 
        ZWrite Off 
        ZTest LEqual
        ColorMask RGB
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "IndirectVolumetricCloudPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct CloudInstanceData
            {
                float4x4 localToWorld;
                float4x4 worldToLocal;
            };

            StructuredBuffer<CloudInstanceData> _CloudInstanceBuffer;

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceID   : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                uint instanceID   : SV_InstanceID;
            };

            float _CloudScale;
            float _CloudDetailScale;
            float _CloudThreshold;
            float _CloudDensityMultiplier;
            float4 _SkyColor;
            float _ForwardScattering;
            float4 _WindDirection;
            int _MaxSteps;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.instanceID = input.instanceID;

                float4x4 l2w = _CloudInstanceBuffer[input.instanceID].localToWorld;
                float4 worldPos = mul(l2w, input.positionOS);

                output.worldPos = worldPos.xyz;
                output.positionCS = TransformWorldToHClip(worldPos.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            bool IntersectBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 invR = 1.0 / (rd + 1e-6);
                float3 t0 = (-0.5 - ro) * invR;
                float3 t1 = ( 0.5 - ro) * invR;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);

                return tFar > max(tNear, 0.0);
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3D(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(hash(p + float3(0,0,0)), hash(p + float3(1,0,0)), f.x),
                         lerp(hash(p + float3(0,1,0)), hash(p + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(p + float3(0,0,1)), hash(p + float3(1,0,1)), f.x),
                         lerp(hash(p + float3(0,1,1)), hash(p + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            float billowFBM(float3 p)
            {
                float val = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    val += amp * abs(noise3D(p) * 2.0 - 1.0);
                    p *= 2.15;
                    amp *= 0.5;
                }
                return val;
            }

            // Dual Phase Function for realistic forward & backward scattering
            float DualHenyeyGreenstein(float g, float costheta)
            {
                float g1 = g;
                float g2 = -0.3;
                float hg1 = (1.0 - g1*g1) / (4.0 * 3.14159265 * pow(max(0.0001, 1.0 + g1*g1 - 2.0*g1*costheta), 1.5));
                float hg2 = (1.0 - g2*g2) / (4.0 * 3.14159265 * pow(max(0.0001, 1.0 + g2*g2 - 2.0*g2*costheta), 1.5));
                return lerp(hg1, hg2, 0.3);
            }

            // Realistic Cumulus Height Map Gradient
            float GetHeightGradient(float heightNorm)
            {
                // Sharp flat bottom base, rounded cauliflower tops
                float bottomFade = smoothstep(0.0, 0.15, heightNorm);
                float topFade = smoothstep(1.0, 0.6, heightNorm);
                return bottomFade * topFade;
            }

            float SampleCloudDensity(float3 localPos)
            {
                float heightNorm = localPos.y + 0.5; // 0.0 at bottom, 1.0 at top
                if (heightNorm < 0.0 || heightNorm > 1.0) return 0.0;

                float heightGradient = GetHeightGradient(heightNorm);

                // Animated wind offset
                float3 windOffset = float3(_WindDirection.xy, 0.1) * (_Time.y * _WindDirection.z);
                float3 animatedPos = (localPos + windOffset);

                // Base Low-Frequency Shape Noise
                float baseNoise = billowFBM(animatedPos * _CloudScale * 100.0);
                
                // Mask base noise with height gradient
                float density = max(0.0, baseNoise - _CloudThreshold) * heightGradient;

                if (density > 0.001)
                {
                    // High-frequency detail erosion for fluffy wisp edges
                    float detailNoise = billowFBM(animatedPos * _CloudDetailScale * 100.0);
                    float detailErosion = (1.0 - detailNoise) * 0.35;
                    density = max(0.0, density - detailErosion);
                }

                return density * _CloudDensityMultiplier;
            }

            float LightMarch(float3 localPos, float3 localSunDir)
            {
                float stepSize = 0.08;
                float totalDensity = 0.0;

                // 4 quick light-marching steps towards the sun
                for (int i = 0; i < 4; i++)
                {
                    localPos += localSunDir * stepSize;
                    if (any(abs(localPos) > 0.5)) break;
                    totalDensity += SampleCloudDensity(localPos) * stepSize;
                }

                // Beer-Sugar Powdered Light Absorption Model
                float d = totalDensity * 6.0;
                float beer = exp(-d * 0.25);
                float powder = 1.0 - exp(-d * 2.0);
                
                return beer * powder * 2.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                float3 mainLightDirWS = mainLight.direction;
                float3 mainLightColor = mainLight.color;

                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = normalize(input.worldPos - rayOriginWS);

                float4x4 w2l = _CloudInstanceBuffer[input.instanceID].worldToLocal;

                float3 rayOriginOS = mul(w2l, float4(rayOriginWS, 1.0)).xyz;
                float3 rayDirOS = normalize(mul((float3x3)w2l, rayDirWS));

                float tNear, tFar;
                if (!IntersectBox(rayOriginOS, rayDirOS, tNear, tFar))
                {
                    discard;
                }

                if (all(abs(rayOriginOS) <= 0.5))
                {
                    tNear = 0.0;
                }

                float2 uv = input.screenPos.xy / input.screenPos.w;
                #if defined(REQUIRE_DEPTH_TEXTURE) || defined(_CAMERA_DEPTH_TEXTURE)
                    float rawDepth = SampleSceneDepth(uv);
                    float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float distToMeshWS = distance(rayOriginWS, input.worldPos);
                    if (sceneLinearDepth < distToMeshWS) discard;
                #endif

                float marchDistance = tFar - tNear;
                
                // Max steps capped at 32-48 for smooth, fast performance!
                int steps = min(_MaxSteps, 48);
                float stepSize = marchDistance / (float)steps;

                // Dithering jitter to hide step banding
                float jitter = hash(input.worldPos * 100.0) * stepSize;
                float3 currentLocalPos = rayOriginOS + rayDirOS * (tNear + jitter);

                float3 localSunDir = normalize(mul((float3x3)w2l, mainLightDirWS));
                float cosTheta = dot(rayDirWS, mainLightDirWS);
                float phase = DualHenyeyGreenstein(_ForwardScattering, cosTheta);

                float transmittance = 1.0;
                float3 lightEnergy = float3(0.0, 0.0, 0.0);

                for (int step = 0; step < 48; step++)
                {
                    if (step >= steps) break;

                    float density = SampleCloudDensity(currentLocalPos);

                    if (density > 0.001)
                    {
                        float lightTransmittance = LightMarch(currentLocalPos, localSunDir);
                        float stepTransmittance = exp(-density * stepSize * 2.5);

                        // Darker ambient shadow base + bright scattered tops
                        float heightNorm = currentLocalPos.y + 0.5;
                        float3 ambient = _SkyColor.rgb * lerp(0.2, 1.0, heightNorm);
                        float3 directLight = mainLightColor * lightTransmittance * phase * 3.5;

                        lightEnergy += transmittance * density * stepSize * (ambient + directLight);
                        transmittance *= stepTransmittance;

                        if (transmittance < 0.01) break;
                    }

                    currentLocalPos += rayDirOS * stepSize;
                }

                float finalAlpha = 1.0 - transmittance;
                return float4(lightEnergy, finalAlpha);
            }
            ENDHLSL
        }
    }
}