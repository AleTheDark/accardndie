Shader "AccardND/VFX/Necromancer Terrain Transparent"
{
    Properties
    {
        _MainTex ("Terrain RGBA", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Necrotic Glow", Color) = (0.08,0.55,0.22,1)
        _GlowStrength ("Glow Strength", Range(0,2)) = 0.28
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Premoltiplicato: il fragment moltiplica gia' l'RGB per l'alpha. Con
        // SrcAlpha/OneMinusSrcAlpha la RenderTexture usciva con alpha al quadrato
        // e la RawImage rimoltiplicava, spegnendo tutta la foschia semitrasparente.
        Blend One OneMinusSrcAlpha
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
                half4 _GlowColor;
                float _GlowStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(tex.a - 0.003h);
                half greenMask = saturate(tex.g - max(tex.r, tex.b) * 0.42h);
                tex.rgb += _GlowColor.rgb * greenMask * _GlowStrength;
                tex.rgb *= tex.a;
                return tex;
            }
            ENDHLSL
        }
    }
}
