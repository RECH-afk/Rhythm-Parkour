using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace RKS.RhythmParkour.UI
{
    public class LoadingSparkleIndicator : MonoBehaviour
    {
        [Header("Rect Transform")]
        public RectTransform SparkleIconOne;
        public RectTransform SparkleIconTwo;

        [Header("Timing")]
        public float stepDuration = 1.2f;
        public Ease moveEase = Ease.OutBack;

        [Header("Colors")]
        public Color bigColor = Color.white;
        public Color smallColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        public Ease colorEase = Ease.InOutSine;

        [Header("Rotation")]
        public float rotationDegrees = 90f;

        [Header("Control")]
        public bool playOnEnable = true;

        Vector2 stateASize;
        Vector2 stateAPos;
        Vector2 stateBSize;
        Vector2 stateBPos;
        bool hasStates;
        Sequence loopSeq;
        Image imageOne;
        Image imageTwo;

        void OnEnable()
        {
            CaptureStates();
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

        void CaptureStates()
        {
            if (SparkleIconOne == null || SparkleIconTwo == null) return;
            stateASize = SparkleIconOne.sizeDelta;
            stateAPos = SparkleIconOne.anchoredPosition;
            stateBSize = SparkleIconTwo.sizeDelta;
            stateBPos = SparkleIconTwo.anchoredPosition;
            imageOne = SparkleIconOne.GetComponent<Image>();
            imageTwo = SparkleIconTwo.GetComponent<Image>();
            hasStates = true;
        }

        public void Play()
        {
            if (!hasStates) CaptureStates();
            if (!hasStates) return;
            if (SparkleIconOne == SparkleIconTwo || stepDuration <= 0f) return;
            Stop();
            DOTween.Kill(SparkleIconOne);
            DOTween.Kill(SparkleIconTwo);
            loopSeq = DOTween.Sequence();
            loopSeq.AppendCallback(() => SparkleIconTwo.transform.SetAsLastSibling());
            loopSeq.Append(SparkleIconOne.DOSizeDelta(stateBSize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconOne.DOAnchorPos(stateBPos, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DOSizeDelta(stateASize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DOAnchorPos(stateAPos, stepDuration).SetEase(moveEase));
            if (imageOne != null) loopSeq.Join(imageOne.DOColor(smallColor, stepDuration).SetEase(colorEase));
            if (imageTwo != null) loopSeq.Join(imageTwo.DOColor(bigColor, stepDuration).SetEase(colorEase));
            loopSeq.AppendCallback(() => SparkleIconOne.transform.SetAsLastSibling());
            loopSeq.Append(SparkleIconTwo.DOSizeDelta(stateBSize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DOAnchorPos(stateBPos, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DORotate(new Vector3(0f, 0f, rotationDegrees), stepDuration, RotateMode.Fast).SetRelative().SetEase(moveEase));
            loopSeq.Join(SparkleIconOne.DOSizeDelta(stateASize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconOne.DOAnchorPos(stateAPos, stepDuration).SetEase(moveEase));
            if (imageOne != null) loopSeq.Join(imageOne.DOColor(bigColor, stepDuration).SetEase(colorEase));
            if (imageTwo != null) loopSeq.Join(imageTwo.DOColor(smallColor, stepDuration).SetEase(colorEase));
            loopSeq.AppendCallback(() => SparkleIconTwo.transform.SetAsLastSibling());
            loopSeq.Append(SparkleIconOne.DOSizeDelta(stateBSize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconOne.DOAnchorPos(stateBPos, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DOSizeDelta(stateASize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DOAnchorPos(stateAPos, stepDuration).SetEase(moveEase));
            if (imageOne != null) loopSeq.Join(imageOne.DOColor(bigColor, stepDuration).SetEase(colorEase));
            if (imageTwo != null) loopSeq.Join(imageTwo.DOColor(smallColor, stepDuration).SetEase(colorEase));
            loopSeq.AppendCallback(() => SparkleIconOne.transform.SetAsLastSibling());
            loopSeq.Append(SparkleIconTwo.DOSizeDelta(stateBSize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DOAnchorPos(stateBPos, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconTwo.DORotate(new Vector3(0f, 0f, rotationDegrees), stepDuration, RotateMode.Fast).SetRelative().SetEase(moveEase));
            loopSeq.Join(SparkleIconOne.DOSizeDelta(stateASize, stepDuration).SetEase(moveEase));
            loopSeq.Join(SparkleIconOne.DOAnchorPos(stateAPos, stepDuration).SetEase(moveEase));
            if (imageOne != null) loopSeq.Join(imageOne.DOColor(bigColor, stepDuration).SetEase(colorEase));
            if (imageTwo != null) loopSeq.Join(imageTwo.DOColor(smallColor, stepDuration).SetEase(colorEase));
            loopSeq.SetLoops(-1);
            loopSeq.Play();
        }

        public void Stop()
        {
            if (loopSeq != null && loopSeq.IsActive()) loopSeq.Kill();
            loopSeq = null;
        }
    }
}
