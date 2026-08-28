using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class RhythmAutoGenerator
{
    // Улучшенная 1-кнопка: спектральный флюкс + адаптивный порог + квантизация
    public static void Generate(RhythmLevelData level, int seed = 0, float density = 0.65f, float threshold = 0.28f)
    {
        if (level == null || level.music == null) { Debug.LogWarning("[Auto] Нет music"); return; }
        var clip = level.music;
        int channels = clip.channels;
        int samples = clip.samples;
        float[] data = new float[samples * channels];
        clip.GetData(data, 0);

        float[] mono = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++) sum += Mathf.Abs(data[i * channels + c]);
            mono[i] = sum / channels;
        }

        float secPerBeat = 60f / Mathf.Max(1, level.bpm);
        int win = Mathf.RoundToInt(clip.frequency * 0.09f); // ~90ms окно — ловит кики/снейры
        if (win < 512) win = 512;
        int hop = win / 2;

        List<float> flux = new List<float>();
        List<float> times = new List<float>();
        float prevEnergy = 0;
        for (int i = 0; i + win < samples; i += hop)
        {
            float e = 0;
            for (int j = 0; j < win; j++) e += Mathf.Abs(mono[i + j]);
            e /= win;
            float curFlux = Mathf.Max(0, e - prevEnergy);
            flux.Add(curFlux);
            times.Add((float)i / clip.frequency);
            prevEnergy = e;
        }

        // Нормализация
        float maxF = 0; foreach (var f in flux) if (f > maxF) maxF = f;
        if (maxF < 0.0001f) maxF = 1f;
        for (int i = 0; i < flux.Count; i++) flux[i] /= maxF;

        System.Random rng = new System.Random(seed == 0 ? level.name.GetHashCode() : seed);
        level.events.Clear();

        // Адаптивный порог по локальному среднему
        int avgWin = 12;
        for (int i = avgWin; i < flux.Count - 1; i++)
        {
            float localSum = 0; for (int k = i - avgWin; k < i; k++) localSum += flux[k];
            float localAvg = localSum / avgWin;
            float localVar = 0; for (int k = i - avgWin; k < i; k++) localVar += (flux[k] - localAvg) * (flux[k] - localAvg);
            float std = Mathf.Sqrt(localVar / avgWin);
            float adaptThr = localAvg + std * 1.2f + threshold * 0.6f;

            bool isPeak = flux[i] > adaptThr && flux[i] > flux[i - 1] && flux[i] > flux[i + 1] && flux[i] > 0.08f;
            if (!isPeak) continue;
            if (rng.NextDouble() > density) continue;

            float time = times[i];
            float beat = level.TimeToBeat(time);
            beat = Mathf.Round(beat * 2f) / 2f; // 0.5 бита
            if (beat < 0) continue;
            time = level.BeatToTime(beat);
            if (level.events.Count > 0 && Mathf.Abs(level.events[level.events.Count - 1].beat - beat) < 0.45f) continue;

            int prefab = 0;
            if (level.obstaclePrefabs.Count > 1)
            {
                // Сильные пики — другой префаб
                float strength = flux[i];
                if (strength > 0.6f && level.obstaclePrefabs.Count > 2) prefab = 2;
                else if (strength > 0.35f && level.obstaclePrefabs.Count > 1) prefab = 1;
                else prefab = 0;
                // немного рандома чтобы не однообразно
                if (rng.NextDouble() < 0.12) prefab = rng.Next(0, level.obstaclePrefabs.Count);
            }
            var ev = ObstacleEvent.Create(beat, prefab, Vector3.zero);
            ev.time = time;
            level.events.Add(ev);
        }

        if (level.events.Count > 0 && level.events[0].beat > 3f)
        {
            float shift = Mathf.Floor(level.events[0].beat);
            for (int i = 0; i < level.events.Count; i++) { var e = level.events[i]; e.beat -= shift; e.time = level.BeatToTime(e.beat); level.events[i] = e; }
        }
        if (level.events.Count < 10)
        {
            for (int i = 0; i < 20; i++)
            {
                float beat = i * 2f;
                var ev = ObstacleEvent.Create(beat, i % Mathf.Max(1, level.obstaclePrefabs.Count), Vector3.zero);
                ev.time = level.BeatToTime(beat);
                if (level.events.Exists(x => Mathf.Abs(x.beat - beat) < 0.3f)) continue;
                level.events.Add(ev);
            }
        }

        level.SortByTime();
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Auto+] Сгенерировано {level.events.Count} нот (flux thr {threshold:0.00} dens {density:0.00})", level);
    }
}
