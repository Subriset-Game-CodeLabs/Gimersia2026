Shader "Roystan/Toon/Water Tut"
{
    Properties
    {
        [Header(Water Depth Colors)]
        _DepthGradientShallow("Depth Gradient Shallow", Color) = (0.325, 0.807, 0.971, 0.85)
        _DepthGradientDeep("Depth Gradient Deep", Color) = (0.086, 0.407, 1, 0.95)
        _DepthMaxDistance("Depth Maximum Distance", Float) = 2.5

        [Header(Radial Island Vignette)]
        _IslandCenter("Island Center (World XYZ)", Vector) = (0, 0, 0, 0)
        _VignetteColor("Outer Sea Tint", Color) = (0.03, 0.16, 0.55, 0.65)
        _VignetteStart("Vignette Start Radius", Float) = 12.0
        _VignetteEnd("Vignette End Radius", Float) = 35.0

        [Header(Voronoi Cellular Caustics)]
        _VoronoiColor("Voronoi Primary Color", Color) = (0.55, 0.9, 1.0, 0.35)
        _VoronoiShadowColor("Voronoi Secondary Tint", Color) = (0.05, 0.25, 0.75, 0.25)
        _VoronoiScale("Voronoi Scale", Float) = 0.45
        _VoronoiSpeed("Voronoi Morph Speed", Float) = 0.8
        _VoronoiThreshold("Voronoi Web Thickness", Range(0.1, 0.9)) = 0.58

        [Header(Stylized Sparkles)]
        [HDR]_SparkleColor("Sparkle Color", Color) = (1.2, 1.2, 1.1, 1.0)
        _SparkleCutoff("Sparkle Threshold", Range(0.1, 0.95)) = 0.48

        [Header(Shoreline Foam)]
        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        _FoamMaxDistance("Foam Maximum Distance", Float) = 1.1
        _FoamMinDistance("Foam Minimum Distance", Float) = 0.04
        _ShoreWaveAmplitude("Shore Wave Amplitude", Range(0, 2)) = 0.45
        _ShoreWaveFrequency("Shore Wave Frequency", Float) = 6.0
        _ShoreWaveSpeed("Shore Wave Speed", Float) = 2.0

        [Header(Surface Noise and Distortion)]
        _SurfaceNoise("Surface Noise", 2D) = "white" {}
        _SurfaceNoiseScroll("Surface Noise Scroll Amount", Vector) = (0.03, 0.03, 0, 0)
        _SurfaceNoiseCutoff("Surface Noise Cutoff", Range(0, 1)) = 0.75
        _SurfaceDistortion("Surface Distortion", 2D) = "white" {}
        _SurfaceDistortionAmount("Surface Distortion Amount", Range(0, 1)) = 0.35

        [Header(Vertex Waves)]
        _VertexWaveAmplitude("Vertex Wave Amplitude", Range(0, 1)) = 0.08
        _VertexWaveFrequency("Vertex Wave Frequency", Float) = 0.35
        _VertexWaveSpeed("Vertex Wave Speed", Float) = 1.5

        [Header(Day Night Lighting)]
        _NightAmbientColor("Night Ambient Tint", Color) = (0.06, 0.1, 0.22, 1)

		[Header(Interactive Ripple Rings)]
		_RippleColor("Ripple Color", Color) = (0.85, 0.96, 1.0, 0.65)
		_RippleMaxDistance("Ripple Max Distance", Float) = 2.2
		_RippleFrequency("Ripple Frequency", Float) = 2.5
		_RippleSpeed("Ripple Speed", Float) = 1.4
		_RippleWidth("Ripple Line Width", Range(0.02, 0.3)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #define SMOOTHSTEP_AA 0.01

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 alphaBlend(float4 top, float4 bottom)
            {
                float3 color = (top.rgb * top.a) + (bottom.rgb * (1 - top.a));
                float alpha = top.a + bottom.a * (1 - top.a);
                return float4(color, alpha);
            }

            // Hash & Procedural Voronoi 2D untuk pola jaring air gaya Wind Waker
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float voronoi(float2 x, float speed)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float m = 8.0;

                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2((float)i, (float)j);
                        float2 o = hash22(n + g);
                        o = 0.5 + 0.5 * sin(_Time.y * speed + 6.2831 * o);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        m = min(m, d);
                    }
                }
                return sqrt(m);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 noiseUV : TEXCOORD0;
                float2 distortUV : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                float3 viewNormal : NORMAL;
                float3 worldPos : TEXCOORD3;
            };

            sampler2D _SurfaceNoise;
            float4 _SurfaceNoise_ST;
            sampler2D _SurfaceDistortion;
            float4 _SurfaceDistortion_ST;

            float _VertexWaveAmplitude;
            float _VertexWaveFrequency;
            float _VertexWaveSpeed;

            v2f vert (appdata v)
            {
                v2f o;

                // 1. Vertex Wave Displacement (dihitung dalam World Space agar konsisten terhadap skala Plane)
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                float wave1 = sin(worldPos.x * _VertexWaveFrequency + _Time.y * _VertexWaveSpeed);
                float wave2 = cos(worldPos.z * _VertexWaveFrequency * 0.8 + _Time.y * _VertexWaveSpeed * 1.15);
                worldPos.y += (wave1 + wave2) * 0.5 * _VertexWaveAmplitude;

                v.vertex = mul(unity_WorldToObject, worldPos);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPosition = ComputeScreenPos(o.vertex);
                COMPUTE_EYEDEPTH(o.screenPosition.z);
                o.distortUV = TRANSFORM_TEX(v.uv, _SurfaceDistortion);
                o.noiseUV = TRANSFORM_TEX(v.uv, _SurfaceNoise);
                o.viewNormal = COMPUTE_VIEW_NORMAL;
                o.worldPos = worldPos.xyz;

                return o;
            }

            float4 _DepthGradientShallow;
            float4 _DepthGradientDeep;
            float _DepthMaxDistance;

            float4 _IslandCenter;
            float4 _VignetteColor;
            float _VignetteStart;
            float _VignetteEnd;

            float4 _VoronoiColor;
            float4 _VoronoiShadowColor;
            float _VoronoiScale;
            float _VoronoiSpeed;
            float _VoronoiThreshold;

            float4 _SparkleColor;
            float _SparkleCutoff;

            float4 _FoamColor;
            float _FoamMaxDistance;
            float _FoamMinDistance;
            float _ShoreWaveAmplitude;
            float _ShoreWaveFrequency;
            float _ShoreWaveSpeed;

            float _SurfaceNoiseCutoff;
            float _SurfaceDistortionAmount;
            float2 _SurfaceNoiseScroll;

            float4 _MainLightColor;
            float4 _NightAmbientColor;

            sampler2D _CameraDepthTexture;
            sampler2D _CameraNormalsTexture;

			float4 _RippleColor;
			float _RippleMaxDistance;
			float _RippleFrequency;
			float _RippleSpeed;
			float _RippleWidth;

            float4 frag (v2f i) : SV_Target
            {
                // 2. Kalkulasi Kedalaman (Mendukung Orthographic & Perspective)
                float existingDepth01 = tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPosition)).r;
                float existingDepthLinear;
                float waterDepth;

                if (unity_OrthoParams.w > 0.5)
                {
                    #if defined(UNITY_REVERSED_Z)
                        existingDepth01 = 1.0 - existingDepth01;
                    #endif
                    existingDepthLinear = lerp(_ProjectionParams.y, _ProjectionParams.z, existingDepth01);
                    waterDepth = i.screenPosition.z;
                }
                else
                {
                    existingDepthLinear = LinearEyeDepth(existingDepth01);
                    waterDepth = i.screenPosition.w;
                }

                float depthDifference = existingDepthLinear - waterDepth;

                // 3. Warna Dasar Kedalaman Air + Radial Vignette di Sekeliling Pulau
                float waterDepthDifference01 = saturate(depthDifference / _DepthMaxDistance);
                float4 waterColor = lerp(_DepthGradientShallow, _DepthGradientDeep, waterDepthDifference01);

                float distFromIsland = length(i.worldPos.xz - _IslandCenter.xz);
                float vignetteFactor = smoothstep(_VignetteStart, _VignetteEnd, distFromIsland);
                waterColor.rgb = lerp(waterColor.rgb, _VignetteColor.rgb, vignetteFactor * _VignetteColor.a);

                // Distorsi permukaan
                float2 distortSample = (tex2D(_SurfaceDistortion, i.distortUV).xy * 2 - 1) * _SurfaceDistortionAmount;

                // 4. Pola Voronoi Cellular Caustics Dua Lapis
                float2 voronoiUV1 = i.worldPos.xz * _VoronoiScale + distortSample * 1.2 + (_Time.y * _SurfaceNoiseScroll * 2.0);
                float2 voronoiUV2 = i.worldPos.xz * (_VoronoiScale * 0.75) - distortSample * 0.8 + float2(2.3, 4.1) - (_Time.y * _SurfaceNoiseScroll * 1.5);

                float v1 = voronoi(voronoiUV1, _VoronoiSpeed);
                float v2 = voronoi(voronoiUV2, _VoronoiSpeed * 0.7);

                float webPrimary = smoothstep(_VoronoiThreshold - 0.04, _VoronoiThreshold + 0.04, v1);
                float webSecondary = smoothstep(_VoronoiThreshold - 0.06, _VoronoiThreshold + 0.06, v2);

                // Lapisan bayangan Voronoi bawah & lapisan cahaya Voronoi atas
                waterColor.rgb = lerp(waterColor.rgb, _VoronoiShadowColor.rgb, webSecondary * _VoronoiShadowColor.a);
                waterColor.rgb += _VoronoiColor.rgb * (webPrimary * _VoronoiColor.a);

                // 5. Kilauan Cahaya Air (Stylized Sparkles)
                float2 sparkleUV1 = i.noiseUV + (_Time.y * _SurfaceNoiseScroll * 1.3) + distortSample * 0.6;
                float2 sparkleUV2 = (i.noiseUV * 1.4) - (_Time.y * _SurfaceNoiseScroll * 0.9) - distortSample * 0.6;
                float sparkleSample = tex2D(_SurfaceNoise, sparkleUV1).r * tex2D(_SurfaceNoise, sparkleUV2).r;
                float sparkleMask = step(_SparkleCutoff, sparkleSample);
                waterColor.rgb += _SparkleColor.rgb * sparkleMask * _SparkleColor.a;

                // 6. Busa Tepi Pulau Bergelombang
                float3 existingNormal = tex2Dproj(_CameraNormalsTexture, UNITY_PROJ_COORD(i.screenPosition));
                float3 normalDot = saturate(dot(existingNormal, i.viewNormal));
                float foamDistance = lerp(_FoamMaxDistance, _FoamMinDistance, normalDot);

                float shorelineWave = sin(_Time.y * _ShoreWaveSpeed + (i.noiseUV.x - i.noiseUV.y) * _ShoreWaveFrequency) * _ShoreWaveAmplitude;
                float distortedDepth = depthDifference + (distortSample.x * 1.5) + shorelineWave;

                float foamDepthDifference01 = saturate(distortedDepth / foamDistance);
                float surfaceNoiseCutoff = lerp(0.25, _SurfaceNoiseCutoff, foamDepthDifference01);

                float2 noiseUV = float2(
                    (i.noiseUV.x + _Time.y * _SurfaceNoiseScroll.x) + distortSample.x,
                    (i.noiseUV.y + _Time.y * _SurfaceNoiseScroll.y) + distortSample.y
                );
                float surfaceNoiseSample = tex2D(_SurfaceNoise, noiseUV).r;
                float surfaceNoise = smoothstep(surfaceNoiseCutoff - SMOOTHSTEP_AA, surfaceNoiseCutoff + SMOOTHSTEP_AA, surfaceNoiseSample);

                float4 surfaceNoiseColor = _FoamColor;
                surfaceNoiseColor.a *= surfaceNoise;
				
				// 6b. Cincin Riak Meluas di sekitar pulau & musuh yang menyentuh air
				float rippleDepth = depthDifference + (distortSample.x * 0.4);
				float rippleMask = (1.0 - saturate(rippleDepth / _RippleMaxDistance)) * step(0.01, rippleDepth);
				float rippleWave = frac(rippleDepth * _RippleFrequency - _Time.y * _RippleSpeed);
				float rippleRing = smoothstep(0.5 - _RippleWidth, 0.5, rippleWave) * (1.0 - smoothstep(0.5, 0.5 + _RippleWidth, rippleWave));

				float4 rippleColor = _RippleColor;
				rippleColor.a *= rippleRing * rippleMask;
				waterColor = alphaBlend(rippleColor, waterColor);
				
                float4 finalColor = alphaBlend(surfaceNoiseColor, waterColor);

                // 7. Sinkronisasi Cahaya Siang / Malam URP
                float3 lighting = _MainLightColor.rgb + _NightAmbientColor.rgb;
                finalColor.rgb *= lighting;

                return finalColor;
            }
            ENDCG
        }
    }
}