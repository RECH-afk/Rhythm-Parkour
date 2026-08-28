Shader "Custom/AbstractPatternURP"
{
    Properties
    {
        [Header(BaseColors)]
        _BaseColor("Background Color", Color) = (0.05, 0.05, 0.08, 1)
        _ColorA("Color A", Color) = (0.2, 0.6, 1, 1)
        _ColorB("Color B", Color) = (1, 0.2, 0.6, 1)
        _ColorC("Color C", Color) = (0.3, 1, 0.5, 1)
        _ColorD("Color D (Extra)", Color) = (1, 0.9, 0.2, 1)

        [Header(PatternSettings)]
        [Enum(Stripes,0,Waves,1,Circles,2,DotsGrid,3,GridTech,4,Truchet,5,FlowNoise,6,ZigZag,7,CheckerMelt,8)] _Pattern("Pattern Type", Float) = 1
        _Scale("Scale (Density)", Range(0.1, 50)) = 10
        _Thickness("Line Thickness", Range(0.01, 1)) = 0.3
        _Smoothness("Edge Smoothness", Range(0.001, 0.5)) = 0.05
        _Contrast("Contrast / Sharpness", Range(0.1, 5)) = 1
        _Angle("Rotation Angle (Degrees)", Range(0, 360)) = 0

        [Header(Animation)]
        _Speed("Global Speed", Range(-5, 5)) = 1
        _SpeedX("Move X", Range(-5,5)) = 0
        _SpeedY("Move Y", Range(-5,5)) = 0.5
        _WaveFreq("Wave Frequency", Range(0, 20)) = 4
        _WaveAmp("Wave Amplitude", Range(0, 2)) = 0.5

        [Header(DistortionWarp)]
        _WarpAmount("Warp Amount", Range(0, 1)) = 0.15
        _WarpScale("Warp Scale", Range(0.1, 20)) = 3
        _WarpSpeed("Warp Speed", Range(0, 5)) = 0.5

        [Header(Coordinates)]
        [Enum(UV,0,WorldXZ,1,WorldXY,2,ObjectXZ,3,TriplanarWorld,4)] _UVMode("UV Mode (for any mesh)", Float) = 0
        _TriplanarBlend("Triplanar Blend Sharpness", Range(0.1, 8)) = 2

        [Header(Extra)]
        _EmissionStrength("Emission Strength", Range(0, 5)) = 0
        _Alpha("Alpha", Range(0,1)) = 1

        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }
        LOD 100

        Blend [_SrcBlend][_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ColorA;
                half4 _ColorB;
                half4 _ColorC;
                half4 _ColorD;
                float _Pattern;
                float _Scale;
                float _Thickness;
                float _Smoothness;
                float _Contrast;
                float _Angle;
                float _Speed;
                float _SpeedX;
                float _SpeedY;
                float _WaveFreq;
                float _WaveAmp;
                float _WarpAmount;
                float _WarpScale;
                float _WarpSpeed;
                float _UVMode;
                float _TriplanarBlend;
                float _EmissionStrength;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0;
                float a = 0.5;
                for(int i=0; i<4; i++)
                {
                    v += a * valueNoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            float2 rotate2D(float2 p, float a)
            {
                float s = sin(a);
                float c = cos(a);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float pattern_stripes(float2 uv, float thickness, float smoothness)
            {
                float v = frac(uv.x);
                float d = abs(v - 0.5) * 2.0;
                return 1.0 - smoothstep(thickness - smoothness, thickness + smoothness, d);
            }

            float pattern_waves(float2 uv, float freq, float amp, float thickness, float smoothness)
            {
                float wave = sin(uv.x * freq) * amp;
                float v = frac(uv.y + wave);
                float d = abs(v - 0.5) * 2.0;
                return 1.0 - smoothstep(thickness - smoothness, thickness + smoothness, d);
            }

            float pattern_circles(float2 uv, float thickness, float smoothness)
            {
                float2 gv = frac(uv) - 0.5;
                float d = length(gv) * 2.0;
                float r = frac(d * 1.5);
                float dist = abs(r - 0.5) * 2.0;
                return 1.0 - smoothstep(thickness - smoothness, thickness + smoothness, dist);
            }

            float pattern_dots(float2 uv, float thickness, float smoothness)
            {
                float2 gv = frac(uv) - 0.5;
                float d = length(gv);
                float size = (1.0 - thickness) * 0.5;
                return 1.0 - smoothstep(size - smoothness, size + smoothness, d);
            }

            float pattern_grid(float2 uv, float thickness, float smoothness)
            {
                float2 gv = frac(uv);
                float2 d = abs(gv - 0.5) * 2.0;
                float lineX = 1.0 - smoothstep(thickness - smoothness, thickness + smoothness, 1.0 - d.x);
                float lineY = 1.0 - smoothstep(thickness - smoothness, thickness + smoothness, 1.0 - d.y);
                return max(lineX, lineY);
            }

            float pattern_truchet(float2 uv)
            {
                float2 id = floor(uv);
                float2 gv = frac(uv) - 0.5;
                float h = hash21(id);
                if (h > 0.5) gv.x *= -1;
                float d1 = abs(length(gv - 0.5) - 0.5);
                float d2 = abs(length(gv + 0.5) - 0.5);
                float d = min(d1, d2);
                return 1.0 - smoothstep(0.04, 0.08, d);
            }

            float pattern_flow(float2 uv)
            {
                float n = fbm(uv * 0.7);
                float lines = frac(n * 6.0);
                float d = abs(lines - 0.5) * 2.0;
                return d;
            }

            float pattern_zigzag(float2 uv, float thickness, float smoothness)
            {
                float tri = abs(frac(uv.x * 0.5) - 0.5) * 4.0 - 1.0;
                float wave = tri * 0.5;
                float v = frac(uv.y + wave);
                float d = abs(v - 0.5) * 2.0;
                return 1.0 - smoothstep(thickness - smoothness, thickness + smoothness, d);
            }

            float pattern_checkerMelt(float2 uv, float time)
            {
                float warp = sin(uv.y * 3.0 + time * 2.0) * 0.2 + sin(uv.x * 2.0) * 0.2;
                uv.x += warp;
                float2 c = floor(uv);
                float checker = frac(c.x + c.y) > 0.5 ? 1 : 0;
                float2 gv = frac(uv);
                float edge = smoothstep(0.0, 0.05, gv.x) * smoothstep(0.0, 0.05, gv.y) * smoothstep(0.0, 0.05, 1.0-gv.x) * smoothstep(0.0,0.05,1.0-gv.y);
                return checker * edge;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs npi = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = npi.normalWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv;
                int mode = (int)_UVMode;
                if (mode == 0) uv = IN.uv;
                else if (mode == 1) uv = IN.positionWS.xz;
                else if (mode == 2) uv = IN.positionWS.xy;
                else if (mode == 3) uv = IN.positionOS.xz;
                else uv = IN.positionWS.xz;

                uv *= _Scale;
                float rad = radians(_Angle);
                uv = rotate2D(uv, rad);
                float t = _Time.y * _Speed;
                float2 move = float2(_SpeedX, _SpeedY) * t;
                uv += move;

                if (_WarpAmount > 0.001)
                {
                    float2 warpUV = uv * 0.15 * _WarpScale + float2(t * _WarpSpeed * 0.3, t * _WarpSpeed * 0.2);
                    float n1 = fbm(warpUV) * 2.0 - 1.0;
                    float n2 = fbm(warpUV + 5.2) * 2.0 - 1.0;
                    uv += float2(n1, n2) * _WarpAmount * 5.0;
                }

                float pat = 0;
                float p = _Pattern;
                if (p < 0.5) pat = pattern_stripes(uv, _Thickness, _Smoothness);
                else if (p < 1.5) pat = pattern_waves(uv, _WaveFreq, _WaveAmp, _Thickness, _Smoothness);
                else if (p < 2.5) pat = pattern_circles(uv, _Thickness, _Smoothness);
                else if (p < 3.5) pat = pattern_dots(uv, _Thickness, _Smoothness);
                else if (p < 4.5) pat = pattern_grid(uv, _Thickness, _Smoothness);
                else if (p < 5.5) pat = pattern_truchet(uv * 0.5);
                else if (p < 6.5)
                {
                    float f = pattern_flow(uv * 0.15);
                    float d = abs(f - 0.5) * 2.0;
                    pat = 1.0 - smoothstep(_Thickness - _Smoothness, _Thickness + _Smoothness, d);
                }
                else if (p < 7.5) pat = pattern_zigzag(uv, _Thickness, _Smoothness);
                else pat = pattern_checkerMelt(uv * 0.5, t);

                pat = saturate(pat);
                pat = pow(pat, _Contrast);

                half3 col = _BaseColor.rgb;
                float colorNoise = valueNoise(uv * 0.3 + t * 0.5);
                half3 colA = _ColorA.rgb;
                half3 colB = _ColorB.rgb;
                half3 colC = _ColorC.rgb;
                half3 colD = _ColorD.rgb;
                float paletteLerp = frac(pat * 1.3 + colorNoise * 0.4);
                half3 paletteCol;
                if (paletteLerp < 0.33) paletteCol = lerp(colA, colB, paletteLerp / 0.33);
                else if (paletteLerp < 0.66) paletteCol = lerp(colB, colC, (paletteLerp - 0.33) / 0.33);
                else paletteCol = lerp(colC, colD, (paletteLerp - 0.66) / 0.34);
                col = lerp(col, paletteCol, pat);

                if (mode == 4)
                {
                    float triNoise = valueNoise(IN.positionWS.xz * 0.5);
                    col *= lerp(1.0, 0.92 + triNoise * 0.16, 0.3);
                }

                half3 finalCol = col + col * _EmissionStrength * pat;
                finalCol = MixFog(finalCol, IN.fogFactor);
                return half4(finalCol, _Alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 ShadowPassFragment(Varyings IN) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "AbstractPatternShaderGUI"
}
