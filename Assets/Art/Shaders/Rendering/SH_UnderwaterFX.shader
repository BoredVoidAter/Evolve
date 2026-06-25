Shader "Rendering/UnderwaterFX"
{
    Properties
    {
        [Header(Colors)]
        _WaterColor ("Water Tint", Color) = (0.1, 0.55, 0.75, 1)
        _DepthColor ("Abyss Fog Color", Color) = (0.01, 0.05, 0.15, 1)
        
        [Header(Cross Section)]
        _CutawayDarkness ("Cutaway Darkness", Range(0, 1)) = 0.25
        _CutawayTolerance ("Solid Cutaway Tolerance", Float) = 0.4
        _DitherBand ("Dither Transition Size", Float) = 0.8

        [Header(Pixelation)]
        _FXResolution ("FX Pixel Resolution", Float) = 150.0
        
        [Header(Pixel Caustics)]
        _CausticIntensity ("Caustic Brightness", Float) = 0.5
        _CausticScale ("Caustic Scale (World)", Float) = 0.4
        _CausticSpeed ("Caustic Speed", Float) = 1.0
        _CausticCutoff ("Caustic Hard Cutoff", Range(0, 1)) = 0.7
        
        [Header(God Rays)]
        _RayIntensity ("Ray Brightness", Float) = 0.2
        _RayWidth ("Ray Width", Float) = 12.0
        _RaySpeed ("Ray Speed", Float) = 1.2
        _RayCutoff ("Ray Hard Cutoff", Range(-2, 2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "UnderwaterFX"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _DepthColor;
                
                float _CutawayDarkness;
                float _CutawayTolerance;
                float _DitherBand;
                
                float _FXResolution;
                
                float _CausticIntensity;
                float _CausticScale;
                float _CausticSpeed;
                float _CausticCutoff;
                
                float _RayIntensity;
                float _RayWidth;
                float _RaySpeed;
                float _RayCutoff;
            CBUFFER_END

            float _GlobalZoom; 
            float _WaterLevel;

            float getCaustics(float2 uv, float time)
            {
                float v1 = sin(uv.x + time) * cos(uv.y - time);
                float v2 = sin(uv.x * 0.7 - time * 0.8) * cos(uv.y * 1.3 + time * 0.5);
                float v3 = sin(uv.x * 1.5 + time * 1.2) * cos(uv.y * 0.5 - time * 1.5);
                return 1.0 - abs((v1 + v2 + v3) / 3.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 pixelUV = uv;
                pixelUV.x *= aspect;
                pixelUV = floor(pixelUV * _FXResolution) / _FXResolution;
                pixelUV.x /= aspect;

                float rawDepth = SampleSceneDepth(pixelUV);
                float3 worldPos = ComputeWorldSpacePosition(pixelUV, rawDepth, UNITY_MATRIX_I_VP);

                bool isSky = false;
                #if UNITY_REVERSED_Z
                    if (rawDepth < 0.00001) isSky = true;
                #else
                    if (rawDepth > 0.99999) isSky = true;
                #endif

                // ------------------------------------------------------------------
                // DITHERED CROSS-SECTION LOGIC
                // ------------------------------------------------------------------
                // We expand the detection slightly downwards to hide zoom artifacts,
                // and then create a gradient band for the dither transition.
                float cutawayStart = _WaterLevel - _CutawayTolerance;
                float safeDitherBand = max(0.001, _DitherBand); // Prevent divide by zero
                float ditherStart = cutawayStart - safeDitherBand;

                // 0.0 means fully underwater, 1.0 means fully cutaway (dirt)
                float cutawayGradient = saturate((worldPos.y - ditherStart) / safeDitherBand);
                
                // Construct a 4x4 Bayer Matrix for perfect pixel-art dithering
                uint px = (uint)(pixelUV.x * aspect * _FXResolution);
                uint py = (uint)(pixelUV.y * _FXResolution);
                int bayerIndex = (py % 4) * 4 + (px % 4);
                
                float bayer[16] = {
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                
                float ditherThreshold = bayer[bayerIndex];

                // If the height gradient beats the dither matrix, it becomes dirt.
                if (!isSky && cutawayGradient > ditherThreshold)
                {
                    color.rgb *= _CutawayDarkness;
                }
                else
                {
                    // ------------------------------------------------------------------
                    // NORMAL UNDERWATER FX
                    // ------------------------------------------------------------------
                    color.rgb *= _WaterColor.rgb * 1.5;

                    float depthMask = smoothstep(-15.0, 5.0, worldPos.y);
                    color.rgb *= lerp(0.5, 1.0, depthMask); 

                    // Caustics
                    float2 cUV = worldPos.xz * _CausticScale;
                    float causticVal = getCaustics(cUV, _Time.y * _CausticSpeed);
                    float caustic = step(_CausticCutoff, causticVal); 
                    color.rgb += caustic * _CausticIntensity * _WaterColor.rgb * depthMask;

                    // God Rays
                    float2 rayUV = pixelUV;
                    rayUV.x *= aspect;
                    rayUV *= (max(_GlobalZoom, 1.0) / 15.0); 
                    
                    float rayCoord = rayUV.x + rayUV.y * 0.6;
                    float rTime = _Time.y * _RaySpeed;
                    float rayVal = sin(rayCoord * _RayWidth + rTime) + cos(rayCoord * _RayWidth * 0.6 - rTime * 1.2);
                    
                    float ray = step(_RayCutoff, rayVal); 
                    color.rgb += ray * _RayIntensity * _WaterColor.rgb * depthMask;

                    // Abyss Fog
                    float finalFog = isSky ? 1.0 : (1.0 - smoothstep(-15.0, 0.0, worldPos.y));
                    color.rgb = lerp(color.rgb, _DepthColor.rgb, finalFog);
                }

                return color;
            }
            ENDHLSL
        }
    }
}
