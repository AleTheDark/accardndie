Shader "AccardND/VFX/Empower Dice Result Mask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Name "ResultStencilMask"
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Cull Back
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Piccola espansione per proteggere il valore anche dalle particelle
                // che sfiorano la faccia. Scrive soltanto nello stencil buffer.
                float3 positionOS = input.positionOS.xyz + input.normalOS * 0.045;
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
