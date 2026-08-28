using UnityEngine;

/// <summary>
/// Вешай на префаб препятствия. Двигается вперёд и удаляется у точки деспавна.
/// </summary>
public class Obstacle : MonoBehaviour
{
    [HideInInspector] public float spawnTime;
    internal RhythmParkourManager manager;
    internal Transform despawnPoint;

    // Движение задаётся менеджером
    private Vector3 direction;
    private float speed;
    private bool moving;

    public void Init(RhythmParkourManager mgr, Vector3 dir, float spd, Transform despawn, float time)
    {
        manager = mgr;
        direction = dir.normalized;
        speed = spd;
        despawnPoint = despawn;
        spawnTime = time;
        moving = true;
    }

    void Update()
    {
        if (!moving) return;
        transform.position += direction * speed * Time.deltaTime;

        if (despawnPoint != null)
        {
            // Если пролетели за деспавн по направлению движения
            Vector3 toDespawn = despawnPoint.position - transform.position;
            // если скалярное произведение < 0 — прошли точку
            if (Vector3.Dot(toDespawn, direction) < 0f)
                Despawn();
        }
        else
        {
            // fallback — далеко от спавна
            if (Vector3.Distance(transform.position, manager != null && manager.spawnPoint != null ? manager.spawnPoint.position : Vector3.zero) > 200f)
                Despawn();
        }
    }

    public void Despawn()
    {
        moving = false;
        if (manager != null) manager.ReturnToPool(this);
        else Destroy(gameObject);
    }
}
