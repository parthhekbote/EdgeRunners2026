Shader "Cyberpunk/Authentic_Blackwall"
{
    Properties
    {
        [Header(Cyberpunk 2077 Canonical Blackwall Palette)]
        [HDR] _CrimsonColor("Resting Blood Crimson", Color) = (5.5, 0.03, 0.07, 1.0)
        [HDR] _MagentaColor("Swell Electric Magenta", Color) = (7.5, 0.08, 4.8, 1.0)
        [HDR] _PeakColor("Peak Crest Blinding White", Color) = (11.0, 11.0, 13.0, 1.0)
        [HDR] _CyanColor("Lower Band Electric Cyan", Color) = (0.02, 3.2, 6.5, 1.0)

        [Header(Signature Lower Cyan Transition Band)]
        _BlueBandHeight("Cyan Band Height Threshold", Range(0.01, 0.25)) = 0.075

        [Header(Ultra Res Laser Needle Dot Profile)]
        _DotPillStretch("Dot Horizontal Tightness", Range(0.5, 6.0)) = 3.6
        _StrandDensity("Micro Filaments Per Cell", Range(1.0, 3.0)) = 2.0
        _LaserCoreSharpness("Gaussian Core Sharpness", Range(20.0, 120.0)) = 65.0

        [Header(Audio Reactive 3D Z Billow)]
        _AudioZDisplacement("Master Audio Z Scale", Range(0.0, 10.0)) = 1.0
        _BassZPush("Bass Volumetric Z Swell", Float) = 4.2
        _VoiceZWave("Voice Harmonic Z Ripple", Float) = 0.8
        _ScreechZSpike("Screech Needle Z Spike", Float) = 3.5
        _MaxZDepth("Max Z Depth For Color Ramp", Float) = 3.5
        _ScatterIntensity("Individual Dot 3D Depth Scatter", Range(0.0, 2.0)) = 0.85

        [Header(Audio Spectrum Energy)]
        _BassEnergy("Audio Bass Energy", Range(0.0, 5.0)) = 0.0
        _ScreechEnergy("Audio Screech Energy", Range(0.0, 5.0)) = 0.0
        _GridCols("Mesh Grid Columns", Float) = 900.0
        _GridRows("Mesh Grid Rows", Float) = 160.0
        _StaticMode("Static Mode During Dialogue", Float) = 0.0
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
        Blend SrcAlpha One

        Pass
        {
            Name "CanonicalLidar3DBlackwallPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv0          : TEXCOORD0; // Intra-quad UV (0..1)
                float2 uv1          : TEXCOORD1; // Normalized Grid UV (0..1)
                float2 uv2          : TEXCOORD2; // Discrete Cell ID (col, row)
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD1;
                float2 intraUV      : TEXCOORD0;
                float2 gridUV       : TEXCOORD3;
                float2 cellID       : TEXCOORD4;
                float  dispZ        : TEXCOORD5;
                float  zRatio       : TEXCOORD6;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CrimsonColor;
                float4 _MagentaColor;
                float4 _PeakColor;
                float4 _CyanColor;
                float  _BlueBandHeight;
                float  _DotPillStretch;
                float  _StrandDensity;
                float  _LaserCoreSharpness;
                float  _AudioZDisplacement;
                float  _BassZPush;
                float  _VoiceZWave;
                float  _ScreechZSpike;
                float  _MaxZDepth;
                float  _ScatterIntensity;
                float  _BassEnergy;
                float  _ScreechEnergy;
                float  _GridCols;
                float  _GridRows;
                float  _StaticMode;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS;
                float2 gUV = input.uv1;
                float2 cell = input.uv2;

                // Static Mode: when talking, wall becomes completely frozen and motionless
                if (_StaticMode > 0.5)
                {
                    VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                    output.positionCS = vertexInput.positionCS;
                    output.positionWS = vertexInput.positionWS;
                    output.intraUV    = input.uv0;
                    output.gridUV     = gUV;
                    output.cellID     = cell;
                    output.dispZ      = 0.0;
                    output.zRatio     = 0.0;
                    return output;
                }

                // 1. Autonomous Random Roving AI Flare Zones:
                // Only specific localized parts of the wall billow out at any time!
                
                // Flare Packet A: Roving burst 1
                float tA = _Time.y * 0.32;
                float centerA = frac(sin(floor(tA) * 12.9898) * 43758.5453);
                float lifeA = sin(frac(tA) * 3.14159);
                float flareA = exp(-pow((gUV.x - centerA) * 7.5, 2.0)) * lifeA;

                // Flare Packet B: Roving burst 2 (independent location & timing)
                float tB = _Time.y * 0.25 + 1.83;
                float centerB = frac(sin(floor(tB) * 78.233) * 23421.6312);
                float lifeB = sin(frac(tB) * 3.14159);
                float flareB = exp(-pow((gUV.x - centerB) * 9.0, 2.0)) * lifeB;

                // Flare Packet C: Fast localized needle pocket
                float tC = _Time.y * 0.48 + 3.47;
                float centerC = frac(sin(floor(tC) * 45.164) * 65432.1987);
                float lifeC = sin(frac(tC) * 3.14159);
                float flareC = exp(-pow((gUV.x - centerC) * 11.5, 2.0)) * lifeC;

                // Flare Packet D: Secondary wandering echo
                float tD = _Time.y * 0.20 + 4.91;
                float centerD = frac(sin(floor(tD) * 93.412) * 31245.7891);
                float lifeD = sin(frac(tD) * 3.14159);
                float flareD = exp(-pow((gUV.x - centerD) * 8.0, 2.0)) * lifeD;

                // Combined localized surge mask across wall
                float surgeMask = saturate(flareA * 1.35 + flareB * 1.10 + flareC * 1.25 + flareD * 0.95);
                
                // Height envelope: surge lives primarily in mid-to-upper wall
                surgeMask *= smoothstep(0.06, 0.30, gUV.y) * (1.0 - smoothstep(0.85, 0.98, gUV.y));

                // 2. Macro Volumetric Swell:
                // Actively driven by audio bass energy + baseline wandering wave
                float waveInternal = sin(gUV.x * 24.0 + _Time.y * 2.8) * 0.3 + 0.7;
                float macroSwell = surgeMask * waveInternal * (_BassZPush + _BassEnergy * 3.5);

                // 3. Individual Dot 3D Depth Scatter (Volumetric Point Cloud Separation):
                float dotHash = Hash21(cell);
                float dotScatter = (dotHash - 0.40) * 1.8 * (macroSwell * _ScatterIntensity);

                // 4. Ghost Screech Needle Spikes (High-frequency rogue AI needle jitter):
                // Erupts dynamically when screech audio peaks occur
                float isSpike = step(0.82 - (_ScreechEnergy * 0.18 + surgeMask * 0.08), dotHash);
                float screechSpike = (dotHash - 0.82) * 5.0 * (_ScreechZSpike + _ScreechEnergy * 4.0) * isSpike * smoothstep(0.04, 0.25, gUV.y);

                // 5. Floor Pinning: Dots near floor remain firmly grounded at Z = 0
                float groundWeight = smoothstep(0.010, 0.08, gUV.y);

                // Total forward displacement along normal (normalOS = (0, 0, -1))
                float totalDispZ = (macroSwell + dotScatter + screechSpike) * _AudioZDisplacement * groundWeight;
                totalDispZ = max(0.0, totalDispZ);

                // Displace all vertices of the dot rigidly:
                pos += input.normalOS * totalDispZ;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.intraUV    = input.uv0;
                output.gridUV     = gUV;
                output.cellID     = cell;
                output.dispZ      = totalDispZ;
                output.zRatio     = saturate(totalDispZ / max(0.01, _MaxZDepth));
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Ultra-High Resolution Micro-Filament Sub-Grid
                // Subdivides each quad into 2 dense laser needle filaments horizontally
                float strandFrac = frac(input.intraUV.x * _StrandDensity);
                float2 microUV = float2(strandFrac - 0.5, input.intraUV.y - 0.5);

                // Needle-fine laser profile: thin horizontal waist, elongated vertical beam
                float dx = microUV.x * _DotPillStretch;
                float dy = microUV.y * 1.25;
                float dist = length(float2(dx, dy));

                // Clean antialiased laser needle point with transparent borders
                float dotAlpha = smoothstep(0.44, 0.04, dist);
                if (dotAlpha <= 0.001) discard;

                // Searing Gaussian laser core in the center
                float laserCore = exp(-dist * dist * _LaserCoreSharpness);

                // 2. Subtle vertical laser scanline pulse (frozen in static mode)
                float scanline = _StaticMode > 0.5 ? 1.0 : (0.88 + 0.12 * cos(input.gridUV.y * 700.0 - _Time.y * 5.0));
                dotAlpha *= scanline;

                // 3. Soft top dissolution into void
                float topDissolve = smoothstep(1.0, 0.88, input.gridUV.y);
                dotAlpha *= topDissolve;

                // 4. Vertical column modulation (alternating strand intensity)
                float colHash = Hash11(input.cellID.x * 0.123);
                float colGlow = 0.82 + 0.28 * sin(input.cellID.x * 0.45) + colHash * 0.20;

                // 5. Cyberpunk 2077 Depth-to-Color Ramp:
                // Resting (zRatio ~ 0): Deep Blood Crimson Red (#C80018)
                // Mid-swell (zRatio ~ 0.4..0.7): Hot Electric Fuchsia / Magenta (#FF0099)
                // Peak Crest (zRatio > 0.75): Blinding HDR White Core (#FFFFFF)
                float3 col = _CrimsonColor.rgb;
                col = lerp(col, _MagentaColor.rgb, smoothstep(0.08, 0.48, input.zRatio));
                col = lerp(col, _PeakColor.rgb, smoothstep(0.48, 0.85, input.zRatio));

                // Add laser core boost
                col += laserCore * 2.2 * _PeakColor.rgb * smoothstep(0.15, 0.70, input.zRatio);
                col *= colGlow;

                // 6. Signature Lower Cyan Scanline Band (grounded dots at bottom Y)
                float cyanBand = smoothstep(_BlueBandHeight, 0.0, input.gridUV.y) * (1.0 - saturate(input.zRatio * 2.5));
                col = lerp(col, _CyanColor.rgb, cyanBand * 0.95);

                // 7. Per-dot digital flicker & rogue AI live packet pulses (disabled in static mode)
                if (_StaticMode <= 0.5)
                {
                    float cellNoise = Hash21(input.cellID + float2(floor(_Time.y * 16.0), 0.0));
                    if (cellNoise > 0.988)
                    {
                        col *= 2.2; // Spark pulse
                    }
                    else if (cellNoise < 0.012)
                    {
                        dotAlpha *= 0.10; // Packet loss
                    }
                }

                return float4(col, dotAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
