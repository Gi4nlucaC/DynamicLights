Shader "Custom/URP_LightmapBlendUnlit"
{
    Properties
    {
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        
        _DayLightmap ("Day Lightmap", 2D) = "white" {}
        _NightLightmap ("Night Lightmap", 2D) = "black" {}
        _Blend("Day/Night Blend", Range(0, 1)) = 0

        [HideInInspector] _LightmapST("Lightmap ST", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            // Dichiarazione delle texture e delle variabili
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_DayLightmap);    SAMPLER(sampler_DayLightmap);
            TEXTURE2D(_NightLightmap);  SAMPLER(sampler_NightLightmap);

            float4 _MainTex_ST;
            float4 _LightmapST;
            float _Blend;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 1. Colore Base (Albedo) ---
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // --- 2. Calcolo Lightmap Personalizzata ---
                float2 lightmapUV = IN.uv * _LightmapST.xy + _LightmapST.zw;
                
                half3 dayLight = SAMPLE_TEXTURE2D(_DayLightmap, sampler_DayLightmap, lightmapUV).rgb;
                half3 nightLight = SAMPLE_TEXTURE2D(_NightLightmap, sampler_NightLightmap, lightmapUV).rgb;
                
                // Anche in URP è importante decodificare i dati della lightmap
                dayLight = DecodeLightmap(half4(dayLight, 1.0));
                nightLight = DecodeLightmap(half4(nightLight, 1.0));

                half3 blendedLight = lerp(dayLight, nightLight, _Blend);

                // --- 3. Applica il Risultato ---
                // Poiché è uno shader Unlit, moltiplichiamo l'albedo per l'illuminazione calcolata
                half3 finalColor = albedo.rgb * blendedLight;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
    }
}