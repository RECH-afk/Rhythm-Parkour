using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

/// <summary>
/// Спавнит префабы строго по дорожке и двигает к despawn под музыку.
/// — Препятствия не выходят за пределы дорожки (кламп по X, лок Y).
/// — Каждый префаб/ивет имеет свою скорость.
/// — При перетаскивании префаба на spawnPoint он встаёт ровно (префабы центрированы в 0,0,0).
/// </summary>
public class RhythmParkourManager : MonoBehaviour
{
    public static RhythmParkourManager Instance { get; private set; }

    [Header("Уровень")]
    public RhythmLevelData levelData;
    public Conductor conductor;
    public AudioSource musicSource;
    public VideoPlayer videoPlayer;

    [Header("Дорожка")]
    [Tooltip("Пол дорожки (Ground). Если пусто — найдётся объект с именем Ground)")]
    public Transform trackFloor;
    [Tooltip("Ширина дорожки. 0 = взять из trackFloor.localScale.x")]
    public float trackWidth = 0f;
    [HideInInspector] public float trackMinX;
    [HideInInspector] public float trackMaxX;
    [HideInInspector] public float trackY;

    [Header("Точки спавна")]
    [Tooltip("Откуда появляются препятствия")]
    public Transform spawnPoint;
    [Tooltip("Где удаляются (за экраном)")]
    public Transform despawnPoint;
    [HideInInspector] public Transform hitTrigger; // авто — игра сама считает дистанцию спавн→хит
    [Tooltip("Куда двигаются — от spawn к despawn. Если despawn пусто — вперёд по -Z")]
    public Vector3 moveDirection = Vector3.forward;
    [Tooltip("Родитель для спавна (для порядка в иерархии)")]
    public Transform spawnParent;

    [Header("Скорость (дефолт)")]
    [Tooltip("Дефолт если у префаба и у ивента speed=0")]
    public float defaultObstacleSpeed = 12f;

    [HideInInspector] public float obstacleSpeed = 12f; // legacy для совместимости

    [Header("Пул")]
    public int poolSizePerPrefab = 14;

    [Header("Автостарт")]
    public bool autoPlayOnStart = true;
    public float autoPlayDelay = 0.5f;

    [Header("Состояние")]
    public int nextEventIndex;
    public bool isPlaying;
    public float currentTime;

    private List<ObstacleEvent> sortedEvents;
    private readonly List<Obstacle> active = new List<Obstacle>();
    private readonly Dictionary<int, Queue<Obstacle>> pools = new Dictionary<int, Queue<Obstacle>>();
    private Vector3 dirNormalized;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (conductor == null) conductor = GetComponent<Conductor>();
        if (conductor == null) conductor = gameObject.AddComponent<Conductor>();
        if (musicSource == null) musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        conductor.musicSource = musicSource;
        if (spawnParent == null) spawnParent = transform;
        UpdateTrackBounds();
        UpdateDirection();
        EnsureHitTrigger();
    }

    void OnValidate()
    {
        UpdateTrackBounds();
        UpdateDirection();
        if (spawnPoint != null && despawnPoint != null)
            moveDirection = (despawnPoint.position - spawnPoint.position);
        // горизонталим
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.001f) moveDirection = new Vector3(0, 0, -1);
    }

    void UpdateTrackBounds()
    {
        if (trackFloor == null)
        {
            var go = GameObject.Find("Ground");
            if (go != null) trackFloor = go.transform;
        }
        float width = trackWidth;
        if (width <= 0.01f)
        {
            if (trackFloor != null) width = Mathf.Abs(trackFloor.localScale.x);
            else width = 6f;
        }
        float centerX = trackFloor != null ? trackFloor.position.x : 0f;
        // небольшой запас чтобы не прилипать к стене (половина толщины коллайдера)
        float half = width * 0.5f - 0.05f;
        trackMinX = centerX - half;
        trackMaxX = centerX + half;
        trackY = trackFloor != null ? trackFloor.position.y + trackFloor.localScale.y * 0.5f : 0.5f;
        // legacy sync
        if (defaultObstacleSpeed < 0.1f) defaultObstacleSpeed = obstacleSpeed > 0.1f ? obstacleSpeed : 12f;
        else obstacleSpeed = defaultObstacleSpeed;
    }

    void UpdateDirection()
    {
        Vector3 dir;
        if (spawnPoint != null && despawnPoint != null)
            dir = despawnPoint.position - spawnPoint.position;
        else
            dir = moveDirection;
        dir.y = 0f; // строго по горизонту дорожки
        if (dir.sqrMagnitude < 0.001f) dir = new Vector3(0, 0, -1);
        dirNormalized = dir.normalized;
    }

    void EnsureHitTrigger()
    {
        if (hitTrigger != null) return;
        // ищем по имени/тегу/IsTrigger
        var go = GameObject.Find("HitTrigger");
        if (go == null) go = GameObject.Find("Trigger");
        if (go == null) go = GameObject.Find("PlayerTrigger");
        if (go != null) { hitTrigger = go.transform; return; }
        // первый триггер в сцене
        var triggers = FindObjectsOfType<Collider>();
        foreach (var c in triggers) if (c.isTrigger && c.CompareTag("Untagged")==false) { /* пропускаем */ }
        foreach (var c in triggers) if (c.isTrigger && c.gameObject.name.ToLower().Contains("trigger")) { hitTrigger = c.transform; return; }
        // фолбэк — точка перед игроком (если есть FirstPersonController)
        var fpc = FindObjectOfType<EasyPeasyFirstPersonController.FirstPersonController>();
        if (fpc != null)
        {
            var t = new GameObject("HitTrigger (Auto)").transform;
            t.position = fpc.transform.position + fpc.transform.forward * 4f;
            t.position = new Vector3(Mathf.Clamp(t.position.x, trackMinX, trackMaxX), 0, t.position.z);
            hitTrigger = t;
        }
    }

    public float GetSpawnToHitDistance()
    {
        if (spawnPoint == null) return 52f;
        Vector3 targetPos;
        if (hitTrigger != null) targetPos = hitTrigger.position;
        else if (despawnPoint != null) targetPos = Vector3.Lerp(spawnPoint.position, despawnPoint.position, 0.88f);
        else targetPos = spawnPoint.position + dirNormalized * 52f;
        Vector3 toHit = targetPos - spawnPoint.position;
        toHit.y = 0f;
        float d = Mathf.Abs(Vector3.Dot(toHit, dirNormalized));
        if (d < 1f) d = 52f;
        return d;
    }
    public float GetTravelTime(float speed)
    {
        if (speed < 0.1f) speed = defaultObstacleSpeed;
        return GetSpawnToHitDistance() / Mathf.Max(1f, speed);
    }
    public float GetTravelTime(ObstacleEvent ev)
    {
        float s = ev.speed;
        if (s < 0.1f && levelData != null)
        {
            var pf = levelData.GetPrefab(ev.prefabIndex);
            if (pf) { var ob = pf.GetComponent<Obstacle>(); if (ob) s = ob.baseSpeed; }
        }
        return GetTravelTime(s);
    }

    void Start()
    {
        // Авто-фикс битых ссылок из сцены (если spawn указывает на Obstacle/Colider)
        if (spawnPoint != null && (spawnPoint.name == "Obstacle" || spawnPoint.name == "Colider"))
            spawnPoint = null;
        if (despawnPoint != null && (despawnPoint.name == "Obstacle" || despawnPoint.name == "Colider"))
            despawnPoint = null;

        UpdateTrackBounds();
        EnsureHitTrigger();
        // Спавн/деспавн/хит строго по центру дорожки X=0, Y на уровне земли
        if (spawnPoint == null) spawnPoint = EnsurePoint("SpawnPoint", new Vector3(0f, 0f, 52f));
        if (despawnPoint == null) despawnPoint = EnsurePoint("DespawnPoint", new Vector3(0f, 0f, -8f));
        if (hitTrigger == null)
        {
            // создаём триггер перед игроком если его нет — в 6м от спавна по дорожке
            Vector3 hp = spawnPoint.position + dirNormalized * (GetSpawnToHitDistance() > 1f ? GetSpawnToHitDistance() : 38f);
            // если дистанция ещё не известна — ставим на 10м перед центром дорожки
            if (Vector3.Distance(hp, spawnPoint.position) < 5f) hp = new Vector3(0, 0, 8f);
            hitTrigger = EnsurePoint("HitTrigger", hp);
            hitTrigger.gameObject.tag = "Untagged";
            var col = hitTrigger.GetComponent<BoxCollider>();
            if (col == null) col = hitTrigger.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(6f, 3f, 1f);
            col.center = new Vector3(0, 1f, 0);
        }
        // выравниваем по дорожке
        AlignPointToTrack(spawnPoint);
        AlignPointToTrack(despawnPoint);
        AlignPointToTrack(hitTrigger);
        hitTrigger.rotation = Quaternion.LookRotation(dirNormalized, Vector3.up);
        if (spawnParent == null) spawnParent = spawnPoint;

        if (musicSource == null) musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        conductor.musicSource = musicSource;

        // Если есть IsGameSceneLoader — он главный, не мешаем ему
        if (FindObjectOfType<IsGameSceneLoader>() != null)
        {
            Debug.Log("[Rhythm] IsGameSceneLoader обнаружен — менеджер откладывает Prepare до загрузки", this);
            // всё равно инициализируем дорожку, но не готовим уровень
            if (levelData == null && LevelTransfer.levelData != null) levelData = LevelTransfer.levelData;
            return;
        }
        // трансфер из редактора — приоритет, чтобы не терять прогресс
        if (LevelTransfer.levelData != null)
        {
            if (LevelTransfer.fromEditor || levelData == null)
            {
                levelData = LevelTransfer.levelData;
                Debug.Log($"[Transfer] IsGameScene взял уровень '{levelData.fullTitle}' из редактора ({LevelTransfer.sourceScene})");
            }
        }

        if (levelData != null) PrepareLevel(levelData);
        else Debug.LogWarning("[Rhythm] Нет LevelData — ждём IsGameSceneLoader или создайте .rksl", this);

        if (autoPlayOnStart && levelData != null && levelData.music != null)
            Invoke(nameof(Play), autoPlayDelay);
    }

    void AlignPointToTrack(Transform t)
    {
        if (t == null) return;
        Vector3 p = t.position;
        p.x = Mathf.Clamp(p.x, trackMinX, trackMaxX);
        // Y ставим на 0 (корень префаба имеет мешь +0.5 для высоты) — чтобы bottom точно на дорожке
        p.y = 0f;
        t.position = p;
        // поворот вдоль дорожки
        t.rotation = Quaternion.LookRotation(dirNormalized != Vector3.zero ? dirNormalized : Vector3.forward, Vector3.up);
    }

    Transform EnsurePoint(string name, Vector3 pos)
    {
        var go = GameObject.Find(name);
        if (go == null) { go = new GameObject(name); go.transform.position = pos; }
        return go.transform;
    }

    public void PrepareLevel(RhythmLevelData data)
    {
        // Уничтожаем старый пул чтобы не течь
        foreach (var kv in pools)
        {
            while (kv.Value.Count > 0)
            {
                var ob = kv.Value.Dequeue();
                if (ob != null) Destroy(ob.gameObject);
            }
        }
        foreach (var o in active) if (o != null) Destroy(o.gameObject);
        active.Clear();
        pools.Clear();

        levelData = data;
        if (data == null) return;
        data.SortByTime();
        // мигрируем старые ивенты где speed=0 -> дефолт из префаба или менеджера
        MigrateSpeeds(data);
        sortedEvents = new List<ObstacleEvent>(data.events);
        nextEventIndex = 0;
        UpdateTrackBounds();
        UpdateDirection();

        // ── ГЛОБАЛЬНЫЕ префабы (как в GD) ──
        int prefabCount = GlobalObstacleCatalog.Count;
        List<GameObject> sourcePrefabs = null;
        if (prefabCount > 0) sourcePrefabs = GlobalObstacleCatalog.GetAll();
        else if (data.obstaclePrefabs != null && data.obstaclePrefabs.Count > 0) { sourcePrefabs = data.obstaclePrefabs; prefabCount = sourcePrefabs.Count; }

        if (sourcePrefabs != null)
        {
            for (int i = 0; i < prefabCount; i++)
            {
                var prefab = i < sourcePrefabs.Count ? sourcePrefabs[i] : null;
                if (prefab == null) prefab = data.GetPrefab(i); // fallback через GetPrefab (глобальный+локальный)
                if (prefab == null) continue;
                var q = new Queue<Obstacle>();
                for (int k = 0; k < poolSizePerPrefab; k++)
                {
                    var go = Instantiate(prefab, spawnParent, false);
                    go.transform.localPosition = prefab.transform.localPosition;
                    go.transform.localRotation = prefab.transform.localRotation;
                    go.transform.localScale = prefab.transform.localScale;
                    go.SetActive(false);
                    var ob = go.GetComponent<Obstacle>();
                    if (ob == null) ob = go.AddComponent<Obstacle>();
                    q.Enqueue(ob);
                }
                pools[i] = q;
            }
        }

        if (videoPlayer != null && data.video != null) videoPlayer.clip = data.video;
    }

    void MigrateSpeeds(RhythmLevelData data)
    {
        bool dirty = false;
        for (int i = 0; i < data.events.Count; i++)
        {
            var e = data.events[i];
            if (e.speed < 0.01f)
            {
                var prefab = data.GetPrefab(e.prefabIndex);
                float prefSpeed = 0f;
                if (prefab != null)
                {
                    var ob = prefab.GetComponent<Obstacle>();
                    if (ob != null) prefSpeed = ob.baseSpeed;
                }
                if (prefSpeed < 0.1f) prefSpeed = defaultObstacleSpeed > 0.1f ? defaultObstacleSpeed : 12f;
                e.speed = prefSpeed;
                data.events[i] = e;
                dirty = true;
            }
        }

    }

    public void Play()
    {
        if (levelData == null || levelData.music == null) { Debug.LogWarning("[Rhythm] Нет LevelData/music", this); return; }
        PrepareLevel(levelData);
        conductor.Play(levelData, musicSource);
        if (videoPlayer != null && levelData.video != null) { videoPlayer.clip = levelData.video; videoPlayer.Play(); }
        nextEventIndex = 0;
        isPlaying = true;
        currentTime = 0f;
        foreach (var o in active.ToArray()) ReturnToPool(o);
        active.Clear();
    }

    public void Stop()
    {
        isPlaying = false;
        conductor?.Stop();
        if (videoPlayer != null) videoPlayer.Stop();
        foreach (var o in active.ToArray()) ReturnToPool(o);
        active.Clear();
        nextEventIndex = 0;
    }

    void Update()
    {
        // Esc — возврат в редактор без потери прогресса (когда пришли из редактора)
        if (Input.GetKeyDown(KeyCode.Escape) && LevelTransfer.fromEditor && LevelTransfer.hasLevel)
        {
            string cur = SceneManager.GetActiveScene().name;
            if (cur == "IsGameScene")
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                string target = LevelTransfer.sourceScene;
                if (string.IsNullOrEmpty(target)) target = "IsLevelEditorScene";
                // ищем сцену в build settings
                bool canLoad = false;
                string found = target;
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string p = SceneUtility.GetScenePathByBuildIndex(i);
                    string n = Path.GetFileNameWithoutExtension(p);
                    if (n == target || n == "IsLevelEditorScene" || n == "LevelEditor")
                    { found = n; canLoad = true; break; }
                }
                if (canLoad) SceneManager.LoadScene(found);
                else
                {
                    // фолбэк — пробуем по имени напрямую (может быть не в билде но загрузится в эдиторе)
                    try { SceneManager.LoadScene(target); } catch { SceneManager.LoadScene("IsLevelEditorScene"); }
                }
                return;
            }
        }

        if (!isPlaying || levelData == null || conductor == null || !conductor.isPlaying) return;
        currentTime = conductor.songPosition;

        while (nextEventIndex < sortedEvents.Count)
        {
            var evt = sortedEvents[nextEventIndex];
            if (currentTime >= evt.time)
            {
                Spawn(evt);
                nextEventIndex++;
            }
            else break;
        }
    }

    void Spawn(ObstacleEvent evt)
    {
        GameObject prefab = levelData.GetPrefab(evt.prefabIndex);
        if (prefab == null) return;

        Obstacle ob = GetFromPool(evt.prefabIndex, prefab);
        if (ob == null) return;

        Transform sp = spawnPoint != null ? spawnPoint : transform;
        float centerX = (trackMinX + trackMaxX) * 0.5f;

        // всегда посередине дорожки — не сохраняем оффсет префаба по X
        ob.transform.SetParent(sp, false);
        ob.transform.localPosition = Vector3.zero;
        ob.transform.localRotation = Quaternion.identity;
        ob.transform.localScale = prefab.transform.localScale;

        // поворот вдоль дорожки
        if (dirNormalized.sqrMagnitude > 0.001f)
            ob.transform.rotation = Quaternion.LookRotation(dirNormalized, Vector3.up);

        // множители из ивента
        if (evt.rotation != Vector3.zero) ob.transform.localRotation *= Quaternion.Euler(evt.rotation);
        if (evt.scale != Vector3.zero && evt.scale != Vector3.one) ob.transform.localScale = Vector3.Scale(ob.transform.localScale, evt.scale);

        // X: центр + lane offset из ивента (pos.x), Y на дорожке
        Vector3 wpos = sp.position;
        float lane = evt.position.x; // 0 = центр, -3..3 внутри дорожки
        wpos.x = Mathf.Clamp(centerX + lane, trackMinX, trackMaxX);
        wpos.y = sp.position.y;
        ob.transform.position = wpos;

        // страховка — если коллайдер шире дорожки, центрируем с учётом lane но клампим
        Collider col = ob.GetComponent<Collider>();
        if (col != null)
        {
            Physics.SyncTransforms();
            Bounds wb = col.bounds;
            float half = wb.extents.x;
            if (half > (trackMaxX - trackMinX) * 0.5f + 0.01f)
            {
                ob.transform.position = new Vector3(centerX, wpos.y, wpos.z);
            }
            else
            {
                // кламп чтобы не выйти за стены даже с lane
                wb = col.bounds;
                float left = wb.min.x, right = wb.max.x;
                if (left < trackMinX - 0.02f) wpos.x += (trackMinX - left);
                else if (right > trackMaxX + 0.02f) wpos.x -= (right - trackMaxX);
                ob.transform.position = wpos;
            }
        }

        // пер-нотный цвет (если задан — перекрашиваем все рендеры), иначе возвращаем к глобальному/белому
        {
            Color toApply = Color.clear;
            if (evt.HasCustomColor) toApply = evt.color;
            else if (levelData != null && levelData.obstacleColor != Color.white) toApply = levelData.obstacleColor;
            else if (levelData != null && levelData.obstacleColor == Color.white) toApply = Color.white; // сброс к белому чтобы не остался старый кастом
            // если toApply прозрачный — не трогаем (оставляем как у префаба)
            if (toApply != Color.clear)
            {
                foreach (var r in ob.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.materials;
                    for (int i=0;i<mats.Length;i++)
                    {
                        if (mats[i].HasProperty("_Color"))
                        {
                            mats[i] = new Material(mats[i]);
                            mats[i].color = toApply;
                        }
                        else if (mats[i].HasProperty("_BaseColor"))
                        {
                            mats[i] = new Material(mats[i]);
                            mats[i].SetColor("_BaseColor", toApply);
                        }
                    }
                    r.materials = mats;
                }
            }
        }

        ob.gameObject.SetActive(true);
        float spd = evt.speed > 0.01f ? evt.speed : (ob.baseSpeed > 0.01f ? ob.baseSpeed : defaultObstacleSpeed);
        ob.Init(this, dirNormalized, spd, despawnPoint, evt.time);
        active.Add(ob);
    }

    Obstacle GetFromPool(int index, GameObject prefab)
    {
        if (pools.TryGetValue(index, out var q) && q.Count > 0)
        {
            var ob = q.Dequeue();
            if (ob == null) return CreateNew(prefab);
            return ob;
        }
        return CreateNew(prefab);
    }

    Obstacle CreateNew(GameObject prefab)
    {
        var go = Instantiate(prefab, spawnParent, false);
        go.transform.localPosition = prefab.transform.localPosition;
        go.transform.localRotation = prefab.transform.localRotation;
        go.transform.localScale = prefab.transform.localScale;
        var ob = go.GetComponent<Obstacle>();
        if (ob == null) ob = go.AddComponent<Obstacle>();
        return ob;
    }

    public void ReturnToPool(Obstacle ob)
    {
        if (ob == null) return;
        active.Remove(ob);
        ob.gameObject.SetActive(false);
        ob.transform.SetParent(spawnParent != null ? spawnParent : transform);

        int idx = 0;
        // ищем индекс по имени в глобальном каталоге (а не в levelData)
        int gCount = GlobalObstacleCatalog.Count;
        if (gCount > 0)
        {
            var all = GlobalObstacleCatalog.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && ob.name.Contains(all[i].name)) { idx = i; break; }
        }
        else if (levelData != null && levelData.obstaclePrefabs != null)
        {
            for (int i = 0; i < levelData.obstaclePrefabs.Count; i++)
                if (levelData.obstaclePrefabs[i] != null && ob.name.Contains(levelData.obstaclePrefabs[i].name)) { idx = i; break; }
        }
        if (!pools.ContainsKey(idx)) pools[idx] = new Queue<Obstacle>();
        pools[idx].Enqueue(ob);
    }

    // Для превью в редакторе
    public void PreviewSpawnAtBeat(float beat, int prefabIndex)
    {
        if (levelData == null) return;
        var evt = ObstacleEvent.Create(beat, prefabIndex, Vector3.zero);
        evt.time = levelData.BeatToTime(beat);
        // скорость по умолчанию из префаба
        var prefab = levelData.GetPrefab(prefabIndex);
        if (prefab != null)
        {
            var obc = prefab.GetComponent<Obstacle>();
            if (obc != null) evt.speed = obc.baseSpeed;
        }
        if (evt.speed < 0.1f) evt.speed = defaultObstacleSpeed;
        Spawn(evt);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        UpdateTrackBounds();
        // дорожка
        if (trackFloor != null)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            Vector3 center = trackFloor.position;
            Vector3 size = new Vector3(Mathf.Abs(trackMaxX - trackMinX) + 0.1f, 0.2f, trackFloor.localScale.z);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireCube(center, size);
        }
        else
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            float w = Mathf.Abs(trackMaxX - trackMinX);
            Gizmos.DrawCube(new Vector3((trackMinX + trackMaxX) * 0.5f, 0f, 25f), new Vector3(w, 0.2f, 60f));
        }
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(spawnPoint.position, Vector3.one * 1.2f);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + dirNormalized * 3f);
            UnityEditor.Handles.Label(spawnPoint.position + Vector3.up * 1.5f, "SPAWN");
            // ширина спавна
            Gizmos.color = new Color(0, 1, 0, 0.25f);
            Gizmos.DrawLine(new Vector3(trackMinX, spawnPoint.position.y, spawnPoint.position.z), new Vector3(trackMaxX, spawnPoint.position.y, spawnPoint.position.z));
        }
        if (despawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(despawnPoint.position, Vector3.one * 1.2f);
            UnityEditor.Handles.Label(despawnPoint.position + Vector3.up * 1.5f, "DESPAWN");
        }
        if (hitTrigger != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(hitTrigger.position, new Vector3(6f, 2f, 1f));
            UnityEditor.Handles.Label(hitTrigger.position + Vector3.up * 2.2f, "HIT TRIGGER — тут нота в момент бита");
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.25f);
            Gizmos.DrawLine(hitTrigger.position + Vector3.left * 3f, hitTrigger.position + Vector3.right * 3f);
        }
        if (spawnPoint != null && despawnPoint != null)
        {
            Gizmos.color = new Color(0, 1, 0.6f, 0.35f);
            Gizmos.DrawLine(spawnPoint.position, despawnPoint.position);
            if (hitTrigger != null)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.5f);
                Gizmos.DrawLine(spawnPoint.position, hitTrigger.position);
                UnityEditor.Handles.Label(Vector3.Lerp(spawnPoint.position, hitTrigger.position, 0.5f) + Vector3.up * 0.5f, $"{GetSpawnToHitDistance():0.0}м");
            }
        }
    }
#endif
}