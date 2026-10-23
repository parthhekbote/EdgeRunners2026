Shader "Cyberpunk/Blackwall_Lidar_Face"
{
    Properties
    {
        [Header(Cyberpunk 2077 Authentic Blackwall Palette)]
        [HDR] _CrimsonColor("Resting Blood Crimson", Color) = (6.0, 0.02, 0.08, 1.0)
        [HDR] _MagentaColor("Swell Electric Magenta", Color) = (8.0, 0.08, 5.0, 1.0)
        [HDR] _PeakColor("Peak Crest Blinding White", Color) = (12.0, 12.0, 14.0, 1.0)
        [HDR] _CyanColor("Eye Sensor Electric Cyan", Color) = (0.05, 5.0, 9.5, 1.0)

        [Header(Discrete LiDAR Dot Matrix Lattice)]
        _LidarDensity("LiDAR Dot Density Across Face", Range(100.0, 600.0)) = 320.0
        _DotRadius("Individual Dot Needle Radius", Range(0.15, 0.48)) = 0.36
        _DotSharpness("Gaussian Core Sharpness", Range(15.0, 90.0)) = 45.0
        _DotStretch("Dot Horizontal Tightness", Range(0.8, 3.0)) = 1.5

        [Header(Scanline Glitch and Data Stream)]
        _ScanlineSpeed("Vertical Scanline Speed", Range(0.5, 12.0)) = 3.5
        _GlitchIntensity("Digital Slice Glitch", Range(0.0, 1.0)) = 0.18
        _EyeGlowIntensity("Cyan Eye Sensor Intensity", Range(0.0, 6.0)) = 3.5

        [Header(Interactive Speech and Emergence)]
        _DissolveAmount("Emergence Dissolve (0=Void, 1=Fully Coalesced)", Range(0.0, 1.0)) = 1.0
        _VoiceAudioAmp("Live Voice Speech Amplitude", Range(0.0, 2.0)) = 0.0
        _JawDrop("Procedural Jaw Open Displacement", Range(0.0, 1.0)) = 0.0

        [Header(Face Edge Dispersion)]
        _EdgeFadeStart("Edge Dispersion Radius", Range(0.6, 1.2)) = 0.88
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+25" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 300
        Cull Back
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "PureLidarDotFacePass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD3;
                float3 positionOS   : TEXCOORD4;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CrimsonColor;
                float4 _MagentaColor;
                float4 _PeakColor;
                float4 _CyanColor;
                float  _LidarDensity;
                float  _DotRadius;
                float  _DotSharpness;
                float  _DotStretch;
                float  _ScanlineSpeed;
                float  _GlitchIntensity;
                float  _EyeGlowIntensity;
                float  _DissolveAmount;
                float  _VoiceAudioAmp;
                float  _JawDrop;
                float  _EdgeFadeStart;
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
                float3 norm = input.normalOS;

                // 1. Procedural Jaw Articulation for Speech:
                // Vertices below mouth level (pos.y < -0.015) displace downward and slightly inward
                float jawMask = saturate((-pos.y - 0.015) * 18.0) * saturate(pos.z * 15.0);
                pos.y -= jawMask * _JawDrop * 0.038;
                pos.z -= jawMask * _JawDrop * 0.012;

                // 2. Harmonic Voice Z-Ripple (audio speech swell)
                float voiceWave = sin(pos.y * 35.0 + _Time.y * 14.0) * 0.003 * _VoiceAudioAmp;
                pos += norm * voiceWave;

                // 3. Digital Slice Glitch Jitter
                float sliceID = floor(pos.y * 45.0 + _Time.y * 12.0);
                float sliceHash = Hash11(sliceID);
                if (sliceHash > (1.0 - _GlitchIntensity * 0.35))
                {
                    pos.x += (sliceHash - 0.5) * 0.012 * _GlitchIntensity;
                    pos.z += (Hash11(sliceID * 2.3) - 0.5) * 0.008 * _GlitchIntensity;
                }

                // Transform to World & Clip Space
                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                VertexNormalInputs normalInput = GetVertexNormalInputs(norm);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalize(normalInput.normalWS);
                output.positionOS = pos;
                output.uv         = input.uv;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Spatial LiDAR Dot Grid Lattice (Object-Space Projection)
                // Maps regular, uniform laser needle dots across the 3D facial contours without UV seams
                float2 dotGrid = input.positionOS.xy * _LidarDensity;
                float2 cellID = floor(dotGrid);
                float2 intraUV = frac(dotGrid) - 0.5;

                // Laser needle elliptical profile
                float dx = intraUV.x * _DotStretch;
                float dy = intraUV.y;
                float dist = length(float2(dx, dy));

                // AGGRESSIVE VOID DISCARD:
                // If fragment is outside the sharp needle dot radius, discard immediately!
                // This guarantees 100% empty space between dots (NO solid polygon surface!)
                if (dist > _DotRadius) discard;

                // Clean antialiased laser dot edge
                float dotAlpha = smoothstep(_DotRadius, _DotRadius * 0.15, dist);
                if (dotAlpha <= 0.002) discard;

                // Searing Gaussian laser core in the center of each dot
                float laserCore = exp(-dist * dist * _DotSharpness);

                // 2. Soft Edge Dispersion at Face Perimeter (forehead, temples, chin)
                // Gradually thins dots at the boundaries so the face emerges organically from the Blackwall
                float normX = input.positionOS.x / 0.098;
                float normY = (input.positionOS.y - 0.02) / 0.142;
                float perimeterDist = length(float2(normX, normY));
                float edgeFade = smoothstep(_EdgeFadeStart + 0.22, _EdgeFadeStart, perimeterDist);
                dotAlpha *= edgeFade;
                if (dotAlpha <= 0.002) discard;

                // 3. Vertical LiDAR Scanline Sweep (laser wave rolling across the face dots)
                float scanline = 0.86 + 0.14 * cos(input.positionWS.y * 5.0 - _Time.y * _ScanlineSpeed);
                dotAlpha *= scanline;

                // 4. Per-Dot Digital Glitch & Network Packet Jitter
                float dotNoise = Hash21(cellID + float2(floor(_Time.y * 18.0), 0.0));
                if (dotNoise < 0.02) dotAlpha *= 0.15; // occasional dropped packet
                float sparkPulse = dotNoise > 0.982 ? 2.2 : 1.0;

                // 5. Volumetric Emergence Dissolve
                float dissolveNoise = Hash21(cellID * 0.15);
                float dissolveMask = smoothstep(1.0 - _DissolveAmount, 1.0 - _DissolveAmount + 0.10, dissolveNoise);
                dotAlpha *= dissolveMask;
                if (dotAlpha <= 0.002) discard;

                // 6. Authentic Cyberpunk 2077 Blackwall Depth-to-Color Ramp:
                // Resting perimeter dots: Deep Blood Crimson (#FF003C)
                // 3D Facial Contours (cheeks, nose, brow, lips): Hot Electric Magenta (#FF0099)
                // Center Needle Core: Searing Blinding White
                float zRatio = saturate((input.positionOS.z - 0.015) / 0.055);
                float3 col = lerp(_CrimsonColor.rgb, _MagentaColor.rgb, zRatio * 0.88);

                // Concentrated searing white needle core at the exact center of each dot
                float coreNeedle = pow(laserCore, 3.0);
                col += _PeakColor.rgb * coreNeedle * 1.8;

                // Live voice speech amplitude surge
                col *= (1.0 + _VoiceAudioAmp * 0.75);
                col *= sparkPulse;

                // 7. Piercing Cyan Optical Sensor Dots (Eyes)
                // Centered exactly at Left (-0.0410, 0.0397) and Right (+0.0410, 0.0397)
                float distEyeL = length(input.positionOS.xy - float2(-0.0410, 0.0397));
                float distEyeR = length(input.positionOS.xy - float2(0.0410, 0.0397));
                float eyeDist = min(distEyeL, distEyeR);
                float isEye = smoothstep(0.013, 0.003, eyeDist);

                if (isEye > 0.01)
                {
                    col = lerp(col, _CyanColor.rgb * _EyeGlowIntensity, isEye * 0.95);
                    // Pupil center searing white laser needle
                    col += _PeakColor.rgb * coreNeedle * isEye * 3.5;
                }

                return float4(col, dotAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
