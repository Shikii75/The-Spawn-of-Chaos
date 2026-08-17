Shader "Sprites/RealisticPurpleWater"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TopColor ("Surface Magenta Color", Color) = (0.85, 0.07, 0.49, 0.9)
        _BottomColor ("Deep Void Purple Color", Color) = (0.14, 0.02, 0.22, 0.95)
        _FoamColor ("Glow Foam Crest Color", Color) = (0.88, 0.25, 0.98, 1.0)
        _FoamHeight ("Foam Crest Thickness", Range(0.001, 0.15)) = 0.04
        _WaveSpeed ("Surface Wave Speed", Float) = 2.5
        _WaveFrequency ("Surface Wave Frequency", Float) = 8.0
        _WaveAmplitude ("Surface Wave Amplitude", Float) = 0.02
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            fixed4 _TopColor;
            fixed4 _BottomColor;
            fixed4 _FoamColor;
            float _FoamHeight;
            float _WaveSpeed;
            float _WaveFrequency;
            float _WaveAmplitude;
            sampler2D _MainTex;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                float wave = sin(_Time.y * _WaveSpeed + IN.vertex.x * _WaveFrequency) * _WaveAmplitude;
                float4 v = IN.vertex;
                
                // Displace upper vertices for dynamic surface motion
                if (IN.texcoord.y > 0.8)
                {
                    v.y += wave;
                }

                OUT.vertex = UnityObjectToClipPos(v);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                OUT.worldPos = mul(unity_ObjectToWorld, v).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Underwater distortion caustics
                float caustic = sin(_Time.y * 3.0 + uv.x * 12.0 + uv.y * 8.0) * 0.06;

                // Vertical gradient from bottom deep purple to top magenta
                fixed4 baseColor = lerp(_BottomColor, _TopColor, saturate(uv.y + caustic));

                // Glowing foam crest along top surface
                float foamMask = smoothstep(1.0 - _FoamHeight, 1.0, uv.y);
                fixed4 col = lerp(baseColor, _FoamColor, foamMask);

                // Add subtle surface highlight
                col.rgb += foamMask * 0.2;
                col *= IN.color;

                return col;
            }
            ENDCG
        }
    }
}
