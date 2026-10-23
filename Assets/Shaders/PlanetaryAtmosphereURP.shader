Shader "Universal Render Pipeline/PlanetaryAtmosphere"
{
    Properties
    {
        _AtmosphereColor("Atmosphere Color", Color) = (1.0, 0.42, 0.14, 1.0)
        _RimPower("Rim Falloff Power", Float) = 2.8
        _RimIntensity("Rim Glow Intensity", Float) = 2.2
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+50" 
            "RenderPipeline"="UniversalPipeline" 
        }
        Pass
        {
            Name "AtmosphereLimb"
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AtmosphereColor;
                float _RimPower;
                float _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float NdotV = saturate(dot(N, V));

                // Rayleigh-style limb scattering: peak at grazing angle, 0 at planetary center
                float fresnel = pow(1.0 - NdotV, _RimPower);
                float alpha = saturate(fresnel * _RimIntensity);

                float3 glowColor = _AtmosphereColor.rgb * alpha;
                return float4(glowColor, alpha * _AtmosphereColor.a);
            }
            ENDHLSL
        }
    }
}
