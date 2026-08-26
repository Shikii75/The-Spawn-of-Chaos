Shader "Sprites/RealisticPurpleWater"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TopColor ("Surface Pink Color", Color) = (1.0, 0.18, 0.58, 0.95)
        _BottomColor ("Deep Plum Color", Color) = (0.10, 0.015, 0.16, 0.98)
        _FoamColor ("Foam Highlight Color", Color) = (1.0, 0.66, 0.92, 1.0)
        _CausticColor ("Caustic Glow Color", Color) = (0.84, 0.22, 0.72, 1.0)
        _HighlightColor ("Surface Sheen Color", Color) = (1.0, 0.88, 0.96, 1.0)
        _FoamHeight ("Foam Crest Thickness", Range(0.001, 0.15)) = 0.045
        _CausticStrength ("Animated Caustic Strength", Range(0.0, 0.5)) = 0.16
        _SurfaceSheen ("Surface Sheen Strength", Range(0.0, 1.0)) = 0.3
        _WaveSpeed ("Surface Wave Speed", Float) = 1.8
        _WaveFrequency ("Surface Wave Frequency", Float) = 7.0
        _WaveAmplitude ("Surface Wave Amplitude", Float) = 0.025
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
            fixed4 _CausticColor;
            fixed4 _HighlightColor;
            float _FoamHeight;
            float _CausticStrength;
            float _SurfaceSheen;
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

                // Two moving wave bands overlap into soft underwater caustics.
                float waveA = sin(uv.x * 18.0 + uv.y * 10.0 - _Time.y * 1.7);
                float waveB = sin(uv.x * 31.0 - uv.y * 13.0 + _Time.y * 2.3);
                float causticPattern = saturate((waveA * waveB + 1.0) * 0.5);
                float caustic = (causticPattern - 0.5) * _CausticStrength;

                // Rich depth gradient from deep plum to luminous pink surface.
                float depthGradient = saturate(uv.y + caustic);
                fixed4 baseColor = lerp(_BottomColor, _TopColor, depthGradient);
                baseColor.rgb += _CausticColor.rgb * causticPattern * _CausticStrength;

                // A broken, softly glowing crest follows the animated surface.
                float crestWave = sin(uv.x * 24.0 - _Time.y * 2.2) * 0.018;
                float foamMask = smoothstep(1.0 - _FoamHeight, 1.0, uv.y + crestWave);
                fixed4 col = lerp(baseColor, _FoamColor, foamMask);

                // Broad moving sheen gives the water a liquid, reflective finish.
                float sheen = pow(saturate(sin(uv.x * 9.0 - _Time.y * 1.15) * 0.5 + 0.5), 8.0);
                sheen *= smoothstep(0.35, 1.0, uv.y) * _SurfaceSheen;
                col.rgb = lerp(col.rgb, _HighlightColor.rgb, sheen * 0.22);
                col.rgb += foamMask * _FoamColor.rgb * 0.14;
                col *= IN.color;

                return col;
            }
            ENDCG
        }
    }
}
