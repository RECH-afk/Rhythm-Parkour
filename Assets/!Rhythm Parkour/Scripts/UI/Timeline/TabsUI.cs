using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TabsUI : RKSBehaviour
    {
        [Header("Tabs")]
        public GameObject[] panels;
        public int defaultIndex = 0;

        protected const float ParkOffsetX = 10000f;
        protected readonly Dictionary<GameObject, Vector2> homePositions = new Dictionary<GameObject, Vector2>();

        int current = -1;

        protected override void OnReady()
        {
            Show(defaultIndex);
        }

        protected Vector2 HomeOf(GameObject panel)
        {
            if (panel == null) return Vector2.zero;
            if (!homePositions.TryGetValue(panel, out var home))
            {
                RectTransform rt = panel.GetComponent<RectTransform>();
                home = rt != null ? rt.anchoredPosition : Vector2.zero;
                if (Mathf.Abs(home.x) > ParkOffsetX * 0.5f) home.x = 0f;
                homePositions[panel] = home;
            }
            return home;
        }

        protected static void SetIgnoreLayout(GameObject panel, bool ignore)
        {
            if (panel == null) return;
            var le = panel.GetComponent<LayoutElement>();
            if (le == null) le = panel.AddComponent<LayoutElement>();
            le.ignoreLayout = ignore;
        }

        protected void ParkPanel(GameObject panel)
        {
            if (panel == null) return;
            Vector2 home = HomeOf(panel);
            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = home + new Vector2(ParkOffsetX, 0f);
            SetIgnoreLayout(panel, true);
        }

        protected void RestorePanel(GameObject panel)
        {
            if (panel == null) return;
            Vector2 home = HomeOf(panel);
            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = home;
            SetIgnoreLayout(panel, false);
        }

        public virtual void Show(int index)
        {
            if (panels == null || panels.Length == 0) return;
            if (index < 0 || index >= panels.Length) return;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                if (!panels[i].activeSelf) panels[i].SetActive(true);
                if (i == index) RestorePanel(panels[i]);
                else ParkPanel(panels[i]);
            }
            current = index;
        }

        public void ShowByName(string name)
        {
            if (panels == null) return;
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && panels[i].name == name) { Show(i); return; }
        }

        public virtual void Next()
        {
            if (panels == null || panels.Length == 0) return;
            Show((current + 1) % panels.Length);
        }

        public virtual void Prev()
        {
            if (panels == null || panels.Length == 0) return;
            Show((current - 1 + panels.Length) % panels.Length);
        }

        public int CurrentIndex => current;

        protected void SetCurrent(int index) { current = index; }
    }
}
