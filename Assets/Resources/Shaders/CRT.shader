// Arcade CRT presentation: barrel curvature, scanlines, aperture grille, chromatic fringe, glow and vignette.
// Used on the full-screen RawImage that shows the low-resolution game render texture.
Shader "Retro/CRT"
{
    Properties
    {
        _MainTex ("Game", 2D) = "black" {}
        _SourceSize ("Source Size (px)", Vector) = (384, 216, 0, 0)
        _Curvature ("Curvature", Range(0, 0.3)) = 0.09
        _Scanline ("Scanline Strength", Range(0, 1)) = 0.42
        _Mask ("Aperture Mask Strength", Range(0, 1)) = 0.22
        _Fringe ("Chromatic Fringe (px)", Range(0, 2)) = 0.55
        _Glow ("Glow", Range(0, 1)) = 0.32
        _Vignette ("Vignette", Range(0, 1)) = 0.38
        _Flicker ("Flicker", Range(0, 0.1)) = 0.018
        _Brightness ("Brightness", Range(0.5, 2)) = 1.22
        _TimeValue ("Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _SourceSize;
            float _Curvature, _Scanline, _Mask, _Fringe, _Glow, _Vignette, _Flicker, _Brightness, _TimeValue;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float2 Curve(float2 uv)
            {
                float2 c = uv * 2.0 - 1.0;
                c *= 1.0 + (c.yx * c.yx) * _Curvature;
                return c * 0.5 + 0.5;
            }

            // Sharp horizontal sampling with a hint of softness, hard vertical rows (so scanlines stay readable).
            float3 SampleSharp(float2 uv)
            {
                float2 texel = uv * _SourceSize.xy;
                float2 centre = floor(texel) + 0.5;
                float fx = texel.x - centre.x;
                float blend = smoothstep(0.25, 0.5, abs(fx));
                float2 neighbour = centre + float2(sign(fx), 0);
                float3 a = tex2D(_MainTex, centre / _SourceSize.xy).rgb;
                float3 b = tex2D(_MainTex, neighbour / _SourceSize.xy).rgb;
                return lerp(a, b, blend * 0.5);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = Curve(i.uv);
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    return fixed4(0, 0, 0, 1);

                float2 fromCentre = uv - 0.5;
                float fringe = _Fringe / _SourceSize.x * (0.4 + length(fromCentre) * 2.0);
                float3 col;
                col.r = SampleSharp(uv + float2(fringe, 0)).r;
                col.g = SampleSharp(uv).g;
                col.b = SampleSharp(uv - float2(fringe, 0)).b;

                // Cheap glow: average of a small cross around the pixel, added on top of bright areas.
                float2 step = 1.5 / _SourceSize.xy;
                float3 glow = tex2D(_MainTex, uv + float2(step.x, 0)).rgb + tex2D(_MainTex, uv - float2(step.x, 0)).rgb
                            + tex2D(_MainTex, uv + float2(0, step.y)).rgb + tex2D(_MainTex, uv - float2(0, step.y)).rgb;
                glow *= 0.25;
                col += glow * glow * _Glow;

                // Scanlines follow source rows.
                float row = frac(uv.y * _SourceSize.y);
                float scan = 1.0 - _Scanline * pow(abs(row - 0.5) * 2.0, 2.2);
                col *= scan;

                // Aperture grille on output pixels.
                float px = fmod(floor(i.pos.x), 3.0);
                float3 mask = px < 1 ? float3(1, 1 - _Mask, 1 - _Mask) : (px < 2 ? float3(1 - _Mask, 1, 1 - _Mask) : float3(1 - _Mask, 1 - _Mask, 1));
                col *= mask;

                float vignette = 1.0 - dot(fromCentre * 2.0, fromCentre * 2.0) * _Vignette * 0.5;
                col *= vignette;
                col *= 1.0 - _Flicker * (0.5 + 0.5 * sin(_TimeValue * 110.0));
                col *= _Brightness;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
