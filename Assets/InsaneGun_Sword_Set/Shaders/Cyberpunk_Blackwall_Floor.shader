Shader "Cyberpunk/Blackwall_Cyberspace_Floor"
{
    Properties
    {
        [Header(Cyberpunk 2077 Cyberspace LIDAR Floor)]
        _BaseVoidColor("Void Base Color", Color) = (0.0, 0.0, 0.0, 1.0)
        [HDR] _DotColor("HDR Crimson Laser Dots", Color) = (5.0, 0.04, 0.15, 1.0)
        [HDR] _CyanAccentColor("HDR Cyan Accent Dots", Color) = (0.1, 1.2, 3.0, 1.0)
        
        [Header(Grid Density)]
        _GridSpacingX("Dot Spacing X (Columns)", Float) = 0.55
        _GridSpacingZ("Dot Spacing Z (Rows)", Float) = 0.85
        _DotLengthZ("Dot Dash Length Z", Range(0.05, 0.8)) = 0.35
        _DotWidthX("Dot Width X", Range(0.05, 0.8)) = 0.20
        _DotBrightness("Dot Emission Intensity", Float) = 2.5
        
        [Header(Distance and Perspective Fade)]
        _NearFade("Near Camera Fade Distance", Float) = 1.0
        _FarFade("Far Horizon Fade Distance", Float) = 45.0
        _PulseSpeed("Subtle Dot Pulse Speed", Float) = 1.2
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
            Name "CyberspaceLidarFloorPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float2 uv           : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseVoidColor;
                float4 _DotColor;
                float4 _CyanAccentColor;
                float _GridSpacingX;
                float _GridSpacingZ;
                float _DotLengthZ;
                float _DotWidthX;
                float _DotBrightness;
                float _NearFade;
                float _FarFade;
                float _PulseSpeed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv         = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 posWS = input.positionWS;
                float3 camPos = _WorldSpaceCameraPos;
                float distToCam = distance(posWS, camPos);

                // Grid coordinates in world space
                float2 gridCoord = float2(posWS.x / _GridSpacingX, posWS.z / _GridSpacingZ);
                float2 cellID = floor(gridCoord);
                float2 cellUV = frac(gridCoord) - 0.5; // [-0.5, 0.5]

                // Elongated dash along Z axis (perspective depth)
                float dX = abs(cellUV.x);
                float dZ = abs(cellUV.y);

                // Smooth capsule dash shape
                float maskX = smoothstep(_DotWidthX, 0.0, dX);
                float maskZ = smoothstep(_DotLengthZ, 0.0, dZ);
                float dotShape = maskX * maskZ;

                // Subtle organic pulse & jitter per dot
                float rnd = Hash21(cellID);
                float pulse = 0.8 + 0.25 * sin(_Time.y * _PulseSpeed + rnd * 6.28);
                
                // Color variation: mostly crimson red, subtle rare cyan dots near wall
                float isCyan = step(0.96, rnd) * saturate((posWS.z - 5.0) * 0.1);
                float3 dotColor = lerp(_DotColor.rgb, _CyanAccentColor.rgb, isCyan);

                // Distance falloff
                float distFade = smoothstep(_NearFade, _NearFade + 2.0, distToCam) * (1.0 - smoothstep(_FarFade * 0.6, _FarFade, distToCam));

                // Final emission
                float3 finalColor = _BaseVoidColor.rgb + dotColor * (dotShape * pulse * _DotBrightness * distFade);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
