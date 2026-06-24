Shader "Rendering/UnderwaterFX"
{
    Properties
    {
        [Header(Colors)]
        _WaterColor ("Water Tint", Color) = (0.1, 0.55, 0.75, 1)
        
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

            // Supplied automatically by your IsometricCameraController.cs script
            float _GlobalZoom; 

            // Intersecting sine waves (creates complex overlapping shapes without noise artifacts)
            float getCaustics(float2 uv, float time)
            {
                float v1 = sin(uv.x + time) * cos(uv.y - time);
                float v2 = sin(uv.x * 0.7 - time * 0.8) * cos(uv.y * 1.3 + time * 0.5);
                float v3 = sin(uv.x * 1.5 + time * 1.2) * cos(uv.y * 0.5 - time * 1.5);
                
                float v = (v1 + v2 + v3) / 3.0;
                return 1.0 - abs(v); // Peaks at 1.0
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                // Keep the base game render crisp and un-pixelated
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // ==========================================
                // 1. MASTER PIXEL GRID SETUP
                // ==========================================
                // We create a chunky screen grid. By using this to sample everything else, 
                // the FX overlay becomes perfectly pixelated.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 pixelUV = uv;
                pixelUV.x *= aspect; // Adjust for screen stretch
                pixelUV = floor(pixelUV * _FXResolution) / _FXResolution;
                pixelUV.x /= aspect; // Return to normalized 0..1 space

                // ==========================================
                // 2. RECONSTRUCT WORLD SPACE (Snaps to Pixel Grid)
                // ==========================================
                float rawDepth = SampleSceneDepth(pixelUV);
                float3 worldPos = ComputeWorldSpacePosition(pixelUV, rawDepth, UNITY_MATRIX_I_VP);

                // ==========================================
                // 3. BASE WATER TINT & DEPTH SHADING
                // ==========================================
                color.rgb *= _WaterColor.rgb * 1.5;

                // Slightly darken the deeper hexes so it doesn't look completely flat
                float depthMask = smoothstep(-15.0, 5.0, worldPos.y);
                color.rgb *= lerp(0.5, 1.0, depthMask); 

                // ==========================================
                // 4. CHUNKY PIXEL CAUSTICS (World Space)
                // ==========================================
                // Because worldPos was calculated using the pixelUV, these automatically 
                // snap to the chunky screen pixels.
                // Note: Because it's mapped to worldPos.xz, it inherently scales perfectly
                // with your camera zoom without needing the _GlobalZoom parameter.
                float2 cUV = worldPos.xz * _CausticScale;
                float cTime = _Time.y * _CausticSpeed;
                
                float causticVal = getCaustics(cUV, cTime);
                
                // Hard Cutoff: Converts the smooth waves into solid on/off pixels
                float caustic = step(_CausticCutoff, causticVal); 
                
                color.rgb += caustic * _CausticIntensity * _WaterColor.rgb * depthMask;

                // ==========================================
                // 5. CHUNKY PIXEL GOD RAYS (Screen Space)
                // ==========================================
                float2 rayUV = pixelUV;
                rayUV.x *= aspect;
                
                // Here we bind the screen-space rays to the camera's _GlobalZoom. 
                // As you zoom in, the rays scale up to match the world visually.
                float currentZoom = max(_GlobalZoom, 1.0);
                rayUV *= (currentZoom / 15.0); 
                
                float rayCoord = rayUV.x + rayUV.y * 0.6; // Diagonal shafts
                float rTime = _Time.y * _RaySpeed;
                float rayVal = sin(rayCoord * _RayWidth + rTime) + cos(rayCoord * _RayWidth * 0.6 - rTime * 1.2);
                
                // Hard Cutoff: Solid pixels only
                float ray = step(_RayCutoff, rayVal); 

                // Add God Rays (masked slightly by depth so they don't flood the deepest areas)
                color.rgb += ray * _RayIntensity * _WaterColor.rgb * depthMask;

                return color;
            }
            ENDHLSL
        }
    }
}
