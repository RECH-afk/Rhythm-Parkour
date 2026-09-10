using System;
using RKS.RhythmParkour.Core;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public enum HitJudgement
    {
        Perfect300,
        Great100,
        Good50,
        Miss
    }

[Serializable]
    public struct LevelResults
    {
        public string levelTitle;
        public int totalNotes;
        public int count300;
        public int count100;
        public int count50;
        public int countMiss;
        public int maxCombo;
        public float flow;
        public float accuracy;
        public string rank;
        public bool failed;
        public int groovePerfect;
        public int grooveGood;
        public int maxMissStreak;

        public int Judged => count300 + count100 + count50 + countMiss;
    }

public class RhythmScoreManager : RKSBehaviour
    {

        [Header("Flow")]
        [Tooltip("Стартовое значение потока")]
        [Range(0, 100)] public float flowStart = 50f;
        [Tooltip("Прирост за Perfect")]
        public float flowGainPerfect = 4f;
        [Tooltip("Прирост за Great")]
        public float flowGainGreat = 2f;
        [Tooltip("Прирост за Good")]
        public float flowGainGood = 1f;
        [Tooltip("Потеря за Miss")]
        public float flowLoseMiss = 15f;
        [Tooltip("Поток за действие в бит (прыжок/слайд)")]
        public float grooveFlowPerfect = 2f;
        [Tooltip("Поток за действие около бита")]
        public float grooveFlowGood = 1f;

        [Header("Beat Streak")]
        [Tooltip("Каждые N битов серии — событие для попапа/звука")]
        public int milestoneStep = 25;

        [Header("Judgement Weights")]
        [Range(0, 100)] public float perfectWeight = 100f;
        [Range(0, 100)] public float greatWeight = 65f;
        [Range(0, 100)] public float goodWeight = 35f;

        [Header("Rank Thresholds")]
        [Range(0, 1)] public float godlikeMin = 0.95f;
        [Range(0, 1)] public float awesomeMin = 0.80f;
        [Range(0, 1)] public float soSoMin = 0.60f;
        [Range(0, 1)] public float badMin = 0.35f;
        [Tooltip("GODLIKE! только без единого мисса")]
        public bool godlikeRequiresNoMiss = true;

        [Header("State")]
        public string levelTitle = "";
        public int totalNotes;
        public int count300;
        public int count100;
        public int count50;
        public int countMiss;
        [Tooltip("Текущая серия битов без урона")]
        public int combo;
        [Tooltip("Лучшая серия битов за уровень")]
        public int maxCombo;
        [Tooltip("Поток 0–100 (вместо очков)")]
        public float flow;
        [Tooltip("Текущая серия миссов подряд")]
        public int missStreak;
        [Tooltip("Прыжки/слайды/приземления идеально в бит")]
        public int groovePerfect;
        [Tooltip("Прыжки/слайды/приземления хорошо в бит")]
        public int grooveGood;
        public bool isLevelActive;
        public bool isFinished;
        public bool isFailed;

public event Action<HitJudgement, int> onJudgement;
        public event Action onScoreChanged;
        public event Action<LevelResults> onLevelFinished;

        public event Action<int> onComboMilestone;

        public int Judged => count300 + count100 + count50 + countMiss;

        int maxMissStreakRuntime;

        [HideInInspector]
        [InjectOptional] public Conductor injectedBeatSource;
        Conductor beatSrc;

        protected override void OnInjected()
        {
            if (injectedBeatSource != null) beatSrc = injectedBeatSource;
        }

        protected override void OnDisposed()
        {
            UnsubscribeBeats();
        }

        void SubscribeBeats()
        {
            UnsubscribeBeats();
            if (beatSrc != null) { beatSrc.onBeat += OnBeatTick; }
        }

        void UnsubscribeBeats()
        {
            if (beatSrc != null) { try { beatSrc.onBeat -= OnBeatTick; } catch {} beatSrc = null; }
        }

void OnBeatTick()
        {
            if (!isLevelActive || isFinished || isFailed) return;
            combo++;
            if (combo > maxCombo) maxCombo = combo;
            CheckMilestone();
            onScoreChanged?.Invoke();
        }

public void BeginLevel(string title, int total)
        {
            levelTitle = title ?? "";
            totalNotes = Mathf.Max(0, total);
            count300 = 0;
            count100 = 0;
            count50 = 0;
            countMiss = 0;
            combo = 0;
            maxCombo = 0;
            flow = Mathf.Clamp(flowStart, 0f, 100f);
            missStreak = 0;
            maxMissStreakRuntime = 0;
            groovePerfect = 0;
            grooveGood = 0;
            isLevelActive = true;
            isFinished = false;
            isFailed = false;
            SubscribeBeats();
            onScoreChanged?.Invoke();
        }

        public void RegisterPerfect() => Register(HitJudgement.Perfect300);
        public void RegisterGreat() => Register(HitJudgement.Great100);
        public void RegisterGood() => Register(HitJudgement.Good50);

public void RegisterJudgement(HitJudgement j) => Register(j);

public void RegisterMiss()
        {
            Register(HitJudgement.Miss);
        }

public void AddGrooveHit(bool perfect)
        {
            if (!isLevelActive || isFinished) return;
            flow = Mathf.Clamp(flow + (perfect ? grooveFlowPerfect : grooveFlowGood), 0f, 100f);
            if (perfect) groovePerfect++; else grooveGood++;
            onScoreChanged?.Invoke();
        }

        void Register(HitJudgement j)
        {
            if (!isLevelActive || isFinished) return;
            switch (j)
            {
                case HitJudgement.Perfect300:
                    count300++;
                    missStreak = 0;
                    flow = Mathf.Clamp(flow + flowGainPerfect, 0f, 100f);
                    break;
                case HitJudgement.Great100:
                    count100++;
                    missStreak = 0;
                    flow = Mathf.Clamp(flow + flowGainGreat, 0f, 100f);
                    break;
                case HitJudgement.Good50:
                    count50++;
                    missStreak = 0;
                    flow = Mathf.Clamp(flow + flowGainGood, 0f, 100f);
                    break;
                case HitJudgement.Miss:
                    countMiss++;
                    combo = 0;
                    flow = Mathf.Clamp(flow - Mathf.Max(0f, flowLoseMiss), 0f, 100f);
                    missStreak++;
                    if (missStreak > maxMissStreakRuntime) maxMissStreakRuntime = missStreak;
                    break;
            }
            if (combo > maxCombo) maxCombo = combo;
            onJudgement?.Invoke(j, combo);
            onScoreChanged?.Invoke();
        }

void CheckMilestone()
        {
            int step = Mathf.Max(1, milestoneStep);
            if (combo > 0 && combo % step == 0)
            {
                try { onComboMilestone?.Invoke(combo); } catch (Exception e) { Debug.LogWarning($"[Score] onComboMilestone: {e.Message}"); }
            }
        }

public void Fail()
        {
            isFailed = true;
        }

public LevelResults Finish(bool failed = false)
        {
            if (failed) isFailed = true;
            isLevelActive = false;
            isFinished = true;
            UnsubscribeBeats();
            var r = BuildResults();
            try { onLevelFinished?.Invoke(r); } catch (Exception e) { Debug.LogWarning($"[Score] onLevelFinished: {e.Message}"); }
            Debug.Log($"[Score] Финиш '{levelTitle}': 300={r.count300} 100={r.count100} 50={r.count50} Miss={r.countMiss} Acc={r.accuracy * 100f:0.00}% Rank={r.rank} Flow={r.flow:0} MaxCombo={r.maxCombo}", this);
            return r;
        }

        public LevelResults BuildResults()
        {
            return new LevelResults
            {
                levelTitle = levelTitle,
                totalNotes = totalNotes,
                count300 = count300,
                count100 = count100,
                count50 = count50,
                countMiss = countMiss,
                maxCombo = maxCombo,
                flow = flow,
                accuracy = Accuracy,
                rank = Rank,
                failed = isFailed,
                groovePerfect = groovePerfect,
                grooveGood = grooveGood,
                maxMissStreak = maxMissStreakRuntime
            };
        }

public float Accuracy
        {
            get
            {
                int judged = Judged;
                if (judged <= 0) return 1f;
                float got = count300 * perfectWeight + count100 * greatWeight + count50 * goodWeight;
                return Mathf.Clamp01(got / (judged * Mathf.Max(1f, perfectWeight)));
            }
        }

public string Rank
        {
            get
            {
                if (isFailed) return "TRASH";
                if (Judged <= 0) return totalNotes > 0 ? "TRASH" : "--";
                float acc = Accuracy;
                if (acc >= godlikeMin && (!godlikeRequiresNoMiss || countMiss == 0)) return "GODLIKE!";
                if (acc >= awesomeMin) return "AWESOME";
                if (acc >= soSoMin) return "SO SO";
                if (acc >= badMin) return "BAD";
                return "TRASH";
            }
        }

        public static Color RankColor(string rank)
        {
            switch (rank)
            {
                case "GODLIKE!": return new Color(1f, 0.84f, 0.25f);
                case "AWESOME": return new Color(0.35f, 0.95f, 0.45f);
                case "SO SO": return new Color(1f, 0.95f, 0.35f);
                case "BAD": return new Color(1f, 0.6f, 0.25f);
                case "TRASH": return new Color(0.55f, 0.55f, 0.6f);
                default: return new Color(0.7f, 0.7f, 0.75f);
            }
        }

        public static string JudgementText(HitJudgement j)
        {
            switch (j)
            {
                case HitJudgement.Perfect300: return "PERFECT";
                case HitJudgement.Great100: return "GREAT";
                case HitJudgement.Good50: return "GOOD";
                default: return "MISS";
            }
        }

        public static Color JudgementColor(HitJudgement j)
        {
            switch (j)
            {
                case HitJudgement.Perfect300: return new Color(0.35f, 0.9f, 1f);
                case HitJudgement.Great100: return new Color(0.45f, 1f, 0.45f);
                case HitJudgement.Good50: return new Color(1f, 0.85f, 0.3f);
                default: return new Color(1f, 0.3f, 0.3f);
            }
        }
    }
}
