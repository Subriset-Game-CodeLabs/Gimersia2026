Shader "Supyrb/Toon/IsometricDioramaURP"
{
    Properties
    {
        [HDR]_Color("Albedo", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Cel Shading)]
        _ShadowColor ("Shadow Tint", Color) = (0.65, 0.7, 0.85, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1.0, 1.0)) = 0.0
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 0.5)) = 0.02

        [Header(Additional Lights (Point Lights))]
        _AdditionalLightIntensity ("Point Light Intensity", Range(0.0, 3.0)) = 1.15

        [Header(Vertical Gradient Fake AO)]
        _GradientBottomColor ("Bottom AO Tint", Color) = (0.55, 0.6, 0.75, 1)
        _GradientMinY ("Gradient Bottom Y (Local)", Float) = 0.0
        _GradientMaxY ("Gradient Top Y (Local)", Float) = 1.5

        [Header(Rim Light)]
        [HDR]_RimColor ("Rim Color (Alpha 0 = Off)", Color) = (1, 1, 1, 0)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.15, 0.15, 0.2, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.004

        [Header(Spirit Occluder Cutout)]
        [Toggle] _EnableOccluderCutout ("Enable Spirit See-Through Cutout", Float) = 0
       
        [Header(Stencil)]
        _Stencil ("Stencil ID [0;255]", Float) = 0
        _ReadMask ("ReadMask [0;255]", Int) = 255
        _WriteMask ("WriteMask [0;255]", Int) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Int) = 0
       
        [Header(Rendering)]
        _Offset("Offset", float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Int) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Int) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Int) = 4
        [Enum(None,0,Alpha,1,Red,8,Green,4,Blue,2,RGB,14,RGBA,15)] _ColorMask("Color Mask", Int) = 15
    }
   
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }

        Stencil
        {
            Ref [_Stencil]
            ReadMask [_ReadMask]
            WriteMask [_WriteMask]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _ShadowColor;
            float _ShadowThreshold;
            float _ShadowSmoothness;
            float _AdditionalLightIntensity;
            half4 _GradientBottomColor;
            float _GradientMinY;
            float _GradientMaxY;
            half4 _RimColor;
            float _RimPower;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _EnableOccluderCutout;
        CBUFFER_END

        // Variabel global yang dikirim oleh SpiritOcclusionController.cs
        float4 _SpiritCutoutBottoms[3];
        float4 _SpiritCutoutTops[3];
        float _SpiritCutoutRadii[3];

        float DistanceToSegment(float3 p, float3 a, float3 b)
        {
            float3 ab = b - a;
            float lenSq = max(dot(ab, ab), 0.0001);
            float t = saturate(dot(p - a, ab) / lenSq);
            float3 closest = a + ab * t;
            return length(p - closest);
        }

        float DistanceToViewCapsule(float3 pixelWS, float3 camWS, float3 spiritBottom, float3 spiritTop)
        {
            float3 spiritCenter = (spiritBottom + spiritTop) * 0.5;
            float3 camToSpirit = spiritCenter - camWS;
            float spiritDist = length(camToSpirit);
            float3 viewDir = camToSpirit / max(spiritDist, 0.0001);

            float pixelDepth = dot(pixelWS - camWS, viewDir);
            if (pixelDepth > spiritDist + 0.5 || pixelDepth < 0.0)
                return 9999.0;

            float scaleRatio = saturate(pixelDepth / max(spiritDist, 0.0001));
            float3 projectedBottom = lerp(camWS, spiritBottom, scaleRatio);
            float3 projectedTop = lerp(camWS, spiritTop, scaleRatio);
            return DistanceToSegment(pixelWS, projectedBottom, projectedTop);
        }

        float BayerDither4x4(float2 screenPixel)
        {
            int x = int(fmod(screenPixel.x, 4.0));
            int y = int(fmod(screenPixel.y, 4.0));
            int index = y * 4 + x;
            const float bayer[16] = {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };
            return bayer[index];
        }

        void ApplySpiritOccluderCutout(float3 positionWS, float2 screenPixel)
        {
            if (_EnableOccluderCutout < 0.5)
                return;

            float3 camWS = GetCameraPositionWS();
            float minMask = 1.0;

            [unroll]
            for (int i = 0; i < 3; i++)
            {
                float radius = _SpiritCutoutRadii[i];
                if (radius > 0.001)
                {
                    float dist = DistanceToViewCapsule(
                        positionWS,
                        camWS,
                        _SpiritCutoutBottoms[i].xyz,
                        _SpiritCutoutTops[i].xyz
                    );
                    float mask = smoothstep(radius * 0.65, radius, dist);
                    minMask = min(minMask, mask);
                }
            }

            if (minMask < 0.99)
            {
                float threshold = BayerDither4x4(screenPixel);
                clip(minMask - threshold);
            }
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float localY : TEXCOORD3;
        };
        ENDHLSL
       
        // Pass 1: Main Toon + Additional Point Lights + Fake AO + Rim Light + Spirit Cutout
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull [_Culling]
            Offset [_Offset], [_Offset]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            ColorMask [_ColorMask]
           
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.localY = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ApplySpiritOccluderCutout(input.positionWS, input.positionCS.xy);

                // 1. Base Texture & Albedo
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                // 2. Vertical Gradient (Fake AO di bagian bawah objek)
                float heightRange = max(0.0001, _GradientMaxY - _GradientMinY);
                float gradFactor = saturate((input.localY - _GradientMinY) / heightRange);
                half3 colWithAO = lerp(texCol.rgb * _GradientBottomColor.rgb, texCol.rgb, gradFactor);

                // 3. 2-Tone Cel Shading (Mengikuti Main Directional Light URP)
                Light mainLight = GetMainLight();
                float3 normal = normalize(input.normalWS);
                float NdotL = dot(normal, mainLight.direction);
                float celIntensity = smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    NdotL
                );
                half3 litCol = colWithAO * mainLight.color;
                half3 shadowCol = colWithAO * _ShadowColor.rgb;
                half3 finalCol = lerp(shadowCol, litCol, celIntensity);

                // 4. Additional Lights (Point Light dari Pohon Elemen & Spirit)
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                    half3 additionalLightsCol = half3(0.0, 0.0, 0.0);

                    #if defined(LIGHT_LOOP_BEGIN)
                        InputData inputData = (InputData)0;
                        inputData.positionWS = input.positionWS;
                        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                        uint pixelLightCount = GetAdditionalLightsCount();
                        LIGHT_LOOP_BEGIN(pixelLightCount)
                            Light light = GetAdditionalLight(lightIndex, input.positionWS);
                            float addNdotL = saturate(dot(normal, light.direction));
                            float softFalloff = smoothstep(0.0, 0.15, light.distanceAttenuation) * light.distanceAttenuation;
                            additionalLightsCol += light.color * (softFalloff * lerp(0.55, 1.0, addNdotL));
                        LIGHT_LOOP_END
                    #else
                        uint pixelLightCount = GetAdditionalLightsCount();
                        for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                        {
                            Light light = GetAdditionalLight(lightIndex, input.positionWS);
                            float addNdotL = saturate(dot(normal, light.direction));
                            float softFalloff = smoothstep(0.0, 0.15, light.distanceAttenuation) * light.distanceAttenuation;
                            additionalLightsCol += light.color * (softFalloff * lerp(0.55, 1.0, addNdotL));
                        }
                    #endif

                    finalCol += colWithAO * additionalLightsCol * _AdditionalLightIntensity;
                #endif

                // 5. Rim Light (Aktif jika Alpha pada Rim Color > 0)
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = 1.0 - saturate(dot(viewDir, normal));
                float rimFactor = pow(rim, _RimPower) * _RimColor.a;
                finalCol += _RimColor.rgb * rimFactor;

                return half4(finalCol, texCol.a);
            }
            ENDHLSL
        }

        // Pass 2: Inverted Hull Outline + Spirit Cutout
        Pass
        {
            Name "OutlinePass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front
            ZWrite On
            ZTest [_ZTest]
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            Varyings vertOutline(Attributes input)
            {
                Varyings output;
                float3 extrudedPos = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(extrudedPos);
                output.uv = input.uv;
                output.normalWS = float3(0, 0, 0);
                output.positionWS = TransformObjectToWorld(extrudedPos);
                output.localY = 0;
                return output;
            }

            half4 fragOutline(Varyings input) : SV_Target
            {
                ApplySpiritOccluderCutout(input.positionWS, input.positionCS.xy);
                return _OutlineColor;
            }
            ENDHLSL
        }
       
        // Pass 3: Shadow Caster bawaan URP
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"

        // Pass 4: Depth Only agar objek tercatat di _CameraDepthTexture (memicu busa air)
        UsePass "Universal Render Pipeline/Lit/DepthOnly"

        // Pass 5: Depth Normals agar arah permukaan objek tercatat di _CameraNormalsTexture
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}