Shader "InsaneGun/HolographicGhostTrail"
{
    Properties
    {
        [HDR] _GhostColor ("Ghost Color", Color) = (0.0, 0.95, 1.0, 0.85)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.2
        _Alpha ("Alpha Multiplier", Range(0.0, 1.0)) = 1.0
        _EmissionGain ("Emission Gain", Range(1.0, 5.0)) = 2.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "GhostForward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColor;
                float _RimPower;
                float _Alpha;
                float _EmissionGain;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float NdotV = saturate(dot(n, v));
                float fresnel = pow(1.0 - NdotV, _RimPower);
                float intensity = (0.25 + fresnel * 0.75) * _Alpha;

                half3 finalColor = _GhostColor.rgb * _GhostColor.a * intensity * _EmissionGain;
                half finalAlpha = saturate(intensity * _GhostColor.a);
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
