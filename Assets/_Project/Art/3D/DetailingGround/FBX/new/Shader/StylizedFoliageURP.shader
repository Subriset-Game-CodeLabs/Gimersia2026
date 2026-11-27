Shader "Custom/StylizedFoliageURP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [Header(Vertical AO and Tip Gradient)]
        _RootColor("Root AO Color (Dark Base)", Color) = (0.24, 0.38, 0.14, 1.0)
        _TipColor("Tip Tint Color (Warm Top)", Color) = (0.72, 0.92, 0.34, 1.0)
        _GradientHeight("Mesh Local Height", Float) = 0.65
        _RootPower("Root AO Falloff", Range(0.5, 3.0)) = 1.35

        [Header(Cel Shading and Rim)]
        _ShadowTint("Cel Shadow Tint", Color) = (0.58, 0.72, 0.45, 1.0)
        _CelThreshold("Cel Light Threshold", Range(-0.5, 0.8)) = 0.1
        _CelSmoothness("Cel Edge Smoothness", Range(0.01, 0.5)) = 0.08
        _RimColor("Backlight Rim Color", Color) = (0.85, 1.0, 0.55, 1.0)
        _RimPower("Rim Power", Range(1.0, 8.0)) = 3.5

        [Header(Flower Blue Dot Fix)]
        [Toggle(_REMAP_BLUE_ON)] _RemapBlue("Remap Blue Dots to Warm Blossom", Float) = 0
        _WarmBlossomColor("Replacement Blossom Color", Color) = (1.0, 0.88, 0.42, 1.0)

        [Header(World Space Wind Sway)]
        _WindSpeed("Wind Speed", Float) = 2.2
        _WindStrength("Wind Strength", Float) = 0.07
        _WindFrequency("Wind Spatial Frequency", Float) = 1.35
        _WindDirection("Wind Direction (XZ)", Vector) = (1.0, 0.0, 0.6, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _RootColor;
            half4 _TipColor;
            float _GradientHeight;
            float _RootPower;
            half4 _ShadowTint;
            float _CelThreshold;
            float _CelSmoothness;
            half4 _RimColor;
            float _RimPower;
            half4 _WarmBlossomColor;
            float _WindSpeed;
            float _WindStrength;
            float _WindFrequency;
            float4 _WindDirection;
        CBUFFER_END

        float3 ApplyWindOS(float3 positionOS)
        {
            float3 worldOrigin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            float heightWeight = saturate(positionOS.y / max(_GradientHeight, 0.001));
            // Kuadratkan bobot agar akar terkunci rapat di tanah dan pucuk melengkung lembut
            float bendFactor = heightWeight * heightWeight;

            float phase = (worldOrigin.x + worldOrigin.z) * _WindFrequency + positionOS.y * 1.5;
            float primaryWave = sin(_Time.y * _WindSpeed + phase);
            float gustWave = cos(_Time.y * (_WindSpeed * 1.7) + phase * 0.7) * 0.35;
            float sway = (primaryWave + gustWave) * _WindStrength * bendFactor;

            float2 windDir = normalize(_WindDirection.xz + float2(1e-5, 1e-5));
            positionOS.x += windDir.x * sway;
            positionOS.z += windDir.y * sway;
            positionOS.y -= abs(sway) * 0.25;
            return positionOS;
        }
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
            #pragma shader_feature_local _REMAP_BLUE_ON

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
                float height01 : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.height01 = saturate(input.positionOS.y / max(_GradientHeight, 0.001));
                float3 displacedOS = ApplyWindOS(input.positionOS.xyz);

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                #if defined(_REMAP_BLUE_ON)
                    // Deteksi piksel biru pekat dari UV ColorPalette_256 dan ubah ke warna bunga hangat
                    half isBlue = step(texCol.r * 1.4, texCol.b) * step(texCol.g * 1.15, texCol.b);
                    texCol.rgb = lerp(texCol.rgb, _WarmBlossomColor.rgb, isBlue);
                #endif

                // Gradasi vertikal dari pangkal gelap (menyatu dengan tanah) ke ujung terang
                half gradT = pow(input.height01, _RootPower);
                half3 gradientTint = lerp(_RootColor.rgb, _TipColor.rgb, gradT);
                half3 albedo = texCol.rgb * gradientTint * 1.65;

                // Normal dua sisi yang sedikit diarahkan ke atas agar daun tidak terlihat gelap sebelah
                float3 normalWS = normalize(input.normalWS * facing + float3(0.0, 0.45, 0.0));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = dot(normalWS, mainLight.direction);
                float celFactor = smoothstep(_CelThreshold - _CelSmoothness, _CelThreshold + _CelSmoothness, NdotL);
                float lightMask = celFactor * mainLight.shadowAttenuation;

                half3 litColor = albedo * mainLight.color;
                half3 shadowColor = albedo * _ShadowTint.rgb;
                half3 color = lerp(shadowColor, litColor, lightMask);

                // Rim light lembut di pucuk daun
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                color += _RimColor.rgb * rim * input.height01 * 0.25 * mainLight.color;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
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
            Cull Off

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

                float3 displacedOS = ApplyWindOS(input.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(displacedOS);
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