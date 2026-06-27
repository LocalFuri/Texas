Shader "UI/AvatarRingSDF"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StrokeWidthPx ("Stroke Width (px)", Float) = 6
        _OuterRadiusPx ("Outer Radius (px)", Float) = 64
        _FillAmount ("Angular Fill", Range(0, 1)) = 1
        _RingLook ("Ring Look (0=Chrome, 1=Gold)", Float) = 0
        _ChromeColorTop ("Chrome Bright", Color) = (0.95, 0.95, 1.00, 1)
        _ChromeColorBot ("Chrome Dark",   Color) = (0.25, 0.25, 0.30, 1)
        _GoldColorTop ("Gold Bright", Color) = (1.00, 0.88, 0.35, 1)
        _GoldColorBot ("Gold Dark",   Color) = (0.45, 0.28, 0.05, 1)
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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _StrokeWidthPx;
            float _OuterRadiusPx;
            float2 _RectSize;
            float _FillAmount;
            float _RingLook;
            fixed4 _ChromeColorTop;
            fixed4 _ChromeColorBot;
            fixed4 _GoldColorTop;
            fixed4 _GoldColorBot;

            // Narrow specular peak on the ring (clockT in [0,1], clockwise from top).
            float SpecPeak(float clockT, float center, float width)
            {
                float d = abs(clockT - center);
                d = min(d, 1.0 - d);
                return smoothstep(width, 0.0, d);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Pixel offset from quad centre (true Euclidean circle in screen px).
                float2 pixelOffset = (IN.texcoord - 0.5) * max(_RectSize, float2(0.001, 0.001));
                float  dist        = length(pixelOffset);

                float outerR = max(_OuterRadiusPx, 0.001);
                float innerR = max(outerR - max(_StrokeWidthPx, 0.001), 0.001);

                // Annulus band SDF — avoids the 1px AA floor that flattened cardinals.
                float midR       = (innerR + outerR) * 0.5;
                float halfStroke = (outerR - innerR) * 0.5;
                float band       = abs(dist - midR) - halfStroke;

                float aa        = max(fwidth(band), 0.5);
                float ringAlpha = 1.0 - smoothstep(0.0, aa, band);

                if (_FillAmount < 0.999)
                {
                    float ang = atan2(pixelOffset.x, pixelOffset.y);
                    if (ang < 0.0)
                        ang += 6.2831853;
                    float clockT = ang / 6.2831853;
                    if (clockT < (1.0 - _FillAmount))
                        ringAlpha = 0.0;
                }

                // --- Procedural metallic tube (no drop shadow) ---
                float strokeW = max(outerR - innerR, 0.001);
                float bandT   = saturate((dist - innerR) / strokeW);
                // Rounded cross-section: crest of the band catches the most light.
                float tube    = sin(bandT * 3.14159265);
                tube          = tube * tube;

                float2 radialDir = dist > 0.001 ? pixelOffset / dist : float2(0.0, 1.0);
                float  angLight  = saturate(dot(radialDir, normalize(float2(-0.62, 0.78))) * 0.5 + 0.5);

                fixed3 metalBright = _RingLook < 0.5 ? _ChromeColorTop.rgb : _GoldColorTop.rgb;
                fixed3 metalDark   = _RingLook < 0.5 ? _ChromeColorBot.rgb : _GoldColorBot.rgb;
                float  metalT      = saturate(tube * 0.6 + angLight * 0.4);
                fixed3 ringRgb     = lerp(metalDark, metalBright, metalT);

                // Subtle specular glints on the raised surface only.
                float ang = atan2(pixelOffset.x, pixelOffset.y);
                if (ang < 0.0)
                    ang += 6.2831853;
                float clockT = ang / 6.2831853;
                float spec = 0.0;
                if (_RingLook < 0.5)
                {
                    spec = max(SpecPeak(clockT, 0.833, 0.038),
                           max(SpecPeak(clockT, 0.167, 0.032),
                               SpecPeak(clockT, 0.417, 0.032)));
                }
                else
                {
                    spec = max(SpecPeak(clockT, 0.833, 0.040),
                               SpecPeak(clockT, 0.333, 0.036));
                }
                spec *= saturate(tube * 1.15);
                fixed3 specTint = _RingLook < 0.5 ? fixed3(1.0, 1.0, 1.0) : fixed3(1.0, 0.97, 0.82);
                ringRgb = lerp(ringRgb, specTint, spec * (_RingLook < 0.5 ? 0.48 : 0.42));
                ringRgb *= IN.color.rgb;
                fixed4 color = fixed4(ringRgb, ringAlpha * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
