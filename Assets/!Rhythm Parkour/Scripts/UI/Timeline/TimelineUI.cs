using System.Collections.Generic;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;

namespace RKS.RhythmParkour.UI.Timeline
{
    public interface ITimelineNoteView { void Setup(int index, ObstacleEvent ev, float hitBeat, bool selected); }

    public class TimelineUI : RKSBehaviour
    {
        [HideInInspector] public RhythmLevelData levelData;
        public AudioSource audioSource;
        [HideInInspector]
        [InjectOptional] public RhythmParkourManager manager;
        [HideInInspector]
        [InjectOptional] public FileLoader fileLoader;
        [HideInInspector]
        [InjectOptional] public LevelTransfer transfer;
        [HideInInspector]
        [InjectOptional] public GlobalObstacleCatalog catalog;
        [HideInInspector]
        [InjectOptional] public Canvas rootCanvas;
        public ScrollRect timelineScrollRect;
        public Scrollbar timelineScrollbar;
        public RectTransform timelineContent;
        public RectTransform timelineViewport;
        public RawImage waveformImage;
        public RectTransform waveformRect;
        public RectTransform notesContainer;
        public RectTransform gridContainer;
        public RectTransform playheadRect;
        public RectTransform playheadHeader;

        public GameObject warningObject;
        public GameObject timelineObject;
        public TextMeshProUGUI playPauseLabel;
        public TextMeshProUGUI timeLabel;
        public TextMeshProUGUI statusLabel;
        [HideInInspector] public TextMeshProUGUI beatLabel;
        public GameObject notePrefab;
        public Vector2 noteSize = new Vector2(36f, 48f);
        [Tooltip("Если true — ширина нот масштабируется вместе с зумом таймлайна")]
        public bool scaleNotesWithZoom = true;
        [Tooltip("Только ширина растягивается, высота почти не меняется")]
        public bool scaleNotesWidthOnly = true;
        [Range(0.5f, 2f)] public float noteZoomScaleMin = 0.7f;
        [Range(1f, 4f)] public float noteZoomScaleMax = 3f;
        public bool notePrefabUseCustomView = true;
        public float quantStep = 0.5f;
        public bool autoQuantize = true;
        public bool snapToGrid = true;
        public KeyCode freeMoveKey = KeyCode.LeftControl;
        public KeyCode freeMoveKeyAlt = KeyCode.LeftAlt;
        public int brushIndex = 0;
        public float defaultNoteSpeed = 12f;
        public bool loopPlayback = false;
        public KeyCode addNoteKey = KeyCode.Return;
        public KeyCode altAddNoteKey = KeyCode.KeypadEnter;
        public float noteDeleteThresholdBeats = 0.35f;
        public Color waveformWaveColor = new Color(0.3f, 0.7f, 1f, 1f);
        public Color waveformBgColor = new Color(0.13f, 0.13f, 0.15f, 1f);
        public Color playheadColor = new Color(0f, 1f, 0.5f, 0.95f);
        public Color gridBarColor = new Color(1f, 1f, 1f, 0.38f);
        public Color gridBeatColor = new Color(1f, 1f, 1f, 0.28f);
        public Color gridHalfColor = new Color(1f, 1f, 1f, 0.18f);
        [Header("Waveform")]
        public bool showWaveform = true;
        public int waveformTexWidth = 8192;
        public int waveformTexHeight = 140;

        [HideInInspector] public bool animateWaveform = false;
        [HideInInspector] public float waveformPulseAmount = 0f;
        public float pixelsPerSecond = 120f;
        public float zoom = 1f;
        public bool autoScrollWithPlayhead = true;
        public float autoScrollMargin = 0.25f;
        [Tooltip("Если true — при включении AutoScroll плейхед остаётся там где был (не прыгает к фиксированному якорю)")]
        public bool autoscrollKeepCurrentPosition = true;
        float autoscrollLockedRatio = 0.35f;
        bool autoscrollHasLockedRatio = false;
        [HideInInspector] public bool followSlider = true;
        public TextMeshProUGUI autoscrollToggleLabel;

        [Header("Preview 3D")]
        [HideInInspector]
        [InjectOptional] public TimelinePreview preview;
        public TextMeshProUGUI previewToggleLabel;

        [Header("Scrub Audio")]
        public bool enableScrubAudio = true;
        [Range(0f, 1f)] public float scrubVolume = 0.85f;
        public float scrubAudibleDuration = 0.35f;
        [Header("Zoom & Scroll Wheel")]
        public float wheelZoomStep = 0.15f;
        public float zoomLerpSpeed = 12f;
        public float wheelScrollSpeed = 0.12f;
        public bool zoomToCursor = true;
        public float followSmoothDuration = 0.45f;
        float targetZoom;
        float zoomVelocity;
        bool isZoomAnimating;
        float _pendingZoomCursorTime = -1f;
        Vector2 _pendingZoomCursorScreenPos;
        Coroutine followSmoothCoroutine;

        public GameObject notePropertiesPanel;
        public TMP_Dropdown propPrefabDropdown;
        public TextMeshProUGUI propTitleLabel;
        public TMP_InputField propSpeedInput;

        [Header("Сетка миниатюр (выбор вида)")]
        public GameObject prefabGridPanel;
        public Transform prefabGridContainer;
        public GameObject prefabThumbPrefab;
        public bool closeGridOnSelect = true;

        [Header("BPM вывод (после загрузки трека)")]
        public TextMeshProUGUI bpmOutputText;
        public bool showBpmAfterLoad = true;

        private TimelineNotesController notesController;
        private TimelineWaveformView waveformView;
        private TimelineGridView gridView;
        private TimelineTransport transport;
        private TimelinePropertiesController propsController;

        protected override void OnInjected()
        {
            if (audioSource == null)
            {
                var go = new GameObject("TimelinePreviewAudio");
                go.transform.SetParent(transform, false);
                audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f;
            }

            if (transfer != null && transfer.hasLevel && transfer.levelData != null)
            {
                string cur = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (cur == "IsLevelEditorScene" || cur == "LevelEditor" || cur == "MainMenu")
                {
                    levelData = transfer.levelData;
                    if (manager != null) manager.levelData = levelData;
                }
            }
            if (levelData == null && manager != null) levelData = manager.levelData;
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (notesController == null) notesController = GetComponent<TimelineNotesController>();
            if (notesController == null) notesController = gameObject.AddComponent<TimelineNotesController>();
            if (waveformView == null) waveformView = GetComponent<TimelineWaveformView>();
            if (waveformView == null) waveformView = gameObject.AddComponent<TimelineWaveformView>();
            if (gridView == null) gridView = GetComponent<TimelineGridView>();
            if (gridView == null) gridView = gameObject.AddComponent<TimelineGridView>();
            if (transport == null) transport = GetComponent<TimelineTransport>();
            if (transport == null) transport = gameObject.AddComponent<TimelineTransport>();
            if (propsController == null) propsController = GetComponent<TimelinePropertiesController>();
            if (propsController == null) propsController = gameObject.AddComponent<TimelinePropertiesController>();
        }

        [Inject]
        public void ConstructTimeline(
            TimelineNotesController notes,
            TimelineWaveformView wave,
            TimelineGridView grid,
            TimelineTransport transportComponent,
            TimelinePropertiesController props)
        {
            if (notes != null) notesController = notes;
            if (wave != null) waveformView = wave;
            if (grid != null) gridView = grid;
            if (transportComponent != null) transport = transportComponent;
            if (props != null) propsController = props;
        }

        void OptimizeTimelineLayout()
        {

            if (timelineContent != null)
            {
                var fitter = timelineContent.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
                var hlg = timelineContent.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null) hlg.enabled = false;
                var vlg = timelineContent.GetComponent<VerticalLayoutGroup>();
                if (vlg != null) vlg.enabled = false;
                var glg = timelineContent.GetComponent<GridLayoutGroup>();
                if (glg != null) glg.enabled = false;
            }
            if (gridContainer != null)
            {
                var fitter = gridContainer.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
            }
            if (notesContainer != null)
            {
                var fitter = notesContainer.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
            }

            if (rootCanvas != null) rootCanvas.pixelPerfect = false;
        }

        protected override void OnReady()
        {
            if (waveformRect == null && timelineContent != null) waveformRect = timelineContent;
            if (timelineViewport == null && timelineScrollRect != null) timelineViewport = timelineScrollRect.viewport;
            if (waveformRect == null && waveformImage != null) waveformRect = waveformImage.rectTransform;
            if (timelineScrollbar == null && timelineScrollRect != null) timelineScrollbar = timelineScrollRect.horizontalScrollbar;
            OptimizeTimelineLayout();
            EnsureComponents();

            BindEvents();
            if (fileLoader != null) fileLoader.onFileLoaded.AddListener(OnFileLoaded);
            if (notePropertiesPanel != null) notePropertiesPanel.SetActive(false);
            if (!followSlider && autoScrollWithPlayhead) autoScrollWithPlayhead = false;
            if (followSlider && !autoScrollWithPlayhead) followSlider = false; else followSlider = autoScrollWithPlayhead;

            RefreshAll();
            transport.SetTime(0f);
            UpdateScrollToPlayhead(true);
            UpdateAutoscrollToggleVisual();
            UpdatePreviewToggleVisual();
            targetZoom = zoom;

            if (transfer != null && transfer.hasLevel && transfer.levelData != null && levelData != transfer.levelData)
            {
                levelData = transfer.levelData;
                if (manager != null) manager.levelData = levelData;
                RefreshAll();
            }

            if (GetComponent<TimingsPanel>() == null) gameObject.AddComponent<TimingsPanel>();
        }

        protected override void OnDisposed()
        {
            if (fileLoader != null) fileLoader.onFileLoaded.RemoveListener(OnFileLoaded);
            if (followSmoothCoroutine != null) StopCoroutine(followSmoothCoroutine);
        }

        void OnValidate()
        {
            quantStep = Mathf.Clamp(quantStep, 0.1f, 4f);
            brushIndex = Mathf.Max(0, brushIndex);
            waveformTexWidth = Mathf.Clamp(waveformTexWidth, 512, 16384);
            waveformTexHeight = Mathf.Clamp(waveformTexHeight, 32, 360);
            pixelsPerSecond = Mathf.Clamp(pixelsPerSecond, 10f, 1000f);
            zoom = Mathf.Clamp(zoom, 0.25f, 4f);
            targetZoom = Mathf.Clamp(targetZoom, 0.25f, 4f);
            wheelZoomStep = Mathf.Clamp(wheelZoomStep, 0.01f, 0.5f);
            zoomLerpSpeed = Mathf.Clamp(zoomLerpSpeed, 1f, 30f);
            autoScrollMargin = Mathf.Clamp01(autoScrollMargin);
            scrubVolume = Mathf.Clamp01(scrubVolume);
            scrubAudibleDuration = Mathf.Clamp(scrubAudibleDuration, 0f, 2f);
#if UNITY_EDITOR
            if (timelineContent != null && Application.isPlaying) RefreshScrollContent();
#endif
        }

        public void SetZoom(float z)
        {
            float nz = Mathf.Clamp(z, 0.25f, 4f);
            if (Mathf.Abs(nz - zoom) < 0.001f && Mathf.Abs(nz - targetZoom) < 0.001f) return;
            targetZoom = nz;
            zoom = nz;
            isZoomAnimating = false;
            RefreshScrollContent();

            gridView.RefreshGrid(false);
            notesController.ApplyNoteZoomScale();
            UpdateScrollToPlayhead(true);
            if (waveformRect != null) waveformRect.localScale = Vector3.one;
        }

        void SetZoomAnimated(float z, Vector2 screenPos)
        {
            float nz = Mathf.Clamp(z, 0.25f, 4f);
            if (Mathf.Abs(nz - targetZoom) < 0.001f) return;
            float timeUnderCursor = -1f;
            bool hasCursor = zoomToCursor && IsMouseOverTimeline(screenPos);
            if (hasCursor) timeUnderCursor = GetTimeFromMouse(screenPos);
            targetZoom = nz;
            isZoomAnimating = true;
            if (hasCursor && timeUnderCursor >= 0f) { _pendingZoomCursorTime = timeUnderCursor; _pendingZoomCursorScreenPos = screenPos; }
            else _pendingZoomCursorTime = -1f;
        }

        bool IsMouseOverTimeline(Vector2 screenPos)
        {
            Camera cam = GetCanvasCamera();
            if (timelineViewport != null && RectTransformUtility.RectangleContainsScreenPoint(timelineViewport, screenPos, cam)) return true;
            if (timelineContent != null && RectTransformUtility.RectangleContainsScreenPoint(timelineContent, screenPos, cam)) return true;
            if (waveformRect != null && RectTransformUtility.RectangleContainsScreenPoint(waveformRect, screenPos, cam)) return true;
            return false;
        }

        public void SetPixelsPerSecond(float pps) { float np = Mathf.Clamp(pps, 10f, 1000f); if (Mathf.Abs(np - pixelsPerSecond) < 0.1f) return; pixelsPerSecond = np; RefreshScrollContent(); if (Mathf.Abs(pps - waveformView.LastPps) > 5f) waveformView.RefreshWaveform(true); }

        void BindEvents()
        {
            propsController.BindPropertiesPanel();
            if (playheadHeader != null)
            {
                var et = playheadHeader.gameObject.GetComponent<EventTrigger>();
                if (et == null) et = playheadHeader.gameObject.AddComponent<EventTrigger>();
                et.triggers.Clear();
                EnsureEvent(et, EventTriggerType.PointerDown, transport.OnHeaderPointerDown);
                EnsureEvent(et, EventTriggerType.Drag, transport.OnHeaderDrag);
                EnsureEvent(et, EventTriggerType.PointerUp, data => transport.EndScrub());
                EnsureEvent(et, EventTriggerType.BeginDrag, transport.OnHeaderPointerDown);
                EnsureEvent(et, EventTriggerType.EndDrag, data => transport.EndScrub());
            }
        }

        void EnsureEvent(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> cb)
        {
            var entry = trigger.triggers.Find(e => e.eventID == type);
            if (entry == null) { entry = new EventTrigger.Entry { eventID = type }; trigger.triggers.Add(entry); }
            entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(cb));
        }

        public void ToggleAutoscroll()
        {
            autoScrollWithPlayhead = !autoScrollWithPlayhead;
            followSlider = autoScrollWithPlayhead;
            UpdateAutoscrollToggleVisual();
            if (autoScrollWithPlayhead)
            {

                if (autoscrollKeepCurrentPosition && timelineScrollRect != null && timelineContent != null && GetClipLength() > 0.01f)
                {
                    RectTransform viewport = timelineViewport != null ? timelineViewport : timelineScrollRect.viewport;
                    if (viewport == null) viewport = timelineScrollRect.GetComponent<RectTransform>();
                    float viewportW = viewport.rect.width;
                    float contentW = timelineContent.rect.width;
                    if (contentW < 1f) contentW = timelineContent.sizeDelta.x;
                    float norm = Mathf.Clamp01(transport.GetCurrentTime() / GetClipLength());
                    float playheadX = norm * contentW;
                    float offset = -timelineContent.anchoredPosition.x;
                    float inView = playheadX - offset;
                    if (viewportW > 1f)
                    {
                        autoscrollLockedRatio = Mathf.Clamp01(inView / viewportW);

                        if (inView < -50f || inView > viewportW + 50f) autoscrollLockedRatio = 0.5f;
                    }
                    else autoscrollLockedRatio = 0.5f;
                    autoscrollHasLockedRatio = true;
                }
                else { autoscrollLockedRatio = 0.35f; autoscrollHasLockedRatio = false; }

                if (followSmoothCoroutine != null) StopCoroutine(followSmoothCoroutine);

                RectTransform vp2 = timelineViewport != null ? timelineViewport : timelineScrollRect.viewport;
                if (vp2 == null) vp2 = timelineScrollRect.GetComponent<RectTransform>();
                float vpW2 = vp2.rect.width;
                float cW2 = timelineContent.rect.width; if (cW2 < 1f) cW2 = timelineContent.sizeDelta.x;
                if (cW2 > vpW2 + 1f)
                {
                    float n2 = Mathf.Clamp01(transport.GetCurrentTime() / GetClipLength());
                    float px2 = n2 * cW2;
                    float off2 = -timelineContent.anchoredPosition.x;
                    float inv2 = px2 - off2;
                    if (inv2 < 0 || inv2 > vpW2)
                        followSmoothCoroutine = StartCoroutine(SmoothFollowToPlayheadCoroutine());
                }
            }
            else
            {
                if (followSmoothCoroutine != null) { StopCoroutine(followSmoothCoroutine); followSmoothCoroutine = null; }
                autoscrollHasLockedRatio = false;
            }
        }

        public void ToggleFollow() => ToggleAutoscroll();
        void UpdateAutoscrollToggleVisual()
        {
            if (autoscrollToggleLabel != null) autoscrollToggleLabel.text = autoScrollWithPlayhead ? "Autoscroll ON" : "Autoscroll OFF";
        }
        void UpdateFollowToggleVisual() => UpdateAutoscrollToggleVisual();

        public void TogglePreview()
        {

            if (preview == null) { Debug.LogWarning("[Timeline] Preview не найден", this); return; }
            preview.TogglePreview();
            UpdatePreviewToggleVisual();
        }
        void UpdatePreviewToggleVisual()
        {
            bool on = preview != null && preview.previewEnabled;
            if (previewToggleLabel != null) previewToggleLabel.text = on ? "Preview ON" : "Preview OFF";
        }
        public void UpdateAutoscrollLockFromScreenPos(Vector2 screenPos)
        {
            if (!autoScrollWithPlayhead || !autoscrollKeepCurrentPosition) return;
            RectTransform viewport = timelineViewport != null ? timelineViewport : timelineScrollRect?.viewport;
            if (viewport == null) viewport = timelineScrollRect?.GetComponent<RectTransform>();
            if (viewport == null) return;
            Camera cam = GetCanvasCamera();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, cam, out local)) return;
            float viewportW = viewport.rect.width;
            if (viewportW < 2f) return;
            float inView = local.x + viewportW * 0.5f;
            autoscrollLockedRatio = Mathf.Clamp01(inView / viewportW);
            autoscrollHasLockedRatio = true;
        }

        System.Collections.IEnumerator SmoothFollowToPlayheadCoroutine()
        {
            if (timelineScrollRect == null || timelineContent == null || GetClipLength() < 0.01f) yield break;
            RectTransform viewport = timelineViewport != null ? timelineViewport : timelineScrollRect.viewport;
            if (viewport == null) viewport = timelineScrollRect.GetComponent<RectTransform>();
            float viewportW = viewport.rect.width;
            float contentW = timelineContent.rect.width;
            if (contentW < 1f) contentW = timelineContent.sizeDelta.x;
            if (contentW <= viewportW + 1f) yield break;
            float norm = Mathf.Clamp01(transport.GetCurrentTime() / GetClipLength());
            float playheadX = norm * contentW;
            float offset = -timelineContent.anchoredPosition.x;
            float anchorRatio = (autoscrollKeepCurrentPosition && autoscrollHasLockedRatio) ? autoscrollLockedRatio : 0.35f;
            float anchor = viewportW * anchorRatio;
            float targetOffset = Mathf.Clamp(playheadX - anchor, 0, contentW - viewportW);

            if (Mathf.Abs(offset - targetOffset) < 2f) { followSmoothCoroutine = null; yield break; }

            float playheadInView = playheadX - offset;
            float margin = viewportW * autoScrollMargin;
            if (playheadInView >= margin && playheadInView <= viewportW - margin && Mathf.Abs(playheadInView - anchor) < viewportW * 0.15f)
            {

                followSmoothCoroutine = null; yield break;
            }
            float denom = contentW - viewportW;
            float targetNorm = denom > 0.001f ? targetOffset / denom : 0f;
            targetNorm = Mathf.Clamp01(targetNorm);
            float startNorm = timelineScrollRect.horizontalNormalizedPosition;
            if (Mathf.Abs(startNorm - targetNorm) < 0.001f) { followSmoothCoroutine = null; yield break; }
            float duration = Mathf.Clamp(followSmoothDuration, 0.35f, 1.2f);

            float dist = Mathf.Abs(targetNorm - startNorm);
            duration = Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(dist * 3f));
            float t = 0f;
            timelineScrollRect.velocity = Vector2.zero;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;

                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                timelineScrollRect.horizontalNormalizedPosition = Mathf.LerpUnclamped(startNorm, targetNorm, e);
                yield return null;
                if (!autoScrollWithPlayhead) yield break;
            }
            timelineScrollRect.horizontalNormalizedPosition = targetNorm;
            followSmoothCoroutine = null;
        }

        public void RefreshAll() { notesController.EnsureExplicitSpeeds(); waveformView.RefreshWaveform(); RefreshScrollContent(); notesController.RefreshNotes(); gridView.RefreshGrid(true); transport.UpdatePlayhead(); UpdateScrollToPlayhead(true); transport.UpdateLabels(); transport.UpdatePlayPauseLabel(); UpdateWarningState(); }

        public void RefreshNotes() => notesController.RefreshNotes();
        public int ResnapAllNotes() => notesController.ResnapAllNotes();
        public string SpeedHint(float speed) => notesController.SpeedHint(speed);
        public int EnsureExplicitSpeeds() => notesController.EnsureExplicitSpeeds();
        public int GetSelectedIndex() => notesController != null ? notesController.GetSelectedIndex() : -1;
        public Texture2D GetWaveformTexture() => waveformView != null ? waveformView.GetWaveformTexture() : null;
        public float[] GetWaveformData() => waveformView != null ? waveformView.GetWaveformData() : null;
        public void Seek(float time) => transport.Seek(time);
        public void Seek(float time, bool isScrubMove) => transport.Seek(time, isScrubMove);
        public float GetCurrentTime() => transport != null ? transport.GetCurrentTime() : 0f;
        public void TogglePlayPause() => transport.TogglePlayPause();
        bool HasTrack() => (levelData != null && levelData.music != null) || (audioSource != null && audioSource.clip != null);
        void UpdateWarningState() { bool has = HasTrack(); if (warningObject != null) warningObject.SetActive(!has); if (timelineObject != null) timelineObject.SetActive(has); }

        void RefreshScrollContent()
        {
            if (timelineContent == null) return;
            float len = GetClipLength(); if (len < 0.1f) len = 10f;
            float w = len * pixelsPerSecond * Mathf.Max(0.1f, zoom);
            float h = timelineContent.sizeDelta.y; if (h < 10) h = 110;
            timelineContent.sizeDelta = new Vector2(w, h);
            if (waveformRect == timelineContent && waveformImage != null)
            {
                var rt = waveformImage.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            if (timelineViewport == null && timelineScrollRect != null) timelineViewport = timelineScrollRect.viewport;
        }


        public bool IsFreeMoveHeld() => Input.GetKey(freeMoveKey) || Input.GetKey(freeMoveKeyAlt) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift);
        public bool ShouldSnap() => autoQuantize && snapToGrid && !IsFreeMoveHeld();

        [Header("Сетка (секунды)")]
        public Color gridSecColor = new Color(1f, 1f, 1f, 0.22f);
        public Color gridSec5Color = new Color(1f, 1f, 1f, 0.32f);
        public Font gridLabelFont;









        void HandleWheelZoom()
        {
            if (timelineViewport == null && timelineContent == null) return;
            if (notesController.IsDragging || transport.IsScrubbing) return;
            float wheel = 0f;
            wheel += Input.GetAxis("Mouse ScrollWheel");
            Vector2 md = Input.mouseScrollDelta;
            if (Mathf.Abs(md.y) > 0.01f) wheel += md.y * 0.1f;
            if (Mathf.Abs(wheel) < 0.001f) return;

            bool isZoom = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (isZoom)
            {
                if (!IsMouseOverTimeline(Input.mousePosition)) return;
                float delta = wheel * wheelZoomStep * Mathf.Max(1f, targetZoom);
                float newTarget = Mathf.Clamp(targetZoom + delta, 0.25f, 4f);
                if (Mathf.Abs(newTarget - targetZoom) < 0.001f) return;
                SetZoomAnimated(newTarget, Input.mousePosition);
            }
            else
            {
                if (!IsMouseOverTimeline(Input.mousePosition)) return;
                if (timelineScrollRect == null) return;
                float cur = timelineScrollRect.horizontalNormalizedPosition;

                cur = Mathf.Clamp01(cur - wheel * wheelScrollSpeed);
                timelineScrollRect.horizontalNormalizedPosition = cur;
                timelineScrollRect.velocity = Vector2.zero;
            }
        }

        void ProcessZoomLerp()
        {
            if (!isZoomAnimating) return;
            if (Mathf.Abs(zoom - targetZoom) < 0.001f)
            {
                zoom = targetZoom;
                isZoomAnimating = false;
                if (waveformRect != null) waveformRect.localScale = Vector3.one;
                RefreshScrollContent();

                gridView.RefreshGrid(false);
                notesController.ApplyNoteZoomScale();
                transport.UpdatePlayhead();
                if (_pendingZoomCursorTime >= 0f) { ApplyZoomCursorCorrection(_pendingZoomCursorTime, _pendingZoomCursorScreenPos); _pendingZoomCursorTime = -1f; }
                else UpdateScrollToPlayhead(false);
                return;
            }
            float prevZoom = zoom;
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, 1f / Mathf.Max(1f, zoomLerpSpeed), 10f, Time.unscaledDeltaTime);
            if (Mathf.Abs(zoom - targetZoom) < 0.005f) zoom = targetZoom;
            RefreshScrollContent();
            notesController.ApplyNoteZoomScale();

            if (Mathf.Abs(zoom - prevZoom) > 0.08f) gridView.RefreshGrid(false);
            transport.UpdatePlayhead();
            if (_pendingZoomCursorTime >= 0f) ApplyZoomCursorCorrection(_pendingZoomCursorTime, _pendingZoomCursorScreenPos);
            else if (autoScrollWithPlayhead) UpdateScrollToPlayhead(false);
        }

        void ApplyZoomCursorCorrection(float timeUnderCursor, Vector2 screenPos)
        {
            if (timelineScrollRect == null || timelineContent == null || GetClipLength() < 0.01f) return;
            float contentW = timelineContent.rect.width;
            if (contentW < 1f) contentW = timelineContent.sizeDelta.x;
            RectTransform viewport = timelineViewport != null ? timelineViewport : timelineScrollRect.viewport;
            if (viewport == null) viewport = timelineScrollRect.GetComponent<RectTransform>();
            float viewportW = viewport.rect.width;
            if (contentW <= viewportW + 1f) return;
            float norm = Mathf.Clamp01(timeUnderCursor / GetClipLength());
            float playheadX = norm * contentW;
            Camera cam = GetCanvasCamera();
            Vector2 localInViewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, cam, out localInViewport)) return;
            float viewportLocalLeft = -viewportW * 0.5f;
            float cursorOffsetInViewport = localInViewport.x - viewportLocalLeft;
            float targetOffset = playheadX - cursorOffsetInViewport;
            targetOffset = Mathf.Clamp(targetOffset, 0, contentW - viewportW);
            float targetNorm = (contentW - viewportW) > 0.001f ? targetOffset / (contentW - viewportW) : 0f;
            timelineScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetNorm);
            timelineScrollRect.velocity = Vector2.zero;
        }

        protected override void Update()
        {
            UpdateWarningState();

            if (transfer != null && transfer.hasLevel && transfer.levelData != null && levelData != transfer.levelData)
            {
                bool need = levelData == null || levelData.events.Count != transfer.levelData.events.Count || levelData.bpm != transfer.levelData.bpm;
                if (need)
                {
                    levelData = transfer.levelData;
                    if (manager != null) manager.levelData = levelData;
                    RefreshAll();
                }
            }
            if (levelData != null && levelData.music != waveformView.LastClip && levelData.music != null) RefreshAll();
            HandleWheelZoom();
            ProcessZoomLerp();
            if (waveformRect != null && waveformRect.localScale != Vector3.one)
            {
                waveformRect.localScale = Vector3.Lerp(waveformRect.localScale, Vector3.one, Time.unscaledDeltaTime * 12f);
                if ((waveformRect.localScale - Vector3.one).sqrMagnitude < 0.0001f) waveformRect.localScale = Vector3.one;
            }
            HandleKeyboard(); transport.UpdateCurrentTimeFromAudio(); HandleMouseInput();
            transport.TickScrubStillness();
            transport.UpdatePlayhead();
            if (autoScrollWithPlayhead)
            {
                if (audioSource != null && audioSource.isPlaying) UpdateScrollToPlayhead(false);
                else if (transport.IsScrubbing) UpdateScrollToPlayhead(false);
            }
            transport.UpdateLabels(); transport.UpdatePlayPauseLabel();
            transport.TickLoop();
        }

        void HandleKeyboard()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                var sel = EventSystem.current.currentSelectedGameObject;
                if (sel.GetComponent<TMPro.TMP_InputField>() != null) return;
                if (sel.GetComponent<UnityEngine.UI.InputField>() != null) return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { if (notesController.HasSelection()) notesController.DeselectNote(); else if (notePropertiesPanel != null && notePropertiesPanel.activeSelf) propsController.HidePropertiesPanel(); }
            if (Input.GetKeyDown(KeyCode.Space)) TogglePlayPause();
            if (Input.GetKeyDown(addNoteKey) || Input.GetKeyDown(altAddNoteKey)) notesController.AddNoteAtCurrentPlayhead();
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) { if (Input.GetKeyDown(KeyCode.A)) { notesController.SelectAllNotes(); return; } }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) notesController.DeleteSelectionOrNearest();
            for (int k = 1; k <= 12; k++) { if (Input.GetKeyDown(KeyCode.Alpha0 + k) || Input.GetKeyDown(KeyCode.Keypad0 + k)) { notesController.SetBrush(k - 1); } }
            if (notesController.HasSelection() && levelData != null && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                float dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1f : -1f;
                float step = Input.GetKey(KeyCode.LeftShift) ? quantStep * 0.5f : quantStep;
                if (!ShouldSnap()) step = 0.05f;
                notesController.NudgeSelection(dir * step);
            }
        }

        void HandleMouseInput()
        {
            if (notesController.IsDragging) return;
            RectTransform headerRect = playheadHeader;
            bool headerClick = headerRect != null && RectTransformUtility.RectangleContainsScreenPoint(headerRect, Input.mousePosition, GetCanvasCamera());
            if (headerClick)
            {
                if (Input.GetMouseButtonDown(0)) { UpdateAutoscrollLockFromScreenPos(Input.mousePosition); transport.BeginScrubAt(transport.GetTimeFromMouseHeader(Input.mousePosition), Input.mousePosition); }
                if (Input.GetMouseButton(0) && transport.IsScrubbing)
                {
                    Vector2 cur = Input.mousePosition;
                    transport.ScrubMoveTo(transport.GetTimeFromMouseHeader(cur), cur);
                }
                if (Input.GetMouseButtonUp(0) && transport.IsScrubbing) transport.EndScrub();
                return;
            }
            RectTransform refRect = waveformRect != null ? waveformRect : timelineContent;
            if (refRect == null) return;
            if (Input.GetMouseButtonDown(0) && !IsPointerOverNote())
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(refRect, Input.mousePosition, GetCanvasCamera()))
                {
                    float t = GetTimeFromMouse(Input.mousePosition);
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand)) { transport.BeginScrubAt(t, Input.mousePosition); }
                    else notesController.AddNoteAtTime(t);
                }
            }
            if (Input.GetMouseButtonUp(0) && transport.IsScrubbing) transport.EndScrub();
            if (transport.IsScrubbing && Input.GetMouseButton(0))
            {
                Vector2 cur = Input.mousePosition;
                RectTransform scrubRect = playheadHeader != null ? playheadHeader : refRect;
                float t = scrubRect == playheadHeader ? transport.GetTimeFromMouseHeader(cur) : GetTimeFromMouse(cur);
                transport.ScrubMoveTo(t, cur);
            }
        }





        bool IsPointerOverNote()
        {
            if (EventSystem.current == null) return false;
            var ray = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ray, results);
            foreach (var r in results) if (r.gameObject != null && r.gameObject.name.StartsWith("Note_")) return true;
            return false;
        }



        public void UpdateScrollToPlayhead(bool immediate = false)
        {
            if (timelineScrollRect == null || timelineContent == null || GetClipLength() < 0.01f) return;
            if (!autoScrollWithPlayhead) return;
            RectTransform viewport = timelineViewport != null ? timelineViewport : timelineScrollRect.viewport;
            if (viewport == null) viewport = timelineScrollRect.GetComponent<RectTransform>();
            float viewportW = viewport.rect.width;
            float contentW = timelineContent.rect.width;
            if (contentW < 1f) contentW = timelineContent.sizeDelta.x;
            if (contentW <= viewportW + 1f) return;
            float norm = Mathf.Clamp01(transport.GetCurrentTime() / GetClipLength());
            float playheadX = norm * contentW;
            float offset = -timelineContent.anchoredPosition.x;
            float margin = viewportW * Mathf.Clamp01(autoScrollMargin);
            float targetOffset = offset;
            bool isPlaying = audioSource != null && audioSource.isPlaying;
            if (immediate) targetOffset = playheadX - viewportW * 0.5f;
            else if (isPlaying)
            {
                float anchorRatio = (autoscrollKeepCurrentPosition && autoscrollHasLockedRatio) ? autoscrollLockedRatio : 0.35f;
                float desiredViewportX = viewportW * anchorRatio;
                float playheadInView = playheadX - offset;

                if (autoscrollKeepCurrentPosition && autoscrollHasLockedRatio)
                {
                    targetOffset = playheadX - desiredViewportX;
                }
                else
                {
                    if (playheadInView > desiredViewportX + 2f || playheadInView < desiredViewportX - viewportW * 0.25f) targetOffset = playheadX - desiredViewportX;
                    else { float leftEdge = offset; float rightEdge = offset + viewportW; if (playheadX < leftEdge + margin) targetOffset = playheadX - margin; else if (playheadX > rightEdge - margin) targetOffset = playheadX - viewportW + margin; else return; }
                }
            }
            else
            {
                float leftEdge = offset; float rightEdge = offset + viewportW;
                if (playheadX < leftEdge + margin) targetOffset = playheadX - margin;
                else if (playheadX > rightEdge - margin) targetOffset = playheadX - viewportW + margin;
                else return;
            }
            targetOffset = Mathf.Clamp(targetOffset, 0, contentW - viewportW);
            float denom = contentW - viewportW;
            float targetNorm = denom > 0.001f ? targetOffset / denom : 0f;
            targetNorm = Mathf.Clamp01(targetNorm);
            if (timelineScrollRect.velocity.sqrMagnitude > 1f) timelineScrollRect.velocity = Vector2.zero;
            timelineScrollRect.horizontalNormalizedPosition = targetNorm;
            if (immediate) { Canvas.ForceUpdateCanvases(); timelineContent.anchoredPosition = new Vector2(-targetOffset, timelineContent.anchoredPosition.y); timelineScrollRect.horizontalNormalizedPosition = targetNorm; }
        }

        [ContextMenu("Центрировать на плейхеде")] public void CenterScrollOnPlayhead() => UpdateScrollToPlayhead(true);
        public string FormatTime(float t) { int m = Mathf.FloorToInt(t / 60f); int s = Mathf.FloorToInt(t % 60f); int ms = Mathf.FloorToInt((t - Mathf.Floor(t)) * 100); return $"{m:0}:{s:00}.{ms:00}"; }







        public float GetClipLength()
        {
            if (levelData != null && levelData.music != null) return levelData.music.length;
            if (audioSource != null && audioSource.clip != null) return audioSource.clip.length;
            return 0f;
        }


        public float GetTimeFromMouse(Vector2 screenPos)
        {
            RectTransform refRect = timelineContent != null ? timelineContent : waveformRect;
            if (refRect == null) refRect = waveformRect;
            if (refRect == null) return transport.GetCurrentTime();
            Camera cam = GetCanvasCamera();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, screenPos, cam, out local)) return transport.GetCurrentTime();
            Rect rect = refRect.rect;
            float norm = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            norm = Mathf.Clamp01(norm);
            return norm * GetClipLength();
        }
        public Camera GetCanvasCamera() { if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>(); if (rootCanvas == null) return null; return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera; }
        public void SetQuant(float q) { quantStep = q; gridView.RefreshGrid(true); }
        void OnFileLoaded(string path, AudioClip clip)
        {
            if (clip == null) return;
            if (levelData == null)
            {
                levelData = new RhythmLevelData();
                levelData.fullTitle = "";
                levelData.songAuthor = "";
                levelData.mapAuthor = "";
                levelData.bpm = 128f;
                levelData.offset = 0f;
                levelData.events = new System.Collections.Generic.List<ObstacleEvent>();
                if (manager != null) manager.levelData = levelData;
                Debug.Log("[Timeline] Создан новый LevelData (был null) при загрузке аудио", this);
            }
            bool isNewClip = waveformView.LastClip != clip;
            bool wasEmptyLevel = levelData.events.Count > 0 && string.IsNullOrEmpty(levelData.fullTitle) && string.IsNullOrEmpty(levelData.songAuthor);
            levelData.music = clip; levelData.audioPath = path;

            if (wasEmptyLevel || (isNewClip && levelData.events.Count > 0 && waveformView.LastClip == null))
            {
                levelData.events.Clear();
                notesController.DeselectNote();
            }
            if (audioSource != null) { audioSource.clip = clip; audioSource.Stop(); transport.SetTime(0f); }

            if (showBpmAfterLoad && clip != null)
            {
                try
                {
                    var det = BpmDetector.Detect(clip);

                    bool detValid = det.bpm >= 40f && det.bpm <= 300f;
                    if (detValid) levelData.bpm = det.bpm;
                    else levelData.bpm = 128f;
                    string bpmTxt = detValid ? $"BPM: {levelData.bpm:0}" : $"BPM: 128 (детект {det.bpm:0} вне 40–300)";

                    if (bpmOutputText != null) bpmOutputText.text = bpmTxt;
                    Debug.Log($"[BPM] {clip.name} -> {det.bpm:0.##} conf {det.confidence:0.##} (сохранен {levelData.bpm:0})", this);
                    FlashStatus($"Загружено: {clip.name}  {clip.length:0.0}с • {bpmTxt} — ставь ноты (Enter/Space/клик)");
                    waveformView.Invalidate(); gridView.Invalidate();
                    RefreshAll();
                    if (bpmOutputText != null) bpmOutputText.text = bpmTxt;
                    return;
                } catch (System.Exception e) { Debug.LogWarning($"[BPM] detect failed: {e.Message}"); }
            }

            waveformView.Invalidate(); gridView.Invalidate();
            RefreshAll();
            FlashStatus($"Загружено: {clip.name}  {clip.length:0.0}с • BPM {levelData.bpm:0.##} — ставь ноты (Enter/Space/клик)");
        }
        public void FlashStatus(string msg) { if (statusLabel != null) statusLabel.text = msg; Debug.Log($"[Timeline] {msg}", this); CancelInvoke(nameof(ClearStatus)); Invoke(nameof(ClearStatus), 3f); }
        public void ClearStatus() { if (statusLabel != null) statusLabel.text = ""; }

        [ContextMenu("Перестроить вейвформу")] void ContextRebuild() { waveformView.Invalidate(); gridView.Invalidate(); RefreshAll(); }
        [ContextMenu("Добавить ноту")] void ContextAdd() => notesController.AddNoteAtCurrentPlayhead();
        [ContextMenu("Очистить")] void ContextClear() => notesController.ClearAllNotes();
    }
}
