using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Единственный авторитетный загрузчик уровня для IsGameScene.
/// Приоритет: in-memory LevelTransfer -> SelectedLevelPath/LastRkslPath -> любой .rksl из FindAllRkslFiles -> дефолт.
/// </summary>
public class IsGameSceneLoader : MonoBehaviour
{
    public RhythmParkourManager manager;
    bool hasLoaded;

    void Awake()
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        Debug.Log($"[IsGameSceneLoader] Awake scene={SceneManager.GetActiveScene().name} manager={(manager?manager.name:"null")} hasLevel={LevelTransfer.hasLevel} rkslPath='{LevelTransfer.rkslPath}' Sel='{PlayerPrefs.GetString("SelectedLevelPath","")}'", this);
    }

    void Start()
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (manager == null) { Debug.LogError("[IsGameSceneLoader] manager не найден!"); return; }
        // Отключаем авто-PREPARE менеджера чтобы не было гонки: он подождёт нас
        // Менеджер сам в Start сделает Prepare если мы не вмешались, но мы вмешаемся раньше
        LoadLevel();
    }

    void LoadLevel()
    {
        if (hasLoaded) return;
        // 1) in-memory из редактора/меню (приоритет)
        if (LevelTransfer.levelData != null && (LevelTransfer.levelData.events.Count > 0 || LevelTransfer.levelData.music != null))
        {
            Debug.Log($"[IsGameSceneLoader] Использую LevelTransfer in-memory '{LevelTransfer.levelName}' events={LevelTransfer.levelData.events.Count}", this);
            ApplyLevel(LevelTransfer.levelData);
            hasLoaded = true;
            return;
        }
        // 2) путь из LevelTransfer/PlayerPrefs
        string path = LevelTransfer.GetEffectivePath();
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            Debug.Log($"[IsGameSceneLoader] Гружу rksl '{path}'", this);
            StartCoroutine(LoadFromRksl(path));
            hasLoaded = true;
            return;
        }
        // 3) любой .rksl из всех известных папок (централизовано)
        var all = RkslFile.FindAllRkslFiles();
        if (all.Count > 0)
        {
            Debug.Log($"[IsGameSceneLoader] Нашел {all.Count} rksl, беру первый: {all[0]}", this);
            StartCoroutine(LoadFromRksl(all[0]));
            hasLoaded = true;
            return;
        }
        // 4) пробуем собрать временный уровень из mp3 в Assets/Levels (для первого запуска без .rksl)
        string fallbackMp3 = TryFindFirstMp3();
        if (!string.IsNullOrEmpty(fallbackMp3) && File.Exists(fallbackMp3))
        {
            Debug.Log($"[IsGameSceneLoader] Нет .rksl, пробую создать уровень из mp3 '{fallbackMp3}'", this);
            StartCoroutine(LoadFromMp3Fallback(fallbackMp3));
            hasLoaded = true;
            return;
        }
        // 5) дефолт из менеджера или пустой
        if (manager.levelData != null && manager.levelData.music != null)
        {
            Debug.Log($"[IsGameSceneLoader] Использую manager.levelData '{manager.levelData.fullTitle}'", this);
            ApplyLevel(manager.levelData);
            hasLoaded = true;
            return;
        }
        Debug.LogWarning("[IsGameSceneLoader] Ничего не нашлось — создаю пустой дефолт", this);
        var def = new RhythmLevelData { fullTitle = "Default", bpm = 128f };
        ApplyLevel(def);
        hasLoaded = true;
    }

    string TryFindFirstMp3()
    {
        string[] dirs = new string[] {
            Path.Combine(Application.dataPath, "!Rhythm Parkour/Levels"),
            Path.Combine(Application.streamingAssetsPath, "GameLevels"),
            Path.Combine(Application.persistentDataPath, "Levels")
        };
        foreach (var d in dirs)
        {
            if (!Directory.Exists(d)) continue;
            try
            {
                var mp3s = Directory.GetFiles(d, "*.mp3", SearchOption.AllDirectories);
                if (mp3s.Length > 0) return mp3s[0];
                var oggs = Directory.GetFiles(d, "*.ogg", SearchOption.AllDirectories);
                if (oggs.Length > 0) return oggs[0];
                var wavs = Directory.GetFiles(d, "*.wav", SearchOption.AllDirectories);
                if (wavs.Length > 0) return wavs[0];
            }
            catch {}
        }
        return null;
    }

    IEnumerator LoadFromMp3Fallback(string mp3Path)
    {
        string dir = Path.GetDirectoryName(mp3Path);
        string name = Path.GetFileNameWithoutExtension(mp3Path);
        // пробуем найти cover рядом
        string coverPath = null;
        if (dir != null)
        {
            foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg" })
            {
                try
                {
                    var covers = Directory.GetFiles(dir, ext);
                    if (covers.Length > 0) { coverPath = covers[0]; break; }
                }
                catch {}
            }
        }
        // грузим аудио
        AudioClip clip = null;
        string url = RkslFile.GetFileUri(mp3Path);
        AudioType type = RkslFile.GetAudioType(mp3Path);
        Debug.Log($"[IsGameSceneLoader] Loading fallback audio {url}", this);
        using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, type))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityWebRequest.Result.Success)
                clip = DownloadHandlerAudioClip.GetContent(uwr);
            else Debug.LogError($"[IsGameSceneLoader] Fallback audio failed {uwr.error}");
        }
        if (clip == null) { Debug.LogError("[IsGameSceneLoader] Fallback clip null"); yield break; }
        Sprite cover = null;
        if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(coverPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes)) cover = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch {}
        }
        var data = new RhythmLevelData
        {
            fullTitle = name,
            songAuthor = "",
            mapAuthor = "Auto",
            bpm = 128f,
            offset = 0f,
            music = clip,
            cover = cover,
            audioPath = mp3Path,
            events = new System.Collections.Generic.List<ObstacleEvent>()
        };
        // детект BPM
        try
        {
            var det = BpmDetector.Detect(clip);
            data.bpm = Mathf.Clamp(det.bpm, 70f, 200f);
            Debug.Log($"[IsGameSceneLoader] Fallback BPM {data.bpm:0} conf {det.confidence:0.##}");
        }
        catch {}
        // авто-генерация если нет нот
        if (data.events.Count == 0)
        {
            var cfg = RhythmAutoGenerator.FlexibleSettings.Default;
            RhythmAutoGenerator.Generate(data, 0, cfg);
        }
        LevelTransfer.levelData = data.CloneDeep();
        ApplyLevel(data);
    }

    void ApplyLevel(RhythmLevelData data)
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (manager == null) { Debug.LogError("[IsGameSceneLoader] manager null в ApplyLevel"); return; }
        RhythmLevelData toApply = data;
        try { toApply = data.CloneDeep(); } catch { }
        // Не даём менеджеру затереть — отключаем его авто-Prepare если он ещё не сделал
        manager.levelData = toApply;
        manager.PrepareLevel(toApply);
        if (toApply.music != null && manager.musicSource != null)
        {
            manager.musicSource.clip = toApply.music;
            manager.musicSource.Stop();
        }
        var mi = FindObjectOfType<MusicInfoUI>();
        if (mi != null) mi.Show(toApply);

        Debug.Log($"[IsGameSceneLoader] Применен '{toApply.fullTitle}' bpm={toApply.bpm} events={toApply.events.Count} music={(toApply.music?toApply.music.name:"null")}", this);

        if (manager.autoPlayOnStart)
        {
            if (toApply.music != null) manager.Invoke(nameof(manager.Play), manager.autoPlayDelay);
            else Debug.LogWarning("[IsGameSceneLoader] autoPlay но music==null — Play не вызван");
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
        Debug.Log($"[IsGameSceneLoader] Manifest title='{man.title}' events={man.events.Count} audioPath='{audioPath}'", this);
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            string url = RkslFile.GetFileUri(audioPath);
            AudioType type = RkslFile.GetAudioType(audioPath);
            Debug.Log($"[IsGameSceneLoader] Loading audio {url} type={type}", this);
            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    clip = DownloadHandlerAudioClip.GetContent(uwr);
                    if (clip != null) Debug.Log($"[IsGameSceneLoader] Audio loaded {clip.name} len={clip.length}", this);
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
        // Если .rksl пустой (0 нот) но есть музыка — авто-генерируем чтобы не было пустого уровня
        if ((data.events == null || data.events.Count == 0) && clip != null)
        {
            Debug.Log($"[IsGameSceneLoader] rksl '{man.title}' пустой, генерирую ноты автоматом", this);
            // пробуем детект BPM
            try
            {
                var det = BpmDetector.Detect(clip);
                data.bpm = Mathf.Clamp(det.bpm, 70f, 200f);
                data.offset = det.offset;
                Debug.Log($"[IsGameSceneLoader] Auto BPM {data.bpm:0} offset {data.offset:0.##} conf {det.confidence:0.##}");
            }
            catch {}
            var cfg = RhythmAutoGenerator.FlexibleSettings.Default;
            RhythmAutoGenerator.Generate(data, 0, cfg);
        }
        LevelTransfer.levelData = data.CloneDeep();
        LevelTransfer.rkslPath = rkslPath;
        LevelTransfer.levelName = man.title;
        PlayerPrefs.SetString("SelectedLevelPath", rkslPath);
        PlayerPrefs.SetString("LastRkslPath", rkslPath);
        PlayerPrefs.Save();
        ApplyLevel(data);
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
