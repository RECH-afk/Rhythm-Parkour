using UnityEngine;

/// <summary>
/// Крутится строго под бит — вешай на сферу.
/// </summary>
public class SphereBeatRotator : MonoBehaviour
{
    public Conductor conductor;
    public Vector3 axis = Vector3.up;
    public float degreesPerBeat = 90f;
    [Range(0.02f, 0.4f)] public float smooth = 0.08f;

    float cur, tgt, vel;

    void Awake()
    {
        if (!conductor) conductor = FindObjectOfType<Conductor>();
        cur = transform.eulerAngles.y;
        tgt = cur;
    }

    void Update()
    {
        if (!conductor || !conductor.isPlaying) return;
        tgt = conductor.songPositionBeats * degreesPerBeat;
        cur = Mathf.SmoothDampAngle(cur, tgt, ref vel, smooth);
        Vector3 e = transform.eulerAngles;
        e.y = cur;
        // если ось не Y — используй Quaternion
        if (axis == Vector3.up) transform.eulerAngles = e;
        else transform.rotation = Quaternion.AngleAxis(cur, axis.normalized);
    }
}
