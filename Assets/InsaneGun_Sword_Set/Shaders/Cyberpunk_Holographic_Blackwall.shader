Shader "Cyberpunk/Holographic_Blackwall"
{
    Properties
    {
        [Header(Blackwall Base ICE Substrate)]
        _BaseColor("Deep ICE Base Color", Color) = (0.015, 0.002, 0.006, 0.96)
        [HDR] _EmissionColor("HDR Crimson Data Stream", Color) = (4.8, 0.02, 0.08, 1.0)
        [HDR] _CoreEnergyColor("HDR Fiery Core Surge", Color) = (5.5, 0.45, 0.08, 1.0)
        [HDR] _GlitchFlashColor("HDR Rogue AI Flash", Color) = (6.0, 0.2, 0.35, 1.0)

        [Header(Data Stream and Matrix Setting)]
        _ScrollSpeed("Vertical Stream Speed", Float) = 2.2
        _DataTiling("Matrix Data Tiling (X, Y)", Vector) = (28.0, 48.0, 0, 0)
        _MacroBlockTiling("Macro Glitch Blocks (X, Y)", Vector) = (8.0, 14.0, 0, 0)
        
        [Header(Rogue AI Neural Tendrils)]
        _TendrilScale("Neural Tendril Scale", Float) = 5.5
        _TendrilSpeed("Tendril Writhing Speed", Float) = 0.8
        _TendrilIntensity("Tendril Emission Power", Float) = 2.2

        [Header(Cyberpunk Optics and Glitch)]
        _FresnelPower("Edge Corona Fresnel Power", Range(0.5, 6.0)) = 2.8
        _FresnelIntensity("Edge Corona Multiplier", Float) = 3.5
        _ScanlineDensity("Horizontal Scanline Density", Float) = 220.0
        _GlitchIntensity("Rogue AI Tearing Jitter", Range(0.0, 2.0)) = 0.85
        _GlitchFrequency("Glitch Step Rate", Float) = 14.0
        _PulseSpeed("Net Breathing Pulse Speed", Float) = 1.4
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+10" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 300
        Cull Off
        ZWrite Off
        // Alpha blend for dense opaque pitch-black substrate + intense HDR red emission
        Blend SrcAlpha OneMinusSrcAlpha 

        Pass
        {
            Name "BlackwallPass"
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
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _CoreEnergyColor;
                float4 _GlitchFlashColor;
                float4 _DataTiling;
                float4 _MacroBlockTiling;
                float _ScrollSpeed;
                float _TendrilScale;
                float _TendrilSpeed;
                float _TendrilIntensity;
                float _FresnelPower;
                float _FresnelIntensity;
                float _ScanlineDensity;
                float _GlitchIntensity;
                float _GlitchFrequency;
                float _PulseSpeed;
            CBUFFER_END

            // High quality pseudo-random hash
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Procedural Voronoi Cellular Noise for Rogue AI Neural Tendrils
            float VoronoiTendrils(float2 uv, float time)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;
                float secondDist = 1.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(float(x), float(y));
                        float2 pointPos = Hash22(g + neighbor);
                        // Animate points organic crawling
                        pointPos = 0.5 + 0.4 * sin(time + 6.2831 * pointPos);
                        float2 diff = neighbor + pointPos - f;
                        float dist = length(diff);

                        if (dist < minDist)
                        {
                            secondDist = minDist;
                            minDist = dist;
                        }
                        else if (dist < secondDist)
                        {
                            secondDist = dist;
                        }
                    }
                }
                // Border distance (sharp cellular boundaries = neural digital veins)
                float border = secondDist - minDist;
                return 1.0 - smoothstep(0.02, 0.14, border);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput   = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;
                output.uv         = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. Rogue AI Horizontal Glitch Displacement & Memory Corruption
                float glitchTime = _Time.y * _GlitchFrequency;
                float glitchBand = floor(uv.y * 45.0 + glitchTime * 0.4);
                float glitchRand = Hash21(float2(glitchBand, floor(glitchTime)));
                
                float isGlitching = step(0.85, glitchRand);
                float horizontalJitter = (glitchRand - 0.5) * 0.16 * _GlitchIntensity * isGlitching;
                
                // Chromatic split UVs
                float2 uvR = uv + float2(horizontalJitter, 0.0);
                float2 uvB = uv - float2(horizontalJitter * 0.6, 0.0);
                float2 activeUV = uvR;

                // 2. Multi-Frequency Cascading Matrix Data Streams
                // A. Fine binary code rain
                float2 dataUV = activeUV * _DataTiling.xy;
                dataUV.y -= _Time.y * _ScrollSpeed;
                float colHash = Hash21(float2(floor(dataUV.x), 0.0));
                float rowHash = Hash21(float2(floor(dataUV.x), floor(dataUV.y)));
                float charPattern = step(0.48, rowHash) * (sin(dataUV.y * 6.28) * 0.5 + 0.5);

                // B. Macro Glitch Data Blocks (corrupted memory packets)
                float2 macroUV = activeUV * _MacroBlockTiling.xy;
                macroUV.y -= _Time.y * (_ScrollSpeed * 0.45);
                float macroHash = Hash21(float2(floor(macroUV.x), floor(macroUV.y)));
                float macroBlock = step(0.78, macroHash) * (sin(_Time.y * 8.0 + macroHash * 20.0) * 0.5 + 0.5);

                // 3. Rogue AI Neural Tendrils (Creeping bio-digital veins)
                float2 tendrilUV = activeUV * _TendrilScale;
                tendrilUV.y += sin(activeUV.x * 3.0 + _Time.y * 0.5) * 0.3;
                float tendrilPattern = VoronoiTendrils(tendrilUV, _Time.y * _TendrilSpeed);
                tendrilPattern = pow(tendrilPattern, 2.0) * _TendrilIntensity;

                // 4. Low-Frequency "Heartbeat of the Net" Power Pulse
                float pulseWave1 = sin(uv.y * 5.0 - _Time.y * _PulseSpeed * 3.14) * 0.5 + 0.5;
                float pulseWave2 = sin(uv.y * 12.0 + _Time.y * _PulseSpeed * 1.5) * 0.5 + 0.5;
                float netHeartbeat = pow(pulseWave1 * 0.7 + pulseWave2 * 0.3, 2.5);

                // 5. Holographic Scanline Grid & Interference Carrier Wave
                float scanlines = sin(uv.y * _ScanlineDensity - _Time.y * 30.0) * 0.5 + 0.5;
                scanlines = pow(scanlines, 2.2);

                // 6. Top Digital Dissolve (De-rezzing into void space)
                float noiseBlock = Hash21(floor(activeUV * float2(32.0, 16.0)));
                float topDissolve = saturate((1.0 - uv.y) * 4.5 + (noiseBlock - 0.5) * 0.4);

                // Bottom contact glow
                float groundFade = saturate(uv.y * 12.0);

                // 7. Edge Corona Fresnel (Grazing angles glow with blazing intensity)
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 normalWS  = normalize(input.normalWS);
                if (dot(normalWS, viewDirWS) < 0.0) normalWS = -normalWS;
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;

                // 8. Composite Deep Black ICE Substrate & Searing HDR Emission
                // Base background is deep pitch-black digital substrate
                float3 finalColor = _BaseColor.rgb;

                // Add streaming matrix code
                float dataGlow = charPattern * 0.65 + macroBlock * 1.2;
                finalColor += _EmissionColor.rgb * dataGlow;

                // Add writhing Rogue AI neural tendrils
                finalColor += lerp(_EmissionColor.rgb, _CoreEnergyColor.rgb, 0.6) * tendrilPattern;

                // Add breathing pulse surge
                finalColor += _EmissionColor.rgb * (netHeartbeat * 0.95);

                // Add scanlines & Fresnel corona
                finalColor += _EmissionColor.rgb * (scanlines * 0.25);
                finalColor += _EmissionColor.rgb * fresnel;

                // Add sudden Rogue AI glitch flash bursts
                float glitchFlash = isGlitching * (0.8 + macroHash * 0.5);
                finalColor += _GlitchFlashColor.rgb * glitchFlash;

                // Base ground laser bleed
                float groundBleed = exp(-uv.y * 8.0) * 1.5;
                finalColor += _CoreEnergyColor.rgb * groundBleed;

                // Alpha composite: Opaque/dense in the body, dissolved only at the very top into space
                float alpha = _BaseColor.a * topDissolve * groundFade;
                // Ensure luminous data burns through cleanly
                alpha = saturate(alpha + (dataGlow + tendrilPattern + netHeartbeat + fresnel) * 0.4);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
