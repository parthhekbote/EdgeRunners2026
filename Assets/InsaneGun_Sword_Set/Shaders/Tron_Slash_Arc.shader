Shader "Cyberpunk/Tron_Slash_Arc"
{
    Properties
    {
        [HDR] _MainColor ("Neon Glow Color", Color) = (0.0, 3.2, 4.5, 1.0)
        [HDR] _CoreColor ("Laser Core Color", Color) = (4.5, 4.5, 4.5, 1.0)
        _TopRailWidth ("Top Rail Thickness", Range(0.01, 0.3)) = 0.08
        _BottomRailWidth ("Bottom Rail Thickness", Range(0.01, 0.3)) = 0.06
        _RailIntensity ("Rail Glow Multiplier", Range(1.0, 8.0)) = 3.2
        _GridFrequency ("Concentric Grid Scanlines", Range(2.0, 40.0)) = 18.0
        _GridIntensity ("Grid Line Intensity", Range(0.0, 2.0)) = 0.55
        _LeadingEdgePower ("Leading Edge Sharpness", Range(1.0, 32.0)) = 8.0
        _LeadingEdgeBoost ("Leading Edge Boost", Range(1.0, 10.0)) = 4.2
        _Dissolve ("De-Rez Dissolve Progress", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+150" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True"
        }
        LOD 100
        Blend One One // Pure Additive Tron Laser Glow
        ZWrite Off
        Cull Off // Double-sided for full 3D visibility from any camera angle

        Pass
        {
            Name "TronSlashForward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _CoreColor;
                float  _TopRailWidth;
                float  _BottomRailWidth;
                float  _RailIntensity;
                float  _GridFrequency;
                float  _GridIntensity;
                float  _LeadingEdgePower;
                float  _LeadingEdgeBoost;
                float  _Dissolve;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. Digital De-Rez Dissolve cutoff from trailing edge forward
                float dissolveThreshold = _Dissolve * 1.15;
                if (uv.x < dissolveThreshold)
                {
                    discard;
                }

                // Smooth feathering just ahead of the dissolve boundary
                float dissolveEdge = saturate((uv.x - dissolveThreshold) / 0.12);

                // 2. Outer & Inner Tron Light Cycle Neon Border Rails (Top & Bottom pipes)
                float topRail = smoothstep(1.0 - _TopRailWidth, 1.0, uv.y);
                float bottomRail = smoothstep(_BottomRailWidth, 0.0, uv.y);
                float rails = (topRail + bottomRail * 0.75) * _RailIntensity;

                // 3. Concentric Digital Grid Scanlines (Tron cybernetic data ribbons)
                float gridLine = abs(frac(uv.y * _GridFrequency) - 0.5) * 2.0;
                float gridScan = pow(gridLine, 8.0) * _GridIntensity;

                // 4. Balanced Leading Blade Edge (crisp laser core without blinding blowout)
                float leadingEdge = pow(saturate(uv.x), _LeadingEdgePower) * (_LeadingEdgeBoost * 0.5);
                float coreFilament = pow(saturate(uv.x), 16.0) * (topRail * 0.8 + 0.35);

                // 5. Kinetic Trail Falloff Curve
                float trailAlpha = pow(saturate(uv.x), 2.2);

                // 6. Color Composition
                // Crisp Tron neon colored body with prominent digital grid scanlines
                half3 neonBody = _MainColor.rgb * (trailAlpha * 0.70 + rails * 0.50 * trailAlpha + gridScan * trailAlpha);
                // Clean white laser core filament at the cutting edge
                half3 laserCore = _CoreColor.rgb * (leadingEdge * 0.65 + coreFilament * 0.75);

                half3 finalColor = (neonBody + laserCore) * dissolveEdge * input.color.rgb;
                // Soft ceiling clamp to completely prevent camera bloom blowout
                finalColor = min(finalColor, half3(2.4, 2.4, 2.4));

                half finalAlpha = saturate((trailAlpha + rails * 0.4 + leadingEdge * 0.4) * dissolveEdge * input.color.a);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
