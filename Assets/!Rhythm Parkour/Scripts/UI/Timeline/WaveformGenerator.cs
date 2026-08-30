using UnityEngine;
public static class WaveformGenerator
{
    public static float[] GenerateData(AudioClip clip, int columns = 2048)
    {
        if (clip == null || columns <= 4) return new float[1];
        int channels = clip.channels;
        int samples = clip.samples;
        if (samples <= 0) return new float[1];
        int clampedColumns = Mathf.Clamp(columns, 64, 8192);
        float[] all = new float[samples * channels];
        try { clip.GetData(all, 0); } catch { return new float[clampedColumns]; }
        float[] data = new float[clampedColumns];
        int samplesPerColumn = Mathf.Max(1, samples / clampedColumns);
        float max = 0.001f;
        for (int c = 0; c < clampedColumns; c++)
        {
            int start = c * samplesPerColumn * channels;
            int end = Mathf.Min(start + samplesPerColumn * channels, all.Length);
            float sum = 0f;
            for (int i = start; i < end; i += channels)
            {
                float s = 0f;
                for (int ch = 0; ch < channels; ch++) s += Mathf.Abs(all[i + ch]);
                s /= channels;
                if (s > sum) sum = s;
            }
            data[c] = sum;
            if (sum > max) max = sum;
        }
        for (int i = 0; i < data.Length; i++)
        {
            float v = data[i] / max;
            v = Mathf.Pow(v, 0.85f);
            data[i] = Mathf.Clamp01(v);
        }
        return data;
    }
    public static Texture2D GenerateTexture(float[] data, int texWidth, int texHeight, Color waveColor, Color backgroundColor, bool mirror = true)
    {
        texWidth = Mathf.Clamp(texWidth, 64, 4096);
        texHeight = Mathf.Clamp(texHeight, 16, 512);
        if (data == null || data.Length == 0) data = new float[1];
        Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[texWidth * texHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;
        for (int x = 0; x < texWidth; x++)
        {
            int dataIdx = Mathf.Clamp(Mathf.RoundToInt((float)x / texWidth * data.Length), 0, data.Length - 1);
            float amp = data[dataIdx];
            if (dataIdx > 0) amp = Mathf.Max(amp, data[dataIdx - 1] * 0.85f);
            if (dataIdx + 1 < data.Length) amp = Mathf.Max(amp, data[dataIdx + 1] * 0.85f);
            int h = Mathf.RoundToInt(amp * texHeight * 0.88f);
            h = Mathf.Max(h, amp > 0.02f ? 1 : 0);
            int center = texHeight / 2;
            int half = h / 2;
            for (int y = center - half; y <= center + half && y < texHeight; y++)
            {
                if (y < 0) continue;
                float dist = Mathf.Abs(y - center) / (float)Mathf.Max(1, half);
                float alphaMul = 1f - dist * 0.15f;
                Color c = waveColor;
                c.a *= alphaMul;
                Color bg = pixels[y * texWidth + x];
                pixels[y * texWidth + x] = Color.Lerp(bg, c, c.a);
            }
        }
        int mid = texHeight / 2;
        for (int x = 0; x < texWidth; x++)
        {
            Color baseCol = pixels[mid * texWidth + x];
            pixels[mid * texWidth + x] = Color.Lerp(baseCol, new Color(waveColor.r, waveColor.g, waveColor.b, 0.25f), 0.35f);
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }
    public static Texture2D GenerateTextureFromClip(AudioClip clip, int texWidth, int texHeight, Color waveColor, Color bgColor)
    {
        var data = GenerateData(clip, texWidth);
        return GenerateTexture(data, texWidth, texHeight, waveColor, bgColor);
    }
}
