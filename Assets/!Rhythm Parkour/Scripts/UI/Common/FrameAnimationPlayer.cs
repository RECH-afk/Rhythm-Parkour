using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;

namespace RKS.RhythmParkour.UI
{
    [DisallowMultipleComponent]
    public class FrameAnimationPlayer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum PlayMode { Loop, Once, PingPong }
        public enum SizeMode { Keep, MatchFrame }

        [Header("Target")]
        public Image targetImage;

        [Header("Frames")]
        public Sprite[] frames;
        [Min(1)] public int framesPerSecond = 12;
        public PlayMode playMode = PlayMode.Loop;
        public SizeMode sizeMode = SizeMode.Keep;

        [Header("Control")]
        public bool playOnEnable;
        public bool restartOnPlay = true;
        public bool resetOnStop;

        [Header("Pointer Triggers")]
        public bool playOnHover;
        public bool stopOnHoverExit = true;
        public bool playOnPress;
        public bool stopOnPressRelease = true;

        [Header("Events")]
        public UnityEvent onFinished;

        IEnumerator runner;
        WaitForSeconds frameWait;
        int cachedFps = -1;
        int frameIndex;
        bool forward = true;

        public bool IsPlaying => runner != null;

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            Stop();
        }

        void OnDestroy()
        {
            Stop();
        }

        void OnValidate()
        {
            if (framesPerSecond < 1) framesPerSecond = 1;
        }

        public void Play()
        {
            if (IsPlaying && !restartOnPlay) return;
            if (frames == null || frames.Length == 0) return;
            Image image = ResolveImage();
            if (image == null) return;
            if (cachedFps != framesPerSecond)
            {
                frameWait = new WaitForSeconds(1f / Mathf.Max(1, framesPerSecond));
                cachedFps = framesPerSecond;
            }
            if (runner != null) StopCoroutine(runner);
            frameIndex = 0;
            forward = true;
            runner = PlayRoutine(image);
            StartCoroutine(runner);
        }

        public void Stop()
        {
            if (runner != null)
            {
                StopCoroutine(runner);
                runner = null;
            }
            if (resetOnStop) ApplyFrame(ResolveImage(), 0);
        }

        public void Restart()
        {
            Stop();
            Play();
        }

        IEnumerator PlayRoutine(Image image)
        {
            if (!ApplyFrame(image, frameIndex)) yield break;
            while (true)
            {
                yield return frameWait;
                AdvanceFrame();
                if (!ApplyFrame(image, frameIndex)) yield break;
                if (playMode == PlayMode.Once && frameIndex >= frames.Length - 1)
                {
                    runner = null;
                    if (onFinished != null) onFinished.Invoke();
                    yield break;
                }
            }
        }

        void AdvanceFrame()
        {
            switch (playMode)
            {
                case PlayMode.Loop:
                    frameIndex = (frameIndex + 1) % frames.Length;
                    break;
                case PlayMode.Once:
                    if (frameIndex < frames.Length - 1) frameIndex++;
                    break;
                case PlayMode.PingPong:
                    if (forward)
                    {
                        if (frameIndex < frames.Length - 1) frameIndex++;
                        else { frameIndex = Mathf.Max(0, frames.Length - 2); forward = false; }
                    }
                    else
                    {
                        if (frameIndex > 0) frameIndex--;
                        else { frameIndex = frames.Length > 1 ? 1 : 0; forward = true; }
                    }
                    break;
            }
        }

        bool ApplyFrame(Image image, int index)
        {
            if (image == null || frames == null || index < 0 || index >= frames.Length) return false;
            image.sprite = frames[index];
            if (sizeMode == SizeMode.MatchFrame) image.SetNativeSize();
            return true;
        }

        Image ResolveImage()
        {
            if (targetImage != null) return targetImage;
            return GetComponent<Image>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (playOnHover) Play();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (playOnHover && stopOnHoverExit) Stop();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (playOnPress) Play();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (playOnPress && stopOnPressRelease) Stop();
        }
    }
}
