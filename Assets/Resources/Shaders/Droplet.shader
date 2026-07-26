// Splash droplets. A soft radial dot tinted by the particle colour — no texture, so the whole
// effects budget of the game is one shader and no memory.
Shader "Rill/Droplet"
{
    Properties
    {
        _Softness ("Softness", Range(0.01, 1)) = 0.55
        _Core     ("Core brightness", Range(0, 2)) = 1.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _Softness;
            float _Core;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float mask = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                fixed3 col = i.color.rgb * (1.0 + (1.0 - d) * (_Core - 1.0));
                return fixed4(col, i.color.a * mask);
            }
            ENDCG
        }
    }

    Fallback Off
}
