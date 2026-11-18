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
            half4 _GradientBottomColor;
            float _GradientMinY;
            float _GradientMaxY;
            half4 _RimColor;
            float _RimPower;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

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
       
        // Pass 1: Main Toon + Fake AO + Rim Light (URP Forward)
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

                // 4. Rim Light (Aktif jika Alpha pada Rim Color > 0)
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = 1.0 - saturate(dot(viewDir, normal));
                float rimFactor = pow(rim, _RimPower) * _RimColor.a;
                finalCol += _RimColor.rgb * rimFactor;

                return half4(finalCol, texCol.a);
            }
            ENDHLSL
        }

        // Pass 2: Inverted Hull Outline (Menggunakan SRPDefaultUnlit agar otomatis dirender URP)
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
                output.positionWS = float3(0, 0, 0);
                output.localY = 0;
                return output;
            }

            half4 fragOutline(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
       
        // Pass 3: Shadow Caster bawaan URP
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}