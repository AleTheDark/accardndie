Shader "AccardND/VFX/Empower Fire Particle"
{
    Properties
    {
        [HDR] _CoreColor("White-hot Core", Color) = (6,3.2,0.35,1)
        [HDR] _EdgeColor("Flame Edge", Color) = (2.2,0.08,0.002,1)
        _Distortion("Distortion", Range(0,1)) = 0.38
        _Sharpness("Flame Sharpness", Range(0.5,6)) = 2.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+60" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "EmpowerFire"
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float seed : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                float _Distortion;
                float _Sharpness;
            CBUFFER_END

            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f*f*(3.0-2.0*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.seed = hash21(input.positionOS.xy + input.color.rg);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float rise = _Time.y * 2.4 + input.seed * 7.0;
                float turbulence = noise(float2(p.x * 3.2, p.y * 2.0 - rise));
                turbulence += noise(float2(p.x * 6.4 + 4.7, p.y * 4.0 - rise * 1.7)) * 0.5;
                p.x += (turbulence - 0.75) * _Distortion * (0.35 + input.uv.y);
                float body = saturate(1.0 - length(float2(p.x * (1.1 + input.uv.y), p.y * 0.82)));
                float lick = saturate(body + (turbulence - 0.55) * 0.72 - input.uv.y * 0.08);
                // Centro piu' trasparente: le lingue restano leggibili ai bordi
                // senza creare una lastra additiva sopra i numeri del dado.
                float alpha = pow(lick, _Sharpness) * input.color.a * 0.72;
                float core = pow(saturate(lick), 4.0);
                half3 color = lerp(_EdgeColor.rgb, _CoreColor.rgb, core) * input.color.rgb;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
