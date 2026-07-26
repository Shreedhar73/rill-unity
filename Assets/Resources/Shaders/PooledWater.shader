// Lakes and sea. Depth comes in through vertex colour (green = depth, alpha = coverage), so a
// basin at 64% full looks exactly like a basin at 64% full with no extra data.
Shader "Rill/PooledWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.35, 0.72, 0.74, 1)
        _DeepColor    ("Deep", Color) = (0.05, 0.20, 0.38, 1)
        _SkyColor     ("Sky reflection", Color) = (0.78, 0.88, 0.98, 1)
        _RippleScale  ("Ripple scale", Range(0.02, 2)) = 0.35
        _RippleSpeed  ("Ripple speed", Range(0, 4)) = 0.8
        _RippleAmount ("Ripple amount", Range(0, 1)) = 0.22
        _Fresnel      ("Fresnel power", Range(0.5, 8)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        LOD 120
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float3 normal : NORMAL;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal   : TEXCOORD1;
                float4 color    : COLOR;
            };

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _SkyColor;
            float  _RippleScale;
            float  _RippleSpeed;
            float  _RippleAmount;
            float  _Fresnel;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float depth01 = i.color.g;
                fixed3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                // Two crossed sine ripples: enough motion to read as liquid, free on any GPU.
                float t = _Time.y * _RippleSpeed;
                float r = sin(i.worldPos.x * _RippleScale + t) * sin(i.worldPos.z * _RippleScale * 1.3 - t * 0.8);
                float ripple = r * 0.5 + 0.5;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fres = pow(1.0 - saturate(dot(normalize(i.normal), viewDir)), _Fresnel);

                // Sky was ADDED on top of the body colour, so fresnel plus ripple pushed every
                // pixel toward white and the lake rendered as a grey film rather than water.
                // Blending toward the sky instead keeps the water's own colour dominant.
                float sky = saturate(fres * 0.45 + ripple * _RippleAmount * 0.25);
                fixed3 col = lerp(body, _SkyColor.rgb, sky);
                float alpha = saturate(i.color.a * (0.72 + fres * 0.45));
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
