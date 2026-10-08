Shader "GrandStrategy/HOI4MapShader"
{
    Properties
    {
        _MainTex ("Base Map Artwork", 2D) = "white" {}
        _ProvinceIDTex ("16-Bit Province ID Map", 2D) = "black" {}
        _LookupTex ("Province Faction Palette (256x256)", 2D) = "black" {}
        
        [Header(Faction Territory Tint)]
        _TintIntensity ("Faction Tint Opacity", Range(0, 1)) = 0.55

        [Header(Province Borders)]
        _ShowBorders ("Show Province Borders", Float) = 1.0
        _BorderWidth ("Border Width", Range(0.5, 3.0)) = 1.0
        _BorderColor ("Border Color", Color) = (0.1, 0.1, 0.1, 0.35)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _ProvinceIDTex;
            sampler2D _LookupTex;
            float4 _ProvinceIDTex_TexelSize;

            float _TintIntensity;
            float _ShowBorders;
            float _BorderWidth;
            float4 _BorderColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Base Map Artwork
                fixed4 baseColor = tex2D(_MainTex, i.uv);

                // 2. Sample 16-Bit Province ID (R = Low Byte, G = High Byte)
                fixed4 provSample = tex2D(_ProvinceIDTex, i.uv);

                // 3. Snap R and G directly to integers
                float u = (round(provSample.r * 255.0) + 0.5) / 256.0;
                float v = (round(provSample.g * 255.0) + 0.5) / 256.0;

                fixed4 factionTint = tex2D(_LookupTex, float2(u, v));

                // 4. Procedural Border Edge Detection (Identifies borders where Province ID changes)
                float isBorder = 0.0;
                if (_ShowBorders > 0.5 && provSample.a > 0.5)
                {
                    float2 offset = _ProvinceIDTex_TexelSize.xy * _BorderWidth;
                    fixed4 pLeft  = tex2D(_ProvinceIDTex, i.uv - float2(offset.x, 0));
                    fixed4 pRight = tex2D(_ProvinceIDTex, i.uv + float2(offset.x, 0));
                    fixed4 pUp    = tex2D(_ProvinceIDTex, i.uv + float2(0, offset.y));
                    fixed4 pDown  = tex2D(_ProvinceIDTex, i.uv - float2(0, offset.y));

                    isBorder = (distance(provSample.rg, pLeft.rg)  > 0.003) ||
                               (distance(provSample.rg, pRight.rg) > 0.003) ||
                               (distance(provSample.rg, pUp.rg)    > 0.003) ||
                               (distance(provSample.rg, pDown.rg)  > 0.003);
                }

                // 5. Apply Faction Tint only to Land Provinces (Alpha > 0.5)
                if (provSample.a > 0.5 && factionTint.a > 0.01)
                {
                    baseColor = lerp(baseColor, baseColor * factionTint, _TintIntensity * factionTint.a);
                }

                // 6. Blend subtle border lines
                if (isBorder > 0.5)
                {
                    baseColor = lerp(baseColor, _BorderColor, _BorderColor.a);
                }

                return baseColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}