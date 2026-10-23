Shader "Cyberpunk/Tron_Red_Cube_Grid"
{
    Properties
    {
        [HDR] _GridColor("Grid Laser Color", Color) = (5.5, 0.08, 0.16, 1.0)
        _GridSpacing("Grid Spacing (Meters)", Float) = 2.0
        _LineWidth("Line Width", Range(0.01, 0.15)) = 0.035
        _MajorInterval("Major Grid Interval", Float) = 4.0
        _FadeStart("Fade Start Distance", Float) = 25.0
        _FadeEnd("Fade End Distance", Float) = 150.0
        _PulseSpeed("Pulse Wave Speed", Float) = 1.0
        _BaseAlpha("Base Opacity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Cull Off
        ZWrite On

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

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
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float  _GridSpacing;
                float  _LineWidth;
                float  _MajorInterval;
                float  _FadeStart;
                float  _FadeEnd;
                float  _PulseSpeed;
                float  _BaseAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 absNorm = abs(normalize(input.normalWS));
                float2 uv;
                
                // Triplanar projection based on dominant surface normal
                if (absNorm.y > 0.5)
                {
                    uv = input.positionWS.xz;
                }
                else if (absNorm.x > 0.5)
                {
                    uv = input.positionWS.zy;
                }
                else
                {
                    uv = input.positionWS.xy;
                }

                // Minor Grid Lines
                float spacing = max(_GridSpacing, 0.25);
                float2 coord = uv / spacing;
                float2 gridDist = abs(frac(coord - 0.5) - 0.5); // distance to integer in [0, 0.5]
                float2 deriv = max(fwidth(coord), 0.0001);
                float halfWidth = (_LineWidth / spacing) * 0.5;
                float2 aa = saturate((halfWidth - gridDist) / deriv);
                float minorLine = max(aa.x, aa.y);

                // Major Grid Lines
                float majorSpacing = spacing * max(_MajorInterval, 1.0);
                float2 majorCoord = uv / majorSpacing;
                float2 majorGridDist = abs(frac(majorCoord - 0.5) - 0.5);
                float2 majorDeriv = max(fwidth(majorCoord), 0.0001);
                float majorHalfWidth = ((_LineWidth * 1.6) / majorSpacing) * 0.5;
                float2 majorAA = saturate((majorHalfWidth - majorGridDist) / majorDeriv);
                float majorLine = max(majorAA.x, majorAA.y);

                // Laser Glow fringe
                float minEdge = min(gridDist.x, gridDist.y);
                float glow = saturate(1.0 - (minEdge / max(halfWidth * 3.5, 0.01))) * 0.40;

                // Distance fade
                float dist = length(input.positionWS - _WorldSpaceCameraPos);
                float distFade = 1.0 - smoothstep(_FadeStart, _FadeEnd, dist);

                // Subtle animated pulse wave radiating outward
                float wave = 0.92 + 0.08 * sin(dist * 0.4 - _Time.y * _PulseSpeed * 2.5);

                // Deep obsidian glossy black floor/wall backdrop
                float3 obsidianBase = float3(0.012, 0.004, 0.008);

                // HDR Emissive Laser Red lines & glow
                float3 laserCol = _GridColor.rgb * (minorLine * 2.2 + majorLine * 4.5 + glow * 1.0) * distFade * wave;

                float3 finalColor = obsidianBase + laserCol;
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
