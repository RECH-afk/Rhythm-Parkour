using TMPro;
using UnityEngine;
using DG.Tweening;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.UI
{
    public enum OptionStatusState
    {
        Idle,
        Busy,
        Success,
        Error,
        Info,
    }

    public class OptionStatusBar : RKSBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private LoadingSparkleIndicator loadingIndicator;

        [Header("Fade")]
        [SerializeField] private CanvasGroup indicatorGroup;
        [SerializeField] private float minLoadDuration = 1f;
        [SerializeField] private float fadeDuration = 0.35f;

        Sequence _revealSeq;
        bool _busy;
        float _busyStartedAt;
        string _lastMessage;
        OptionStatusState _lastState;
        bool _hasStatus;

        protected override void OnInjected()
        {
            EnsureIndicatorGroup();
        }

        protected override void OnReady()
        {
            EnsureIndicatorGroup();
            if (!_hasStatus) HideImmediate();
        }

        protected override void Update()
        {
            if (_busy) return;
            if (_revealSeq != null && _revealSeq.IsActive()) return;
            if (statusText == null) return;

            bool visible = _lastState == OptionStatusState.Info
                || _lastState == OptionStatusState.Success
                || _lastState == OptionStatusState.Error;
            float target = visible ? 1f : 0f;
            if (!Mathf.Approximately(statusText.color.a, target))
                SetTextAlpha(target);
        }

        protected override void OnDisposed()
        {
            KillReveal();
            if (loadingIndicator != null) loadingIndicator.Stop();
        }

        void OnEnable()
        {
            if (_hasStatus)
            {
                SetStatus(_lastMessage, _lastState);
                return;
            }
            HideImmediate();
        }

        public void SetStatus(string message, OptionStatusState state = OptionStatusState.Idle)
        {
            if (statusText == null) return;

            _lastMessage = message ?? string.Empty;
            _lastState = state;
            _hasStatus = true;

            KillReveal();

            statusText.text = message ?? string.Empty;

            EnsureIndicatorGroup();

            switch (state)
            {
                case OptionStatusState.Busy:
                    _busy = true;
                    _busyStartedAt = Time.unscaledTime;
                    SetTextAlpha(0f);
                    if (indicatorGroup != null) indicatorGroup.alpha = 1f;
                    if (loadingIndicator != null) loadingIndicator.Play();
                    break;

                case OptionStatusState.Success:
                case OptionStatusState.Error:
                    PlayReveal();
                    break;

                case OptionStatusState.Info:
                    if (loadingIndicator != null) loadingIndicator.Stop();
                    if (indicatorGroup != null) indicatorGroup.alpha = 0f;
                    SetTextAlpha(1f);
                    _busy = false;
                    break;

                default:
                    HideImmediate();
                    break;
            }
        }

        void PlayReveal()
        {
            if (indicatorGroup == null && statusText == null) return;

            float wait = 0f;
            if (_busy)
            {
                wait = Mathf.Max(0f, minLoadDuration - (Time.unscaledTime - _busyStartedAt));
                _busy = false;
            }

            _revealSeq = DOTween.Sequence().SetUpdate(true);
            _revealSeq.AppendInterval(wait);

            bool hasIndicatorFade = false;
            if (indicatorGroup != null)
            {
                _revealSeq.Append(indicatorGroup.DOFade(0f, fadeDuration).SetEase(Ease.InOutQuad));
                hasIndicatorFade = true;
            }

            if (statusText != null)
            {
                Tween textFade = DOTween.To(GetTextAlpha, SetTextAlpha, 1f, fadeDuration).SetEase(Ease.InOutQuad);
                if (hasIndicatorFade) _revealSeq.Join(textFade);
                else _revealSeq.Append(textFade);
            }

            _revealSeq.AppendCallback(() =>
            {
                if (loadingIndicator != null) loadingIndicator.Stop();
            });
        }

        void HideImmediate()
        {
            KillReveal();
            _busy = false;
            SetTextAlpha(0f);
            if (indicatorGroup != null) indicatorGroup.alpha = 0f;
            if (loadingIndicator != null) loadingIndicator.Stop();
        }

        void KillReveal()
        {
            if (_revealSeq != null && _revealSeq.IsActive()) _revealSeq.Kill();
            _revealSeq = null;
        }

        void EnsureIndicatorGroup()
        {
            if (loadingIndicator == null || indicatorGroup != null) return;
            indicatorGroup = loadingIndicator.GetComponent<CanvasGroup>();
            if (indicatorGroup == null) indicatorGroup = loadingIndicator.gameObject.AddComponent<CanvasGroup>();
        }

        float GetTextAlpha() => statusText != null ? statusText.color.a : 0f;

        void SetTextAlpha(float alpha)
        {
            if (statusText == null) return;
            Color c = statusText.color;
            c.a = alpha;
            statusText.color = c;
        }
    }
}
