// Geology as papercraft.
//
// The strata are computed PER PIXEL from world height, not interpolated from vertex colours.
// That distinction is the whole art direction: at 2 m vertex spacing and ~10 m bands, vertex
// interpolation smears the layers into a gradient and the mountain reads as a bedsheet. Banding
// in the fragment shader gives hard contour edges at any mesh resolution, so every metre the
// player carves exposes a visibly different layer — the core verb and the reward, same pixels.
Shader "Rill/Strata"
{
    Properties
    {
        _Ambient      ("Ambient", Color) = (0.40, 0.45, 0.56, 1)
        _SunTint      ("Sun tint", Color) = (1.0, 0.96, 0.88, 1)
        _Wrap         ("Diffuse wrap", Range(0,1)) = 0.35

        _BandHeight   ("Metres per stratum", Range(0.5, 12)) = 3.2
        _BandContrast ("Band contrast", Range(0, 0.6)) = 0.17
        _SeamDarken   ("Seam darkness", Range(0, 1)) = 0.34
        _SeamWidth    ("Seam width", Range(0.01, 0.5)) = 0.07

        _AOStrength   ("Occlusion strength", Range(0, 1)) = 0.75
        _WetDarken    ("Wet darkening", Range(0, 1)) = 0.28
        _CliffDarken  ("Cliff darkening", Range(0, 1)) = 0.30

        _RimColor     ("Rim", Color) = (0.85, 0.92, 1.0, 1)
        _RimPower     ("Rim power", Range(0.5, 8)) = 3.2
        _RimStrength  ("Rim strength", Range(0, 1)) = 0.14
        _OverlayColor ("Carve overlay", Color) = (0.55, 0.95, 1.0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 150

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;   // x = occlusion, y = wetness
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 normal   : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv       : TEXCOORD2;
                float4 color    : COLOR;
            };

            fixed4 _Ambient, _SunTint, _RimColor, _OverlayColor, _HazeColor;
            float  _Wrap, _RimPower, _RimStrength;
            float  _BandHeight, _BandContrast, _SeamDarken, _SeamWidth;
            float  _AOStrength, _WetDarken, _CliffDarken;
            float  _HazeStart, _HazeRange, _HazeMax;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // Cheap stable hash so each stratum gets its own slight tone. Deterministic in world
            // space, so a band keeps its character as the player erodes down into it.
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float flatness = saturate(n.y);

                // ---- strata, per pixel
                float f = i.worldPos.y / _BandHeight;
                float bandIndex = floor(f);
                float withinBand = f - bandIndex;

                fixed3 albedo = i.color.rgb;

                // Each layer sits slightly lighter or darker than its neighbours.
                float tone = 1.0 + (Hash11(bandIndex) - 0.5) * 2.0 * _BandContrast;
                albedo *= tone;

                // A dark seam at every bedding plane. This is the line that makes rock read as rock.
                float seam = smoothstep(0.0, _SeamWidth, withinBand) *
                             smoothstep(1.0, 1.0 - _SeamWidth, withinBand);
                albedo *= lerp(1.0 - _SeamDarken, 1.0, seam);

                // Seams are erased by the eye on near-vertical faces, so fade them out there and
                // let slope shading carry the form instead.
                albedo = lerp(i.color.rgb * tone, albedo, saturate(flatness * 1.6));

                // ---- form
                float ao = lerp(1.0, saturate(i.uv.x), _AOStrength);
                float cliff = lerp(1.0 - _CliffDarken, 1.0, flatness);
                float wet = saturate(i.uv.y);

                float ndl = dot(n, l);
                float diff = saturate((ndl + _Wrap) / (1.0 + _Wrap));

                fixed3 lit = albedo * (_Ambient.rgb + _LightColor0.rgb * _SunTint.rgb * diff) * ao * cliff;

                // Damp rock is darker and slightly glossier — the fringe life grows on.
                lit *= lerp(1.0, 1.0 - _WetDarken, wet);

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                lit += _RimColor.rgb * rim * _RimStrength;

                // Carve overlay: this run's own change, glowing for a few seconds after it ends.
                float glow = i.color.a;
                if (glow > 0.001)
                {
                    float pulse = 0.65 + 0.35 * sin(_Time.y * 4.0);
                    lit = lerp(lit, _OverlayColor.rgb, saturate(glow * pulse));
                }

                // Aerial perspective. Without it every ridge is the same contrast at every
                // distance, and the mountain reads as a flat map rather than a landscape with
                // depth — the single cheapest thing that makes terrain look big.
                float dist = length(_WorldSpaceCameraPos - i.worldPos);
                float haze = saturate((dist - _HazeStart) / max(_HazeRange, 1.0));
                haze = haze * haze * (3.0 - 2.0 * haze);          // smoothstep, so near ground is untouched
                lit = lerp(lit, _HazeColor.rgb, haze * _HazeMax);

                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
