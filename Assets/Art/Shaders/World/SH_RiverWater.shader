Shader "Custom/RiverWater"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.2, 0.5, 0.8, 0.9)
        _FoamColor ("Foam Color", Color) = (0.9, 0.95, 1.0, 1.0)
        _FlowSpeed ("Flow Speed", Float) = 2.0
        _NoiseScale ("Noise Scale", Float) = 4.0
        _PixelResolution ("Pixel Resolution", Float) = 12.0 // Controls how pixelated the river is
        _ColorSteps ("Color Steps", Float) = 4.0 // NEW: Controls the number of possible color bands
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;       
                float3 flowDir : TEXCOORD1;  
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 flowDir : TEXCOORD2;
            };

            float4 _WaterColor;
            float4 _FoamColor;
            float _FlowSpeed;
            float _NoiseScale;
            float _PixelResolution;
            float _ColorSteps;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.flowDir = IN.flowDir;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 flow = normalize(IN.flowDir + float3(0.0001, 0.0, 0.0001));
                
                float dist = dot(IN.positionWS, flow);
                
                float3 up = abs(flow.y) > 0.9 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 right = normalize(cross(flow, up));
                float crossDist = dot(IN.positionWS, right);
                
                float2 scrolledPos = float2(crossDist, dist - _Time.y * _FlowSpeed);
                
                // --- PIXELIZE THE NOISE COORDINATES ---
                scrolledPos = floor(scrolledPos * _PixelResolution) / _PixelResolution;
                
                float n1 = sin(scrolledPos.x * _NoiseScale) * cos(scrolledPos.y * _NoiseScale);
                float n2 = sin(scrolledPos.x * _NoiseScale * 1.5 + 2.0) * cos(scrolledPos.y * _NoiseScale * 1.5);
                float noise = (n1 + n2) * 0.5;
                
                float foamMask = smoothstep(0.3, 0.6, noise);
                
                // --- PIXELIZE THE EDGES ---
                float pixelUVx = floor(IN.uv.x * _PixelResolution) / _PixelResolution;
                float edgeFoam = pow(abs(pixelUVx - 0.5) * 2.0, 6.0);
                
                foamMask = saturate(foamMask + edgeFoam);
                
                // --- QUANTIZE THE COLORS (STEPPED SHADING) ---
                float steps = max(1.0, _ColorSteps - 1.0);
                foamMask = round(foamMask * steps) / steps;
                
                return lerp(_WaterColor, _FoamColor, foamMask);
            }
            ENDHLSL
        }
    }
}
