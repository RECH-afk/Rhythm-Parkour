using UnityEngine;

/// <summary>
/// Вешай на префаб препятствия. Двигается строго по дорожке и удаляется у точки деспавна.
/// Скорость теперь индивидуальна — задаётся в префабе и переопределяется событием.
/// </summary>
public class Obstacle : MonoBehaviour
{
    [Header("Скорость")]
    [Tooltip("Базовая скорость этого типа препятствия. Переопределяется событием если speed>0")]
    public float baseSpeed = 12f;
    [Tooltip("Разброс скорости для вариативности (не используется если Init задал точную)")]
    public Vector2 speedRandomRange = new Vector2(0.9f, 1.1f);

    [HideInInspector] public float spawnTime;
    internal RhythmParkourManager manager;
    internal Transform despawnPoint;

    private Vector3 direction;
    private float speed;
    private bool moving;
    // кэш дорожки для клампа по X
    private float trackMinX;
    private float trackMaxX;
    private float lockedY;

    public void Init(RhythmParkourManager mgr, Vector3 dir, float evtSpeed, Transform despawn, float time)
    {
        manager = mgr;
        // скорость: приоритет у события, иначе baseSpeed префаба, иначе дефолт менеджера
        float chosen = evtSpeed > 0.01f ? evtSpeed : baseSpeed;
        if (chosen < 0.1f) chosen = 12f;
        speed = chosen;
        direction = dir.normalized;
        // делаем движение строго горизонтальным (убираем Y из направления)
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        direction.Normalize();
        despawnPoint = despawn;
        spawnTime = time;
        moving = true;

        // запоминаем границы дорожки и Y
        if (mgr != null)
        {
            trackMinX = mgr.trackMinX;
            trackMaxX = mgr.trackMaxX;
            lockedY = transform.position.y; // Y уже выставлен спавном на высоте дорожки
        }
        else
        {
            lockedY = transform.position.y;
            trackMinX = -3f;
            trackMaxX = 3f;
        }
    }

    // Для обратной совместимости
    public void Init(RhythmParkourManager mgr, Vector3 dir, float spd, Transform despawn, float time, bool _legacy)
    {
        Init(mgr, dir, spd, despawn, time);
    }

    void Update()
    {
        if (!moving) return;
        Vector3 pos = transform.position;
        pos += direction * speed * Time.deltaTime;
        // жёсткая привязка к дорожке: X кламп, Y лок
        pos.x = Mathf.Clamp(pos.x, trackMinX, trackMaxX);
        pos.y = lockedY;
        transform.position = pos;

        if (despawnPoint != null)
        {
            Vector3 toDespawn = despawnPoint.position - transform.position;
            // проецируем на направление (игнор Y)
            toDespawn.y = 0f;
            if (Vector3.Dot(toDespawn, direction) < 0f)
                Despawn();
        }
        else
        {
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
