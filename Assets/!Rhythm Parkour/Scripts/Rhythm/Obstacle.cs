using System.Collections.Generic;
using RKS.RhythmParkour.Core;
using UnityEngine;
using TMPro;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;







namespace RKS.RhythmParkour.Rhythm
{
    public class Obstacle : RKSBehaviour
    {
        [Header("Speed")]
        [Tooltip("Базовая скорость этого типа препятствия. Переопределяется событием если speed>0")]
        public float baseSpeed = 12f;
        [Tooltip("Разброс скорости для вариативности (не используется если Init задал точную)")]
        public Vector2 speedRandomRange = new Vector2(0.9f, 1.1f);

        [Header("Damage")]
        [Tooltip("Сколько HP снимает при касании игрока")]
        public int damage = 20;
        [Tooltip("Уничтожать препятствие после удара?")]
        public bool destroyOnHit = false;
        [Tooltip("Кулдаун засчитывания удара этим препятствием (сек)")]
        public float damageCooldown = 0.6f;
        float lastDamageTime;

        [Header("Spawn Animation")]
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

        [Header("Hitbox")]
        [Tooltip("Строить триггер по реальному визуалу (рендеры) при каждом спавне. Выкл = как настроено в префабе")]
        public bool useVisualHitbox = true;
        [Tooltip("Расширение хитбокса от визуала со всех сторон (м). Можно в минус — тогда хитбокс МЕНЬШЕ картинки (прощение)")]
        public float hitboxPadding = 0.05f;

        [Header("Key Note (tap)")]
        [Tooltip("Если вкл — урона касанием нет, надо нажать клавишу в момент пролёта мимо игрока")]
        public bool isKeyNote = false;
        [Tooltip("Какую клавишу жать (буква пишется на ноте)")]
        public KeyCode keyCode = KeyCode.F;
        [Tooltip("Окно идеального нажатия (сек, ± от пролёта)")]
        public float keyPerfectWindow = 0.09f;
        [Tooltip("Окно хорошего нажатия (сек, ± от пролёта)")]
        public float keyGoodWindow = 0.18f;
        [Tooltip("Показывать букву клавиши на ноте")]
        public bool showKeyLabel = true;
        [Tooltip("Размер буквы (масштаб текста)")]
        public float keyLabelSize = 1f;
        public Color keyLabelColor = Color.white;
        [Tooltip("Смещение текста от центра ноты (локальные единицы)")]
        public Vector3 keyLabelOffset = new Vector3(0f, 0.9f, 0f);
        [Tooltip("Поворачивать текст к камере")]
        public bool keyLabelFacePlayer = true;

        [Header("Precise Hit Test")]
        [Tooltip("Считать ударом капсулу игрока (точно по форме), а не(box) её bounds-квадрат")]
        public bool useCapsuleHitTest = true;
        [Tooltip("Камера внутри триггера = удар, даже если капсула не задета (голова в стене)")]
        public bool cameraCountsAsHit = true;
        [Tooltip("Точек-семплов вдоль капсулы для замера зазора (больше = точнее, дороже)")]
        [Range(2, 9)] public int capsuleGapSamples = 5;

        [Header("Dodge Check")]
        [Tooltip("Мин. время подлёта ноты для реакции (сек). Меньше — варнинг")]
        public float minReactionTime = 0.7f;

        [HideInInspector] public float spawnTime;
        internal RhythmParkourManager manager;
        internal Transform despawnPoint;

        [InjectOptional] internal RhythmScoreManager score;
        [InjectOptional] internal Conductor songClock;
        [InjectOptional] internal PlayerHealth boundPlayer;

        [Header("Score (osu)")]
        [Tooltip("Уникальный id ноты в текущем прохождении. Ставит менеджер при спавне")]
        [HideInInspector] public int noteId = -1;
        [Tooltip("По этой ноте уже засчитан Miss (повторный урон не плодит миссы)")]
        [HideInInspector] public bool countedAsMiss;
        [Tooltip("Препятствие реально задело игрока (даже если HP-урон заблокирован неуязвимостью) — Perfect уже невозможен")]
        [HideInInspector] public bool touchedPlayer;
        [Tooltip("Мин. зазор между объёмами игрока и препятствия за жизнь ноты — из него считается опасность")]
        [HideInInspector] public float minGap = float.MaxValue;
        private Transform playerTf;
        private Collider playerCol;
        private CapsuleCollider playerCapsule;
        private Camera playerCam;
        private PlayerHealth playerHealth;
        private EasyPeasyFirstPersonController.FirstPersonController playerFpc;
        private bool triggerAutoBuilt;

        private bool keyJudged;
        private HitJudgement keyResult;
        private bool keyCrossed;
        private float keyCrossTime;
        private readonly List<float> keyPressTimes = new List<float>();
        private Transform keyLabelT;
        private TextMeshPro keyLabelTmp;
        private TextMesh keyLabelLegacy;
        private Camera labelCam;

        private Vector3 direction;
        private float speed;
        private bool moving;

        private float trackMinX;
        private float trackMaxX;
        private float lockedY;
        private Vector3 spawnPosition;

        private bool isSpawning;
        private float spawnAnimTimer;
        private Vector3 spawnTargetScale;
        private float spawnStartY;

        private BoxCollider triggerCol;
        private Vector3 triggerBaseSize, triggerBaseCenter, triggerBaseScale;

        protected override void Awake()
        {
            base.Awake();
            EnsurePhysics();
        }

        protected override void OnInjected()
        {
            EnsurePhysics();
        }

        void EnsurePhysics()
        {

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




            triggerCol = null;
            triggerAutoBuilt = false;
            foreach (var c in GetComponents<Collider>()) if (c.isTrigger && c is BoxCollider) { triggerCol = (BoxCollider)c; break; }
            if (triggerCol == null)
            {
                BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = new Vector3(0, 0.75f, 0);
                bc.size = new Vector3(1f, 1.5f, 1f);
                triggerCol = bc;
                triggerAutoBuilt = true;
            }
            if (triggerCol != null) CaptureTriggerBase();
        }

        void CaptureTriggerBase()
        {
            if (triggerCol == null) return;
            triggerBaseSize = triggerCol.size;
            triggerBaseCenter = triggerCol.center;
            triggerBaseScale = transform.localScale;
            if (triggerBaseScale.x == 0) triggerBaseScale.x = 1;
            if (triggerBaseScale.y == 0) triggerBaseScale.y = 1;
            if (triggerBaseScale.z == 0) triggerBaseScale.z = 1;
        }






        void RefreshHitboxFromVisual()
        {
            if (triggerCol == null) return;
            if (!useVisualHitbox || !triggerAutoBuilt) { CaptureTriggerBase(); return; }

            Bounds vb = new Bounds(transform.position, Vector3.zero);
            bool has = false;
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (!has) { vb = b; has = true; }
                else vb.Encapsulate(b);
            }
            if (!has)
            {

                foreach (var c in GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger || c == triggerCol) continue;
                    Bounds b = c.bounds;
                    if (b.size.sqrMagnitude < 1e-8f) continue;
                    if (!has) { vb = b; has = true; }
                    else vb.Encapsulate(b);
                }
            }
            if (has)
            {
                vb.Expand(hitboxPadding * 2f);

                vb.size = new Vector3(Mathf.Max(0.05f, vb.size.x), Mathf.Max(0.05f, vb.size.y), Mathf.Max(0.05f, vb.size.z));
                Vector3 ls = transform.lossyScale;
                ls.x = Mathf.Abs(ls.x) < 0.001f ? 1f : ls.x;
                ls.y = Mathf.Abs(ls.y) < 0.001f ? 1f : ls.y;
                ls.z = Mathf.Abs(ls.z) < 0.001f ? 1f : ls.z;
                triggerCol.center = transform.InverseTransformPoint(vb.center);
                triggerCol.size = new Vector3(vb.size.x / ls.x, vb.size.y / ls.y, vb.size.z / ls.z);
            }

            CaptureTriggerBase();
        }


        void OnTriggerEnter(Collider other) => TryDamage(other);
        void OnTriggerStay(Collider other) => TryDamage(other);

        void OnCollisionEnter(Collision col) { if (col.collider != null) TryDamage(col.collider); }

        void TryDamage(Collider col)
        {
            if (!moving || isKeyNote) return;

            var ph = col.GetComponentInParent<PlayerHealth>();
            if (ph == null) ph = col.GetComponent<PlayerHealth>();
            if (ph == null) return;
            HitPlayerChecked(ph);
        }


        bool HitboxDodged()
        {
            if (triggerCol == null) return false;

            if (useCapsuleHitTest && playerCapsule != null)
            {
                if (CapsuleHitsTrigger()) return false;
            }
            else
            {
                if (!HasPlayerBounds) return false;
                if (triggerCol.bounds.Intersects(GetPlayerBounds())) return false;
            }
            if (CameraInsideTrigger()) return false;
            return true;
        }


        public void HitPlayerChecked(PlayerHealth ph)
        {
            if (!moving || ph == null) return;
            if (HitboxDodged()) return;
            HitPlayer(ph);
        }






        public void HitPlayer(PlayerHealth ph)
        {
            if (!moving || ph == null || isKeyNote) return;
            touchedPlayer = true;
            if (Time.time - lastDamageTime < damageCooldown) return;
            if (isSpawning && spawnAnimTimer < 0.18f) return;

            if (ph.IsDead || ph.IsInvincible) return;
            lastDamageTime = Time.time;
            bool applied = ph.TakeDamage(damage > 0 ? damage : 20, this);
            if (applied && !countedAsMiss)
            {
                countedAsMiss = true;
                var sm = score ?? manager?.score;
                if (sm != null) sm.RegisterMiss();
            }
            if (destroyOnHit) Despawn();
        }

        public void Init(RhythmParkourManager mgr, Vector3 dir, float evtSpeed, Transform despawn, float time)
        {
            manager = mgr;

            float chosen = evtSpeed > 0.01f ? evtSpeed : baseSpeed;
            if (chosen < 0.1f) chosen = 12f;
            speed = chosen;
            direction = dir.normalized;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();
            despawnPoint = despawn;
            spawnTime = time;
            moving = true;

            countedAsMiss = false;
            touchedPlayer = false;
            lastDamageTime = -999f;
            minGap = float.MaxValue;
            keyJudged = false;
            keyCrossed = false;
            keyCrossTime = 0f;
            keyPressTimes.Clear();
            labelCam = null;
            EnsureKeyLabel();
            SetKeyLabel(isKeyNote && showKeyLabel ? KeyName(keyCode) : "");
            if (keyLabelT != null) keyLabelT.gameObject.SetActive(isKeyNote && showKeyLabel);
            if (playerTf == null || playerHealth == null)
            {

                var ph = boundPlayer ?? manager?.playerController?.GetComponent<PlayerHealth>();
                if (ph == null && manager?.playerController != null)
                    ph = manager.playerController.GetComponentInChildren<PlayerHealth>();
                if (ph != null)
                {
                    playerTf = ph.transform;
                    playerHealth = ph;
                    var cc = ph.GetComponentInChildren<CharacterController>();
                    playerCol = cc != null ? cc : ph.GetComponentInChildren<Collider>();
                    playerCapsule = ph.GetComponentInChildren<CapsuleCollider>();
                    if (playerCam == null)
                    {
                        playerCam = ph.GetComponentInChildren<Camera>();
                        if (playerCam == null) playerCam = Camera.main;
                    }
                }
            }
            if (playerTf != null)
            {
                playerFpc = playerTf.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>();
                if (playerFpc == null) playerFpc = playerTf.GetComponentInChildren<EasyPeasyFirstPersonController.FirstPersonController>();
            }

            RefreshHitboxFromVisual();
            if (manager != null && manager.debugHits)
            {
                if (playerHealth == null) Debug.LogWarning($"[Hits] {name}: игрок (PlayerHealth) не найден!", this);
                if (triggerCol == null) Debug.LogWarning($"[Hits] {name}: нет триггера!", this);
                else if (!HasPlayerBounds) Debug.LogWarning($"[Hits] {name}: у игрока нет коллайдера!", this);
                else Debug.Log($"[Hits] Spawn {name}: trigger={triggerCol.bounds.size.x:0.00}x{triggerCol.bounds.size.y:0.00}x{triggerCol.bounds.size.z:0.00}м", this);
            }


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


            if (manager != null && manager.debugHits) CheckDodgeable();


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


        public void Init(RhythmParkourManager mgr, Vector3 dir, float spd, Transform despawn, float time, bool _legacy)
        {
            Init(mgr, dir, spd, despawn, time);
        }

        protected override void Update()
        {
            if (!moving) return;


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


            Vector3 basePos;
            float songTime = -1f;
            if (manager != null) songTime = manager.currentTime;
            else if (songClock != null && songClock.isPlaying) songTime = songClock.songPosition;

            if (songTime >= -900f && manager != null && manager.isPlaying)
            {
                float elapsed = songTime - spawnTime;
                if (elapsed < 0) elapsed = 0;
                basePos = spawnPosition + direction * speed * elapsed;
            }
            else
            {

                basePos = transform.position + direction * speed * Time.deltaTime;

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


            if (isKeyNote && !keyJudged)
                UpdateKeyNote();




            if (!isKeyNote && triggerCol != null)
            {
                float gap;
                if (useCapsuleHitTest && playerCapsule != null) gap = CapsuleTriggerGap();
                else if (HasPlayerBounds) gap = BoundsGap(triggerCol.bounds, GetPlayerBounds());
                else gap = float.MaxValue;
                if (gap < minGap) minGap = gap;
            }





            if (!isKeyNote && triggerCol != null && playerHealth != null)
            {
                Vector3 toPlayer = playerTf.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 25f)
                {
                    bool hit;
                    if (useCapsuleHitTest && playerCapsule != null) hit = CapsuleHitsTrigger();
                    else hit = HasPlayerBounds && triggerCol.bounds.Intersects(GetPlayerBounds());
                    if (!hit) hit = CameraInsideTrigger();
                    if (hit)
                    {
                        if (minGap > 0f) minGap = 0f;
                        HitPlayer(playerHealth);
                    }
                }
            }

            if (despawnPoint != null)
            {
                Vector3 toDespawn = despawnPoint.position - transform.position;
                toDespawn.y = 0f;
                if (Vector3.Dot(toDespawn, direction) < 0f) Despawn();
            }
            else if (Vector3.Distance(transform.position, spawnPosition) > 220f) Despawn();


            if (manager != null && manager.isPlaying && songTime >= -900f)
            {
                float travel = 60f / Mathf.Max(1f, speed);

                if (manager.hitTrigger) travel = manager.GetTravelTime(speed);
                if (songTime - spawnTime > travel + 3.5f) Despawn();
            }
        }






        public bool HasPlayerBounds => playerCol != null;

        public Bounds GetPlayerBounds()
        {
            return playerCol.bounds;
        }


        public static float BoundsGap(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x);
            float dy = Mathf.Max(a.min.y - b.max.y, b.min.y - a.min.y);
            float dz = Mathf.Max(a.min.z - b.max.z, b.min.z - a.min.z);
            dx = Mathf.Max(0f, dx);
            dy = Mathf.Max(0f, dy);
            dz = Mathf.Max(0f, dz);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }


        public static bool GetCapsuleWorld(CapsuleCollider cap, out Vector3 a, out Vector3 b, out float r)
        {
            a = b = Vector3.zero; r = 0.5f;
            if (cap == null) return false;
            Transform t = cap.transform;
            Vector3 ls = t.lossyScale;
            float axisScale, rScale;
            if (cap.direction == 0) { axisScale = Mathf.Abs(ls.x); rScale = Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)); }
            else if (cap.direction == 2) { axisScale = Mathf.Abs(ls.z); rScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y)); }
            else { axisScale = Mathf.Abs(ls.y); rScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)); }
            axisScale = Mathf.Max(axisScale, 0.001f); rScale = Mathf.Max(rScale, 0.001f);
            Vector3 axisL = cap.direction == 0 ? Vector3.right : (cap.direction == 2 ? Vector3.forward : Vector3.up);
            Vector3 c = t.TransformPoint(cap.center);
            Vector3 axisW = t.TransformDirection(axisL).normalized;
            float half = Mathf.Max(0f, cap.height * 0.5f * axisScale - cap.radius * rScale);
            r = cap.radius * rScale;
            a = c - axisW * half;
            b = c + axisW * half;
            return true;
        }


        bool CapsuleHitsTrigger()
        {
            if (triggerCol == null || playerCapsule == null) return false;
            return Physics.ComputePenetration(triggerCol, triggerCol.transform.position, triggerCol.transform.rotation,
                playerCapsule, playerCapsule.transform.position, playerCapsule.transform.rotation,
                out _, out _);
        }


        float CapsuleTriggerGap()
        {
            if (triggerCol == null || playerCapsule == null) return float.MaxValue;
            if (!GetCapsuleWorld(playerCapsule, out var a, out var b, out float r)) return float.MaxValue;
            int n = Mathf.Clamp(capsuleGapSamples, 2, 9);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / (n - 1));
                Vector3 q = triggerCol.ClosestPoint(p);
                float d = Vector3.Distance(p, q) - r;
                if (d < best) best = d;
            }
            return best;
        }


        bool CameraInsideTrigger()
        {
            if (!cameraCountsAsHit || triggerCol == null || playerCam == null) return false;
            Vector3 lp = triggerCol.transform.InverseTransformPoint(playerCam.transform.position);
            Vector3 e = triggerCol.size * 0.5f;
            Vector3 c = triggerCol.center;
            const float eps = 0.02f;
            return Mathf.Abs(lp.x - c.x) <= e.x + eps
                && Mathf.Abs(lp.y - c.y) <= e.y + eps
                && Mathf.Abs(lp.z - c.z) <= e.z + eps;
        }


        float KeyNow()
        {
            if (manager != null && manager.isPlaying) return manager.currentTime;
            return Time.time;
        }


        void UpdateKeyNote()
        {
            if (Input.GetKeyDown(keyCode)) keyPressTimes.Add(KeyNow());
            if (!keyCrossed)
            {
                Transform anchor = playerTf != null ? playerTf : (manager != null ? manager.hitTrigger : null);
                Vector3 ap = anchor != null ? anchor.position : transform.position;
                if (Vector3.Dot(ap - transform.position, direction) <= 0f)
                {
                    keyCrossed = true;
                    keyCrossTime = KeyNow();
                }
            }

            if (keyLabelFacePlayer && keyLabelT != null)
            {
                if (labelCam == null) labelCam = playerCam != null ? playerCam : Camera.main;
                if (labelCam != null)
                {
                    Vector3 toCam = keyLabelT.position - labelCam.transform.position;
                    if (toCam.sqrMagnitude > 1e-6f) keyLabelT.rotation = Quaternion.LookRotation(toCam);
                }
            }
            if (keyCrossed && KeyNow() >= keyCrossTime + keyGoodWindow)
            {
                JudgeKeyNote();
                Despawn();
            }
        }


        void JudgeKeyNote()
        {
            if (keyJudged) return;
            keyJudged = true;
            float best = float.MaxValue;
            foreach (float p in keyPressTimes)
            {
                float d = Mathf.Abs(p - keyCrossTime);
                if (d < best) best = d;
            }
            if (best <= keyPerfectWindow) keyResult = HitJudgement.Perfect300;
            else if (best <= keyGoodWindow) keyResult = HitJudgement.Great100;
            else if (best <= keyGoodWindow * 2f) keyResult = HitJudgement.Good50;
            else keyResult = HitJudgement.Miss;
            var sm = score ?? manager?.score;
            if (sm != null) sm.RegisterJudgement(keyResult);
        }


        void EnsureKeyLabel()
        {
            if (!isKeyNote || !showKeyLabel) return;
            if (keyLabelT != null) return;
            var go = new GameObject("KeyLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = keyLabelOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            TextMeshPro tmp = null;
            TextMesh legacy = null;
            try
            {
                tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 10;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = keyLabelColor;
                if (tmp.font == null)
                {
                    var found = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                    if (found != null && found.Length > 0) tmp.font = found[0];
                }
                if (tmp.font == null) { Destroy(tmp); tmp = null; }
            }
            catch { if (tmp != null) Destroy(tmp); tmp = null; }
            if (tmp == null)
            {
                legacy = go.AddComponent<TextMesh>();
                legacy.alignment = TextAlignment.Center;
                legacy.anchor = TextAnchor.MiddleCenter;
                legacy.fontSize = 64;
                legacy.characterSize = 0.02f * Mathf.Max(0.1f, keyLabelSize);
                legacy.color = keyLabelColor;
            }
            else
            {
                go.transform.localScale = Vector3.one * Mathf.Max(0.1f, keyLabelSize);
            }
            keyLabelT = go.transform;
            keyLabelTmp = tmp;
            keyLabelLegacy = legacy;
        }

        void SetKeyLabel(string s)
        {
            if (keyLabelTmp != null) keyLabelTmp.text = s;
            if (keyLabelLegacy != null) keyLabelLegacy.text = s;
        }

        static string KeyName(KeyCode k)
        {
            string s = k.ToString();
            if (s.StartsWith("Alpha") && s.Length == 6) return s.Substring(5);
            return s;
        }

        void OnValidate()
        {

            SetKeyLabel(isKeyNote && showKeyLabel ? KeyName(keyCode) : "");
        }

        public void Despawn()
        {
            if (moving)
            {

                var sm = score ?? manager?.score;
                if (sm != null && sm.isLevelActive && !sm.isFinished && manager != null && manager.isPlaying)
                {
                    if (!countedAsMiss)
                    {
                        if (isKeyNote)
                        {

                            if (!keyJudged) JudgeKeyNote();
                        }

                        else if (touchedPlayer) { countedAsMiss = true; sm.RegisterMiss(); }
                        else sm.RegisterPerfect();
                    }
                }
                if (manager != null && manager.debugHits)
                {
                    Debug.Log($"[Hits] Despawn {name}: gap={minGap:0.00} touched={touchedPlayer} miss={countedAsMiss}", this);
                }
            }
            moving = false;
            if (manager != null) manager.ReturnToPool(this);
            else Destroy(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {

            if (triggerCol != null)
            {
                Gizmos.color = touchedPlayer ? Color.red : new Color(0.2f, 1f, 0.4f, 0.8f);
                Gizmos.DrawWireCube(triggerCol.bounds.center, triggerCol.bounds.size);
            }

            if (useCapsuleHitTest && playerCapsule != null && GetCapsuleWorld(playerCapsule, out var ca, out var cb, out float cr))
            {
                Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
                Gizmos.DrawWireSphere(ca, cr);
                Gizmos.DrawWireSphere(cb, cr);
                Gizmos.DrawLine(ca, cb);
            }
            else if (HasPlayerBounds)
            {
                Bounds pb = GetPlayerBounds();
                Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
                Gizmos.DrawWireCube(pb.center, pb.size);
            }

            if (cameraCountsAsHit && playerCam != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(playerCam.transform.position, 0.06f);
            }
        }
#endif





        void CheckDodgeable()
        {
            if (isKeyNote) return;
            if (triggerCol == null || manager == null || playerHealth == null) return;
            Bounds tb = triggerCol.bounds;
            if (tb.size.sqrMagnitude < 1e-8f) return;
            float feetY = playerCol != null ? playerCol.bounds.min.y : playerTf.position.y;

            bool canJump = false; float apex = 0f;
            if (playerFpc != null && playerFpc.gravity > 0.01f && playerFpc.jumpSpeed > 0f)
            {
                apex = playerFpc.jumpSpeed * playerFpc.jumpSpeed / (2f * playerFpc.gravity);
                canJump = tb.max.y < feetY + apex - 0.15f;
            }
            bool canSlide = false;
            if (playerFpc != null)
            {
                float slideH = Mathf.Max(0.3f, playerFpc.crouchHeight);
                canSlide = tb.min.y > feetY + slideH + 0.1f;
            }
            float trackWidth = Mathf.Max(0.5f, trackMaxX - trackMinX);
            float pw = playerCol != null ? Mathf.Max(0.3f, playerCol.bounds.size.x) : 0.6f;
            bool canStrafe = tb.size.x < trackWidth - pw - 0.4f;
            float travel = speed > 0.01f ? manager.GetTravelTime(speed) : 999f;
            bool reactOk = travel >= minReactionTime;

            string opts = $"jump:{(canJump ? "OK" : "--")} slide:{(canSlide ? "OK" : "--")} strafe:{(canStrafe ? "OK" : "--")} react:{travel:0.00}с";
            string dims = $"trigger={tb.size.x:0.0}x{tb.size.y:0.0}x{tb.size.z:0.0}@{tb.center.y:0.00} apex={apex:0.00} feet={feetY:0.00} track={trackWidth:0.00}";
            if ((canJump || canSlide || canStrafe) && reactOk)
                Debug.Log($"[Dodge] {name}: {opts} {dims}", this);
            else
                Debug.LogWarning($"[Dodge] {name}: IMPOSSIBLE? {opts} {dims}", this);
        }
    }
}
