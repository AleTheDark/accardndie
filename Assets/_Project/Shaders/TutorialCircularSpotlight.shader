Shader "AccardND/UI/Tutorial Circular Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0,0,0,1)
        _HoleCenter ("Hole Center", Vector) = (0.5,0.5,0,0)
        _HoleRadius ("Hole Radius", Float) = 0
        _HoleCenter2 ("Second Hole Center", Vector) = (0.5,0.5,0,0)
        _HoleRadius2 ("Second Hole Radius", Float) = 0
        _Aspect ("Rect Aspect", Float) = 1
        _Feather ("Edge Feather", Float) = 0.018
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            float4 _HoleCenter;
            float _HoleRadius;
            float4 _HoleCenter2;
            float _HoleRadius2;
            float _Aspect;
            float _Feather;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 delta = input.uv - _HoleCenter.xy;
                delta.x *= _Aspect;
                float distanceFromCenter = length(delta);
                float firstHole = smoothstep(_HoleRadius, _HoleRadius + max(_Feather, 0.0001), distanceFromCenter);
                float2 delta2 = input.uv - _HoleCenter2.xy;
                delta2.x *= _Aspect;
                float secondDistance = length(delta2);
                float secondHole = smoothstep(_HoleRadius2, _HoleRadius2 + max(_Feather, 0.0001), secondDistance);
                float overlay = min(firstHole, secondHole);
                fixed4 color = _Color * input.color;
                color.a *= overlay;
                return color;
            }
            ENDCG
        }
    }
}
