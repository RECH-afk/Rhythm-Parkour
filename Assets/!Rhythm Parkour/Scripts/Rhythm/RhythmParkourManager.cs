using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Спавнит префабы из палитры в точке spawnPoint и двигает вперёд к despawnPoint под музыку.
/// </summary>
public class RhythmParkourManager : MonoBehaviour
{
    public static RhythmParkourManager Instance { get; private set; }

    [Header("Уровень")]
    public RhythmLevelData levelData;
    public Conductor conductor;
    public AudioSource musicSource;
    public VideoPlayer videoPlayer;

    [Header("Точки спавна")]
    [Tooltip("Откуда появляются препятствия")]
    public Transform spawnPoint;
    [Tooltip("Где удаляются (за экраном)")]
    public Transform despawnPoint;
    [Tooltip("Куда двигаются — от spawn к despawn. Если despawn пусто — вперёд по Z")]
    public Vector3 moveDirection = Vector3.forward;
    [Tooltip("Скорость движения препятствий")]
    public float obstacleSpeed = 10f;
    [Tooltip("Родитель для спавна (для порядка в иерархии)")]
    public Transform spawnParent;

    [Header("Пул")]
    public int poolSizePerPrefab = 10;

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
        UpdateDirection();
    }

    void OnValidate()
    {
        UpdateDirection();
        if (spawnPoint != null && despawnPoint != null)
            moveDirection = (despawnPoint.position - spawnPoint.position).normalized;
    }

    void UpdateDirection()
    {
        if (spawnPoint != null && despawnPoint != null)
            dirNormalized = (despawnPoint.position - spawnPoint.position).normalized;
        else
            dirNormalized = moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : Vector3.forward;
    }

    void Start()
    {
        // Авто-фикс битых ссылок из сцены (если spawn указывает на Obstacle/Colider)
        if (spawnPoint != null && (spawnPoint.name == "Obstacle" || spawnPoint.name == "Colider"))
            spawnPoint = null;
        if (despawnPoint != null && (despawnPoint.name == "Obstacle" || despawnPoint.name == "Colider"))
            despawnPoint = null;
        if (spawnPoint == null) spawnPoint = EnsurePoint("SpawnPoint", new Vector3(0, 1, 40));
        if (despawnPoint == null) despawnPoint = EnsurePoint("DespawnPoint", new Vector3(0, 1, -15));
        if (spawnParent == null) spawnParent = spawnPoint;

        if (musicSource == null) musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        conductor.musicSource = musicSource;

        if (levelData == null)
        {
            // пробуем найти любой LevelData в проекте
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:RhythmLevelData");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                levelData = UnityEditor.AssetDatabase.LoadAssetAtPath<RhythmLevelData>(path);
                Debug.Log($"[Rhythm] Авто-найден LevelData: {path}", this);
            }
#endif
        }

        if (levelData != null) PrepareLevel(levelData);
        else Debug.LogWarning("[Rhythm] Нет LevelData! Создай через Create → Rhythm Parkour → Level Data и назначь в Manager", this);

        if (autoPlayOnStart && levelData != null && levelData.music != null)
            Invoke(nameof(Play), autoPlayDelay);
    }

    Transform EnsurePoint(string name, Vector3 pos)
    {
        var go = GameObject.Find(name);
        if (go == null) { go = new GameObject(name); go.transform.position = pos; }
        return go.transform;
    }

    public void PrepareLevel(RhythmLevelData data)
    {
        levelData = data;
        if (data == null) return;
        data.SortByTime();
        sortedEvents = new List<ObstacleEvent>(data.events);
        nextEventIndex = 0;
        active.Clear();
        pools.Clear();
        UpdateDirection();

        for (int i = 0; i < data.obstaclePrefabs.Count; i++)
        {
            var prefab = data.obstaclePrefabs[i];
            if (prefab == null) continue;
            var q = new Queue<Obstacle>();
            for (int k = 0; k < poolSizePerPrefab; k++)
            {
                var go = Instantiate(prefab, spawnParent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = prefab.transform.localScale;
                go.SetActive(false);
                var ob = go.GetComponent<Obstacle>();
                if (ob == null) ob = go.AddComponent<Obstacle>();
                q.Enqueue(ob);
            }
            pools[i] = q;
        }

        if (videoPlayer != null && data.video != null) videoPlayer.clip = data.video;
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

        // Точно как перетаскивание префаба на объект спавна — без рандома, ровно по дорожке
        Transform sp = spawnPoint != null ? spawnPoint : transform;
        ob.transform.SetParent(sp, false);
        ob.transform.localPosition = Vector3.zero;
        ob.transform.localRotation = Quaternion.identity;
        // Размер строго как в префабе
        ob.transform.localScale = prefab.transform.localScale;
        // Если в событии указан поворот/масштаб — применяем как множитель
        if (evt.rotation != Vector3.zero) ob.transform.localRotation = Quaternion.Euler(evt.rotation);
        if (evt.scale != Vector3.zero && evt.scale != Vector3.one) ob.transform.localScale = Vector3.Scale(prefab.transform.localScale, evt.scale);
        ob.gameObject.SetActive(true);
        ob.Init(this, dirNormalized, obstacleSpeed, despawnPoint, evt.time);
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
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
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
        if (levelData != null)
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
        Spawn(evt);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(spawnPoint.position, Vector3.one * 1.2f);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + dirNormalized * 3f);
            UnityEditor.Handles.Label(spawnPoint.position + Vector3.up * 1.5f, "SPAWN");
        }
        if (despawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(despawnPoint.position, Vector3.one * 1.2f);
            UnityEditor.Handles.Label(despawnPoint.position + Vector3.up * 1.5f, "DESPAWN");
        }
        if (spawnPoint != null && despawnPoint != null)
        {
            Gizmos.color = new Color(0, 1, 0.6f, 0.3f);
            Gizmos.DrawLine(spawnPoint.position, despawnPoint.position);
        }
    }
#endif
}
