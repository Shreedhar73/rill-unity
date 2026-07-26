// Ecosystem props: moss, reeds, bushes, huts, and the markers on revealed secrets. Instanced,
// unlit-with-a-hint-of-lambert, two-sided so crossed quads read as tufts from any angle.
Shader "Rill/Prop"
{
    Properties
    {
        _Color   ("Color", Color) = (0.4, 0.6, 0.35, 1)
        _Ambient ("Ambient", Color) = (0.45, 0.48, 0.55, 1)
        _Wrap    ("Diffuse wrap", Range(0,1)) = 0.6
        _Cutoff  ("Alpha cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        LOD 120
        Cull Off

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 2.5
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 normal : TEXCOORD0;
                fixed4 shade  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // One material per prop type, so colour is a plain uniform: instancing only has to
            // batch transforms, which keeps this working on the oldest devices we target.
            fixed4 _Color;
            fixed4 _Ambient;
            float  _Wrap;
            float  _Cutoff;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                // Baked vertical shading from PropMeshes. One material per prop type keeps these
                // instanced, so vertex colour is the only channel that can give a prop internal
                // form — without it a conifer is one flat tone and reads as a paper cutout.
                o.shade = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 c = _Color;
                c.rgb *= i.shade.rgb;
                clip(c.a - _Cutoff * 0.01);

                float3 n = normalize(i.normal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float diff = saturate((dot(n, l) + _Wrap) / (1.0 + _Wrap));
                fixed3 lit = c.rgb * (_Ambient.rgb + _LightColor0.rgb * diff);
                return fixed4(lit, c.a);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
