using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;

namespace RKS.RhythmParkour.UI.Timeline
{
    public static class WaveformGenerator
    {
        public static float[] GenerateData(AudioClip clip, int columns = 8192)
        {
            if (clip == null || columns <= 4) return new float[1];
            int channels = Mathf.Max(1, clip.channels);
            int samples = clip.samples;
            if (samples <= 0) return new float[1];
            int clampedColumns = Mathf.Clamp(columns, 512, 16384);
            float[] all = null;
            try { all = new float[samples * channels]; clip.GetData(all, 0); }
            catch { return new float[clampedColumns]; }

            float[] data = new float[clampedColumns];
            double samplesPerColumnD = (double)samples / clampedColumns;
            float globalMax = 0.001f;
            for (int c = 0; c < clampedColumns; c++)
            {
                int startSample = (int)(c * samplesPerColumnD);
                int endSample = (int)((c + 1) * samplesPerColumnD);
                if (endSample <= startSample) endSample = startSample + 1;
                if (endSample > samples) endSample = samples;
                int startIdx = startSample * channels;
                int endIdx = endSample * channels;
                if (endIdx > all.Length) endIdx = all.Length;
                float peak = 0f; double sumSq = 0; int cnt = 0;
                for (int i = startIdx; i < endIdx; i += channels)
                {
                    float mono = 0f;
                    for (int ch = 0; ch < channels; ch++) mono += Mathf.Abs(all[i + ch]);
                    mono /= channels;
                    if (mono > peak) peak = mono;
                    sumSq += mono * mono; cnt++;
                }
                float rms = cnt > 0 ? Mathf.Sqrt((float)(sumSq / cnt)) : 0f;
                float combined = peak * 0.72f + rms * 0.38f;
                data[c] = combined;
                if (combined > globalMax) globalMax = combined;
            }
            if (globalMax < 0.001f) globalMax = 0.001f;
            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i] / globalMax;
                v = Mathf.Pow(Mathf.Clamp01(v), 0.82f);
                v = v / (1f + v * 0.15f) * 1.15f;
                data[i] = Mathf.Clamp01(v);
            }
            return data;
        }

        public static Texture2D GenerateTexture(float[] data, int texWidth, int texHeight, Color waveColor, Color backgroundColor, bool mirror = true)
        {
            texWidth = Mathf.Clamp(texWidth, 512, 16384);
            texHeight = Mathf.Clamp(texHeight, 32, 512);
            if (data == null || data.Length == 0) data = new float[Mathf.Min(texWidth, 64)];

            Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false, false);
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 4;
            tex.mipMapBias = -0.3f;

            Color bg = backgroundColor;
            Color[] pixels = new Color[texWidth * texHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            int center = texHeight / 2;
            float[] smooth = new float[texWidth];
            for (int x = 0; x < texWidth; x++)
            {
                float fIdx = (float)x / texWidth * data.Length;
                int i0 = Mathf.Clamp(Mathf.FloorToInt(fIdx), 0, data.Length - 1);
                int i1 = Mathf.Clamp(i0 + 1, 0, data.Length - 1);
                float t = fIdx - Mathf.Floor(fIdx);
                float amp = Mathf.Lerp(data[i0], data[i1], t);
                if (i0 > 0) amp = Mathf.Max(amp, data[i0 - 1] * 0.75f);
                if (i1 < data.Length - 1) amp = Mathf.Max(amp, data[i1] * 0.75f);
                smooth[x] = Mathf.Clamp01(amp);
            }
            float[] blurred = new float[texWidth];
            for (int x = 0; x < texWidth; x++)
            {
                float v = smooth[x] * 0.6f;
                if (x > 0) v += smooth[x - 1] * 0.2f;
                if (x + 1 < texWidth) v += smooth[x + 1] * 0.2f;
                blurred[x] = Mathf.Clamp01(v);
            }

            Color waveInner = Color.Lerp(waveColor, Color.white, 0.22f);
            waveInner.a = waveColor.a;

            for (int x = 0; x < texWidth; x++)
            {
                float amp = blurred[x];
                if (amp < 0.001f) continue;
                float hFloat = amp * (texHeight * 0.86f);
                if (hFloat < 1f && amp > 0.015f) hFloat = 1f;
                float half = hFloat * 0.5f;
                int yStart = Mathf.Clamp(Mathf.FloorToInt(center - half - 1f), 0, texHeight - 1);
                int yEnd = Mathf.Clamp(Mathf.CeilToInt(center + half + 1f), 0, texHeight - 1);
                for (int y = yStart; y <= yEnd; y++)
                {
                    float dist = Mathf.Abs(y - center);
                    float coverage;
                    if (dist <= half - 0.5f) coverage = 1f;
                    else if (dist <= half + 0.5f) { coverage = Mathf.Clamp01((half + 0.5f - dist)); coverage = coverage * coverage * (3f - 2f * coverage); }
                    else continue;
                    if (coverage <= 0.001f) continue;
                    float vDist = half > 0.001f ? dist / half : 0f;
                    float grad = 1f - vDist * 0.18f;
                    Color src = Color.Lerp(waveColor, waveInner, 1f - vDist);
                    src.a *= Mathf.Clamp01(grad) * coverage;
                    if (vDist > 0.85f) src.a *= 0.85f;
                    int idx = y * texWidth + x;
                    Color dst = pixels[idx];
                    pixels[idx] = Color.Lerp(dst, src, src.a);
                }
            }
            int mid = texHeight / 2;
            for (int x = 0; x < texWidth; x++)
            {
                int idx = mid * texWidth + x;
                Color baseCol = pixels[idx];
                Color centerCol = new Color(waveColor.r, waveColor.g, waveColor.b, 0.18f);
                pixels[idx] = Color.Lerp(baseCol, Color.Lerp(baseCol, centerCol, 0.5f), 0.45f);
                if (mid > 0) { int idx2 = (mid - 1) * texWidth + x; pixels[idx2] = Color.Lerp(pixels[idx2], centerCol, 0.12f); }
                if (mid + 1 < texHeight) { int idx3 = (mid + 1) * texWidth + x; pixels[idx3] = Color.Lerp(pixels[idx3], centerCol, 0.12f); }
            }
            tex.SetPixels(pixels);
            tex.Apply(true, false);
            return tex;
        }

        public static Texture2D GenerateTextureFromClip(AudioClip clip, int texWidth, int texHeight, Color waveColor, Color bgColor)
        {
            var data = GenerateData(clip, texWidth);
            return GenerateTexture(data, texWidth, texHeight, waveColor, bgColor);
        }
    }
}
