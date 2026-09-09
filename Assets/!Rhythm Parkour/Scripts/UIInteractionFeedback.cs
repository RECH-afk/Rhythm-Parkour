using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;


[RequireComponent(typeof(RectTransform))]
public class UIInteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Sounds")]
    [SerializeField] private string onPointerEnterSoundName = "Bubble";
    [SerializeField] private string onClickSoundName = "TrickleClicker";

    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float pressScale = 0.9f;
    [SerializeField] private float duration = 0.15f;

    [Header("Ease")]
    [SerializeField] private Ease hoverEase = Ease.OutBack;
    [SerializeField] private Ease pressEase = Ease.OutQuad;

    private Vector3 startScale;
    private Tween scaleTween;

    void Awake()
    {
        startScale = transform.localScale;
    }

    private void OnEnable()
    {
        ResetState();
    }

    private void OnDisable()
    {
        scaleTween?.Kill();
    }

    private void ResetState()
    {
        transform.localScale = startScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ScaleTo(startScale * hoverScale, duration, hoverEase);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ScaleTo(startScale, duration, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ScaleTo(startScale * pressScale, duration * 0.8f, pressEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {

        ScaleTo(startScale * hoverScale, duration, hoverEase);
    }

    private void ScaleTo(Vector3 target, float time, Ease ease)
    {
        scaleTween?.Kill();
        scaleTween = transform
            .DOScale(target, time)
            .SetEase(ease);
    }
}