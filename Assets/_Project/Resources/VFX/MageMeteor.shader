Shader "AccardND/VFX/Mage Meteor"
{
    Properties
    {
        _BaseMap ("Meteor Texture", 2D) = "white" {}
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Hot Edge Width", Range(0.001,0.25)) = 0.08
        _EmissionStrength ("Emission Strength", Range(0,8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Dissolve;
                float _EdgeWidth;
                float _EmissionStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash31(i), Hash31(i + float3(1,0,0)), f.x),
                                 lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x),
                                 lerp(Hash31(i + float3(0,1,1)), Hash31(i + 1), f.x), f.y), f.z);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float breakup = Noise(input.positionOS * 7.0) * 0.68 + Noise(input.positionOS * 19.0) * 0.32;
                float threshold = _Dissolve * 1.18 - 0.09;
                clip(breakup - threshold);
                float edge = 1.0 - smoothstep(threshold, threshold + _EdgeWidth, breakup);
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float lava = saturate(tex.r * 1.45 - max(tex.g, tex.b) * 0.55);
                float rim = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(GetWorldSpaceViewDir(TransformObjectToWorld(input.positionOS))))), 3.0);
                half3 hot = half3(1.0, 0.12, 0.005) * (edge * 5.0 + lava * _EmissionStrength);
                return half4(tex * (0.72 + rim * 0.28) + hot, 1.0);
            }
            ENDHLSL
        }
    }
}
