using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Managers
{
    public class TransitionManager : RKSBehaviour
    {
        [SerializeField] private GameObject transitionPrefab;
        [SerializeField] private float delayBeforeLoad = 0f;
        [SerializeField] private string[] loadingStages = new string[]
        {
            "загружаем сцену",
            "читаем файлы",
            "читаем манифест",
            "распаковываем ресурсы",
            "декодируем звук",
            "загружаем аудио",
            "загружаем видео",
            "подгружаем обложку",
            "считаем BPM",
            "строим waveform",
            "подключаем кондуктор",
            "строим уровень",
            "спавним трек",
            "выравниваем дорожку",
            "расставляем препятствия",
            "греем пулы объектов",
            "настраиваем свет",
            "компилируем шейдеры",
            "синхронизируем часы",
            "проверяем данные",
            "чистим память",
            "финальный проход",
            "почти готово",
            "открываем занавес",
            "полетели, масса!"
        };
        [SerializeField] private float percentSmoothSpeed = 1.2f;
        [SerializeField] private float phraseChangeInterval = 0.9f;
        [SerializeField] private float endHoldTime = 0.8f;

        private readonly string triggerIn = "TransitionIn";
        private readonly string triggerOut = "TransitionOut";

        private Animator _animator;
        private TextMeshProUGUI _progressLabel;
        private bool _isTransitioning;

        protected override void OnReady()
        {
            if (transitionPrefab == null) { Debug.LogError("[Transition] transitionPrefab не назначен", this); return; }
            var instance = Instantiate(transitionPrefab, transform);
            instance.SetActive(true);
            var canvas = instance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas != null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 9999);
            }
            _animator = instance.GetComponent<Animator>();
            if (_animator == null) _animator = instance.GetComponentInChildren<Animator>(true);
            if (_animator == null) Debug.LogError("[Transition] Animator не найден в префабе перехода", this);
            _progressLabel = null;
            foreach (var tmp in instance.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp != null && tmp.gameObject.name == "TextLabel") { _progressLabel = tmp; break; }
            }
            if (_progressLabel == null) _progressLabel = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            UpdateProgressLabel(0f, 0);
        }

        void UpdateProgressLabel(float norm, int phraseIdx)
        {
            if (_progressLabel == null) return;
            int pct = Mathf.Clamp(Mathf.RoundToInt(norm * 100f), 0, 100);
            string stage = loadingStages != null && loadingStages.Length > 0
                ? loadingStages[Mathf.Clamp(phraseIdx, 0, loadingStages.Length - 1)]
                : "";
            _progressLabel.text = $"<size=35>{pct}%</size>\n\n<size=20>{stage}</size>";
        }

        int NextPhraseIndex(int current)
        {
            if (loadingStages == null || loadingStages.Length == 0) return 0;
            if (loadingStages.Length == 1) return 0;
            int next = current;
            while (next == current) next = UnityEngine.Random.Range(0, loadingStages.Length);
            return next;
        }

        public void LoadScene(string sceneName)
        {
            _ = LoadSceneAsync(sceneName);
        }

        public async Task LoadSceneAsync(string sceneName)
        {
            if (_isTransitioning)
                return;

            _isTransitioning = true;

            if (_animator)
                _animator.SetTrigger(triggerIn);

            if (delayBeforeLoad > 0)
                await Task.Delay(TimeSpan.FromSeconds(delayBeforeLoad));

            var async = SceneManager.LoadSceneAsync(sceneName);
            async.allowSceneActivation = false;

            float shown = 0f;
            int phraseIdx = 0;
            float phraseTimer = 0f;
            UpdateProgressLabel(0f, phraseIdx);
            while (async.progress < 0.9f)
            {
                shown = Mathf.MoveTowards(shown, async.progress / 0.9f, Time.unscaledDeltaTime * Mathf.Max(0.1f, percentSmoothSpeed));
                phraseTimer += Time.unscaledDeltaTime;
                if (phraseTimer >= Mathf.Max(0.1f, phraseChangeInterval)) { phraseTimer = 0f; phraseIdx = NextPhraseIndex(phraseIdx); }
                UpdateProgressLabel(shown, phraseIdx);
                await Task.Yield();
            }

            async.allowSceneActivation = true;
            while (shown < 0.999f)
            {
                shown = Mathf.MoveTowards(shown, 1f, Time.unscaledDeltaTime * Mathf.Max(0.1f, percentSmoothSpeed));
                phraseTimer += Time.unscaledDeltaTime;
                if (phraseTimer >= Mathf.Max(0.1f, phraseChangeInterval)) { phraseTimer = 0f; phraseIdx = NextPhraseIndex(phraseIdx); }
                UpdateProgressLabel(shown, phraseIdx);
                await Task.Yield();
            }
            phraseIdx = loadingStages != null && loadingStages.Length > 0 ? loadingStages.Length - 1 : 0;
            UpdateProgressLabel(1f, phraseIdx);
            float endLeft = Mathf.Max(0f, endHoldTime);
            while (endLeft > 0f)
            {
                endLeft -= Time.unscaledDeltaTime;
                await Task.Yield();
            }

            if (_animator)
                _animator.SetTrigger(triggerOut);

            _isTransitioning = false;
        }
    }
}
