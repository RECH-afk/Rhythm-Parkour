using UnityEngine;

/// <summary>
/// Статический перенос уровня между сценами без потери прогресса.
/// Хранит клон данных уровня при переходе Редактор -> Игра и обратно.
/// </summary>
public static class LevelTransfer
{
    public static RhythmLevelData levelData;
    public static string rkslPath = "";
    public static string levelName = "";
    public static string sourceScene = "";
    public static bool fromEditor = false;
    // hasLevel истинен только если есть валидный in-memory или существующий файл
    public static bool hasLevel
    {
        get
        {
            if (levelData != null) return true;
            if (!string.IsNullOrEmpty(rkslPath) && System.IO.File.Exists(rkslPath)) return true;
            // также проверяем PlayerPrefs как fallback (для перезапуска)
            string sel = UnityEngine.PlayerPrefs.GetString("SelectedLevelPath", "");
            if (!string.IsNullOrEmpty(sel) && System.IO.File.Exists(sel)) return true;
            string last = UnityEngine.PlayerPrefs.GetString("LastRkslPath", "");
            if (!string.IsNullOrEmpty(last) && System.IO.File.Exists(last)) return true;
            return false;
        }
    }

    public static string GetEffectivePath()
    {
        if (!string.IsNullOrEmpty(rkslPath) && System.IO.File.Exists(rkslPath)) return rkslPath;
        string sel = UnityEngine.PlayerPrefs.GetString("SelectedLevelPath", "");
        if (!string.IsNullOrEmpty(sel) && System.IO.File.Exists(sel)) return sel;
        string last = UnityEngine.PlayerPrefs.GetString("LastRkslPath", "");
        if (!string.IsNullOrEmpty(last) && System.IO.File.Exists(last)) return last;
        return rkslPath;
    }

    public static void SetLevel(RhythmLevelData data, string srcScene)
    {
        if (data == null) { levelData = null; rkslPath = ""; return; }
        levelData = data.CloneDeep();
        levelName = string.IsNullOrEmpty(data.fullTitle) ? System.IO.Path.GetFileNameWithoutExtension(data.audioPath) : data.fullTitle;
        sourceScene = srcScene;
        fromEditor = srcScene == "LevelEditor" || srcScene == "IsLevelEditorScene";
        rkslPath = ""; // in-memory имеет приоритет
        UnityEngine.Debug.Log($"[LevelTransfer] SetLevel '{levelName}' from {srcScene} events={data.events?.Count ?? 0}");
    }

    public static void SetRkslPath(string path, string srcScene)
    {
        rkslPath = path;
        levelName = System.IO.Path.GetFileNameWithoutExtension(path);
        sourceScene = srcScene;
        fromEditor = false;
        levelData = null; // будет загружен в IsGameSceneLoader
        // Сохраняем сразу в PlayerPrefs чтобы пережить перезапуск
        UnityEngine.PlayerPrefs.SetString("SelectedLevelPath", path);
        UnityEngine.PlayerPrefs.SetString("LastRkslPath", path);
        UnityEngine.PlayerPrefs.Save();
        UnityEngine.Debug.Log($"[LevelTransfer] SetRkslPath '{path}' from {srcScene}");
    }

    public static void Clear()
    {
        levelData = null;
        rkslPath = "";
        levelName = "";
        sourceScene = "";
        fromEditor = false;
    }
}
