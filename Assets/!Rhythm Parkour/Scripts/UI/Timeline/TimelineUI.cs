using System.Collections.Generic;
using System.Globalization;
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
        public RhythmLevelData levelData;
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
        Vector2 lastScrubScreenPos;
        float lastScrubMoveTime;
        bool scrubPausedDueToStill;
        const float scrubStillThresholdPxSq = 4f;
        const float scrubStationaryPauseDelay = 0.18f;
        Coroutine followSmoothCoroutine;

        public GameObject notePropertiesPanel;
        public TMP_InputField propPrefabIndexInput;
        public TMP_Dropdown propPrefabDropdown;
        public TextMeshProUGUI propTitleLabel;
        public TMP_InputField propSpeedInput;
        private TextMeshProUGUI propPrefabButtonLabel;

        [Header("Сетка миниатюр (выбор вида)")]
        public GameObject prefabGridPanel;
        public Transform prefabGridContainer;
        public GameObject prefabThumbPrefab;
        public bool closeGridOnSelect = true;

        [Header("BPM вывод (после загрузки трека)")]
        public TextMeshProUGUI bpmOutputText;
        public bool showBpmAfterLoad = true;

        Texture2D waveformTex;
        float[] waveformData;
        AudioClip lastClip;
        int lastWaveformGenWidth = -1;
        float lastWaveformGenPPS = -1f;
        float lastWaveformGenZoom = -1f;
        float lastGridZoom = -999f;
        float lastGridStepSec = -1f;
        float lastGridClipLen = -1f;
        float currentTime;

        List<GameObject> gridLinePool = new List<GameObject>();
        List<GameObject> gridLabelPool = new List<GameObject>();
        float lastGridRefreshTime;
        int lastGridLabelEvery = -1;
        List<GameObject> notePool = new List<GameObject>();
        bool isScrubbingWaveform;
        bool wasPlayingBeforeScrub;
        float scrubSavedVolume = 1f;
        bool scrubWasPlaying;
        int selectedIndex = -1;
        HashSet<int> selectedIndices = new HashSet<int>();
        List<GameObject> noteGos = new List<GameObject>();
        Canvas injectedCanvas;
        bool isDraggingNote;
        int dragNoteIdx = -1;
        RectTransform dragNoteRect;
        Dictionary<int, float> dragOrigHitBeats = new Dictionary<int, float>();
        float dragStartHitBeat;
        float dragDeltaBeat;

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


            BindEvents();
            if (fileLoader != null) fileLoader.onFileLoaded.AddListener(OnFileLoaded);
            if (notePropertiesPanel != null) notePropertiesPanel.SetActive(false);
            if (!followSlider && autoScrollWithPlayhead) autoScrollWithPlayhead = false;
            if (followSlider && !autoScrollWithPlayhead) followSlider = false; else followSlider = autoScrollWithPlayhead;

            RefreshAll();
            currentTime = 0f;
            UpdatePlayhead();
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
            if (waveformTex != null) Destroy(waveformTex);
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

            if (Time.unscaledTime - lastGridRefreshTime > 0.08f) RefreshGrid(false);
            ApplyNoteZoomScale();
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

        public void SetPixelsPerSecond(float pps) { float np = Mathf.Clamp(pps, 10f, 1000f); if (Mathf.Abs(np - pixelsPerSecond) < 0.1f) return; pixelsPerSecond = np; RefreshScrollContent(); if (Mathf.Abs(pps - lastWaveformGenPPS) > 5f) RefreshWaveform(true); }

        void BindEvents()
        {
            BindPropertiesPanel();
            if (playheadHeader != null)
            {
                var et = playheadHeader.gameObject.GetComponent<EventTrigger>();
                if (et == null) et = playheadHeader.gameObject.AddComponent<EventTrigger>();
                et.triggers.Clear();
                EnsureEvent(et, EventTriggerType.PointerDown, OnHeaderPointerDown);
                EnsureEvent(et, EventTriggerType.Drag, OnHeaderDrag);
                EnsureEvent(et, EventTriggerType.PointerUp, data => EndScrub());
                EnsureEvent(et, EventTriggerType.BeginDrag, OnHeaderPointerDown);
                EnsureEvent(et, EventTriggerType.EndDrag, data => EndScrub());
            }
        }



        void BindPropertiesPanel()
        {
            if (propPrefabIndexInput != null) propPrefabIndexInput.onSubmit.AddListener(_ => ApplyPropertiesFromPanel());
            if (propPrefabDropdown != null)
            {
                propPrefabDropdown.onValueChanged.RemoveListener(OnPrefabDropdownChanged);
                propPrefabDropdown.onValueChanged.AddListener(OnPrefabDropdownChanged);
            }

            if (prefabGridPanel != null) prefabGridPanel.SetActive(false);
        }

        void OnPrefabDropdownChanged(int idx)
        {
            if (propPrefabIndexInput != null) propPrefabIndexInput.SetTextWithoutNotify(idx.ToString());
            ApplyPropertiesFromPanel();
        }

        void EnsurePrefabGrid()
        {
            if (prefabGridPanel != null && prefabGridContainer != null) return;

            if (prefabGridPanel != null && prefabGridContainer == null)
            {
                var t = prefabGridPanel.transform.Find("Grid");
                if (t != null) prefabGridContainer = t;
                else prefabGridContainer = prefabGridPanel.transform;
            }

            if (prefabGridPanel == null)
            {
                if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
                if (rootCanvas == null) return;
                var panelGO = new GameObject("PrefabGridPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                panelGO.transform.SetParent(rootCanvas.transform, false);
                var rt = panelGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(560, 420);
                var img = panelGO.GetComponent<Image>(); img.color = new Color(0.12f, 0.12f, 0.14f, 0.96f); img.raycastTarget = true;
                var vlg = panelGO.GetComponent<VerticalLayoutGroup>(); vlg.padding = new RectOffset(12,12,12,12); vlg.spacing = 8; vlg.childAlignment = TextAnchor.UpperCenter; vlg.childControlWidth = true; vlg.childControlHeight = false;

                var titleGO = new GameObject("Title", typeof(RectTransform));
                titleGO.transform.SetParent(panelGO.transform, false);
                var ttmp = titleGO.AddComponent<TextMeshProUGUI>(); ttmp.text = "Выбор вида препятствия"; ttmp.fontSize = 16; ttmp.alignment = TextAlignmentOptions.Center; ttmp.color = Color.white;
                var le = titleGO.AddComponent<LayoutElement>(); le.minHeight = 24;

                var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
                scrollGO.transform.SetParent(panelGO.transform, false);
                var srt = scrollGO.GetComponent<RectTransform>(); srt.sizeDelta = new Vector2(0, 320);
                var sle = scrollGO.AddComponent<LayoutElement>(); sle.flexibleHeight = 1; sle.minHeight = 200;
                var sImg = scrollGO.GetComponent<Image>(); sImg.color = new Color(0,0,0,0.15f);
                scrollGO.GetComponent<Mask>().showMaskGraphic = false;
                var scroll = scrollGO.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
                var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
                gridGO.transform.SetParent(scrollGO.transform, false);
                var grt = gridGO.GetComponent<RectTransform>(); grt.anchorMin = new Vector2(0,1); grt.anchorMax = new Vector2(1,1); grt.pivot = new Vector2(0.5f,1); grt.anchoredPosition = Vector2.zero; grt.sizeDelta = new Vector2(0,0);
                var glg = gridGO.GetComponent<GridLayoutGroup>(); glg.cellSize = new Vector2(80, 80); glg.spacing = new Vector2(8,8); glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 6; glg.childAlignment = TextAnchor.UpperCenter;
                var csf = gridGO.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = grt;
                scroll.viewport = srt;

                var btnRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                btnRow.transform.SetParent(panelGO.transform, false);
                var brow = btnRow.GetComponent<HorizontalLayoutGroup>(); brow.spacing = 8; brow.childAlignment = TextAnchor.MiddleCenter; brow.childControlWidth = false;
                var ble = btnRow.AddComponent<LayoutElement>(); ble.minHeight = 32;
                var closeGO = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
                closeGO.transform.SetParent(btnRow.transform, false);
                var crt = closeGO.GetComponent<RectTransform>(); crt.sizeDelta = new Vector2(120, 32);
                var cimg = closeGO.GetComponent<Image>(); cimg.color = new Color(0.5f,0.2f,0.2f,1);
                var cbtn = closeGO.GetComponent<Button>();
                var ctxt = new GameObject("Text", typeof(RectTransform)); ctxt.transform.SetParent(closeGO.transform,false);
                var ctrt = ctxt.GetComponent<RectTransform>(); ctrt.anchorMin=Vector2.zero; ctrt.anchorMax=Vector2.one; ctrt.offsetMin=Vector2.zero; ctrt.offsetMax=Vector2.zero;
                var ctmp = ctxt.AddComponent<TextMeshProUGUI>(); ctmp.text="Закрыть"; ctmp.fontSize=14; ctmp.alignment=TextAlignmentOptions.Center; ctmp.color=Color.white;
                cbtn.onClick.AddListener(HidePrefabGrid);
                prefabGridPanel = panelGO;
                prefabGridContainer = grt.transform;
                prefabGridPanel.SetActive(false);
            }
        }

        public void TogglePrefabGrid()
        {
            EnsurePrefabGrid();
            if (prefabGridPanel == null) return;
            if (prefabGridPanel.activeSelf) HidePrefabGrid();
            else ShowPrefabGrid();
        }
        public void ShowPrefabGrid()
        {
            EnsurePrefabGrid();
            if (prefabGridPanel == null) return;
            RefreshPrefabGridThumbs();
            prefabGridPanel.SetActive(true);

            if (rootCanvas != null) prefabGridPanel.transform.SetAsLastSibling();
        }
        public void HidePrefabGrid()
        {
            if (prefabGridPanel != null) prefabGridPanel.SetActive(false);
        }
        void RefreshPrefabGridThumbs()
        {
            if (prefabGridContainer == null) return;

            for (int i=prefabGridContainer.childCount-1;i>=0;i--) Destroy(prefabGridContainer.GetChild(i).gameObject);
            int total = (catalog != null ? catalog.Count : 0);
            if (total==0 && levelData!=null) total = levelData.PrefabCount(catalog);
            if (total==0) total=1;
            for (int i=0;i<total;i++)
            {
                var pf = catalog != null ? catalog.GetPrefab(i) : null;
                if (pf==null && levelData!=null) pf = levelData.GetPrefab(i, catalog);
                string name = pf!=null? pf.name : $"#{i}";
                GameObject btnGO;
                if (prefabThumbPrefab != null) btnGO = Instantiate(prefabThumbPrefab, prefabGridContainer);
                else
                {
                    btnGO = new GameObject($"Thumb_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGO.transform.SetParent(prefabGridContainer, false);
                    var rt = btnGO.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(80,80);
                    var img = btnGO.GetComponent<Image>(); img.color = new Color(0.22f,0.22f,0.24f,1f);

                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGO.transform.SetParent(btnGO.transform, false);
                    var irt = iconGO.GetComponent<RectTransform>(); irt.anchorMin = new Vector2(0.1f,0.2f); irt.anchorMax = new Vector2(0.9f,0.85f); irt.offsetMin=irt.offsetMax=Vector2.zero;
                    var iimg = iconGO.GetComponent<Image>(); iimg.color = Color.white; iimg.preserveAspect = true;

#if UNITY_EDITOR
                    try{
                        var tex = UnityEditor.AssetPreview.GetAssetPreview(pf);
                        if (tex != null) iimg.sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f,0.5f));
                        else {
                            var mini = UnityEditor.AssetPreview.GetMiniThumbnail(pf);
                            if (mini != null) iimg.sprite = Sprite.Create(mini, new Rect(0,0,mini.width,mini.height), new Vector2(0.5f,0.5f));
                        }
                    } catch {}
#endif
                    if (iimg.sprite == null)
                    {

                        iimg.color = GetColorForPrefab(i);
                    }

                    var lblGO = new GameObject("Label", typeof(RectTransform));
                    lblGO.transform.SetParent(btnGO.transform, false);
                    var lrt = lblGO.GetComponent<RectTransform>(); lrt.anchorMin=new Vector2(0,0); lrt.anchorMax=new Vector2(1,0.22f); lrt.offsetMin=lrt.offsetMax=Vector2.zero;
                    var ltmp = lblGO.AddComponent<TextMeshProUGUI>(); ltmp.text = $"{i}: {name}"; ltmp.fontSize=8; ltmp.alignment=TextAlignmentOptions.Center; ltmp.color=new Color(1,1,1,0.9f); ltmp.enableWordWrapping=false; ltmp.overflowMode=TextOverflowModes.Ellipsis;
                }
                var btn = btnGO.GetComponent<Button>();
                int idx=i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(()=>{ OnThumbClicked(idx); });

                if (selectedIndex>=0 && selectedIndex < levelData.events.Count && levelData.events[selectedIndex].prefabIndex==i)
                {
                    var ol = btnGO.GetComponent<Outline>(); if (ol==null) ol=btnGO.AddComponent<Outline>();
                    ol.effectColor = Color.green; ol.effectDistance = new Vector2(3,3);
                }
            }
        }
        void OnThumbClicked(int idx)
        {
            if (levelData==null || selectedIndex<0 || selectedIndex>=levelData.events.Count) return;
            var tgt = selectedIndices.Count>1 ? new System.Collections.Generic.List<int>(selectedIndices) : new System.Collections.Generic.List<int>{selectedIndex};
            foreach (var ti in tgt) { if (ti<0||ti>=levelData.events.Count) continue; var e=levelData.events[ti]; e.prefabIndex=idx; levelData.events[ti]=e; }
            if (propPrefabDropdown!=null) propPrefabDropdown.SetValueWithoutNotify(Mathf.Clamp(idx,0,propPrefabDropdown.options.Count-1));
            if (propPrefabIndexInput!=null) propPrefabIndexInput.SetTextWithoutNotify(idx.ToString());
            RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            if (closeGridOnSelect) HidePrefabGrid();
            preview?.ForceRefresh();
            FlashStatus($"Вид → {GetPrefabName(idx)}");
        }

        void OnColorButtonClicked()
        {
            if (levelData == null || selectedIndex < 0) return;
            var ev = levelData.events[selectedIndex];
            Color newCol = ev.HasCustomColor ? ev.color : Color.white;
            float h = Random.value;
            newCol = Color.HSVToRGB(h, 0.85f, 1f);
            newCol.a = 1f;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                newCol = new Color(0,0,0,0);
            ev.color = newCol;
            levelData.events[selectedIndex] = ev;
            RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            FlashStatus(newCol.a < 0.1f ? "Цвет сброшен к глобальному" : $"Цвет → {ColorUtility.ToHtmlStringRGB(newCol)}");
        }

        void RefreshPrefabDropdown()
        {
            if (propPrefabDropdown == null) return;
            propPrefabDropdown.ClearOptions();
            int total = (catalog != null ? catalog.Count : 0);
            if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            if (total == 0) total = 1;
            var opts = new System.Collections.Generic.List<string>();
            for (int i=0;i<total;i++)
            {
                var pf = catalog != null ? catalog.GetPrefab(i) : null;
                if (pf == null && levelData != null) pf = levelData.GetPrefab(i, catalog);
                string name = pf != null ? $"{i}: {pf.name}" : $"{i}";
                opts.Add(name);
            }
            propPrefabDropdown.AddOptions(opts);

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
                    float norm = Mathf.Clamp01(currentTime / GetClipLength());
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
                    float n2 = Mathf.Clamp01(currentTime / GetClipLength());
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
        void UpdateAutoscrollLockFromScreenPos(Vector2 screenPos)
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
            float norm = Mathf.Clamp01(currentTime / GetClipLength());
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

        public void RefreshAll() { RefreshWaveform(); RefreshScrollContent(); RefreshNotes(); RefreshGrid(true); UpdatePlayhead(); UpdateScrollToPlayhead(true); UpdateLabels(); UpdatePlayPauseLabel(); UpdateWarningState(); }
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

        void RefreshWaveform(bool force = false)
        {
            if (waveformImage == null) return;
            if (!showWaveform)
            {
                waveformImage.enabled = false;
                if (waveformImage.texture != null) waveformImage.texture = null;
                return;
            }
            waveformImage.enabled = true;
            AudioClip clip = levelData != null ? levelData.music : null;
            if (clip == null && audioSource != null) clip = audioSource.clip;
            if (clip == null) { waveformImage.texture = null; waveformImage.color = new Color(1, 1, 1, 0.08f); return; }

            int desiredWidth = Mathf.Clamp(Mathf.RoundToInt(GetClipLength() * pixelsPerSecond * Mathf.Clamp(zoom, 0.8f, 2.2f)), 4096, 16384);
            desiredWidth = Mathf.Clamp(Mathf.Max(desiredWidth, waveformTexWidth), 4096, 16384);
            bool needRegen = force || clip != lastClip || waveformTex == null || desiredWidth != lastWaveformGenWidth || Mathf.Abs(pixelsPerSecond - lastWaveformGenPPS) > 1f || Mathf.Abs(zoom - lastWaveformGenZoom) > 0.22f;
            if (!needRegen) return;
            lastClip = clip;
            lastWaveformGenWidth = desiredWidth;
            lastWaveformGenPPS = pixelsPerSecond;
            lastWaveformGenZoom = zoom;
            if (waveformTex != null) Destroy(waveformTex);
            waveformData = WaveformGenerator.GenerateData(clip, desiredWidth);
            int h = Mathf.Clamp(waveformTexHeight, 32, 360);
            waveformTex = WaveformGenerator.GenerateTexture(waveformData, desiredWidth, h, waveformWaveColor, waveformBgColor);
            waveformImage.texture = waveformTex;
            waveformImage.color = Color.white;

            if (waveformTex != null) waveformTex.filterMode = FilterMode.Bilinear;
            if (waveformRect != null) waveformRect.localScale = Vector3.one;
        }

        bool IsFreeMoveHeld() => Input.GetKey(freeMoveKey) || Input.GetKey(freeMoveKeyAlt) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift);
        bool ShouldSnap() => autoQuantize && snapToGrid && !IsFreeMoveHeld();

        [Header("Сетка (секунды)")]
        public Color gridSecColor = new Color(1f, 1f, 1f, 0.22f);
        public Color gridSec5Color = new Color(1f, 1f, 1f, 0.32f);
        public Font gridLabelFont;

        void RefreshGrid(bool force = false)
        {
            if (gridContainer == null) return;
            float clipLen = GetClipLength();
            if (clipLen < 0.01f)
            {
                foreach (var go in gridLinePool) if (go) go.SetActive(false);
                foreach (var go in gridLabelPool) if (go) go.SetActive(false);
                gridContainer.gameObject.SetActive(false);
                return;
            }
            float pps = pixelsPerSecond * Mathf.Max(0.1f, zoom);
            int labelEvery = 1;
            if (pps < 25f) labelEvery = 10;
            else if (pps < 50f) labelEvery = 5;
            else if (pps < 90f) labelEvery = 2;
            else labelEvery = 1;

            bool need = force
                || Mathf.Abs(clipLen - lastGridClipLen) > 0.1f
                || Mathf.Abs(pixelsPerSecond - lastGridStepSec) > 1f
                || labelEvery != lastGridLabelEvery
                || gridLinePool.Count == 0;
            if (!need)
            {

                if (Mathf.Abs(zoom - lastGridZoom) < 0.05f) return;
            }

            if (!force && Time.unscaledTime - lastGridRefreshTime < 0.08f) return;
            lastGridZoom = zoom;
            lastGridClipLen = clipLen;
            lastGridStepSec = pixelsPerSecond;
            lastGridLabelEvery = labelEvery;
            lastGridRefreshTime = Time.unscaledTime;


            if (gridLinePool.Count == 0 && gridContainer.childCount > 0)
            {
                for (int i = gridContainer.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(gridContainer.GetChild(i).gameObject);
                    else Destroy(gridContainer.GetChild(i).gameObject);
#else
                    Destroy(gridContainer.GetChild(i).gameObject);
#endif
                }
            }

            gridContainer.gameObject.SetActive(true);
            int totalSecs = Mathf.CeilToInt(clipLen);
            int neededLines = totalSecs + 1;

            while (gridLinePool.Count < neededLines)
            {
                var lineGO = new GameObject($"Grid_Pooled_{gridLinePool.Count}s", typeof(RectTransform), typeof(Image));
                lineGO.transform.SetParent(gridContainer, false);
                var img = lineGO.GetComponent<Image>(); img.raycastTarget = false;
                gridLinePool.Add(lineGO);
            }
            for (int sec = 0; sec < neededLines; sec++)
            {
                float norm = Mathf.Clamp01(sec / clipLen);
                bool is5 = sec % 5 == 0;
                bool is10 = sec % 10 == 0;
                var lineGO = gridLinePool[sec];
                lineGO.SetActive(true);
                lineGO.name = $"Grid_{sec}s";
                var rt = lineGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(norm, 0f);
                rt.anchorMax = new Vector2(norm, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(is10 ? 2f : is5 ? 1.6f : 1f, 0f);
                var img = lineGO.GetComponent<Image>();
                if (is10) img.color = gridSec5Color;
                else if (is5) img.color = Color.Lerp(gridSecColor, gridSec5Color, 0.5f);
                else img.color = gridSecColor * 0.65f;
            }
            for (int i = neededLines; i < gridLinePool.Count; i++) if (gridLinePool[i]) gridLinePool[i].SetActive(false);


            int neededLabels = 0;
            for (int sec = 0; sec <= totalSecs; sec++)
            {
                bool is5 = sec % 5 == 0;
                if (sec % labelEvery == 0 || is5) neededLabels++;
            }
            while (gridLabelPool.Count < neededLabels)
            {
                var labelGO = new GameObject($"Lbl_Pooled_{gridLabelPool.Count}", typeof(RectTransform));
                labelGO.transform.SetParent(gridContainer, false);
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.pivot = new Vector2(0f, 1f);
                lrt.sizeDelta = new Vector2(70f, 18f);
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                var bgGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
                bgGO.transform.SetParent(labelGO.transform, false);
                bgGO.transform.SetAsFirstSibling();
                var bgRT = bgGO.GetComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = new Vector2(-2, -1); bgRT.offsetMax = new Vector2(2, 1);
                var bgImg = bgGO.GetComponent<Image>(); bgImg.color = new Color(0,0,0,0.28f); bgImg.raycastTarget = false;
                gridLabelPool.Add(labelGO);
            }
            int labelIdx = 0;
            for (int sec = 0; sec <= totalSecs; sec++)
            {
                bool is5 = sec % 5 == 0;
                bool is10 = sec % 10 == 0;
                if (!(sec % labelEvery == 0 || is5)) continue;
                float norm = Mathf.Clamp01(sec / clipLen);
                var labelGO = gridLabelPool[labelIdx++];
                labelGO.SetActive(true);
                labelGO.name = $"Lbl_{sec}";
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = lrt.anchorMax = new Vector2(norm, 1f);
                lrt.anchoredPosition = new Vector2(3f, -2f);
                var tmp = labelGO.GetComponent<TextMeshProUGUI>();
                tmp.text = sec == 0 ? "0:00" : FormatTime(sec);
                tmp.fontSize = is10 ? 11f : 10f;
                tmp.color = is10 ? new Color(1,1,1,0.85f) : is5 ? new Color(1,1,1,0.65f) : new Color(1,1,1,0.45f);
            }
            for (int i = labelIdx; i < gridLabelPool.Count; i++) if (gridLabelPool[i]) gridLabelPool[i].SetActive(false);
        }

        public void RefreshNotes()
        {
            if (isDraggingNote) return;
            if (notesContainer == null || levelData == null) return;
            float clipLen = GetClipLength();
            if (clipLen < 0.01f)
            {
                clipLen = 60f;
                if (levelData.events.Count > 0)
                {
                    float maxT = 0f;
                    foreach (var ev in levelData.events) { float ht = GetHitTime(ev); if (ht > maxT) maxT = ht; }
                    clipLen = Mathf.Max(30f, maxT + 5f);
                }
            }


            for (int i = levelData.events.Count; i < noteGos.Count; i++)
            {
                if (noteGos[i] != null) noteGos[i].SetActive(false);
            }

            while (noteGos.Count > levelData.events.Count)
            {
                var go = noteGos[noteGos.Count - 1];
                noteGos.RemoveAt(noteGos.Count - 1);
                if (go != null) { go.SetActive(false); notePool.Add(go); }
            }
            for (int i = 0; i < levelData.events.Count; i++)
            {
                var ev = levelData.events[i];
                float hitTime = GetHitTime(ev);
                float norm = Mathf.Clamp01(hitTime / clipLen);
                GameObject go = null;
                if (i < noteGos.Count && noteGos[i] != null)
                {
                    go = noteGos[i];

                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null) { rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.anchoredPosition = Vector2.zero; }
                    go.name = $"Note_{i}_{ev.prefabIndex}";

                    UpdateNoteVisual(go, i, ev);
                    go.SetActive(true);
                }
                else
                {
                    if (notePool.Count > 0)
                    {
                        go = notePool[notePool.Count - 1];
                        notePool.RemoveAt(notePool.Count - 1);
                        go.transform.SetParent(notesContainer, false);
                        var rt = go.GetComponent<RectTransform>();
                        if (rt != null) { rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = GetScaledNoteSize(); }
                        go.name = $"Note_{i}_{ev.prefabIndex}";
                        UpdateNoteVisual(go, i, ev);
                        go.SetActive(true);
                        if (i < noteGos.Count) noteGos[i] = go;
                        else noteGos.Add(go);
                    }
                    else
                    {
                        go = CreateNoteGO(i, ev, norm);
                        if (i < noteGos.Count) noteGos[i] = go;
                        else noteGos.Add(go);
                    }
                }
            }

            ApplyNoteZoomScale();
            if (selectedIndex >= 0) RefreshPropertiesPanel(); else HidePropertiesPanel();
        }

        GameObject CreateNoteGO(int index, ObstacleEvent ev, float norm)
        {
            GameObject go; RectTransform rt; Image img = null;
            if (notePrefab != null)
            {
                go = Instantiate(notePrefab, notesContainer); go.name = $"Note_{index}_{ev.prefabIndex}";
                rt = go.GetComponent<RectTransform>(); if (rt == null) rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = GetScaledNoteSize();
                if (ev.scale != Vector3.one && ev.scale != Vector3.zero) { float avg=(ev.scale.x+ev.scale.y+ev.scale.z)/3f; rt.sizeDelta *= Mathf.Clamp(avg,0.6f,2.2f); }
                if (rt.sizeDelta.x < 8) rt.sizeDelta = GetScaledNoteSize();

                if (Mathf.Abs(ev.position.x) > 0.01f) rt.anchoredPosition += new Vector2(0, ev.position.x * 7f);
                img = go.GetComponent<Image>(); if (img == null) img = go.GetComponentInChildren<Image>();
                if (img != null) { Color noteCol = ev.HasCustomColor ? ev.color : GetColorForPrefab(ev.prefabIndex); img.color = noteCol; img.raycastTarget = true; }
                bool isSelForView = selectedIndex == index || selectedIndices.Contains(index);
                if (notePrefabUseCustomView)
                {
                    var custom = go.GetComponent<ITimelineNoteView>();
                    if (custom != null) custom.Setup(index, ev, GetHitBeat(ev), isSelForView);
                    else go.SendMessage("OnTimelineNoteSetup", new object[] { index, ev, GetHitBeat(ev), isSelForView }, SendMessageOptions.DontRequireReceiver);
                }
                bool isSelected = isSelForView;
                Transform selTf = go.transform.Find("Selected");
                if (selTf != null) selTf.gameObject.SetActive(isSelected);
                else if (isSelected && go.GetComponent<Outline>() == null && img != null) { var outline = go.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(2, 2); }
            }
            else
            {
                go = new GameObject($"Note_{index}_{ev.prefabIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(notesContainer, false);
                rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = GetScaledNoteSize(); rt.anchoredPosition = Vector2.zero;
                if (ev.scale != Vector3.one && ev.scale != Vector3.zero) { float avg=(ev.scale.x+ev.scale.y+ev.scale.z)/3f; rt.sizeDelta *= Mathf.Clamp(avg,0.6f,2.2f); }
                if (Mathf.Abs(ev.position.x) > 0.01f) rt.anchoredPosition += new Vector2(0, ev.position.x * 7f);
                img = go.GetComponent<Image>(); Color nc = ev.HasCustomColor ? ev.color : GetColorForPrefab(ev.prefabIndex); img.color = nc; img.raycastTarget = true;
                var edgeGO = new GameObject("Edge", typeof(RectTransform), typeof(Image)); edgeGO.transform.SetParent(go.transform, false);
                var eRT = edgeGO.GetComponent<RectTransform>(); eRT.anchorMin = new Vector2(0, 0); eRT.anchorMax = new Vector2(1, 0); eRT.pivot = new Vector2(0.5f, 0); eRT.sizeDelta = new Vector2(0, 3); eRT.anchoredPosition = Vector2.zero;
                edgeGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f); edgeGO.GetComponent<Image>().raycastTarget = false;
                bool isSelected = selectedIndex == index || selectedIndices.Contains(index);
                if (isSelected) { var outline = go.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(2, 2); }
            }
            int captured = index;
            var btn = go.GetComponent<Button>(); if (btn == null) btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint; var colors = btn.colors; colors.highlightedColor = Color.white; btn.colors = colors;
            btn.onClick.AddListener(() => { bool multi = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.LeftShift); if (multi) ToggleSelectNote(captured); else SelectNote(captured); });
            var et = go.GetComponent<EventTrigger>(); if (et == null) et = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener((data) => { var ped = (PointerEventData)data; if (ped.button == PointerEventData.InputButton.Right) RemoveNoteAt(captured); });
            et.triggers.Add(entry);
            var hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hoverEntry.callback.AddListener((_) => { float ht = GetHitTime(ev); if (statusLabel != null) statusLabel.text = $"{FormatTime(ht)} • #{ev.prefabIndex} • {ev.speed:0}m/s — ЛКМ выбор, Ctrl+клик множ., ПКМ удалить, тащи (Ctrl свободно)"; });
            et.triggers.Add(hoverEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => ClearStatus()); et.triggers.Add(exitEntry);
            var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            beginDrag.callback.AddListener((data) => { var ped = (PointerEventData)data; ped.Use(); StartNoteDrag(captured, ped); }); et.triggers.Add(beginDrag);
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) => OnNoteDrag((PointerEventData)data)); et.triggers.Add(drag);
            var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            endDrag.callback.AddListener((_) => EndNoteDrag()); et.triggers.Add(endDrag);
            return go;
        }

        void UpdateNoteVisual(GameObject go, int index, ObstacleEvent ev)
        {
            if (go == null) return;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = GetScaledNoteSize();
                if (ev.scale != Vector3.one && ev.scale != Vector3.zero) { float avg=(ev.scale.x+ev.scale.y+ev.scale.z)/3f; rt.sizeDelta *= Mathf.Clamp(avg,0.6f,2.2f); }
                if (rt.sizeDelta.x < 8) rt.sizeDelta = GetScaledNoteSize();

                Vector2 basePos = Vector2.zero;
                if (Mathf.Abs(ev.position.x) > 0.01f) basePos += new Vector2(0, ev.position.x * 7f);
                rt.anchoredPosition = basePos;
            }
            var img = go.GetComponent<Image>(); if (img == null) img = go.GetComponentInChildren<Image>();
            if (img != null)
            {
                Color noteCol = ev.HasCustomColor ? ev.color : GetColorForPrefab(ev.prefabIndex);
                img.color = noteCol;
            }
            bool isSel = selectedIndex == index || selectedIndices.Contains(index);
            if (notePrefabUseCustomView)
            {
                var custom = go.GetComponent<ITimelineNoteView>();
                if (custom != null) custom.Setup(index, ev, GetHitBeat(ev), isSel);
            }
            Transform selTf = go.transform.Find("Selected");
            if (selTf != null) selTf.gameObject.SetActive(isSel);
            var outline = go.GetComponent<Outline>();
            if (isSel)
            {
                if (outline == null) { outline = go.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(2,2); }
                if (selTf == null) outline.enabled = true;
            }
            else
            {
                if (outline != null && selTf == null) { Destroy(outline); }
                else if (outline != null) outline.enabled = false;
            }

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int captured = index;
                btn.onClick.AddListener(() => { bool multi = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.LeftShift); if (multi) ToggleSelectNote(captured); else SelectNote(captured); });
            }
            var et = go.GetComponent<EventTrigger>();
            if (et != null)
            {
                et.triggers.Clear();
                int captured = index;
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener((data) => { var ped = (PointerEventData)data; if (ped.button == PointerEventData.InputButton.Right) RemoveNoteAt(captured); });
                et.triggers.Add(entry);
                var hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hoverEntry.callback.AddListener((_) => { float ht = GetHitTime(ev); if (statusLabel != null) statusLabel.text = $"{FormatTime(ht)} • #{ev.prefabIndex} • {ev.speed:0}m/s — ЛКМ выбор, Ctrl+клик множ., ПКМ удалить, тащи (Ctrl свободно)"; });
                et.triggers.Add(hoverEntry);
                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => ClearStatus()); et.triggers.Add(exitEntry);
                var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
                beginDrag.callback.AddListener((data) => { var ped = (PointerEventData)data; ped.Use(); StartNoteDrag(captured, ped); }); et.triggers.Add(beginDrag);
                var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                drag.callback.AddListener((data) => OnNoteDrag((PointerEventData)data)); et.triggers.Add(drag);
                var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
                endDrag.callback.AddListener((_) => EndNoteDrag()); et.triggers.Add(endDrag);
            }
        }

        void StartNoteDrag(int idx, PointerEventData ped)
        {
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) return;
            dragNoteIdx = idx; isDraggingNote = true;
            if (!selectedIndices.Contains(idx))
            {
                bool multi = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftShift);
                if (!multi) { selectedIndices.Clear(); selectedIndices.Add(idx); selectedIndex = idx; ShowPropertiesPanel(idx); }
                else { selectedIndices.Add(idx); selectedIndex = idx; ShowPropertiesPanel(idx); }
            }
            if (idx >= 0 && idx < noteGos.Count && noteGos[idx] != null) dragNoteRect = noteGos[idx].GetComponent<RectTransform>(); else dragNoteRect = null;
            isScrubbingWaveform = false;
            dragOrigHitBeats.Clear(); dragStartHitBeat = GetHitBeat(levelData.events[idx]); dragDeltaBeat = 0f;
            var indicesToSave = selectedIndices.Count > 0 && selectedIndices.Contains(idx) ? selectedIndices : new HashSet<int> { idx };
            foreach (var i in indicesToSave) if (i >= 0 && i < levelData.events.Count) dragOrigHitBeats[i] = GetHitBeat(levelData.events[i]);
            if (dragOrigHitBeats.Count == 0) dragOrigHitBeats[idx] = dragStartHitBeat;
        }

        void OnNoteDrag(PointerEventData ped)
        {
            if (!isDraggingNote || dragNoteIdx < 0 || levelData == null) return;
            if (GetClipLength() < 0.01f) return;
            float t = GetTimeFromMouse(ped.position);
            float hitBeat = levelData.TimeToBeat(t);
            if (ShouldSnap()) hitBeat = Mathf.Round(hitBeat / quantStep) * quantStep;
            hitBeat = Mathf.Clamp(hitBeat, 0f, levelData.TimeToBeat(GetClipLength()));
            float delta = hitBeat - dragStartHitBeat; dragDeltaBeat = delta;
            HashSet<int> indices = dragOrigHitBeats.Count > 1 ? new HashSet<int>(dragOrigHitBeats.Keys) : new HashSet<int> { dragNoteIdx };
            if (indices.Count == 1)
            {
                bool occupied = false;
                for (int i = 0; i < levelData.events.Count; i++) { if (indices.Contains(i)) continue; if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hitBeat) < 0.02f) { occupied = true; break; } }
                if (occupied) return;
            }
            foreach (var idx in new List<int>(indices))
            {
                if (!dragOrigHitBeats.ContainsKey(idx)) continue;
                float orig = dragOrigHitBeats[idx];
                float newHb = Mathf.Clamp(orig + delta, 0f, levelData.TimeToBeat(GetClipLength()));
                if (ShouldSnap()) newHb = Mathf.Round(newHb / quantStep) * quantStep;
                var ev = levelData.events[idx];
                float hitT = levelData.BeatToTime(newHb);
                float travel = GetTravelForEvent(ev);
                float spawnT = hitT - travel;
                ev.beat = levelData.TimeToBeat(spawnT);
                ev.time = spawnT;
                levelData.events[idx] = ev;
            }
            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= noteGos.Count || noteGos[idx] == null) continue;
                var ev = levelData.events[idx];
                float ht = GetHitTime(ev);
                float norm2 = Mathf.Clamp01(ht / GetClipLength());
                var r = noteGos[idx].GetComponent<RectTransform>();
                if (r != null) { r.anchorMin = new Vector2(norm2, 0.5f); r.anchorMax = new Vector2(norm2, 0.5f); }
            }
            if (dragNoteRect != null) { float norm = Mathf.Clamp01(levelData.BeatToTime(hitBeat) / GetClipLength()); dragNoteRect.anchorMin = new Vector2(norm, 0.5f); dragNoteRect.anchorMax = new Vector2(norm, 0.5f); }
            if (statusLabel != null) { if (dragOrigHitBeats.Count > 1) statusLabel.text = $"Перемещение {dragOrigHitBeats.Count} нот → {FormatTime(levelData.BeatToTime(hitBeat))} (Ctrl свободно)"; else statusLabel.text = $"Перемещение → {FormatTime(levelData.BeatToTime(hitBeat))} (Ctrl свободно)"; }
        }

        void EndNoteDrag()
        {
            if (!isDraggingNote) return;
            isDraggingNote = false; dragNoteRect = null;
            var origBeatsCopy = new Dictionary<int, float>(dragOrigHitBeats); float delta = dragDeltaBeat; dragOrigHitBeats.Clear();
            if (levelData != null)
            {
                levelData.SortByTime();

                if (origBeatsCopy.Count > 1)
                {
                    var newSelected = new HashSet<int>();
                    foreach (var kv in origBeatsCopy)
                    {
                        float newHb = Mathf.Clamp(kv.Value + delta, 0f, levelData.TimeToBeat(GetClipLength()));
                        if (ShouldSnap()) newHb = Mathf.Round(newHb / quantStep) * quantStep;
                        int bestIdx = -1; float bestDist = float.MaxValue;
                        for (int i = 0; i < levelData.events.Count; i++) { float hb = GetHitBeat(levelData.events[i]); float d = Mathf.Abs(hb - newHb); if (d < bestDist && d < 0.05f) { bestDist = d; bestIdx = i; } }
                        if (bestIdx >= 0) newSelected.Add(bestIdx);
                    }
                    selectedIndices = newSelected; selectedIndex = newSelected.Count > 0 ? new List<int>(newSelected)[new List<int>(newSelected).Count - 1] : -1;
                }
                else if (dragNoteIdx >= 0)
                {
                    int best = dragNoteIdx; float bestDist = float.MaxValue;
                    for (int i = 0; i < levelData.events.Count; i++) { float d = Mathf.Abs(GetHitBeat(levelData.events[i]) - (dragStartHitBeat + delta)); if (d < bestDist) { bestDist = d; best = i; } }
                    selectedIndex = best;
                    if (selectedIndices.Count == 1) selectedIndices.Clear();
                    if (selectedIndices.Count <= 1) { selectedIndices.Clear(); if (best >= 0) selectedIndices.Add(best); }
                }
                RefreshNotes(); RefreshPropertiesPanel();
            }
            dragNoteIdx = -1; dragDeltaBeat = 0f;
        }

        Vector2 GetScaledNoteSize()
        {
            if (!scaleNotesWithZoom) return noteSize;
            float t = Mathf.InverseLerp(0.25f, 4f, zoom);
            float s = Mathf.Lerp(noteZoomScaleMin, noteZoomScaleMax, t);
            if (scaleNotesWidthOnly) return new Vector2(noteSize.x * s, noteSize.y * Mathf.Lerp(1f, 1.08f, t));
            return noteSize * s;
        }
        void ApplyNoteZoomScale()
        {
            if (!scaleNotesWithZoom || noteGos == null) return;
            Vector2 baseScaled = GetScaledNoteSize();
            for (int i = 0; i < noteGos.Count; i++)
            {
                var go = noteGos[i];
                if (go == null) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;
                Vector2 scaled = baseScaled;

                if (levelData != null && i < levelData.events.Count)
                {
                    var ev = levelData.events[i];
                    if (ev.scale != Vector3.one && ev.scale != Vector3.zero)
                    {
                        float avg = (ev.scale.x + ev.scale.y + ev.scale.z) / 3f;
                        scaled *= Mathf.Clamp(avg, 0.6f, 2.2f);
                        if (scaled.x < 8) scaled = baseScaled;
                    }
                }
                rt.sizeDelta = scaled;
            }
        }
        Color GetColorForPrefab(int idx) { float h = (idx * 0.37f) % 1f; return Color.HSVToRGB(h, 0.78f, 0.92f); }

        void HandleWheelZoom()
        {
            if (timelineViewport == null && timelineContent == null) return;
            if (isDraggingNote || isScrubbingWaveform) return;
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

                if (Time.unscaledTime - lastGridRefreshTime > 0.05f) RefreshGrid(false);
                ApplyNoteZoomScale();
                UpdatePlayhead();
                if (_pendingZoomCursorTime >= 0f) { ApplyZoomCursorCorrection(_pendingZoomCursorTime, _pendingZoomCursorScreenPos); _pendingZoomCursorTime = -1f; }
                else UpdateScrollToPlayhead(false);
                return;
            }
            float prevZoom = zoom;
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, 1f / Mathf.Max(1f, zoomLerpSpeed), 10f, Time.unscaledDeltaTime);
            if (Mathf.Abs(zoom - targetZoom) < 0.005f) zoom = targetZoom;
            RefreshScrollContent();
            ApplyNoteZoomScale();

            if (Mathf.Abs(zoom - prevZoom) > 0.08f && Time.unscaledTime - lastGridRefreshTime > 0.1f) RefreshGrid(false);
            UpdatePlayhead();
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
            if (levelData != null && levelData.music != lastClip && levelData.music != null) RefreshAll();
            HandleWheelZoom();
            ProcessZoomLerp();
            if (waveformRect != null && waveformRect.localScale != Vector3.one)
            {
                waveformRect.localScale = Vector3.Lerp(waveformRect.localScale, Vector3.one, Time.unscaledDeltaTime * 12f);
                if ((waveformRect.localScale - Vector3.one).sqrMagnitude < 0.0001f) waveformRect.localScale = Vector3.one;
            }
            HandleKeyboard(); UpdateCurrentTimeFromAudio(); HandleMouseInput();
            if (isScrubbingWaveform && enableScrubAudio && audioSource != null && audioSource.isPlaying && !scrubPausedDueToStill)
            {
                if (Time.unscaledTime - lastScrubMoveTime > scrubStationaryPauseDelay) { audioSource.Pause(); scrubPausedDueToStill = true; }
            }
            UpdatePlayhead();
            if (autoScrollWithPlayhead)
            {
                if (audioSource != null && audioSource.isPlaying) UpdateScrollToPlayhead(false);
                else if (isScrubbingWaveform) UpdateScrollToPlayhead(false);
            }
            UpdateLabels(); UpdatePlayPauseLabel();
            if (loopPlayback && audioSource != null && audioSource.clip != null && audioSource.isPlaying) { if (audioSource.time >= audioSource.clip.length - 0.05f) { audioSource.time = 0f; audioSource.Play(); } }
        }

        void HandleKeyboard()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                var sel = EventSystem.current.currentSelectedGameObject;
                if (sel.GetComponent<TMPro.TMP_InputField>() != null) return;
                if (sel.GetComponent<UnityEngine.UI.InputField>() != null) return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { if (selectedIndex >= 0) DeselectNote(); else if (notePropertiesPanel != null && notePropertiesPanel.activeSelf) HidePropertiesPanel(); }
            if (Input.GetKeyDown(KeyCode.Space)) TogglePlayPause();
            if (Input.GetKeyDown(addNoteKey) || Input.GetKeyDown(altAddNoteKey)) AddNoteAtCurrentPlayhead();
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) { if (Input.GetKeyDown(KeyCode.A)) { selectedIndices.Clear(); for (int i = 0; i < levelData.events.Count; i++) selectedIndices.Add(i); selectedIndex = selectedIndices.Count > 0 ? new List<int>(selectedIndices)[0] : -1; RefreshNotes(); if (selectedIndex >= 0) ShowPropertiesPanel(selectedIndex); FlashStatus($"Выделено {selectedIndices.Count} нот (Ctrl+A)"); return; } }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (selectedIndices.Count > 1) RemoveSelectedNotes();
                else if (selectedIndex >= 0 && selectedIndex < levelData.events.Count) RemoveNoteAt(selectedIndex);
                else if (selectedIndices.Count == 1) { int idx = new List<int>(selectedIndices)[0]; RemoveNoteAt(idx); }
                else RemoveNearestNote();
            }
            for (int k = 1; k <= 12; k++) { if (Input.GetKeyDown(KeyCode.Alpha0 + k) || Input.GetKeyDown(KeyCode.Keypad0 + k)) { brushIndex = k - 1; int c = (catalog != null ? catalog.Count : 0); if (c == 0 && levelData != null) c = levelData.PrefabCount(catalog); if (c > 0) brushIndex = Mathf.Clamp(brushIndex, 0, c - 1); FlashStatus($"Кисть → {brushIndex} {(levelData != null && levelData.GetPrefab(brushIndex) ? levelData.GetPrefab(brushIndex).name : (catalog.GetPrefab(brushIndex) ? catalog.GetPrefab(brushIndex).name : ""))}"); } }
            if ((selectedIndex >= 0 || selectedIndices.Count > 0) && levelData != null && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                float dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1f : -1f;
                float step = Input.GetKey(KeyCode.LeftShift) ? quantStep * 0.5f : quantStep;
                if (!ShouldSnap()) step = 0.05f;
                if (selectedIndices.Count > 1) NudgeSelected(dir * step);
                else { int idx = selectedIndex >= 0 ? selectedIndex : new List<int>(selectedIndices)[0]; NudgeNote(idx, dir * step); }
            }
        }

        void HandleMouseInput()
        {
            if (isDraggingNote) return;
            RectTransform headerRect = playheadHeader;
            bool headerClick = headerRect != null && RectTransformUtility.RectangleContainsScreenPoint(headerRect, Input.mousePosition, GetCanvasCamera());
            if (headerClick)
            {
                if (Input.GetMouseButtonDown(0)) { UpdateAutoscrollLockFromScreenPos(Input.mousePosition); float t = GetTimeFromMouseHeader(Input.mousePosition); Seek(t, true); BeginScrub(); lastScrubScreenPos = Input.mousePosition; lastScrubMoveTime = Time.unscaledTime; }
                if (Input.GetMouseButton(0) && isScrubbingWaveform)
                {
                    Vector2 cur = Input.mousePosition;
                    float sq = (cur - lastScrubScreenPos).sqrMagnitude;
                    float t = GetTimeFromMouseHeader(cur);
                    bool moved = sq > scrubStillThresholdPxSq || Mathf.Abs(t - currentTime) > 0.006f;
                    if (moved) { UpdateAutoscrollLockFromScreenPos(cur); lastScrubScreenPos = cur; lastScrubMoveTime = Time.unscaledTime; if (scrubPausedDueToStill && enableScrubAudio && audioSource != null && audioSource.clip != null) { scrubPausedDueToStill = false; audioSource.volume = Mathf.Clamp01(scrubVolume); try { audioSource.time = Mathf.Clamp(t, 0f, audioSource.clip.length - 0.02f); } catch {} if (!audioSource.isPlaying) audioSource.Play(); } Seek(t, true); }
                    else { if (enableScrubAudio && audioSource != null && audioSource.isPlaying && Time.unscaledTime - lastScrubMoveTime > scrubStationaryPauseDelay) { audioSource.Pause(); scrubPausedDueToStill = true; } }
                }
                if (Input.GetMouseButtonUp(0) && isScrubbingWaveform) EndScrub();
                return;
            }
            RectTransform refRect = waveformRect != null ? waveformRect : timelineContent;
            if (refRect == null) return;
            if (Input.GetMouseButtonDown(0) && !IsPointerOverNote())
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(refRect, Input.mousePosition, GetCanvasCamera()))
                {
                    float t = GetTimeFromMouse(Input.mousePosition);
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand)) { Seek(t, true); BeginScrub(); lastScrubScreenPos = Input.mousePosition; lastScrubMoveTime = Time.unscaledTime; }
                    else AddNoteAtTime(t);
                }
            }
            if (Input.GetMouseButtonUp(0) && isScrubbingWaveform) EndScrub();
            if (isScrubbingWaveform && Input.GetMouseButton(0))
            {
                Vector2 cur = Input.mousePosition;
                float sq = (cur - lastScrubScreenPos).sqrMagnitude;
                RectTransform scrubRect = playheadHeader != null ? playheadHeader : refRect;
                float t = scrubRect == playheadHeader ? GetTimeFromMouseHeader(cur) : GetTimeFromMouse(cur);
                bool moved = sq > scrubStillThresholdPxSq || Mathf.Abs(t - currentTime) > 0.006f;
                if (moved) { lastScrubScreenPos = cur; lastScrubMoveTime = Time.unscaledTime; if (scrubPausedDueToStill && enableScrubAudio && audioSource != null && audioSource.clip != null) { scrubPausedDueToStill = false; audioSource.volume = Mathf.Clamp01(scrubVolume); try { audioSource.time = Mathf.Clamp(t, 0f, audioSource.clip.length - 0.02f); } catch {} if (!audioSource.isPlaying) audioSource.Play(); } Seek(t, true); }
                else { if (enableScrubAudio && audioSource != null && audioSource.isPlaying && Time.unscaledTime - lastScrubMoveTime > scrubStationaryPauseDelay) { audioSource.Pause(); scrubPausedDueToStill = true; } }
            }
        }

        float GetTimeFromMouseHeader(Vector2 screenPos)
        {
            RectTransform refRect = playheadHeader != null ? playheadHeader : (timelineContent != null ? timelineContent : waveformRect);
            if (refRect == null) return currentTime;
            Camera cam = GetCanvasCamera();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, screenPos, cam, out local)) return currentTime;
            Rect rect = refRect.rect;
            float norm = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            norm = Mathf.Clamp01(norm);
            return norm * GetClipLength();
        }

        void BeginScrub()
        {
            if (isScrubbingWaveform) return;
            isScrubbingWaveform = true;
            wasPlayingBeforeScrub = audioSource != null && audioSource.isPlaying;
            scrubWasPlaying = wasPlayingBeforeScrub;
            lastScrubScreenPos = Input.mousePosition;
            lastScrubMoveTime = Time.unscaledTime;
            scrubPausedDueToStill = false;
            if (audioSource == null || audioSource.clip == null) return;
            if (enableScrubAudio)
            {
                scrubSavedVolume = audioSource.volume;
                audioSource.volume = Mathf.Clamp01(scrubVolume);
                float target = Mathf.Clamp(currentTime, 0f, audioSource.clip.length - 0.02f);
                try { audioSource.time = target; } catch { audioSource.time = 0f; }
                audioSource.loop = false;
                if (!audioSource.isPlaying) audioSource.Play();
                CancelInvoke(nameof(StopScrubPreview));
            }
            else { if (wasPlayingBeforeScrub) audioSource.Pause(); }
        }

        void EndScrub()
        {
            if (!isScrubbingWaveform) return;
            isScrubbingWaveform = false;
            if (audioSource == null || audioSource.clip == null) { wasPlayingBeforeScrub = false; return; }
            if (enableScrubAudio)
            {
                audioSource.volume = scrubSavedVolume;
                if (!wasPlayingBeforeScrub) { CancelInvoke(nameof(StopScrubPreview)); Invoke(nameof(StopScrubPreview), Mathf.Max(0.05f, scrubAudibleDuration)); }
                else { if (!audioSource.isPlaying) { try { audioSource.time = Mathf.Clamp(currentTime, 0f, audioSource.clip.length - 0.02f); } catch {} audioSource.Play(); } }
            }
            else { if (wasPlayingBeforeScrub && !audioSource.isPlaying) { try { audioSource.time = Mathf.Clamp(currentTime, 0f, audioSource.clip.length - 0.02f); } catch {} audioSource.Play(); } }
            wasPlayingBeforeScrub = false;
        }

        void StopScrubPreview() { if (isScrubbingWaveform) return; if (!scrubWasPlaying && audioSource != null && audioSource.isPlaying) { audioSource.Pause(); audioSource.volume = scrubSavedVolume; } }

        bool IsPointerOverNote()
        {
            if (EventSystem.current == null) return false;
            var ray = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ray, results);
            foreach (var r in results) if (r.gameObject != null && r.gameObject.name.StartsWith("Note_")) return true;
            return false;
        }

        void UpdateCurrentTimeFromAudio()
        {
            if (audioSource == null || audioSource.clip == null) { if (audioSource != null && !audioSource.isPlaying) return; }
            if (audioSource.isPlaying) currentTime = audioSource.time;
            else if (!isScrubbingWaveform) currentTime = audioSource.time;
            currentTime = Mathf.Clamp(currentTime, 0f, GetClipLength() - 0.001f);
        }

        void UpdatePlayhead()
        {
            RectTransform refRect = waveformRect != null ? waveformRect : timelineContent;
            if (playheadRect == null || refRect == null || GetClipLength() < 0.01f) return;
            float norm = Mathf.Clamp01(currentTime / GetClipLength());
            float width = refRect.rect.width;
            if (width < 1f && refRect == timelineContent) width = refRect.sizeDelta.x;
            float x = norm * width;
            if (refRect == timelineContent) playheadRect.anchoredPosition = new Vector2(x, playheadRect.anchoredPosition.y);
            else { float xMin = refRect.rect.xMin; playheadRect.anchoredPosition = new Vector2(xMin + x, playheadRect.anchoredPosition.y); }
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
            float norm = Mathf.Clamp01(currentTime / GetClipLength());
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
        void UpdateLabels() { if (timeLabel != null) { float len = GetClipLength(); timeLabel.text = $"{FormatTime(currentTime)} / {FormatTime(len)}"; } if (beatLabel != null && beatLabel.gameObject.activeSelf) beatLabel.gameObject.SetActive(false); }
        void UpdatePlayPauseLabel() { if (playPauseLabel != null) { bool playing = audioSource != null && audioSource.isPlaying; playPauseLabel.text = playing ? "■" : "►"; } }
        string FormatTime(float t) { int m = Mathf.FloorToInt(t / 60f); int s = Mathf.FloorToInt(t % 60f); int ms = Mathf.FloorToInt((t - Mathf.Floor(t)) * 100); return $"{m:0}:{s:00}.{ms:00}"; }
        public void OnWaveformDrag(BaseEventData data) { var ped = data as PointerEventData; if (ped != null) { Seek(GetTimeFromMouse(ped.position), true); if (!isScrubbingWaveform) BeginScrub(); lastScrubScreenPos = ped.position; lastScrubMoveTime = Time.unscaledTime; } }
        public void OnWaveformPointerUp(BaseEventData data) { if (isScrubbingWaveform) EndScrub(); }
        void OnHeaderPointerDown(BaseEventData data) { var ped = data as PointerEventData; if (ped == null) return; UpdateAutoscrollLockFromScreenPos(ped.position); float t = GetTimeFromMouseHeader(ped.position); Seek(t, true); BeginScrub(); lastScrubScreenPos = ped.position; lastScrubMoveTime = Time.unscaledTime; }
        void OnHeaderDrag(BaseEventData data) { var ped = data as PointerEventData; if (ped == null) return; UpdateAutoscrollLockFromScreenPos(ped.position); float t = GetTimeFromMouseHeader(ped.position); Seek(t, true); lastScrubScreenPos = ped.position; lastScrubMoveTime = Time.unscaledTime; }
        public void AddNoteAtCurrentPlayhead() => AddNoteAtTime(currentTime);
        public void AddNoteAtTime(float hitTime)
        {
            if (levelData == null) { FlashStatus("Нет LevelData!"); return; }
            if (levelData.music == null && audioSource != null && audioSource.clip != null) levelData.music = audioSource.clip;
            if (levelData.music == null) { FlashStatus("Нет музыки — загрузите аудио!"); return; }
            int gCountAdd = (catalog != null ? catalog.Count : 0); if (gCountAdd == 0) gCountAdd = levelData.PrefabCount(catalog);
            if (gCountAdd == 0) { FlashStatus("Нет префабов! Заполни GlobalObstacleCatalog в Resources/"); return; }
            float hitBeat = levelData.TimeToBeat(hitTime);
            if (ShouldSnap()) hitBeat = Mathf.Round(hitBeat / quantStep) * quantStep;
            hitBeat = Mathf.Clamp(hitBeat, 0f, levelData.TimeToBeat(levelData.music.length) - 0.1f);
            float hitTimeQ = levelData.BeatToTime(hitBeat);
            foreach (var ev2 in levelData.events) if (Mathf.Abs(GetHitBeat(ev2) - hitBeat) < 0.02f) { FlashStatus($"Уже есть нота на {FormatTime(hitTimeQ)}"); return; }
            float speed = GetSpeedForBrush();
            float travel = GetTravelForSpeed(speed);
            float spawnTime = hitTimeQ - travel;
            float spawnBeat = levelData.TimeToBeat(spawnTime);


            int maxIdx = (catalog != null ? catalog.Count : 0) > 0 ? (catalog != null ? catalog.Count : 0) - 1 : Mathf.Max(0, levelData.PrefabCount(catalog) - 1);
            brushIndex = Mathf.Clamp(brushIndex, 0, maxIdx);
            var ev = ObstacleEvent.Create(spawnBeat, brushIndex, Vector3.zero, speed);
            ev.time = spawnTime;
            levelData.events.Add(ev);
            levelData.SortByTime();

            int newIdx = levelData.events.IndexOf(ev);
            if (newIdx < 0) for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hitBeat) < 0.01f) { newIdx = i; break; }
            selectedIndex = newIdx >= 0 ? newIdx : levelData.events.Count - 1;
            selectedIndices.Clear(); selectedIndices.Add(selectedIndex);
            RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            FlashStatus($"+ Нота {FormatTime(hitTimeQ)} → спавн {FormatTime(spawnTime)}  #{brushIndex}  Ctrl — свободно");
        }
        public void RemoveNoteAt(int idx)
        {
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) return;
            levelData.events.RemoveAt(idx);

            selectedIndex = Mathf.Clamp(idx - 1, -1, levelData.events.Count - 1);
            selectedIndices.Clear();
            if (selectedIndex >= 0) selectedIndices.Add(selectedIndex);
            RefreshNotes();
            if (selectedIndices.Count == 0) HidePropertiesPanel();
            FlashStatus($"Удалена нота #{idx}");
        }
        public void RemoveNearestNote()
        {
            if (levelData == null || levelData.events.Count == 0) return;
            float curBeat = levelData.TimeToBeat(currentTime);
            int best = -1; float bestDist = float.MaxValue;
            for (int i = 0; i < levelData.events.Count; i++) { float hb = GetHitBeat(levelData.events[i]); float d = Mathf.Abs(hb - curBeat); if (d < bestDist) { bestDist = d; best = i; } }
            if (best >= 0 && bestDist <= noteDeleteThresholdBeats + 0.5f) RemoveNoteAt(best);
            else FlashStatus("Нет ноты рядом");
        }
        public void SelectNote(int idx) { selectedIndices.Clear(); selectedIndices.Add(idx); selectedIndex = idx; RefreshNotes(); if (idx >= 0 && idx < levelData.events.Count) { FlashStatus($"Выбрано #{idx}  {FormatTime(GetHitTime(levelData.events[idx]))} — Ctrl+клик множ."); ShowPropertiesPanel(idx); } else HidePropertiesPanel(); }
        public void ToggleSelectNote(int idx) { if (selectedIndices.Contains(idx)) { selectedIndices.Remove(idx); if (selectedIndex == idx) selectedIndex = selectedIndices.Count > 0 ? new List<int>(selectedIndices)[selectedIndices.Count - 1] : -1; } else { selectedIndices.Add(idx); selectedIndex = idx; } RefreshNotes(); if (selectedIndex >= 0) ShowPropertiesPanel(selectedIndex); else HidePropertiesPanel(); FlashStatus($"Выделено {selectedIndices.Count} нот"); }
        public void DeselectNote() { selectedIndex = -1; selectedIndices.Clear(); RefreshNotes(); HidePropertiesPanel(); }
        public static bool TryParseFloat(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        Button MakePropButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, Color bg)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var le = go.AddComponent<LayoutElement>(); le.minHeight = 30; le.flexibleWidth = 1f;
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var trt = txtGO.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>(); tmp.text = text; tmp.fontSize = 13; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            return btn;
        }

        void EnsurePropertiesUI()
        {
            if (notePropertiesPanel == null) return;
            Transform parent = notePropertiesPanel.transform;
            var scroll = notePropertiesPanel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.content != null) parent = scroll.content;
            if (parent.GetComponent<VerticalLayoutGroup>() == null)
            {
                var pvlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
                pvlg.spacing = 6;
                pvlg.padding = new RectOffset(8, 8, 8, 8);
                pvlg.childAlignment = TextAnchor.UpperCenter;
                pvlg.childControlWidth = true;
                pvlg.childControlHeight = false;
                pvlg.childForceExpandWidth = true;
                pvlg.childForceExpandHeight = false;
                var pcsf = parent.gameObject.AddComponent<ContentSizeFitter>();
                pcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (propTitleLabel == null)
            {
                var titleGO = new GameObject("Title", typeof(RectTransform));
                titleGO.transform.SetParent(parent, false);
                var ttmp = titleGO.AddComponent<TextMeshProUGUI>(); ttmp.fontSize = 14; ttmp.fontStyle = FontStyles.Bold; ttmp.alignment = TextAlignmentOptions.Center; ttmp.color = Color.white;
                var tle = titleGO.AddComponent<LayoutElement>(); tle.minHeight = 24; tle.flexibleWidth = 1f;
                propTitleLabel = ttmp;
            }

            if (propPrefabButtonLabel == null)
            {
                var btn = MakePropButton(parent, "Вид", () => ShowPrefabGrid(), new Color(0.16f, 0.22f, 0.32f, 1f));
                btn.name = "PrefabButton";
                propPrefabButtonLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            }

            EnsurePropSpeedRow();

            if (parent.Find("DeleteCloseRow") == null)
            {
                var rowGO = new GameObject("DeleteCloseRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                rowGO.transform.SetParent(parent, false);
                var hlg = rowGO.GetComponent<HorizontalLayoutGroup>(); hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleCenter; hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
                var rle = rowGO.AddComponent<LayoutElement>(); rle.minHeight = 30;
                var del = MakePropButton(rowGO.transform, "× Удалить", () => OnPropDelete(), new Color(0.5f, 0.2f, 0.2f, 1f));
                del.name = "DeleteButton";
                var close = MakePropButton(rowGO.transform, "Закрыть", () => DeselectNote(), new Color(0.25f, 0.25f, 0.28f, 1f));
                close.name = "CloseButton";
            }
        }

        void EnsurePropSpeedRow()
        {
            if (propSpeedInput != null || notePropertiesPanel == null) return;
            Transform parent = notePropertiesPanel.transform;
            var scroll = notePropertiesPanel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.content != null) parent = scroll.content;
            if (parent.GetComponent<VerticalLayoutGroup>() == null)
            {
                var pvlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
                pvlg.spacing = 6;
                pvlg.padding = new RectOffset(8, 8, 8, 8);
                pvlg.childAlignment = TextAnchor.UpperCenter;
                pvlg.childControlWidth = true;
                pvlg.childControlHeight = false;
                pvlg.childForceExpandWidth = true;
                pvlg.childForceExpandHeight = false;
                var pcsf = parent.gameObject.AddComponent<ContentSizeFitter>();
                pcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            var rowGO = new GameObject("SpeedRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGO.transform.SetParent(parent, false);
            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>(); hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleLeft; hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var rle = rowGO.AddComponent<LayoutElement>(); rle.minHeight = 30;

            var labGO = new GameObject("Label", typeof(RectTransform));
            labGO.transform.SetParent(rowGO.transform, false);
            var ltmp = labGO.AddComponent<TextMeshProUGUI>(); ltmp.text = "Скорость"; ltmp.fontSize = 13; ltmp.alignment = TextAlignmentOptions.MidlineLeft; ltmp.color = new Color(1, 1, 1, 0.85f);
            var lle = labGO.AddComponent<LayoutElement>(); lle.minWidth = 90; lle.preferredWidth = 90;

            var inGO = new GameObject("SpeedInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inGO.transform.SetParent(rowGO.transform, false);
            var iimg = inGO.GetComponent<Image>(); iimg.color = new Color(0, 0, 0, 0.35f);
            var input = inGO.GetComponent<TMP_InputField>();
            var areaGO = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGO.transform.SetParent(inGO.transform, false);
            var art = areaGO.GetComponent<RectTransform>(); art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = new Vector2(6, 2); art.offsetMax = new Vector2(-6, -2);
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(areaGO.transform, false);
            var trt = txtGO.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var ttmp = txtGO.AddComponent<TextMeshProUGUI>(); ttmp.fontSize = 13; ttmp.color = Color.white; ttmp.alignment = TextAlignmentOptions.MidlineLeft;
            var phGO = new GameObject("Placeholder", typeof(RectTransform));
            phGO.transform.SetParent(areaGO.transform, false);
            var prt = phGO.GetComponent<RectTransform>(); prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            var ptmp = phGO.AddComponent<TextMeshProUGUI>(); ptmp.text = "авто"; ptmp.fontSize = 13; ptmp.color = new Color(1, 1, 1, 0.35f); ptmp.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = art;
            input.textComponent = ttmp;
            input.placeholder = ptmp;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 7;
            var ile = inGO.AddComponent<LayoutElement>(); ile.minHeight = 30; ile.flexibleWidth = 1f;
            propSpeedInput = input;
            propSpeedInput.onEndEdit.AddListener(_ => ApplyPropertiesFromPanel());
        }

        void ShowPropertiesPanel(int idx)
        {
            if (notePropertiesPanel == null)
            {

                Debug.LogWarning("[TimelineUI] notePropertiesPanel не назначен.", this);
                return;
            }
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) { HidePropertiesPanel(); return; }
            notePropertiesPanel.SetActive(true);
            var ev = levelData.events[idx];
            float hitTime = GetHitTime(ev);
            string speedTxt = ev.speed > 0.01f ? $" • {ev.speed:0.#} м/с" : " • авто";
            if (selectedIndices.Count > 1 && propTitleLabel != null) propTitleLabel.text = $"Выделено {selectedIndices.Count} нот";
            else if (propTitleLabel != null) propTitleLabel.text = $"Нота #{idx} — {FormatTime(hitTime)} • {GetPrefabName(ev.prefabIndex)}{speedTxt}";

            if (propPrefabIndexInput != null) { propPrefabIndexInput.gameObject.SetActive(true); propPrefabIndexInput.SetTextWithoutNotify(ev.prefabIndex.ToString()); }
            EnsurePropertiesUI();
            if (propPrefabButtonLabel != null) propPrefabButtonLabel.text = $"Вид: {GetPrefabName(ev.prefabIndex)}";
            if (propSpeedInput != null) { propSpeedInput.gameObject.SetActive(true); propSpeedInput.SetTextWithoutNotify(ev.speed > 0.01f ? ev.speed.ToString("0.##", CultureInfo.InvariantCulture) : ""); }
            if (propPrefabDropdown != null)
            {
                propPrefabDropdown.gameObject.SetActive(true);
                RefreshPrefabDropdown();
                propPrefabDropdown.SetValueWithoutNotify(Mathf.Clamp(ev.prefabIndex, 0, propPrefabDropdown.options.Count-1));
                var thumb = propPrefabDropdown.transform.Find("Thumb");
                if (thumb == null && propPrefabDropdown.template != null) thumb = propPrefabDropdown.template.Find("Thumb");
            }
            if (prefabGridPanel != null) prefabGridPanel.SetActive(false);
            EnsurePrefabGrid();
        }

        public void OnPropDelete()
        {
            if (selectedIndices.Count > 1) RemoveSelectedNotes();
            else if (selectedIndex >= 0) RemoveNoteAt(selectedIndex);
            HidePropertiesPanel();
        }

        string GetPrefabName(int idx)
        {
            var pf = catalog != null ? catalog.GetPrefab(idx) : null;
            if (pf == null && levelData != null) pf = levelData.GetPrefab(idx, catalog);
            return pf != null ? pf.name : $"#{idx}";
        }
        public void HidePropertiesPanel() { if (notePropertiesPanel != null) notePropertiesPanel.SetActive(false); if (prefabGridPanel != null) prefabGridPanel.SetActive(false); }
        void RefreshPropertiesPanel() { if (selectedIndex >= 0 && notePropertiesPanel != null && notePropertiesPanel.activeSelf) ShowPropertiesPanel(selectedIndex); }
        public void ApplyPropertiesFromPanel()
        {
            if (levelData == null || selectedIndex < 0 || selectedIndex >= levelData.events.Count) return;
            var ev = levelData.events[selectedIndex];

            int newPrefab = ev.prefabIndex;
            if (propPrefabDropdown != null && propPrefabDropdown.options.Count > 0) newPrefab = Mathf.Clamp(propPrefabDropdown.value, 0, Mathf.Max(0, ((catalog != null ? catalog.Count : 0)>0?(catalog != null ? catalog.Count : 0):levelData.PrefabCount(catalog))-1));
            else if (propPrefabIndexInput != null && int.TryParse(propPrefabIndexInput.text, out int pi)) { int total = (catalog != null ? catalog.Count : 0); if (total == 0) total = levelData.PrefabCount(catalog); newPrefab = Mathf.Clamp(pi, 0, Mathf.Max(0, total - 1)); }

            var tgt = selectedIndices.Count > 1 ? new System.Collections.Generic.List<int>(selectedIndices) : new System.Collections.Generic.List<int>{selectedIndex};
            float newSpeed = ev.speed;
            bool hasSpeed = false;
            if (propSpeedInput != null && propSpeedInput.gameObject.activeInHierarchy)
            {
                if (string.IsNullOrWhiteSpace(propSpeedInput.text)) { newSpeed = 0f; hasSpeed = true; }
                else if (TryParseFloat(propSpeedInput.text, out float sv)) { newSpeed = Mathf.Clamp(sv, 0f, 60f); hasSpeed = true; }
                else FlashStatus("Скорость: число м/с (пусто = авто)");
            }
            foreach (var ti in tgt)
            {
                if (ti < 0 || ti >= levelData.events.Count) continue;
                var ee = levelData.events[ti];
                ee.prefabIndex = newPrefab;
                if (hasSpeed && Mathf.Abs(ee.speed - newSpeed) > 0.001f)
                {
                    float hb = GetHitBeat(ee);
                    ee.speed = newSpeed;
                    float travel = GetTravelForEvent(ee);
                    float spawnT = levelData.BeatToTime(hb) - travel;
                    ee.beat = levelData.TimeToBeat(spawnT);
                    ee.time = spawnT;
                }
                levelData.events[ti] = ee;
            }
            preview?.ForceRefresh();
            levelData.SortByTime();

            for (int i=0;i<levelData.events.Count;i++) if (tgt.Contains(i) || levelData.events[i].prefabIndex==newPrefab) {  }
            RefreshNotes(); ShowPropertiesPanel(selectedIndex);
            FlashStatus($"Вид → {GetPrefabName(newPrefab)}");
        }
        public void NudgeNote(int idx, float beatDelta)
        {
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) return;
            var ev = levelData.events[idx];
            float hb = GetHitBeat(ev) + beatDelta;
            hb = Mathf.Clamp(hb, 0f, levelData.TimeToBeat(GetClipLength()));
            if (ShouldSnap()) hb = Mathf.Round(hb / quantStep) * quantStep;
            float hitT = levelData.BeatToTime(hb);
            float travel = GetTravelForEvent(ev);
            float spawnT = hitT - travel;
            ev.beat = levelData.TimeToBeat(spawnT);
            ev.time = spawnT;
            levelData.events[idx] = ev;
            levelData.SortByTime();

            for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hb) < 0.01f) { selectedIndex = i; break; }
            RefreshNotes();
        }
        public int ResnapAllNotes()
        {
            if (levelData == null || levelData.events.Count == 0) return 0;
            float q = Mathf.Max(0.05f, quantStep);
            float maxBeat = levelData.music != null ? levelData.TimeToBeat(levelData.music.length) : float.MaxValue;
            int n = 0;
            for (int i = 0; i < levelData.events.Count; i++)
            {
                var ev = levelData.events[i];
                float snapped = Mathf.Clamp(Mathf.Round(GetHitBeat(ev) / q) * q, 0f, maxBeat);
                if (Mathf.Abs(snapped - GetHitBeat(ev)) < 1e-4f) continue;
                float travel = GetTravelForEvent(ev);
                float spawnT = levelData.BeatToTime(snapped) - travel;
                ev.beat = levelData.TimeToBeat(spawnT);
                ev.time = spawnT;
                levelData.events[i] = ev;
                n++;
            }
            levelData.SortByTime();
            selectedIndex = -1; selectedIndices.Clear();
            RefreshNotes(); HidePropertiesPanel();
            preview?.ForceRefresh();
            return n;
        }
        public void ClearAllNotes()
        {
            if (levelData == null) return;
            levelData.events.Clear();

            selectedIndex = -1; selectedIndices.Clear(); RefreshNotes(); HidePropertiesPanel(); FlashStatus("Все ноты удалены");
        }
        public void RemoveSelectedNotes()
        {
            if (levelData == null || selectedIndices.Count == 0) return;
            var sorted = new List<int>(selectedIndices); sorted.Sort((a, b) => b.CompareTo(a));
            foreach (var idx in sorted) if (idx >= 0 && idx < levelData.events.Count) levelData.events.RemoveAt(idx);

            int cnt = sorted.Count; selectedIndices.Clear(); selectedIndex = -1; HidePropertiesPanel(); RefreshNotes(); FlashStatus($"Удалено {cnt} нот");
        }
        public void NudgeSelected(float beatDelta)
        {
            if (levelData == null || selectedIndices.Count == 0) return;
            var newHits = new Dictionary<int, float>();
            foreach (var idx in selectedIndices)
            {
                if (idx < 0 || idx >= levelData.events.Count) continue;
                float hb = GetHitBeat(levelData.events[idx]) + beatDelta;
                hb = Mathf.Clamp(hb, 0f, levelData.TimeToBeat(GetClipLength()));
                if (ShouldSnap()) hb = Mathf.Round(hb / quantStep) * quantStep;
                newHits[idx] = hb;
            }
            foreach (var kv in newHits)
            {
                int idx = kv.Key; float hb = kv.Value;
                var ev = levelData.events[idx];
                float hitT = levelData.BeatToTime(hb);
                float travel = GetTravelForEvent(ev);
                float spawnT = hitT - travel;
                ev.beat = levelData.TimeToBeat(spawnT);
                ev.time = spawnT;
                levelData.events[idx] = ev;
            }
            levelData.SortByTime();

            var newSelected = new HashSet<int>();
            foreach (var kv in newHits)
            {
                float hb = kv.Value;
                if (ShouldSnap()) hb = Mathf.Round(hb / quantStep) * quantStep;
                for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hb) < 0.02f) { newSelected.Add(i); break; }
            }
            selectedIndices = newSelected;
            if (newSelected.Count > 0) selectedIndex = new List<int>(newSelected)[0];
            RefreshNotes(); RefreshPropertiesPanel();
        }
        float GetClipLength()
        {
            if (levelData != null && levelData.music != null) return levelData.music.length;
            if (audioSource != null && audioSource.clip != null) return audioSource.clip.length;
            return 0f;
        }
        float GetTravelForSpeed(float speed)
        {
            if (speed < 0.1f) speed = defaultNoteSpeed;
            if (manager != null) return manager.GetTravelTime(speed);
            return 52f / Mathf.Max(1f, speed);
        }
        float GetTravelForEvent(ObstacleEvent ev)
        {
            float s = ev.speed;
            if (s < 0.1f && levelData != null) { var pf = levelData.GetPrefab(ev.prefabIndex, catalog); if (pf) { var ob = pf.GetComponent<Obstacle>(); if (ob) s = ob.baseSpeed; } }
            if (s < 0.1f) s = defaultNoteSpeed;
            return GetTravelForSpeed(s);
        }
        float GetHitTime(ObstacleEvent ev) => ev.time + GetTravelForEvent(ev);
        float GetHitBeat(ObstacleEvent ev) => levelData != null ? levelData.TimeToBeat(GetHitTime(ev)) : ev.beat;
        float GetSpeedForBrush()
        {
            float s = defaultNoteSpeed;

            var pf = catalog != null ? catalog.GetPrefab(brushIndex) : null;
            if (!pf && levelData != null) pf = levelData.GetPrefab(brushIndex, catalog);
            if (pf) { var ob = pf.GetComponent<Obstacle>(); if (ob && ob.baseSpeed > 0.1f) s = ob.baseSpeed; }
            if (s < 0.1f) s = defaultNoteSpeed;
            return s;
        }
        float GetTimeFromMouse(Vector2 screenPos)
        {
            RectTransform refRect = timelineContent != null ? timelineContent : waveformRect;
            if (refRect == null) refRect = waveformRect;
            if (refRect == null) return currentTime;
            Camera cam = GetCanvasCamera();
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, screenPos, cam, out local)) return currentTime;
            Rect rect = refRect.rect;
            float norm = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            norm = Mathf.Clamp01(norm);
            return norm * GetClipLength();
        }
        Camera GetCanvasCamera() { if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>(); if (rootCanvas == null) return null; return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera; }
        public void Seek(float time) { Seek(time, false); }
        public void Seek(float time, bool isScrubMove)
        {
            if (GetClipLength() < 0.01f) return;
            time = Mathf.Clamp(time, 0f, GetClipLength() - 0.01f);
            if (isScrubbingWaveform && enableScrubAudio && !isScrubMove && Mathf.Abs(time - currentTime) < 0.005f)
            {
                currentTime = time;
                UpdatePlayhead();
                if (autoScrollWithPlayhead) UpdateScrollToPlayhead(false);
                UpdateLabels();
                return;
            }
            currentTime = time;
            if (audioSource != null && audioSource.clip != null)
            {
                if (isScrubbingWaveform && enableScrubAudio)
                {
                    if (scrubPausedDueToStill) { }
                    else
                    {
                        if (Mathf.Abs(audioSource.time - time) > 0.012f) try { audioSource.time = time; } catch { audioSource.time = time; }
                        if (!audioSource.isPlaying) audioSource.Play();
                    }
                }
                else
                {
                    bool wasPlaying = audioSource.isPlaying;
                    try { audioSource.time = time; } catch {}
                    if (wasPlaying && !audioSource.isPlaying) audioSource.Play();
                }
            }
            UpdatePlayhead();
            if (autoScrollWithPlayhead) UpdateScrollToPlayhead(false);
            UpdateLabels();
        }
        public void SeekNormalized(float norm) => Seek(norm * GetClipLength());
        public float GetCurrentTime() => currentTime;
        public float GetCurrentBeat() => levelData != null ? levelData.TimeToBeat(currentTime) : 0f;
        public float GetNormalizedTime() => GetClipLength() > 0.01f ? currentTime / GetClipLength() : 0f;
        public Texture2D GetWaveformTexture() => waveformTex;
        public float[] GetWaveformData() => waveformData;
        public int GetSelectedIndex() => selectedIndex;
        public void Play() => PlayFromTime(currentTime);
        public void Pause() { if (audioSource != null) audioSource.Pause(); }
        public void PlayFromTime(float t)
        {
            if (GetClipLength() < 0.01f) { FlashStatus("Нет аудио"); return; }
            AudioClip clip = levelData != null && levelData.music != null ? levelData.music : (audioSource != null ? audioSource.clip : null);
            if (clip == null) { FlashStatus("Нет клипа"); return; }
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            if (audioSource.clip != clip) audioSource.clip = clip;
            audioSource.time = Mathf.Clamp(t, 0f, clip.length - 0.02f);
            audioSource.volume = 1f;
            audioSource.Play();
            currentTime = audioSource.time;
            UpdatePlayPauseLabel();
        }
        public void TogglePlayPause() { if (audioSource != null && audioSource.isPlaying) Pause(); else Play(); }
        public void SetQuant(float q) { quantStep = q; RefreshGrid(true); }
        public void SetBrush(int idx) { int c = (catalog != null ? catalog.Count : 0); if (c == 0 && levelData != null) c = levelData.PrefabCount(catalog); if (c == 0) c = 7; brushIndex = Mathf.Clamp(idx, 0, c - 1); FlashStatus($"Кисть {brushIndex}"); }
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
            bool isNewClip = lastClip != clip;
            bool wasEmptyLevel = levelData.events.Count > 0 && string.IsNullOrEmpty(levelData.fullTitle) && string.IsNullOrEmpty(levelData.songAuthor);
            levelData.music = clip; levelData.audioPath = path;

            if (wasEmptyLevel || (isNewClip && levelData.events.Count > 0 && lastClip == null))
            {
                levelData.events.Clear();
                selectedIndex = -1; selectedIndices.Clear();
            }
            if (audioSource != null) { audioSource.clip = clip; audioSource.Stop(); currentTime = 0f; }


            if (showBpmAfterLoad && clip != null)
            {
                try
                {
                    var det = BpmDetector.Detect(clip);

                    levelData.bpm = Mathf.Clamp(det.bpm, 40f, 300f);
                    string bpmTxt = $"BPM: {levelData.bpm:0}";

                    if (bpmOutputText != null) bpmOutputText.text = bpmTxt;
                    Debug.Log($"[BPM] {clip.name} -> {det.bpm:0.##} conf {det.confidence:0.##} (сохранен {levelData.bpm:0})", this);
                    FlashStatus($"Загружено: {clip.name}  {clip.length:0.0}с • {bpmTxt} — ставь ноты (Enter/Space/клик)");
                    lastClip = null; lastWaveformGenWidth = -1; lastGridClipLen = -1f;
                    RefreshAll();
                    if (bpmOutputText != null) bpmOutputText.text = bpmTxt;
                    return;
                } catch (System.Exception e) { Debug.LogWarning($"[BPM] detect failed: {e.Message}"); }
            }

            lastClip = null; lastWaveformGenWidth = -1; lastGridClipLen = -1f;
            RefreshAll();
            FlashStatus($"Загружено: {clip.name}  {clip.length:0.0}с • BPM {levelData.bpm:0.##} — ставь ноты (Enter/Space/клик)");
        }
        void FlashStatus(string msg) { if (statusLabel != null) statusLabel.text = msg; Debug.Log($"[Timeline] {msg}", this); CancelInvoke(nameof(ClearStatus)); Invoke(nameof(ClearStatus), 3f); }
        void ClearStatus() { if (statusLabel != null) statusLabel.text = ""; }

        [ContextMenu("Перестроить вейвформу")] void ContextRebuild() { lastClip = null; lastWaveformGenWidth = -1; RefreshAll(); }
        [ContextMenu("Добавить ноту")] void ContextAdd() => AddNoteAtCurrentPlayhead();
        [ContextMenu("Очистить")] void ContextClear() => ClearAllNotes();
    }
}
