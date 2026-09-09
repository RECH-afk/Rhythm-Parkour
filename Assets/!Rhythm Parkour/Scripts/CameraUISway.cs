using UnityEngine;
using UnityEngine.UI;
using EasyPeasyFirstPersonController;

public class CameraUISway : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform uiSwayObject;
    [SerializeField] private Transform targetCamera;
    [SerializeField] private FirstPersonController player;

    [Header("Mouse Sway")]
    [SerializeField] private float swayAmount = 35f;
    [SerializeField] private float smoothTime = 8f;
    [SerializeField] private float returnSpeed = 3f;

    [Header("Walk Bob")]
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmountX = 4f;
    [SerializeField] private float bobAmountY = 2.5f;

    private CharacterController characterController;

    private Vector2 targetPosition;
    private Vector2 currentVelocity;
    private Vector3 lastCameraRotation;
    private float bobTimer;

     void Start()
    {
        characterController = player.GetComponent<CharacterController>();
        lastCameraRotation = targetCamera.eulerAngles;
    }

    void Update()
    {
        if (uiSwayObject == null || targetCamera == null || characterController == null)
            return;

        Vector3 currentRotation = targetCamera.eulerAngles;

        float deltaX = Mathf.DeltaAngle(lastCameraRotation.y, currentRotation.y);
        float deltaY = Mathf.DeltaAngle(lastCameraRotation.x, currentRotation.x);

        targetPosition.x = Mathf.Clamp(-deltaX * swayAmount, -swayAmount, swayAmount);
        targetPosition.y = Mathf.Clamp(deltaY * swayAmount, -swayAmount, swayAmount);

        if (Mathf.Abs(deltaX) < 0.01f && Mathf.Abs(deltaY) < 0.01f)
        {
            targetPosition = Vector2.MoveTowards(
                targetPosition,
                Vector2.zero,
                returnSpeed * Time.deltaTime);
        }

        Vector3 velocity = characterController.velocity;
        velocity.y = 0f;

        Vector2 bobOffset = Vector2.zero;

        if (velocity.sqrMagnitude > 0.01f)
        {
            // покачивание в бит, если играет музыка, иначе свободное
            var conductor = Conductor.Instance;
            if (conductor != null && conductor.isPlaying && conductor.songPositionBeats >= 0f)
                bobTimer = conductor.songPositionBeats * Mathf.PI * 2f * 0.5f;
            else
                bobTimer += Time.deltaTime * bobSpeed;

            bobOffset.x = Mathf.Sin(bobTimer) * bobAmountX;
            bobOffset.y = Mathf.Abs(Mathf.Cos(bobTimer)) * bobAmountY;
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 5f);
        }

        Vector2 finalTarget = targetPosition + bobOffset;

        uiSwayObject.anchoredPosition = Vector2.SmoothDamp(
            uiSwayObject.anchoredPosition,
            finalTarget,
            ref currentVelocity,
            1f / smoothTime);

        lastCameraRotation = currentRotation;
    }
}