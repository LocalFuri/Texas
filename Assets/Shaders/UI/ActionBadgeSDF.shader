Shader "UI/ActionBadgeSDF"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RectSize ("Rect Size (px)", Vector) = (156, 64, 0, 0)
        _PillSize ("Pill Size (px)", Vector) = (120, 40, 0, 0)
        _CornerRadiusPx ("Corner Radius (px)", Float) = 20
        _BorderWidthPx ("Border Width (px)", Float) = 5
        _GlowSpreadPx ("Glow Spread (px)", Float) = 28
        _GlowStrength ("Glow Strength", Range(0, 4)) = 2.4
        _GlowFalloff ("Glow Falloff", Range(0.8, 3)) = 1.15
        _BorderColor ("Border Color", Color) = (0, 0.6666667, 1, 1)
        _FillColorTop ("Fill Top", Color) = (0, 0, 0, 1)
        _FillColorBot ("Fill Bottom", Color) = (0, 0, 0, 1)
        _HighlightColor ("Highlight", Color) = (0.92, 0.94, 1, 1)
        _HighlightStrength ("Highlight Strength", Range(0, 0.6)) = 0.38
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;
            float2 _RectSize;
            float2 _PillSize;
            float _CornerRadiusPx;
            float _BorderWidthPx;
            float _GlowSpreadPx;
            float _GlowStrength;
            float _GlowFalloff;
            fixed4 _BorderColor;
            fixed4 _FillColorTop;
            fixed4 _FillColorBot;
            fixed4 _HighlightColor;
            float _HighlightStrength;

            float sdRoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 ApplyClip(fixed4 color, float4 worldPosition)
            {
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 pixelOffset = (IN.texcoord - 0.5) * max(_RectSize, float2(0.001, 0.001));
                float2 halfPill    = max(_PillSize * 0.5, float2(0.001, 0.001));
                float  cornerR     = min(max(_CornerRadiusPx, 0.001), min(halfPill.x, halfPill.y));
                float  borderW     = max(_BorderWidthPx, 0.001);
                float  spread      = max(_GlowSpreadPx, 0.001);

                float dist = sdRoundedBox(pixelOffset, halfPill, cornerR);
                float aa   = max(fwidth(dist), 0.5);

                // Solid dark fill — 1 inside the pill past the border band, 0 at the border.
                float fillAlpha   = 1.0 - smoothstep(-aa, aa, dist + borderW);

                // Neon border band (dist: -borderW to 0).
                // FIX: was `* (1 - smoothstep(...))` — that was inverted and leaked neon into fill.
                float strokeAlpha = smoothstep(aa, 0.0, dist)
                                  * smoothstep(-borderW - aa, -borderW + aa, dist);

                // Vertical fill gradient (defaults to black).
                float  yNorm   = saturate((pixelOffset.y + halfPill.y) / max(halfPill.y * 2.0, 0.001));
                fixed3 neonRgb = _BorderColor.rgb * IN.color.rgb;
                fixed3 fillRgb = lerp(_FillColorBot.rgb, _FillColorTop.rgb, yNorm);

                // Body: dark fill + neon stroke.
                fixed3 bodyRgb   = fillRgb * fillAlpha + neonRgb * strokeAlpha;
                float  bodyAlpha = saturate(max(fillAlpha, strokeAlpha));

                // Outer glow — (1-bodyAlpha) keeps it out of the opaque body region.
                float  glowT     = pow(saturate(1.0 - max(dist, 0.0) / spread), max(_GlowFalloff, 0.8));
                float  glowAlpha = saturate(glowT * _GlowStrength * (1.0 - bodyAlpha));

                // FIX: clean lerp composite — avoids squaring glowAlpha via double-multiply.
                fixed3 rgb   = lerp(neonRgb, bodyRgb, bodyAlpha);
                float  alpha = saturate(lerp(glowAlpha, 1.0, bodyAlpha)) * IN.color.a;

                return ApplyClip(fixed4(rgb, alpha), IN.worldPosition);
            }
            ENDCG
        }
    }
}
