using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Rhythm;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimelineNotesController : RKSBehaviour
    {
        private TimelineUI ui;
        private TimelinePropertiesController propsCache;

        private TimelinePropertiesController props
        {
            get
            {
                if (propsCache == null) propsCache = GetComponent<TimelinePropertiesController>();
                return propsCache;
            }
        }

        private readonly List<GameObject> notePool = new List<GameObject>();
        private readonly List<GameObject> noteGos = new List<GameObject>();
        private int selectedIndex = -1;
        private readonly HashSet<int> selectedIndices = new HashSet<int>();
        private bool isDraggingNote;
        private int dragNoteIdx = -1;
        private RectTransform dragNoteRect;
        private readonly Dictionary<int, float> dragOrigHitBeats = new Dictionary<int, float>();
        private float dragStartHitBeat;
        private float dragDeltaBeat;

        protected override void Awake()
        {
            ui = GetComponent<TimelineUI>();
            propsCache = GetComponent<TimelinePropertiesController>();
        }

        public bool IsDragging => isDraggingNote;
        public int GetSelectedIndex() => selectedIndex;

        public List<int> GetApplyTargets()
        {
            if (selectedIndices.Count > 1) return new List<int>(selectedIndices);
            return new List<int> { selectedIndex };
        }


        public void RefreshNotes()
        {
            if (ui == null) ui = GetComponent<TimelineUI>();
            if (ui == null) return;
            if (isDraggingNote) return;
            var levelData = ui.levelData;
            if (ui.notesContainer == null || levelData == null) return;
            float clipLen = ui.GetClipLength();
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
                        go.transform.SetParent(ui.notesContainer, false);
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
            if (selectedIndex >= 0) props.RefreshPropertiesPanel(); else props.HidePropertiesPanel();
        }

        GameObject CreateNoteGO(int index, ObstacleEvent ev, float norm)
        {
            GameObject go; RectTransform rt; Image img = null;
            if (ui.notePrefab != null)
            {
                go = Instantiate(ui.notePrefab, ui.notesContainer); go.name = $"Note_{index}_{ev.prefabIndex}";
                rt = go.GetComponent<RectTransform>(); if (rt == null) rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = GetScaledNoteSize();
                if (ev.scale != Vector3.one && ev.scale != Vector3.zero) { float avg = (ev.scale.x + ev.scale.y + ev.scale.z) / 3f; rt.sizeDelta *= Mathf.Clamp(avg, 0.6f, 2.2f); }
                if (rt.sizeDelta.x < 8) rt.sizeDelta = GetScaledNoteSize();

                if (Mathf.Abs(ev.position.x) > 0.01f) rt.anchoredPosition += new Vector2(0, ev.position.x * 7f);
                img = go.GetComponent<Image>(); if (img == null) img = go.GetComponentInChildren<Image>();
                if (img != null) { Color noteCol = ev.HasCustomColor ? ev.color : GetColorForPrefab(ev.prefabIndex); img.color = noteCol; img.raycastTarget = true; }
                bool isSelForView = selectedIndex == index || selectedIndices.Contains(index);
                if (ui.notePrefabUseCustomView)
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
                go.transform.SetParent(ui.notesContainer, false);
                rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(norm, 0.5f); rt.anchorMax = new Vector2(norm, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = GetScaledNoteSize(); rt.anchoredPosition = Vector2.zero;
                if (ev.scale != Vector3.one && ev.scale != Vector3.zero) { float avg = (ev.scale.x + ev.scale.y + ev.scale.z) / 3f; rt.sizeDelta *= Mathf.Clamp(avg, 0.6f, 2.2f); }
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
            hoverEntry.callback.AddListener((_) => { float ht = GetHitTime(ev); if (ui.statusLabel != null) ui.statusLabel.text = $"{ui.FormatTime(ht)} • #{ev.prefabIndex} • {ev.speed:0}m/s — ЛКМ выбор, Ctrl+клик множ., ПКМ удалить, тащи (Ctrl свободно)"; });
            et.triggers.Add(hoverEntry);
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) => ui.ClearStatus()); et.triggers.Add(exitEntry);
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
                if (ev.scale != Vector3.one && ev.scale != Vector3.zero) { float avg = (ev.scale.x + ev.scale.y + ev.scale.z) / 3f; rt.sizeDelta *= Mathf.Clamp(avg, 0.6f, 2.2f); }
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
            if (ui.notePrefabUseCustomView)
            {
                var custom = go.GetComponent<ITimelineNoteView>();
                if (custom != null) custom.Setup(index, ev, GetHitBeat(ev), isSel);
            }
            Transform selTf = go.transform.Find("Selected");
            if (selTf != null) selTf.gameObject.SetActive(isSel);
            var outline = go.GetComponent<Outline>();
            if (isSel)
            {
                if (outline == null) { outline = go.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(2, 2); }
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
                hoverEntry.callback.AddListener((_) => { float ht = GetHitTime(ev); if (ui.statusLabel != null) ui.statusLabel.text = $"{ui.FormatTime(ht)} • #{ev.prefabIndex} • {ev.speed:0}m/s — ЛКМ выбор, Ctrl+клик множ., ПКМ удалить, тащи (Ctrl свободно)"; });
                et.triggers.Add(hoverEntry);
                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => ui.ClearStatus()); et.triggers.Add(exitEntry);
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
            var levelData = ui.levelData;
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) return;
            dragNoteIdx = idx; isDraggingNote = true;
            if (!selectedIndices.Contains(idx))
            {
                bool multi = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftShift);
                if (!multi) { selectedIndices.Clear(); selectedIndices.Add(idx); selectedIndex = idx; props.ShowPropertiesPanel(idx); }
                else { selectedIndices.Add(idx); selectedIndex = idx; props.ShowPropertiesPanel(idx); }
            }
            if (idx >= 0 && idx < noteGos.Count && noteGos[idx] != null) dragNoteRect = noteGos[idx].GetComponent<RectTransform>(); else dragNoteRect = null;
            dragOrigHitBeats.Clear(); dragStartHitBeat = GetHitBeat(levelData.events[idx]); dragDeltaBeat = 0f;
            var indicesToSave = selectedIndices.Count > 0 && selectedIndices.Contains(idx) ? selectedIndices : new HashSet<int> { idx };
            foreach (var i in indicesToSave) if (i >= 0 && i < levelData.events.Count) dragOrigHitBeats[i] = GetHitBeat(levelData.events[i]);
            if (dragOrigHitBeats.Count == 0) dragOrigHitBeats[idx] = dragStartHitBeat;
        }

        void OnNoteDrag(PointerEventData ped)
        {
            var levelData = ui.levelData;
            if (!isDraggingNote || dragNoteIdx < 0 || levelData == null) return;
            if (ui.GetClipLength() < 0.01f) return;
            float t = ui.GetTimeFromMouse(ped.position);
            float hitBeat = levelData.TimeToBeat(t);
            if (ui.ShouldSnap()) hitBeat = Mathf.Round(hitBeat / ui.quantStep) * ui.quantStep;
            hitBeat = Mathf.Clamp(hitBeat, 0f, levelData.TimeToBeat(ui.GetClipLength()));
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
                float newHb = Mathf.Clamp(orig + delta, 0f, levelData.TimeToBeat(ui.GetClipLength()));
                if (ui.ShouldSnap()) newHb = Mathf.Round(newHb / ui.quantStep) * ui.quantStep;
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
                float norm2 = Mathf.Clamp01(ht / ui.GetClipLength());
                var r = noteGos[idx].GetComponent<RectTransform>();
                if (r != null) { r.anchorMin = new Vector2(norm2, 0.5f); r.anchorMax = new Vector2(norm2, 0.5f); }
            }
            if (dragNoteRect != null) { float norm = Mathf.Clamp01(levelData.BeatToTime(hitBeat) / ui.GetClipLength()); dragNoteRect.anchorMin = new Vector2(norm, 0.5f); dragNoteRect.anchorMax = new Vector2(norm, 0.5f); }
            if (ui.statusLabel != null) { if (dragOrigHitBeats.Count > 1) ui.statusLabel.text = $"Перемещение {dragOrigHitBeats.Count} нот → {ui.FormatTime(levelData.BeatToTime(hitBeat))} (Ctrl свободно)"; else ui.statusLabel.text = $"Перемещение → {ui.FormatTime(levelData.BeatToTime(hitBeat))} (Ctrl свободно)"; }
        }

        void EndNoteDrag()
        {
            if (!isDraggingNote) return;
            isDraggingNote = false; dragNoteRect = null;
            var levelData = ui.levelData;
            var origBeatsCopy = new Dictionary<int, float>(dragOrigHitBeats); float delta = dragDeltaBeat; dragOrigHitBeats.Clear();
            if (levelData != null)
            {
                levelData.SortByTime();

                if (origBeatsCopy.Count > 1)
                {
                    var newSelected = new HashSet<int>();
                    foreach (var kv in origBeatsCopy)
                    {
                        float newHb = Mathf.Clamp(kv.Value + delta, 0f, levelData.TimeToBeat(ui.GetClipLength()));
                        if (ui.ShouldSnap()) newHb = Mathf.Round(newHb / ui.quantStep) * ui.quantStep;
                        int bestIdx = -1; float bestDist = float.MaxValue;
                        for (int i = 0; i < levelData.events.Count; i++) { float hb = GetHitBeat(levelData.events[i]); float d = Mathf.Abs(hb - newHb); if (d < bestDist && d < 0.05f) { bestDist = d; bestIdx = i; } }
                        if (bestIdx >= 0) newSelected.Add(bestIdx);
                    }
                    selectedIndices.Clear();
                    foreach (var s in newSelected) selectedIndices.Add(s);
                    selectedIndex = newSelected.Count > 0 ? new List<int>(newSelected)[new List<int>(newSelected).Count - 1] : -1;
                }
                else if (dragNoteIdx >= 0)
                {
                    int best = dragNoteIdx; float bestDist = float.MaxValue;
                    for (int i = 0; i < levelData.events.Count; i++) { float d = Mathf.Abs(GetHitBeat(levelData.events[i]) - (dragStartHitBeat + delta)); if (d < bestDist) { bestDist = d; best = i; } }
                    selectedIndex = best;
                    if (selectedIndices.Count == 1) selectedIndices.Clear();
                    if (selectedIndices.Count <= 1) { selectedIndices.Clear(); if (best >= 0) selectedIndices.Add(best); }
                }
                RefreshNotes(); props.RefreshPropertiesPanel();
            }
            dragNoteIdx = -1; dragDeltaBeat = 0f;
        }

        Vector2 GetScaledNoteSize()
        {
            if (!ui.scaleNotesWithZoom) return ui.noteSize;
            float t = Mathf.InverseLerp(0.25f, 4f, ui.zoom);
            float s = Mathf.Lerp(ui.noteZoomScaleMin, ui.noteZoomScaleMax, t);
            if (ui.scaleNotesWidthOnly) return new Vector2(ui.noteSize.x * s, ui.noteSize.y * Mathf.Lerp(1f, 1.08f, t));
            return ui.noteSize * s;
        }

        public void ApplyNoteZoomScale()
        {
            if (!ui.scaleNotesWithZoom || noteGos == null) return;
            var levelData = ui.levelData;
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

        public Color GetColorForPrefab(int idx) { float h = (idx * 0.37f) % 1f; return Color.HSVToRGB(h, 0.78f, 0.92f); }

        public void SelectNote(int idx) { var levelData = ui.levelData; selectedIndices.Clear(); selectedIndices.Add(idx); selectedIndex = idx; RefreshNotes(); if (idx >= 0 && levelData != null && idx < levelData.events.Count) { ui.FlashStatus($"Выбрано #{idx}  {ui.FormatTime(GetHitTime(levelData.events[idx]))} — Ctrl+клик множ."); props.ShowPropertiesPanel(idx); } else props.HidePropertiesPanel(); }
        public bool HasSelection() => selectedIndex >= 0 || selectedIndices.Count > 0;

        public void SelectAllNotes()
        {
            var levelData = ui.levelData;
            if (levelData == null) return;
            selectedIndices.Clear();
            for (int i = 0; i < levelData.events.Count; i++) selectedIndices.Add(i);
            selectedIndex = selectedIndices.Count > 0 ? new List<int>(selectedIndices)[0] : -1;
            RefreshNotes();
            if (selectedIndex >= 0) props.ShowPropertiesPanel(selectedIndex);
            ui.FlashStatus($"Выделено {selectedIndices.Count} нот (Ctrl+A)");
        }

        public void DeleteSelectionOrNearest()
        {
            var levelData = ui.levelData;
            if (levelData == null) return;
            if (selectedIndices.Count > 1) RemoveSelectedNotes();
            else if (selectedIndex >= 0 && selectedIndex < levelData.events.Count) RemoveNoteAt(selectedIndex);
            else if (selectedIndices.Count == 1) { int idx = new List<int>(selectedIndices)[0]; RemoveNoteAt(idx); }
            else RemoveNearestNote();
        }

        public void NudgeSelection(float beatDelta)
        {
            if (selectedIndices.Count > 1) NudgeSelected(beatDelta);
            else
            {
                int idx = selectedIndex >= 0 ? selectedIndex : (selectedIndices.Count > 0 ? new List<int>(selectedIndices)[0] : -1);
                if (idx >= 0) NudgeNote(idx, beatDelta);
            }
        }
        public void ToggleSelectNote(int idx) { if (selectedIndices.Contains(idx)) { selectedIndices.Remove(idx); if (selectedIndex == idx) selectedIndex = selectedIndices.Count > 0 ? new List<int>(selectedIndices)[selectedIndices.Count - 1] : -1; } else { selectedIndices.Add(idx); selectedIndex = idx; } RefreshNotes(); if (selectedIndex >= 0) props.ShowPropertiesPanel(selectedIndex); else props.HidePropertiesPanel(); ui.FlashStatus($"Выделено {selectedIndices.Count} нот"); }
        public void DeselectNote() { selectedIndex = -1; selectedIndices.Clear(); RefreshNotes(); props.HidePropertiesPanel(); }

        public void AddNoteAtCurrentPlayhead() => AddNoteAtTime(ui.GetCurrentTime());

        public void AddNoteAtTime(float hitTime)
        {
            var levelData = ui.levelData;
            if (levelData == null) { ui.FlashStatus("Нет LevelData!"); return; }
            if (levelData.music == null) { ui.FlashStatus("Нет музыки — загрузите аудио!"); return; }
            var catalog = ui.catalog;
            int gCountAdd = (catalog != null ? catalog.Count : 0); if (gCountAdd == 0) gCountAdd = levelData.PrefabCount(catalog);
            if (gCountAdd == 0) { ui.FlashStatus("Нет префабов! Заполни GlobalObstacleCatalog в Resources/"); return; }
            float hitBeat = levelData.TimeToBeat(hitTime);
            if (ui.ShouldSnap()) hitBeat = Mathf.Round(hitBeat / ui.quantStep) * ui.quantStep;
            hitBeat = Mathf.Clamp(hitBeat, 0f, levelData.TimeToBeat(levelData.music.length) - 0.1f);
            float hitTimeQ = levelData.BeatToTime(hitBeat);
            foreach (var ev2 in levelData.events) if (Mathf.Abs(GetHitBeat(ev2) - hitBeat) < 0.02f) { ui.FlashStatus($"Уже есть нота на {ui.FormatTime(hitTimeQ)}"); return; }
            float speed = GetSpeedForBrush();
            float travel = GetTravelForSpeed(speed);
            float spawnTime = hitTimeQ - travel;
            float spawnBeat = levelData.TimeToBeat(spawnTime);

            if (ui.brushIndex < 0 || ui.brushIndex >= gCountAdd) { ui.FlashStatus($"Кисть {ui.brushIndex} вне каталога (0–{gCountAdd - 1})"); return; }
            var ev = ObstacleEvent.Create(spawnBeat, ui.brushIndex, Vector3.zero, speed);
            ev.time = spawnTime;
            levelData.events.Add(ev);
            levelData.SortByTime();

            int newIdx = levelData.events.IndexOf(ev);
            if (newIdx < 0) for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hitBeat) < 0.01f) { newIdx = i; break; }
            selectedIndex = newIdx >= 0 ? newIdx : levelData.events.Count - 1;
            selectedIndices.Clear(); selectedIndices.Add(selectedIndex);
            RefreshNotes();
            props.ShowPropertiesPanel(selectedIndex);
            ui.FlashStatus($"+ Нота {ui.FormatTime(hitTimeQ)} → спавн {ui.FormatTime(spawnTime)}  #{ui.brushIndex}  Ctrl — свободно");
        }

        public void RemoveNoteAt(int idx)
        {
            var levelData = ui.levelData;
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) return;
            levelData.events.RemoveAt(idx);

            selectedIndex = Mathf.Clamp(idx - 1, -1, levelData.events.Count - 1);
            selectedIndices.Clear();
            if (selectedIndex >= 0) selectedIndices.Add(selectedIndex);
            RefreshNotes();
            if (selectedIndices.Count == 0) props.HidePropertiesPanel();
            ui.FlashStatus($"Удалена нота #{idx}");
        }

        public void RemoveNearestNote()
        {
            var levelData = ui.levelData;
            if (levelData == null || levelData.events.Count == 0) return;
            float curBeat = levelData.TimeToBeat(ui.GetCurrentTime());
            int best = -1; float bestDist = float.MaxValue;
            for (int i = 0; i < levelData.events.Count; i++) { float hb = GetHitBeat(levelData.events[i]); float d = Mathf.Abs(hb - curBeat); if (d < bestDist) { bestDist = d; best = i; } }
            if (best >= 0 && bestDist <= ui.noteDeleteThresholdBeats + 0.5f) RemoveNoteAt(best);
            else ui.FlashStatus("Нет ноты рядом");
        }

        public void RemoveSelectedNotes()
        {
            var levelData = ui.levelData;
            if (levelData == null || selectedIndices.Count == 0) return;
            var sorted = new List<int>(selectedIndices); sorted.Sort((a, b) => b.CompareTo(a));
            foreach (var idx in sorted) if (idx >= 0 && idx < levelData.events.Count) levelData.events.RemoveAt(idx);

            int cnt = sorted.Count; selectedIndices.Clear(); selectedIndex = -1; props.HidePropertiesPanel(); RefreshNotes(); ui.FlashStatus($"Удалено {cnt} нот");
        }

        public void ClearAllNotes()
        {
            var levelData = ui.levelData;
            if (levelData == null) return;
            levelData.events.Clear();

            selectedIndex = -1; selectedIndices.Clear(); RefreshNotes(); props.HidePropertiesPanel(); ui.FlashStatus("Все ноты удалены");
        }

        public void NudgeNote(int idx, float beatDelta)
        {
            var levelData = ui.levelData;
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) return;
            var ev = levelData.events[idx];
            float hb = GetHitBeat(ev) + beatDelta;
            hb = Mathf.Clamp(hb, 0f, levelData.TimeToBeat(ui.GetClipLength()));
            if (ui.ShouldSnap()) hb = Mathf.Round(hb / ui.quantStep) * ui.quantStep;
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

        public void NudgeSelected(float beatDelta)
        {
            var levelData = ui.levelData;
            if (levelData == null || selectedIndices.Count == 0) return;
            var newHits = new Dictionary<int, float>();
            foreach (var idx in selectedIndices)
            {
                if (idx < 0 || idx >= levelData.events.Count) continue;
                float hb = GetHitBeat(levelData.events[idx]) + beatDelta;
                hb = Mathf.Clamp(hb, 0f, levelData.TimeToBeat(ui.GetClipLength()));
                if (ui.ShouldSnap()) hb = Mathf.Round(hb / ui.quantStep) * ui.quantStep;
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
                if (ui.ShouldSnap()) hb = Mathf.Round(hb / ui.quantStep) * ui.quantStep;
                for (int i = 0; i < levelData.events.Count; i++) if (Mathf.Abs(GetHitBeat(levelData.events[i]) - hb) < 0.02f) { newSelected.Add(i); break; }
            }
            selectedIndices.Clear();
            foreach (var s in newSelected) selectedIndices.Add(s);
            if (newSelected.Count > 0) selectedIndex = new List<int>(newSelected)[0];
            RefreshNotes(); props.RefreshPropertiesPanel();
        }

        public int ResnapAllNotes()
        {
            var levelData = ui.levelData;
            if (levelData == null || levelData.events.Count == 0) return 0;
            float q = Mathf.Max(0.05f, ui.quantStep);
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
            RefreshNotes(); props.HidePropertiesPanel();
            ui.preview?.ForceRefresh();
            return n;
        }

        public void SetBrush(int idx)
        {
            var levelData = ui.levelData;
            var catalog = ui.catalog;
            int c = (catalog != null ? catalog.Count : 0); if (c == 0 && levelData != null) c = levelData.PrefabCount(catalog);
            if (c == 0) { ui.FlashStatus("Нет префабов! Заполни GlobalObstacleCatalog в Resources/"); return; }
            if (idx < 0 || idx >= c) { ui.FlashStatus($"Кисть: только 0–{c - 1}"); return; }
            ui.brushIndex = idx;
            ui.FlashStatus($"Кисть {ui.brushIndex}");
        }

        float GetTravelForSpeed(float speed)
        {
            speed = Mathf.Max(1f, speed);
            var manager = ui.manager;
            if (manager != null) return manager.GetTravelTime(speed);
            return 52f / speed;
        }
        public float GetTravelForEvent(ObstacleEvent ev)
        {

            return GetTravelForSpeed(ev.speed);
        }

        public float GetHitTime(ObstacleEvent ev) => ev.time + GetTravelForEvent(ev);
        public float GetHitBeat(ObstacleEvent ev) { var levelData = ui.levelData; return levelData != null ? levelData.TimeToBeat(GetHitTime(ev)) : ev.beat; }

        float GetSpeedForBrush()
        {
            return Mathf.Clamp(ui.defaultNoteSpeed, 1f, 60f);
        }

        public string SpeedHint(float speed)
        {
            var manager = ui.manager;
            float s = Mathf.Max(1f, speed);
            float dist = manager != null ? manager.GetSpawnToHitDistance() : 52f;
            if (dist < 1f) dist = 52f;
            float travel = dist / s;
            return $"{s:0.#} м/с • {travel:0.0}с полёта ({dist:0.0}м)";
        }

        public int EnsureExplicitSpeeds()
        {
            var levelData = ui.levelData;
            if (levelData == null) return 0;
            float def = Mathf.Clamp(ui.defaultNoteSpeed, 1f, 60f);
            var manager = ui.manager;
            var catalog = ui.catalog;
            float dist = manager != null ? manager.GetSpawnToHitDistance() : 52f;
            if (dist < 1f) dist = 52f;
            int n = 0;
            for (int i = 0; i < levelData.events.Count; i++)
            {
                var ev = levelData.events[i];
                if (ev.speed < 0.1f)
                {
                    float oldS = def;
                    var pf = levelData.GetPrefab(ev.prefabIndex, catalog);
                    if (pf != null)
                    {
                        var ob = pf.GetComponent<Obstacle>();
                        if (ob != null && ob.baseSpeed > 0.1f) oldS = ob.baseSpeed;
                    }
                    float oldTravel = dist / Mathf.Max(1f, oldS);
                    float hitT = ev.time + oldTravel;
                    float newTravel = dist / def;
                    float spawnT = hitT - newTravel;
                    ev.speed = def;
                    ev.time = spawnT;
                    ev.beat = levelData.TimeToBeat(spawnT);
                    levelData.events[i] = ev;
                    n++;
                }
            }
            if (n > 0) { levelData.SortByTime(); RefreshNotes(); }
            return n;
        }



















    }
}
