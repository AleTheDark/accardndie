Shader "AccardND/VFX/Empower Dice Shell"
{
    Properties
    {
        [HDR] _RimColor("Infernal Rim", Color) = (4.5,0.42,0.015,1)
        [HDR] _CoreColor("Divine Core", Color) = (7,3.5,0.4,1)
        _RimPower("Rim Power", Range(0.5,8)) = 2.2
        _PulseSpeed("Pulse Speed", Range(0,20)) = 8
        _FlowScale("Flow Scale", Range(1,30)) = 11
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+40" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Front

        Pass
        {
            Name "InfernalFresnelShell"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 localPos : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half4 _CoreColor;
                float _RimPower;
                float _PulseSpeed;
                float _FlowScale;
            CBUFFER_END

            float hash31(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float pulse = sin(_Time.y * _PulseSpeed + dot(input.positionOS.xyz, 9.0)) * 0.012 + 1.035;
                float3 expanded = input.positionOS.xyz + input.normalOS * (pulse - 1.0);
                VertexPositionInputs pos = GetVertexPositionInputs(expanded);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.localPos = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);
                float flow = sin((input.localPos.y + input.localPos.x * 0.35) * _FlowScale - _Time.y * 7.0) * 0.5 + 0.5;
                float cells = hash31(floor(input.localPos * 18.0 + _Time.y * float3(0,2.4,0)));
                float veins = smoothstep(0.58, 0.92, flow * 0.72 + cells * 0.42);
                float pulse = 0.78 + sin(_Time.y * _PulseSpeed) * 0.18;
                // L'energia resta sul profilo: il centro della faccia e quindi
                // il numero stampato conservano contrasto e leggibilita'.
                float energy = saturate(fresnel * (pulse + veins * 0.24));
                half3 color = lerp(_RimColor.rgb, _CoreColor.rgb, veins * 0.55) * energy;
                return half4(color, energy);
            }
            ENDHLSL
        }
    }
}
