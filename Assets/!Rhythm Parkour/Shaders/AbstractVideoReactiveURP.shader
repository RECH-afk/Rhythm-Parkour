Shader "Custom/AbstractVideoReactiveURP"
{
    Properties
    {
        [Header(VideoInput)]
        _VideoTex("Video Texture (from VideoPlayer)", 2D) = "black" {}
        _VideoInfluence("Video Influence", Range(0,1)) = 0.85
        [Enum(Direct,0,Palette4,1,DominantHueShift,2,Average,3)] _VideoColorMode("Video Color Mode", Float) = 1
        _VideoSaturation("Video Saturation Boost", Range(0,2)) = 1.2
        _VideoContrast("Video Contrast", Range(0.5,2)) = 1.0
        _VideoLumReactivity("Luminance Reactivity", Range(0,1)) = 0.4
        _VideoScaleReactivity("Scale Reactivity", Range(0,1)) = 0.25
        _VideoWarpReactivity("Warp Reactivity", Range(0,1)) = 0.5

        [Header(BaseColorsFallback)]
        _BaseColor("Background (Fallback)", Color) = (0.05, 0.05, 0.08, 1)
        _ColorA("Color A Fallback", Color) = (0.2, 0.6, 1, 1)
        _ColorB("Color B Fallback", Color) = (1, 0.2, 0.6, 1)
        _ColorC("Color C Fallback", Color) = (0.3, 1, 0.5, 1)
        _ColorD("Color D Fallback", Color) = (1, 0.9, 0.2, 1)

        [Header(PatternSettings)]
        [Enum(Stripes,0,Waves,1,Circles,2,DotsGrid,3,GridTech,4,Truchet,5,FlowNoise,6,ZigZag,7,CheckerMelt,8)] _Pattern("Pattern Type", Float) = 1
        _Scale("Scale (Density)", Range(0.1, 50)) = 10
        _Thickness("Line Thickness", Range(0.01, 1)) = 0.3
        _Smoothness("Edge Smoothness", Range(0.001, 0.5)) = 0.05
        _Contrast("Contrast", Range(0.1, 5)) = 1
        _Angle("Rotation Angle", Range(0, 360)) = 0

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
        _TriplanarBlend("Triplanar Sharpness", Range(0.1, 8)) = 2

        [Header(Extra)]
        _EmissionStrength("Emission Strength", Range(0, 5)) = 0.3
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

            TEXTURE2D(_VideoTex);
            SAMPLER(sampler_VideoTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _VideoTex_ST;
                float _VideoInfluence;
                float _VideoColorMode;
                float _VideoSaturation;
                float _VideoContrast;
                float _VideoLumReactivity;
                float _VideoScaleReactivity;
                float _VideoWarpReactivity;
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

            // Helpers
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
                f = f*f*(3.0-2.0*f);
                float a = hash21(i);
                float b = hash21(i+float2(1,0));
                float c = hash21(i+float2(0,1));
                float d = hash21(i+float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }
            float fbm(float2 p)
            {
                float v=0; float a=0.5;
                for(int i=0;i<4;i++){ v+=a*valueNoise(p); p*=2.0; a*=0.5; }
                return v;
            }
            float2 rotate2D(float2 p,float a){ float s=sin(a); float c=cos(a); return float2(c*p.x - s*p.y, s*p.x + c*p.y); }

            half luminance(half3 c){ return dot(c, half3(0.299,0.587,0.114)); }
            half3 saturationBoost(half3 c, half sat){ half lum=luminance(c); return lerp(half3(lum,lum,lum), c, sat); }
            half3 contrastBoost(half3 c, half con){ return (c - 0.5)*con + 0.5; }

            half3 rgb2hsv(half3 c)
            {
                half4 K = half4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            half3 hsv2rgb(half3 c)
            {
                half4 K = half4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }
            half3 hueShift(half3 c, half h){ half3 hsv=rgb2hsv(c); hsv.x=frac(hsv.x+h); return hsv2rgb(hsv); }

            // Patterns
            float pattern_stripes(float2 uv, float th, float sm){ float v=frac(uv.x); float d=abs(v-0.5)*2.0; return 1.0 - smoothstep(th-sm, th+sm, d); }
            float pattern_waves(float2 uv, float fr, float am, float th, float sm){ float w=sin(uv.x*fr)*am; float v=frac(uv.y+w); float d=abs(v-0.5)*2.0; return 1.0 - smoothstep(th-sm, th+sm, d); }
            float pattern_circles(float2 uv, float th, float sm){ float2 gv=frac(uv)-0.5; float d=length(gv)*2.0; float r=frac(d*1.5); float dist=abs(r-0.5)*2.0; return 1.0 - smoothstep(th-sm, th+sm, dist); }
            float pattern_dots(float2 uv, float th, float sm){ float2 gv=frac(uv)-0.5; float d=length(gv); float sz=(1.0-th)*0.5; return 1.0 - smoothstep(sz-sm, sz+sm, d); }
            float pattern_grid(float2 uv, float th, float sm){ float2 gv=frac(uv); float2 d=abs(gv-0.5)*2.0; float lx=1.0-smoothstep(th-sm, th+sm, 1.0-d.x); float ly=1.0-smoothstep(th-sm, th+sm, 1.0-d.y); return max(lx,ly); }
            float pattern_truchet(float2 uv){ float2 id=floor(uv); float2 gv=frac(uv)-0.5; float h=hash21(id); if(h>0.5) gv.x*=-1; float d1=abs(length(gv-0.5)-0.5); float d2=abs(length(gv+0.5)-0.5); float d=min(d1,d2); return 1.0 - smoothstep(0.04,0.08,d); }
            float pattern_flow(float2 uv){ float n=fbm(uv*0.7); float lines=frac(n*6.0); float d=abs(lines-0.5)*2.0; return d; }
            float pattern_zigzag(float2 uv, float th, float sm){ float tri=abs(frac(uv.x*0.5)-0.5)*4.0-1.0; float w=tri*0.5; float v=frac(uv.y+w); float d=abs(v-0.5)*2.0; return 1.0 - smoothstep(th-sm, th+sm, d); }
            float pattern_checkerMelt(float2 uv, float tm){ float warp=sin(uv.y*3.0+tm*2.0)*0.2+sin(uv.x*2.0)*0.2; uv.x+=warp; float2 c=floor(uv); float chk=frac(c.x+c.y)>0.5?1:0; float2 gv=frac(uv); float edge=smoothstep(0.0,0.05,gv.x)*smoothstep(0.0,0.05,gv.y)*smoothstep(0.0,0.05,1.0-gv.x)*smoothstep(0.0,0.05,1.0-gv.y); return chk*edge; }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs vpi=GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs npi=GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS=vpi.positionCS;
                OUT.positionWS=vpi.positionWS;
                OUT.normalWS=npi.normalWS;
                OUT.positionOS=IN.positionOS.xyz;
                OUT.uv=TRANSFORM_TEX(IN.uv, _VideoTex);
                // keep original uv for pattern if needed? use IN.uv for pattern, video uv separate
                OUT.uv=IN.uv;
                OUT.fogFactor=ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // --- Pattern UV ---
                float2 uv;
                int mode=(int)_UVMode;
                if(mode==0) uv=IN.uv;
                else if(mode==1) uv=IN.positionWS.xz;
                else if(mode==2) uv=IN.positionWS.xy;
                else if(mode==3) uv=IN.positionOS.xz;
                else uv=IN.positionWS.xz;

                float t=_Time.y*_Speed;
                float rad=radians(_Angle);
                // base scale before video reactivity
                float baseScale=_Scale;
                float baseThickness=_Thickness;
                float baseWarp=_WarpAmount;

                // --- Sample Video for reactivity and palette ---
                float2 videoUV = TRANSFORM_TEX(IN.uv, _VideoTex);
                // also sample in world space if needed? use IN.uv for video sampling (most intuitive for quad)
                half3 videoSample = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, videoUV).rgb;
                half videoLum = luminance(videoSample);
                // palette samples - 4 key points + average
                half3 vidA = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, float2(0.2,0.3)).rgb;
                half3 vidB = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, float2(0.8,0.3)).rgb;
                half3 vidC = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, float2(0.5,0.7)).rgb;
                half3 vidD = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, float2(0.8,0.8)).rgb;
                half3 vidCenter = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, float2(0.5,0.5)).rgb;
                // average
                half3 vidAvg = (vidA+vidB+vidC+vidD+vidCenter)/5.0;

                // adjust video colors with saturation/contrast controls
                vidA = contrastBoost(saturationBoost(vidA, _VideoSaturation), _VideoContrast);
                vidB = contrastBoost(saturationBoost(vidB, _VideoSaturation), _VideoContrast);
                vidC = contrastBoost(saturationBoost(vidC, _VideoSaturation), _VideoContrast);
                vidD = contrastBoost(saturationBoost(vidD, _VideoSaturation), _VideoContrast);
                vidCenter = contrastBoost(saturationBoost(vidCenter, _VideoSaturation), _VideoContrast);
                vidAvg = contrastBoost(saturationBoost(vidAvg, _VideoSaturation), _VideoContrast);
                videoSample = contrastBoost(saturationBoost(videoSample, _VideoSaturation), _VideoContrast);

                // If video is black (no texture), detect and reduce influence
                half videoPresent = step(0.02, dot(vidCenter, half3(1,1,1)));
                half effInfluence = _VideoInfluence * videoPresent;

                // --- Video reactivity on pattern params ---
                // luminance drives scale/thickness/warp
                float lumFactor = 1.0 + (videoLum - 0.5) * 2.0 * _VideoLumReactivity;
                float localScale = baseScale * lerp(1.0, lumFactor, _VideoScaleReactivity);
                float localThickness = saturate(baseThickness * lerp(1.0, lumFactor, _VideoLumReactivity));
                float localWarp = baseWarp * lerp(1.0, 1.0 + videoLum * 1.5, _VideoWarpReactivity);

                uv *= localScale;
                uv = rotate2D(uv, rad);
                float2 move=float2(_SpeedX,_SpeedY)*t;
                // add video-driven movement: hue shift moves with video luminance
                move += float2(videoLum*0.2, videoLum*0.1) * _VideoWarpReactivity;
                uv += move;

                if(localWarp > 0.001)
                {
                    float2 warpUV = uv * 0.15 * _WarpScale + float2(t*_WarpSpeed*0.3, t*_WarpSpeed*0.2);
                    float n1=fbm(warpUV)*2.0-1.0;
                    float n2=fbm(warpUV+5.2)*2.0-1.0;
                    uv += float2(n1,n2)*localWarp*5.0;
                }

                // --- Pattern evaluation ---
                float pat=0; float p=_Pattern;
                if(p<0.5) pat=pattern_stripes(uv, localThickness, _Smoothness);
                else if(p<1.5) pat=pattern_waves(uv, _WaveFreq, _WaveAmp, localThickness, _Smoothness);
                else if(p<2.5) pat=pattern_circles(uv, localThickness, _Smoothness);
                else if(p<3.5) pat=pattern_dots(uv, localThickness, _Smoothness);
                else if(p<4.5) pat=pattern_grid(uv, localThickness, _Smoothness);
                else if(p<5.5) pat=pattern_truchet(uv*0.5);
                else if(p<6.5){ float f=pattern_flow(uv*0.15); float d=abs(f-0.5)*2.0; pat=1.0 - smoothstep(localThickness-_Smoothness, localThickness+_Smoothness, d); }
                else if(p<7.5) pat=pattern_zigzag(uv, localThickness, _Smoothness);
                else pat=pattern_checkerMelt(uv*0.5, t);

                pat=saturate(pat);
                pat=pow(pat, _Contrast);

                // --- Build palette ---
                half3 fallbackA=_ColorA.rgb, fallbackB=_ColorB.rgb, fallbackC=_ColorC.rgb, fallbackD=_ColorD.rgb;
                half3 palA, palB, palC, palD;

                int vmode=(int)_VideoColorMode;
                if(vmode==0) // Direct - use current video pixel and its hue shifts
                {
                    half3 baseCol = videoSample;
                    // if video is dark, fallback to average
                    half useAvg = step(dot(baseCol,half3(1,1,1)), 0.05);
                    baseCol = lerp(baseCol, vidAvg, useAvg);
                    palA = baseCol;
                    palB = hueShift(baseCol, 0.08);
                    palC = hueShift(baseCol, -0.08);
                    palD = hueShift(baseCol, 0.5);
                    // boost
                    palB = lerp(fallbackB, palB, effInfluence);
                    palA = lerp(fallbackA, palA, effInfluence);
                }
                else if(vmode==1) // Palette4 - 4 sampled points
                {
                    palA = lerp(fallbackA, vidA, effInfluence);
                    palB = lerp(fallbackB, vidB, effInfluence);
                    palC = lerp(fallbackC, vidC, effInfluence);
                    palD = lerp(fallbackD, vidD, effInfluence);
                }
                else if(vmode==2) // Dominant + HueShift
                {
                    half3 dom = vidAvg;
                    // if average is gray, use center
                    half gray = step(length(dom - half3(luminance(dom),luminance(dom),luminance(dom))), 0.05);
                    dom = lerp(dom, vidCenter, gray);
                    palA = lerp(fallbackA, dom, effInfluence);
                    palB = lerp(fallbackB, hueShift(dom, 0.12), effInfluence);
                    palC = lerp(fallbackC, hueShift(dom, -0.12), effInfluence);
                    palD = lerp(fallbackD, hueShift(dom, 0.5), effInfluence);
                }
                else // Average - all colors are average with slight variation
                {
                    palA = lerp(fallbackA, vidAvg, effInfluence);
                    palB = lerp(fallbackB, hueShift(vidAvg, 0.05), effInfluence);
                    palC = lerp(fallbackC, hueShift(vidAvg, -0.05), effInfluence);
                    palD = palA;
                }

                // subtle noise for palette variation driven by video
                float colorNoise = valueNoise(uv*0.3 + t*0.5 + videoLum*2.0);
                half3 col = _BaseColor.rgb;
                // if video influence high, tint background with video average as well
                half3 bgVideo = lerp(_BaseColor.rgb, vidAvg * 0.5 + _BaseColor.rgb*0.5, effInfluence * 0.4);
                col = bgVideo;

                float paletteLerp = frac(pat*1.3 + colorNoise*0.4 + videoLum*0.2);
                half3 paletteCol;
                if(paletteLerp < 0.33) paletteCol = lerp(palA, palB, paletteLerp/0.33);
                else if(paletteLerp < 0.66) paletteCol = lerp(palB, palC, (paletteLerp-0.33)/0.33);
                else paletteCol = lerp(palC, palD, (paletteLerp-0.66)/0.34);

                col = lerp(col, paletteCol, pat);

                // Triplanar subtle
                if(mode==4)
                {
                    float triNoise=valueNoise(IN.positionWS.xz*0.5);
                    col *= lerp(1.0, 0.92+triNoise*0.16, 0.3);
                }

                half3 finalCol = col + col * _EmissionStrength * pat;
                // add video additive glow based on luminance
                finalCol += videoSample * effInfluence * 0.15 * pat;
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
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings ShadowPassVertex(Attributes IN){ Varyings OUT; UNITY_SETUP_INSTANCE_ID(IN); UNITY_TRANSFER_INSTANCE_ID(IN, OUT); OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half4 ShadowPassFragment(Varyings IN):SV_TARGET{ return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "AbstractVideoReactiveShaderGUI"
}
