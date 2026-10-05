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
        [Header("Speed")]
        public float moveSpeed = 8f;
        public float fastMultiplier = 3f;
        public float mouseSensitivity = 2f;
        public float scrollSpeed = 6f;

        [Header("Smoothing")]
        public float acceleration = 12f;
        public float damping = 6f;

        [Header("Bounds")]
        [Tooltip("Specific collider the camera cannot fly past. Empty = layer check")]
        public Collider boundaryCollider;
        [Tooltip("Keep the camera inside the boundary collider instead of outside")]
        public bool confineInside = false;
        public float collideRadius = 0.3f;
        public LayerMask collideLayers = ~0;

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

            Vector3 delta = velocity * Time.deltaTime;

            if (Input.GetMouseButton(2))
            {
                float mx = -Input.GetAxis("Mouse X") * 0.6f;
                float my = -Input.GetAxis("Mouse Y") * 0.6f;
                delta += transform.right * mx + transform.up * my;
            }

            transform.position = MoveWithCollision(transform.position, delta);
        }

        Vector3 MoveWithCollision(Vector3 from, Vector3 delta)
        {
            float dist = delta.magnitude;
            if (dist < 0.00001f) return from;
            Vector3 dir = delta / dist;
            if (boundaryCollider != null && boundaryCollider.enabled && boundaryCollider.gameObject.activeInHierarchy)
            {
                if (confineInside) return MoveConfinedInside(from, delta);
                RaycastHit h;
                if (boundaryCollider.Raycast(new Ray(from, dir), out h, dist + collideRadius))
                {
                    float allowed = Mathf.Max(0f, h.distance - collideRadius);
                    Vector3 slide = Vector3.ProjectOnPlane(dir * (dist - allowed), h.normal);
                    return from + dir * allowed + slide;
                }
                return from + delta;
            }
            RaycastHit hit;
            if (Physics.SphereCast(from, collideRadius, dir, out hit, dist, collideLayers, QueryTriggerInteraction.Ignore))
            {
                float allowed = Mathf.Max(0f, hit.distance - 0.01f);
                Vector3 slide = Vector3.ProjectOnPlane(dir * (dist - allowed), hit.normal);
                return from + dir * allowed + slide;
            }
            return from + delta;
        }

        bool IsInsideBoundary(Vector3 p)
        {
            if (boundaryCollider == null) return true;
            Vector3 cp = boundaryCollider.ClosestPoint(p);
            return (cp - p).sqrMagnitude < 0.000001f;
        }

        Vector3 MoveConfinedInside(Vector3 from, Vector3 delta)
        {
            Vector3 target = from + delta;
            if (IsInsideBoundary(target)) return target;
            if (!IsInsideBoundary(from)) return from;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 12; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (IsInsideBoundary(from + delta * mid)) lo = mid;
                else hi = mid;
            }
            float dist = delta.magnitude;
            float allowed = lo;
            if (dist > 0.00001f) allowed = Mathf.Max(0f, lo - collideRadius / dist);
            return from + delta * allowed;
        }
    }
}
