using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private string onPointerEnterSoundName = "Bubble";
        [SerializeField] private string onClickSoundName = "TrickleClicker";

        [Header("Scale")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private float pressScale = 0.9f;
        [SerializeField] private float duration = 0.15f;

        [Header("Ease")]
        [SerializeField] private Ease hoverEase = Ease.OutBack;
        [SerializeField] private Ease pressEase = Ease.OutQuad;

        private Vector3 startScale;
        private Tween scaleTween;

        protected override void OnInjected()
        {
            startScale = transform.localScale;
            if (Audio != null)
            {

            }
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
            if (!string.IsNullOrEmpty(onPointerEnterSoundName) && Audio != null) Audio.Play(onPointerEnterSoundName);
            ScaleTo(startScale * hoverScale, duration, hoverEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ScaleTo(startScale, duration, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(onClickSoundName) && Audio != null) Audio.Play(onClickSoundName);
            ScaleTo(startScale * pressScale, duration * 0.8f, pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {

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
