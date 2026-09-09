using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Здоровье игрока для Rhythm Parkour. Вешается на FirstPersonController (там где CharacterController).
/// Препятствия (Obstacle) снимают HP при касании. Смерь = провал уровня (TRASH).
/// Встроенного UI НЕТ: HP-бар вешай сам в инспекторе через событие onHealthChanged.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public int maxHealth = 100;
    [Min(1)] public int currentHealth = 100;
    [Tooltip("Неуязвимость после получения урона (сек)")]
    public float invincibilityDuration = 1.0f;
    [Tooltip("Урон по умолчанию, если у Obstacle не задан damage")]
    public int defaultDamage = 20;

    [Header("События (для своего UI в инспекторе)")]
    public UnityEvent<int,int> onHealthChanged; // cur, max
    public UnityEvent onDamage;
    public UnityEvent onDeath;

    [Header("Флеш при попадании (не HUD, просто вспышка)")]
    public bool flashOnDamage = true;
    public Color damageFlashColor = new Color(1,0.2f,0.2f,0.35f);
    public float flashDuration = 0.18f;

    [Header("Показ хитбокса в игре")]
    [Tooltip("Рисовать рамку коллайдера тела прямо в игре — видны реальные размеры для уворота")]
    public bool showHitbox = true;
    public Color hitboxColor = new Color(0.3f, 0.6f, 1f, 0.9f);
    [Tooltip("Раздуть рамку поверх хитбокса (1.05 = +5%), чтобы линии не тонули в меше модели")]
    [Range(1f, 1.3f)] public float hitboxGrow = 1.05f;

    Transform hitboxVisual;
    static Mesh wireCubeMesh;
    static Mesh wireCapsuleMesh;
    Material hitboxMat;
    CapsuleCollider capsule; // кэш капсулы игрока (хит-тест идёт по ней, а не по квадрату)

    float lastDamageTime = -999f;
    float flashTimer;
    bool isDead;
    CharacterController controller;
    Vector3 lastSafePos;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null) controller = GetComponentInChildren<CharacterController>();
        capsule = GetComponentInChildren<CapsuleCollider>();
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (currentHealth <= 0) currentHealth = maxHealth;
        lastSafePos = transform.position;
    }

    void Update()
    {
        if (!isDead && controller != null && controller.isGrounded)
            lastSafePos = transform.position;

        if (flashTimer > 0) flashTimer -= Time.deltaTime;

        UpdateHitboxVisual();

        // тест: H — урон (для отладки в эдиторе)
#if UNITY_EDITOR
        if (Application.isEditor && !isDead)
        {
            if (Input.GetKeyDown(KeyCode.H)) TakeDamage(10, null);
        }
#endif
        // смерть — рестарт по R
        if (isDead && Input.GetKeyDown(KeyCode.R))
        {
            Respawn();
        }
    }

    public bool IsInvincible => Time.time - lastDamageTime < invincibilityDuration;
    public bool IsDead => isDead;

    /// <summary>Возвращает true если урон реально прошёл (для подсчёта Miss).</summary>
    public bool TakeDamage(int amount, Obstacle from)
    {
        if (isDead) return false;
        if (amount <= 0) amount = defaultDamage;
        if (IsInvincible) return false;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        lastDamageTime = Time.time;
        flashTimer = flashDuration;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onDamage?.Invoke();

        if (currentHealth <= 0) { Die(); return true; }
        return true;
    }

    // перегрузка для вызова без ссылки
    public bool TakeDamage(int amount) => TakeDamage(amount, null);

    void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onDeath?.Invoke();
        Debug.Log("[Health] Игрок погиб! Нажми R для рестарта", this);

        // стопаем музыку/спавн и показываем результаты с рангом TRASH
        var mgr = RhythmParkourManager.Instance;
        if (mgr != null) mgr.FailLevel();
        else
        {
            var score = RhythmScoreManager.Instance;
            if (score != null && !score.isFinished) { score.Fail(); score.Finish(true); }
        }

        // лочим управление
        var fpc = GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
        if (fpc != null) fpc.SetControl(false);
        // покажем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Respawn()
    {
        // ресет здоровья
        currentHealth = maxHealth;
        isDead = false;
        lastDamageTime = -999f;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        // телепорт на последнюю безопасную позицию или старт (0,1,0)
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = lastSafePos + Vector3.up * 0.2f;
            // если lastSafePos вблизи препятствий — сбросим на старт
            if (Physics.CheckSphere(transform.position, 0.5f, LayerMask.GetMask("Default")))
                transform.position = new Vector3(0, 1f, 0);
            controller.enabled = true;
        }
        else transform.position = new Vector3(0, 1f, 0);

        var fpc = GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
        if (fpc != null) fpc.SetControl(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var mgr = RhythmParkourManager.Instance;
        if (mgr != null) mgr.Play();
        Debug.Log("[Health] Респавн", this);
    }

    // ── детекция касаний ──

    // CharacterController → триггер препятствия
    void OnTriggerEnter(Collider other)
    {
        TryHitBy(other);
    }
    void OnTriggerStay(Collider other)
    {
        // если застрял внутри — не спамим, сработает invincibility
        TryHitBy(other);
    }
    // На случай если препятствие — не триггер, а solid (OnControllerColliderHit)
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;
        TryHitBy(hit.collider);
    }
    // На случай если у препятствия Rigidbody+Collision
    void OnCollisionEnter(Collision col)
    {
        if (col.collider != null) TryHitBy(col.collider);
    }

    void TryHitBy(Collider col)
    {
        if (col == null || isDead) return;
        // ищем Obstacle на объекте или родителе
        var obs = col.GetComponentInParent<Obstacle>();
        if (obs == null) return;
        // единая точка удара с учётом хитбокса игрока — там кулдаун, гресия спавна и подсчёт Miss
        obs.HitPlayerChecked(this);
    }

    // ── рамка хитбокса в игре: видно реальные размеры (капсула, не квадрат) ──
    void UpdateHitboxVisual()
    {
        if (!showHitbox || controller == null)
        {
            if (hitboxVisual != null) hitboxVisual.gameObject.SetActive(false);
            return;
        }
        bool useCapsule = capsule != null && capsule.direction == 1;
        EnsureHitboxVisual(useCapsule);
        if (hitboxVisual == null) return;
        hitboxVisual.gameObject.SetActive(true);
        if (useCapsule && Obstacle.GetCapsuleWorld(capsule, out var ca, out var cb, out float cr))
        {
            // рамка-капсула: позиция — середина отрезка, масштаб под радиус и высоту (+раздутие от z-fighting)
            hitboxVisual.position = (ca + cb) * 0.5f;
            hitboxVisual.rotation = Quaternion.identity;
            float h = Vector3.Distance(ca, cb) + cr * 2f;
            hitboxVisual.localScale = new Vector3(Mathf.Max(0.01f, cr * 2f * hitboxGrow), Mathf.Max(0.01f, h * 0.5f * hitboxGrow), Mathf.Max(0.01f, cr * 2f * hitboxGrow));
        }
        else
        {
            // fallback: квадрат по bounds (капсулы нет)
            Bounds b = controller.bounds;
            hitboxVisual.position = b.center;
            hitboxVisual.rotation = Quaternion.identity;
            hitboxVisual.localScale = new Vector3(Mathf.Max(0.01f, b.size.x * hitboxGrow), Mathf.Max(0.01f, b.size.y * hitboxGrow), Mathf.Max(0.01f, b.size.z * hitboxGrow));
        }
    }

    void EnsureHitboxVisual(bool useCapsule)
    {
        if (hitboxVisual != null) return;
        Mesh mesh;
        if (useCapsule)
        {
            if (wireCapsuleMesh == null) wireCapsuleMesh = BuildWireCapsuleMesh();
            mesh = wireCapsuleMesh;
        }
        else
        {
            if (wireCubeMesh == null) wireCubeMesh = BuildWireCubeMesh();
            mesh = wireCubeMesh;
        }
        var mat = hitboxMat;
        if (mat == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh == null) return; // без шейдера рамку не рисуем
            mat = new Material(sh);
            mat.color = hitboxColor;
            hitboxMat = mat;
        }
        else mat.color = hitboxColor;
        var go = new GameObject("HitboxVisual (Player)");
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        hitboxVisual = go.transform;
    }

    static Mesh BuildWireCubeMesh()
    {
        // куб [-0.5, 0.5], 12 рёбер списком отрезков
        Vector3[] v = new Vector3[8];
        for (int i = 0; i < 8; i++)
            v[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
        int[] e = { 0,1, 0,2, 0,4, 1,3, 1,5, 2,3, 2,6, 3,7, 4,5, 4,6, 5,7, 6,7 };
        var m = new Mesh { name = "WireCube" };
        m.vertices = v;
        m.SetIndices(e, MeshTopology.Lines, 0);
        m.RecalculateBounds();
        return m;
    }

    static Mesh BuildWireCapsuleMesh()
    {
        // юнит-капсула: радиус 0.5, цилиндр y∈[-0.5,0.5], итого высота 2, центр в нуле.
        // кольца на широтах + 4 вертикали, всё отрезками.
        var verts = new System.Collections.Generic.List<Vector3>();
        var idx = new System.Collections.Generic.List<int>();
        void Ring(float y, float r)
        {
            const int N = 12;
            int start = verts.Count;
            for (int k = 0; k < N; k++)
            {
                float a = k * Mathf.PI * 2f / N;
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
            for (int k = 0; k < N; k++) { idx.Add(start + k); idx.Add(start + (k + 1) % N); }
        }
        foreach (float deg in new float[] { 0f, 30f, 60f })
        {
            float rad = deg * Mathf.Deg2Rad;
            float y = 0.5f + 0.5f * Mathf.Sin(rad);
            float r = 0.5f * Mathf.Cos(rad);
            if (r < 0.01f) continue;
            Ring(y, r);
            Ring(-y, r);
        }
        // 4 вертикали строго по цилиндру (|y|<=0.5), иначе торчали бы из полусфер
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            Vector3 top = new Vector3(Mathf.Cos(a) * 0.5f, 0.5f, Mathf.Sin(a) * 0.5f);
            Vector3 bot = new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f);
            idx.Add(verts.Count); verts.Add(bot);
            idx.Add(verts.Count); verts.Add(top);
        }
        var m = new Mesh { name = "WireCapsule" };
        m.vertices = verts.ToArray();
        m.SetIndices(idx.ToArray(), MeshTopology.Lines, 0);
        m.RecalculateBounds();
        return m;
    }

    void OnDisable()
    {
        if (hitboxVisual != null) hitboxVisual.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (hitboxVisual != null) Destroy(hitboxVisual.gameObject);
    }

    // ── только вспышка попадания (HP-бар делай сам через onHealthChanged) ──
    void OnGUI()
    {
        if (flashOnDamage && flashTimer > 0)
        {
            float a = flashTimer / flashDuration;
            GUI.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, damageFlashColor.a * a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // радиус проверки для дебага
        Gizmos.color = IsInvincible ? new Color(1,1,1,0.2f) : new Color(1,0.2f,0.2f,0.35f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
    }

    // капсула хитбокса — видна всегда в Scene (не только при выделении)
    void OnDrawGizmos()
    {
        if (!showHitbox) return;
        if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
        Gizmos.color = hitboxColor;
        if (capsule != null && Obstacle.GetCapsuleWorld(capsule, out var a, out var b, out float r))
        {
            Gizmos.DrawWireSphere(a, r);
            Gizmos.DrawWireSphere(b, r);
            Gizmos.DrawLine(a, b);
        }
        else if (controller != null)
        {
            Gizmos.DrawWireCube(controller.bounds.center, controller.bounds.size);
        }
    }
#endif
}
