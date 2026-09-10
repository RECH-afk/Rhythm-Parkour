using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public static class RhythmAutoGenerator
    {
        public struct FlexibleSettings
        {
            public float density;
            public float threshold;
            public float minGapBeats;
            public float quantStep;
            public int maxIn4Beats;
            public float minSpeed, maxSpeed;
            public bool strictSnap;
            public static FlexibleSettings Default => new FlexibleSettings { density=0.72f, threshold=0.24f, minGapBeats=1.0f, quantStep=0.5f, maxIn4Beats=3, minSpeed=9f, maxSpeed=20f, strictSnap=true };
            public static FlexibleSettings Few => new FlexibleSettings { density=0.45f, threshold=0.32f, minGapBeats=1.5f, quantStep=1f, maxIn4Beats=2, minSpeed=9f, maxSpeed=18f, strictSnap=true };
            public static FlexibleSettings Many => new FlexibleSettings { density=0.88f, threshold=0.14f, minGapBeats=0.7f, quantStep=0.5f, maxIn4Beats=4, minSpeed=10f, maxSpeed=22f, strictSnap=true };
            public static FlexibleSettings Extreme => new FlexibleSettings { density=0.95f, threshold=0.08f, minGapBeats=0.5f, quantStep=0.25f, maxIn4Beats=5, minSpeed=11f, maxSpeed=26f, strictSnap=false };
        }


        public static void Generate(RhythmLevelData level, int seed = 0, float density = 0.72f, float threshold = 0.24f, GlobalObstacleCatalog catalog = null)
        {
            Generate(level, seed, density, threshold, 9f, 20f, catalog);
        }

        public static void Generate(RhythmLevelData level, int seed, float density, float threshold, float minSpeed, float maxSpeed, GlobalObstacleCatalog catalog = null)
        {
            var s = FlexibleSettings.Default;
            s.density = density; s.threshold = threshold; s.minSpeed = minSpeed; s.maxSpeed = maxSpeed;
            Generate(level, seed, s, catalog);
        }


        public static void Generate(RhythmLevelData level, int seed, FlexibleSettings cfg, GlobalObstacleCatalog catalog = null)
        {
            if (level == null || level.music == null) { Debug.LogWarning("[Auto] Нет music"); return; }
            float density = cfg.density;
            float threshold = cfg.threshold;
            float minSpeed = cfg.minSpeed;
            float maxSpeed = cfg.maxSpeed;
            float minGap = Mathf.Clamp(cfg.minGapBeats, 0.4f, 2f);
            float quantStep = cfg.quantStep <= 0.01f ? 0.5f : cfg.quantStep;
            int maxIn4 = Mathf.Clamp(cfg.maxIn4Beats, 1, 6);
            bool strictSnap = cfg.strictSnap;
            var clip = level.music;
            int channels = clip.channels;
            int samples = clip.samples;
            float[] data = new float[samples * channels];
            clip.GetData(data, 0);


            float[] mono = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++) sum += data[i * channels + c];
                mono[i] = sum / Mathf.Max(1, channels);
            }

            int freq = clip.frequency;
            float secPerBeat = 60f / Mathf.Max(1, level.bpm);

            int winShort = Mathf.Clamp(Mathf.RoundToInt(freq * 0.06f), 512, 2048);
            int hop = winShort / 2;
            int winLong = winShort * 4;

            List<float> flux = new List<float>();
            List<float> fluxHigh = new List<float>();
            List<float> times = new List<float>();
            List<float> rmsLong = new List<float>();

            float prevEnergy = 0f;
            float prevDiff = 0f;
            for (int i = 0; i + winLong < samples; i += hop)
            {
                float eShort = 0f;
                for (int j = 0; j < winShort; j++) eShort += Mathf.Abs(mono[i + j]);
                eShort /= winShort;

                float eLong = 0f;
                for (int j = 0; j < winLong; j++) eLong += mono[i + j] * mono[i + j];
                eLong = Mathf.Sqrt(eLong / winLong);

                float curFlux = Mathf.Max(0f, eShort - prevEnergy);
                float diff = Mathf.Abs(mono[i + winShort/2] - mono[i]);
                float highFlux = Mathf.Max(0f, diff - prevDiff * 0.5f);

                flux.Add(curFlux);
                fluxHigh.Add(highFlux);
                rmsLong.Add(eLong);
                times.Add((float)i / freq);
                prevEnergy = eShort;
                prevDiff = diff;
            }


            float maxF = 0f, maxHF = 0f, maxRMS = 0f;
            foreach (var f in flux) if (f > maxF) maxF = f;
            foreach (var f in fluxHigh) if (f > maxHF) maxHF = f;
            foreach (var f in rmsLong) if (f > maxRMS) maxRMS = f;
            if (maxF < 0.0001f) maxF = 1f;
            if (maxHF < 0.0001f) maxHF = 1f;
            if (maxRMS < 0.0001f) maxRMS = 1f;
            for (int i = 0; i < flux.Count; i++) flux[i] /= maxF;
            for (int i = 0; i < fluxHigh.Count; i++) fluxHigh[i] /= maxHF;
            for (int i = 0; i < rmsLong.Count; i++) rmsLong[i] /= maxRMS;


            List<float> onset = new List<float>(flux.Count);
            for (int i = 0; i < flux.Count; i++)
                onset.Add(Mathf.Clamp01(flux[i] * 0.75f + fluxHigh[i] * 0.35f + rmsLong[i] * 0.12f));

            System.Random rng = new System.Random(seed == 0 ? level.fullTitle.GetHashCode() : seed);
            level.events.Clear();

            float totalSec = (float)samples / freq;
            float totalBeats = level.TimeToBeat(totalSec);

            float stepBeat = quantStep;

            int adaptBeats = Mathf.RoundToInt(2f / secPerBeat / stepBeat);


            List<float> beatOnset = new List<float>();
            List<float> beatTimes = new List<float>();
            List<float> beats = new List<float>();
            for (float b = 0f; b <= totalBeats; b += stepBeat)
            {
                float t = level.BeatToTime(b);
                if (t < 0 || t >= totalSec) continue;
                int idx = Mathf.Clamp(Mathf.RoundToInt(t * freq / hop), 0, onset.Count - 1);

                float v = onset[idx];
                if (idx > 0) v = Mathf.Max(v, onset[idx - 1] * 0.9f);
                if (idx + 1 < onset.Count) v = Mathf.Max(v, onset[idx + 1] * 0.9f);
                beats.Add(b);
                beatTimes.Add(t);
                beatOnset.Add(v);
            }


            for (int i = 0; i < beats.Count; i++)
            {

                int a0 = Mathf.Max(0, i - adaptBeats);
                int a1 = Mathf.Min(beats.Count - 1, i + adaptBeats);
                float sum = 0f;
                for (int k = a0; k <= a1; k++) sum += beatOnset[k];
                float avg = sum / (a1 - a0 + 1);
                float var = 0f;
                for (int k = a0; k <= a1; k++) var += (beatOnset[k] - avg) * (beatOnset[k] - avg);
                float std = Mathf.Sqrt(var / (a1 - a0 + 1) + 1e-6f);


                float densFactorThr = Mathf.Clamp01((density - 0.2f) / 0.8f);
                float stdMul = Mathf.Lerp(0.90f, 0.62f, densFactorThr);
                float thrMul = Mathf.Lerp(0.45f, 0.28f, densFactorThr);
                float adaptThr = avg + std * stdMul + threshold * thrMul;
                float rmsAt = rmsLong[Mathf.Clamp(Mathf.RoundToInt(beatTimes[i] * freq / hop), 0, rmsLong.Count - 1)];
                adaptThr = Mathf.Lerp(adaptThr, adaptThr * 0.76f, rmsAt);

                float peakMin = Mathf.Lerp(0.07f, 0.035f, densFactorThr);
                bool isPeak = beatOnset[i] > adaptThr && beatOnset[i] > peakMin;

                if (i > 0 && i + 1 < beatOnset.Count)
                {
                    if (density > 0.85f)
                        isPeak = isPeak && beatOnset[i] >= beatOnset[i - 1] * 0.95f && beatOnset[i] >= beatOnset[i + 1] * 0.95f;
                    else
                        isPeak = isPeak && beatOnset[i] >= beatOnset[i - 1] && beatOnset[i] >= beatOnset[i + 1];
                }
                if (!isPeak) continue;
                if (rng.NextDouble() > density) continue;

                float beat = beats[i];
                float time = beatTimes[i];
                if (!strictSnap)
                {

                    int pIdx = Mathf.Clamp(Mathf.RoundToInt(time * freq / hop), 0, onset.Count - 1);
                    float rawBeat = level.TimeToBeat((float)pIdx * hop / freq);
                    float delta = Mathf.Clamp(rawBeat - beat, -quantStep*0.45f, quantStep*0.45f);
                    beat += delta * 0.35f;
                    beat = Mathf.Round(beat / (quantStep*0.5f)) * (quantStep*0.5f);
                    time = level.BeatToTime(beat);
                }

                if (level.events.Count > 0)
                {
                    float lastGap = beat - level.events[level.events.Count - 1].beat;
                    if (lastGap < minGap)
                    {

                        if (beatOnset[i] < 0.75f || rng.NextDouble() > 0.25) continue;
                    }

                    if (level.events.Count >= 2)
                    {
                        float gap1 = level.events[level.events.Count - 1].beat - level.events[level.events.Count - 2].beat;
                        float gap2 = beat - level.events[level.events.Count - 1].beat;
                        if (gap1 < 1.5f && gap2 < 1.5f)
                        {

                            continue;
                        }
                    }

                    if (level.events.Count >= maxIn4)
                    {
                        int cntInWindow = 0;
                        for (int k = level.events.Count - 1; k >= 0; k--)
                        {
                            if (beat - level.events[k].beat <= 4.0f) cntInWindow++;
                            else break;
                        }
                        if (cntInWindow >= maxIn4) continue;
                    }
                }


                int prefab = 0;
                int prefabCount = catalog != null ? catalog.Count : 0;
                if (prefabCount == 0) prefabCount = level.PrefabCount(catalog);
                if (prefabCount > 0)
                {
                    float strength = beatOnset[i];
                    strength = Mathf.Clamp01(strength * (0.85f + rmsAt * 0.3f));
                    if (prefabCount >= 7)
                    {
                        if (strength > 0.68f) prefab = 0;
                        else if (strength > 0.52f) prefab = rng.NextDouble() < 0.6 ? 1 : 2;
                        else if (strength > 0.38f) prefab = rng.Next(3, 5);
                        else prefab = rng.Next(5, 7);
                    }
                    else if (prefabCount > 1)
                    {
                        if (strength > 0.6f && prefabCount > 2) prefab = 2;
                        else if (strength > 0.35f) prefab = 1;
                        else prefab = 0;
                        if (rng.NextDouble() < 0.10) prefab = rng.Next(0, prefabCount);
                    }
                    prefab = Mathf.Clamp(prefab, 0, prefabCount - 1);
                    if (level.events.Count >= 2)
                    {
                        int p1 = level.events[level.events.Count - 1].prefabIndex;
                        int p2 = level.events[level.events.Count - 2].prefabIndex;
                        if (p1 == prefab && p2 == prefab) prefab = (prefab + 1 + rng.Next(0, prefabCount - 1)) % prefabCount;
                    }
                }


                float bpmFactor = Mathf.Clamp(level.bpm / 120f, 0.75f, 1.35f);
                float baseSpd = Mathf.Lerp(minSpeed, maxSpeed, Mathf.Pow(beatOnset[i], 0.8f));
                baseSpd *= bpmFactor;

                float varScale = (float)(0.88 + rng.NextDouble() * 0.24);
                float spd = baseSpd * varScale;

                if (prefab == 1 || prefab == 2) spd *= 1.08f;
                spd = Mathf.Clamp(spd, 6f, 28f);

                var ev = ObstacleEvent.Create(beat, prefab, Vector3.zero, spd);
                ev.time = time;

                level.events.Add(ev);
            }


            if (level.events.Count > 0 && level.events[0].beat > 4f)
            {
                float shift = Mathf.Floor(level.events[0].beat - 2f);
                for (int i = 0; i < level.events.Count; i++) { var e = level.events[i]; e.beat -= shift; e.time = level.BeatToTime(e.beat); level.events[i] = e; }
            }

            if (level.events.Count > 1)
            {
                var sorted = new List<ObstacleEvent>(level.events);
                sorted.Sort((a,b)=>a.beat.CompareTo(b.beat));
                List<ObstacleEvent> filled = new List<ObstacleEvent>(sorted);
                float avgSpd = (minSpeed + maxSpeed) * 0.5f;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    float gap = sorted[i + 1].beat - sorted[i].beat;
                    if (gap > 4f + minGap)
                    {

                        float beat = sorted[i].beat + gap * 0.5f;
                        beat = Mathf.Round(beat / quantStep) * quantStep;
                        if (!strictSnap) beat = Mathf.Clamp(beat + (float)(rng.NextDouble()-0.5)*0.18f, sorted[i].beat + minGap*0.8f, sorted[i+1].beat - minGap*0.8f);
                        if (beat >= sorted[i + 1].beat - minGap*0.9f) continue;

                        int cntNear = 0;
                        foreach (var ex in filled) if (Mathf.Abs(ex.beat - beat) <= 2.0f) cntNear++;
                        if (cntNear >= 2) continue;
                        int bIdx = beats.IndexOf(beat);
                        if (bIdx < 0) bIdx = Mathf.Clamp(Mathf.RoundToInt(level.TimeToBeat(level.BeatToTime(beat)) * 2f), 0, beatOnset.Count - 1);
                        if (bIdx >= 0 && bIdx < beatOnset.Count && beatOnset[bIdx] < 0.05f) continue;
                        int pCount2 = catalog != null ? catalog.Count : 0; if (pCount2 == 0) pCount2 = level.PrefabCount(catalog); if (pCount2 == 0) pCount2 = 1;
                        int p = rng.Next(0, Mathf.Max(1, pCount2));
                        var ev = ObstacleEvent.Create(beat, p, Vector3.zero, avgSpd * (float)(0.92 + rng.NextDouble()*0.16));
                        ev.time = level.BeatToTime(beat);
                        filled.Add(ev);
                    }
                }
                level.events = filled;
            }


            float densFactor = Mathf.Clamp01((density - 0.2f) / 0.8f);
            float targetRate = Mathf.Lerp(0.18f, 0.55f, Mathf.Pow(densFactor, 0.85f));
            if (quantStep < 0.4f) targetRate *= 1.28f;
            targetRate = Mathf.Clamp(targetRate, 0.12f, 0.70f);
            int targetMin = Mathf.RoundToInt(totalBeats * targetRate);
            targetMin = Mathf.Clamp(targetMin, 12, 220);
            if (level.events.Count < targetMin)
            {
                float avgSpd = (minSpeed + maxSpeed) * 0.5f;
                int need = targetMin - level.events.Count;

                float fillStep = quantStep * 3f;
                if (fillStep < minGap) fillStep = minGap;
                for (float b = 2f; b < totalBeats && need > 0; b += fillStep)
                {
                    if (level.events.Exists(x => Mathf.Abs(x.beat - b) < minGap*0.9f)) continue;

                    int cntInWin = 0;
                    foreach (var ex in level.events) if (Mathf.Abs(ex.beat - b) <= 4f) cntInWin++;
                    if (cntInWin >= maxIn4) continue;
                    int bIdx = Mathf.Clamp(Mathf.RoundToInt((b - beats[0]) / stepBeat), 0, beatOnset.Count - 1);
                    if (bIdx >= 0 && bIdx < beatOnset.Count && beatOnset[bIdx] < 0.05f) continue;
                    if (rng.NextDouble() > 0.35f) continue;
                    float fb = Mathf.Round(b / quantStep) * quantStep;
                    if (!strictSnap) fb += (float)(rng.NextDouble()-0.5)*quantStep*0.35f;
                    fb = Mathf.Round(fb / quantStep) * quantStep;
                    int pCount3 = catalog != null ? catalog.Count : 0; if (pCount3 == 0) pCount3 = level.PrefabCount(catalog); if (pCount3 == 0) pCount3 = 1;
                    int p = rng.Next(0, Mathf.Max(1, pCount3));
                    var ev = ObstacleEvent.Create(fb, p, Vector3.zero, avgSpd * (float)(0.92 + rng.NextDouble()*0.18));
                    ev.time = level.BeatToTime(fb);
                    level.events.Add(ev);
                    need--;
                }

                for (int i = 0; level.events.Count < targetMin && i < 500; i++)
                {
                    float beat = 4f + i * Mathf.Max(2f, minGap*2f);
                    beat = Mathf.Round(beat / quantStep) * quantStep;
                    if (beat >= totalBeats) break;
                    if (level.events.Exists(x => Mathf.Abs(x.beat - beat) < minGap*0.9f)) continue;
                    int pCount4 = catalog != null ? catalog.Count : 0; if (pCount4 == 0) pCount4 = level.PrefabCount(catalog); if (pCount4 == 0) pCount4 = 1;
                    int p = i % Mathf.Max(1, pCount4);
                    var ev = ObstacleEvent.Create(beat, p, Vector3.zero, avgSpd);
                    ev.time = level.BeatToTime(beat);
                    level.events.Add(ev);
                }
            }




            level.SortByTime();
#if UNITY_EDITOR
            AssetDatabase.SaveAssets();
#endif
            Debug.Log($"[Auto★] Сгенерировано {level.events.Count} нот (thr {threshold:0.00} dens {density:0.00} bpm {level.bpm} spd {minSpeed:0}-{maxSpeed:0})");
        }
    }
}
