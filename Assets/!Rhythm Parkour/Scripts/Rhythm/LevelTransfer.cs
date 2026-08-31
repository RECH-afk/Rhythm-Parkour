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
    public static bool hasLevel => levelData != null || !string.IsNullOrEmpty(rkslPath);

    public static void SetLevel(RhythmLevelData data, string srcScene)
    {
        if (data == null) { levelData = null; rkslPath = ""; return; }
        levelData = data.CloneDeep();
        levelName = data.fullTitle;
        sourceScene = srcScene;
        fromEditor = srcScene == "LevelEditor" || srcScene == "IsLevelEditorScene";
        rkslPath = ""; // in-memory имеет приоритет
    }

    public static void SetRkslPath(string path, string srcScene)
    {
        rkslPath = path;
        levelName = System.IO.Path.GetFileNameWithoutExtension(path);
        sourceScene = srcScene;
        fromEditor = false;
        levelData = null; // будет загружен в IsGameSceneLoader
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
