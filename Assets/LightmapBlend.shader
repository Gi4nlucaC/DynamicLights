Shader "Custom/URP_LightmapShadowmask_Fixed"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
        _LightmapDay ("Lightmap Day", 2D) = "white" {}
        _LightmapNight("Lightmap Night", 2D) = "white" {}
        _LightmapDirDay("Lightmap Directional Day", 2D) = "white" {}
        _LightmapDirNight("Lightmap Directional Night", 2D) = "white" {}
        _Blend("Lightmap Blender", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Properties
            float4 _BaseColor;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_LightmapDay); SAMPLER(sampler_LightmapDay);
            TEXTURE2D(_LightmapNight); SAMPLER(sampler_LightmapNight);
            TEXTURE2D(_LightmapDirDay); SAMPLER(sampler_LightmapDirDay);
            TEXTURE2D(_LightmapDirNight); SAMPLER(sampler_LightmapDirNight);
            float _Blend;
            
            // **NUOVO**: Dichiarazione della texture della shadowmask
            //TEXTURE2D(unity_ShadowMask); SAMPLER(samplerunity_ShadowMask);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS    : SV_POSITION;
                float2 uv             : TEXCOORD0;
                float3 normalWS       : TEXCOORD1;
                float2 staticLightmapUV : TEXCOORD2;
                float3 vertexSH       : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = normInputs.normalWS;
                OUT.uv = IN.uv;
                OUT.staticLightmapUV = IN.uv2 * unity_LightmapST.xy + unity_LightmapST.zw;
                OUT.vertexSH = SampleSH(OUT.normalWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * _BaseColor.rgb;

                #ifdef UNITY_LIGHTMAP_FULL_HDR
                bool encodedLightmap = false;
            #else
                bool encodedLightmap = true;
            #endif
            
                half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
            
                // The shader library sample lightmap functions transform the lightmap uv coords to apply bias and scale.
                // However, universal pipeline already transformed those coords in vertex. We pass half4(1, 1, 0, 0) and
                // the compiler will optimize the transform away.
                half4 transformCoords = half4(1, 1, 0, 0);
                
                // 1. Campiona il colore per GIORNO e NOTTE
                //float3 colorDay = SampleDirectionalLightmap(
                //    TEXTURE2D_LIGHTMAP_ARGS(_LightmapDay, sampler_LightmapDay),
                //    TEXTURE2D_LIGHTMAP_ARGS(_LightmapDirDay, sampler_LightmapDirDay),
                //    IN.staticLightmapUV, normalWS);

                float3 colorDay = SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_ARGS(_LightmapDay, sampler_LightmapDay), TEXTURE2D_LIGHTMAP_ARGS(_LightmapDirDay, sampler_LightmapDirDay), IN.staticLightmapUV, transformCoords, normalWS, encodedLightmap, decodeInstructions);

                //float3 colorNight = SampleDirectionalLightmap(
                //    TEXTURE2D_LIGHTMAP_ARGS(_LightmapNight, sampler_LightmapNight),
                //    TEXTURE2D_LIGHTMAP_ARGS(_LightmapDirNight, sampler_LightmapDirNight),
                //    IN.staticLightmapUV, normalWS);

                float3 colorNight = SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_ARGS(_LightmapNight, sampler_LightmapNight), TEXTURE2D_LIGHTMAP_ARGS(_LightmapDirNight, sampler_LightmapDirNight), IN.staticLightmapUV, transformCoords, normalWS, encodedLightmap, decodeInstructions);

                // 2. Campiona l'occlusione dalla shadowmask di Unity
                // La shadowmask è unica, non c'è una versione giorno/notte
                float occlusion = 1.0;
                #if defined(SHADOWS_SHADOWMASK)
                    // Il canale .a contiene l'occlusione per le luci dinamiche (Distance Shadowmask)
                    occlusion = SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, IN.staticLightmapUV).a;
                #endif

                // 4. Interpola tra i due float4
                float3 bakedGI = lerp(colorDay, colorNight, _Blend);

                // --- Ricostruisci GI come fa Unity ---
                float3 gi = bakedGI + IN.vertexSH; // add spherical harmonics
                gi *= occlusion;                        // applica occlusion

                // --- Colore finale ---
                float3 finalColor = baseCol * gi;

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}