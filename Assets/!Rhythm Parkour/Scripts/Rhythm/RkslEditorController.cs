using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Контроллер редактора для работы с .rksl файлами
/// Привязывается к SettingsWindow или к TimelineUI
/// Собирает метаданные из InputFields, медиа из FileLoader, ноты из TimelineUI.levelData
/// Сохраняет/загружает единый .rksl (zip) файл
/// </summary>
public class RkslEditorController : MonoBehaviour
{
    [Header("Ссылки UI Метаданные")]
    public TMP_InputField titleInput;   // TrackNameInputField
    public TMP_InputField artistInput;  // TrackArtistInputField
    public TMP_InputField creatorInput; // LevelCreatorInputField

    [Header("Загрузчики файлов")]
    public FileLoader audioLoader; // SelectSongFileButton
    public FileLoader videoLoader; // SelectVideoFileButton
    public FileLoader coverLoader; // SelectCoverFileButton

    [Header("Таймлайн и данные")]
    public TimelineUI timelineUI;
    [HideInInspector] public RhythmLevelData levelData; // рантайм — создаётся автоматически, не ассет

    [Header("Кнопка Сохранения — только SAVE")]
    public Button saveRkslButton;

    [Header("Превью")]
    public Image coverPreviewImage;
    public TextMeshProUGUI statusText;

    string currentAudioPath;
    string currentVideoPath;
    string currentCoverPath;
    AudioClip currentAudioClip;
    Sprite currentCoverSprite;

    void Awake()
    {
        // приоритет — трансфер из меню (DontDestroyOnLoad)
        if (LevelTransfer.hasLevel && LevelTransfer.levelData != null)
        {
            levelData = LevelTransfer.levelData;
            if (timelineUI != null) timelineUI.levelData = levelData;
            var man = FindObjectOfType<RhythmParkourManager>();
            if (man != null) man.levelData = levelData;
        }
        if (timelineUI == null) timelineUI = FindObjectOfType<TimelineUI>();
        if (levelData == null && timelineUI != null) levelData = timelineUI.levelData;
        if (levelData == null) levelData = FindObjectOfType<RhythmParkourManager>()?.levelData;
        if (levelData == null && LevelTransfer.hasLevel) levelData = LevelTransfer.levelData;

        // авто-поиск если не назначено
        if (titleInput == null) titleInput = GameObject.Find("TrackNameInputField")?.GetComponent<TMP_InputField>();
        if (artistInput == null) artistInput = GameObject.Find("TrackArtistInputField")?.GetComponent<TMP_InputField>();
        if (creatorInput == null) creatorInput = GameObject.Find("LevelCreatorInputField")?.GetComponent<TMP_InputField>();
        if (audioLoader == null) audioLoader = GameObject.Find("SelectSongFileButton")?.GetComponent<FileLoader>();
        if (videoLoader == null) videoLoader = GameObject.Find("SelectVideoFileButton")?.GetComponent<FileLoader>();
        if (coverLoader == null) coverLoader = GameObject.Find("SelectCoverFileButton")?.GetComponent<FileLoader>();
        if (coverPreviewImage == null) coverPreviewImage = GameObject.Find("CoverImage")?.GetComponent<Image>();

        if (saveRkslButton == null)
        {
            var go = GameObject.Find("SaveRkslButton");
            if (go) saveRkslButton = go.GetComponent<Button>();
        }
    }

    void Start()
    {
        // подхватить трансфер если пришли из меню
        if (LevelTransfer.hasLevel && LevelTransfer.levelData != null)
        {
            levelData = LevelTransfer.levelData;
            if (timelineUI != null) timelineUI.levelData = levelData;
            var manTmp = FindObjectOfType<RhythmParkourManager>();
            if (manTmp != null) manTmp.levelData = levelData;
        }
        if (audioLoader != null) audioLoader.onFileLoaded.AddListener(OnAudioLoaded);
        if (videoLoader != null) videoLoader.onFileLoaded.AddListener((p, c) => { currentVideoPath = p; UpdateStatus($"Видео: {Path.GetFileName(p)}"); });
        if (coverLoader != null) coverLoader.onFileLoaded.AddListener((p, c) => { currentCoverPath = p; UpdateStatus($"Обложка: {Path.GetFileName(p)}"); LoadCoverPreview(p); });

        EnsureButtons();

        if (saveRkslButton != null) { saveRkslButton.onClick.RemoveAllListeners(); saveRkslButton.onClick.AddListener(SaveRksl); }

        if (levelData != null) PopulateUIFromData();
        else if (timelineUI != null && timelineUI.levelData != null) { levelData = timelineUI.levelData; PopulateUIFromData(); }
        else if (LevelTransfer.hasLevel) { levelData = LevelTransfer.levelData; PopulateUIFromData(); }
        UpdateStatus("Готов — загрузите аудио и создавайте уровень (SAVE .RKSL)");
        // если есть трансфер — сразу обновить таймлайн
        if (LevelTransfer.hasLevel && timelineUI != null) { timelineUI.levelData = LevelTransfer.levelData; timelineUI.RefreshAll(); }
    }

    void EnsureButtons()
    {
        if (saveRkslButton != null) return;
        Transform parent = null;
        if (timelineUI != null && timelineUI.transform.parent != null) parent = timelineUI.transform.parent;
        if (parent == null)
        {
            var go = GameObject.Find("SettingsWindow");
            if (go) parent = go.transform;
        }
        if (parent == null) parent = transform;
        saveRkslButton = CreateButton(parent, "SaveRkslButton", "SAVE .RKSL", new Vector2(0, -520), new Vector2(220, 48), new Color(0.2f, 0.7f, 0.3f));
    }

    Button CreateButton(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.color = col; img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>(); tmp.text = text; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.raycastTarget = false;
        return btn;
    }

    void OnDestroy()
    {
        if (audioLoader != null) audioLoader.onFileLoaded.RemoveListener(OnAudioLoaded);
    }

    void OnAudioLoaded(string path, AudioClip clip)
    {
        currentAudioPath = path;
        currentAudioClip = clip;
        if (timelineUI != null && timelineUI.levelData != null)
        {
            // фиксим баг: при создании нового уровня после загрузки музыки не должно быть старых нот
            // если это первый аудиофайл для нового уровня — чистим
            if (timelineUI.levelData.events.Count > 0 && string.IsNullOrEmpty(timelineUI.levelData.fullTitle) && timelineUI.levelData.music != clip)
            {
                // считаем что это новый уровень — чистим
                timelineUI.levelData.events.Clear();
                timelineUI.RefreshNotes();
            }
        }
        UpdateStatus($"Аудио: {Path.GetFileName(path)} {clip.length:0.0}с");
    }

    void LoadCoverPreview(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        StartCoroutine(LoadCoverCoroutine(path));
    }
    IEnumerator LoadCoverCoroutine(string path)
    {
        using (var uwr = UnityWebRequestTexture.GetTexture("file://" + path))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(uwr);
                var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                currentCoverSprite = spr;
                if (coverPreviewImage != null) coverPreviewImage.sprite = spr;
                if (levelData != null) levelData.cover = spr;
            }
        }
    }

    void PopulateUIFromData()
    {
        if (levelData == null) return;
        if (titleInput != null) titleInput.text = levelData.fullTitle;
        if (artistInput != null) artistInput.text = levelData.songAuthor;
        if (creatorInput != null) creatorInput.text = levelData.mapAuthor;
        if (coverPreviewImage != null && levelData.cover != null) coverPreviewImage.sprite = levelData.cover;
    }

    public void CreateNewLevel()
    {
        if (levelData == null) levelData = new RhythmLevelData();
        levelData.fullTitle = "";
        levelData.songAuthor = "";
        levelData.mapAuthor = "";
        levelData.events.Clear();
        levelData.music = null;
        levelData.video = null;
        levelData.cover = null;
        levelData.audioPath = "";
        levelData.videoPath = "";
        levelData.bpm = 128f;
        levelData.offset = 0f;
        currentAudioPath = null; currentVideoPath = null; currentCoverPath = null; currentAudioClip = null; currentCoverSprite = null;
        if (titleInput != null) titleInput.text = "";
        if (artistInput != null) artistInput.text = "";
        if (creatorInput != null) creatorInput.text = "";
        if (coverPreviewImage != null) coverPreviewImage.sprite = null;
        if (timelineUI != null)
        {
            timelineUI.levelData = levelData;
            timelineUI.RefreshAll();
        }
        UpdateStatus("Новый уровень — загрузите аудио");
    }

    public void SaveRksl()
    {
        // всегда берем актуальные данные с таймлайна (там ноты)
        if (timelineUI != null && timelineUI.levelData != null) levelData = timelineUI.levelData;
        if (levelData == null && timelineUI != null) levelData = timelineUI.levelData;
        if (levelData == null) { UpdateStatus("Нет данных уровня"); return; }
        // синхронизируем обратно чтобы таймлайн видел метаданные
        if (timelineUI != null && timelineUI.levelData != levelData) timelineUI.levelData = levelData;

        // собрать метаданные из UI
        if (titleInput != null) levelData.fullTitle = titleInput.text;
        if (artistInput != null) levelData.songAuthor = artistInput.text;
        if (creatorInput != null) levelData.mapAuthor = creatorInput.text;

        if (string.IsNullOrEmpty(levelData.fullTitle))
        {
            UpdateStatus("Введите название трека!");
            return;
        }
        if (levelData.music == null && currentAudioClip != null) levelData.music = currentAudioClip;
        if (levelData.music == null)
        {
            UpdateStatus("Загрузите аудио!");
            return;
        }
        // актуальные пути из лоадеров (на случай если событие не пришло)
        if (string.IsNullOrEmpty(currentAudioPath) && audioLoader != null) currentAudioPath = audioLoader.CurrentPath;
        if (string.IsNullOrEmpty(currentVideoPath) && videoLoader != null) currentVideoPath = videoLoader.CurrentPath;
        if (string.IsNullOrEmpty(currentCoverPath) && coverLoader != null) currentCoverPath = coverLoader.CurrentPath;
        if (string.IsNullOrEmpty(levelData.audioPath)) levelData.audioPath = currentAudioPath;
        if (string.IsNullOrEmpty(levelData.videoPath)) levelData.videoPath = currentVideoPath;

        string defaultName = string.IsNullOrEmpty(levelData.fullTitle) ? "NewLevel" : levelData.fullTitle;
        defaultName = string.Join("_", defaultName.Split(Path.GetInvalidFileNameChars()));

#if UNITY_EDITOR
        string path = EditorUtility.SaveFilePanel("Сохранить .rksl", "", defaultName + ".rksl", "rksl");
        if (string.IsNullOrEmpty(path)) return;
#else
        string dir = Path.Combine(Application.persistentDataPath, "Levels");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, defaultName + ".rksl");
#endif
        var manifest = RkslFile.FromRuntimeData(levelData);
        manifest.title = levelData.fullTitle;
        manifest.artist = levelData.songAuthor;
        manifest.creator = levelData.mapAuthor;

        string audioSrc = !string.IsNullOrEmpty(currentAudioPath) ? currentAudioPath : levelData.audioPath;
        string videoSrc = !string.IsNullOrEmpty(currentVideoPath) ? currentVideoPath : levelData.videoPath;
        string coverSrc = !string.IsNullOrEmpty(currentCoverPath) ? currentCoverPath : null;
        bool ok = RkslFile.Save(path, manifest, audioSrc, videoSrc, coverSrc, currentCoverSprite ?? levelData.cover);
        if (ok) UpdateStatus($"Сохранено: {Path.GetFileName(path)}");
        else UpdateStatus("Ошибка сохранения");
    }

    public void LoadRkslDialog()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Открыть .rksl", "", "rksl");
        if (string.IsNullOrEmpty(path)) return;
        StartCoroutine(LoadRkslCoroutine(path));
#else
        UpdateStatus("В билде перетащите .rksl файл на окно");
#endif
    }

    public void LoadRkslFromPath(string rkslPath)
    {
        StartCoroutine(LoadRkslCoroutine(rkslPath));
    }

    IEnumerator LoadRkslCoroutine(string rkslPath)
    {
        if (!File.Exists(rkslPath)) { UpdateStatus("Файл не найден"); yield break; }
        string extractDir = Path.Combine(Application.temporaryCachePath, "RkslExtract_" + Path.GetFileNameWithoutExtension(rkslPath));
        if (!RkslFile.Extract(rkslPath, extractDir, out var manifest, out var audioPath, out var videoPath, out var coverPath))
        {
            UpdateStatus("Ошибка распаковки .rksl");
            yield break;
        }

        // загружаем аудио
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            string url = "file://" + audioPath;
            AudioType type = GetAudioType(audioPath);
            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(uwr);
                else UpdateStatus($"Ошибка аудио: {uwr.error}");
            }
        }
        // cover
        Sprite coverSpr = null;
        if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
        {
            byte[] bytes = File.ReadAllBytes(coverPath);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                coverSpr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        // video не грузим как VideoClip (нужен VideoPlayer.url) — сохраним путь
        // создаём runtime data
        var data = RkslFile.ToRuntimeData(manifest, clip, null, coverSpr);
        data.audioPath = audioPath;
        data.videoPath = videoPath;

        // применяем к редактору
        levelData = data;
        if (timelineUI != null) timelineUI.levelData = data;
        var manager = FindObjectOfType<RhythmParkourManager>();
        if (manager != null) manager.levelData = data;

        if (titleInput != null) titleInput.text = manifest.title;
        if (artistInput != null) artistInput.text = manifest.artist;
        if (creatorInput != null) creatorInput.text = manifest.creator;
        if (coverPreviewImage != null && coverSpr != null) coverPreviewImage.sprite = coverSpr;

        currentAudioPath = audioPath; currentVideoPath = videoPath; currentCoverPath = coverPath;
        currentAudioClip = clip; currentCoverSprite = coverSpr;

        if (timelineUI != null)
        {
            timelineUI.RefreshAll();
            timelineUI.Seek(0);
        }
        UpdateStatus($"Загружен: {manifest.title} ({manifest.events.Count} нот)");
        // сохраняем путь для игры
        PlayerPrefs.SetString("LastRkslPath", rkslPath);
        PlayerPrefs.Save();
    }

    AudioType GetAudioType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext) { case ".mp3": return AudioType.MPEG; case ".wav": return AudioType.WAV; case ".ogg": return AudioType.OGGVORBIS; default: return AudioType.UNKNOWN; }
    }

    void UpdateStatus(string msg) { if (statusText != null) statusText.text = msg; Debug.Log($"[RkslEditor] {msg}"); }
}
