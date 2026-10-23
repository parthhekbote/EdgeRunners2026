Shader "Cyberpunk/Tron_Infinite_Floor_Grid"
{
    Properties
    {
        [HDR] _GridColor("Grid Neon Color", Color) = (0.0, 3.2, 4.0, 1.0)
        _GridSpacing("Grid Spacing (Meters)", Float) = 2.5
        _LineWidth("Line Thickness", Range(0.005, 0.15)) = 0.035
        _FadeStart("Horizon Fade Start", Float) = 15.0
        _FadeEnd("Horizon Fade End", Float) = 75.0
        _PulseSpeed("Pulse Wave Speed", Float) = 1.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend One One // Pure additive glow
        ZWrite Off
        Cull Back

        Pass
        {
            Name "TronGridForward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float  _GridSpacing;
                float  _LineWidth;
                float  _FadeStart;
                float  _FadeEnd;
                float  _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 coord = input.positionWS.xz / max(_GridSpacing, 0.1);
                float2 grid = abs(frac(coord - 0.5) - 0.5);
                float2 derivative = fwidth(coord);
                float2 aa = smoothstep(_LineWidth + derivative, _LineWidth - derivative, grid);
                float lineMask = max(aa.x, aa.y);

                // Major grid lines every 5 units
                float2 majorCoord = coord / 5.0;
                float2 majorGrid = abs(frac(majorCoord - 0.5) - 0.5);
                float2 majorDerivative = fwidth(majorCoord);
                float2 majorAa = smoothstep(_LineWidth * 1.5 + majorDerivative, _LineWidth * 1.5 - majorDerivative, majorGrid);
                float majorLine = max(majorAa.x, majorAa.y) * 1.6;

                float finalLine = max(lineMask, majorLine);

                // Distance fade to black horizon
                float dist = length(input.positionWS.xz - _WorldSpaceCameraPos.xz);
                float distFade = 1.0 - smoothstep(_FadeStart, _FadeEnd, dist);
                float nearFade = smoothstep(3.0, 6.8, dist);

                // Subtle animated pulse wave radiating from center
                float wave = 0.9 + 0.1 * sin(dist * 0.35 - _Time.y * _PulseSpeed * 2.5);

                float3 col = _GridColor.rgb * finalLine * distFade * nearFade * wave;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
