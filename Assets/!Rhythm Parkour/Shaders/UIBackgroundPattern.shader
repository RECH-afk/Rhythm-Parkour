Shader "UI/UIBackgroundPattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_CLIP_RECT)] _UseUIRectClip ("Use Rectangle Clip", Float) = 0
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Space(10)]
        _Speed ("Master Anim Speed", Float) = 1
        [HideInInspector] _Phase ("Phase Offset", Float) = 0

        [Space(10)]
        [Header(Base Image and Video)]
        _BaseTex ("Background Photo Video", 2D) = "white" {}
        _BaseColor ("Base Tint", Color) = (1,1,1,1)
        _BaseTiling ("Base Tiling", Vector) = (1,1,0,0)
        
        [Toggle] _LinkBaseAndGradient ("Link Base and Gradient crossfade", Float) = 0
        _BaseStrength ("Base Blend Strength", Range(0, 1)) = 0.5

        [Space(10)]
        [Header(Gradient)]
        _GradientColorA ("Color A", Color) = (0.10, 0.20, 0.55, 1)
        _GradientColorB ("Color B", Color) = (0.30, 0.55, 0.95, 1)
        _GradientAngle ("Angle (deg)", Float) = 90
        _GradientOffset ("Offset", Range(-1, 1)) = 0
        _GradientSoftness ("Softness", Range(0.01, 1)) = 1
        _GradientAngleSpeed ("Angle Speed (deg/s)", Float) = 10
        _GradientOffsetAmp ("Offset Pulse Amp", Range(0, 1)) = 0.1
        _GradientOffsetSpeed ("Offset Pulse Speed", Float) = 0.6

        [Space(10)]
        [Header(Moving Glow)]
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 0.18)
        _GlowSize ("Glow Size", Range(0.05, 1.5)) = 0.8
        _GlowMoveAmp ("Glow Move Amp", Range(0, 0.5)) = 0.3
        _GlowSpeed ("Glow Move Speed", Float) = 0.3

        [Space(10)]
        [Header(Pattern From Texture)]
        [NoScaleOffset] _PatternTex ("Pattern Image", 2D) = "white" {}
        _PatternColor ("Pattern Tint", Color) = (0.55, 0.75, 1.0, 0.28)
        _PatternTiling ("Tiling (XY)", Vector) = (6, 6, 0, 0)
        _PatternSize ("Icon Size in Cell", Range(0.05, 1)) = 0.8
        _BrickOffset ("Row Offset (0.5 = stagger)", Range(0, 1)) = 0.5
        _AltBrightness ("Alt Cell Brightness", Range(0.3, 1)) = 1
        [Toggle] _AltFlip ("Alt Cells Rotate 180", Float) = 0
        _PatternScrollSpeed ("Scroll Speed (cells/s)", Vector) = (0.25, 0.12, 0, 0)
        _PatternRotateSpeed ("Rotate Speed (deg/s)", Float) = 0
        _PatternPulseAmp ("Pulse Amp", Range(0, 0.5)) = 0.05
        _PatternPulseSpeed ("Pulse Speed", Float) = 1.2

        [Space(10)]
        [Header(Vignette And Blur)]
        _Vignette ("Vignette", Range(0, 1)) = 0.15
        _BlurRadius ("Blur Radius UV", Range(0, 0.05)) = 0.003
        [Toggle] _BlurPattern ("Apply Blur To Pattern", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIPatternBackground"
            HLSLPROGRAM 
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

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
                float4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color; 
            
            float4 _TextureSampleAdd; 
            float4 _ClipRect;

            float _Speed;
            float _Phase;

            sampler2D _BaseTex;
            float4 _BaseColor;
            float4 _BaseTiling;
            float _LinkBaseAndGradient;
            float _BaseStrength;

            float4 _GradientColorA;
            float4 _GradientColorB;
            float  _GradientAngle;
            float  _GradientOffset;
            float  _GradientSoftness;
            float  _GradientAngleSpeed;
            float  _GradientOffsetAmp;
            float  _GradientOffsetSpeed;

            float4 _GlowColor;
            float  _GlowSize;
            float  _GlowMoveAmp;
            float  _GlowSpeed;

            sampler2D _PatternTex;
            float4 _PatternColor;
            float4 _PatternTiling;
            float  _PatternSize;
            float  _BrickOffset;
            float  _AltBrightness;
            float  _AltFlip;
            float4 _PatternScrollSpeed;
            float  _PatternRotateSpeed;
            float  _PatternPulseAmp;
            float  _PatternPulseSpeed;

            float _Vignette;
            float _BlurRadius;
            float _BlurPattern;

            static const float2 kPoisson[12] =
            {
                float2(-0.326, -0.406), float2(-0.840, -0.074),
                float2(-0.696,  0.457), float2(-0.203,  0.621),
                float2( 0.962, -0.195), float2( 0.473, -0.480),
                float2( 0.519,  0.767), float2( 0.185, -0.893),
                float2( 0.507,  0.064), float2( 0.896,  0.412),
                float2(-0.322, -0.933), float2(-0.792, -0.598)
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float4 GetBackground(float2 uv, float t)
            {
                float ang = radians(_GradientAngle + t * _GradientAngleSpeed);
                float2 dir = float2(cos(ang), sin(ang));
                float off = _GradientOffset + _GradientOffsetAmp * sin(t * _GradientOffsetSpeed);
                float g = dot(uv - 0.5, dir) + 0.5 + off;
                float gs = _GradientSoftness * 0.5;
                g = smoothstep(0.5 - gs, 0.5 + gs, g);

                float3 gradRGB = lerp(_GradientColorA.rgb, _GradientColorB.rgb, g);
                float gradA = lerp(_GradientColorA.a, _GradientColorB.a, g);

                float2 gp = 0.5 + float2(sin(t * _GlowSpeed), sin(t * _GlowSpeed * 0.7 + 1.7)) * _GlowMoveAmp;
                float glow = 1.0 - smoothstep(0.0, _GlowSize, length(uv - gp));
                gradRGB += _GlowColor.rgb * (glow * _GlowColor.a);

                float4 baseCol = tex2D(_BaseTex, uv * _BaseTiling.xy) * _BaseColor;

                float3 finalRGB;
                float finalA;

                if (_LinkBaseAndGradient > 0.5)
                {
                    finalRGB = lerp(gradRGB, baseCol.rgb, _BaseStrength);
                    finalA = lerp(gradA, baseCol.a, _BaseStrength);
                }
                else
                {
                    finalRGB = lerp(gradRGB, gradRGB * baseCol.rgb, _BaseStrength);
                    finalA = gradA * lerp(1.0, baseCol.a, _BaseStrength);
                }

                float vd = length(uv - 0.5) * 1.4142;
                finalRGB *= 1.0 - _Vignette * smoothstep(0.4, 1.0, vd);

                float4 col;
                col.rgb = finalRGB;
                col.a = finalA;
                return col;
            }

            float4 GetPattern(float2 uv, float t)
            {
                float2 puv = uv - 0.5;
                float ra = radians(t * _PatternRotateSpeed);
                float rs, rc;
                sincos(ra, rs, rc);
                puv = float2(puv.x * rc - puv.y * rs, puv.x * rs + puv.y * rc) + 0.5;

                float pulse = 1.0 + _PatternPulseAmp * sin(t * _PatternPulseSpeed);
                float2 p = puv * _PatternTiling.xy * pulse + t * _PatternScrollSpeed.xy;

                float2 cell = floor(p);
                float2 pb = float2(p.x + floor(p.y) * _BrickOffset, p.y);

                float2 tuv = (frac(pb) - 0.5) / _PatternSize + 0.5;
                float chk = fmod(cell.x + cell.y, 2.0);
                tuv = lerp(tuv, 1.0 - tuv, _AltFlip * chk);

                float2 ed = 0.5 - abs(tuv - 0.5);
                float inside = smoothstep(0.0, 0.02, min(ed.x, ed.y));

                float4 pat = tex2D(_PatternTex, tuv);
                pat.rgb *= _PatternColor.rgb * lerp(1.0, _AltBrightness, chk);
                pat.a *= _PatternColor.a * inside;

                float4 col;
                col.rgb = pat.rgb;
                col.a = pat.a;
                return col;
            }

            float4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y * _Speed + _Phase;
                float2 uv = IN.texcoord;

                float4 bgCol, patCol;

                if (_BlurRadius > 0.0)
                {
                    bgCol = 0;
                    patCol = 0;
                    
                    [loop]
                    for (int i = 0; i < 12; i++)
                    {
                        float2 offset = kPoisson[i] * _BlurRadius;
                        bgCol += GetBackground(uv + offset, t);
                        
                        if (_BlurPattern > 0.5)
                            patCol += GetPattern(uv + offset, t);
                    }
                    
                    bgCol /= 12.0;
                    
                    if (_BlurPattern > 0.5)
                        patCol /= 12.0;
                    else
                        patCol = GetPattern(uv, t);
                }
                else
                {
                    bgCol = GetBackground(uv, t);
                    patCol = GetPattern(uv, t);
                }

                float3 rgb = lerp(bgCol.rgb, patCol.rgb, patCol.a);
                float a = max(bgCol.a, patCol.a);

                float4 color = float4(rgb, a);

                color *= (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDHLSL 
        }
    }
}