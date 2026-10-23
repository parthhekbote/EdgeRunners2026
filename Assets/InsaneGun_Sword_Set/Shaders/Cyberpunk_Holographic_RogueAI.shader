Shader "Cyberpunk/Holographic_RogueAI"
{
    Properties
    {
        [Header(Base Holographic Substrate)]
        _BaseMap("Base Texture", 2D) = "white" {}
        _HoloColor("Hologram Color", Color) = (1.0, 0.08, 0.35, 1.0)
        _CoreAlpha("Core Translucency", Range(0.05, 0.9)) = 0.35

        [Header(Cyberpunk Rogue AI Rim Glow)]
        [HDR] _RimColor("Fresnel Rim Glow", Color) = (3.5, 0.15, 1.2, 1.0)
        _RimPower("Fresnel Rim Power", Range(0.5, 6.0)) = 2.6

        [Header(Digital Scanline and Grid Optics)]
        _ScanlineDensity("Scanline Density", Float) = 150.0
        _ScanlineSpeed("Scanline Speed", Float) = 3.5
        _ScanlineIntensity("Scanline Intensity", Range(0.0, 1.0)) = 0.35
        _GridDensity("Matrix Grid Density", Float) = 32.0
        _GridIntensity("Grid Intensity", Range(0.0, 1.0)) = 0.25

        [Header(Digital Glitch Instability)]
        _GlitchIntensity("Digital Glitch Jitter", Range(0.0, 1.0)) = 0.3
        _GlitchSpeed("Glitch Rate", Float) = 16.0

        [Header(Holographic Dissolve Emergence)]
        _Dissolve("Materialization Progress", Range(0.0, 1.0)) = 1.0
        _DissolveEdgeWidth("Dissolve Edge Width", Range(0.01, 0.3)) = 0.08
        [HDR] _DissolveEdgeColor("Searing Edge Glow", Color) = (6.0, 2.0, 0.5, 1.0)
        _DissolveAxis("Dissolve Mode (0=Humanoid WorldY, 1=Starship FrontToBack)", Float) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 300
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "HolographicEmergencePass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float2 uv           : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _HoloColor;
                float4 _RimColor;
                float4 _DissolveEdgeColor;
                float  _CoreAlpha;
                float  _RimPower;
                float  _ScanlineDensity;
                float  _ScanlineSpeed;
                float  _ScanlineIntensity;
                float  _GridDensity;
                float  _GridIntensity;
                float  _GlitchIntensity;
                float  _GlitchSpeed;
                float  _Dissolve;
                float  _DissolveEdgeWidth;
                float  _DissolveAxis;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Horizontal digital glitch slice displacement
                float3 pos = input.positionOS.xyz;
                float glitchTime = floor(_Time.y * _GlitchSpeed);
                float glitchHash = Hash21(float2(glitchTime, floor(pos.y * 6.0)));
                if (glitchHash > 0.93 && _GlitchIntensity > 0.01)
                {
                    pos.x += (glitchHash - 0.93) * 1.5 * _GlitchIntensity;
                }

                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;
                output.uv         = input.uv;
                return output;
            }

            float4 frag(Varyings input, float facing : VFACE) : SV_Target
            {
                // 1. Materialization Dissolve
                float normCoord = 0.0;
                if (_DissolveAxis > 0.5)
                {
                    // Starship Emergence: Unified front-to-back nose-first emergence through Blackwall
                    float3 rootLocalPos = mul(GetWorldToObjectMatrix(), float4(input.positionWS, 1.0)).xyz;
                    normCoord = saturate((4.2 - rootLocalPos.z) / 8.4);
                }
                else
                {
                    // Humanoid Emergence: Bottom-to-top ground materialization
                    normCoord = saturate((input.positionWS.y + 0.1) / 2.3);
                }

                float dNoise = Hash21(input.uv * 16.0 + float2(0.0, _Time.y * 0.4)) * 0.12;
                float dVal = normCoord + dNoise;
                float threshold = _Dissolve * 1.25 - 0.1;

                // Clip pixels that have not materialized yet
                clip(threshold + _DissolveEdgeWidth - dVal);

                // Edge glow line at the materialization boundary
                float edgeGlow = smoothstep(threshold - _DissolveEdgeWidth, threshold, dVal) *
                                 (1.0 - smoothstep(threshold, threshold + _DissolveEdgeWidth, dVal));

                // 2. Base Texture & Detail Luminance
                float4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float lum = dot(texCol.rgb, float3(0.299, 0.587, 0.114));
                float detail = 0.4 + 0.6 * lum;

                // 3. Normal & Fresnel Rim Calculation
                float3 normalWS  = normalize(input.normalWS);
                // Invert normal for backfaces so Fresnel works on both sides
                if (facing < 0.0) normalWS = -normalWS;

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _RimPower);

                // 4. Animated Scanlines
                float scan = 0.5 + 0.5 * sin(input.positionWS.y * _ScanlineDensity - _Time.y * _ScanlineSpeed);
                float scanline = lerp(1.0, scan, _ScanlineIntensity);

                // 5. Digital Matrix Coordinate Grid
                float2 gridCoord = frac(input.positionWS.xz * _GridDensity);
                float gridLine = step(0.92, gridCoord.x) + step(0.92, gridCoord.y);
                gridLine = saturate(gridLine) * _GridIntensity;

                // 6. Holographic Jitter Flicker
                float flicker = 0.94 + 0.06 * sin(_Time.y * 38.0) * cos(_Time.y * 17.0);

                // 7. Composite Color Assembly
                float faceDim = facing > 0.0 ? 1.0 : 0.45; // dimmer interior backfaces
                float3 bodyColor = _HoloColor.rgb * detail * faceDim;
                float3 rimColor  = _RimColor.rgb * fresnel * faceDim;

                float3 col = bodyColor * _CoreAlpha + rimColor + (gridLine * _HoloColor.rgb * 1.2);
                col += edgeGlow * _DissolveEdgeColor.rgb * 3.5;
                col *= scanline * flicker;

                // Micro glitch sparks
                float spark = step(0.988, Hash21(input.uv + float2(_Time.y * 24.0, 0.0)));
                col += spark * _RimColor.rgb * 2.0;

                // 8. Alpha calculation (additive blend modulated by alpha)
                float alpha = saturate((_CoreAlpha * detail + fresnel * 0.9 + edgeGlow * 2.0) * scanline * faceDim);

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
