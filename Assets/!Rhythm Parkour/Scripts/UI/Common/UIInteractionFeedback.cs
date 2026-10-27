using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using Zenject;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;


namespace RKS.RhythmParkour
{
    [RequireComponent(typeof(RectTransform))]
    public class UIInteractionFeedback : RKSBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Sounds")]
        [SerializeField] private string onPointerEnterSoundName = "button_sfx";
        [SerializeField] private string onClickSoundName = "button_sfx";

        [Header("Scale")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float pressScale = 0.9f;
        [SerializeField] private float duration = 0.15f;

        [Header("Ease")]
        [SerializeField] private Ease hoverEase = Ease.OutBack;
        [SerializeField] private Ease pressEase = Ease.OutQuad;

        [Header("Edge Stability")]
        [SerializeField] private float edgeTolerance = 8f;

        private Vector3 startScale = Vector3.one;
        private Vector3 hoverTarget;
        private Vector3 pressTarget;
        private Tween scaleTween;
        private Button button;
        private Canvas canvas;
        private Camera canvasCamera;
        private RectTransform rect;
        private float halfW;
        private float halfH;
        private float enterLimitX;
        private float enterLimitY;
        private float leaveLimitX;
        private float leaveLimitY;
        private Vector3 lastMouse;
        private Vector3 lastPos;
        private bool hasSample;
        private bool hovered;
        private bool pressed;
        private float lastEnterAt = -999f;

        const float SoundDebounceSeconds = 0.1f;

        protected override void Awake()
        {
            base.Awake();
            button = GetComponent<Button>();
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasCamera = canvas.worldCamera;
            CacheStartScale();
        }

        protected override void OnInjected()
        {
            CacheStartScale();
        }

        private void CacheStartScale()
        {
            if (transform.localScale != Vector3.zero)
                startScale = transform.localScale;
            else if (startScale == Vector3.zero)
                startScale = Vector3.one;

            hoverTarget = startScale * hoverScale;
            pressTarget = startScale * pressScale;
            RefreshGeometry();
        }

        private void RefreshGeometry()
        {
            if (rect == null) return;
            Rect r = rect.rect;
            halfW = r.width * 0.5f;
            halfH = r.height * 0.5f;
            enterLimitX = halfW;
            enterLimitY = halfH;
            leaveLimitX = Mathf.Max(halfW * hoverScale, halfW + edgeTolerance);
            leaveLimitY = Mathf.Max(halfH * hoverScale, halfH + edgeTolerance);
        }

        private bool EffectsAllowed()
        {
            return button == null || button.interactable;
        }

        private void OnEnable()
        {
            hasSample = false;
            ResetState();
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            hovered = false;
            pressed = false;
            hasSample = false;
        }

        private void ResetState()
        {
            transform.localScale = startScale;
        }

        protected override void Update()
        {
            if (pressed) return;

            Vector3 mouse = Input.mousePosition;
            Vector3 pos = rect.position;
            if (hasSample && mouse == lastMouse && pos == lastPos) return;
            lastMouse = mouse;
            lastPos = pos;
            hasSample = true;

            Rect r = rect.rect;
            if (r.width != halfW * 2f || r.height != halfH * 2f) RefreshGeometry();

            if (!EffectsAllowed())
            {
                if (hovered)
                {
                    hovered = false;
                    ScaleTo(startScale, duration, Ease.OutQuad);
                }
                return;
            }

            float limitX = hovered ? leaveLimitX : enterLimitX;
            float limitY = hovered ? leaveLimitY : enterLimitY;
            bool over = IsPointerInside(mouse, limitX, limitY);
            if (over == hovered) return;
            hovered = over;

            if (hovered)
            {
                if (!string.IsNullOrEmpty(onPointerEnterSoundName) && Audio != null
                    && Time.unscaledTime - lastEnterAt >= SoundDebounceSeconds)
                    Audio.Play(onPointerEnterSoundName);
                lastEnterAt = Time.unscaledTime;
                ScaleTo(hoverTarget, duration, hoverEase);
            }
            else
            {
                ScaleTo(startScale, duration, Ease.OutQuad);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!EffectsAllowed()) return;
            pressed = true;
            hasSample = false;
            if (!string.IsNullOrEmpty(onClickSoundName) && Audio != null) Audio.Play(onClickSoundName);
            ScaleTo(pressTarget, duration * 0.8f, pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            hasSample = false;
            if (!EffectsAllowed()) return;

            if (IsPointerInside(eventData.position, enterLimitX, enterLimitY))
            {
                hovered = true;
                lastEnterAt = Time.unscaledTime;
                ScaleTo(hoverTarget, duration, hoverEase);
            }
            else
            {
                hovered = false;
                ScaleTo(startScale, duration, Ease.OutQuad);
            }
        }

        private bool IsPointerInside(Vector2 screenPos, float limitX, float limitY)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, canvasCamera, out Vector2 localPoint))
                return false;

            Vector3 scale = transform.localScale;
            float sx = scale.x != 0f && startScale.x != 0f ? scale.x / startScale.x : 1f;
            float sy = scale.y != 0f && startScale.y != 0f ? scale.y / startScale.y : 1f;
            localPoint.x *= sx;
            localPoint.y *= sy;

            return Mathf.Abs(localPoint.x) <= limitX && Mathf.Abs(localPoint.y) <= limitY;
        }

        private void ScaleTo(Vector3 target, float time, Ease ease)
        {
            scaleTween?.Kill();
            scaleTween = transform
                .DOScale(target, time)
                .SetEase(ease)
                .SetUpdate(true);
        }
    }
}
