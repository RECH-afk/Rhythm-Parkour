using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Rhythm;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimingsPanel : RKSBehaviour
    {
        public TimelineUI timelineUI;
        public CollapsibleTabsUI tabs;
        public string timingsTabName = "ТАЙМИНГИ";
        public int timingsTabIndex = 2;

        GameObject root;
        TMP_InputField bpmInput;
        TMP_InputField offsetInput;
        TMP_InputField speedInput;
        TextMeshProUGUI speedHintLabel;
        TextMeshProUGUI statusLabel;
        TextMeshProUGUI quantLabel;
        TextMeshProUGUI snapLabel;
        TextMeshProUGUI autoQuantLabel;

        protected override void OnReady()
        {
            EnsurePanel();
        }

        void OnEnable()
        {
            RefreshAll();
        }

        static bool TryParseFloat(string s, out float v)
        {
            v = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        void EnsurePanel()
        {
            if (root != null) return;
            if (timelineUI == null) timelineUI = GetComponent<TimelineUI>();
            if (timelineUI == null) return;
            if (tabs == null) tabs = FindFirstObjectByType<CollapsibleTabsUI>();

            GameObject refWin = null;
            if (tabs != null && tabs.panels != null && tabs.panels.Length > 0 && tabs.panels[0] != null)
                refWin = tabs.panels[0];
            if (tabs == null)
                Debug.LogWarning("[Timings] CollapsibleTabsUI не найден — вкладка не будет зарегистрирована в табах.", this);
            Transform parent = refWin != null && refWin.transform.parent != null
                ? refWin.transform.parent
                : timelineUI.transform;
            if (parent == null) return;

            root = new GameObject("TimingsWindow", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rrt = root.GetComponent<RectTransform>();
            var refRt = refWin != null ? refWin.GetComponent<RectTransform>() : null;
            if (refRt != null)
            {
                rrt.anchorMin = refRt.anchorMin;
                rrt.anchorMax = refRt.anchorMax;
                rrt.anchoredPosition = refRt.anchoredPosition;
                rrt.sizeDelta = refRt.sizeDelta;
                rrt.pivot = refRt.pivot;
            }
            else
            {
                rrt.anchorMin = Vector2.zero;
                rrt.anchorMax = Vector2.one;
                rrt.offsetMin = Vector2.zero;
                rrt.offsetMax = Vector2.zero;
            }
            root.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.97f);
            var vlg = root.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            MakeLabel(root.transform, "ТАЙМИНГИ", 16, TextAlignmentOptions.Center);

            var bpmRow = MakeRow(root.transform);
            MakeLabel(bpmRow, "BPM", 13, TextAlignmentOptions.MidlineLeft, 60f);
            bpmInput = MakeInput(bpmRow);
            bpmInput.onEndEdit.AddListener(OnBpmSubmit);
            MakeButton(bpmRow, "Детект", OnDetectBpm, 90f);

            var offRow = MakeRow(root.transform);
            MakeLabel(offRow, "Offset", 13, TextAlignmentOptions.MidlineLeft, 60f);
            offsetInput = MakeInput(offRow);
            offsetInput.onEndEdit.AddListener(OnOffsetSubmit);
            MakeButton(offRow, "−", () => NudgeOffset(-0.05f), 44f);
            MakeButton(offRow, "+", () => NudgeOffset(0.05f), 44f);

            var qRow = MakeRow(root.transform);
            MakeLabel(qRow, "Квант", 13, TextAlignmentOptions.MidlineLeft, 60f);
            quantLabel = MakeLabel(qRow, "", 13, TextAlignmentOptions.MidlineLeft, 70f);
            MakeButton(qRow, "1", () => SetQuantStep(1f), 52f);
            MakeButton(qRow, "1/2", () => SetQuantStep(0.5f), 52f);
            MakeButton(qRow, "1/4", () => SetQuantStep(0.25f), 52f);
            MakeButton(qRow, "1/8", () => SetQuantStep(0.125f), 52f);

            var snapRow = MakeRow(root.transform);
            var snapBtn = MakeButton(snapRow, "", OnToggleSnap, 0f);
            snapLabel = snapBtn.GetComponentInChildren<TextMeshProUGUI>();
            var aqBtn = MakeButton(snapRow, "", OnToggleAutoQuant, 0f);
            autoQuantLabel = aqBtn.GetComponentInChildren<TextMeshProUGUI>();

            MakeButton(root.transform, "Привязать все ноты к сетке", OnResnapAll, 0f);

            var spdRow = MakeRow(root.transform);
            MakeLabel(spdRow, "Скорость новых", 13, TextAlignmentOptions.MidlineLeft, 120f);
            speedInput = MakeInput(spdRow);
            speedInput.onEndEdit.AddListener(OnSpeedSubmit);

            speedHintLabel = MakeLabel(root.transform, "", 11, TextAlignmentOptions.TopLeft, 0f);
            speedHintLabel.color = new Color(1, 1, 1, 0.55f);
            var speedHintLE = speedHintLabel.gameObject.AddComponent<LayoutElement>();
            speedHintLE.minHeight = 18f;
            speedHintLE.flexibleWidth = 1f;

            statusLabel = MakeLabel(root.transform, "", 12, TextAlignmentOptions.TopLeft, 0f);
            statusLabel.color = new Color(1, 1, 1, 0.6f);
            var sle = statusLabel.gameObject.AddComponent<LayoutElement>();
            sle.minHeight = 44f;
            sle.flexibleWidth = 1f;

            root.SetActive(false);

            if (tabs != null)
            {
                if (tabs.panels == null || tabs.panels.Length <= timingsTabIndex)
                {
                    var arr = new GameObject[timingsTabIndex + 1];
                    if (tabs.panels != null)
                        for (int i = 0; i < tabs.panels.Length && i < arr.Length; i++) arr[i] = tabs.panels[i];
                    tabs.panels = arr;
                }
                tabs.panels[timingsTabIndex] = root;
                if (tabs.tabNames == null || tabs.tabNames.Length <= timingsTabIndex)
                {
                    var narr = new string[timingsTabIndex + 1];
                    if (tabs.tabNames != null)
                        for (int i = 0; i < tabs.tabNames.Length && i < narr.Length; i++) narr[i] = tabs.tabNames[i];
                    tabs.tabNames = narr;
                }
                if (string.IsNullOrEmpty(tabs.tabNames[timingsTabIndex]))
                    tabs.tabNames[timingsTabIndex] = timingsTabName;
            }

            RefreshAll();
        }

        Transform MakeRow(Transform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 32;
            le.flexibleWidth = 1f;
            return go.transform;
        }

        TextMeshProUGUI MakeLabel(Transform parent, string text, int size, TextAlignmentOptions align, float width = 0f)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = new Color(1, 1, 1, 0.85f);
            if (width > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.minWidth = width;
                le.preferredWidth = width;
            }
            return tmp;
        }

        Button MakeButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, float minWidth)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.32f, 1f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 32;
            if (minWidth > 0f) { le.minWidth = minWidth; le.preferredWidth = minWidth; }
            else le.flexibleWidth = 1f;
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            return btn;
        }

        TMP_InputField MakeInput(Transform parent)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);
            var input = go.GetComponent<TMP_InputField>();
            var areaGO = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGO.transform.SetParent(go.transform, false);
            var art = areaGO.GetComponent<RectTransform>();
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(6, 2);
            art.offsetMax = new Vector2(-6, -2);
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(areaGO.transform, false);
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var ttmp = txtGO.AddComponent<TextMeshProUGUI>();
            ttmp.fontSize = 13;
            ttmp.color = Color.white;
            ttmp.alignment = TextAlignmentOptions.MidlineLeft;
            var phGO = new GameObject("Placeholder", typeof(RectTransform));
            phGO.transform.SetParent(areaGO.transform, false);
            var prt = phGO.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            var ptmp = phGO.AddComponent<TextMeshProUGUI>();
            ptmp.fontSize = 13;
            ptmp.color = new Color(1, 1, 1, 0.35f);
            ptmp.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = art;
            input.textComponent = ttmp;
            input.placeholder = ptmp;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.characterLimit = 8;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 30;
            le.flexibleWidth = 1f;
            return input;
        }

        void RefreshAll()
        {
            if (root == null) EnsurePanel();
            if (root == null) return;
            var d = timelineUI != null ? timelineUI.levelData : null;
            if (bpmInput != null) bpmInput.SetTextWithoutNotify(d != null ? d.bpm.ToString("0.##", CultureInfo.InvariantCulture) : "");
            if (offsetInput != null) offsetInput.SetTextWithoutNotify(d != null ? d.offset.ToString("0.##", CultureInfo.InvariantCulture) : "");
            if (speedInput != null && timelineUI != null) speedInput.SetTextWithoutNotify(timelineUI.defaultNoteSpeed.ToString("0.##", CultureInfo.InvariantCulture));
            SyncLabels();
            SyncSpeedHint();
        }

        void SyncSpeedHint()
        {
            if (speedHintLabel == null || timelineUI == null) return;

            speedHintLabel.text = timelineUI.SpeedHint(timelineUI.defaultNoteSpeed);
        }

        void SyncLabels()
        {
            if (timelineUI == null) return;
            if (quantLabel != null) quantLabel.text = "Квант: " + QuantName(timelineUI.quantStep);
            if (snapLabel != null) snapLabel.text = "Привязка: " + (timelineUI.snapToGrid ? "ВКЛ" : "ВЫКЛ");
            if (autoQuantLabel != null) autoQuantLabel.text = "Авто-квант: " + (timelineUI.autoQuantize ? "ВКЛ" : "ВЫКЛ");
            SyncSpeedHint();
        }

        static string QuantName(float q)
        {
            if (q >= 0.99f) return "1";
            if (q >= 0.49f) return "1/2";
            if (q >= 0.24f) return "1/4";
            return "1/8";
        }

        void Status(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
            Debug.Log("[Timings] " + msg, this);
        }

        void OnBpmSubmit(string s)
        {
            if (timelineUI == null || timelineUI.levelData == null) return;
            if (!TryParseFloat(s, out float v)) { Status("BPM: введите число 40–300"); RefreshAll(); return; }
            if (!BpmDetector.ApplyBpm(timelineUI.levelData, v, true)) { Status("BPM: только 40–300, без округления"); RefreshAll(); return; }
            timelineUI.RefreshAll();
            RefreshAll();
            Status($"BPM → {timelineUI.levelData.bpm:0.##}, биты нот сохранены");
        }

        void OnDetectBpm()
        {
            if (timelineUI == null || timelineUI.levelData == null || timelineUI.levelData.music == null)
            {
                Status("Нет аудиотрека — загрузите аудио");
                return;
            }
            try
            {
                var det = BpmDetector.Detect(timelineUI.levelData.music);
                if (!BpmDetector.ApplyBpm(timelineUI.levelData, det.bpm, true)) { Status($"Детект дал {det.bpm:0.##} — вне 40–300, не применён"); return; }
                if (timelineUI.bpmOutputText != null) timelineUI.bpmOutputText.text = $"BPM: {timelineUI.levelData.bpm:0}";
                timelineUI.RefreshAll();
                RefreshAll();
                Status($"Детект: BPM {det.bpm:0.##} (точн. {det.confidence:P0}), смещение {det.offset:0.##}с");
            }
            catch (System.Exception e) { Status("Детект не удался: " + e.Message); }
        }

        void OnOffsetSubmit(string s)
        {
            if (timelineUI == null || timelineUI.levelData == null) return;
            if (!TryParseFloat(s, out float v)) { Status("Offset: число в секундах"); RefreshAll(); return; }
            timelineUI.levelData.offset = v;
            timelineUI.RefreshAll();
            RefreshAll();
            Status($"Offset → {v:0.##}с");
        }

        void NudgeOffset(float d)
        {
            if (timelineUI == null || timelineUI.levelData == null) return;
            timelineUI.levelData.offset += d;
            timelineUI.RefreshAll();
            RefreshAll();
            Status($"Offset → {timelineUI.levelData.offset:0.##}с");
        }

        void SetQuantStep(float q)
        {
            if (timelineUI == null) return;
            timelineUI.SetQuant(q);
            SyncLabels();
            Status($"Квант → {QuantName(q)}");
        }

        void OnToggleSnap()
        {
            if (timelineUI == null) return;
            timelineUI.snapToGrid = !timelineUI.snapToGrid;
            SyncLabels();
            Status("Привязка: " + (timelineUI.snapToGrid ? "ВКЛ" : "ВЫКЛ"));
        }

        void OnToggleAutoQuant()
        {
            if (timelineUI == null) return;
            timelineUI.autoQuantize = !timelineUI.autoQuantize;
            SyncLabels();
            Status("Авто-квант: " + (timelineUI.autoQuantize ? "ВКЛ" : "ВЫКЛ"));
        }

        void OnResnapAll()
        {
            if (timelineUI == null) return;
            int n = timelineUI.ResnapAllNotes();
            RefreshAll();
            Status(n > 0 ? $"Привязано нот: {n}" : "Все ноты уже на сетке");
        }

        void OnSpeedSubmit(string s)
        {
            if (timelineUI == null) return;

            if (string.IsNullOrWhiteSpace(s)) { Status("Скорость: введите число 1–60 м/с"); RefreshAll(); return; }
            if (!TryParseFloat(s, out float v)) { Status("Скорость: введите число 1–60 м/с"); RefreshAll(); return; }
            if (v < 1f || v > 60f) { Status("Скорость: только 1–60 м/с, без округления"); RefreshAll(); return; }
            timelineUI.defaultNoteSpeed = v;
            RefreshAll();
            Status($"Скорость новых нот → {timelineUI.SpeedHint(timelineUI.defaultNoteSpeed)}");
        }
    }
}
