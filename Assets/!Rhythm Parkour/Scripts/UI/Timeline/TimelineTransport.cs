using UnityEngine;
using UnityEngine.EventSystems;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Rhythm;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TimelineTransport : RKSBehaviour
    {
        private TimelineUI ui;
        private float currentTime;
        private bool isScrubbingWaveform;
        private bool wasPlayingBeforeScrub;
        private float scrubSavedVolume = 1f;
        private bool scrubWasPlaying;
        private Vector2 lastScrubScreenPos;
        private float lastScrubTargetTime;
        private const float scrubStillThresholdPxSq = 4f;

        protected override void Awake()
        {
            ui = GetComponent<TimelineUI>();
        }

        public bool IsScrubbing => isScrubbingWaveform;
        public float CurrentTime => currentTime;

        public void SetTime(float t)
        {
            currentTime = t;
            UpdatePlayhead();
            UpdateLabels();
        }

        public void Seek(float time) { Seek(time, false); }

        public void Seek(float time, bool isScrubMove)
        {
            if (ui.GetClipLength() < 0.01f) return;
            time = Mathf.Clamp(time, 0f, ui.GetClipLength() - 0.01f);
            var audioSource = ui.audioSource;
            if (isScrubbingWaveform && ui.enableScrubAudio && !isScrubMove && Mathf.Abs(time - currentTime) < 0.005f)
            {
                currentTime = time;
                UpdatePlayhead();
                if (ui.autoScrollWithPlayhead) ui.UpdateScrollToPlayhead(false);
                UpdateLabels();
                return;
            }
            currentTime = time;
            if (audioSource != null && audioSource.clip != null)
            {
                if (isScrubbingWaveform && ui.enableScrubAudio)
                {
                    if (Mathf.Abs(audioSource.time - time) > 0.012f) try { audioSource.time = time; } catch { audioSource.time = time; }
                    if (!audioSource.isPlaying) audioSource.Play();
                }
                else
                {
                    bool wasPlaying = audioSource.isPlaying;
                    try { audioSource.time = time; } catch { }
                    if (wasPlaying && !audioSource.isPlaying) audioSource.Play();
                }
            }
            UpdatePlayhead();
            if (ui.autoScrollWithPlayhead) ui.UpdateScrollToPlayhead(false);
            UpdateLabels();
        }

        public void SeekNormalized(float norm) => Seek(norm * ui.GetClipLength());
        public float GetCurrentTime() => currentTime;
        public float GetCurrentBeat() { var levelData = ui.levelData; return levelData != null ? levelData.TimeToBeat(currentTime) : 0f; }
        public float GetNormalizedTime() => ui.GetClipLength() > 0.01f ? currentTime / ui.GetClipLength() : 0f;

        public void Play() => PlayFromTime(currentTime);

        public void Pause()
        {
            if (ui.audioSource != null) ui.audioSource.Pause();
        }

        public void PlayFromTime(float t)
        {
            var levelData = ui.levelData;
            var audioSource = ui.audioSource;
            if (ui.GetClipLength() < 0.01f) { ui.FlashStatus("Нет аудио"); return; }
            AudioClip clip = levelData != null && levelData.music != null ? levelData.music : (audioSource != null ? audioSource.clip : null);
            if (clip == null) { ui.FlashStatus("Нет клипа"); return; }
            if (audioSource == null) audioSource = ui.gameObject.AddComponent<AudioSource>();
            if (audioSource.clip != clip) audioSource.clip = clip;
            audioSource.time = Mathf.Clamp(t, 0f, clip.length - 0.02f);
            audioSource.volume = 1f;
            audioSource.loop = ui.loopPlayback;
            audioSource.Play();
            currentTime = audioSource.time;
            UpdatePlayPauseLabel();
        }

        public void TogglePlayPause()
        {
            var audioSource = ui.audioSource;
            if (audioSource != null && audioSource.isPlaying) Pause(); else Play();
        }

        public void BeginScrub()
        {
            var audioSource = ui.audioSource;
            if (isScrubbingWaveform) return;
            isScrubbingWaveform = true;
            wasPlayingBeforeScrub = audioSource != null && audioSource.isPlaying;
            scrubWasPlaying = wasPlayingBeforeScrub;
            lastScrubScreenPos = Input.mousePosition;
            lastScrubTargetTime = currentTime;
            if (audioSource == null || audioSource.clip == null) return;
            if (ui.enableScrubAudio)
            {
                scrubSavedVolume = audioSource.volume;
                audioSource.volume = Mathf.Clamp01(ui.scrubVolume);
                float target = Mathf.Clamp(currentTime, 0f, audioSource.clip.length - 0.02f);
                try { audioSource.time = target; } catch { audioSource.time = 0f; }
                audioSource.loop = false;
                if (!audioSource.isPlaying) audioSource.Play();
                CancelInvoke(nameof(StopScrubPreview));
            }
            else { if (wasPlayingBeforeScrub) audioSource.Pause(); }
        }

        public void EndScrub()
        {
            var audioSource = ui.audioSource;
            if (!isScrubbingWaveform) return;
            isScrubbingWaveform = false;
            if (audioSource == null || audioSource.clip == null) { wasPlayingBeforeScrub = false; return; }
            if (ui.enableScrubAudio)
            {
                audioSource.volume = scrubSavedVolume;
                if (!wasPlayingBeforeScrub) { CancelInvoke(nameof(StopScrubPreview)); Invoke(nameof(StopScrubPreview), Mathf.Max(0.05f, ui.scrubAudibleDuration)); }
                else { if (!audioSource.isPlaying) { try { audioSource.time = Mathf.Clamp(currentTime, 0f, audioSource.clip.length - 0.02f); } catch { } audioSource.Play(); } }
            }
            else { if (wasPlayingBeforeScrub && !audioSource.isPlaying) { try { audioSource.time = Mathf.Clamp(currentTime, 0f, audioSource.clip.length - 0.02f); } catch { } audioSource.Play(); } }
            if (audioSource != null && audioSource.clip != null) audioSource.loop = ui.loopPlayback;
            wasPlayingBeforeScrub = false;
        }

        void StopScrubPreview()
        {
            var audioSource = ui.audioSource;
            if (isScrubbingWaveform) return;
            if (!scrubWasPlaying && audioSource != null && audioSource.isPlaying) { audioSource.Pause(); audioSource.volume = scrubSavedVolume; }
        }

        public void BeginScrubAt(float t, Vector2 screenPos)
        {
            Seek(t, true);
            BeginScrub();
            lastScrubScreenPos = screenPos;
            lastScrubTargetTime = t;
        }

        public void ScrubMoveTo(float t, Vector2 screenPos)
        {
            float sq = (screenPos - lastScrubScreenPos).sqrMagnitude;
            bool screenMoved = sq > scrubStillThresholdPxSq;
            bool timeJumped = Mathf.Abs(t - lastScrubTargetTime) > 0.006f;
            if (!screenMoved && !timeJumped) return;
            lastScrubScreenPos = screenPos;
            lastScrubTargetTime = t;
            ui.UpdateAutoscrollLockFromScreenPos(screenPos);
            Seek(t, true);
        }

        public void TickLoop()
        {
            var audioSource = ui.audioSource;
            if (ui.loopPlayback && audioSource != null && audioSource.clip != null && audioSource.isPlaying)
            {
                if (audioSource.time >= audioSource.clip.length - 0.05f) { audioSource.time = 0f; audioSource.Play(); }
            }
        }

        public void UpdateCurrentTimeFromAudio()
        {
            var audioSource = ui.audioSource;
            if (audioSource == null || audioSource.clip == null) { if (audioSource != null && !audioSource.isPlaying) return; }
            if (audioSource.isPlaying) currentTime = audioSource.time;
            else if (!isScrubbingWaveform) currentTime = audioSource.time;
            currentTime = Mathf.Clamp(currentTime, 0f, ui.GetClipLength() - 0.001f);
        }

        public void UpdatePlayhead()
        {
            var playheadRect = ui.playheadRect;
            RectTransform refRect = ui.waveformRect != null ? ui.waveformRect : ui.timelineContent;
            if (playheadRect == null || refRect == null || ui.GetClipLength() < 0.01f) return;
            float norm = Mathf.Clamp01(currentTime / ui.GetClipLength());
            float width = refRect.rect.width;
            if (width < 1f && refRect == ui.timelineContent) width = refRect.sizeDelta.x;
            float x = norm * width;
            if (refRect == ui.timelineContent) playheadRect.anchoredPosition = new Vector2(x, playheadRect.anchoredPosition.y);
            else { float xMin = refRect.rect.xMin; playheadRect.anchoredPosition = new Vector2(xMin + x, playheadRect.anchoredPosition.y); }
        }

        public void UpdateLabels()
        {
            if (ui.timeLabel != null) { float len = ui.GetClipLength(); ui.timeLabel.text = $"{ui.FormatTime(currentTime)} / {ui.FormatTime(len)}"; }
            if (ui.beatLabel != null && ui.beatLabel.gameObject.activeSelf) ui.beatLabel.gameObject.SetActive(false);
        }

        public void UpdatePlayPauseLabel()
        {
            var audioSource = ui.audioSource;
            if (ui.playPauseLabel != null) { bool playing = audioSource != null && audioSource.isPlaying; ui.playPauseLabel.text = playing ? "■" : "►"; }
        }

        public float GetTimeFromMouseHeader(Vector2 screenPos)
        {
            return ui.GetTimeFromMouse(screenPos);
        }

        public void OnHeaderPointerDown(BaseEventData data)
        {
            var ped = data as PointerEventData;
            if (ped == null) return;
            ui.UpdateAutoscrollLockFromScreenPos(ped.position);
            float t = GetTimeFromMouseHeader(ped.position);
            BeginScrubAt(t, ped.position);
        }

        public void OnHeaderDrag(BaseEventData data)
        {
            var ped = data as PointerEventData;
            if (ped == null) return;
            ui.UpdateAutoscrollLockFromScreenPos(ped.position);
            float t = GetTimeFromMouseHeader(ped.position);
            Seek(t, true);
            lastScrubScreenPos = ped.position;
            lastScrubTargetTime = t;
        }

        public void OnWaveformDrag(BaseEventData data)
        {
            var ped = data as PointerEventData;
            if (ped == null) return;
            BeginScrubAt(ui.GetTimeFromMouse(ped.position), ped.position);
        }

        public void OnWaveformPointerUp(BaseEventData data)
        {
            if (isScrubbingWaveform) EndScrub();
        }
    }
}
