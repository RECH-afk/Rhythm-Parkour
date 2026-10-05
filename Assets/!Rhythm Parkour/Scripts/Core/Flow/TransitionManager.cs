using System;
using System.Collections.Generic;
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
        private Canvas _overlayCanvas;
        private bool _isTransitioning;
        private int _loadGen;
        private readonly List<GraphicRaycaster> _overlayRaycasters = new List<GraphicRaycaster>();
        private readonly List<Graphic> _overlayGraphics = new List<Graphic>();

        protected override void OnReady()
        {
            if (transitionPrefab == null) { Debug.LogError("[Transition] transitionPrefab не назначен", this); return; }
            var instance = Instantiate(transitionPrefab, transform);
            instance.SetActive(true);
            foreach (var gr in instance.GetComponentsInChildren<GraphicRaycaster>(true))
            {
                if (gr == null) continue;
                gr.enabled = false;
                _overlayRaycasters.Add(gr);
            }
            if (_overlayRaycasters.Count == 0)
            {
                var added = instance.AddComponent<GraphicRaycaster>();
                if (added != null) _overlayRaycasters.Add(added);
            }
            foreach (var g in instance.GetComponentsInChildren<Graphic>(true))
            {
                if (g == null) continue;
                g.raycastTarget = false;
                _overlayGraphics.Add(g);
            }
            var canvas = instance.GetComponentInChildren<Canvas>(true);
            if (canvas == null) canvas = instance.AddComponent<Canvas>();
            _overlayCanvas = canvas;
            foreach (var c in instance.GetComponentsInChildren<Canvas>(true))
            {
                if (c == null) continue;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.overrideSorting = true;
                if (c.sortingOrder < 9999) c.sortingOrder = 9999;
            }
            EnforceOverlayCanvas();
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

        void EnforceOverlayCanvas()
        {
            if (_overlayCanvas == null) return;
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.overrideSorting = true;
            if (_overlayCanvas.sortingOrder < 9999) _overlayCanvas.sortingOrder = 9999;
        }

        void SetOverlayBlocking(bool blocking)
        {
            foreach (var gr in _overlayRaycasters)
                if (gr != null) gr.enabled = blocking;
            foreach (var g in _overlayGraphics)
                if (g != null) g.raycastTarget = blocking;
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
            _loadGen++;
            int gen = _loadGen;
            _isTransitioning = true;
            EnforceOverlayCanvas();
            SetOverlayBlocking(true);
            UpdateProgressLabel(0f, 0);

            if (_animator)
                _animator.SetTrigger(triggerIn);

            if (delayBeforeLoad > 0)
                await Task.Delay(TimeSpan.FromSeconds(delayBeforeLoad));
            if (gen != _loadGen) return;

            var async = SceneManager.LoadSceneAsync(sceneName);
            async.allowSceneActivation = false;

            float shown = 0f;
            int phraseIdx = 0;
            float phraseTimer = 0f;
            UpdateProgressLabel(0f, phraseIdx);
            while (async.progress < 0.9f)
            {
                if (gen != _loadGen) return;
                shown = Mathf.MoveTowards(shown, async.progress / 0.9f, Time.unscaledDeltaTime * Mathf.Max(0.1f, percentSmoothSpeed));
                phraseTimer += Time.unscaledDeltaTime;
                if (phraseTimer >= Mathf.Max(0.1f, phraseChangeInterval)) { phraseTimer = 0f; phraseIdx = NextPhraseIndex(phraseIdx); }
                UpdateProgressLabel(shown, phraseIdx);
                await Task.Yield();
            }

            if (gen != _loadGen) return;
            async.allowSceneActivation = true;
            while (shown < 0.999f)
            {
                if (gen != _loadGen) return;
                shown = Mathf.MoveTowards(shown, 1f, Time.unscaledDeltaTime * Mathf.Max(0.1f, percentSmoothSpeed));
                phraseTimer += Time.unscaledDeltaTime;
                if (phraseTimer >= Mathf.Max(0.1f, phraseChangeInterval)) { phraseTimer = 0f; phraseIdx = NextPhraseIndex(phraseIdx); }
                UpdateProgressLabel(shown, phraseIdx);
                await Task.Yield();
            }
            if (gen != _loadGen) return;
            phraseIdx = loadingStages != null && loadingStages.Length > 0 ? loadingStages.Length - 1 : 0;
            UpdateProgressLabel(1f, phraseIdx);
            float endLeft = Mathf.Max(0f, endHoldTime);
            while (endLeft > 0f)
            {
                if (gen != _loadGen) return;
                endLeft -= Time.unscaledDeltaTime;
                await Task.Yield();
            }

            if (gen != _loadGen) return;
            SetOverlayBlocking(false);
            if (_animator)
                _animator.SetTrigger(triggerOut);

            _isTransitioning = false;
        }
    }
}
