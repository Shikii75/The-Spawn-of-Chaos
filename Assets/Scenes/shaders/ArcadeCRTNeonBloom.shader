Shader "SpawnOfChaos/ArcadeCRTNeonBloom"
{
    Properties
    {
        [PerRendererData] _MainTex ("Arcade Display Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Curvature ("Screen Curvature", Range(0.0, 0.15)) = 0.04
        _ScanlineCount ("Scanline Frequency", Range(100.0, 800.0)) = 360.0
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 0.6)) = 0.18
        _ChromaticAberration ("Chromatic Aberration", Range(0.0, 0.02)) = 0.005
        _NeonBoost ("Neon Bloom Boost", Range(1.0, 2.5)) = 1.35
        _VignetteRoundness ("Vignette Intensity", Range(0.0, 1.0)) = 0.35
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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            float _Curvature;
            float _ScanlineCount;
            float _ScanlineIntensity;
            float _ChromaticAberration;
            float _NeonBoost;
            float _VignetteRoundness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Barrel distortion for CRT tube curvature
            float2 CurveUV(float2 uv)
            {
                float2 center = uv - 0.5;
                float dist = dot(center, center);
                return uv + center * dist * _Curvature * 2.0;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = CurveUV(IN.texcoord);

                // Discard pixels bent outside CRT bezel
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return fixed4(0.01, 0.01, 0.03, 1.0);
                }

                // Chromatic Aberration RGB split
                float2 offset = (uv - 0.5) * _ChromaticAberration;
                fixed r = tex2D(_MainTex, uv + offset).r;
                fixed g = tex2D(_MainTex, uv).g;
                fixed b = tex2D(_MainTex, uv - offset).b;
                fixed a = tex2D(_MainTex, uv).a;

                fixed3 col = fixed3(r, g, b);

                // Neon Bloom / Color Boost
                col *= _NeonBoost;

                // Subtle rolling scanlines
                float scanline = sin(uv.y * _ScanlineCount * 3.14159265 + (_Time.y * 3.0));
                float scanlineFactor = 1.0 - (abs(scanline) * _ScanlineIntensity);
                col *= scanlineFactor;

                // Subtle Vignette at corners
                float2 vigUV = (uv - 0.5) * 2.0;
                float vig = 1.0 - dot(vigUV, vigUV) * _VignetteRoundness * 0.5;
                col *= saturate(vig);

                return fixed4(col, a) * IN.color;
            }
        ENDCG
        }
    }
}
