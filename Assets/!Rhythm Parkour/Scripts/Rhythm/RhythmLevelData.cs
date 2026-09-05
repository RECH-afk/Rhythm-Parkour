using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public struct ObstacleEvent
{
    public float time;       // спавн (сек) — считается автоматом из beat
    public float beat;       // хит-бит (когда у игрока) — главное поле
    public int prefabIndex;
    [HideInInspector] public Vector3 position; // x = lane offset внутри дорожки
    public Vector3 rotation;
    public Vector3 scale;
    public float speed;      // 0 = baseSpeed префаба
    public Color color;      // per-note цвет, a=0 => использовать глобальный obstacleColor
    public string comment;

    public bool HasCustomColor => color.a > 0.01f;
    public Color EffectiveColor(Color fallback) => HasCustomColor ? color : fallback;

    public static ObstacleEvent Create(float beat, int prefabIndex, Vector3 pos, float speed = 0f) => new ObstacleEvent
    {
        beat = beat,
        prefabIndex = prefabIndex,
        position = pos,
        rotation = Vector3.zero,
        scale = Vector3.one,
        speed = speed,
        color = new Color(0,0,0,0) // прозрачный = нет оверрайда, будет глобальный
    };
}

[System.Serializable]
public class RhythmLevelData
{
    [Header("Музыка")]
    public AudioClip music;
    public VideoClip video;
    public Sprite cover;
    public string fullTitle;
    public string songAuthor;
    public string mapAuthor;
    [Min(1)] public float bpm = 128f;
    public float offset = 0f;
    public string audioPath;
    public string videoPath;

    [Header("Визуал карты")]
    public bool particlesEnabled = true;
    public Color particleColor = new Color(0.2f, 0.7f, 1f, 1f);
    public Color obstacleColor = Color.white;
    public Color trackColor = new Color(0.2f, 0.6f, 1f, 1f);
    public bool sphereRotates = true;
    [Tooltip("Имя материала для препятствий по умолчанию (пусто = материал префаба). Сохраняется в .rksl")]
    public string defaultObstacleMaterialName = "";
    [Tooltip("Опционально прямой референс для редактора (не сериализуется в .rksl, синхронизируется по имени)")]
    public Material defaultObstacleMaterial;

    [Header("Префабы (DEPRECATED — теперь глобально)")]
    [Tooltip("Устарело: теперь префабы берутся из GlobalObstacleCatalog (Resources/GlobalObstacleCatalog). Оставлено для совместимости старых уровней.")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("Ноты — ХИТ у игрока")]
    public List<ObstacleEvent> events = new List<ObstacleEvent>();

    public float BeatToTime(float beat) => offset + beat * 60f / Mathf.Max(1f, bpm);
    public float TimeToBeat(float time) => (time - offset) * Mathf.Max(1f, bpm) / 60f;
    public float Duration => music ? music.length : 0f;
    public float TotalBeats => music ? TimeToBeat(music.length) : 0f;
    public void SortByTime() => events.Sort((a, b) => a.time.CompareTo(b.time));
    public GameObject GetPrefab(int index)
    {
        // 1) пробуем глобальный каталог (как в GD — один на все уровни)
        var global = GlobalObstacleCatalog.GetPrefab(index);
        if (global != null) return global;
        // 2) fallback для старых уровней где ещё заполнен локальный список
        if (obstaclePrefabs != null && obstaclePrefabs.Count > 0)
        {
            if (index < 0 || index >= obstaclePrefabs.Count) return obstaclePrefabs[0];
            return obstaclePrefabs[index];
        }
        return null;
    }

    /// <summary>Сколько префабов доступно (глобально, либо локально для старых данных)</summary>
    public int PrefabCount
    {
        get
        {
            int g = GlobalObstacleCatalog.Count;
            if (g > 0) return g;
            return obstaclePrefabs != null ? obstaclePrefabs.Count : 0;
        }
    }

    /// <summary>Миграция старого уровня: если глобальный пуст а локальный заполнен — копируем в глобальный (вызови один раз в редакторе)</summary>
    public void MigratePrefabsToGlobal()
    {
#if UNITY_EDITOR
        if (obstaclePrefabs == null || obstaclePrefabs.Count == 0) return;
        var cat = GlobalObstacleCatalog.Instance;
        if (cat == null) return;
        if (cat.prefabs != null && cat.prefabs.Count > 0) return; // уже заполнен
        cat.prefabs = new List<GameObject>(obstaclePrefabs);
        UnityEditor.EditorUtility.SetDirty(cat);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[RhythmLevelData] Мигрировано {obstaclePrefabs.Count} префабов в GlobalObstacleCatalog из уровня '{fullTitle}'");
#endif
    }
    public void OnValidate() => bpm = Mathf.Max(1f, bpm);
    public RhythmLevelData Clone()
    {
        return (RhythmLevelData)MemberwiseClone();
    }
    public RhythmLevelData CloneDeep()
    {
        var c = (RhythmLevelData)MemberwiseClone();
        c.obstaclePrefabs = new System.Collections.Generic.List<GameObject>(obstaclePrefabs ?? new System.Collections.Generic.List<GameObject>());
        c.events = new System.Collections.Generic.List<ObstacleEvent>(events ?? new System.Collections.Generic.List<ObstacleEvent>());
        return c;
    }
}
