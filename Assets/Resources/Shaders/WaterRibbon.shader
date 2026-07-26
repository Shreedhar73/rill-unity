// The stream. A luminous ribbon with soft edges and flow lines that speed up with the water,
// because this is the object the player's eye is locked to for the whole run.
Shader "Rill/WaterRibbon"
{
    Properties
    {
        _SlowColor  ("Slow water", Color) = (0.35, 0.66, 0.86, 1)
        _FastColor  ("Fast water", Color) = (0.72, 0.94, 1.00, 1)
        _FoamColor  ("Foam", Color) = (1, 1, 1, 1)
        _FlowSpeed  ("Flow speed", Range(0, 8)) = 2.6
        _EdgeSoft   ("Edge softness", Range(0.01, 1)) = 0.55
        _FoamAmount ("Foam amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _SlowColor;
            fixed4 _FastColor;
            fixed4 _FoamColor;
            float  _FlowSpeed;
            float  _EdgeSoft;
            float  _FoamAmount;

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
                // uv.x runs across the ribbon, uv.y along it. Energy rides in the green channel.
                float energy = i.color.g;
                float across = abs(i.uv.x - 0.5) * 2.0;

                // Soft shoulder rather than a hard edge: water has no outline.
                float body = 1.0 - smoothstep(1.0 - _EdgeSoft, 1.0, across);

                float scroll = i.uv.y - _Time.y * (_FlowSpeed * (0.35 + energy));
                float lines = sin(scroll * 12.0) * 0.5 + 0.5;
                float foam = pow(lines, 3.0) * _FoamAmount * (0.25 + energy);

                fixed3 col = lerp(_SlowColor.rgb, _FastColor.rgb, energy);
                col += _FoamColor.rgb * foam * (1.0 - across * 0.6);

                // Centre of the ribbon reads brighter, which sells volume without any geometry.
                col *= 1.0 + (1.0 - across) * 0.25;

                float alpha = i.color.a * body;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
