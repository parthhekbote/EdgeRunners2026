Shader "Cyberpunk/Tron_Blade_LightWall"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _NeonColor ("Neon Color", Color) = (0.0, 3.2, 4.2, 1.0)
        [HDR] _CoreColor ("Core Laser Color", Color) = (4.0, 4.0, 4.0, 1.0)
        _RailWidth ("Border Rail Thickness", Range(0.01, 0.35)) = 0.12
        _RailGlow ("Border Rail Glow Multiplier", Range(1.0, 8.0)) = 3.0
        _ScanlineFreq ("Holographic Scanline Frequency", Range(2.0, 50.0)) = 22.0
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 2.0)) = 0.60
        _CoreFilamentWidth ("Core Filament Width", Range(0.05, 0.8)) = 0.30
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+120" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True"
        }
        LOD 100
        Blend One One // Pure Additive Tron Laser Glow
        ZWrite Off
        Cull Off

        Pass
        {
            Name "TronBladeTrailForward"
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
                float4 _NeonColor;
                float4 _CoreColor;
                float  _RailWidth;
                float  _RailGlow;
                float  _ScanlineFreq;
                float  _ScanlineIntensity;
                float  _CoreFilamentWidth;
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

                // uv.x is along the trail length: 0 = new (at blade edge), 1 = old (tail)
                // uv.y is across the ribbon height: 0 = hilt side, 1 = tip side

                float ageAlpha = saturate(1.0 - uv.x);
                float kineticTail = pow(ageAlpha, 1.4);

                // 1. Top & Bottom Tron Light Cycle Border Rails
                float topRail = smoothstep(1.0 - _RailWidth, 1.0, uv.y);
                float bottomRail = smoothstep(_RailWidth, 0.0, uv.y);
                float rails = (topRail + bottomRail * 0.70) * _RailGlow;

                // 2. Center White-Hot Core Laser Filament
                float centerDist = abs(uv.y - 0.5) * 2.0;
                float coreFilament = pow(saturate(1.0 - centerDist / _CoreFilamentWidth), 3.0) * 2.8;

                // 3. Digital Grid Scanlines running along the light wall
                float scanline = abs(frac(uv.y * _ScanlineFreq) - 0.5) * 2.0;
                float scanGrid = pow(scanline, 6.0) * _ScanlineIntensity;

                // 4. Leading Edge Flash near the sword
                float headBoost = pow(ageAlpha, 6.0) * 1.8;

                // 5. Composite Color
                half3 neon = _NeonColor.rgb * (kineticTail * 0.70 + rails * 0.45 * kineticTail + scanGrid * 0.70 * kineticTail);
                half3 core = _CoreColor.rgb * (coreFilament * 0.65 * kineticTail + headBoost * 0.60 + topRail * 0.45 * kineticTail);

                half3 finalColor = (neon + core) * input.color.rgb;
                // Soft ceiling clamp to prevent over-blooming
                finalColor = min(finalColor, half3(2.2, 2.2, 2.2));

                half finalAlpha = saturate((kineticTail + rails * 0.35 + coreFilament * 0.4) * input.color.a);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
