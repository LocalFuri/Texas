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
        //_GlowSpreadPx ("Glow Spread (px)", Float) = 28
        _GlowSpreadPx ("Glow Spread (px)", Float) = 60
        //_GlowStrength ("Glow Strength", Range(0, 4)) = 2.4
        _GlowStrength ("Glow Strength", Range(0, 4)) = 1.0
        //_GlowFalloff ("Glow Falloff", Range(0.8, 3)) = 1.15
        _GlowFalloff ("Glow Falloff", Range(0.8, 3)) = 2.0
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
                float  falloff     = max(_GlowFalloff, 0.8);

                float dist = sdRoundedBox(pixelOffset, halfPill, cornerR);
                float aa   = max(fwidth(dist), 0.5);

                float fillAlpha = 1.0 - smoothstep(-aa, aa, dist + borderW);

                float strokeAlpha = smoothstep(aa, 0.0, dist)
                                  * smoothstep(-borderW - aa, -borderW + aa, dist);

                // Tube: hot core toward inner edge of the stroke band.
                float  strokeT    = saturate(-dist / borderW);
                fixed3 tubeOuter  = _BorderColor.rgb * IN.color.rgb;
                fixed3 tubeCore   = lerp(_BorderColor.rgb, _HighlightColor.rgb, _HighlightStrength) * IN.color.rgb;
                fixed3 strokeRgb  = lerp(tubeOuter, tubeCore, pow(strokeT, 0.5));

                float yNorm = saturate((pixelOffset.y + halfPill.y) / max(halfPill.y * 2.0, 0.001));
                fixed3 fillRgb = lerp(_FillColorBot.rgb, _FillColorTop.rgb, yNorm);

                // Faint inner bleed — light seeping into the black fill near the tube.
                float innerBleed = (1.0 - smoothstep(-borderW - aa, -borderW * 2.5, dist))
                                 * fillAlpha
                                 * _HighlightStrength
                                 * 0.22;
                fixed3 bodyRgb = fillRgb * fillAlpha
                               + strokeRgb * strokeAlpha
                               + tubeCore * innerBleed;
                float bodyAlpha = saturate(max(fillAlpha, strokeAlpha) + innerBleed * 0.35);

                // Dual outer halo: tight bright rim + wide soft bloom.
                float distOut   = max(dist, 0.0);
                float tightGlow = pow(saturate(1.0 - distOut / max(spread * 0.42, 0.001)), falloff * 1.15);
                float wideGlow  = pow(saturate(1.0 - distOut / spread), falloff * 0.9);
                float glowMix   = tightGlow * 0.6 + wideGlow * 0.4;
                float glowAlpha = saturate(glowMix * _GlowStrength * (1.0 - saturate(bodyAlpha)));

                fixed3 glowRgb = lerp(tubeCore, tubeOuter, 0.25);
                fixed3 rgb     = lerp(glowRgb, bodyRgb, saturate(bodyAlpha));
                float  alpha   = saturate(lerp(glowAlpha, 1.0, saturate(bodyAlpha))) * IN.color.a;

                return ApplyClip(fixed4(rgb, alpha), IN.worldPosition);
            }
            ENDCG
        }
    }
}
