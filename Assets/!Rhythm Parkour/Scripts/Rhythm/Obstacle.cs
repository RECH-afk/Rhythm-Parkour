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

    [Header("Урон")]
    [Tooltip("Сколько HP снимает при касании игрока")]
    public int damage = 20;
    [Tooltip("Уничтожать препятствие после удара?")]
    public bool destroyOnHit = false;
    [Tooltip("Кулдаун нанесения урона этим препятствием (сек)")]
    public float damageCooldown = 0.6f;
    float lastDamageTime;

    [Header("Анимация появления")]
    [Tooltip("Длительность вырастания (сек). Короче = точнее хит, но менее красиво")]
    public float spawnAnimDuration = 0.18f;
    [Tooltip("Кривая масштаба. По умолчанию с лёгким overshoot")]
    public AnimationCurve spawnCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.2f),
        new Keyframe(0.55f, 1.08f, 0f, 0f),
        new Keyframe(1f, 1f, -0.9f, 0f));
    [Tooltip("Старт снизу (выдвижение из пола)")]
    public bool spawnFromGround = true;
    public float spawnGroundOffset = 0.9f;

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
    private Vector3 spawnPosition; // где заспавнили (для точного расчёта по песне)
    // анимация
    private bool isSpawning;
    private float spawnAnimTimer;
    private Vector3 spawnTargetScale;
    private float spawnStartY;
    // триггер — храним базу чтобы не масштабировать вместе с визуалом (хит точно в бит)
    private BoxCollider triggerCol;
    private Vector3 triggerBaseSize, triggerBaseCenter, triggerBaseScale;

    void Awake()
    {
        EnsurePhysics();
    }

    void EnsurePhysics()
    {
        // для триггера нужен Rigidbody (kinematic) — CharacterController триггерит и без него, но для надёжности добавим
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        // гарантируем триггер-коллайдер для урона (отдельно от солидного MeshCollider)
        bool hasTrigger = false;
        foreach (var c in GetComponents<Collider>()) if (c.isTrigger) { hasTrigger = true; break; }
        if (!hasTrigger)
        {
            var solid = GetComponent<Collider>();
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            if (solid != null && solid != bc)
            {
                Bounds wb = solid.bounds;
                Vector3 localCenter = transform.InverseTransformPoint(wb.center);
                Vector3 ls = transform.lossyScale;
                ls.x = Mathf.Abs(ls.x) < 0.001f ? 1f : ls.x;
                ls.y = Mathf.Abs(ls.y) < 0.001f ? 1f : ls.y;
                ls.z = Mathf.Abs(ls.z) < 0.001f ? 1f : ls.z;
                Vector3 localSize = new Vector3(wb.size.x / ls.x, wb.size.y / ls.y, wb.size.z / ls.z);
                bc.center = localCenter;
                bc.size = localSize * 0.99f;
            }
            else
            {
                bc.center = new Vector3(0, 0.75f, 0);
                bc.size = new Vector3(1f, 1.5f, 1f);
            }
        }
        // запомним триггер для компенсации масштаба (хит точно в бит, даже когда визуал ещё маленький)
        foreach (var c in GetComponents<Collider>()) if (c.isTrigger && c is BoxCollider) { triggerCol = (BoxCollider)c; break; }
        if (triggerCol != null) { triggerBaseSize = triggerCol.size; triggerBaseCenter = triggerCol.center; triggerBaseScale = transform.localScale; if (triggerBaseScale.x==0) triggerBaseScale.x=1; if(triggerBaseScale.y==0) triggerBaseScale.y=1; if(triggerBaseScale.z==0) triggerBaseScale.z=1; }
    }

    // прямой урон игроку (надёжнее чем только PlayerHealth.OnTriggerEnter — работает с обеих сторон)
    void OnTriggerEnter(Collider other) => TryDamage(other);
    void OnTriggerStay(Collider other) => TryDamage(other);
    // для solid-столкновения (если триггер не сработал)
    void OnCollisionEnter(Collision col) { if (col.collider != null) TryDamage(col.collider); }

    void TryDamage(Collider col)
    {
        if (!moving) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        if (isSpawning && spawnAnimTimer < 0.18f) return;
        // ищем PlayerHealth строго на коллайдере игрока — без FindObjectOfType (иначе бьёт сквозь воздух)
        var ph = col.GetComponentInParent<PlayerHealth>();
        if (ph == null) ph = col.GetComponent<PlayerHealth>();
        if (ph == null) return;
        if (ph.IsDead || ph.IsInvincible) return;
        // триггер уже гарантирует контакт — дистанцию не проверяем, только реальное касание коллайдеров
        lastDamageTime = Time.time;
        ph.TakeDamage(damage > 0 ? damage : 20, this);
        if (destroyOnHit) Despawn();
    }

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

        // запоминаем границы дорожки и Y — spawnPosition это точка СПАВНА (а не хита)
        if (mgr != null)
        {
            trackMinX = mgr.trackMinX;
            trackMaxX = mgr.trackMaxX;
            lockedY = transform.position.y;
        }
        else
        {
            lockedY = transform.position.y;
            trackMinX = -3f;
            trackMaxX = 3f;
        }
        spawnPosition = new Vector3(transform.position.x, lockedY, transform.position.z);

        // ── анимация появления ──
        spawnTargetScale = transform.localScale;
        if (spawnCurve == null || spawnCurve.length == 0)
            spawnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (spawnAnimDuration > 0.001f)
        {
            isSpawning = true;
            spawnAnimTimer = 0f;
            transform.localScale = Vector3.zero;
            spawnStartY = lockedY - (spawnFromGround ? spawnGroundOffset : 0f);
            if (spawnFromGround)
            {
                Vector3 p = transform.position;
                p.y = spawnStartY;
                transform.position = p;
            }
        }
        else
        {
            isSpawning = false;
            transform.localScale = spawnTargetScale;
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

        // анимация масштаба — триггер компенсируем чтобы хит был точно в бит даже когда визуал ещё маленький
        if (isSpawning)
        {
            spawnAnimTimer += Time.deltaTime;
            float t = Mathf.Clamp01(spawnAnimTimer / Mathf.Max(0.001f, spawnAnimDuration));
            float k = spawnCurve != null ? spawnCurve.Evaluate(t) : 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, spawnTargetScale, k);
            if (triggerCol != null)
            {
                Vector3 cur = transform.localScale;
                float sx = Mathf.Abs(cur.x) < 0.01f ? 0.01f : cur.x;
                float sy = Mathf.Abs(cur.y) < 0.01f ? 0.01f : cur.y;
                float sz = Mathf.Abs(cur.z) < 0.01f ? 0.01f : cur.z;
                Vector3 worldSize = new Vector3(triggerBaseSize.x * Mathf.Abs(triggerBaseScale.x), triggerBaseSize.y * Mathf.Abs(triggerBaseScale.y), triggerBaseSize.z * Mathf.Abs(triggerBaseScale.z));
                Vector3 worldCenter = new Vector3(triggerBaseCenter.x * triggerBaseScale.x, triggerBaseCenter.y * triggerBaseScale.y, triggerBaseCenter.z * triggerBaseScale.z);
                triggerCol.size = new Vector3(worldSize.x / sx, worldSize.y / sy, worldSize.z / sz);
                triggerCol.center = new Vector3(worldCenter.x / sx, worldCenter.y / sy, worldCenter.z / sz);
            }
            if (t >= 1f) { isSpawning = false; transform.localScale = spawnTargetScale; if(triggerCol){ triggerCol.size=triggerBaseSize; triggerCol.center=triggerBaseCenter; } }
        }

        // ── ДВИЖЕНИЕ СИНХРОННО С МУЗЫКОЙ ── чтобы хит был точно в бит на триггере
        Vector3 basePos;
        float songTime = -1f;
        if (manager != null) songTime = manager.currentTime;
        else if (Conductor.Instance != null && Conductor.Instance.isPlaying) songTime = Conductor.Instance.songPosition;

        if (songTime >= -900f && manager != null && manager.isPlaying)
        {
            float elapsed = songTime - spawnTime;
            if (elapsed < 0) elapsed = 0;
            basePos = spawnPosition + direction * speed * elapsed;
        }
        else
        {
            // превью/редактор без музыки — старое поведение по дельте
            basePos = transform.position + direction * speed * Time.deltaTime;
            // обновляем spawnPosition для следующего кадра превью (не критично)
            spawnPosition = basePos - direction * speed * 0.016f;
        }
        basePos.x = Mathf.Clamp(basePos.x, trackMinX, trackMaxX);
        if (isSpawning && spawnFromGround)
        {
            float t = Mathf.Clamp01(spawnAnimTimer / Mathf.Max(0.001f, spawnAnimDuration));
            float yk = Mathf.SmoothStep(0, 1, t);
            basePos.y = Mathf.LerpUnclamped(spawnStartY, lockedY, yk);
            if (t >= 1f) basePos.y = lockedY;
        }
        else basePos.y = lockedY;

        transform.position = basePos;

        if (despawnPoint != null)
        {
            Vector3 toDespawn = despawnPoint.position - transform.position;
            toDespawn.y = 0f;
            if (Vector3.Dot(toDespawn, direction) < 0f) Despawn();
        }
        else if (Vector3.Distance(transform.position, spawnPosition) > 220f) Despawn();

        // страховка: если песня ушла далеко за хит — удаляем
        if (manager != null && manager.isPlaying && songTime >= -900f)
        {
            float travel = 60f / Mathf.Max(1f, speed);
            // точнее: дистанция до хит-триггера
            if (manager.hitTrigger) travel = manager.GetTravelTime(speed);
            if (songTime - spawnTime > travel + 3.5f) Despawn();
        }
    }

    public void Despawn()
    {
        moving = false;
        if (manager != null) manager.ReturnToPool(this);
        else Destroy(gameObject);
    }
}
