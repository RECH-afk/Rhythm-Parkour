using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI.Timeline;








namespace RKS.RhythmParkour.UI
{
    public class MusicInfoUI : RKSBehaviour
    {
        [Header("Ссылки (инжект)")]
        public RhythmLevelData levelData;
        [InjectOptional] public RhythmParkourManager manager;
        [InjectOptional] public LevelTransfer transfer;

        [Header("UI — TextMeshPro")]
        public CanvasGroup canvasGroup;
        public RectTransform panelRect;
        public Image coverImage;
        public Image coverBackground;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI durationText;
        public TextMeshProUGUI extraText;

        [Header("Тайминг")]
        public float showDelay = 0.3f;
        public float stayDuration = 2.2f;
        [Tooltip("Сверху → середина")]
        public float slideInDuration = 0.85f;
        [Tooltip("Середина → вниз")]
        public float slideOutDuration = 0.65f;

        [Header("DOTween")]
        public Ease slideInEase = Ease.OutCubic;
        public Ease slideOutEase = Ease.InCubic;
        public float topY = 650f;
        public float middleY = 0f;
        public float bottomY = -650f;

        [Header("Формат")]
        public string durationFormat = "{0:0}:{1:00}";

        Tween activeTween;
        bool isShowing;

        void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            panelRect = GetComponent<RectTransform>();
            if (panelRect == null) panelRect = GetComponentInChildren<RectTransform>(true);
        }

        protected override void OnInjected()
        {
            if (panelRect == null) panelRect = transform as RectTransform;
            if (panelRect == null) panelRect = GetComponentInChildren<RectTransform>(true);
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (panelRect) panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, topY);

            ResolveLevelRef();
        }

        void ResolveLevelRef()
        {
            if (manager != null && levelData == null) levelData = manager.levelData;
            if (levelData == null && transfer != null && transfer.hasLevel) levelData = transfer.levelData;
        }

        protected override void OnReady()
        {
            ResolveLevelRef();
            Invoke(nameof(TriggerShow), showDelay);
        }

        protected override void OnDisposed() { activeTween?.Kill(); }

        void TriggerShow()
        {
            ResolveLevelRef();
            if (levelData == null) return;
            Show(levelData);
        }

        public void Show(RhythmLevelData data)
        {
            if (data == null) return;
            levelData = data;

            if (coverImage)
            {
                if (data.cover)
                {
                    coverImage.sprite = data.cover;
                    coverImage.enabled = true;
                    coverImage.preserveAspect = true;
                }
                else coverImage.enabled = false;
            }
            if (coverBackground) { coverBackground.sprite = data.cover; coverBackground.enabled = data.cover != null; }

            string title = !string.IsNullOrWhiteSpace(data.fullTitle) ? data.fullTitle : (data.music ? data.music.name : "Unknown");
            if (titleText) titleText.text = title;
            if (extraText) extraText.text = "";

            float len = data.music ? data.music.length : 0f;
            if (durationText) durationText.text = len > 0.1f ? $"{Mathf.FloorToInt(len/60):0}:{Mathf.FloorToInt(len%60):00}" : "--:--";

            PlayDOTween();
        }

        void PlayDOTween()
        {
            if (panelRect == null) panelRect = transform as RectTransform;
            activeTween?.Kill();
            isShowing = true;
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, topY);


            panelRect.localScale = Vector3.one * 0.96f;

            Sequence seq = DOTween.Sequence();

            seq.Append(panelRect.DOAnchorPosY(middleY, slideInDuration).SetEase(slideInEase));
            seq.Join(canvasGroup.DOFade(1f, slideInDuration * 0.55f).SetEase(Ease.OutSine));
            seq.Join(panelRect.DOScale(Vector3.one, slideInDuration * 0.7f).SetEase(Ease.OutBack, 0.9f));

            seq.AppendInterval(stayDuration);

            seq.Append(panelRect.DOAnchorPosY(bottomY, slideOutDuration).SetEase(slideOutEase));
            seq.Join(canvasGroup.DOFade(0f, slideOutDuration * 0.85f).SetEase(Ease.InSine));
            seq.Join(panelRect.DOScale(Vector3.one * 0.92f, slideOutDuration).SetEase(Ease.InBack, 0.6f));
            seq.OnComplete(() =>
            {
                isShowing = false;
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, topY);
                panelRect.localScale = Vector3.one;
            });
            seq.SetUpdate(true);
            activeTween = seq;
        }

        public void HideImmediate()
        {
            activeTween?.Kill();
            canvasGroup.alpha = 0f;
            isShowing = false;
            if (panelRect) panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, topY);
        }

        public void Refresh(){ ResolveLevelRef(); if (levelData != null) Show(levelData); }

#if UNITY_EDITOR
        [ContextMenu("Preview DOTween")]
        void PreviewShow(){ ResolveLevelRef(); if (levelData != null) Show(levelData); }
#endif
    }
}
