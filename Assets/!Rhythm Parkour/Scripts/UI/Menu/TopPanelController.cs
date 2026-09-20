using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Zenject;
using RKS.RhythmParkour.Core;

namespace RKS.RhythmParkour.UI
{
    public class TopPanelController : RKSBehaviour
    {
        [Header("Panel")]
        public RectTransform root;

        [Header("Track Info")]
        public TextMeshProUGUI trackText;
        public TextMeshProUGUI timeText;

        [Header("Play Control")]
        public Image playIcon;
        public TextMeshProUGUI playLabel;
        public Sprite playSprite;
        public Sprite pauseSprite;

        [Header("Repeat Control")]
        public Image repeatIcon;
        public Color repeatOnColor;
        public Color repeatOffColor;

        [Header("Minimize Control")]
        public Image minimizeIcon;
        public TextMeshProUGUI minimizeLabel;
        public Color minimizeOnColor;
        public Color minimizeOffColor;

        [Header("Minimize Target")]
        public RectTransform infoTarget;
        public float minimizedLeft;
        public float minimizedRight;
        public float minimizeDuration = 0.35f;

        [Header("Motion")]
        public float slideDuration = 0.4f;
        public float shownY = -12f;
        public float parkedY = 140f;

        private MenuController menu;
        private bool shown;
        private bool minimized;
        private string lastTrackKey = "";
        private bool lastPlaying;
        private bool lastRepeatState = true;
        private string lastTimeText = "";
        private float lastKnownTime = -1f;
        private Tween slideTween;
        private Tween minTween;
        private Tween rootNudgeTween;
        private float baseRootX;
        private Vector2 expandedMin;
        private Vector2 expandedMax;
        private bool expandedCaptured;

        [Inject]
        public void ConstructPanel(MenuController menuController)
        {
            if (menuController != null) menu = menuController;
        }

        protected override void OnReady()
        {
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, parkedY);
            baseRootX = root.anchoredPosition.x;
            root.gameObject.SetActive(false);
            if (infoTarget != null)
            {
                expandedMin = infoTarget.offsetMin;
                expandedMax = infoTarget.offsetMax;
                expandedCaptured = true;
            }
            RefreshPlayIcon();
            RefreshRepeatIcon();
            RefreshMinimizeIcon();
        }

        protected override void OnDisposed()
        {
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            if (minTween != null && minTween.IsActive()) minTween.Kill();
            if (rootNudgeTween != null && rootNudgeTween.IsActive()) rootNudgeTween.Kill();
        }

        void NudgeRoot()
        {
            if (root == null) return;
            if (rootNudgeTween != null && rootNudgeTween.IsActive()) rootNudgeTween.Kill();
            rootNudgeTween = DOTween.Sequence()
                .Append(root.DOAnchorPosX(baseRootX + 28f, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true))
                .Append(root.DOAnchorPosX(baseRootX, 0.55f).SetEase(Ease.OutBack, 1.2f).SetUpdate(true));
        }

        protected override void Update()
        {
            if (menu == null || root == null) return;
            bool want = menu.HasPreview
                && (menu.IsPreviewPlaying || menu.PreviewPaused)
                && menu.IsMainMenuVisible
                && !menu.IsDetailsVisible;
            if (want && !shown) Show();
            else if (!want && shown) Hide();
            if (!shown) return;
            if (trackText != null)
            {
                bool trackPlaying = menu.IsPreviewPlaying;
                string key = (trackPlaying ? "play|" : "pause|") + menu.CurrentPreviewTitle + "|" + menu.CurrentPreviewArtist;
                if (key != lastTrackKey)
                {
                    lastTrackKey = key;
                    string prefix = trackPlaying ? "сейчас играет трек:" : "сейчас на паузе трек:";
                    trackText.text = $"{prefix} {menu.CurrentPreviewTitle} от {menu.CurrentPreviewArtist}";
                }
            }
            bool playing = menu.IsPreviewPlaying;
            if (playing != lastPlaying)
            {
                lastPlaying = playing;
                RefreshPlayIcon();
            }
            if (menu.RepeatEnabled != lastRepeatState)
            {
                lastRepeatState = menu.RepeatEnabled;
                RefreshRepeatIcon();
            }
            if (timeText != null)
            {
                float t = menu.PreviewTime;
                if (t >= 0f) lastKnownTime = t;
                string txt = FormatTime(lastKnownTime) + " / " + FormatTime(menu.PreviewTrackLength);
                if (txt != lastTimeText)
                {
                    lastTimeText = txt;
                    timeText.text = txt;
                }
            }
        }

        public void PlayToggleButton()
        {
            if (menu != null) menu.TogglePreviewPlayback();
        }

        public void RepeatToggleButton()
        {
            if (menu != null) menu.ToggleRepeat();
        }

        void RefreshRepeatIcon()
        {
            if (repeatIcon == null) return;
            bool on = menu != null && menu.RepeatEnabled;
            repeatIcon.color = on ? repeatOnColor : repeatOffColor;
        }

        void RefreshMinimizeIcon()
        {
            if (minimizeIcon != null)
                minimizeIcon.color = minimized ? minimizeOnColor : minimizeOffColor;
            if (minimizeLabel != null)
                minimizeLabel.text = minimized ? "РАСКРЫТЬ" : "СКРЫТЬ";
        }

        static string FormatTime(float seconds)
        {
            if (seconds < 0f) return "--:--";
            int total = Mathf.RoundToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }

        public void MinimizeToggleButton()
        {
            if (root == null || infoTarget == null) return;
            minimized = !minimized;
            if (minTween != null && minTween.IsActive()) minTween.Kill();
            Vector2 fromMin = infoTarget.offsetMin;
            Vector2 fromMax = infoTarget.offsetMax;
            Vector2 toMin = minimized ? new Vector2(minimizedLeft, expandedMin.y) : expandedMin;
            Vector2 toMax = minimized ? new Vector2(minimizedRight, expandedMax.y) : expandedMax;
            minTween = DOTween.To(
                () => 0f,
                t =>
                {
                    if (infoTarget == null) return;
                    infoTarget.offsetMin = Vector2.LerpUnclamped(fromMin, toMin, t);
                    infoTarget.offsetMax = Vector2.LerpUnclamped(fromMax, toMax, t);
                },
                1f,
                Mathf.Max(0.01f, minimizeDuration))
                .SetEase(Ease.OutBack, 0.9f)
                .SetUpdate(true);
            NudgeRoot();
            RefreshMinimizeIcon();
        }

        void RefreshPlayIcon()
        {
            bool playing = menu != null && menu.IsPreviewPlaying;
            if (playIcon != null && playSprite != null && pauseSprite != null)
                playIcon.sprite = playing ? pauseSprite : playSprite;
            if (playLabel != null)
                playLabel.text = playing ? "ПАУЗА" : "ИГРАТЬ";
        }

        void Show()
        {
            if (root == null || shown) return;
            shown = true;
            lastTrackKey = "";
            lastTimeText = "";
            lastKnownTime = -1f;
            RefreshPlayIcon();
            RefreshRepeatIcon();
            root.gameObject.SetActive(true);
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            var ap = root.anchoredPosition;
            root.anchoredPosition = new Vector2(ap.x, parkedY);
            slideTween = root.DOAnchorPosY(shownY, slideDuration).SetEase(Ease.OutBack, 1.1f).SetUpdate(true);
        }

        void Hide()
        {
            if (root == null || !shown) return;
            shown = false;
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            slideTween = root.DOAnchorPosY(parkedY, slideDuration * 0.8f).SetEase(Ease.InCubic).SetUpdate(true)
                .OnComplete(() => { if (!shown && root != null) root.gameObject.SetActive(false); });
        }

    }
}
