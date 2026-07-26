Shader "Custom/URP_AdvancedVolumetricCloudsWebGPU"
{
    Properties
    {
        [Header(Cloud Coverage and Shape)]
        _CloudScale ("Base Noise Scale", Range(0.001, 0.1)) = 0.035
        _DetailScale ("Detail Worley Scale", Range(0.01, 0.3)) = 0.12
        _CloudCoverage ("Global Cloud Coverage", Range(0.0, 1.0)) = 0.55
        _CloudType ("Cloud Type (0:Stratus, 0.5:Cumulus, 1:Anvil)", Range(0.0, 1.0)) = 0.5
        _CloudDensityMultiplier ("Cloud Density", Range(0.1, 50.0)) = 25.0

        [Header(Lighting and Scattering)]
        _LightAbsorption ("Light Absorption (Beer)", Range(0.1, 10.0)) = 2.8
        _PowderIntensity ("Beer-Powder Darkening Effect", Range(0.0, 10.0)) = 3.5
        _PhaseForward ("Forward Scattering (Silver Lining)", Range(0.0, 0.99)) = 0.8
        _PhaseBackward ("Backward Scattering (Backlight Glow)", Range(-0.99, 0.0)) = -0.3
        _PhaseBlend ("Forward/Backward Scattering Blend", Range(0.0, 1.0)) = 0.7
        _AmbientColor ("Ambient Atmosphere Color", Color) = (0.1, 0.13, 0.18, 1.0)
        _WindDirection ("Wind Vector (XY:Dir, Z:Speed)", Vector) = (0.04, 0.02, 0.6, 0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            Name "AdvancedVolumetricClouds"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_STEPS 32

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint instanceID   : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float4 uv         : TEXCOORD1;
            };

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<float4x4> _ObjectToWorldBuffer;
                StructuredBuffer<float4x4> _WorldToObjectBuffer;
            #endif

            void setup()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    uint id = unity_InstanceID;
                    unity_ObjectToWorld = _ObjectToWorldBuffer[id];
                    unity_WorldToObject = _WorldToObjectBuffer[id];
                #endif
            }

            // Explicit std140 layout padding for WebGPU/WGSL translation
            CBUFFER_START(UnityPerMaterial)
                float _CloudScale;
                float _DetailScale;
                float _CloudCoverage;
                float _CloudType;

                float _CloudDensityMultiplier;
                float _LightAbsorption;
                float _PowderIntensity;
                float _PhaseForward;

                float _PhaseBackward;
                float _PhaseBlend;
                float2 _Pad0;

                float4 _AmbientColor;
                float4 _WindDirection;
            CBUFFER_END

            // --- Advanced Procedural Noise Generators ---
            float hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 hash33(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx);
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(hash13(i + float3(0,0,0)), hash13(i + float3(1,0,0)), f.x),
                         lerp(hash13(i + float3(0,1,0)), hash13(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash13(i + float3(0,0,1)), hash13(i + float3(1,0,1)), f.x),
                         lerp(hash13(i + float3(0,1,1)), hash13(i + float3(1,1,1)), f.x), f.y),
                    f.z
                );
            }

            // Worley (Cellular) Noise to simulate crisp cloud cell boundaries
            float worleyNoise3D(float3 p)
            {
                float3 id = floor(p);
                float3 fd = frac(p);
                float minDist = 1.0;

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            float3 offset = float3(x, y, z);
                            float3 cellPoint = hash33(id + offset);
                            float3 r = offset + cellPoint - fd;
                            float d = dot(r, r);
                            minDist = min(minDist, d);
                        }
                    }
                }
                return sqrt(minDist);
            }

            // Composite Perlin-Worley Base Noise
            float perlinWorleyFBM(float3 p)
            {
                float perlin = noise3D(p) * 0.5 + noise3D(p * 2.0) * 0.25 + noise3D(p * 4.0) * 0.125;
                float worley = 1.0 - worleyNoise3D(p * 2.0); // Inverted Worley creates puffy centers
                return lerp(perlin, worley, 0.45);
            }

            // --- Multi-Altitude Profile Remapping ---
            float GetAltitudeGradient(float heightNorm, float cloudType)
            {
                // Remap height curve based on cloud type classification:
                // 0.0 = Stratus (Low flat layers)
                // 0.5 = Cumulus (Puffy mid towers)
                // 1.0 = Cumulonimbus / Anvil (Massive full vertical expanse)
                float stratus  = smoothstep(0.0, 0.1, heightNorm) * smoothstep(0.3, 0.15, heightNorm);
                float cumulus  = smoothstep(0.05, 0.25, heightNorm) * smoothstep(0.7, 0.4, heightNorm);
                float anvil    = smoothstep(0.0, 0.1, heightNorm) * smoothstep(1.0, 0.75, heightNorm);

                float lowMid = lerp(stratus, cumulus, saturate(cloudType * 2.0));
                float midHigh = lerp(cumulus, anvil, saturate((cloudType - 0.5) * 2.0));
                return lerp(lowMid, midHigh, step(0.5, cloudType));
            }

            // --- Advanced Lighting Calculations ---
            float SingleHGPhase(float cosAngle, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(max(0.0001, 1.0 + g2 - 2.0 * g * cosAngle), 1.5));
            }

            // Dual-Lobe Henyey-Greenstein Scattering (Silver Lining + Backlight Glow)
            float DualHGPhase(float cosAngle, float gForward, float gBackward, float blend)
            {
                float fwd = SingleHGPhase(cosAngle, gForward);
                float bwd = SingleHGPhase(cosAngle, gBackward);
                return lerp(bwd, fwd, blend);
            }

            // Beer-Powder Law (Approximates multiple scattering light trapping inside cloud cores)
            float BeerPowderLaw(float opticalDepth, float density)
            {
                float beer = exp(-opticalDepth);
                // Tamed powder effect: clamps the darkening and prevents total blackouts
                float powder = 1.0 - exp(-max(0.0, density) * _PowderIntensity * 0.2);
                return max(beer, beer * powder * 2.0);
            }

            bool IntersectBox(float3 rayOrigin, float3 rayDir, out float tNear, out float tFar)
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);

                float3 invDir = 1.0 / (rayDir + sign(rayDir) * 1e-6);
                float3 tMin = (boxMin - rayOrigin) * invDir;
                float3 tMax = (boxMax - rayOrigin) * invDir;

                float3 t1 = min(tMin, tMax);
                float3 t2 = max(tMin, tMax);

                tNear = max(max(t1.x, t1.y), t1.z);
                tFar  = min(min(t2.x, t2.y), t2.z);

                return (tFar > max(0.0, tNear));
            }

            float SampleCloudDensity(float3 localPos)
            {
                float heightNorm = localPos.y + 0.5;
                if (heightNorm < 0.0 || heightNorm > 1.0) return 0.0;

                float altitudeGradient = GetAltitudeGradient(heightNorm, _CloudType);
                if (altitudeGradient <= 0.001) return 0.0;

                // Center bias to ensure cloud coverage over camera origin
                float distFromCenter = length(localPos.xz);
                float centerBias = lerp(1.3, 0.7, smoothstep(0.0, 0.5, distFromCenter));

                float3 windOffset = float3(_WindDirection.xy, 0.1) * (_Time.y * _WindDirection.z);
                float3 animatedPos = localPos + windOffset;

                // Step 1: Base Perlin-Worley shape
                float baseNoise = perlinWorleyFBM(animatedPos * _CloudScale * 100.0) * centerBias;
                
                // Remap base noise using global coverage
                float threshold = 1.0 - _CloudCoverage;
                float density = saturate((baseNoise - threshold) / max(0.001, 1.0 - threshold));
                density *= altitudeGradient;

                // Step 2: Carve wispy details using Worley Erosion
                if (density > 0.001)
                {
                    float detailWorley = worleyNoise3D(animatedPos * _DetailScale * 100.0);
                    float detailErosion = (1.0 - detailWorley) * 0.35 * (1.0 - heightNorm);
                    density = saturate(density - detailErosion);
                }

                return density * _CloudDensityMultiplier;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.uv = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 cameraOS = TransformWorldToObject(GetCameraPositionWS());
                float3 positionOS = input.positionOS;
                float3 rayDirOS = normalize(positionOS - cameraOS);

                float tNear, tFar;
                if (!IntersectBox(cameraOS, rayDirOS, tNear, tFar)) discard;

                bool cameraInside = all(abs(cameraOS) <= 0.5);
                if (cameraInside) tNear = 0.001;

                if (tFar <= tNear) discard;

                float2 screenUV = input.uv.xy / input.uv.w;
                #if defined(REQUIRE_DEPTH_TEXTURE)
                    float rawDepth = SampleSceneDepth(screenUV);
                    float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    
                    #if UNITY_REVERSED_Z
                        bool isSky = (rawDepth <= 0.00001);
                    #else
                        bool isSky = (rawDepth >= 0.99999);
                    #endif

                    float boxDistWS = distance(GetCameraPositionWS(), TransformObjectToWorld(cameraOS + rayDirOS * tNear));
                    if (!isSky && boxDistWS >= linearDepth) discard;
                #endif

                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(TransformWorldToObjectDir(mainLight.direction));
                float3 lightColor = mainLight.color * mainLight.distanceAttenuation;

                float cosAngle = dot(rayDirOS, lightDirOS);
                float phase = DualHGPhase(cosAngle, _PhaseForward, _PhaseBackward, _PhaseBlend);

                float stepSize = (tFar - tNear) / float(MAX_STEPS);
                float jitter = hash13(input.positionCS.xyz + _Time.y) * 0.2;
                float3 currentPosOS = cameraOS + rayDirOS * (tNear + stepSize * jitter);

                float transmittance = 1.0;
                float3 accumulatedLight = float3(0,0,0);

                [loop]
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float density = SampleCloudDensity(currentPosOS);

                    if (density > 0.001)
                    {
                        float sampleStepDensity = density * stepSize;
                        float opticalDepth = sampleStepDensity * _LightAbsorption;
                        
                        // Advanced Multiple Scattering Approximation via Beer-Powder
                        float sampleTransmittance = BeerPowderLaw(opticalDepth, density);

                        // Light Sample along sun ray
                        float3 lightSamplePos = currentPosOS + lightDirOS * 0.06;
                        float lightDensity = SampleCloudDensity(lightSamplePos);
                        float lightOpticalDepth = lightDensity * _LightAbsorption * 0.6;
                        float lightAttenuation = BeerPowderLaw(lightOpticalDepth, lightDensity);

                        float3 directLight = lightColor * lightAttenuation * phase;
                        float3 ambientLight = _AmbientColor.rgb * (currentPosOS.y + 0.5); // Altitude ambient gradient
                        float3 stepLight = (directLight + ambientLight) * sampleStepDensity;

                        accumulatedLight += stepLight * transmittance;
                        transmittance *= sampleTransmittance;

                        if (transmittance < 0.01)
                        {
                            transmittance = 0.0;
                            break;
                        }
                    }

                    currentPosOS += rayDirOS * stepSize;
                }

                float alpha = 1.0 - transmittance;
                if (alpha <= 0.001) discard;

                return float4(accumulatedLight, alpha);
            }
            ENDHLSL
        }
    }
}