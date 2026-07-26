Shader "FREE Food Pack/Food" {
    Properties {
        _MainTex ("MainTex", 2D) = "white" {}
        _FresnelSize ("FresnelSize", Range(0.5, 5)) = 0.6153846
        _FresnelIntensity ("FresnelIntensity", Float ) = 1
        _FresnelColor ("FresnelColor", Color) = (0.5,0.5,0.5,1)
        _push ("push", Range(0, 0.01)) = 0
        _Speed ("Speed", Float ) = 1
    }
    SubShader {
        Tags {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }
        Pass {
            Name "ForwardLit"
            Tags {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FresnelColor;
                float _FresnelSize;
                float _FresnelIntensity;
                float _push;
                float _Speed;
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float sinTime = sin(_Time.y * _Speed) * 0.5 + 0.5;
                float3 pushedPosOS = input.positionOS.xyz + (input.normalOS * (_push * sinTime));

                VertexPositionInputs vertexInput = GetVertexPositionInputs(pushedPosOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                float fresnelTerm = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelSize);
                float3 fresnel = _FresnelColor.rgb * fresnelTerm * _FresnelIntensity;

                float3 finalColor = mainTexColor.rgb + fresnel;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShadowUtils.hlsl" // FIX: Required for ApplyShadowBias

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FresnelColor;
                float _FresnelSize;
                float _FresnelIntensity;
                float _push;
                float _Speed;
            CBUFFER_END

            // Uniform provided by URP for light direction during shadow pass
            float3 _LightDirection;

            Varyings vert(Attributes input) {
                Varyings output;
                float sinTime = sin(_Time.y * _Speed) * 0.5 + 0.5;
                float3 pushedPosOS = input.positionOS.xyz + (input.normalOS * (_push * sinTime));

                float3 positionWS = TransformObjectToWorld(pushedPosOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // FIX: Pass actual light direction vector instead of hardcoded float3(0,1,0)
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}