using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
public interface ITimelineNoteView { void Setup(int index, ObstacleEvent ev, float hitBeat, bool selected); }
public class TimelineUI : MonoBehaviour
{
    public RhythmLevelData levelData;
    public AudioSource audioSource;
    public RhythmParkourManager manager;
    public FileLoader fileLoader;
    public ScrollRect timelineScrollRect;
    public RectTransform timelineContent;
    public RectTransform timelineViewport;
    public RawImage waveformImage;
    public RectTransform waveformRect;
    public RectTransform notesContainer;
    public RectTransform gridContainer;
    public RectTransform playheadRect;
    public RectTransform playheadHeader;
    public Slider timelineSlider;
    public GameObject warningObject;
    public GameObject timelineObject;
    public Button playPauseButton;
    public TextMeshProUGUI playPauseLabel;
    public TextMeshProUGUI timeLabel;
    public TextMeshProUGUI statusLabel;
    [HideInInspector] public TextMeshProUGUI beatLabel;
    public GameObject notePrefab;
    public Vector2 noteSize = new Vector2(36f, 48f);
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
    public Color gridBarColor = new Color(1f, 1f, 1f, 0.22f);
    public Color gridBeatColor = new Color(1f, 1f, 1f, 0.10f);
    public Color gridHalfColor = new Color(1f, 1f, 1f, 0.05f);
    public int waveformTexWidth = 2048;
    public int waveformTexHeight = 110;
    public float pixelsPerSecond = 120f;
    public float zoom = 1f;
    public bool autoScrollWithPlayhead = true;
    public float autoScrollMargin = 0.3f;
    public bool followSlider = true;
    public Button followToggleButton;
    public TextMeshProUGUI followToggleLabel;
    public bool isSelectionMode = false;
    public Button selectionModeButton;
    public RectTransform selectionBoxRect;
    public Image selectionBoxImage;
    public Color selectionBoxColor = new Color(0.2f, 0.6f, 1f, 0.25f);
    public GameObject notePropertiesPanel;
    public TMP_InputField propHitBeatInput;
    public TMP_InputField propHitTimeInput;
    public TMP_InputField propSpawnBeatInput;
    public TMP_InputField propSpeedInput;
    public TMP_InputField propPrefabIndexInput;
    public Button propApplyButton;
    public Button propDeleteButton;
    public Button propCloseButton;
    public TextMeshProUGUI propTitleLabel;
    Texture2D waveformTex;
    float[] waveformData;
    AudioClip lastClip;
    float currentTime;
    bool isScrubbingWaveform;
    bool isDraggingSlider;
    bool wasPlayingBeforeScrub;
    int selectedIndex = -1;
    HashSet<int> selectedIndices = new HashSet<int>();
    List<GameObject> noteGos = new List<GameObject>();
    Canvas rootCanvas;
    bool isDraggingNote;
    int dragNoteIdx = -1;
    RectTransform dragNoteRect;
    Dictionary<int, float> dragOrigHitBeats = new Dictionary<int, float>();
    float dragStartHitBeat;
    float dragDeltaBeat;
    bool isRectSelecting;
    Vector2 rectStartLocalPos;
    Vector2 rectStartScreenPos;
    void Awake()
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (audioSource == null)
        {
            var previewGO = GameObject.Find("TimelinePreviewAudio");
            if (previewGO != null) audioSource = previewGO.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                var go = new GameObject("TimelinePreviewAudio");
                go.transform.SetParent(transform, false);
                audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f;
            }
        }
        if (levelData == null && manager != null) levelData = manager.levelData;
        if (fileLoader == null) fileLoader = FindObjectOfType<FileLoader>();
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();
    }
    void Start()
    {
        if (waveformRect == null && timelineContent != null) waveformRect = timelineContent;
        if (timelineViewport == null && timelineScrollRect != null) timelineViewport = timelineScrollRect.viewport;
        if (waveformRect == null && waveformImage != null) waveformRect = waveformImage.rectTransform;
        BindEvents();
        if (fileLoader != null) fileLoader.onFileLoaded.AddListener(OnFileLoaded);
        if (notePropertiesPanel != null) notePropertiesPanel.SetActive(false);
        if (selectionBoxRect != null) selectionBoxRect.gameObject.SetActive(false);
        RefreshAll();
        currentTime = 0f;
        UpdatePlayhead();
        UpdateSliderFromTime();
        UpdateScrollToPlayhead(true);
        UpdateFollowToggleVisual();
        UpdateSelectionModeButtonVisual();
    }
    void OnDestroy()
    {
        if (fileLoader != null) fileLoader.onFileLoaded.RemoveListener(OnFileLoaded);
        if (timelineSlider != null) timelineSlider.onValueChanged.RemoveListener(OnSliderChanged);
        if (playPauseButton != null) playPauseButton.onClick.RemoveListener(TogglePlayPause);
        if (followToggleButton != null) followToggleButton.onClick.RemoveListener(ToggleFollow);
        if (waveformTex != null) Destroy(waveformTex);
    }
    void OnValidate()
    {
        quantStep = Mathf.Clamp(quantStep, 0.1f, 4f);
        brushIndex = Mathf.Max(0, brushIndex);
        waveformTexWidth = Mathf.Clamp(waveformTexWidth, 256, 4096);
        waveformTexHeight = Mathf.Clamp(waveformTexHeight, 32, 256);
        pixelsPerSecond = Mathf.Clamp(pixelsPerSecond, 10f, 1000f);
        zoom = Mathf.Clamp(zoom, 0.25f, 4f);
#if UNITY_EDITOR
        if (timelineContent != null && Application.isPlaying) RefreshScrollContent();
#endif
    }
    public void SetZoom(float z) { zoom = Mathf.Clamp(z, 0.25f, 4f); RefreshScrollContent(); RefreshGrid(); }
    public void SetPixelsPerSecond(float pps) { pixelsPerSecond = Mathf.Clamp(pps, 10f, 1000f); RefreshScrollContent(); }
    void BindEvents()
    {
        BindPropertiesPanel();
        if (playPauseButton != null) { playPauseButton.onClick.RemoveListener(TogglePlayPause); playPauseButton.onClick.AddListener(TogglePlayPause); }
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.RemoveListener(OnSliderChanged);
            timelineSlider.onValueChanged.AddListener(OnSliderChanged);
            var et = timelineSlider.gameObject.GetComponent<EventTrigger>();
            if (et == null) et = timelineSlider.gameObject.AddComponent<EventTrigger>();
            EnsureEvent(et, EventTriggerType.PointerDown, data => { isDraggingSlider = true; wasPlayingBeforeScrub = audioSource != null && audioSource.isPlaying; if (wasPlayingBeforeScrub && audioSource != null) audioSource.Pause(); });
            EnsureEvent(et, EventTriggerType.PointerUp, data => { isDraggingSlider = false; OnSliderChanged(timelineSlider.value); if (wasPlayingBeforeScrub && audioSource != null) audioSource.Play(); });
            EnsureEvent(et, EventTriggerType.Drag, data => { isDraggingSlider = true; });
        }
        if (followToggleButton != null) { followToggleButton.onClick.RemoveListener(ToggleFollow); followToggleButton.onClick.AddListener(ToggleFollow); }
        if (selectionModeButton != null) { selectionModeButton.onClick.RemoveListener(ToggleSelectionMode); selectionModeButton.onClick.AddListener(ToggleSelectionMode); UpdateSelectionModeButtonVisual(); }
        if (selectionBoxRect != null) selectionBoxRect.gameObject.SetActive(false);
        if (playheadHeader != null)
        {
            var et = playheadHeader.gameObject.GetComponent<EventTrigger>();
            if (et == null) et = playheadHeader.gameObject.AddComponent<EventTrigger>();
            EnsureEvent(et, EventTriggerType.PointerDown, OnHeaderPointerDown);
            EnsureEvent(et, EventTriggerType.Drag, OnHeaderDrag);
            EnsureEvent(et, EventTriggerType.PointerUp, data => EndScrub());
        }
    }
    void BindPropertiesPanel()
    {
        if (propApplyButton != null) { propApplyButton.onClick.RemoveListener(ApplyPropertiesFromPanel); propApplyButton.onClick.AddListener(ApplyPropertiesFromPanel); }
        if (propDeleteButton != null) { propDeleteButton.onClick.RemoveListener(OnPropDelete); propDeleteButton.onClick.AddListener(OnPropDelete); }
        if (propCloseButton != null) { propCloseButton.onClick.RemoveListener(HidePropertiesPanel); propCloseButton.onClick.AddListener(HidePropertiesPanel); }
        if (propHitBeatInput != null) propHitBeatInput.onSubmit.AddListener(_ => ApplyPropertiesFromPanel());
        if (propHitTimeInput != null) propHitTimeInput.onSubmit.AddListener(_ => ApplyPropertiesFromPanel());
        if (propSpeedInput != null) propSpeedInput.onSubmit.AddListener(_ => ApplyPropertiesFromPanel());
        if (propPrefabIndexInput != null) propPrefabIndexInput.onSubmit.AddListener(_ => ApplyPropertiesFromPanel());
        if (propSpawnBeatInput != null) propSpawnBeatInput.onSubmit.AddListener(_ => ApplyPropertiesFromPanel());
    }
    void OnPropDelete() { if (selectedIndices.Count > 1) RemoveSelectedNotes(); else if (selectedIndex >= 0) RemoveNoteAt(selectedIndex); HidePropertiesPanel(); }
    void EnsureEvent(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> cb)
    {
        var entry = trigger.triggers.Find(e => e.eventID == type);
        if (entry == null) { entry = new EventTrigger.Entry { eventID = type }; trigger.triggers.Add(entry); }
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(cb));
    }
    public void ToggleFollow() { followSlider = !followSlider; UpdateFollowToggleVisual(); }
    void UpdateFollowToggleVisual()
    {
        if (followToggleButton == null) return;
        if (followToggleLabel != null) followToggleLabel.text = followSlider ? "Follow ON" : "Follow OFF";
        var img = followToggleButton.image;
        if (img != null) img.color = followSlider ? new Color(0.2f, 0.7f, 0.3f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f);
    }
    public void ToggleSelectionMode() { isSelectionMode = !isSelectionMode; UpdateSelectionModeButtonVisual(); if (statusLabel != null) statusLabel.text = isSelectionMode ? "Выделение: тяни рамку" : "Постановка нот"; if (!isSelectionMode && selectionBoxRect != null) selectionBoxRect.gameObject.SetActive(false); }
    void UpdateSelectionModeButtonVisual()
    {
        if (selectionModeButton == null) return;
        var img = selectionModeButton.image;
        if (img != null) img.color = isSelectionMode ? new Color(0.2f, 0.6f, 1f, 1f) : Color.white;
        var txt = selectionModeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = isSelectionMode ? "⬚ Выделение (вкл)" : "⬚ Выделение";
    }
    public void RefreshAll() { RefreshWaveform(); RefreshScrollContent(); RefreshNotes(); RefreshGrid(); UpdatePlayhead(); UpdateSliderFromTime(); UpdateScrollToPlayhead(true); UpdateLabels(); UpdatePlayPauseLabel(); UpdateWarningState(); }
    bool HasTrack() => (levelData != null && levelData.music != null) || (audioSource != null && audioSource.clip != null);
    void UpdateWarningState()
    {
        bool has = HasTrack();
        if (warningObject != null) warningObject.SetActive(!has);
        if (timelineObject != null) timelineObject.SetActive(has);
    }
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
    void RefreshWaveform()
    {
        if (waveformImage == null) return;
        AudioClip clip = levelData != null ? levelData.music : null;
        if (clip == null && audioSource != null) clip = audioSource.clip;
        if (clip == null) { waveformImage.texture = null; waveformImage.color = new Color(1, 1, 1, 0.08f); return; }
        if (clip == lastClip && waveformTex != null) return;
        lastClip = clip;
        if (waveformTex != null) Destroy(waveformTex);
        waveformData = WaveformGenerator.GenerateData(clip, waveformTexWidth);
        waveformTex = WaveformGenerator.GenerateTexture(waveformData, waveformTexWidth, waveformTexHeight, waveformWaveColor, waveformBgColor);
        waveformImage.texture = waveformTex;
        waveformImage.color = Color.white;
    }
    bool IsFreeMoveHeld() => Input.GetKey(freeMoveKey) || Input.GetKey(freeMoveKeyAlt) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift);
    bool ShouldSnap() => autoQuantize && snapToGrid && !IsFreeMoveHeld();
    void RefreshGrid()
    {
        if (gridContainer == null) return;
        for (int i = gridContainer.childCount - 1; i >= 0; i--) DestroyImmediate(gridContainer.GetChild(i).gameObject);
        float clipLen = GetClipLength(); if (clipLen < 0.1f) return;
        float bpm = levelData != null && levelData.bpm > 1 ? levelData.bpm : 120f;
        float beatLen = 60f / bpm;
        float stepBeats = quantStep; if (stepBeats < 0.1f) stepBeats = 0.5f;
        float stepTime = beatLen * stepBeats;
        float contentW = timelineContent != null ? timelineContent.sizeDelta.x : 1000f; if (contentW < 1 && timelineContent != null) contentW = timelineContent.rect.width;
        float vpW = timelineViewport != null ? timelineViewport.rect.width : 800f;
        if (contentW > 0 && vpW > 0) { float visibleTime = clipLen * (vpW / contentW); float desiredLines = 80f; if (visibleTime / stepTime > desiredLines) stepTime = Mathf.Ceil(visibleTime / desiredLines / (beatLen * 0.25f)) * (beatLen * 0.25f); if (stepTime < 0.05f) stepTime = 0.05f; }
        float totalBeatsForLoop = levelData != null ? levelData.TimeToBeat(clipLen) : clipLen / beatLen;
        for (float b = 0; b <= totalBeatsForLoop + 0.001f; b += stepBeats)
        {
            float t = levelData != null ? levelData.BeatToTime(b) : b * beatLen;
            float norm = t / clipLen; if (norm < -0.01f || norm > 1.01f) continue;
            bool isBar = Mathf.Abs(b % 4f) < 0.001f; bool isBeat = Mathf.Abs(b % 1f) < 0.001f; bool isHalf = !isBeat && Mathf.Abs((b * 2f) % 1f) < 0.001f;
            Color col; float w; float hMul = 1f;
            if (isBar) { col = gridBarColor; w = 2.5f; } else if (isBeat) { col = gridBeatColor; w = 1.5f; hMul = 0.85f; } else if (isHalf) { col = gridHalfColor; w = 1f; hMul = 0.55f; } else { col = new Color(gridHalfColor.r, gridHalfColor.g, gridHalfColor.b, gridHalfColor.a * 0.5f); w = 1f; hMul = 0.4f; }
            if (col.a < 0.01f) continue;
            var lineGO = new GameObject($"Grid_{b:0.##}", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(gridContainer, false);
            var rt = lineGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(norm, 0); rt.anchorMax = new Vector2(norm, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            if (isBar) { rt.anchorMin = new Vector2(norm, 0); rt.anchorMax = new Vector2(norm, 1); rt.sizeDelta = new Vector2(w, 0); }
            else { rt.anchorMin = new Vector2(norm, 0.5f - hMul * 0.5f); rt.anchorMax = new Vector2(norm, 0.5f + hMul * 0.5f); rt.sizeDelta = new Vector2(w, 0); }
            rt.anchoredPosition = Vector2.zero;
            var img = lineGO.GetComponent<Image>(); img.color = col; img.raycastTarget = false;
            bool showBarLabel = isBar; bool showBeatLabel = isBeat && !isBar && zoom > 1.2f; bool showTimeLabel = isBar || showBeatLabel;
            if (showTimeLabel)
            {
                var labelGO = new GameObject("Label", typeof(RectTransform)); labelGO.transform.SetParent(lineGO.transform, false);
                var lrt = labelGO.GetComponent<RectTransform>(); lrt.anchorMin = new Vector2(0.5f, 1); lrt.anchorMax = new Vector2(0.5f, 1); lrt.pivot = new Vector2(0, 1); lrt.anchoredPosition = new Vector2(4, -2); lrt.sizeDelta = new Vector2(isBar ? 80 : 50, 14);
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                if (isBar) { int barIdx = Mathf.RoundToInt(b / 4f) + 1; tmp.text = $"{FormatTime(t)}<size=8><color=#FFFFFF66>  Такт {barIdx}</color></size>"; tmp.fontSize = 9; tmp.color = new Color(1, 1, 1, 0.88f); }
                else { tmp.text = $"{FormatTime(t)}"; tmp.fontSize = 8; tmp.color = new Color(1, 1, 1, 0.5f); }
                tmp.alignment = TextAlignmentOptions.TopLeft; tmp.raycastTarget = false;
            }
        }
        var centerGO = new GameObject("CenterLine", typeof(RectTransform), typeof(Image)); centerGO.transform.SetParent(gridContainer, false);
        var cRT = centerGO.GetComponent<RectTransform>(); cRT.anchorMin = new Vector2(0, 0.5f); cRT.anchorMax = new Vector2(1, 0.5f); cRT.pivot = new Vector2(0.5f, 0.5f); cRT.sizeDelta = new Vector2(0, 1); cRT.anchoredPosition = Vector2.zero;
        var cImg = centerGO.GetComponent<Image>(); cImg.color = new Color(1, 1, 1, 0.07f); cImg.raycastTarget = false;
    }
    void RefreshNotes()
    {
        if (isDraggingNote) return;
        if (notesContainer == null || levelData == null) return;
        for (int i = noteGos.Count - 1; i >= 0; i--) if (noteGos[i] != null) Destroy(noteGos[i]);
        noteGos.Clear();
        if (levelData.music == null) return;
        float clipLen = levelData.music.length; if (clipLen < 0.01f) return;
        for (int i = 0; i < levelData.events.Count; i++) { var ev = levelData.events[i]; float hitTime = GetHitTime(ev); float norm = Mathf.Clamp01(hitTime / clipLen); var go = CreateNoteGO(i, ev, norm); noteGos.Add(go); }
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
            if (rt.sizeDelta.x < 4 || rt.sizeDelta.y < 4) rt.sizeDelta = noteSize;
            img = go.GetComponent<Image>(); if (img == null) img = go.GetComponentInChildren<Image>();
            if (img != null) { img.color = GetColorForPrefab(ev.prefabIndex); img.raycastTarget = true; }
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
            rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = noteSize; rt.anchoredPosition = Vector2.zero;
            img = go.GetComponent<Image>(); img.color = GetColorForPrefab(ev.prefabIndex); img.raycastTarget = true;
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
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(levelData);
#endif
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
    Color GetColorForPrefab(int idx) { float h = (idx * 0.37f) % 1f; return Color.HSVToRGB(h, 0.78f, 0.92f); }
    void Update()
    {
        UpdateWarningState();
        if (levelData != null && levelData.music != lastClip && levelData.music != null) RefreshAll();
        HandleKeyboard(); UpdateCurrentTimeFromAudio(); HandleMouseInput();
        if (!isScrubbingWaveform && !isDraggingSlider) { UpdatePlayhead(); UpdateSliderFromTime(); if (autoScrollWithPlayhead && audioSource != null && audioSource.isPlaying) UpdateScrollToPlayhead(false); }
        else if (isDraggingSlider) UpdatePlayhead();
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
        for (int k = 1; k <= 7; k++) { if (Input.GetKeyDown(KeyCode.Alpha0 + k) || Input.GetKeyDown(KeyCode.Keypad0 + k)) { brushIndex = k - 1; if (levelData != null && levelData.obstaclePrefabs.Count > 0) brushIndex = Mathf.Clamp(brushIndex, 0, levelData.obstaclePrefabs.Count - 1); FlashStatus($"Кисть → {brushIndex} {(levelData.GetPrefab(brushIndex) ? levelData.GetPrefab(brushIndex).name : "")}"); } }
        if ((selectedIndex >= 0 || selectedIndices.Count > 0) && levelData != null && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
        {
            float dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1f : -1f;
            float step = Input.GetKey(KeyCode.LeftShift) ? quantStep * 0.5f : quantStep;
            if (ShouldSnap()) step = step; else step = 0.05f;
            if (selectedIndices.Count > 1) NudgeSelected(dir * step);
            else { int idx = selectedIndex >= 0 ? selectedIndex : new List<int>(selectedIndices)[0]; NudgeNote(idx, dir * step); }
        }
    }
    void HandleMouseInput()
    {
        if (isDraggingNote) return;
        if (isSelectionMode && timelineContent != null) { HandleRectSelection(); return; }
        RectTransform headerRect = playheadHeader;
        bool headerClick = headerRect != null && RectTransformUtility.RectangleContainsScreenPoint(headerRect, Input.mousePosition, GetCanvasCamera());
        if (headerClick)
        {
            if (Input.GetMouseButtonDown(0)) { float t = GetTimeFromMouseHeader(Input.mousePosition); Seek(t); BeginScrub(); }
            if (Input.GetMouseButton(0) && isScrubbingWaveform) { float t = GetTimeFromMouseHeader(Input.mousePosition); Seek(t); }
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
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) { Seek(t); BeginScrub(); }
                else AddNoteAtTime(t);
            }
        }
        if (Input.GetMouseButtonUp(0) && isScrubbingWaveform) EndScrub();
        if (isScrubbingWaveform && Input.GetMouseButton(0))
        {
            RectTransform scrubRect = playheadHeader != null ? playheadHeader : refRect;
            float t = scrubRect == playheadHeader ? GetTimeFromMouseHeader(Input.mousePosition) : GetTimeFromMouse(Input.mousePosition);
            Seek(t);
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
        if (wasPlayingBeforeScrub && audioSource != null) audioSource.Pause();
    }
    void EndScrub()
    {
        isScrubbingWaveform = false;
        if (wasPlayingBeforeScrub && audioSource != null) audioSource.Play();
    }
    void HandleRectSelection()
    {
        if (timelineContent == null || timelineScrollRect == null) return;
        Camera cam = GetCanvasCamera();
        RectTransform refRect = timelineContent;
        if (Input.GetMouseButtonDown(0))
        {
            RectTransform checkRect = timelineViewport != null ? timelineViewport : (RectTransform)timelineScrollRect.transform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(checkRect, Input.mousePosition, cam)) return;
            if (IsPointerOverNote() && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
            isRectSelecting = true;
            rectStartScreenPos = Input.mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, rectStartScreenPos, cam, out rectStartLocalPos);
            if (selectionBoxRect != null)
            {
                selectionBoxRect.gameObject.SetActive(true);
                selectionBoxRect.SetParent(refRect, false);
                selectionBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
                selectionBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
                selectionBoxRect.pivot = new Vector2(0.5f, 0.5f);
                selectionBoxRect.anchoredPosition = rectStartLocalPos;
                selectionBoxRect.sizeDelta = Vector2.zero;
                if (selectionBoxImage != null) selectionBoxImage.color = selectionBoxColor;
            }
        }
        if (isRectSelecting && Input.GetMouseButton(0))
        {
            Vector2 curLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, Input.mousePosition, cam, out curLocal);
            Vector2 delta = curLocal - rectStartLocalPos;
            Vector2 center = rectStartLocalPos + delta * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            if (size.y < 20) size.y = refRect.rect.height;
            if (selectionBoxRect != null) { selectionBoxRect.anchoredPosition = center; selectionBoxRect.sizeDelta = size; }
        }
        if (isRectSelecting && Input.GetMouseButtonUp(0))
        {
            isRectSelecting = false;
            if (selectionBoxRect != null)
            {
                Vector2 endLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, Input.mousePosition, cam, out endLocal);
                float x1 = rectStartLocalPos.x, x2 = endLocal.x;
                float minX = Mathf.Min(x1, x2), maxX = Mathf.Max(x1, x2);
                if (Mathf.Abs(maxX - minX) < 5f) { selectionBoxRect.gameObject.SetActive(false); if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) DeselectNote(); return; }
                Rect cr = refRect.rect;
                float n1 = Mathf.InverseLerp(cr.xMin, cr.xMax, minX);
                float n2 = Mathf.InverseLerp(cr.xMin, cr.xMax, maxX);
                float normMin = Mathf.Clamp01(Mathf.Min(n1, n2));
                float normMax = Mathf.Clamp01(Mathf.Max(n1, n2));
                float tMin = normMin * GetClipLength();
                float tMax = normMax * GetClipLength();
                var newSelection = new HashSet<int>();
                for (int i = 0; i < levelData.events.Count; i++) { float ht = GetHitTime(levelData.events[i]); if (ht >= tMin && ht <= tMax) newSelection.Add(i); }
                bool additive = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftShift);
                if (additive) { foreach (var idx in newSelection) selectedIndices.Add(idx); if (newSelection.Count > 0) selectedIndex = new List<int>(newSelection)[new List<int>(newSelection).Count - 1]; }
                else { selectedIndices = newSelection; selectedIndex = selectedIndices.Count > 0 ? new List<int>(selectedIndices)[new List<int>(selectedIndices).Count - 1] : -1; }
                if (selectedIndices.Count > 0 && selectedIndex >= 0) ShowPropertiesPanel(selectedIndex); else HidePropertiesPanel();
                RefreshNotes();
                FlashStatus($"Выделено {selectedIndices.Count} нот");
                selectionBoxRect.gameObject.SetActive(false);
            }
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
    void UpdateCurrentTimeFromAudio()
    {
        if (audioSource == null || audioSource.clip == null) { if (audioSource != null && !audioSource.isPlaying) return; }
        if (audioSource.isPlaying) currentTime = audioSource.time;
        else if (!isScrubbingWaveform && !isDraggingSlider) currentTime = audioSource.time;
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
    void UpdateSliderFromTime()
    {
        if (timelineSlider == null || GetClipLength() < 0.01f) return;
        if (!followSlider) return;
        float norm = currentTime / GetClipLength();
        timelineSlider.SetValueWithoutNotify(norm);
    }
    public void OnSliderChanged(float norm)
    {
        if (GetClipLength() < 0.01f) return;
        if (!isDraggingSlider && !isScrubbingWaveform) return;
        float t = norm * GetClipLength();
        Seek(t);
    }
    public void UpdateScrollToPlayhead(bool immediate = false)
    {
        if (timelineScrollRect == null || timelineContent == null || GetClipLength() < 0.01f) return;
        if (!autoScrollWithPlayhead) return;
        if (!immediate && (audioSource == null || !audioSource.isPlaying)) return;
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
        float leftEdge = offset;
        float rightEdge = offset + viewportW;
        float targetOffset = offset;
        if (playheadX < leftEdge + margin) targetOffset = playheadX - margin;
        else if (playheadX > rightEdge - margin) targetOffset = playheadX - viewportW + margin;
        targetOffset = Mathf.Clamp(targetOffset, 0, contentW - viewportW);
        float targetNorm = targetOffset / (contentW - viewportW);
        timelineScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetNorm);
    }
    [ContextMenu("Центрировать на плейхеде")] public void CenterScrollOnPlayhead() => UpdateScrollToPlayhead(true);
    void UpdateLabels()
    {
        if (timeLabel != null) { float len = GetClipLength(); timeLabel.text = $"{FormatTime(currentTime)} / {FormatTime(len)}"; }
        if (beatLabel != null && beatLabel.gameObject.activeSelf) beatLabel.gameObject.SetActive(false);
    }
    void UpdatePlayPauseLabel() { if (playPauseLabel != null) { bool playing = audioSource != null && audioSource.isPlaying; playPauseLabel.text = playing ? "■" : "►"; } }
    string FormatTime(float t) { int m = Mathf.FloorToInt(t / 60f); int s = Mathf.FloorToInt(t % 60f); int ms = Mathf.FloorToInt((t - Mathf.Floor(t)) * 100); return $"{m:0}:{s:00}.{ms:00}"; }
    public void OnWaveformDrag(BaseEventData data) { var ped = data as PointerEventData; if (ped != null) { Seek(GetTimeFromMouse(ped.position)); isScrubbingWaveform = true; } }
    public void OnWaveformPointerUp(BaseEventData data) { isScrubbingWaveform = false; }
    void OnHeaderPointerDown(BaseEventData data) { var ped = data as PointerEventData; if (ped == null) return; float t = GetTimeFromMouseHeader(ped.position); Seek(t); BeginScrub(); }
    void OnHeaderDrag(BaseEventData data) { var ped = data as PointerEventData; if (ped == null) return; float t = GetTimeFromMouseHeader(ped.position); Seek(t); }
    public void AddNoteAtCurrentPlayhead() => AddNoteAtTime(currentTime);
    public void AddNoteAtTime(float hitTime)
    {
        if (levelData == null) { FlashStatus("Нет LevelData!"); return; }
        if (levelData.music == null && audioSource != null && audioSource.clip != null) levelData.music = audioSource.clip;
        if (levelData.music == null) { FlashStatus("Нет музыки — загрузите аудио!"); return; }
        if (levelData.obstaclePrefabs.Count == 0) { FlashStatus("Нет префабов!"); return; }
        float hitBeat = levelData.TimeToBeat(hitTime);
        if (ShouldSnap()) hitBeat = Mathf.Round(hitBeat / quantStep) * quantStep;
        hitBeat = Mathf.Clamp(hitBeat, 0f, levelData.TimeToBeat(levelData.music.length) - 0.1f);
        float hitTimeQ = levelData.BeatToTime(hitBeat);
        foreach (var ev2 in levelData.events) if (Mathf.Abs(GetHitBeat(ev2) - hitBeat) < 0.02f) { FlashStatus($"Уже есть нота на {FormatTime(hitTimeQ)}"); return; }
        float speed = GetSpeedForBrush();
        float travel = GetTravelForSpeed(speed);
        float spawnTime = hitTimeQ - travel;
        float spawnBeat = levelData.TimeToBeat(spawnTime);
        if (spawnTime < -1f) { spawnTime = Mathf.Max(spawnTime, -0.5f); spawnBeat = levelData.TimeToBeat(spawnTime); }
        brushIndex = Mathf.Clamp(brushIndex, 0, Mathf.Max(0, levelData.obstaclePrefabs.Count - 1));
        var ev = ObstacleEvent.Create(spawnBeat, brushIndex, Vector3.zero, speed);
        ev.time = spawnTime;
        levelData.events.Add(ev);
        levelData.SortByTime();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
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
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
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
    void ShowPropertiesPanel(int idx)
    {
        if (notePropertiesPanel == null) return;
        if (levelData == null || idx < 0 || idx >= levelData.events.Count) { HidePropertiesPanel(); return; }
        notePropertiesPanel.SetActive(true);
        var ev = levelData.events[idx];
        float hitBeat = GetHitBeat(ev);
        float hitTime = GetHitTime(ev);
        float spawnBeat = ev.beat;
        if (selectedIndices.Count > 1 && propTitleLabel != null) propTitleLabel.text = $"Выделено {selectedIndices.Count} нот";
        else if (propTitleLabel != null) propTitleLabel.text = $"Нота #{idx} — {FormatTime(hitTime)}";
        if (propHitBeatInput != null) propHitBeatInput.SetTextWithoutNotify(hitBeat.ToString("0.##"));
        if (propHitTimeInput != null) propHitTimeInput.SetTextWithoutNotify(hitTime.ToString("0.00"));
        if (propSpawnBeatInput != null) propSpawnBeatInput.SetTextWithoutNotify(spawnBeat.ToString("0.##"));
        if (propSpeedInput != null) propSpeedInput.SetTextWithoutNotify(ev.speed.ToString("0.##"));
        if (propPrefabIndexInput != null) propPrefabIndexInput.SetTextWithoutNotify(ev.prefabIndex.ToString());
    }
    void HidePropertiesPanel() { if (notePropertiesPanel != null) notePropertiesPanel.SetActive(false); }
    void RefreshPropertiesPanel() { if (selectedIndex >= 0 && notePropertiesPanel != null && notePropertiesPanel.activeSelf) ShowPropertiesPanel(selectedIndex); }
    public void ApplyPropertiesFromPanel()
    {
        if (levelData == null || selectedIndex < 0 || selectedIndex >= levelData.events.Count) return;
        var ev = levelData.events[selectedIndex];
        float newHitBeat = GetHitBeat(ev); bool hasHitBeat = false;
        if (propHitBeatInput != null && !string.IsNullOrWhiteSpace(propHitBeatInput.text) && float.TryParse(propHitBeatInput.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hb)) { newHitBeat = hb; hasHitBeat = true; }
        else if (propHitTimeInput != null && !string.IsNullOrWhiteSpace(propHitTimeInput.text) && float.TryParse(propHitTimeInput.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ht)) { newHitBeat = levelData.TimeToBeat(ht); hasHitBeat = true; }
        else if (propSpawnBeatInput != null && !string.IsNullOrWhiteSpace(propSpawnBeatInput.text) && float.TryParse(propSpawnBeatInput.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sb)) { ev.beat = sb; ev.time = levelData.BeatToTime(sb); hasHitBeat = false; }
        float newSpeed = ev.speed;
        if (propSpeedInput != null && float.TryParse(propSpeedInput.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sp)) newSpeed = sp;
        int newPrefab = ev.prefabIndex;
        if (propPrefabIndexInput != null && int.TryParse(propPrefabIndexInput.text, out int pi)) newPrefab = Mathf.Clamp(pi, 0, Mathf.Max(0, levelData.obstaclePrefabs.Count - 1));
        if (hasHitBeat)
        {
            newHitBeat = Mathf.Clamp(newHitBeat, 0f, levelData.TimeToBeat(GetClipLength()));
            if (ShouldSnap()) newHitBeat = Mathf.Round(newHitBeat / quantStep) * quantStep;
            float hitT = levelData.BeatToTime(newHitBeat);
            float travel = GetTravelForSpeed(newSpeed > 0.01f ? newSpeed : GetSpeedForBrush());
            float spawnT = hitT - travel;
            ev.beat = levelData.TimeToBeat(spawnT);
            ev.time = spawnT;
        }
        ev.speed = newSpeed; ev.prefabIndex = newPrefab;
        levelData.events[selectedIndex] = ev;
        levelData.SortByTime();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
        float hbFinal = GetHitBeat(ev);
        for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hbFinal) < 0.01f) { selectedIndex = i; break; }
        RefreshNotes(); ShowPropertiesPanel(selectedIndex);
        FlashStatus($"Сохранено {FormatTime(levelData.BeatToTime(hbFinal))}  скор {newSpeed:0.##}  префаб {newPrefab}");
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
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
        for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hb) < 0.01f) { selectedIndex = i; break; }
        RefreshNotes();
    }
    public void ClearAllNotes()
    {
        if (levelData == null) return;
        levelData.events.Clear();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
        selectedIndex = -1; selectedIndices.Clear(); RefreshNotes(); HidePropertiesPanel(); FlashStatus("Все ноты удалены");
    }
    public void RemoveSelectedNotes()
    {
        if (levelData == null || selectedIndices.Count == 0) return;
        var sorted = new List<int>(selectedIndices); sorted.Sort((a, b) => b.CompareTo(a));
        foreach (var idx in sorted) if (idx >= 0 && idx < levelData.events.Count) levelData.events.RemoveAt(idx);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
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
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(levelData);
#endif
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
        Transform sp = null, hit = null;
        if (manager != null) { sp = manager.spawnPoint; hit = manager.hitTrigger; }
        if (sp == null) { var go = GameObject.Find("SpawnPoint"); if (go) sp = go.transform; }
        if (hit == null) { var go = GameObject.Find("HitTrigger"); if (!go) go = GameObject.Find("Trigger"); if (go) hit = go.transform; }
        float d = 52f;
        if (sp != null && hit != null)
        {
            Vector3 toHit = hit.position - sp.position; toHit.y = 0;
            Vector3 dir = (manager != null && manager.spawnPoint && manager.despawnPoint) ? (manager.despawnPoint.position - manager.spawnPoint.position) : new Vector3(0, 0, -1);
            dir.y = 0; if (dir.sqrMagnitude < 0.001f) dir = new Vector3(0, 0, -1); dir.Normalize();
            d = Mathf.Abs(Vector3.Dot(toHit, dir));
            if (d < 1f) d = 52f;
        }
        return d / Mathf.Max(1f, speed);
    }
    float GetTravelForEvent(ObstacleEvent ev)
    {
        float s = ev.speed;
        if (s < 0.1f && levelData != null)
        {
            var pf = levelData.GetPrefab(ev.prefabIndex);
            if (pf) { var ob = pf.GetComponent<Obstacle>(); if (ob) s = ob.baseSpeed; }
        }
        if (s < 0.1f) s = defaultNoteSpeed;
        return GetTravelForSpeed(s);
    }
    float GetHitTime(ObstacleEvent ev) => ev.time + GetTravelForEvent(ev);
    float GetHitBeat(ObstacleEvent ev) => levelData != null ? levelData.TimeToBeat(GetHitTime(ev)) : ev.beat;
    float GetSpeedForBrush()
    {
        float s = defaultNoteSpeed;
        if (levelData != null && levelData.obstaclePrefabs.Count > brushIndex && brushIndex >= 0)
        {
            var pf = levelData.GetPrefab(brushIndex);
            if (pf) { var ob = pf.GetComponent<Obstacle>(); if (ob && ob.baseSpeed > 0.1f) s = ob.baseSpeed; }
        }
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
    Camera GetCanvasCamera()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return null;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }
    public void Seek(float time)
    {
        if (GetClipLength() < 0.01f) return;
        time = Mathf.Clamp(time, 0f, GetClipLength() - 0.01f);
        currentTime = time;
        if (audioSource != null)
        {
            bool wasPlaying = audioSource.isPlaying;
            audioSource.time = time;
            if (wasPlaying && !audioSource.isPlaying) audioSource.Play();
        }
        UpdatePlayhead();
        UpdateSliderFromTime();
        UpdateScrollToPlayhead(false);
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
        audioSource.Play();
        currentTime = audioSource.time;
        UpdatePlayPauseLabel();
    }
    public void TogglePlayPause() { if (audioSource != null && audioSource.isPlaying) Pause(); else Play(); }
    public void SetQuant(float q) { quantStep = q; RefreshGrid(); }
    public void SetBrush(int idx) { brushIndex = Mathf.Clamp(idx, 0, levelData != null ? Mathf.Max(0, levelData.obstaclePrefabs.Count - 1) : 6); FlashStatus($"Кисть {brushIndex}"); }
    void OnFileLoaded(string path, AudioClip clip)
    {
        if (clip == null) return;
        if (levelData != null) { levelData.music = clip; levelData.audioPath = path;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(levelData);
#endif
        }
        if (audioSource != null) { audioSource.clip = clip; audioSource.Stop(); currentTime = 0f; }
        lastClip = null;
        RefreshAll();
        FlashStatus($"Загружено: {clip.name}  {clip.length:0.0}с");
    }
    void FlashStatus(string msg) { if (statusLabel != null) statusLabel.text = msg; Debug.Log($"[Timeline] {msg}", this); CancelInvoke(nameof(ClearStatus)); Invoke(nameof(ClearStatus), 3f); }
    void ClearStatus() { if (statusLabel != null) statusLabel.text = ""; }
    [ContextMenu("Перестроить вейвформу")] void ContextRebuild() { lastClip = null; RefreshAll(); }
    [ContextMenu("Добавить ноту")] void ContextAdd() => AddNoteAtCurrentPlayhead();
    [ContextMenu("Очистить")] void ContextClear() => ClearAllNotes();
}
