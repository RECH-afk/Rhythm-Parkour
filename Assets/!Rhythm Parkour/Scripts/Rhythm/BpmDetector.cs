using UnityEngine;

/// <summary>
/// Простой авто-детектор BPM по аудио.
/// Использует onset envelope + автокорреляцию. Достаточно для демо; для точности можно заменить на более сложный анализ.
/// </summary>
public static class BpmDetector
{
    public struct Result
    {
        public float bpm;
        public float confidence; // 0..1
        public float offset; // оценка оффсета первого бита (сек)
    }

    /// <summary>
    /// Определить BPM клипа. Диапазон 70-200 по умолчанию.
    /// </summary>
    public static Result Detect(AudioClip clip, float minBpm = 70f, float maxBpm = 200f)
    {
        var res = new Result { bpm = 128f, confidence = 0f, offset = 0f };
        if (clip == null) return res;

        int ch = clip.channels;
        int samples = clip.samples;
        int freq = clip.frequency;
        if (samples <= 0 || freq <= 0) return res;

        float[] data = new float[samples * ch];
        try { clip.GetData(data, 0); } catch { return res; }

        // mono
        float[] mono = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float s = 0f;
            for (int c = 0; c < ch; c++) s += data[i * ch + c];
            mono[i] = s / Mathf.Max(1, ch);
        }

        // envelope на 100 Гц
        int envRate = 100; // Гц
        int win = Mathf.Max(1, freq / envRate);
        int envLen = samples / win;
        if (envLen < 200) return res;
        float[] env = new float[envLen];
        for (int i = 0; i < envLen; i++)
        {
            float sum = 0f;
            int baseIdx = i * win;
            for (int j = 0; j < win && baseIdx + j < mono.Length; j++)
                sum += Mathf.Abs(mono[baseIdx + j]);
            env[i] = sum / win;
        }
        // нормализация
        float envMax = 0f;
        for (int i = 0; i < envLen; i++) if (env[i] > envMax) envMax = env[i];
        if (envMax < 1e-6f) return res;
        for (int i = 0; i < envLen; i++) env[i] /= envMax;

        // onset (flux)
        float[] flux = new float[envLen];
        flux[0] = 0f;
        for (int i = 1; i < envLen; i++)
        {
            float diff = env[i] - env[i - 1];
            // усиливаем транзиенты
            flux[i] = diff > 0 ? diff : 0f;
            // немного подавляем тихие части
            if (env[i] < 0.08f) flux[i] *= 0.5f;
        }
        // сгладим flux
        float[] fluxS = new float[envLen];
        for (int i = 1; i < envLen - 1; i++) fluxS[i] = (flux[i - 1] * 0.25f + flux[i] * 0.5f + flux[i + 1] * 0.25f);
        flux = fluxS;

        // автокорреляция в диапазоне BPM
        float secPerSample = 1f / envRate;
        int minLag = Mathf.RoundToInt(60f / maxBpm / secPerSample);
        int maxLag = Mathf.RoundToInt(60f / minBpm / secPerSample);
        minLag = Mathf.Clamp(minLag, 10, envLen / 2);
        maxLag = Mathf.Clamp(maxLag, minLag + 1, envLen / 2);

        float bestCorr = -1f;
        int bestLag = Mathf.RoundToInt(60f / 128f / secPerSample);
        float[] corr = new float[maxLag + 1];

        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0;
            double norm = 0;
            int cnt = 0;
            for (int i = 0; i + lag < envLen; i++)
            {
                sum += flux[i] * flux[i + lag];
                norm += flux[i] * flux[i] + flux[i + lag] * flux[i + lag];
                cnt++;
            }
            float c = cnt > 0 ? (float)(sum / (Mathf.Sqrt((float)norm) + 1e-6f) * 2f) : 0f;
            // взвешиваем к 120 bpm чуть ближе (приоритет средних темпов)
            float bpmAtLag = 60f / (lag * secPerSample);
            float tempoWeight = 1f - Mathf.Abs(bpmAtLag - 120f) * 0.0008f; // лёгкий бонус к средним
            c *= Mathf.Clamp01(tempoWeight);
            corr[lag] = c;
            if (c > bestCorr) { bestCorr = c; bestLag = lag; }
        }

        // уточняем пик параболой
        if (bestLag > minLag && bestLag < maxLag)
        {
            float c0 = corr[bestLag - 1], c1 = corr[bestLag], c2 = corr[bestLag + 1];
            float denom = (c0 - 2 * c1 + c2);
            if (Mathf.Abs(denom) > 1e-6f)
            {
                float shift = 0.5f * (c0 - c2) / denom;
                float refinedLag = bestLag + Mathf.Clamp(shift, -0.5f, 0.5f);
                bestLag = Mathf.RoundToInt(refinedLag);
                // float уже есть, но bpm пересчитаем
            }
        }

        float detectedBpm = 60f / (bestLag * secPerSample);
        // проверка на half/double — если пик на 2x тоже сильный, выбираем более правдоподобный
        int doubleLag = bestLag * 2;
        int halfLag = Mathf.RoundToInt(bestLag * 0.5f);
        if (doubleLag >= minLag && doubleLag <= maxLag && corr[doubleLag] > bestCorr * 0.88f)
        {
            // если double тоже сильный и ближе к 100-130 — возможно half-tempo, оставляем оригинал (более быстрый) если уверенность высокая
            // иначе ничего не делаем
        }
        if (halfLag >= minLag && halfLag <= maxLag && corr[halfLag] > bestCorr * 0.92f)
        {
            // half темп иногда даёт ложный пик на 2x BPM, проверяем энергию
            float halfBpm = 60f / (halfLag * secPerSample);
            if (halfBpm >= 90f && halfBpm <= 170f && corr[halfLag] > bestCorr * 0.97f)
            {
                detectedBpm = halfBpm;
                bestCorr = corr[halfLag];
                bestLag = halfLag;
            }
        }

        // оценка оффсета — ищем первый сильный onset perto 0
        float offset = 0f;
        float thresh = 0.18f;
        for (int i = 0; i < envLen; i++)
        {
            if (flux[i] > thresh)
            {
                offset = i * secPerSample;
                // квантуем к ближайшему биту detectedBpm
                // offset должен быть < period
                float period = 60f / detectedBpm;
                offset = offset % period;
                // если offset > period*0.7 — считаем что первый бит раньше
                if (offset > period * 0.5f) offset -= period;
                if (offset < 0) offset = 0;
                break;
            }
        }

        float conf = Mathf.Clamp01(bestCorr * 2.5f); // эмпирически
        res.bpm = Mathf.Clamp(detectedBpm, minBpm, maxBpm);
        res.confidence = conf;
        res.offset = offset;
        return res;
    }

    /// <summary>Быстро применить BPM к уровню (пересчитать времена нот чтобы карта стала быстрее/медленнее)</summary>
    public static void ApplyBpm(RhythmLevelData level, float newBpm, bool keepBeats = true)
    {
        if (level == null) return;
        newBpm = Mathf.Clamp(newBpm, 40f, 300f);
        if (Mathf.Abs(level.bpm - newBpm) < 0.01f) return;
        float oldBpm = level.bpm;
        level.bpm = newBpm;
        if (!keepBeats) return;
        // keep beats — пересчитываем spawnTime из spawnBeat
        for (int i = 0; i < level.events.Count; i++)
        {
            var ev = level.events[i];
            // beat — это spawnBeat, пересчитываем time
            ev.time = level.BeatToTime(ev.beat);
            level.events[i] = ev;
        }
        level.SortByTime();
        Debug.Log($"[BPM] {oldBpm:0.##} -> {newBpm:0.##} (keepBeats={keepBeats}, {level.events.Count} нот пересчитано)");
    }

    /// <summary>Пересчитать beats из текущих times (сохранить тайминги при смене BPM)</summary>
    public static void KeepTimes(RhythmLevelData level, float newBpm)
    {
        if (level == null) return;
        newBpm = Mathf.Clamp(newBpm, 40f, 300f);
        level.bpm = newBpm;
        for (int i = 0; i < level.events.Count; i++)
        {
            var ev = level.events[i];
            ev.beat = level.TimeToBeat(ev.time);
            level.events[i] = ev;
        }
        level.SortByTime();
    }
}
