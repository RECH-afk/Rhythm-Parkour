using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Глобальная библиотека объектов (как в Geometry Dash).
/// Один список префабов на всю игру — используется всеми уровнями.
/// Индекс префаба в ObstacleEvent теперь ссылается сюда, а не в levelData.obstaclePrefabs.
/// </summary>
[CreateAssetMenu(menuName = "Rhythm Parkour/Global Obstacle Catalog", fileName = "GlobalObstacleCatalog")]
public class GlobalObstacleCatalog : ScriptableObject
{
    [Tooltip("Общий пул объектов для всех уровней (как в GD). Порядок важен — индекс сохраняется в .rksl")]
    public List<GameObject> prefabs = new List<GameObject>();

    // ── Singleton via Resources ──
    static GlobalObstacleCatalog _instance;
    static bool _triedLoad;

    public static GlobalObstacleCatalog Instance
    {
        get
        {
            if (_instance != null) return _instance;
            if (_triedLoad && _instance == null) TryLoad();
            else if (!_triedLoad) TryLoad();
            return _instance;
        }
    }

    static void TryLoad()
    {
        _triedLoad = true;
        _instance = Resources.Load<GlobalObstacleCatalog>("GlobalObstacleCatalog");
#if UNITY_EDITOR
        if (_instance == null)
        {
            // fallback: поиск по AssetDatabase (удобно если лежит не в Resources)
            string[] guids = AssetDatabase.FindAssets("t:GlobalObstacleCatalog");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = AssetDatabase.LoadAssetAtPath<GlobalObstacleCatalog>(path);
            }
            // автосоздание если вообще нет
            if (_instance == null)
            {
                EnsureAssetExists();
                if (_instance == null)
                {
                    string[] guids2 = AssetDatabase.FindAssets("t:GlobalObstacleCatalog");
                    if (guids2.Length > 0)
                        _instance = AssetDatabase.LoadAssetAtPath<GlobalObstacleCatalog>(AssetDatabase.GUIDToAssetPath(guids2[0]));
                }
            }
        }
#endif
    }

#if UNITY_EDITOR
    static void EnsureAssetExists()
    {
        string[] guids = AssetDatabase.FindAssets("t:GlobalObstacleCatalog");
        if (guids.Length > 0) return;

        string resourcesDir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string path = "Assets/Resources/GlobalObstacleCatalog.asset";
        if (System.IO.File.Exists(path)) return;

        var asset = CreateInstance<GlobalObstacleCatalog>();
        // попробуем наполнить из папки Prefabs/Obstacles если пусто
        TryAutoFill(asset);

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GlobalCatalog] Создан {path} — перетащи туда префабы препятствий (порядок = ID)");
    }

    static void TryAutoFill(GlobalObstacleCatalog catalog)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/!Rhythm Parkour/Prefabs/Obstacles" });
        if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/!Rhythm Parkour/Prefabs" });
        List<GameObject> found = new List<GameObject>();
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null && go.GetComponent<Obstacle>() != null)
                found.Add(go);
        }
        // сортируем по имени чтобы порядок был детерминирован (Obstacle1..9)
        found.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        catalog.prefabs = found;
        if (found.Count > 0)
            Debug.Log($"[GlobalCatalog] Авто-заполнено {found.Count} префабов из Obstacles/");
    }

    [InitializeOnLoadMethod]
    static void EditorInit()
    {
        EditorApplication.delayCall += () =>
        {
            if (Instance == null) return;
            // если каталог пустой но в проекте есть префабы — предложим автозаполнение
            if (Instance.prefabs == null || Instance.prefabs.Count == 0)
            {
                // не спамим, только если есть что заполнить
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/!Rhythm Parkour/Prefabs/Obstacles" });
                if (guids.Length > 0)
                    Debug.LogWarning("[GlobalCatalog] Каталог пуст! Открой Assets/Resources/GlobalObstacleCatalog и перетащи префабы (или они заполнятся автоматически при создании). Пока уровни будут показывать 'нет префабов'.");
            }
        };
    }
#endif

    public static void InvalidateCache() { _instance = null; _triedLoad = false; }

    // ── Удобные статик-хелперы (используй их везде вместо levelData.obstaclePrefabs) ──

    public static int Count
    {
        get
        {
            var inst = Instance;
            if (inst == null || inst.prefabs == null) return 0;
            return inst.prefabs.Count;
        }
    }

    public static GameObject GetPrefab(int index)
    {
        var inst = Instance;
        if (inst == null || inst.prefabs == null || inst.prefabs.Count == 0) return null;
        if (index < 0 || index >= inst.prefabs.Count) return inst.prefabs[0];
        return inst.prefabs[index];
    }

    public static int ClampIndex(int index)
    {
        int c = Count;
        if (c == 0) return 0;
        return Mathf.Clamp(index, 0, c - 1);
    }

    public static Color GetColor(int index)
    {
        float h = (index * 0.37f) % 1f;
        return Color.HSVToRGB(h, 0.78f, 0.92f);
    }

    // Для совместимости: отдать список (только чтение)
    public static List<GameObject> GetAll()
    {
        var inst = Instance;
        if (inst == null) return new List<GameObject>();
        return inst.prefabs;
    }
}
