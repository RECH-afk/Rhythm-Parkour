using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Загрузчик .rksl в игре (IsGameScene)
/// Позволяет закинуть .rksl файл в папку Levels и играть.
/// Также поддерживает drag-and-drop и кнопку загрузки.
/// </summary>
public class RkslGameLoader : MonoBehaviour
{
    public RhythmParkourManager manager;
    public AudioSource musicSource;
    public Button loadButton;
    public TextMeshProUGUI statusText;
    public string levelsFolder = "Levels"; // относительно persistentDataPath

    void Awake()
    {
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (musicSource == null && manager != null) musicSource = manager.musicSource;
        if (loadButton != null) loadButton.onClick.AddListener(OpenFileDialog);
    }

    void Start()
    {
        // приоритет — трансфер из меню/редактора (чтобы не перетирать)
        if (LevelTransfer.hasLevel && LevelTransfer.levelData != null)
        {
            Debug.Log($"[RkslGameLoader] Пропуск авто-загрузки — есть LevelTransfer '{LevelTransfer.levelName}'");
            return;
        }
        // пробуем загрузить последний сохранённый rksl
        string last = PlayerPrefs.GetString("LastRkslPath", "");
        if (!string.IsNullOrEmpty(last) && File.Exists(last))
        {
            StartCoroutine(LoadRksl(last));
            return;
        }
        // иначе ищем любой .rksl в persistentDataPath/Levels
        string dir = Path.Combine(Application.persistentDataPath, levelsFolder);
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.rksl");
            if (files.Length > 0) StartCoroutine(LoadRksl(files[0]));
        }
        // также проверяем StreamingAssets/GameLevels
        string streamDir = Path.Combine(Application.streamingAssetsPath, "GameLevels");
        if (Directory.Exists(streamDir))
        {
            var files = Directory.GetFiles(streamDir, "*.rksl");
            if (files.Length > 0) StartCoroutine(LoadRksl(files[0]));
        }
    }

    void Update()
    {
        // поддержка перетаскивания файла на окно (в Editor и Standalone с SFB)
        // Для простоты проверяем если в папке появился новый файл
    }

    public void OpenFileDialog()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Открыть .rksl", "", "rksl");
        if (!string.IsNullOrEmpty(path)) StartCoroutine(LoadRksl(path));
#else
        // в билде требуем StandaloneFileBrowser
        UpdateStatus("Перетащите .rksl в папку " + Path.Combine(Application.persistentDataPath, levelsFolder));
        string dir = Path.Combine(Application.persistentDataPath, levelsFolder);
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.rksl");
            if (files.Length > 0) StartCoroutine(LoadRksl(files[0]));
        }
#endif
    }

    public IEnumerator LoadRksl(string rkslPath)
    {
        UpdateStatus($"Загрузка {Path.GetFileName(rkslPath)}...");
        string extractDir = Path.Combine(Application.temporaryCachePath, "RkslGame_" + Path.GetFileNameWithoutExtension(rkslPath));
        if (!RkslFile.Extract(rkslPath, extractDir, out var manifest, out var audioPath, out var videoPath, out var coverPath))
        {
            UpdateStatus("Ошибка .rksl");
            yield break;
        }
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            string url = "file://" + audioPath;
            AudioType type = GetAudioType(audioPath);
            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(uwr);
                else { UpdateStatus($"Аудио ошибка: {uwr.error}"); yield break; }
            }
        }
        if (clip == null) { UpdateStatus("Нет аудио в .rksl"); yield break; }

        Sprite cover = null;
        if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
        {
            byte[] bytes = File.ReadAllBytes(coverPath);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes)) cover = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        var data = RkslFile.ToRuntimeData(manifest, clip, null, cover);
        data.audioPath = audioPath;
        data.videoPath = videoPath;

        if (manager != null)
        {
            manager.levelData = data;
            manager.PrepareLevel(data);
            UpdateStatus($"Уровень {manifest.title} загружен ({manifest.events.Count} нот) — Нажмите Play");
            // авто-старт если нужно
            // manager.Play();
        }
        PlayerPrefs.SetString("LastRkslPath", rkslPath);
        PlayerPrefs.Save();
    }

    AudioType GetAudioType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext) { case ".mp3": return AudioType.MPEG; case ".wav": return AudioType.WAV; case ".ogg": return AudioType.OGGVORBIS; default: return AudioType.UNKNOWN; }
    }

    void UpdateStatus(string msg) { if (statusText != null) statusText.text = msg; Debug.Log($"[RkslGame] {msg}"); }
}
