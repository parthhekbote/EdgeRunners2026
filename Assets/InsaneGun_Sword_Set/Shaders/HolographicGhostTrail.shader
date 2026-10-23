Shader "InsaneGun/HolographicGhostTrail"
{
    Properties
    {
        [HDR] _GhostColor ("Ghost Color", Color) = (0.0, 1.8, 2.2, 0.9)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.0
        _Alpha ("Alpha Multiplier", Range(0.0, 1.0)) = 1.0
        _EmissionGain ("Emission Gain", Range(1.0, 8.0)) = 3.5
        _CoreOpacity ("Core Opacity", Range(0.2, 1.0)) = 0.65
        _ScanlineFreq ("Scanline Frequency", Range(10.0, 200.0)) = 85.0
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 1.0)) = 0.40
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+200" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True"
        }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
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
                float3 positionWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColor;
                float _RimPower;
                float _Alpha;
                float _EmissionGain;
                float _CoreOpacity;
                float _ScanlineFreq;
                float _ScanlineIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.positionWS = posInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float NdotV = saturate(dot(n, v));
                float fresnel = pow(1.0 - NdotV, _RimPower);

                // High-tech Cyberpunk holographic scanline lattice
                float scanlinePattern = sin(input.positionWS.y * _ScanlineFreq) * 0.5 + 0.5;
                float scanlines = lerp(1.0, scanlinePattern, _ScanlineIntensity);

                // Punchy core fill ensures the projection stands out clearly in bright daylight / lit environments
                float core = _CoreOpacity;
                float bodyIntensity = (core + fresnel * (1.0 - core * 0.35)) * scanlines;

                half3 finalColor = _GhostColor.rgb * bodyIntensity * _EmissionGain;
                half finalAlpha = saturate((core * 0.85 + fresnel * 0.95) * _GhostColor.a * _Alpha);
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Particles/Unlit"
}
