Shader "AccardND/VFX/Necromancer Soul Wisp Additive"
{
    Properties
    {
        _MainTex ("Soul Wisp RGBA", 2D) = "white" {}
        [HDR] _Color ("Energy Tint", Color) = (0.72,1.5,0.9,1)
        _Intensity ("Intensity", Range(0,4)) = 1.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+110" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Additivo in forma premoltiplicata: l'energia e' gia' moltiplicata per
        // l'alpha e l'alpha in uscita resta 0, cosi' la luce si somma al tavolo
        // quando la RenderTexture viene composta invece di coprirlo.
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = tex.a * input.color.a;
                clip(alpha - 0.002h);
                half3 energy = tex.rgb * _Color.rgb * input.color.rgb * _Intensity * alpha;
                return half4(energy, 0.0h);
            }
            ENDHLSL
        }
    }
}
