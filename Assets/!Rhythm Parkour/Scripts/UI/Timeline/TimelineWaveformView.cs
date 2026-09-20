using UnityEngine;
using UnityEngine.UI;
using RKS.RhythmParkour.Core;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimelineWaveformView : RKSBehaviour
    {
        private TimelineUI ui;
        private Texture2D waveformTex;
        private float[] waveformData;
        private AudioClip lastClip;
        private int lastWaveformGenWidth = -1;
        private float lastWaveformGenPPS = -1f;
        private float lastWaveformGenZoom = -1f;

        protected override void Awake()
        {
            ui = GetComponent<TimelineUI>();
        }

        protected override void OnDisposed()
        {
            if (waveformTex != null) Destroy(waveformTex);
            waveformTex = null;
        }

        public AudioClip LastClip => lastClip;
        public float LastPps => lastWaveformGenPPS;
        public Texture2D GetWaveformTexture() => waveformTex;
        public float[] GetWaveformData() => waveformData;

        public void Invalidate()
        {
            lastClip = null;
            lastWaveformGenWidth = -1;
        }

        public void RefreshWaveform(bool force = false)
        {
            if (ui == null) ui = GetComponent<TimelineUI>();
            if (ui == null || ui.waveformImage == null) return;
            if (!ui.showWaveform)
            {
                ui.waveformImage.enabled = false;
                if (ui.waveformImage.texture != null) ui.waveformImage.texture = null;
                return;
            }
            ui.waveformImage.enabled = true;
            AudioClip clip = ui.levelData != null ? ui.levelData.music : null;
            if (clip == null && ui.audioSource != null) clip = ui.audioSource.clip;
            if (clip == null) { ui.waveformImage.texture = null; ui.waveformImage.color = new Color(1, 1, 1, 0.08f); return; }

            int desiredWidth = Mathf.Clamp(Mathf.RoundToInt(ui.GetClipLength() * ui.pixelsPerSecond * Mathf.Clamp(ui.zoom, 0.8f, 2.2f)), 4096, 16384);
            desiredWidth = Mathf.Clamp(Mathf.Max(desiredWidth, ui.waveformTexWidth), 4096, 16384);
            bool needRegen = force || clip != lastClip || waveformTex == null || desiredWidth != lastWaveformGenWidth || Mathf.Abs(ui.pixelsPerSecond - lastWaveformGenPPS) > 1f || Mathf.Abs(ui.zoom - lastWaveformGenZoom) > 0.22f;
            if (!needRegen) return;
            lastClip = clip;
            lastWaveformGenWidth = desiredWidth;
            lastWaveformGenPPS = ui.pixelsPerSecond;
            lastWaveformGenZoom = ui.zoom;
            if (waveformTex != null) Destroy(waveformTex);
            waveformData = WaveformGenerator.GenerateData(clip, desiredWidth);
            int h = Mathf.Clamp(ui.waveformTexHeight, 32, 360);
            waveformTex = WaveformGenerator.GenerateTexture(waveformData, desiredWidth, h, ui.waveformWaveColor, ui.waveformBgColor);
            ui.waveformImage.texture = waveformTex;
            ui.waveformImage.color = Color.white;

            if (waveformTex != null) waveformTex.filterMode = FilterMode.Bilinear;
            if (ui.waveformRect != null) ui.waveformRect.localScale = Vector3.one;
        }
    }
}
