using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Settings;
using TMPro;
using Zenject;

namespace RKS.RhythmParkour.UI
{
    public class VolumeMixerUI : RKSBehaviour
    {
        const float OpenDuration = 0.4f;
        const float CloseDuration = 0.42f;
        const float OpenPop = 0.06f;
        const float ContainerSwing = 6f;
        const float SideDrift = 0.12f;
        const float StaggerDelay = 0.07f;
        const float KnobPop = 0.3f;
        const float OpenSpin = 20f;
        const float TickDuration = 0.2f;
        const float TickPunch = 0.05f;
        const float TickSpin = 5f;
        const float HoverScale = 1.05f;
        const float HoverDuration = 0.15f;
        const float IdleWobbleAmplitude = 2.6f;
        const float IdleWobbleSpeed = 0.9f;
        const float IdleBreathScale = 0.03f;
        const float SaveDelay = 0.5f;
        const float MaxDisplayDuration = 0.35f;
        const float SpeedUpMax = 4f;
        const float AccelRamp = 0.16f;
        const float AccelResetDelay = 0.4f;
        const float SlideMargin = 32f;
        const int SortingOrder = 800;
        static readonly Color ClampFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
        static readonly List<Graphic> graphicsBuffer = new List<Graphic>(32);

        [Serializable]
        public class MixerChannel
        {
            public GameObject root;
            public Image fill;
            public TMP_Text percent;
            [NonSerialized] public float value;
            [NonSerialized] public float display;
            [NonSerialized] public Vector3 baseScale;
            [NonSerialized] public Quaternion baseRotation;
            [NonSerialized] public Vector3 percentBaseScale;
            [NonSerialized] public Color percentBaseColor;
        }

        [Header("Channels")]
        [SerializeField] MixerChannel music = new MixerChannel();
        [SerializeField] MixerChannel general = new MixerChannel();
        [SerializeField] MixerChannel sounds = new MixerChannel();

        [Header("Wheel")]
        [Range(1, 25)] public int stepPercent = 5;
        [Range(0.3f, 6f)] public float hideDelay = 1.6f;
        [SerializeField] RectTransform visualRoot;

        [HideInInspector]
        [InjectOptional] public SettingsService settingsService;

        CanvasGroup group;
        Canvas canvasComponent;
        bool shown;
        bool mixerEnabled = true;
        float lastWheelTime = -999f;
        Tween fadeTween;
        Tween popTween;
        Tween containerTween;
        Vector3 popBaseScale = Vector3.one;
        Quaternion popBaseRotation = Quaternion.identity;
        Vector2 basePos;
        MixerChannel hovered;
        readonly Dictionary<MixerChannel, Tween> channelScaleTweens = new Dictionary<MixerChannel, Tween>();
        readonly Dictionary<MixerChannel, Tween> channelRotateTweens = new Dictionary<MixerChannel, Tween>();
        readonly Dictionary<MixerChannel, Tween> channelDisplayTweens = new Dictionary<MixerChannel, Tween>();
        readonly Dictionary<MixerChannel, Tween> channelPercentTweens = new Dictionary<MixerChannel, Tween>();
        readonly Dictionary<MixerChannel, Tween> channelFlashTweens = new Dictionary<MixerChannel, Tween>();
        readonly Dictionary<MixerChannel, float> wobbleWeight = new Dictionary<MixerChannel, float>();
        int pointerCheckFrame = -1;
        bool overScrollCache;
        MixerChannel overChannelCache;
        float saveTimer;
        bool saveDirty;
        float accelLevel;
        float lastNotchDirection;
        float prevTickTime = -999f;
        readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);

        protected override void OnInjected()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
            shown = false;
            EnsurePopTarget();
            popBaseScale = visualRoot.localScale;
            popBaseRotation = visualRoot.localRotation;
            basePos = visualRoot.anchoredPosition;
            InitChannels();
        }

        protected override void OnReady()
        {
            EnforceOverlay();
            RebuildRaycastTargets();
            ReadCurrent();
            RefreshAll();
            if (canvasComponent != null) canvasComponent.enabled = false;
            if (settingsService != null)
            {
                settingsService.OnVolumeMixerChanged += OnMixerEnabledChanged;
                settingsService.OnChanged += OnSettingsChanged;
                mixerEnabled = settingsService.IsVolumeMixerEnabled();
            }
        }

        void EnsurePopTarget()
        {
            if (visualRoot != null) return;
            RectTransform self = transform as RectTransform;
            if (self == null) return;
            var ownCanvas = GetComponent<Canvas>();
            if (ownCanvas == null || !ownCanvas.isRootCanvas)
            {
                visualRoot = self;
                return;
            }

            var wrapperGo = new GameObject("MixerPopRoot", typeof(RectTransform));
            RectTransform wrapper = wrapperGo.transform as RectTransform;
            wrapper.SetParent(transform, false);
            wrapper.anchorMin = Vector2.zero;
            wrapper.anchorMax = Vector2.one;
            wrapper.offsetMin = Vector2.zero;
            wrapper.offsetMax = Vector2.zero;
            wrapper.localScale = Vector3.one;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == wrapper) continue;
                child.SetParent(wrapper, false);
            }
            visualRoot = wrapper;
        }

        void InitChannels()
        {
            foreach (var c in AllChannels())
            {
                if (c == null || c.root == null) continue;
                c.baseScale = c.root.transform.localScale;
                c.baseRotation = c.root.transform.localRotation;
                if (c.percent != null)
                {
                    c.percentBaseScale = c.percent.transform.localScale;
                    c.percentBaseColor = c.percent.color;
                }
            }
        }

        void OnSettingsChanged()
        {
            ReadCurrent();
            foreach (var c in AllChannels())
                TweenChannelDisplay(c, 0f, Ease.OutQuad);
        }

        void OnMixerEnabledChanged(bool enabled)
        {
            mixerEnabled = enabled;
            if (!enabled && shown)
            {
                lastWheelTime = -999f;
                UpdateVisibility();
            }
        }

        protected override void Update()
        {
            UpdateSave();
            if (!mixerEnabled)
            {
                if (shown)
                {
                    lastWheelTime = -999f;
                    UpdateVisibility();
                }
                return;
            }
            PollPointer();
            HandleWheel();
            UpdateVisibility();
            UpdateHoverVisual();
            UpdateIdleWobble();
        }

        protected override void OnDisposed()
        {
            if (settingsService != null)
            {
                settingsService.OnVolumeMixerChanged -= OnMixerEnabledChanged;
                settingsService.OnChanged -= OnSettingsChanged;
            }
            if (fadeTween != null && fadeTween.IsActive()) fadeTween.Kill();
            if (popTween != null && popTween.IsActive()) popTween.Kill();
            if (containerTween != null && containerTween.IsActive()) containerTween.Kill();
            wobbleWeight.Clear();
            KillAll(channelScaleTweens);
            KillAll(channelRotateTweens);
            KillAll(channelDisplayTweens);
            KillAll(channelPercentTweens);
            KillAll(channelFlashTweens);
            if (saveDirty) PersistAll();
        }

        IEnumerable<MixerChannel> AllChannels()
        {
            yield return music;
            yield return general;
            yield return sounds;
        }

        void EnforceOverlay()
        {
            canvasComponent = GetComponent<Canvas>();
            if (canvasComponent == null) canvasComponent = GetComponentInChildren<Canvas>(true);
            if (canvasComponent == null) return;
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.overrideSorting = true;
            if (canvasComponent.sortingOrder < SortingOrder) canvasComponent.sortingOrder = SortingOrder;
        }

        void RebuildRaycastTargets()
        {
            graphicsBuffer.Clear();
            GetComponentsInChildren(true, graphicsBuffer);
            foreach (var g in graphicsBuffer)
            {
                if (g == null) continue;
                Transform t = g.transform;
                g.raycastTarget = Inside(t, music.root) || Inside(t, sounds.root) || Inside(t, general.root);
            }
        }

        void ReadCurrent()
        {
            if (Audio == null) return;
            music.value = Audio.GetMusicVolume();
            sounds.value = Audio.GetSoundsVolume();
            general.value = Audio.GetVolume();
        }

        void PollPointer()
        {
            if (pointerCheckFrame == Time.frameCount) return;
            pointerCheckFrame = Time.frameCount;
            overScrollCache = PointerOverScroll();
            overChannelCache = overScrollCache ? null : GetHoveredChannel();
        }

        void HandleWheel()
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.001f) return;
            if (overScrollCache) return;

            MixerChannel target = GetTargetChannel();
            lastWheelTime = Time.unscaledTime;

            float now = Time.unscaledTime;
            float direction = Mathf.Sign(wheel);
            if (direction != lastNotchDirection || now - prevTickTime > AccelResetDelay)
            {
                accelLevel = 0f;
                lastNotchDirection = direction;
            }
            else
            {
                accelLevel = Mathf.Min(1f, accelLevel + AccelRamp + AccelRamp * 0.5f * (1f - accelLevel));
            }
            prevTickTime = now;

            float step = Mathf.Max(1, stepPercent) / 100f * (1f + accelLevel * (SpeedUpMax - 1f));
            SetChannelValue(target, target.value + direction * step);
        }

        MixerChannel GetTargetChannel()
        {
            if (Input.GetMouseButton(1)) return sounds;
            if (Input.GetMouseButton(0)) return music;
            if (overChannelCache != null) return overChannelCache;
            return general;
        }

        bool PointerOverScroll()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            PointerEventData ped = new PointerEventData(es) { position = Input.mousePosition };
            raycastResults.Clear();
            es.RaycastAll(ped, raycastResults);
            foreach (var r in raycastResults)
                if (r.gameObject != null && r.gameObject.GetComponentInParent<ScrollRect>() != null)
                    return true;
            return false;
        }

        MixerChannel GetHoveredChannel()
        {
            Vector2 mp = Input.mousePosition;
            if (PointIn(music.root, mp)) return music;
            if (PointIn(sounds.root, mp)) return sounds;
            if (PointIn(general.root, mp)) return general;
            return null;
        }

        static bool PointIn(GameObject root, Vector2 screenPos)
        {
            if (root == null || !root.activeInHierarchy) return false;
            RectTransform rt = root.transform as RectTransform;
            if (rt == null) return false;
            Canvas c = rt.GetComponentInParent<Canvas>();
            Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
        }

        static bool Inside(Transform t, GameObject root)
        {
            if (root == null) return false;
            while (t != null)
            {
                if (t.gameObject == root) return true;
                t = t.parent;
            }
            return false;
        }

        void SetChannelValue(MixerChannel c, float v)
        {
            float clamped = Mathf.Clamp01(v);
            float delta = clamped - c.value;
            c.value = clamped;

            if (settingsService != null)
            {
                if (c == music) settingsService.SetMusicVolume(c.value, false);
                else if (c == sounds) settingsService.SetSoundsVolume(c.value, false);
                else settingsService.SetSoundVolume(c.value, false);
            }
            else if (Audio != null)
            {
                if (c == music) Audio.SetMusicVolume(c.value);
                else if (c == sounds) Audio.SetSoundsVolume(c.value);
                else Audio.SetVolume(c.value);
            }

            TweenChannelDisplay(c, 0f, Ease.OutQuad);

            if (Mathf.Abs(delta) > 0.0001f)
            {
                PunchChannel(c);
                TickRotation(c, Mathf.Sign(delta));
                PunchPercent(c);
            }
            if (c.value <= 0.001f || c.value >= 0.999f) FlashClamped(c);

            saveDirty = true;
            saveTimer = Mathf.Max(0.1f, SaveDelay);
        }

        void TweenChannelDisplay(MixerChannel c, float duration, Ease ease, Action onComplete = null)
        {
            if (c == null || c.root == null)
            {
                onComplete?.Invoke();
                return;
            }
            KillTween(channelDisplayTweens, c);
            float delta = Mathf.Abs(c.value - c.display);
            if (delta < 0.0005f)
            {
                c.display = c.value;
                ApplyDisplay(c);
                onComplete?.Invoke();
                return;
            }
            float dur = duration > 0f ? duration : Mathf.Clamp(delta * 0.8f, 0.1f, MaxDisplayDuration);
            Tween tween = DOTween.To(
                    () => c.display,
                    x => { c.display = x; ApplyDisplay(c); },
                    c.value, dur)
                .SetUpdate(true).SetEase(ease);
            if (onComplete != null) tween.OnComplete(() => onComplete());
            channelDisplayTweens[c] = tween;
        }

        void ApplyDisplay(MixerChannel c)
        {
            if (c.fill != null) c.fill.fillAmount = c.display;
            if (c.percent != null) c.percent.text = $"{Mathf.RoundToInt(c.display * 100f)}%";
        }

        void PunchChannel(MixerChannel c)
        {
            if (c.root == null || TickPunch < 0.001f) return;
            float mul = hovered == c ? HoverScale : 1f;
            KillTween(channelScaleTweens, c);
            Transform t = c.root.transform;
            t.localScale = c.baseScale * mul;
            channelScaleTweens[c] = t.DOScale(c.baseScale * (mul + TickPunch), TickDuration * 0.35f)
                .SetUpdate(true).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (t != null)
                        channelScaleTweens[c] = t.DOScale(c.baseScale * mul, TickDuration * 0.65f)
                            .SetUpdate(true).SetEase(Ease.OutQuad);
                });
        }

        void TickRotation(MixerChannel c, float dir)
        {
            if (c.root == null || TickSpin < 0.001f) return;
            KillTween(channelRotateTweens, c);
            c.root.transform.localRotation = c.baseRotation * Quaternion.Euler(0f, 0f, -dir * TickSpin);
            channelRotateTweens[c] = c.root.transform.DOLocalRotateQuaternion(c.baseRotation, TickDuration)
                .SetUpdate(true).SetEase(Ease.OutCubic);
        }

        void PunchPercent(MixerChannel c)
        {
            if (c.percent == null || TickPunch < 0.001f) return;
            KillTween(channelPercentTweens, c);
            Transform t = c.percent.transform;
            t.localScale = c.percentBaseScale;
            channelPercentTweens[c] = t.DOScale(c.percentBaseScale * 1.12f, TickDuration * 0.35f)
                .SetUpdate(true).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (t != null)
                        channelPercentTweens[c] = t.DOScale(c.percentBaseScale, TickDuration * 0.65f)
                            .SetUpdate(true).SetEase(Ease.OutQuad);
                });
        }

        void FlashClamped(MixerChannel c)
        {
            if (c.percent == null) return;
            if (TweenActive(channelFlashTweens, c)) return;
            c.percent.color = c.percentBaseColor;
            channelFlashTweens[c] = DOTween.To(
                    () => c.percent.color,
                    x => { if (c.percent != null) c.percent.color = x; },
                    ClampFlashColor, TickDuration * 0.5f)
                .SetTarget(c.percent)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true);
        }

        void RefreshAll()
        {
            foreach (var c in AllChannels())
            {
                KillTween(channelDisplayTweens, c);
                c.display = c.value;
                ApplyDisplay(c);
            }
        }

        void UpdateVisibility()
        {
            bool show = Time.unscaledTime - lastWheelTime < Mathf.Max(0.3f, hideDelay);
            if (show == shown) return;
            shown = show;
            Animate(show);
        }

        void Animate(bool show)
        {
            if (fadeTween != null && fadeTween.IsActive()) fadeTween.Kill();
            if (popTween != null && popTween.IsActive()) popTween.Kill();
            if (containerTween != null && containerTween.IsActive()) containerTween.Kill();
            fadeTween = null;
            popTween = null;
            containerTween = null;
            KillAll(channelScaleTweens);
            KillAll(channelRotateTweens);
            KillAll(channelDisplayTweens);
            KillAll(channelPercentTweens);
            KillAll(channelFlashTweens);
            if (group == null) return;

            group.interactable = false;
            if (show)
            {
                group.blocksRaycasts = true;
                if (canvasComponent != null) canvasComponent.enabled = true;
                visualRoot.anchoredPosition = ComputeOffPos(true);
                visualRoot.localRotation = popBaseRotation * Quaternion.Euler(0f, 0f, ContainerSwing);
                visualRoot.localScale = popBaseScale * (1f - OpenPop);
                fadeTween = visualRoot.DOAnchorPos(basePos, OpenDuration).SetUpdate(true).SetEase(Ease.OutBack, 0.5f);
                popTween = visualRoot.DOScale(popBaseScale, OpenDuration).SetUpdate(true).SetEase(Ease.OutBack, 0.5f);
                containerTween = visualRoot.DOLocalRotateQuaternion(popBaseRotation, OpenDuration * 1.15f)
                    .SetUpdate(true).SetEase(Ease.OutBack, 0.5f);
                AnimateChannelOpen(general, 0.06f, -OpenSpin * 0.4f);
                AnimateChannelOpen(music, 0.06f + StaggerDelay * 1.5f, -OpenSpin);
                AnimateChannelOpen(sounds, 0.06f + StaggerDelay * 1.5f, OpenSpin);
            }
            else
            {
                group.blocksRaycasts = false;
                fadeTween = visualRoot.DOAnchorPos(ComputeOffPos(false), CloseDuration).SetUpdate(true).SetEase(Ease.InCubic)
                    .OnComplete(() =>
                    {
                        if (canvasComponent != null && !shown) canvasComponent.enabled = false;
                    });
                containerTween = visualRoot.DOLocalRotateQuaternion(
                        popBaseRotation * Quaternion.Euler(0f, 0f, -ContainerSwing * 0.9f), CloseDuration)
                    .SetUpdate(true).SetEase(Ease.InCubic);
                AnimateChannelClose(music, 0f, -OpenSpin * 0.7f);
                AnimateChannelClose(sounds, 0f, OpenSpin * 0.7f);
                AnimateChannelClose(general, StaggerDelay * 1.2f, OpenSpin * 0.3f);
            }
        }

        Vector2 ComputeOffPos(bool entering)
        {
            Canvas rootCanvas = canvasComponent != null ? canvasComponent.rootCanvas : null;
            float scale = rootCanvas != null && rootCanvas.scaleFactor > 0.0001f ? rootCanvas.scaleFactor : 1f;
            float heightUnits = visualRoot.rect.height * Mathf.Max(0.0001f, visualRoot.localScale.y);
            float travel = Screen.height / scale + heightUnits + SlideMargin / scale;
            float drift = Screen.width * SideDrift / scale;
            Vector2 off = basePos + Vector2.down * travel;
            off.x += entering ? -drift : drift;
            return off;
        }

        void AnimateChannelOpen(MixerChannel c, float delay, float startSpin)
        {
            if (c == null || c.root == null) return;
            float dur = OpenDuration * 0.95f;

            KillTween(channelPercentTweens, c);
            KillTween(channelFlashTweens, c);
            if (c.percent != null) c.percent.color = c.percentBaseColor;

            float pop = c == general ? KnobPop * 1.15f : KnobPop;
            Vector3 stretch = new Vector3(1f - pop, 1f - pop * 0.55f, 1f);
            c.root.transform.localScale = Vector3.Scale(c.baseScale, stretch);
            channelScaleTweens[c] = c.root.transform.DOScale(c.baseScale, dur)
                .SetDelay(delay).SetUpdate(true).SetEase(Ease.OutBack, 0.6f);

            c.root.transform.localRotation = c.baseRotation * Quaternion.Euler(0f, 0f, startSpin);
            channelRotateTweens[c] = c.root.transform.DOLocalRotateQuaternion(c.baseRotation, dur)
                .SetDelay(delay).SetUpdate(true).SetEase(Ease.OutBack, 0.6f);

            c.display = 0f;
            ApplyDisplay(c);
            TweenChannelDisplay(c, dur * 1.1f, Ease.OutCubic, () => PunchPercent(c));
        }

        void AnimateChannelClose(MixerChannel c, float delay, float endSpin)
        {
            if (c == null || c.root == null) return;
            float dur = CloseDuration * 0.9f;

            float pop = c == general ? KnobPop * 1.15f : KnobPop;
            Vector3 squash = new Vector3(1f + pop * 0.35f, 1f - pop * 0.55f, 1f);
            channelScaleTweens[c] = c.root.transform.DOScale(Vector3.Scale(c.baseScale, squash), dur)
                .SetDelay(delay).SetUpdate(true).SetEase(Ease.InBack);

            channelRotateTweens[c] = c.root.transform.DOLocalRotateQuaternion(
                    c.baseRotation * Quaternion.Euler(0f, 0f, endSpin), dur)
                .SetDelay(delay).SetUpdate(true).SetEase(Ease.InCubic);
        }

        void UpdateHoverVisual()
        {
            MixerChannel h = null;
            if (!overScrollCache)
            {
                if (Input.GetMouseButton(1)) h = sounds;
                else if (Input.GetMouseButton(0)) h = music;
                else h = overChannelCache;
            }
            if (h == hovered) return;
            MixerChannel prev = hovered;
            hovered = h;
            if (prev != null) TweenScale(prev, 1f);
            if (hovered != null) TweenScale(hovered, HoverScale);
        }

        void TweenScale(MixerChannel c, float multiplier)
        {
            if (c == null || c.root == null) return;
            KillTween(channelScaleTweens, c);
            channelScaleTweens[c] = c.root.transform.DOScale(c.baseScale * multiplier, HoverDuration)
                .SetUpdate(true).SetEase(Ease.OutQuad);
        }

        void UpdateIdleWobble()
        {
            if (!shown || IdleWobbleAmplitude < 0.001f) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime * IdleWobbleSpeed;
            int index = 0;
            foreach (var c in AllChannels())
            {
                if (c != null && c.root != null)
                {
                    bool rotating = TweenActive(channelRotateTweens, c);
                    bool scaling = TweenActive(channelScaleTweens, c);
                    wobbleWeight.TryGetValue(c, out float w);
                    float targetW = rotating || scaling || c == hovered ? 0f : 1f;
                    w = Mathf.Lerp(w, targetW, Mathf.Min(1f, dt * 2.2f));
                    wobbleWeight[c] = w;

                    float phase = index * 2.4f;
                    if (!rotating && w > 0.001f)
                    {
                        float wob = Mathf.Sin(t * 0.9f + phase) * 0.7f + Mathf.Sin(t * 1.7f + phase * 1.6f) * 0.3f;
                        c.root.transform.localRotation = c.baseRotation * Quaternion.Euler(0f, 0f, wob * IdleWobbleAmplitude * w);
                    }

                    if (!rotating && !scaling && c != hovered && w > 0.001f)
                    {
                        float breath = Mathf.Sin(t * 0.6f + phase * 1.7f) * 0.65f + Mathf.Sin(t * 1.15f + phase * 0.7f) * 0.35f;
                        c.root.transform.localScale = c.baseScale * (1f + breath * IdleBreathScale * w);
                    }
                }
                index++;
            }
        }

        void UpdateSave()
        {
            if (!saveDirty) return;
            saveTimer -= Time.unscaledDeltaTime;
            if (saveTimer > 0f) return;
            PersistAll();
        }

        void PersistAll()
        {
            saveDirty = false;
            if (settingsService == null) return;
            settingsService.Write();
        }

        static void KillTween(Dictionary<MixerChannel, Tween> map, MixerChannel c)
        {
            if (c == null || !map.TryGetValue(c, out var t)) return;
            if (t != null && t.IsActive()) t.Kill();
        }

        static bool TweenActive(Dictionary<MixerChannel, Tween> map, MixerChannel c)
        {
            return map.TryGetValue(c, out var t) && t != null && t.IsActive();
        }

        static void KillAll(Dictionary<MixerChannel, Tween> map)
        {
            foreach (var kv in map)
                if (kv.Value != null && kv.Value.IsActive()) kv.Value.Kill();
            map.Clear();
        }
    }
}
