Shader "Rendering/IDAndNormals"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "IDAndNormals"

            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            struct FragmentOutput
            {
                float4 idMap     : SV_Target0;
                float4 normalMap : SV_Target1;
            };

            FragmentOutput frag(Varyings input)
            {
                FragmentOutput output;
                output.idMap = input.color;
                output.normalMap = float4(normalize(input.normalWS) * 0.5 + 0.5, 1.0);
                return output;
            }
            ENDHLSL
        }
    }
}
