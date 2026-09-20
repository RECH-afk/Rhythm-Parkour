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
        [Tooltip("Корень панели из сцены")]
        public RectTransform root;

        [Header("Track Info")]
        [Tooltip("Строка «сейчас играет трек»")]
        public TextMeshProUGUI trackText;
        [Tooltip("Строка времени «текущее / длина»")]
        public TextMeshProUGUI timeText;

        [Header("Play Control")]
        public Button playButton;
        public Image playIcon;
        public TextMeshProUGUI playLabel;
        public Sprite playSprite;
        public Sprite pauseSprite;

        [Header("Repeat Control")]
        public Button repeatButton;
        public Image repeatIcon;
        [Tooltip("Цвет иконки повтора когда включён")]
        public Color repeatOnColor = Color.white;
        [Tooltip("Цвет иконки повтора когда выключен")]
        public Color repeatOffColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Minimize Control")]
        public Button minimizeButton;
        public Image minimizeIcon;
        [Tooltip("Цвет иконки сворачивания когда панель свернута")]
        public Color minimizeOnColor = Color.white;
        [Tooltip("Цвет иконки сворачивания когда панель развёрнута")]
        public Color minimizeOffColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Motion")]
        [Tooltip("Масштаб панели в свёрнутом виде")]
        public float minimizedScale = 0.62f;
        [Tooltip("Длительность выезда сверху")]
        public float slideDuration = 0.4f;
        public float shownY = -12f;
        public float parkedY = 140f;

        private MenuController menu;
        private bool shown;
        private bool minimized;
        private string lastTrackKey = "";
        private bool lastPlaying = true;
        private bool lastRepeatState = true;
        private string lastTimeText = "";
        private float lastKnownTime = -1f;
        private Tween slideTween;
        private Tween minTween;

        [Inject]
        public void ConstructPanel(MenuController menuController)
        {
            if (menuController != null) menu = menuController;
        }

        protected override void OnReady()
        {
            if (menu == null) menu = FindFirstObjectByType<MenuController>();
            ResolveRefs();
            if (root == null)
            {
                Debug.LogWarning("[TopPanel] Root не назначен.", this);
                return;
            }
            playButton?.onClick.RemoveListener(OnPlayPressed);
            playButton?.onClick.AddListener(OnPlayPressed);
            repeatButton?.onClick.RemoveListener(OnRepeatPressed);
            repeatButton?.onClick.AddListener(OnRepeatPressed);
            minimizeButton?.onClick.RemoveListener(OnMinimizePressed);
            minimizeButton?.onClick.AddListener(OnMinimizePressed);
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, parkedY);
            root.gameObject.SetActive(false);
            RefreshPlayIcon();
            RefreshRepeatIcon();
            RefreshMinimizeIcon();
        }

        protected override void OnDisposed()
        {
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            if (minTween != null && minTween.IsActive()) minTween.Kill();
        }

        protected override void Update()
        {
            if (menu == null)
            {
                menu = FindFirstObjectByType<MenuController>();
                if (menu == null) return;
            }
            if (root == null) return;
            bool want = menu.HasPreview
                && (menu.IsPreviewPlaying || menu.PreviewPaused)
                && menu.IsMainMenuVisible
                && !menu.IsDetailsVisible;
            if (want && !shown) Show();
            else if (!want && shown) Hide();
            if (!shown) return;
            if (trackText != null)
            {
                string key = menu.CurrentPreviewTitle + "|" + menu.CurrentPreviewArtist;
                if (key != lastTrackKey)
                {
                    lastTrackKey = key;
                    trackText.text = $"сейчас играет трек: {menu.CurrentPreviewTitle} от {menu.CurrentPreviewArtist}";
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

        void ResolveRefs()
        {
            if (root == null)
            {
                var go = GameObject.Find("TopPanel");
                if (go != null) root = go.GetComponent<RectTransform>();
            }
            if (root == null) return;
            if (trackText == null) trackText = FindInRoot<TextMeshProUGUI>("TrackText");
            if (playButton == null) playButton = FindButton("PlayButton");
            if (repeatButton == null) repeatButton = FindButton("RepeatButton");
            if (minimizeButton == null) minimizeButton = FindButton("MinimizeButton");
            if (playIcon == null && playButton != null) playIcon = FindInButtons(playButton, "Icon");
            if (repeatIcon == null && repeatButton != null) repeatIcon = FindInButtons(repeatButton, "Icon");
            if (minimizeIcon == null && minimizeButton != null) minimizeIcon = FindInButtons(minimizeButton, "Icon");
            if (playLabel == null && playButton != null) playLabel = playButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (timeText == null) timeText = FindInRoot<TextMeshProUGUI>("TimeText");
        }

        T FindInRoot<T>(string name) where T : Component
        {
            var t = root.transform.Find(name);
            if (t != null)
            {
                var c = t.GetComponent<T>();
                if (c != null) return c;
            }
            return root.GetComponentInChildren<T>(true);
        }

        Button FindButton(string name)
        {
            var t = root.transform.Find(name);
            if (t != null)
            {
                var b = t.GetComponent<Button>();
                if (b != null) return b;
            }
            return null;
        }

        Image FindInButtons(Button btn, string childName)
        {
            if (btn == null) return null;
            var t = btn.transform.Find(childName);
            if (t != null) return t.GetComponent<Image>();
            return null;
        }

        void OnPlayPressed()
        {
            if (menu != null) menu.TogglePreviewPlayback();
        }

        void OnRepeatPressed()
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
            if (minimizeIcon == null) return;
            minimizeIcon.color = minimized ? minimizeOnColor : minimizeOffColor;
        }

        static string FormatTime(float seconds)
        {
            if (seconds < 0f) return "--:--";
            int total = Mathf.RoundToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }

        void OnMinimizePressed()
        {
            ToggleMinimize();
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
            root.gameObject.SetActive(true);
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            var ap = root.anchoredPosition;
            root.anchoredPosition = new Vector2(ap.x, parkedY);
            slideTween = root.DOAnchorPosY(shownY, slideDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        void Hide()
        {
            if (root == null || !shown) return;
            shown = false;
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            slideTween = root.DOAnchorPosY(parkedY, slideDuration * 0.8f).SetEase(Ease.InCubic).SetUpdate(true)
                .OnComplete(() => { if (!shown && root != null) root.gameObject.SetActive(false); });
        }

        void ToggleMinimize()
        {
            if (root == null) return;
            minimized = !minimized;
            if (minTween != null && minTween.IsActive()) minTween.Kill();
            float target = minimized ? minimizedScale : 1f;
            minTween = root.DOScale(Vector3.one * target, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
            RefreshMinimizeIcon();
        }
    }
}
