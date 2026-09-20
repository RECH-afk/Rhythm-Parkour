using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RKS.RhythmParkour.Core;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimelineGridView : RKSBehaviour
    {
        private TimelineUI ui;
        private readonly List<GameObject> gridLinePool = new List<GameObject>();
        private readonly List<GameObject> gridLabelPool = new List<GameObject>();
        private float lastGridRefreshTime;
        private int lastGridLabelEvery = -1;
        private float lastGridZoom = -999f;
        private float lastGridStepSec = -1f;
        private float lastGridClipLen = -1f;

        protected override void Awake()
        {
            ui = GetComponent<TimelineUI>();
        }

        public void Invalidate()
        {
            lastGridClipLen = -1f;
        }

        public void RefreshGrid(bool force = false)
        {
            if (ui == null) ui = GetComponent<TimelineUI>();
            if (ui == null || ui.gridContainer == null) return;
            float clipLen = ui.GetClipLength();
            if (clipLen < 0.01f)
            {
                foreach (var go in gridLinePool) if (go) go.SetActive(false);
                foreach (var go in gridLabelPool) if (go) go.SetActive(false);
                ui.gridContainer.gameObject.SetActive(false);
                return;
            }
            float pps = ui.pixelsPerSecond * Mathf.Max(0.1f, ui.zoom);
            int labelEvery = 1;
            if (pps < 25f) labelEvery = 10;
            else if (pps < 50f) labelEvery = 5;
            else if (pps < 90f) labelEvery = 2;
            else labelEvery = 1;

            bool need = force
                || Mathf.Abs(clipLen - lastGridClipLen) > 0.1f
                || Mathf.Abs(ui.pixelsPerSecond - lastGridStepSec) > 1f
                || labelEvery != lastGridLabelEvery
                || gridLinePool.Count == 0;
            if (!need)
            {
                if (Mathf.Abs(ui.zoom - lastGridZoom) < 0.05f) return;
            }

            if (!force && Time.unscaledTime - lastGridRefreshTime < 0.08f) return;
            lastGridZoom = ui.zoom;
            lastGridClipLen = clipLen;
            lastGridStepSec = ui.pixelsPerSecond;
            lastGridLabelEvery = labelEvery;
            lastGridRefreshTime = Time.unscaledTime;

            if (gridLinePool.Count == 0 && ui.gridContainer.childCount > 0)
            {
                for (int i = ui.gridContainer.childCount - 1; i >= 0; i--)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(ui.gridContainer.GetChild(i).gameObject);
                    else Destroy(ui.gridContainer.GetChild(i).gameObject);
#else
                    Destroy(ui.gridContainer.GetChild(i).gameObject);
#endif
                }
            }

            ui.gridContainer.gameObject.SetActive(true);
            int totalSecs = Mathf.CeilToInt(clipLen);
            int neededLines = totalSecs + 1;

            while (gridLinePool.Count < neededLines)
            {
                var lineGO = new GameObject($"Grid_Pooled_{gridLinePool.Count}s", typeof(RectTransform), typeof(Image));
                lineGO.transform.SetParent(ui.gridContainer, false);
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
                if (is10) img.color = ui.gridSec5Color;
                else if (is5) img.color = Color.Lerp(ui.gridSecColor, ui.gridSec5Color, 0.5f);
                else img.color = ui.gridSecColor * 0.65f;
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
                labelGO.transform.SetParent(ui.gridContainer, false);
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
                var bgImg = bgGO.GetComponent<Image>(); bgImg.color = new Color(0, 0, 0, 0.28f); bgImg.raycastTarget = false;
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
                tmp.text = sec == 0 ? "0:00" : ui.FormatTime(sec);
                tmp.fontSize = is10 ? 11f : 10f;
                tmp.color = is10 ? new Color(1, 1, 1, 0.85f) : is5 ? new Color(1, 1, 1, 0.65f) : new Color(1, 1, 1, 0.45f);
            }
            for (int i = labelIdx; i < gridLabelPool.Count; i++) if (gridLabelPool[i]) gridLabelPool[i].SetActive(false);
        }
    }
}
