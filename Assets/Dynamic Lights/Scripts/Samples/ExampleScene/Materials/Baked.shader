Shader "Custom/Baked"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}
        _LightmapDay ("Lightmap Day", 2D) = "white" {}
        _LightmapNight("Lightmap Night", 2D) = "white" {}
        _LightmapDirDay("Lightmap Directional Day", 2D) = "white" {}
        _LightmapDirNight("Lightmap Directional Night", 2D) = "white" {}

        _Blend("Lightmap Blender", Range(0,1)) = 0.0
 
        // BlendMode
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("Src", Float) = 1.0
        [HideInInspector] _DstBlend("Dst", Float) = 0.0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
 
        // Editmode props
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
 
        Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]
 
        Pass
        {
            Name "BakedLit"
            Tags{ "LightMode" = "UniversalForward" }
 
            // -------------------------------------
            // Render State Commands
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex BakedLitForwardPassVertex
            #pragma fragment BakedLitForwardPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            // Lighting include is needed because of GI
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_LightmapDay);   SAMPLER(sampler_LightmapDay);
            TEXTURE2D(_LightmapNight); SAMPLER(sampler_LightmapNight);
            TEXTURE2D(_LightmapDirDay);   SAMPLER(sampler_LightmapDirDay);
            TEXTURE2D(_LightmapDirNight); SAMPLER(sampler_LightmapDirNight);

            float _Blend;
        
            struct Attributes
            {
                float4 positionOS       : POSITION;
                float2 uv               : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float3 normalOS         : NORMAL;
                float4 tangentOS        : TANGENT;
 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
 
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv0AndFogCoord : TEXCOORD0; // xy: uv0, z: fogCoord
                half3 normalWS : TEXCOORD2;
                float2 staticLightmapUV : TEXCOORD1;
                float3 vertexSH       : TEXCOORD3;

                #if defined(_NORMALMAP)
                half4 tangentWS : TEXCOORD3;
                #endif

                #if defined(DEBUG_DISPLAY)
                float3 positionWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;

                #if defined(DEBUG_DISPLAY)
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = input.viewDirWS;
                #else
                inputData.positionWS = float3(0, 0, 0);
                inputData.viewDirectionWS = half3(0, 0, 1);
                #endif

                #if defined(_NORMALMAP)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);

                inputData.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
                #else
                inputData.normalWS = input.normalWS;
                #endif

                inputData.shadowCoord = float4(0, 0, 0, 0);
                inputData.fogCoord = input.uv0AndFogCoord.z;
                inputData.vertexLighting = half3(0, 0, 0);
                // --- HDR decode flags ---
                #ifdef UNITY_LIGHTMAP_FULL_HDR
                bool encodedLightmap = false;
                #else
                bool encodedLightmap = true;
                #endif
                half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
                half4 transformCoords = half4(1, 1, 0, 0);

                // --- Campiona lightmap giorno e notte ---
                float3 colorDay = SampleDirectionalLightmap(
                    TEXTURE2D_LIGHTMAP_ARGS(_LightmapDay, sampler_LightmapDay),
                    TEXTURE2D_LIGHTMAP_ARGS(_LightmapDirDay, sampler_LightmapDirDay),
                    input.staticLightmapUV, transformCoords, input.normalWS, encodedLightmap, decodeInstructions);

                float3 colorNight = SampleDirectionalLightmap(
                    TEXTURE2D_LIGHTMAP_ARGS(_LightmapNight, sampler_LightmapNight),
                    TEXTURE2D_LIGHTMAP_ARGS(_LightmapDirNight, sampler_LightmapDirNight),
                    input.staticLightmapUV, transformCoords, input.normalWS, encodedLightmap, decodeInstructions);

                // --- Blend day/night ---
                float3 blendedBakedRGB = lerp(colorDay, colorNight, _Blend);

                // --- Occlusion (shadowmask) ---
                float occlusion = 1.0;
                #if defined(SHADOWS_SHADOWMASK)
                    occlusion = SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, input.staticLightmapUV).a;
                #endif
                
                inputData.bakedGI = blendedBakedRGB + input.vertexSH;
                //inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                //inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.shadowMask = float4(occlusion, occlusion, occlusion, occlusion);


                #if defined(DEBUG_DISPLAY)
                #if defined(LIGHTMAP_ON)
                inputData.staticLightmapUV = input.staticLightmapUV;
                #else
                inputData.vertexSH = input.vertexSH;
                #endif
                #endif
            }
 
            Varyings BakedLitForwardPassVertex(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv0AndFogCoord.xy = TRANSFORM_TEX(input.uv, _BaseMap);
                #if defined(_FOG_FRAGMENT)
                output.uv0AndFogCoord.z = vertexInput.positionVS.z;
                #else
                output.uv0AndFogCoord.z = ComputeFogFactor(vertexInput.positionCS.z);
                #endif

                // normalWS and tangentWS already normalize.
                // this is required to avoid skewing the direction during interpolation
                // also required for per-vertex SH evaluation
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                #if defined(_NORMALMAP)
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
                #endif
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                #if defined(DEBUG_DISPLAY)
                output.positionWS = vertexInput.positionWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                #endif

                return output;
            }
 
            void BakedLitForwardPassFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
                )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            
                half2 uv = input.uv0AndFogCoord.xy;
                #if defined(_NORMALMAP)
                half3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap)).xyz;
                #else
                half3 normalTS = half3(0, 0, 1);
                #endif
                InputData inputData;
                InitializeInputData(input, normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv0AndFogCoord.xy, _BaseMap);
            
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 color = texColor.rgb * _BaseColor.rgb;
                half alpha = texColor.a * _BaseColor.a;
            
                alpha = AlphaDiscard(alpha, _Cutoff);
                color = AlphaModulate(color, alpha);
            
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
            
            #ifdef _DBUFFER
                ApplyDecalToBaseColorAndNormal(input.positionCS, color, inputData.normalWS);
            #endif
            
                half4 finalColor = UniversalFragmentBakedLit(inputData, color, alpha, normalTS);
            
                finalColor.a = OutputAlpha(finalColor.a, _Surface);
                outColor = finalColor;

            #ifdef _WRITE_RENDERING_LAYERS
                uint renderingLayers = GetMeshRenderingLayer();
                outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
            #endif

            }
            ENDHLSL
        }
 
        Pass
        {
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture with the forward renderer or the depthNormal prepass with the deferred renderer.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // Same as DepthNormals pass, but used for deferred renderer and forwardOnly materials.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // Defines
            #define BUMP_SCALE_NOT_SUPPORTED 1

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaUnlit

            // -------------------------------------
            // Unity defined keywords
            #pragma shader_feature EDITOR_VISUALIZATION

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"

            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
    CustomEditor "MyBakedShaderGUI"
}