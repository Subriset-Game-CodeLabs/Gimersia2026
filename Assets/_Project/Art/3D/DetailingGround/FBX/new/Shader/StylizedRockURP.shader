Shader "Custom/StylizedRockURP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [Header(Stylized Rock Palette)]
        _LitColor("Lit Facet Color (Warm Stone)", Color) = (0.86, 0.84, 0.79, 1.0)
        _MidColor("Midtone Facet Color", Color) = (0.62, 0.65, 0.68, 1.0)
        _ShadowColor("Shadow Facet Color (Cool Slate)", Color) = (0.36, 0.41, 0.48, 1.0)

        [Header(2 Step Cel Thresholds)]
        _LightThreshold("Main Light Threshold", Range(-0.5, 0.9)) = 0.25
        _ShadowThreshold("Deep Shadow Threshold", Range(-0.8, 0.5)) = -0.15
        _CelSmoothness("Facet Edge Smoothness", Range(0.005, 0.2)) = 0.03

        [Header(Ground Contact AO and Top Accent)]
        _GroundAOColor("Ground Contact AO Color", Color) = (0.24, 0.30, 0.20, 1.0)
        _GroundAOHeight("Ground AO Height", Float) = 0.28
        _TopTintColor("Top Surface Tint", Color) = (0.94, 0.95, 0.88, 1.0)
        _TopTintStrength("Top Tint Strength", Range(0.0, 0.6)) = 0.25

        [Header(Crisp Rim Light)]
        _RimColor("Edge Rim Color", Color) = (0.95, 0.96, 0.90, 1.0)
        _RimPower("Rim Power", Range(1.5, 8.0)) = 4.0
        _RimStrength("Rim Strength", Range(0.0, 0.6)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _LitColor;
            half4 _MidColor;
            half4 _ShadowColor;
            float _LightThreshold;
            float _ShadowThreshold;
            float _CelSmoothness;
            half4 _GroundAOColor;
            float _GroundAOHeight;
            half4 _TopTintColor;
            float _TopTintStrength;
            half4 _RimColor;
            float _RimPower;
            float _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float localY : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.localY = input.positionOS.y;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                // Normalisasi kecerahan tekstur agar variasi antar batu tetap terjaga tanpa terlalu pucat
                half texLuma = dot(texCol, half3(0.299, 0.587, 0.114));
                half stoneVar = lerp(0.88, 1.12, saturate((texLuma - 0.5) * 3.0 + 0.5));

                float3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = dot(normalWS, mainLight.direction) * mainLight.shadowAttenuation;

                // 3-Tone Stylized Cel Shading
                float highBand = smoothstep(_LightThreshold - _CelSmoothness, _LightThreshold + _CelSmoothness, NdotL);
                float midBand = smoothstep(_ShadowThreshold - _CelSmoothness, _ShadowThreshold + _CelSmoothness, NdotL);

                half3 celColor = lerp(_ShadowColor.rgb, _MidColor.rgb, midBand);
                celColor = lerp(celColor, _LitColor.rgb, highBand);
                celColor *= stoneVar * mainLight.color;

                // Highlight halus pada bidang atas batu
                float topMask = saturate(normalWS.y);
                celColor = lerp(celColor, celColor * _TopTintColor.rgb * 1.15, topMask * _TopTintStrength);

                // Ground Contact AO agar pangkal batu menyatu dengan rumput pulau
                float aoMask = saturate(input.localY / max(_GroundAOHeight, 0.001));
                celColor = lerp(_GroundAOColor.rgb, celColor, aoMask);

                // Rim light tipis di tepi siluet
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                celColor += _RimColor.rgb * rim * _RimStrength * aoMask;

                celColor = MixFog(celColor, input.fogFactor);
                return half4(celColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}