Shader "AccardND/UI/AssassinSilverFilm"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "SilverFilm"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Una lieve rifrazione rompe la silhouette e suggerisce che la luce
                // stia attraversando la pedina, anziche' rimbalzare su del metallo.
                float wave = sin(input.texcoord.y * 34.0 + _Time.y * 2.8)
                           + sin(input.texcoord.y * 71.0 - _Time.y * 1.7) * 0.35;
                float2 refractedUv = input.texcoord + float2(wave * 0.0035, 0.0);
                fixed4 source = (tex2D(_MainTex, refractedUv) + _TextureSampleAdd) * input.color;
                float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));

                // Contorno ricavato dall'alpha della texture: il corpo quasi sparisce,
                // mentre la sagoma resta leggibile come residuo di energia fredda.
                float alphaRight = tex2D(_MainTex, refractedUv + float2(_MainTex_TexelSize.x * 2.0, 0.0)).a;
                float alphaLeft = tex2D(_MainTex, refractedUv - float2(_MainTex_TexelSize.x * 2.0, 0.0)).a;
                float alphaUp = tex2D(_MainTex, refractedUv + float2(0.0, _MainTex_TexelSize.y * 2.0)).a;
                float alphaDown = tex2D(_MainTex, refractedUv - float2(0.0, _MainTex_TexelSize.y * 2.0)).a;
                float alphaNeighbour = min(min(alphaRight, alphaLeft), min(alphaUp, alphaDown));
                float edge = saturate((source.a - alphaNeighbour) * 5.0);

                float3 ghostDark = float3(0.10, 0.18, 0.24);
                float3 ghostLight = float3(0.48, 0.78, 0.92);
                float3 ghost = lerp(ghostDark, ghostLight, saturate(luminance * 0.72 + 0.12));

                // Sottili bande intermittenti fanno percepire la dissolvenza senza
                // trasformare nuovamente la pedina in una superficie lucida piena.
                float scan = pow(saturate(sin(input.texcoord.y * 52.0 - _Time.y * 3.5) * 0.5 + 0.5), 12.0);
                float pulse = sin(_Time.y * 2.1 + input.texcoord.y * 5.0) * 0.5 + 0.5;
                ghost += edge * float3(0.32, 0.72, 0.95) * (0.65 + pulse * 0.35);
                ghost += scan * float3(0.10, 0.24, 0.32);

                float ghostAlpha = source.a * (0.24 + luminance * 0.12 + scan * 0.08);
                ghostAlpha = max(ghostAlpha, edge * source.a * 0.72);
                fixed4 color = fixed4(saturate(ghost), ghostAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
