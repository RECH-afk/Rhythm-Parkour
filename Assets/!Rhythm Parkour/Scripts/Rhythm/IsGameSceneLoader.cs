using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Простая загрузка уровня для IsGameScene.
/// Смотрит: LevelTransfer.rkslPath -> PlayerPrefs SelectedLevelPath/LastRkslPath -> файлы в Levels -> дефолт менеджера
/// </summary>
public class IsGameSceneLoader : MonoBehaviour
{
    public RhythmParkourManager manager;

    void Awake()
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        Debug.Log($"[IsGameSceneLoader] Awake scene={SceneManager.GetActiveScene().name} manager={(manager?manager.name:"null")} LevelTransfer.hasLevel={LevelTransfer.hasLevel} rkslPath='{LevelTransfer.rkslPath}' LastRksl='{PlayerPrefs.GetString("LastRkslPath","")}' Selected='{PlayerPrefs.GetString("SelectedLevelPath","")}'", this);
    }

    void Start()
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (manager == null) { Debug.LogError("[IsGameSceneLoader] manager не найден!"); return; }

        // 1) in-memory из редактора (самый приоритет)
        if (LevelTransfer.hasLevel && LevelTransfer.levelData != null && LevelTransfer.levelData.events != null && LevelTransfer.levelData.events.Count >= 0)
        {
            // даже если music==null — берем, чтобы не пусто
            if (LevelTransfer.levelData.events.Count > 0 || LevelTransfer.levelData.music != null)
            {
                Debug.Log($"[IsGameSceneLoader] Использую LevelTransfer in-memory '{LevelTransfer.levelName}' events={LevelTransfer.levelData.events.Count}", this);
                ApplyLevel(LevelTransfer.levelData);
                return;
            }
        }
        // 2) путь из меню (SelectedLevelPath) или LevelTransfer.rkslPath
        string path = LevelTransfer.rkslPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            path = PlayerPrefs.GetString("SelectedLevelPath", "");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            path = PlayerPrefs.GetString("LastRkslPath", "");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            path = LevelTransfer.rkslPath; // еще раз
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            Debug.Log($"[IsGameSceneLoader] Гружу rksl '{path}'", this);
            StartCoroutine(LoadFromRksl(path));
            return;
        }
        // 3) любой файл в папках
        string[] dirs = new string[] {
            Path.Combine(Application.persistentDataPath, "Levels"),
            Path.Combine(Application.streamingAssetsPath, "GameLevels"),
            Path.Combine(Application.dataPath, "!Rhythm Parkour/Levels"),
            Path.Combine(Application.temporaryCachePath, "LevelTransfer"),
            Path.Combine(Application.temporaryCachePath, "RkslGame")
        };
        foreach (var d in dirs)
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
            var files = Directory.GetFiles(d, "*.rksl");
            if (files.Length > 0)
            {
                Debug.Log($"[IsGameSceneLoader] Нашел файл в {d}: {files[0]}", this);
                StartCoroutine(LoadFromRksl(files[0]));
                return;
            }
        }
        // 4) дефолт из сцены
        if (manager.levelData != null)
        {
            Debug.Log($"[IsGameSceneLoader] Использую manager.levelData '{manager.levelData.fullTitle}' events={manager.levelData.events.Count}", this);
            ApplyLevel(manager.levelData);
            return;
        }
        Debug.LogWarning("[IsGameSceneLoader] Ничего не нашлось — пустой уровень, черный экран?", this);
        var def = new RhythmLevelData();
        def.fullTitle = "Default"; def.bpm = 128f;
        ApplyLevel(def);
    }

    void ApplyLevel(RhythmLevelData data)
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (manager == null) { Debug.LogError("[IsGameSceneLoader] manager null в ApplyLevel"); return; }
        // клонируем чтобы не мутировать LevelTransfer
        RhythmLevelData toApply = data;
        try { toApply = data.CloneDeep(); } catch { }
        manager.levelData = toApply;
        manager.PrepareLevel(toApply);
        if (toApply.music != null && manager.musicSource != null)
        {
            manager.musicSource.clip = toApply.music;
            manager.musicSource.Stop();
        }
        var mi = FindObjectOfType<MusicInfoUI>();
        if (mi != null) mi.Show(toApply);
        else Debug.LogWarning("[IsGameSceneLoader] MusicInfoUI не найден");

        // mise en place — трек должен быть виден даже без музыки
        Debug.Log($"[IsGameSceneLoader] Применен '{toApply.fullTitle}' bpm={toApply.bpm} events={toApply.events.Count} music={(toApply.music?toApply.music.name:"null")}", this);

        if (manager.autoPlayOnStart)
        {
            if (toApply.music != null) manager.Invoke(nameof(manager.Play), manager.autoPlayDelay);
            else Debug.LogWarning("[IsGameSceneLoader] autoPlayOnStart но music==null — Play не вызван, нажми Play вручную");
        }
    }

    IEnumerator LoadFromRksl(string rkslPath)
    {
        Debug.Log($"[IsGameSceneLoader] Extract {rkslPath}", this);
        string extractDir = Path.Combine(Application.temporaryCachePath, "RkslGame_" + Path.GetFileNameWithoutExtension(rkslPath));
        try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch {}
        if (!RkslFile.Extract(rkslPath, extractDir, out var man, out var audioPath, out var videoPath, out var coverPath))
        {
            Debug.LogError($"[IsGameSceneLoader] Extract failed {rkslPath}");
            yield break;
        }
        Debug.Log($"[IsGameSceneLoader] Manifest title='{man.title}' events={man.events.Count} audioPath='{audioPath}' exists={File.Exists(audioPath)}", this);
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            string url = "file://" + audioPath;
            AudioType type = GetAudioType(audioPath);
            Debug.Log($"[IsGameSceneLoader] Loading audio {url} type={type}", this);
            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    clip = DownloadHandlerAudioClip.GetContent(uwr);
                    if (clip != null) Debug.Log($"[IsGameSceneLoader] Audio loaded {clip.name} len={clip.length} ch={clip.channels} freq={clip.frequency}", this);
                    else Debug.LogError("[IsGameSceneLoader] clip==null после DownloadHandler");
                }
                else Debug.LogError($"[IsGameSceneLoader] Audio load failed {uwr.error} url={url}");
            }
        }
        else Debug.LogWarning($"[IsGameSceneLoader] audioPath пустой или не существует '{audioPath}'");

        Sprite cover = null;
        if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(coverPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes)) cover = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception e) { Debug.LogWarning($"[IsGameSceneLoader] cover load failed {e.Message}"); }
        }
        var data = RkslFile.ToRuntimeData(man, clip, null, cover);
        data.audioPath = audioPath;
        data.videoPath = videoPath;
        // сохраняем в трансфер для MusicInfo и т.д.
        LevelTransfer.levelData = data;
        LevelTransfer.rkslPath = rkslPath;
        LevelTransfer.levelName = man.title;
        PlayerPrefs.SetString("LastRkslPath", rkslPath);
        PlayerPrefs.Save();
        ApplyLevel(data);
    }

    AudioType GetAudioType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext) { case ".mp3": return AudioType.MPEG; case ".wav": return AudioType.WAV; case ".ogg": return AudioType.OGGVORBIS; case ".aiff": return AudioType.AIFF; case ".m4a": return AudioType.MPEG; default: return AudioType.UNKNOWN; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        string cur = SceneManager.GetActiveScene().name;
        if (cur != "IsGameScene") return;
        if (FindObjectOfType<IsGameSceneLoader>() != null) return;
        var manager = FindObjectOfType<RhythmParkourManager>();
        if (manager == null) { Debug.LogWarning("[IsGameSceneLoader] AutoCreate: manager не найден"); return; }
        var go = new GameObject("IsGameSceneLoader (Auto)");
        var comp = go.AddComponent<IsGameSceneLoader>();
        comp.manager = manager;
        Debug.Log("[IsGameSceneLoader] AutoCreated", go);
    }
}
