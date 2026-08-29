using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Здоровье игрока для Rhythm Parkour. Вешается на FirstPersonController (там где CharacterController).
/// Препятствия (Obstacle) наносят урон при касании. Есть неуязвимость, смерть, рестарт.
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
    [Tooltip("Отбрасывание при уроне (опционально)")]
    public float knockbackForce = 2f;

    [Header("События")]
    public UnityEvent<int,int> onHealthChanged; // cur, max
    public UnityEvent onDamage;
    public UnityEvent onDeath;
    public UnityEvent onHeal;

    [Header("Визуал")]
    public bool flashOnDamage = true;
    public Color damageFlashColor = new Color(1,0.2f,0.2f,0.35f);
    public float flashDuration = 0.18f;
    [Tooltip("Показывать бар через OnGUI если нет UI")]
    public bool showDebugBar = true;

    float lastDamageTime = -999f;
    float flashTimer;
    bool isDead;
    CharacterController controller;
    Vector3 lastSafePos;

    // для OnGUI бара
    GUIStyle barBg, barFill, barText;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null) controller = GetComponentInChildren<CharacterController>();
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (currentHealth <= 0) currentHealth = maxHealth;
        lastSafePos = transform.position;
        // гарантируем триггер для детекции если нет — добавим сферу-триггер маленькую для надёжности (не нужен CharacterController сам триггерит)
    }

    void Update()
    {
        if (!isDead && controller != null && controller.isGrounded)
            lastSafePos = transform.position;

        if (flashTimer > 0) flashTimer -= Time.deltaTime;

        // тест: H — урон, J — хил (для отладки в эдиторе)
#if UNITY_EDITOR
        if (Application.isEditor && !isDead)
        {
            if (Input.GetKeyDown(KeyCode.H)) TakeDamage(10, null);
            if (Input.GetKeyDown(KeyCode.J)) Heal(10);
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

    public void TakeDamage(int amount, Obstacle from)
    {
        if (isDead) return;
        if (amount <= 0) amount = defaultDamage;
        if (IsInvincible) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        lastDamageTime = Time.time;
        flashTimer = flashDuration;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onDamage?.Invoke();

        // лёгкое отбрасывание назад (против направления препятствия)
        if (knockbackForce > 0.01f && controller != null)
        {
            Vector3 dir = from != null ? (transform.position - from.transform.position) : -transform.forward;
            dir.y = 0; dir.Normalize();
            // CharacterController.Move с knockback
            controller.Move(dir * knockbackForce * 0.2f);
        }

        if (currentHealth <= 0) Die();
        else
        {
            // вибрация/звук можно добавить
            Debug.Log($"[Health] Урон {amount} от {from?.name} | HP {currentHealth}/{maxHealth}", this);
        }
    }

    // перегрузка для вызова из Obstacle без ссылки
    public void TakeDamage(int amount) => TakeDamage(amount, null);

    public void Heal(int amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onHeal?.Invoke();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0;
        onHealthChanged?.Invoke(currentHealth, maxHealth);
        onDeath?.Invoke();
        Debug.Log("[Health] Игрок погиб! Нажми R для рестарта", this);

        // стопаем музыку/спавн
        var mgr = RhythmParkourManager.Instance;
        if (mgr != null) mgr.Stop();

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

    // ── детекция урона ──

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
        // во время анимации появления не бьём (чтобы не спавнить внутри игрока)
        // но если уже вырос — бьём
        // проверяем что препятствие активно движется
        int dmg = obs.damage > 0 ? obs.damage : defaultDamage;
        TakeDamage(dmg, obs);
        if (obs.destroyOnHit)
            obs.Despawn();
    }

    // ── UI (простой OnGUI, замени на Canvas если нужно) ──
    void OnGUI()
    {
        if (!showDebugBar) return;
        // бар здоровья вверху слева
        float pad = 12f;
        float w = 260f, h = 22f;
        Rect bg = new Rect(pad, pad, w, h);
        float pct = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        if (barBg == null)
        {
            barBg = new GUIStyle(GUI.skin.box);
            barFill = new GUIStyle(GUI.skin.box);
            barText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
        GUI.color = new Color(0, 0, 0, 0.55f);
        GUI.Box(bg, "", barBg);
        GUI.color = IsInvincible ? new Color(1, 1, 1, 0.9f) : new Color(0.18f, 0.75f, 0.25f, 1f);
        if (isDead) GUI.color = new Color(0.8f, 0.15f, 0.15f, 1f);
        GUI.Box(new Rect(bg.x + 2, bg.y + 2, (bg.width - 4) * pct, bg.height - 4), "", barFill);
        GUI.color = Color.white;
        GUI.Label(bg, $"{currentHealth} / {maxHealth} HP{(IsInvincible && !isDead ? "  ★" : "")}", barText);

        if (flashOnDamage && flashTimer > 0)
        {
            float a = flashTimer / flashDuration;
            GUI.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, damageFlashColor.a * a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (isDead)
        {
            Rect r = new Rect(Screen.width * 0.5f - 180, Screen.height * 0.5f - 40, 360, 80);
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 24), "ПОТРАЧЕНО", new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter, fontSize=22, fontStyle=FontStyle.Bold, normal=new GUIStyleState{textColor=Color.red}});
            GUI.Label(new Rect(r.x, r.y + 36, r.width, 20), "Нажми  R  — рестарт  •  Esc — меню", new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // радиус проверки для дебага
        Gizmos.color = IsInvincible ? new Color(1,1,1,0.2f) : new Color(1,0.2f,0.2f,0.35f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
    }
#endif
}
