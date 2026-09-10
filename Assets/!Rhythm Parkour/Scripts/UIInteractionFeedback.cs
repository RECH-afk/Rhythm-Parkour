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
    public class UIInteractionFeedback : RKSBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
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

        private Vector3 startScale = Vector3.one;
        private Tween scaleTween;
        private Button button;

        protected override void Awake()
        {
            base.Awake();
            button = GetComponent<Button>();
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
        }

        private bool EffectsAllowed()
        {
            return button == null || button.interactable;
        }

        private void OnEnable()
        {
            ResetState();
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
        }

        private void ResetState()
        {
            transform.localScale = startScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!EffectsAllowed()) return;
            if (!string.IsNullOrEmpty(onPointerEnterSoundName) && Audio != null) Audio.Play(onPointerEnterSoundName);
            ScaleTo(startScale * hoverScale, duration, hoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!EffectsAllowed()) return;
            ScaleTo(startScale, duration, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!EffectsAllowed()) return;
            if (!string.IsNullOrEmpty(onClickSoundName) && Audio != null) Audio.Play(onClickSoundName);
            ScaleTo(startScale * pressScale, duration * 0.8f, pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {

            if (!EffectsAllowed()) return;
            ScaleTo(startScale * hoverScale, duration, hoverEase);
        }

        private void ScaleTo(Vector3 target, float time, Ease ease)
        {
            scaleTween?.Kill();
            scaleTween = transform
                .DOScale(target, time)
                .SetEase(ease);
        }
    }
}
