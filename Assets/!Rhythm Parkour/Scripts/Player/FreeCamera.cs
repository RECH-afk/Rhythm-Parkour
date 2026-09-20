using RKS.RhythmParkour.Core;
using UnityEngine;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour
{
    public class FreeCamera : RKSBehaviour
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

        protected override void OnReady()
        {
            Vector3 e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }

        protected override void Update()
        {

            if (Input.GetMouseButtonDown(1)) { rotating = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Input.GetMouseButtonUp(1)) { rotating = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            if (rotating)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, -88f, 88f);
                transform.eulerAngles = new Vector3(pitch, yaw, 0f);
            }

Vector3 input = Vector3.zero;
            if (Input.GetKey(KeyCode.A)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D)) input.x += 1f;
            if (Input.GetKey(KeyCode.W)) input.z += 1f;
            if (Input.GetKey(KeyCode.S)) input.z -= 1f;
            if (Input.GetKey(KeyCode.Q)) input.y -= 1f;
            if (Input.GetKey(KeyCode.E)) input.y += 1f;
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
            Vector3 wish = transform.TransformDirection(input) * speed;

            velocity = Vector3.Lerp(velocity, wish, Time.deltaTime * acceleration);
            if (input.sqrMagnitude < 0.01f) velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * damping);

            transform.position += velocity * Time.deltaTime;

if (Input.GetMouseButton(2))
            {
                float mx = -Input.GetAxis("Mouse X") * 0.6f;
                float my = -Input.GetAxis("Mouse Y") * 0.6f;
                transform.position += transform.right * mx + transform.up * my;
            }
        }
    }
}
