Shader "Cyberpunk/Alita_Tron_Compilation_Core"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Porcelain Skin Color", Color) = (0.92, 0.94, 0.96, 1.0)
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.2
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.85

        [Header(Compilation Matrix)]
        _CompileProgress("Compilation Progress (0 to 1)", Range(0.0, 1.0)) = 0.0
        [HDR] _CoreColor("Amber Voxel Core", Color) = (4.0, 1.8, 0.25, 1.0)
        [HDR] _ScanColor("Laser Scanline Edge", Color) = (5.0, 3.5, 1.0, 1.0)
        _ScanLineWidth("Scanline Width", Range(0.01, 0.15)) = 0.04
        _VoxelScale("Voxel Circuit Density", Float) = 45.0

        [Header(Neural Optic Seams)]
        [HDR] _SeamColor("Neural Circuit Seams", Color) = (4.0, 0.1, 0.2, 1.0)
        _SeamIntensity("Seam Intensity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float  _CompileProgress;
                float4 _CoreColor;
                float4 _ScanColor;
                float  _ScanLineWidth;
                float  _VoxelScale;
                float4 _SeamColor;
                float  _SeamIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = normalInput.normalWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // World-space Z height along slab from boots (-0.82m) to head (+0.94m)
                // Head is at Top (Z = +0.94m, normalizedHeight = 1.0)
                // Boots are at Bottom (Z = -0.82m, normalizedHeight = 0.0)
                float normalizedHeight = saturate((input.positionWS.z - (-0.82)) / 1.76);
                
                // Top-to-Bottom compilation wave:
                // When _CompileProgress is 0: compileThreshold = 1.0 (Head starts compiling first)
                // When _CompileProgress is 1: compileThreshold = 0.0 (Boots finish last)
                float compileThreshold = 1.0 - _CompileProgress;
                float diff = normalizedHeight - compileThreshold;

                // Vectors
                float3 N = normalize(input.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);

                // Main directional light
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float NdotL = saturate(dot(N, L));
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float spec = pow(NdotH, 24.0) * _Smoothness * 0.65;
                float fresnel = pow(1.0 - saturate(dot(N, V)), 3.5);

                float3 totalDiffuse = mainLight.color * NdotL;
                float3 totalSpec = mainLight.color * spec;

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    float3 addL = normalize(addLight.direction);
                    float addNdotL = saturate(dot(N, addL));
                    float3 addH = normalize(addL + V);
                    float addSpec = pow(saturate(dot(N, addH)), 24.0) * _Smoothness * 0.65;
                    float atten = addLight.distanceAttenuation;
                    totalDiffuse += addLight.color * (addNdotL * atten);
                    totalSpec += addLight.color * (addSpec * atten);
                }
                #endif

                // Soft ambient
                float3 ambient = SampleSH(N) * 0.35 + float3(0.08, 0.05, 0.06);

                // Sample character color map (preserving all face features, lips, eyes, eyebrows, hair)
                float4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 characterAlbedo = texCol.rgb * _BaseColor.rgb;

                // TRON Ares glowing neural crimson seams
                float seamMask = saturate(sin(input.uv.y * 70.0 + input.uv.x * 40.0) * 2.0 - 0.9);
                float3 seamEmission = _SeamColor.rgb * seamMask * _SeamIntensity * 3.5;

                // Compiled surface: beautifully shaded porcelain skin / cyber suit with specular gloss and rim
                float3 litSurface = characterAlbedo * (totalDiffuse + ambient) + totalSpec;
                float3 rimGlow = float3(1.0, 0.25, 0.15) * fresnel * 0.45;
                float3 compiledColor = litSurface + rimGlow + seamEmission;

                // Uncompiled matrix: GLOWING 3D NANO-CUBES & QUANTUM VOXEL LATTICE
                float3 cubeCoord = input.positionOS * _VoxelScale;
                float3 cubeCell = floor(cubeCoord);
                float3 cubeFract = abs(frac(cubeCoord) - 0.5);
                float cubeEdge = step(0.36, max(cubeFract.x, max(cubeFract.y, cubeFract.z)));
                float cubeCore = 1.0 - cubeEdge;
                float cellHash = frac(sin(dot(cubeCell, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
                float cubePulse = 0.85 + 0.35 * sin(_Time.y * 7.0 + cellHash * 6.28);

                // Near scanline boundary (diff between -0.16 and 0): Nano-cubes heat up with incandescent energy!
                float cubeHeat = saturate((diff + 0.16) / 0.16);
                float3 cubeColor = lerp(_CoreColor.rgb, _ScanColor.rgb, cubeHeat);
                float3 nanoEmission = cubeColor * (cubeEdge * 2.8 + cubeCore * cubeHeat * 1.6) * cubePulse;

                float3 uncompiledBase = characterAlbedo * 0.06 + float3(0.04, 0.02, 0.03) * (totalDiffuse + ambient);
                float3 rawCore = uncompiledBase + nanoEmission + rimGlow * 0.45;

                // Searing laser scanline transition edge with intense incandescent flash (sweeping from top to bottom)
                float isScanning = step(0.001, _CompileProgress) * step(_CompileProgress, 0.999);
                float scanEdge = smoothstep(_ScanLineWidth, 0.0, abs(diff)) * isScanning;
                float3 scanEmission = _ScanColor.rgb * scanEdge * 3.8;

                // Final blend: TOP TO BOTTOM compilation
                // Above scanline (diff >= 0): solid compiled Alita
                // Below scanline (diff < 0): glowing nano-cubes assembling
                float compiledMask = (_CompileProgress >= 0.999) ? 1.0 : ((_CompileProgress <= 0.0005) ? 0.0 : step(0.0, diff));
                float3 finalColor = lerp(rawCore, compiledColor, compiledMask) + scanEmission;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
