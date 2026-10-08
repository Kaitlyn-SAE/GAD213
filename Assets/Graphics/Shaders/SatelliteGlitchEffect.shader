Shader "GrandStrategy/SatelliteGlitchEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.0
        _NoiseTex ("Noise Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

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
            float _GlitchIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Pseudo-random noise
            float randomNoise(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_GlitchIntensity <= 0.001)
                {
                    return tex2D(_MainTex, i.uv);
                }

                float2 uv = i.uv;

                // Horizontal scanline tearing
                float slice = floor(uv.y * 30.0);
                float noise = randomNoise(float2(_Time.y, slice));
                if (noise < _GlitchIntensity * 0.4)
                {
                    uv.x += (randomNoise(float2(_Time.y, uv.y)) - 0.5) * _GlitchIntensity * 0.15;
                }

                // Chromatic Aberration (RGB channel splitting)
                float split = _GlitchIntensity * 0.02;
                float r = tex2D(_MainTex, uv + float2(split, 0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(split, 0)).b;

                // Digital grain static
                float grain = (randomNoise(uv * _Time.y) - 0.5) * _GlitchIntensity * 0.6;

                fixed4 finalColor = fixed4(r + grain, g + grain, b + grain, 1.0);
                return finalColor;
            }
            ENDCG
        }
    }
}