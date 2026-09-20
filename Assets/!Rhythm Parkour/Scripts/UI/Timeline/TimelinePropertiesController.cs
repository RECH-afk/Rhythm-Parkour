using System.Collections.Generic;
using System.Globalization;
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
        private TextMeshProUGUI propSpeedHintLabel;
        private TextMeshProUGUI propPrefabButtonLabel;

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

        public static bool TryParseFloat(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
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
            var notePropertiesPanel = ui.notePropertiesPanel;
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

            if (ui.propTitleLabel == null)
            {
                var titleGO = new GameObject("Title", typeof(RectTransform));
                titleGO.transform.SetParent(parent, false);
                var ttmp = titleGO.AddComponent<TextMeshProUGUI>(); ttmp.fontSize = 14; ttmp.fontStyle = FontStyles.Bold; ttmp.alignment = TextAlignmentOptions.Center; ttmp.color = Color.white;
                var tle = titleGO.AddComponent<LayoutElement>(); tle.minHeight = 24; tle.flexibleWidth = 1f;
                ui.propTitleLabel = ttmp;
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
                var close = MakePropButton(rowGO.transform, "Закрыть", () => notes.DeselectNote(), new Color(0.25f, 0.25f, 0.28f, 1f));
                close.name = "CloseButton";
            }
        }

        void EnsurePropSpeedRow()
        {
            var notePropertiesPanel = ui.notePropertiesPanel;
            if (notePropertiesPanel == null) return;
            if (ui.propSpeedInput != null)
            {
                EnsurePropSpeedHint();
                return;
            }
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
            var ptmp = phGO.AddComponent<TextMeshProUGUI>(); ptmp.text = "м/с"; ptmp.fontSize = 13; ptmp.color = new Color(1, 1, 1, 0.35f); ptmp.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = art;
            input.textComponent = ttmp;
            input.placeholder = ptmp;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 7;
            var ile = inGO.AddComponent<LayoutElement>(); ile.minHeight = 30; ile.flexibleWidth = 1f;
            ui.propSpeedInput = input;
            ui.propSpeedInput.onEndEdit.AddListener(_ => ApplyPropertiesFromPanel());
            EnsurePropSpeedHint();
        }

        void EnsurePropSpeedHint()
        {
            if (propSpeedHintLabel != null) return;
            var notePropertiesPanel = ui.notePropertiesPanel;
            if (notePropertiesPanel == null) return;
            Transform parent = notePropertiesPanel.transform;
            var scroll = notePropertiesPanel.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.content != null) parent = scroll.content;
            var hintGO = new GameObject("SpeedHint", typeof(RectTransform));
            hintGO.transform.SetParent(parent, false);
            var htmp = hintGO.AddComponent<TextMeshProUGUI>();
            htmp.name = "SpeedHintLabel";
            htmp.fontSize = 11;
            htmp.alignment = TextAlignmentOptions.TopLeft;
            htmp.color = new Color(1, 1, 1, 0.55f);
            var hle = hintGO.AddComponent<LayoutElement>(); hle.minHeight = 16; hle.flexibleWidth = 1f;
            propSpeedHintLabel = htmp;
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
            else if (ui.propTitleLabel != null) ui.propTitleLabel.text = $"Нота #{idx} — {ui.FormatTime(hitTime)} • {GetPrefabName(ev.prefabIndex)}{speedTxt}";

            EnsurePropertiesUI();
            if (propPrefabButtonLabel != null) propPrefabButtonLabel.text = $"Вид: {GetPrefabName(ev.prefabIndex)}";
            if (ui.propSpeedInput != null) { ui.propSpeedInput.gameObject.SetActive(true); ui.propSpeedInput.SetTextWithoutNotify(ev.speed.ToString("0.##", CultureInfo.InvariantCulture)); }
            if (propSpeedHintLabel != null) propSpeedHintLabel.text = notes.SpeedHint(ev.speed);
            if (ui.propPrefabDropdown != null)
            {
                ui.propPrefabDropdown.gameObject.SetActive(true);
                RefreshPrefabDropdown();
                if (ev.prefabIndex < 0 || ev.prefabIndex >= ui.propPrefabDropdown.options.Count)
                {
                    var repaired = levelData.events[idx];
                    repaired.prefabIndex = 0;
                    levelData.events[idx] = repaired;
                    ev = repaired;
                    notes.RefreshNotes();
                    ui.FlashStatus("Вид ноты был вне каталога — явно сброшен в 0");
                }
                if (ui.propPrefabDropdown.options.Count > 0)
                    ui.propPrefabDropdown.SetValueWithoutNotify(ev.prefabIndex);
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

            int total = (catalog != null ? catalog.Count : 0);
            if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            if (total == 0) { ui.FlashStatus("Нет префабов! Заполни GlobalObstacleCatalog в Resources/"); return; }
            int newPrefab = ev.prefabIndex;
            if (ui.propPrefabDropdown != null && ui.propPrefabDropdown.options.Count > 0)
            {
                if (ui.propPrefabDropdown.value < 0 || ui.propPrefabDropdown.value >= total) { ui.FlashStatus("Вид вне каталога"); return; }
                newPrefab = ui.propPrefabDropdown.value;
            }

            var tgt = notes.GetApplyTargets();
            float newSpeed = ev.speed;
            bool hasSpeed = false;
            if (ui.propSpeedInput != null && ui.propSpeedInput.gameObject.activeInHierarchy)
            {
                if (string.IsNullOrWhiteSpace(ui.propSpeedInput.text)) { ui.FlashStatus("Скорость: введите число 1–60 м/с"); return; }
                else if (TryParseFloat(ui.propSpeedInput.text, out float sv))
                {
                    if (sv < 1f || sv > 60f) { ui.FlashStatus("Скорость: только 1–60 м/с, без округления"); return; }
                    newSpeed = sv; hasSpeed = true;
                }
                else { ui.FlashStatus("Скорость: число м/с 1–60"); return; }
            }
            foreach (var ti in tgt)
            {
                if (ti < 0 || ti >= levelData.events.Count) continue;
                var ee = levelData.events[ti];
                ee.prefabIndex = newPrefab;
                if (hasSpeed && Mathf.Abs(ee.speed - newSpeed) > 0.001f)
                {
                    float hb = notes.GetHitBeat(ee);
                    ee.speed = newSpeed;
                    float travel = notes.GetTravelForEvent(ee);
                    float spawnT = levelData.BeatToTime(hb) - travel;
                    ee.beat = levelData.TimeToBeat(spawnT);
                    ee.time = spawnT;
                }
                levelData.events[ti] = ee;
            }
            ui.preview?.ForceRefresh();
            levelData.SortByTime();

            for (int i = 0; i < levelData.events.Count; i++) if (tgt.Contains(i) || levelData.events[i].prefabIndex == newPrefab) { }
            notes.RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            ui.FlashStatus($"Вид → {GetPrefabName(newPrefab)}");
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

            if (ui.prefabGridPanel == null)
            {
                var rootCanvas = ui.rootCanvas;
                if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
                if (rootCanvas == null) return;
                var panelGO = new GameObject("PrefabGridPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                panelGO.transform.SetParent(rootCanvas.transform, false);
                var rt = panelGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(560, 420);
                var img = panelGO.GetComponent<Image>(); img.color = new Color(0.12f, 0.12f, 0.14f, 0.96f); img.raycastTarget = true;
                var vlg = panelGO.GetComponent<VerticalLayoutGroup>(); vlg.padding = new RectOffset(12, 12, 12, 12); vlg.spacing = 8; vlg.childAlignment = TextAnchor.UpperCenter; vlg.childControlWidth = true; vlg.childControlHeight = false;

                var titleGO = new GameObject("Title", typeof(RectTransform));
                titleGO.transform.SetParent(panelGO.transform, false);
                var ttmp = titleGO.AddComponent<TextMeshProUGUI>(); ttmp.text = "Выбор вида препятствия"; ttmp.fontSize = 16; ttmp.alignment = TextAlignmentOptions.Center; ttmp.color = Color.white;
                var le = titleGO.AddComponent<LayoutElement>(); le.minHeight = 24;

                var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
                scrollGO.transform.SetParent(panelGO.transform, false);
                var srt = scrollGO.GetComponent<RectTransform>(); srt.sizeDelta = new Vector2(0, 320);
                var sle = scrollGO.AddComponent<LayoutElement>(); sle.flexibleHeight = 1; sle.minHeight = 200;
                var sImg = scrollGO.GetComponent<Image>(); sImg.color = new Color(0, 0, 0, 0.15f);
                scrollGO.GetComponent<Mask>().showMaskGraphic = false;
                var scroll = scrollGO.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
                var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
                gridGO.transform.SetParent(scrollGO.transform, false);
                var grt = gridGO.GetComponent<RectTransform>(); grt.anchorMin = new Vector2(0, 1); grt.anchorMax = new Vector2(1, 1); grt.pivot = new Vector2(0.5f, 1); grt.anchoredPosition = Vector2.zero; grt.sizeDelta = new Vector2(0, 0);
                var glg = gridGO.GetComponent<GridLayoutGroup>(); glg.cellSize = new Vector2(80, 80); glg.spacing = new Vector2(8, 8); glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 6; glg.childAlignment = TextAnchor.UpperCenter;
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
                var cimg = closeGO.GetComponent<Image>(); cimg.color = new Color(0.5f, 0.2f, 0.2f, 1);
                var cbtn = closeGO.GetComponent<Button>();
                var ctxt = new GameObject("Text", typeof(RectTransform)); ctxt.transform.SetParent(closeGO.transform, false);
                var ctrt = ctxt.GetComponent<RectTransform>(); ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one; ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;
                var ctmp = ctxt.AddComponent<TextMeshProUGUI>(); ctmp.text = "Закрыть"; ctmp.fontSize = 14; ctmp.alignment = TextAlignmentOptions.Center; ctmp.color = Color.white;
                cbtn.onClick.AddListener(HidePrefabGrid);
                ui.prefabGridPanel = panelGO;
                ui.prefabGridContainer = grt.transform;
                ui.prefabGridPanel.SetActive(false);
            }
        }

        void RefreshPrefabGridThumbs()
        {
            var prefabGridContainer = ui.prefabGridContainer;
            if (prefabGridContainer == null) return;
            var levelData = ui.levelData;
            var catalog = ui.catalog;

            for (int i = prefabGridContainer.childCount - 1; i >= 0; i--) Destroy(prefabGridContainer.GetChild(i).gameObject);
            int total = (catalog != null ? catalog.Count : 0);
            if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            if (total == 0)
            {
                ui.FlashStatus("Нет префабов! Заполни GlobalObstacleCatalog в Resources/");
                return;
            }
            for (int i = 0; i < total; i++)
            {
                var pf = catalog != null ? catalog.GetPrefab(i) : null;
                if (pf == null && levelData != null) pf = levelData.GetPrefab(i, catalog);
                if (pf == null) continue;
                string name = pf.name;
                GameObject btnGO;
                if (ui.prefabThumbPrefab != null) btnGO = Instantiate(ui.prefabThumbPrefab, prefabGridContainer);
                else
                {
                    btnGO = new GameObject($"Thumb_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGO.transform.SetParent(prefabGridContainer, false);
                    var rt = btnGO.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(80, 80);
                    var img = btnGO.GetComponent<Image>(); img.color = new Color(0.22f, 0.22f, 0.24f, 1f);

                    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGO.transform.SetParent(btnGO.transform, false);
                    var irt = iconGO.GetComponent<RectTransform>(); irt.anchorMin = new Vector2(0.1f, 0.2f); irt.anchorMax = new Vector2(0.9f, 0.85f); irt.offsetMin = irt.offsetMax = Vector2.zero;
                    var iimg = iconGO.GetComponent<Image>(); iimg.color = Color.white; iimg.preserveAspect = true;

#if UNITY_EDITOR
                    try
                    {
                        var tex = UnityEditor.AssetPreview.GetAssetPreview(pf);
                        if (tex != null) iimg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        else
                        {
                            var mini = UnityEditor.AssetPreview.GetMiniThumbnail(pf);
                            if (mini != null) iimg.sprite = Sprite.Create(mini, new Rect(0, 0, mini.width, mini.height), new Vector2(0.5f, 0.5f));
                        }
                    }
                    catch { }
#endif
                    if (iimg.sprite == null)
                    {
                        iimg.color = notes.GetColorForPrefab(i);
                    }

                    var lblGO = new GameObject("Label", typeof(RectTransform));
                    lblGO.transform.SetParent(btnGO.transform, false);
                    var lrt = lblGO.GetComponent<RectTransform>(); lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0.22f); lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                    var ltmp = lblGO.AddComponent<TextMeshProUGUI>(); ltmp.text = $"{i}: {name}"; ltmp.fontSize = 8; ltmp.alignment = TextAlignmentOptions.Center; ltmp.color = new Color(1, 1, 1, 0.9f); ltmp.enableWordWrapping = false; ltmp.overflowMode = TextOverflowModes.Ellipsis;
                }
                var btn = btnGO.GetComponent<Button>();
                int idx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { OnThumbClicked(idx); });

                int sel = notes.GetSelectedIndex();
                if (levelData != null && sel >= 0 && sel < levelData.events.Count && levelData.events[sel].prefabIndex == i)
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
            foreach (var ti in tgt) { if (ti < 0 || ti >= levelData.events.Count) continue; var e = levelData.events[ti]; e.prefabIndex = idx; levelData.events[ti] = e; }
            if (ui.propPrefabDropdown != null && ui.propPrefabDropdown.options.Count > idx) ui.propPrefabDropdown.SetValueWithoutNotify(idx);
            notes.RefreshNotes();
            ShowPropertiesPanel(selectedIndex);
            if (ui.closeGridOnSelect) HidePrefabGrid();
            ui.preview?.ForceRefresh();
            ui.FlashStatus($"Вид → {GetPrefabName(idx)}");
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
            propPrefabDropdown.ClearOptions();
            int total = (catalog != null ? catalog.Count : 0);
            if (total == 0 && levelData != null) total = levelData.PrefabCount(catalog);
            if (total == 0) return;
            var opts = new List<string>();
            for (int i = 0; i < total; i++)
            {
                var pf = catalog != null ? catalog.GetPrefab(i) : null;
                if (pf == null && levelData != null) pf = levelData.GetPrefab(i, catalog);
                if (pf == null) continue;
                string name = $"{i}: {pf.name}";
                opts.Add(name);
            }
            propPrefabDropdown.AddOptions(opts);
        }
    }
}
