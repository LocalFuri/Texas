Shader "UI/HudPanelGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PanelSize ("Panel Size (px)", Vector) = (220, 70, 0, 0)
        _CornerRadiusPx ("Corner Radius (px)", Float) = 14
        _GlowSpreadPx ("Glow Spread (px)", Float) = 30
        _RimCorePx ("Rim Core (px)", Float) = 3
        _GlowIntensity ("Glow Intensity", Range(0, 1.5)) = 0
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowFalloff ("Glow Falloff", Range(0.8, 2.5)) = 1.1
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
            float2 _PanelSize;
            float _CornerRadiusPx;
            float _GlowSpreadPx;
            float _RimCorePx;
            float _GlowIntensity;
            fixed4 _GlowColor;
            float _GlowFalloff;

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
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 pixelOffset = (IN.texcoord - 0.5) * max(_RectSize, float2(0.001, 0.001));
                float2 halfPanel   = max(_PanelSize * 0.5, float2(0.001, 0.001));
                float  cornerR     = max(_CornerRadiusPx, 0.001);
                float  spread      = max(_GlowSpreadPx, 0.001);
                float  rimCore     = clamp(_RimCorePx, 0.5, spread - 0.5);

                float dist = sdRoundedBox(pixelOffset, halfPanel, cornerR);

                // Uniform outer rim: even-width bright core + smooth fade (isotropic SDF distance).
                float glow = 0.0;
                if (_GlowIntensity > 0.001)
                {
                    float aa      = max(fwidth(dist), 0.5);
                    float outside = smoothstep(0.0, aa, dist);
                    float d       = max(dist, 0.0);

                    float core = 1.0 - smoothstep(0.0, rimCore, d);
                    float halo = 1.0 - smoothstep(rimCore, spread, d);
                    float t    = max(core, halo * 0.55);
                    t = pow(max(t, 0.0), max(_GlowFalloff, 0.8));

                    glow = t * outside * _GlowIntensity;
                }

                fixed3 rgb = _GlowColor.rgb * IN.color.rgb;
                fixed4 color = fixed4(rgb, glow * _GlowColor.a * IN.color.a);

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
