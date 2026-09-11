using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class CollapsibleTabsUI : TabsUI
    {
        [Header("Tabs")]
        public string[] tabNames;
        [Header("Collapse")]
        public float slideDuration = 0.4f;
        public float toggleDockY = 70f;
        [Header("Tab Transition")]
        public float tabSwitchDuration = 0.35f;

        private Button prevButton;
        private Button nextButton;
        private Button toggleButton;
        private TextMeshProUGUI toggleLabel;

        bool collapsed;
        bool isAnimating;
        float origY;
        float hiddenY;
        float toggleOrigY;
        float toggleHiddenY;
        Vector2 prevHome;
        Vector2 nextHome;
        private readonly Dictionary<GameObject, Vector2> tabHomePositions = new Dictionary<GameObject, Vector2>();
        Tween slideTween;
        Tween tabTween;

        protected override void OnReady()
        {
            prevButton = FindButton("ButtonLeft", nameof(Prev));
            nextButton = FindButton("ButtonRight", nameof(Next));
            toggleButton = FindButton("ButtonCurrentWindow", nameof(ToggleCollapse));
            if (toggleButton != null)
                toggleLabel = toggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
            collapsed = false;
            base.OnReady();
            RefreshArrows();
        }

        protected override void OnDisposed()
        {
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            if (tabTween != null && tabTween.IsActive()) tabTween.Kill();
        }

        public override void Show(int index)
        {
            if (panels == null || panels.Length == 0) return;
            if (index < 0 || index >= panels.Length) return;
            int prev = CurrentIndex;
            base.Show(index);
            if (collapsed)
            {
                if (panels[index] != null)
                {
                    RectTransform rt = panels[index].GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, hiddenY);
                }
                RefreshLabel();
                return;
            }
            GameObject oldPanel = (prev >= 0 && prev < panels.Length && prev != index) ? panels[prev] : null;
            GameObject newPanel = panels[index];
            if (oldPanel == null || newPanel == null)
            {
                RefreshLabel();
                return;
            }
            AnimateTabTransition(oldPanel, newPanel, index > prev ? 1 : -1);
            RefreshLabel();
        }

        private void AnimateTabTransition(GameObject oldPanel, GameObject newPanel, int dir)
        {
            if (tabTween != null && tabTween.IsActive()) tabTween.Kill();
            RectTransform oldRt = oldPanel.GetComponent<RectTransform>();
            RectTransform newRt = newPanel.GetComponent<RectTransform>();
            Vector2 oldHome = TabHome(oldPanel);
            Vector2 newHome = TabHome(newPanel);
            float dist = (newRt != null && newRt.rect.width > 1f) ? newRt.rect.width : 800f;
            oldPanel.SetActive(true);
            if (oldRt != null) oldRt.anchoredPosition = oldHome;
            if (newRt != null) newRt.anchoredPosition = new Vector2(newHome.x - dist * dir, newHome.y);
            Sequence seq = DOTween.Sequence();
            if (oldRt != null) seq.Join(oldRt.DOAnchorPosX(oldHome.x + dist * dir, tabSwitchDuration).SetEase(Ease.InCubic));
            if (newRt != null) seq.Join(newRt.DOAnchorPosX(newHome.x, tabSwitchDuration).SetEase(Ease.OutCubic));
            seq.OnComplete(() =>
            {
                oldPanel.SetActive(false);
                if (oldRt != null) oldRt.anchoredPosition = oldHome;
            });
            tabTween = seq;
        }

        private Vector2 TabHome(GameObject panel)
        {
            if (panel == null) return Vector2.zero;
            if (!tabHomePositions.TryGetValue(panel, out var home))
            {
                RectTransform rt = panel.GetComponent<RectTransform>();
                home = rt != null ? rt.anchoredPosition : Vector2.zero;
                tabHomePositions[panel] = home;
            }
            return home;
        }

        public override void Next()
        {
            if (collapsed) return;
            base.Next();
        }

        public override void Prev()
        {
            if (collapsed) return;
            base.Prev();
        }

        public void ToggleCollapse()
        {
            SetCollapsed(!collapsed);
        }

        public void SetCollapsed(bool value)
        {
            if (panels == null || panels.Length == 0) return;
            if (collapsed == value) return;
            if (isAnimating) return;
            collapsed = value;
            isAnimating = true;
            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            RectTransform rt = ActivePanelRect();
            RectTransform toggleRt = toggleButton != null ? (RectTransform)toggleButton.transform : null;
            Sequence seq = DOTween.Sequence();
            if (collapsed)
            {
                if (rt != null)
                {
                    origY = rt.anchoredPosition.y;
                    hiddenY = origY - rt.rect.height - 100f;
                    seq.Join(rt.DOAnchorPosY(hiddenY, slideDuration).SetEase(Ease.InQuad));
                }
                Vector2? toggleSpot = null;
                if (toggleRt != null)
                {
                    toggleOrigY = toggleRt.anchoredPosition.y;
                    toggleHiddenY = Mathf.Max(toggleOrigY - (origY - hiddenY), toggleDockY);
                    toggleSpot = new Vector2(toggleRt.anchoredPosition.x, toggleHiddenY);
                    seq.Join(toggleRt.DOAnchorPosY(toggleHiddenY, slideDuration).SetEase(Ease.InOutCubic));
                }
                FlyArrowTo(prevButton, ref prevHome, toggleSpot, seq, true);
                FlyArrowTo(nextButton, ref nextHome, toggleSpot, seq, true);
            }
            else
            {
                if (rt != null)
                    seq.Join(rt.DOAnchorPosY(origY, slideDuration).SetEase(Ease.OutCubic));
                if (toggleRt != null)
                    seq.Join(toggleRt.DOAnchorPosY(toggleOrigY, slideDuration).SetEase(Ease.InOutCubic));
                FlyArrowTo(prevButton, ref prevHome, toggleSpotOfToggle(), seq, false);
                FlyArrowTo(nextButton, ref nextHome, toggleSpotOfToggle(), seq, false);
            }
            seq.OnComplete(() =>
            {
                isAnimating = false;
                RefreshArrows();
            });
            slideTween = seq;
            RefreshLabel();
        }

        private Vector2? toggleSpotOfToggle()
        {
            if (toggleButton == null) return null;
            RectTransform toggleRt = (RectTransform)toggleButton.transform;
            return new Vector2(toggleRt.anchoredPosition.x, toggleHiddenY);
        }

        private void FlyArrowTo(Button arrow, ref Vector2 home, Vector2? toggleSpot, Sequence seq, bool hide)
        {
            if (arrow == null) return;
            RectTransform art = (RectTransform)arrow.transform;
            if (hide)
            {
                arrow.gameObject.SetActive(true);
                home = art.anchoredPosition;
                Vector2 dest = toggleSpot ?? home;
                seq.Join(art.DOAnchorPos(dest, slideDuration * 0.9f).SetEase(Ease.InCubic));
                seq.Join(art.DOScale(Vector3.zero, slideDuration * 0.9f).SetEase(Ease.InCubic));
            }
            else
            {
                arrow.gameObject.SetActive(true);
                Vector2 from = toggleSpot ?? home;
                art.anchoredPosition = from;
                art.localScale = Vector3.zero;
                seq.Join(art.DOAnchorPos(home, slideDuration).SetEase(Ease.OutBack));
                seq.Join(art.DOScale(Vector3.one, slideDuration).SetEase(Ease.OutBack));
            }
        }

        public bool IsCollapsed => collapsed;

        private Button FindButton(string objectName, string methodName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogWarning("[TabsUI] GameObject '" + objectName + "' not found.", this);
                return null;
            }
            Button btn = go.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning("[TabsUI] No Button on '" + objectName + "'.", this);
                return null;
            }
            if (btn.onClick.GetPersistentEventCount() == 0)
                Debug.LogWarning("[TabsUI] Wire OnClick of '" + objectName + "' to TabsUI." + methodName + ".", this);
            return btn;
        }

        private RectTransform ActivePanelRect()
        {
            if (panels == null || CurrentIndex < 0 || CurrentIndex >= panels.Length || panels[CurrentIndex] == null) return null;
            return panels[CurrentIndex].GetComponent<RectTransform>();
        }

        private string TabName(int index)
        {
            if (tabNames != null && index >= 0 && index < tabNames.Length && !string.IsNullOrEmpty(tabNames[index]))
                return tabNames[index];
            if (panels != null && index >= 0 && index < panels.Length && panels[index] != null)
                return panels[index].name;
            return "Tab " + index;
        }

        private void RefreshLabel()
        {
            if (toggleLabel == null) return;
            if (panels == null || panels.Length == 0) return;
            int idx = Mathf.Clamp(CurrentIndex < 0 ? defaultIndex : CurrentIndex, 0, panels.Length - 1);
            string name = TabName(idx);
            toggleLabel.text = collapsed ? "↑↑↑ " + name + " ↑↑↑" : "↓↓↓ " + name + " ↓↓↓";
        }

        private void RefreshArrows()
        {
            if (prevButton != null) prevButton.gameObject.SetActive(!collapsed);
            if (nextButton != null) nextButton.gameObject.SetActive(!collapsed);
        }
    }
}
