using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Rhythm;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimelinePropertiesController : RKSBehaviour
    {
        private TimelineUI ui;
        private TimelineNotesController notes;
        private int prefabGridTab;
        private int triggerTypePick = 1;

        protected override void Awake()
        {
            ui = GetComponent<TimelineUI>();
            notes = GetComponent<TimelineNotesController>();
        }

        void EnsureRefs()
        {
            if (ui == null) ui = GetComponent<TimelineUI>();
            if (notes == null) notes = GetComponent<TimelineNotesController>();
        }

        public void BindPropertiesPanel()
        {
            EnsureRefs();
            if (ui.propPrefabDropdown != null)
            {
                ui.propPrefabDropdown.onValueChanged.RemoveListener(OnPrefabDropdownChanged);
                ui.propPrefabDropdown.onValueChanged.AddListener(OnPrefabDropdownChanged);
            }
            if (ui.prefabGridPanel != null) ui.prefabGridPanel.SetActive(false);
        }

        void OnPrefabDropdownChanged(int idx)
        {
            ApplyPropertiesFromPanel();
        }

        public void ShowPropertiesPanel(int idx)
        {
            EnsureRefs();
            var notePropertiesPanel = ui.notePropertiesPanel;
            if (notePropertiesPanel == null)
            {
                Debug.LogWarning("[TimelineUI] notePropertiesPanel не назначен.", this);
                return;
            }
            var levelData = ui.levelData;
            if (levelData == null || idx < 0 || idx >= levelData.events.Count) { HidePropertiesPanel(); return; }
            notePropertiesPanel.SetActive(true);
            var ev = levelData.events[idx];
            float hitTime = notes.GetHitTime(ev);
            string speedTxt = $" • {notes.SpeedHint(ev.speed)}";
            if (notes.GetApplyTargets().Count > 1 && ui.propTitleLabel != null) ui.propTitleLabel.text = $"Выделено {notes.GetApplyTargets().Count} нот";
            else if (ui.propTitleLabel != null) ui.propTitleLabel.text = $"Нота #{idx} — {ui.FormatTime(hitTime)} • {GetEventPrefabName(ev)}{speedTxt}" + (ev.isTrigger ? (ev.triggerType == 2 ? " • ТРИГГЕР переверн." : " • ТРИГГЕР норм.") : "");

            if (ui.propPrefabDropdown != null)
            {
                ui.propPrefabDropdown.gameObject.SetActive(true);
                RefreshPrefabDropdown();
                int curIdx = ev.isTrigger ? ev.triggerPrefabIndex : ev.prefabIndex;
                if (curIdx < 0 || curIdx >= ui.propPrefabDropdown.options.Count)
                {
                    var repaired = levelData.events[idx];
                    if (ev.isTrigger) repaired.triggerPrefabIndex = 0;
                    else repaired.prefabIndex = 0;
                    levelData.events[idx] = repaired;
                    ev = repaired;
                    curIdx = 0;
                    notes.RefreshNotes();
                    ui.FlashStatus("Вид ноты был вне каталога — явно сброшен в 0");
                }
                if (ui.propPrefabDropdown.options.Count > 0)
                    ui.propPrefabDropdown.SetValueWithoutNotify(curIdx);
                var thumb = ui.propPrefabDropdown.transform.Find("Thumb");
                if (thumb == null && ui.propPrefabDropdown.template != null) thumb = ui.propPrefabDropdown.template.Find("Thumb");
            }
            if (ui.prefabGridPanel != null) ui.prefabGridPanel.SetActive(false);
            EnsurePrefabGrid();
        }

        public void OnPropDelete()
        {
            EnsureRefs();
            if (notes.GetApplyTargets().Count > 1) notes.RemoveSelectedNotes();
            else if (notes.GetSelectedIndex() >= 0) notes.RemoveNoteAt(notes.GetSelectedIndex());
            HidePropertiesPanel();
        }

        string GetPrefabName(int idx)
        {
            var catalog = ui.catalog;
            var levelData = ui.levelData;
            var pf = catalog != null ? catalog.GetPrefab(idx) : null;
            if (pf == null && levelData != null) pf = levelData.GetPrefab(idx, catalog);
            return pf != null ? pf.name : "—";
        }

        string GetTriggerPrefabName(int idx)
        {
            var catalog = ui.catalog;
            var levelData = ui.levelData;
            var pf = catalog != null ? catalog.GetTriggerPrefab(idx) : null;
            if (pf == null && levelData != null) pf = levelData.GetTriggerPrefab(idx, catalog);
            return pf != null ? pf.name : "—";
        }

        string GetEventPrefabName(ObstacleEvent ev)
        {
            if (ev.isTrigger) return GetTriggerPrefabName(ev.triggerPrefabIndex);
            return GetPrefabName(ev.prefabIndex);
        }

        public void HidePropertiesPanel()
        {
            EnsureRefs();
            if (ui.notePropertiesPanel != null) ui.notePropertiesPanel.SetActive(false);
            if (ui.prefabGridPanel != null) ui.prefabGridPanel.SetActive(false);
        }

        public void RefreshPropertiesPanel()
        {
            EnsureRefs();
            int sel = notes.GetSelectedIndex();
            if (sel >= 0 && ui.notePropertiesPanel != null && ui.notePropertiesPanel.activeSelf) ShowPropertiesPanel(sel);
        }

        public void ApplyPropertiesFromPanel()
        {
            EnsureRefs();
            var levelData = ui.levelData;
            int selectedIndex = notes.GetSelectedIndex();
            if (levelData == null || selectedIndex < 0 || selectedIndex >= levelData.events.Count) return;
            var ev = levelData.events[selectedIndex];
            var catalog = ui.catalog;

            bool triggersMode = ev.isTrigger;
            int total = 0;
            if (triggersMode)
            {
                total = (catalog != null ? catalog.TriggerCount : 0);
                if (total == 0 && levelData != null) total = levelData.TriggerPrefabCount(catalog);
            }
            else
            {
                total = (catalog != null ? catalog.Count : 0);
                if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            }
            if (total == 0) { ui.FlashStatus(triggersMode ? "Нет префабов триггеров! Заполни triggerPrefabs в GlobalObstacleCatalog" : "Нет префабов! Заполни GlobalObstacleCatalog в Resources/"); return; }
            int newPrefab = triggersMode ? ev.triggerPrefabIndex : ev.prefabIndex;
            if (ui.propPrefabDropdown != null && ui.propPrefabDropdown.options.Count > 0)
            {
                if (ui.propPrefabDropdown.value < 0 || ui.propPrefabDropdown.value >= total) { ui.FlashStatus("Вид вне каталога"); return; }
                newPrefab = ui.propPrefabDropdown.value;
            }

            var tgt = notes.GetApplyTargets();
            foreach (var ti in tgt)
            {
                if (ti < 0 || ti >= levelData.events.Count) continue;
                var ee = levelData.events[ti];
                if (triggersMode)
                {
                    if (!ee.isTrigger) continue;
                    ee.triggerPrefabIndex = newPrefab;
                }
                else
                {
                    if (ee.isTrigger) continue;
                    ee.prefabIndex = newPrefab;
                }
                levelData.events[ti] = ee;
            }
            ui.preview?.ForceRefresh();
            levelData.SortByTime();

            for (int i = 0; i < levelData.events.Count; i++) if (tgt.Contains(i) || levelData.events[i].prefabIndex == newPrefab) { }
            notes.RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            ui.FlashStatus(triggersMode ? $"Триггер → {GetTriggerPrefabName(newPrefab)}" : $"Вид → {GetPrefabName(newPrefab)}");
        }

        public void TogglePrefabGrid()
        {
            EnsureRefs();
            EnsurePrefabGrid();
            if (ui.prefabGridPanel == null) return;
            if (ui.prefabGridPanel.activeSelf) HidePrefabGrid();
            else ShowPrefabGrid();
        }

        public void ShowPrefabGrid()
        {
            EnsureRefs();
            EnsurePrefabGrid();
            if (ui.prefabGridPanel == null) return;
            int sel = notes.GetSelectedIndex();
            var levelData = ui.levelData;
            if (levelData != null && sel >= 0 && sel < levelData.events.Count && levelData.events[sel].isTrigger)
            {
                prefabGridTab = 1;
                triggerTypePick = levelData.events[sel].triggerType == 2 ? 2 : 1;
            }
            EnsurePrefabGridTabs();
            RefreshPrefabGridThumbs();
            ui.prefabGridPanel.SetActive(true);
            var rootCanvas = ui.rootCanvas;
            if (rootCanvas != null) ui.prefabGridPanel.transform.SetAsLastSibling();
        }

        public void HidePrefabGrid()
        {
            EnsureRefs();
            if (ui.prefabGridPanel != null) ui.prefabGridPanel.SetActive(false);
        }

        void EnsurePrefabGrid()
        {
            if (ui.prefabGridPanel != null && ui.prefabGridContainer != null) return;

            if (ui.prefabGridPanel != null && ui.prefabGridContainer == null)
            {
                var t = ui.prefabGridPanel.transform.Find("Grid");
                if (t != null) ui.prefabGridContainer = t;
                else ui.prefabGridContainer = ui.prefabGridPanel.transform;
            }
        }

        void EnsurePrefabGridTabs()
        {
            if (ui.prefabGridPanel == null) return;
            RefreshPrefabGridTabs();
            RefreshTrigTypeRow();
        }

        public void SetPrefabGridTab(int tab)
        {
            prefabGridTab = Mathf.Clamp(tab, 0, 1);
            RefreshPrefabGridTabs();
            RefreshTrigTypeRow();
            RefreshPrefabGridThumbs();
        }

        public void SetTriggerTypePick(int type)
        {
            triggerTypePick = Mathf.Clamp(type, 1, 2);
            RefreshTrigTypeRow();
        }

        void RefreshTrigTypeRow()
        {
            if (ui.prefabGridPanel == null) return;
            var row = ui.prefabGridPanel.transform.Find("TrigTypeRow");
            if (row == null) return;
            row.gameObject.SetActive(prefabGridTab == 1);
            for (int t = 1; t <= 2; t++)
            {
                var tf = row.Find("TrigType_" + t);
                if (tf == null) continue;
                var img = tf.GetComponent<Image>();
                if (img != null) img.color = t == triggerTypePick ? new Color(0.2f, 0.6f, 0.3f, 1f) : new Color(0.16f, 0.22f, 0.32f, 1f);
            }
        }

        void RefreshPrefabGridTabs()
        {
            if (ui.prefabGridPanel == null) return;
            for (int t = 0; t <= 1; t++)
            {
                var tf = ui.prefabGridPanel.transform.Find("TabRow/GridTab_" + t);
                if (tf == null) tf = ui.prefabGridPanel.transform.Find("GridTab_" + t);
                if (tf == null) continue;
                var img = tf.GetComponent<Image>();
                if (img != null) img.color = t == prefabGridTab ? new Color(0.2f, 0.6f, 0.3f, 1f) : new Color(0.16f, 0.22f, 0.32f, 1f);
            }
        }

        void RefreshPrefabGridThumbs()
        {
            var prefabGridContainer = ui.prefabGridContainer;
            if (prefabGridContainer == null) return;
            var levelData = ui.levelData;
            var catalog = ui.catalog;

            for (int i = prefabGridContainer.childCount - 1; i >= 0; i--) Destroy(prefabGridContainer.GetChild(i).gameObject);
            bool triggersTab = prefabGridTab == 1;
            int total = 0;
            if (triggersTab)
            {
                total = (catalog != null ? catalog.TriggerCount : 0);
                if (total == 0 && levelData != null) total = levelData.TriggerPrefabCount(catalog);
            }
            else
            {
                total = (catalog != null ? catalog.Count : 0);
                if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            }
            if (total == 0)
            {
                ui.FlashStatus(triggersTab ? "Нет префабов триггеров! Заполни triggerPrefabs в GlobalObstacleCatalog" : "Нет префабов! Заполни GlobalObstacleCatalog в Resources/");
                return;
            }
            for (int i = 0; i < total; i++)
            {
                GameObject pf = null;
                if (triggersTab)
                {
                    pf = catalog != null ? catalog.GetTriggerPrefab(i) : null;
                    if (pf == null && levelData != null) pf = levelData.GetTriggerPrefab(i, catalog);
                }
                else
                {
                    pf = catalog != null ? catalog.GetPrefab(i) : null;
                    if (pf == null && levelData != null) pf = levelData.GetPrefab(i, catalog);
                }
                if (pf == null) continue;
                string name = pf.name;
                if (ui.prefabThumbPrefab == null)
                {
                    ui.FlashStatus("Не назначен prefabThumbPrefab — миниатюры берутся из сцены");
                    return;
                }
                GameObject btnGO = Instantiate(ui.prefabThumbPrefab, prefabGridContainer);
                btnGO.name = $"Thumb_{i}_{name}";
                var btn = btnGO.GetComponent<Button>();
                if (btn == null) continue;
                int idx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { OnThumbClicked(idx); });

                int sel = notes.GetSelectedIndex();
                bool selMatch = false;
                if (levelData != null && sel >= 0 && sel < levelData.events.Count)
                {
                    var sev = levelData.events[sel];
                    selMatch = triggersTab ? (sev.isTrigger && sev.triggerPrefabIndex == i) : (!sev.isTrigger && sev.prefabIndex == i);
                }
                if (selMatch)
                {
                    var ol = btnGO.GetComponent<Outline>(); if (ol == null) ol = btnGO.AddComponent<Outline>();
                    ol.effectColor = Color.green; ol.effectDistance = new Vector2(3, 3);
                }
            }
        }

        void OnThumbClicked(int idx)
        {
            var levelData = ui.levelData;
            int selectedIndex = notes.GetSelectedIndex();
            if (levelData == null || selectedIndex < 0 || selectedIndex >= levelData.events.Count) return;
            var tgt = notes.GetApplyTargets();
            bool triggersTab = prefabGridTab == 1;
            foreach (var ti in tgt)
            {
                if (ti < 0 || ti >= levelData.events.Count) continue;
                var e = levelData.events[ti];
                if (triggersTab)
                {
                    e.isTrigger = true;
                    e.triggerPrefabIndex = idx;
                    e.triggerType = triggerTypePick;
                }
                else
                {
                    e.isTrigger = false;
                    e.triggerType = 0;
                    e.prefabIndex = idx;
                }
                levelData.events[ti] = e;
            }
            if (ui.propPrefabDropdown != null && ui.propPrefabDropdown.options.Count > idx) ui.propPrefabDropdown.SetValueWithoutNotify(idx);
            notes.RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            if (ui.closeGridOnSelect) HidePrefabGrid();
            ui.preview?.ForceRefresh();
            if (triggersTab) ui.FlashStatus($"Триггер → {GetTriggerPrefabName(idx)}");
            else ui.FlashStatus($"Вид → {GetPrefabName(idx)}");
        }

        void OnColorButtonClicked()
        {
            var levelData = ui.levelData;
            int selectedIndex = notes.GetSelectedIndex();
            if (levelData == null || selectedIndex < 0) return;
            var ev = levelData.events[selectedIndex];
            Color newCol = ev.HasCustomColor ? ev.color : Color.white;
            float h = Random.value;
            newCol = Color.HSVToRGB(h, 0.85f, 1f);
            newCol.a = 1f;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                newCol = new Color(0, 0, 0, 0);
            ev.color = newCol;
            levelData.events[selectedIndex] = ev;
            notes.RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            ui.FlashStatus(newCol.a < 0.1f ? "Цвет сброшен к глобальному" : $"Цвет → {ColorUtility.ToHtmlStringRGB(newCol)}");
        }

        void RefreshPrefabDropdown()
        {
            var propPrefabDropdown = ui.propPrefabDropdown;
            if (propPrefabDropdown == null) return;
            var levelData = ui.levelData;
            var catalog = ui.catalog;
            int sel = notes.GetSelectedIndex();
            bool triggersMode = levelData != null && sel >= 0 && sel < levelData.events.Count && levelData.events[sel].isTrigger;
            propPrefabDropdown.ClearOptions();
            int total = 0;
            if (triggersMode)
            {
                total = (catalog != null ? catalog.TriggerCount : 0);
                if (total == 0 && levelData != null) total = levelData.TriggerPrefabCount(catalog);
            }
            else
            {
                total = (catalog != null ? catalog.Count : 0);
                if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            }
            if (total == 0) return;
            var opts = new List<string>();
            for (int i = 0; i < total; i++)
            {
                GameObject pf = null;
                if (triggersMode)
                {
                    pf = catalog != null ? catalog.GetTriggerPrefab(i) : null;
                    if (pf == null && levelData != null) pf = levelData.GetTriggerPrefab(i, catalog);
                }
                else
                {
                    pf = catalog != null ? catalog.GetPrefab(i) : null;
                    if (pf == null && levelData != null) pf = levelData.GetPrefab(i, catalog);
                }
                if (pf == null) continue;
                string name = $"{i}: {pf.name}";
                opts.Add(name);
            }
            propPrefabDropdown.AddOptions(opts);
        }
    }
}
