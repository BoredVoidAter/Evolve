Shader "Rendering/IDAndNormals"
{
    Properties
    {
        _EncodedSubjectID ("Encoded Subject ID", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "IDAndNormals"
            
            // Just required for structural stability
            ZWrite Off
            ZTest Always // Ensure it always draws over the active depth buffer
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            // Expose the variable properly for the SRP batcher to avoid black output
            CBUFFER_START(UnityPerMaterial)
                float4 _EncodedSubjectID;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            struct FragmentOutput
            {
                float4 idMap : SV_Target0;
                float4 normalMap : SV_Target1;
            };

            FragmentOutput frag(Varyings input)
            {
                FragmentOutput output;
                output.idMap = _EncodedSubjectID;
                
                // Normalize to defend against transform scaling 
                output.normalMap = float4(normalize(input.normalWS) * 0.5 + 0.5, 1.0);
                return output;
            }
            ENDHLSL
        }
    }
}
