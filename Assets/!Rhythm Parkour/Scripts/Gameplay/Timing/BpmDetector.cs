using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public static class BpmDetector
    {
        public struct Result
        {
            public float bpm;
            public float confidence;
            public float offset;
        }

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

float[] mono = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += data[i * ch + c];
                mono[i] = s / Mathf.Max(1, ch);
            }

int envRate = 100;
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

            float envMax = 0f;
            for (int i = 0; i < envLen; i++) if (env[i] > envMax) envMax = env[i];
            if (envMax < 1e-6f) return res;
            for (int i = 0; i < envLen; i++) env[i] /= envMax;

float[] flux = new float[envLen];
            flux[0] = 0f;
            for (int i = 1; i < envLen; i++)
            {
                float diff = env[i] - env[i - 1];

                flux[i] = diff > 0 ? diff : 0f;

                if (env[i] < 0.08f) flux[i] *= 0.5f;
            }

            float[] fluxS = new float[envLen];
            for (int i = 1; i < envLen - 1; i++) fluxS[i] = (flux[i - 1] * 0.25f + flux[i] * 0.5f + flux[i + 1] * 0.25f);
            flux = fluxS;

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

                float bpmAtLag = 60f / (lag * secPerSample);
                float tempoWeight = 1f - Mathf.Abs(bpmAtLag - 120f) * 0.0008f;
                c *= Mathf.Clamp01(tempoWeight);
                corr[lag] = c;
                if (c > bestCorr) { bestCorr = c; bestLag = lag; }
            }

if (bestLag > minLag && bestLag < maxLag)
            {
                float c0 = corr[bestLag - 1], c1 = corr[bestLag], c2 = corr[bestLag + 1];
                float denom = (c0 - 2 * c1 + c2);
                if (Mathf.Abs(denom) > 1e-6f)
                {
                    float shift = 0.5f * (c0 - c2) / denom;
                    float refinedLag = bestLag + Mathf.Clamp(shift, -0.5f, 0.5f);
                    bestLag = Mathf.RoundToInt(refinedLag);

                }
            }

            float detectedBpm = 60f / (bestLag * secPerSample);

            int doubleLag = bestLag * 2;
            int halfLag = Mathf.RoundToInt(bestLag * 0.5f);
            if (doubleLag >= minLag && doubleLag <= maxLag && corr[doubleLag] > bestCorr * 0.88f)
            {

}
            if (halfLag >= minLag && halfLag <= maxLag && corr[halfLag] > bestCorr * 0.92f)
            {

                float halfBpm = 60f / (halfLag * secPerSample);
                if (halfBpm >= 90f && halfBpm <= 170f && corr[halfLag] > bestCorr * 0.97f)
                {
                    detectedBpm = halfBpm;
                    bestCorr = corr[halfLag];
                    bestLag = halfLag;
                }
            }

float offset = 0f;
            float thresh = 0.18f;
            for (int i = 0; i < envLen; i++)
            {
                if (flux[i] > thresh)
                {
                    offset = i * secPerSample;

float period = 60f / detectedBpm;
                    offset = offset % period;

                    if (offset > period * 0.5f) offset -= period;
                    if (offset < 0) offset = 0;
                    break;
                }
            }

            float conf = Mathf.Clamp01(bestCorr * 2.5f);
            res.bpm = Mathf.Clamp(detectedBpm, minBpm, maxBpm);
            res.confidence = conf;
            res.offset = offset;
            return res;
        }

public static bool ApplyBpm(RhythmLevelData level, float newBpm, bool keepBeats = true)
        {
            if (level == null) return false;
            if (newBpm < 40f || newBpm > 300f) return false;
            if (Mathf.Abs(level.bpm - newBpm) < 0.01f) return true;
            float oldBpm = level.bpm;
            level.bpm = newBpm;
            if (!keepBeats) return true;

            for (int i = 0; i < level.events.Count; i++)
            {
                var ev = level.events[i];

                ev.time = level.BeatToTime(ev.beat);
                level.events[i] = ev;
            }
            level.SortByTime();
            return true;
        }

public static bool KeepTimes(RhythmLevelData level, float newBpm)
        {
            if (level == null) return false;
            if (newBpm < 40f || newBpm > 300f) return false;
            level.bpm = newBpm;
            for (int i = 0; i < level.events.Count; i++)
            {
                var ev = level.events[i];
                ev.beat = level.TimeToBeat(ev.time);
                level.events[i] = ev;
            }
            level.SortByTime();
            return true;
        }
    }
}
