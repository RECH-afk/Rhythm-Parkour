using UnityEngine;
using TMPro;

namespace RKS.RhythmParkour.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class TextScroller : MonoBehaviour
    {
        [Header("Marquee")]
        public bool allowAutoScroll = true;
        public float scrollSpeed = 40f;
        public float pauseAtStart = 1.2f;
        public float pauseAtEnd = 1.2f;

        private TMP_Text tmp;
        private RectTransform rt;
        private Vector2 basePos;
        private bool baseCaptured;
        private string lastText;
        private float lastWidth = -1f;
        private float lastFontSize = -1f;
        private bool overflowing;
        private float contentWidth;
        private float scrollOffset;
        private float phaseTime;
        private int phase;
        private float checkTimer;

        void Awake()
        {
            rt = GetComponent<RectTransform>();
            tmp = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            baseCaptured = false;
            overflowing = false;
            scrollOffset = 0f;
            phase = 0;
            phaseTime = 0f;
            checkTimer = 0f;
        }

        void OnDisable()
        {
            if (baseCaptured && rt != null) rt.anchoredPosition = basePos;
        }

        void Update()
        {
            if (!allowAutoScroll || rt == null) return;
            if (tmp == null) return;
            string curText = tmp.text;
            float w = rt.rect.width;
            float fs = tmp.fontSize;
            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer <= 0f || curText != lastText || !Mathf.Approximately(w, lastWidth) || !Mathf.Approximately(fs, lastFontSize))
            {
                if (curText != lastText) tmp.ForceMeshUpdate();
                lastText = curText;
                lastWidth = w;
                lastFontSize = fs;
                checkTimer = 0.5f;
                UpdateOverflow(w);
            }
            TickScroll(w);
        }

        void UpdateOverflow(float w)
        {
            bool was = overflowing;
            overflowing = false;
            contentWidth = 0f;
            if (w <= 1f)
            {
                if (was) ResetPose();
                return;
            }
            contentWidth = tmp.preferredWidth;
            overflowing = tmp.textInfo.lineCount <= 1 && contentWidth > w + 2f;
            if (!overflowing)
            {
                if (was) ResetPose();
                return;
            }
            if (!baseCaptured || phase == 0)
            {
                basePos = rt.anchoredPosition;
                baseCaptured = true;
            }
        }

        void ResetPose()
        {
            scrollOffset = 0f;
            phase = 0;
            phaseTime = 0f;
            if (baseCaptured && rt != null) rt.anchoredPosition = basePos;
        }

        void TickScroll(float w)
        {
            if (!overflowing) return;
            if (!baseCaptured)
            {
                basePos = rt.anchoredPosition;
                baseCaptured = true;
            }
            float maxOffset = Mathf.Max(0f, contentWidth - w);
            if (maxOffset <= 0f) return;
            float forwardDuration = Mathf.Max(0.01f, maxOffset / Mathf.Max(1f, scrollSpeed));
            float rewindDuration = Mathf.Clamp(maxOffset / Mathf.Max(1f, scrollSpeed * 3f), 0.25f, 0.8f);
            phaseTime += Time.unscaledDeltaTime;
            if (phase == 0)
            {
                scrollOffset = 0f;
                if (phaseTime >= pauseAtStart) { phase = 1; phaseTime = 0f; }
            }
            else if (phase == 1)
            {
                float t = Mathf.Clamp01(phaseTime / forwardDuration);
                scrollOffset = maxOffset * SmootherStep(t);
                if (t >= 1f) { phase = 2; phaseTime = 0f; }
            }
            else if (phase == 2)
            {
                scrollOffset = maxOffset;
                if (phaseTime >= pauseAtEnd) { phase = 3; phaseTime = 0f; }
            }
            else
            {
                float t = Mathf.Clamp01(phaseTime / rewindDuration);
                scrollOffset = maxOffset * (1f - SmootherStep(t));
                if (t >= 1f) { phase = 0; phaseTime = 0f; scrollOffset = 0f; }
            }
            rt.anchoredPosition = basePos + new Vector2(-scrollOffset, 0f);
        }

        static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
