Shader "Custom/Water"
{
    Properties
    {
        _ShallowColor("Shallow Water Color", Color) = (0.2, 0.6, 0.9, 0.6)
        _DeepColor("Deep Water Color", Color) = (0.1, 0.3, 0.7, 0.8)
        _MaxDepth("Max Depth", Float) = 3.0
        _DepthBands("Depth Bands", Float) = 4.0

        [Header(Pixel Foam)]
        _FoamColor("Foam Color", Color) = (1.0, 1.0, 1.0, 0.9)
        _FoamDepth("Foam Intersection Depth", Float) = 0.6
        _FoamNoiseScale("Foam Noise Scale", Float) = 3.0
        _FoamSpeed("Foam Speed", Float) = 2.0
        _FoamSlopeMask("Exclude Flat Surfaces", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Required for depth sampling and world position reconstruction
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _MaxDepth;
                float _DepthBands;
                
                half4 _FoamColor;
                float _FoamDepth;
                float _FoamNoiseScale;
                float _FoamSpeed;
                float _FoamSlopeMask;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                
                // Calculate screen position for depth texture sampling
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            // Procedural wavy noise function for organic foam movement
            float getFoamNoise(float2 uv, float time)
            {
                float v1 = sin(uv.x * 1.5 + time) * cos(uv.y * 0.8 - time * 0.5);
                float v2 = sin(uv.x * 0.5 - time * 0.8) * cos(uv.y * 1.2 + time * 1.1);
                return (v1 + v2) * 0.5 + 0.5; // Normalized 0 to 1
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Calculate screen UVs
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Sample raw depth from the scene
                #if UNITY_REVERSED_Z
                    real rawDepth = SampleSceneDepth(screenUV);
                #else
                    real rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
                #endif

                // Reconstruct world position of the terrain underneath the water
                float3 scenePosWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                // Because terrain is extruded vertically on your hex grid, true depth is just the difference in Y
                float depth = max(0, i.positionWS.y - scenePosWS.y);

                // Normalize depth between 0 and _MaxDepth
                float depthFactor = saturate((depth) / _MaxDepth);

                // Stepped bands to maintain the distinct pixel art stylized aesthetic (instead of smooth gradients)
                depthFactor = floor(depthFactor * _DepthBands) / _DepthBands;

                // Interpolate between shallow and deep colors based on our stepped depth
                half4 finalColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                // =========================================================
                // Apply the pixelized dither foam over the water
                // =========================================================
                
                // Reconstruct the terrain normal using screen-space derivatives of the world position
                float3 ddxPos = ddx(scenePosWS);
                float3 ddyPos = ddy(scenePosWS);
                float3 sceneNormalWS = normalize(cross(ddyPos, ddxPos));
                
                // Calculate slope mask (0.0 = completely flat/upward facing, 1.0 = steep slope/cliff)
                // We use smoothstep to create a hard cut-off determined by _FoamSlopeMask
                float slopeMask = smoothstep(_FoamSlopeMask, _FoamSlopeMask - 0.1, abs(sceneNormalWS.y));

                // 1. Calculate shore proximity (1.0 = right at shore, 0.0 = deeper than _FoamDepth)
                float shoreFactor = 1.0 - saturate(depth / max(0.001, _FoamDepth));
                
                // Mask out the shore factor on upward facing flat surfaces
                shoreFactor *= slopeMask;

                // 2. Generate pixelated world-space noise for organic foam blocks
                float2 noiseUV = i.positionWS.xz * _FoamNoiseScale;
                noiseUV = floor(noiseUV); // Floor coordinates to lock noise into pixelated chunks
                float noiseVal = getFoamNoise(noiseUV, _Time.y * _FoamSpeed);

                // 3. Combine shore proximity with the wavy noise
                float foamGradient = saturate(shoreFactor * noiseVal * 1.5);

                // 4. Construct a 4x4 Bayer Matrix to convert the gradient into screen-space pixel dither
                uint px = (uint)(screenUV.x * _ScreenParams.x);
                uint py = (uint)(screenUV.y * _ScreenParams.y);
                int bayerIndex = (py % 4) * 4 + (px % 4);
                
                float bayer[16] = {
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                float ditherThreshold = bayer[bayerIndex];

                // 5. Apply the foam wherever the noise + shore gradient beats the dither matrix
                if (foamGradient > ditherThreshold && shoreFactor > 0.0)
                {
                    finalColor.rgb = lerp(finalColor.rgb, _FoamColor.rgb, _FoamColor.a);
                    finalColor.a = max(finalColor.a, _FoamColor.a);
                }

                return finalColor;
            }
            ENDHLSL
        }
    }
}
