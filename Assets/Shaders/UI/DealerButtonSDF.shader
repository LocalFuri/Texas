Shader "UI/DealerButtonSDF"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RectSize ("Rect Size (px)", Vector) = (48, 48, 0, 0)
        _RadiusPx ("Radius (px)", Float) = 23
        _RimWidthPx ("Outer Rim Width (px)", Float) = 2.5
        _InnerRingWidthPx ("Inner Ring Width (px)", Float) = 1.5
        _InnerRingRadiusFrac ("Inner Ring Radius", Range(0.5, 0.95)) = 0.78
        _GoldColorTop ("Gold Bright", Color) = (1.00, 0.85, 0.20, 1)
        _GoldColorBot ("Gold Dark", Color) = (0.65, 0.55, 0.13, 1)
        _RimColor ("Rim / Inner Ring", Color) = (0.12, 0.08, 0.02, 1)
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.35
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
            float2 _RectSize;
            float _RadiusPx;
            float _RimWidthPx;
            float _InnerRingWidthPx;
            float _InnerRingRadiusFrac;
            fixed4 _GoldColorTop;
            fixed4 _GoldColorBot;
            fixed4 _RimColor;
            float _HighlightStrength;

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
                float2 pixelOffset = (IN.texcoord - 0.5) * max(_RectSize, float2(0.001, 0.001));
                float  dist        = length(pixelOffset);
                float  radius      = max(_RadiusPx, 1.0);
                float  aa          = max(fwidth(dist), 0.75);

                float discAlpha = 1.0 - smoothstep(radius - aa, radius + aa * 0.25, dist);
                if (discAlpha <= 0.001)
                    discard;

                float normY = saturate((pixelOffset.y + radius) / max(radius * 2.0, 0.001));
                float normR = saturate(1.0 - dist / radius);
                fixed3 gold = lerp(_GoldColorBot.rgb, _GoldColorTop.rgb, normY * 0.55 + normR * 0.25);

                float ang = atan2(pixelOffset.x, pixelOffset.y);
                if (ang < 0.0)
                    ang += 6.2831853;
                float clockT = ang / 6.2831853;
                float spec = max(SpecPeak(clockT, 0.83, 0.05), SpecPeak(clockT, 0.33, 0.04));
                spec *= normR * _HighlightStrength;
                gold = lerp(gold, fixed3(1.0, 0.98, 0.86), spec);

                float rimBand = radius - dist;
                float outerRim = smoothstep(_RimWidthPx + aa, _RimWidthPx * 0.25, rimBand);
                gold = lerp(gold, _RimColor.rgb, outerRim * 0.85);

                float innerR = radius * _InnerRingRadiusFrac;
                float innerBand = abs(dist - innerR) - _InnerRingWidthPx * 0.5;
                float innerRing = 1.0 - smoothstep(0.0, aa, innerBand);
                gold = lerp(gold, _RimColor.rgb, innerRing * 0.75);

                fixed4 color = fixed4(gold * IN.color.rgb, discAlpha * IN.color.a);

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
