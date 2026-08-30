using UnityEngine;

/// <summary>
/// Свободная камера — WASD + мышь + Q/E + Shift. Вешай на Camera.
/// ПКМ — зажать для вращения, СКМ — панорама.
/// </summary>
public class FreeCamera : MonoBehaviour
{
    [Header("Скорость")]
    public float moveSpeed = 8f;
    public float fastMultiplier = 3f;
    public float mouseSensitivity = 2f;
    public float scrollSpeed = 6f;

    [Header("Плавность")]
    public float acceleration = 12f;
    public float damping = 6f;

    Vector3 velocity;
    float yaw, pitch;
    bool rotating;

    void OnEnable()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
    }

    void Update()
    {
        // ПКМ удерживать для вращения, иначе просто WASD
        if (Input.GetMouseButtonDown(1)) { rotating = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Input.GetMouseButtonUp(1)) { rotating = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        if (rotating)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -88f, 88f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // движение
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        if (Input.GetKey(KeyCode.Q)) input.y -= 1f;
        if (Input.GetKey(KeyCode.E)) input.y += 1f;
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 wish = transform.TransformDirection(input) * speed;

        velocity = Vector3.Lerp(velocity, wish, Time.deltaTime * acceleration);
        if (input.sqrMagnitude < 0.01f) velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * damping);

        transform.position += velocity * Time.deltaTime;

        // колесо — вперёд/назад
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            transform.position += transform.forward * scroll * scrollSpeed;

        // средняя кнопка — панорама
        if (Input.GetMouseButton(2))
        {
            float mx = -Input.GetAxis("Mouse X") * 0.6f;
            float my = -Input.GetAxis("Mouse Y") * 0.6f;
            transform.position += transform.right * mx + transform.up * my;
        }
    }
}
