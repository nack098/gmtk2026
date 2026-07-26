Shader "Custom/URP_IndirectVolumetricFog_Smooth"
{
    Properties
    {
        [Header(Fog Density and Distribution)]
        _FogDensity ("Global Fog Density", Range(0.001, 1.0)) = 0.05
        _FogHeightFalloff ("Height Attenuation Falloff", Range(0.001, 0.1)) = 0.01
        _FogBaseHeight ("Fog Base Y Level", Float) = 0.0
        
        [Header(Noise and Animation)]
        _NoiseScale ("3D Fog Noise Scale", Range(0.0001, 0.02)) = 0.002
        _WindVector ("Wind Vector (XYZ:Dir, W:Speed)", Vector) = (0.05, 0.0, 0.02, 0.5)

        [Header(Atmospheric Colors)]
        _FogColor ("In-Scattering / Sun Color", Color) = (0.75, 0.85, 0.95, 1.0)
        _ShadowColor ("Ambient Shadow Color", Color) = (0.3, 0.35, 0.45, 1.0)
        _SunPower ("Sun Scattering Directivity", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent-100" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off 
        ZTest LEqual 

        Pass
        {
            Name "IndirectVolumetricFogSmooth"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define FOG_STEPS 24

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceID   : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
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

            CBUFFER_START(UnityPerMaterial)
                float _FogDensity;
                float _FogHeightFalloff;
                float _FogBaseHeight;

                float _NoiseScale;
                float4 _WindVector;

                float4 _FogColor;
                float4 _ShadowColor;
                float _SunPower;
            CBUFFER_END

            // --- Fast 3D Value Noise ---
            float hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
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

            float SampleFogDensity(float3 worldPos)
            {
                float heightDiff = worldPos.y - _FogBaseHeight;
                float heightFactor = exp(-max(0.0, heightDiff) * _FogHeightFalloff);

                float3 windOffset = _WindVector.xyz * (_Time.y * _WindVector.w);
                float noise = noise3D((worldPos + windOffset) * _NoiseScale);

                return heightFactor * noise * _FogDensity;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.screenPos  = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                
                // Read linear depth from depth buffer
                float rawDepth = SampleSceneDepth(uv);
                float sceneDepthLinear = LinearEyeDepth(rawDepth, _ZBufferParams);
                if (rawDepth <= 0.00001) sceneDepthLinear = 5000.0; // Skybox fallback

                float3 cameraWS = GetCameraPositionWS();
                float3 cameraOS = TransformWorldToObject(cameraWS);
                float3 positionOS = input.positionOS;
                
                float3 rayDirOS = normalize(positionOS - cameraOS);
                float3 rayDirWS = normalize(TransformObjectToWorldDir(rayDirOS));

                float tNear, tFar;
                if (!IntersectBox(cameraOS, rayDirOS, tNear, tFar)) discard;

                bool cameraInside = all(abs(cameraOS) <= 0.5);
                if (cameraInside) tNear = 0.0;

                float boxScaleFactor = length(TransformObjectToWorldDir(float3(1,0,0)));
                float rayStart = tNear * boxScaleFactor;
                float rayEnd = min(tFar * boxScaleFactor, sceneDepthLinear);

                if (rayEnd <= rayStart) discard;

                float rayLength = rayEnd - rayStart;
                float stepSize = rayLength / float(FOG_STEPS);
                float3 currentPosWS = cameraWS + rayDirWS * rayStart;

                Light mainLight = GetMainLight();
                float accumulatedDensity = 0.0;
                float accumulatedLight = 0.0;

                [loop]
                for (int i = 0; i < FOG_STEPS; i++)
                {
                    float density = SampleFogDensity(currentPosWS);

                    if (density > 0.00001)
                    {
                        accumulatedDensity += density * stepSize;

                        float NdotL = dot(rayDirWS, mainLight.direction);
                        float phase = lerp(0.5, saturate(NdotL * 0.5 + 0.5), _SunPower);
                        accumulatedLight += phase * density * stepSize;
                    }

                    currentPosWS += rayDirWS * stepSize;
                }

                if (accumulatedDensity <= 0.00001) discard;

                float lightRatio = saturate(accumulatedLight / max(0.0001, accumulatedDensity));
                float3 finalFogColor = lerp(_ShadowColor.rgb, _FogColor.rgb * mainLight.color, lightRatio);
                
                float alpha = saturate(1.0 - exp(-accumulatedDensity));

                return float4(finalFogColor, alpha);
            }
            ENDHLSL
        }
    }
}
