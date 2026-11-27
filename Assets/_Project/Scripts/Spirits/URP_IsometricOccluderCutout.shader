Shader "Custom/URP_IsometricOccluderCutout"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowStrength("Shadow Brightness", Range(0.0, 1.0)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _ShadowStrength;
            CBUFFER_END

            // Variabel global dari SpiritOcclusionController.cs
            float4 _SpiritCutoutBottoms[3];
            float4 _SpiritCutoutTops[3];
            float _SpiritCutoutRadii[3];

            // Menghitung jarak terdekat titik P ke segmen kapsul [A, B]
            float DistanceToSegment(float3 p, float3 a, float3 b)
            {
                float3 ab = b - a;
                float lenSq = max(dot(ab, ab), 0.0001);
                float t = saturate(dot(p - a, ab) / lenSq);
                float3 closest = a + ab * t;
                return length(p - closest);
            }

            // Proyeksi garis pandang kamera agar objek di depan Spirit juga terpotong sesuai kapsul Spirit
            float DistanceToViewCapsule(float3 pixelWS, float3 camWS, float3 spiritBottom, float3 spiritTop)
            {
                float3 spiritCenter = (spiritBottom + spiritTop) * 0.5;
                float3 camToSpirit = spiritCenter - camWS;
                float spiritDist = length(camToSpirit);
                float3 viewDir = camToSpirit / max(spiritDist, 0.0001);

                float pixelDepth = dot(pixelWS - camWS, viewDir);
                // Hanya potong objek yang berada di antara kamera dan Spirit (dengan toleransi 0.5m di belakang Spirit)
                if (pixelDepth > spiritDist + 0.5 || pixelDepth < 0.0)
                    return 9999.0;

                float scaleRatio = saturate(pixelDepth / max(spiritDist, 0.0001));
                float3 projectedBottom = lerp(camWS, spiritBottom, scaleRatio);
                float3 projectedTop = lerp(camWS, spiritTop, scaleRatio);
                return DistanceToSegment(pixelWS, projectedBottom, projectedTop);
            }

            // Pola dither 4x4 Bayer untuk efek tembus pandang halus tanpa sorting bug
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(posInputs);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(texColor.a - _Cutoff);

                // Evaluasi cutout kapsul untuk ke-3 Spirit
                float3 camWS = GetCameraPositionWS();
                float minMask = 1.0;

                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float radius = _SpiritCutoutRadii[i];
                    if (radius > 0.001)
                    {
                        float dist = DistanceToViewCapsule(
                            input.positionWS,
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
                    float threshold = BayerDither4x4(input.positionCS.xy);
                    clip(minMask - threshold);
                }

                Light mainLight = GetMainLight(input.shadowCoord);
                half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half lighting = lerp(_ShadowStrength, 1.0, NdotL * mainLight.shadowAttenuation);

                return half4(texColor.rgb * mainLight.color * lighting, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}