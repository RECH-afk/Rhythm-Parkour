using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimelineNoteView : RKSBehaviour, ITimelineNoteView
    {
        public Image backgroundImage;
        public Image accentImage;
        public TextMeshProUGUI indexLabel;
        public TextMeshProUGUI beatLabel;
        public GameObject selectedHighlight;
        public CanvasGroup canvasGroup;
        RectTransform rect;
        protected override void OnInjected()
        {
            rect = GetComponent<RectTransform>();
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = GetComponentInChildren<Image>();
            if (selectedHighlight != null) selectedHighlight.SetActive(false);
        }
        public void Setup(int index, ObstacleEvent ev, float hitBeat, bool selected)
        {
            Color col = GetColorForPrefab(ev.prefabIndex);
            if (backgroundImage != null) { backgroundImage.color = col; backgroundImage.raycastTarget = true; }
            if (accentImage != null) accentImage.color = Color.Lerp(col, Color.white, 0.3f);
            if (indexLabel != null) indexLabel.text = $"#{index}";
            if (beatLabel != null) beatLabel.text = $"{hitBeat:0.##}";
            if (selectedHighlight != null) selectedHighlight.SetActive(selected);
            else
            {
                if (canvasGroup != null) canvasGroup.alpha = selected ? 1f : 0.9f;
                var outline = GetComponent<Outline>();
                if (outline != null) outline.enabled = selected;
            }
        }
        void OnTimelineNoteSetup(object[] args)
        {
            if (args.Length >= 4)
            {
                int idx = (int)args[0];
                ObstacleEvent ev = (ObstacleEvent)args[1];
                float hb = (float)args[2];
                bool sel = (bool)args[3];
                Setup(idx, ev, hb, sel);
            }
        }
        Color GetColorForPrefab(int idx) { float h = (idx * 0.37f) % 1f; return Color.HSVToRGB(h, 0.78f, 0.92f); }
    }
}
