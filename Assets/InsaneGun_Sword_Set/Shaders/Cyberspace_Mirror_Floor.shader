Shader "Cyberpunk/Cyberspace_Mirror_Floor"
{
    Properties
    {
        [Header(Base Abyss Floor)]
        _BaseColor("Pure Black Abyss Floor", Color) = (0.0, 0.0, 0.0, 1.0)
        _MirrorReflectivity("Water Mirror Reflectivity", Range(0.0, 1.0)) = 0.95
        _FresnelPower("Water Fresnel Exponent", Range(1.0, 8.0)) = 2.2
        _WaterTint("Shallow Liquid Water Tint", Color) = (0.92, 0.96, 1.0, 1.0)

        [Header(Planar Reflection)]
        _ReflectionTex("Planar Reflection Texture", 2D) = "black" {}
        _DistortionStrength("Liquid Ripple Distortion", Range(0.0, 0.05)) = 0.004
        _WaveSpeed("Liquid Flow Speed", Float) = 0.8
        _WaveTiling("Liquid Ripple Tiling", Float) = 2.5

        [Header(Wet Specular Sheen)]
        _SpecularGloss("Water Surface Smoothness", Range(0.1, 1.0)) = 0.92
        _SpecularIntensity("Surface Wet Glint Intensity", Range(0.0, 5.0)) = 1.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : NORMAL;
                float4 screenPos    : TEXCOORD2;
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _WaterTint;
                float  _MirrorReflectivity;
                float  _FresnelPower;
                float  _DistortionStrength;
                float  _WaveSpeed;
                float  _WaveTiling;
                float  _SpecularGloss;
                float  _SpecularIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;
                output.screenPos  = ComputeScreenPos(output.positionCS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 baseNormalWS = normalize(input.normalWS);

                // 1. Dual-harmonic flowing water micro-ripples
                float2 waveUV1 = input.positionWS.xz * _WaveTiling * 0.05 + float2(_Time.y * _WaveSpeed * 0.12, _Time.y * _WaveSpeed * 0.07);
                float2 waveUV2 = input.positionWS.xz * _WaveTiling * 0.11 - float2(_Time.y * _WaveSpeed * 0.07, _Time.y * _WaveSpeed * 0.15);
                float2 waveUV3 = input.positionWS.xz * _WaveTiling * 0.02 + float2(-_Time.y * _WaveSpeed * 0.04, _Time.y * _WaveSpeed * 0.05);

                float w1 = sin(waveUV1.x * 6.283) * cos(waveUV1.y * 6.283);
                float w2 = cos(waveUV2.x * 6.283 + 1.25) * sin(waveUV2.y * 6.283 + 2.15);
                float w3 = sin(waveUV3.x * 6.283 - 0.75) * cos(waveUV3.y * 6.283 + 1.45);

                // Water surface normal gradient
                float2 normalPerturb = float2(
                    cos(waveUV1.x * 6.283) * 0.4 + sin(waveUV2.x * 6.283) * 0.35 + cos(waveUV3.x * 6.283) * 0.25,
                    -sin(waveUV1.y * 6.283) * 0.4 + cos(waveUV2.y * 6.283) * 0.35 - sin(waveUV3.y * 6.283) * 0.25
                );
                float3 waterNormalWS = normalize(baseNormalWS + float3(normalPerturb.x, 0, normalPerturb.y) * 0.06);

                // 2. Ripple Screen UV Offset for Planar Reflection
                float2 rippleOffset = (normalPerturb * 0.5 + float2(w1 + w2, w2 + w3) * 0.5) * _DistortionStrength;
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                screenUV += rippleOffset;
                screenUV = clamp(screenUV, 0.001, 0.999);

                float4 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, screenUV);

                // 3. Fresnel Reflectance (Glassy Salar de Uyuni water optics)
                float NdotV = saturate(dot(waterNormalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                float waterReflectance = lerp(0.38, 1.0, fresnel) * _MirrorReflectivity;

                // 4. Pure Black Abyss Floor with Liquid Water Reflection
                float3 reflectionCol = reflection.rgb * _WaterTint.rgb;
                float3 floorColor = lerp(_BaseColor.rgb, reflectionCol, waterReflectance);

                // 5. Wet Specular Sheen (Subtle glossy glint from main light on water ripples)
                Light mainLight = GetMainLight();
                if (mainLight.distanceAttenuation > 0.0)
                {
                    float3 halfDir = normalize(mainLight.direction + viewDirWS);
                    float NdotH = saturate(dot(waterNormalWS, halfDir));
                    float spec = pow(NdotH, _SpecularGloss * 128.0) * _SpecularIntensity;
                    floorColor += mainLight.color * spec * fresnel;
                }

                return float4(floorColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
