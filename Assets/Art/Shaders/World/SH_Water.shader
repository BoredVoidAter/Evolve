Shader "Custom/Water"
{
    Properties
    {
        _ShallowColor("Shallow Water Color", Color) = (0.2, 0.6, 0.9, 0.6)
        _DeepColor("Deep Water Color", Color) = (0.1, 0.3, 0.7, 0.8)
        _MaxDepth("Max Depth", Float) = 3.0
        _DepthBands("Depth Bands", Float) = 4.0
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

                // Apply the foam over the water

                return finalColor;
            }
            ENDHLSL
        }
    }
}
