using System.IO;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using Zenject;

#if UNITY_EDITOR
using UnityEditor;
#endif
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.Core.Storage;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public class RkslEditorController : RKSBehaviour
    {
        [Header("Metadata UI")]
        public TMP_InputField titleInput;
        public TMP_InputField artistInput;
        public TMP_InputField creatorInput;

        [Header("File Loaders")]
        public FileLoader audioLoader;
        public FileLoader videoLoader;
        public FileLoader coverLoader;

        [Header("Timeline & Data")]
        [HideInInspector]
        [InjectOptional] public TimelineUI timelineUI;
        [HideInInspector] public RhythmLevelData levelData;

        [Header("Scene Preview")]
        [HideInInspector]
        [InjectOptional] public RhythmParkourManager previewManager;
        [HideInInspector]
        [InjectOptional] public LevelTransfer transfer;
        [HideInInspector]
        [InjectOptional] public LevelVisualApplier visual;
        [HideInInspector]
        [InjectOptional] public IRkslStore rksl;

        private IRkslStore Store => rksl ?? RkslStore.Shared;
        [HideInInspector]
        [InjectOptional] public UnityEngine.Video.VideoPlayer previewVideo;
        [HideInInspector]
        [InjectOptional] public LevelEditorVisualSettings visualSettings;

        [Header("Preview")]
        public Image coverPreviewImage;
        public TextMeshProUGUI statusText;

        [Header("Test Play")]
        public string gameSceneName = "IsGameScene";

        [Header("Exit")]
        public GameObject exitConfirmWindow;
        public string menuSceneName = "IsMenuScene";

        Button testPlayButton;
        Button saveButton;
        Button exitButton;
        int savedFingerprint;
        bool hasSavedState;

        string currentAudioPath;
        string currentVideoPath;
        string currentCoverPath;
        AudioClip currentAudioClip;
        Sprite currentCoverSprite;

        protected override void OnInjected()
        {

            if (transfer != null && transfer.hasLevel && transfer.levelData != null)
            {
                levelData = transfer.levelData;
                if (timelineUI != null) timelineUI.levelData = levelData;
                if (previewManager != null) previewManager.levelData = levelData;
            }
            if (levelData == null && timelineUI != null) levelData = timelineUI.levelData;
            if (levelData == null && previewManager != null) levelData = previewManager.levelData;
            if (levelData == null && transfer != null && transfer.hasLevel && transfer.levelData != null) levelData = transfer.levelData;

if (titleInput == null || artistInput == null || creatorInput == null)
                Debug.LogWarning("[RkslEditor] InputFields не назначены — задай в инспекторе.", this);
            if (audioLoader == null || videoLoader == null || coverLoader == null)
                Debug.LogWarning("[RkslEditor] FileLoaders не назначены — задай в инспекторе.", this);
            ValidateLoader(audioLoader, AllowedFileTypes.Audio, nameof(audioLoader));
            ValidateLoader(videoLoader, AllowedFileTypes.Video, nameof(videoLoader));
            ValidateLoader(coverLoader, AllowedFileTypes.Photo, nameof(coverLoader));
        }

        protected override void OnReady()
        {

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (transfer != null && transfer.hasLevel && transfer.levelData != null)
            {
                levelData = transfer.levelData;
                if (timelineUI != null) timelineUI.levelData = levelData;
                if (previewManager != null) previewManager.levelData = levelData;
            }
            if (audioLoader != null) audioLoader.onFileLoaded.AddListener(OnAudioLoaded);
            if (videoLoader != null) videoLoader.onFileLoaded.AddListener((p, c) => { currentVideoPath = p; UpdateStatus($"Видео: {Path.GetFileName(p)}"); if (levelData != null) levelData.videoPath = p; PreviewVideo(p); });
            if (coverLoader != null) coverLoader.onFileLoaded.AddListener((p, c) => { currentCoverPath = p; UpdateStatus($"Обложка: {Path.GetFileName(p)}"); LoadCoverPreview(p); });
            SubscribeRkslLoaders();

            EnsureVisualSettings();
            if (exitConfirmWindow != null) exitConfirmWindow.SetActive(false);
            RestorePathsFromData();

            if (levelData != null) PopulateUIFromData();
            else if (timelineUI != null && timelineUI.levelData != null) { levelData = timelineUI.levelData; PopulateUIFromData(); }
            else if (transfer != null && transfer.hasLevel && transfer.levelData != null) { levelData = transfer.levelData; PopulateUIFromData(); }
            else if (levelData == null) { levelData = new RhythmLevelData(); if (timelineUI != null) timelineUI.levelData = levelData; }
            UpdateStatus("Готов — загрузите аудио и создавайте уровень (SAVE .RKSL)");

            if (transfer != null && transfer.hasLevel && transfer.levelData != null && timelineUI != null) { timelineUI.levelData = transfer.levelData; timelineUI.RefreshAll(); }
            RestoreTimelineAudio();

            if (levelData != null && visual != null) visual.Apply(levelData, previewVideo, true);
            CaptureSavedState();

            if (titleInput != null) titleInput.onValueChanged.AddListener(OnMetadataInputChanged);
            if (artistInput != null) artistInput.onValueChanged.AddListener(OnMetadataInputChanged);
            if (creatorInput != null) creatorInput.onValueChanged.AddListener(OnMetadataInputChanged);
            if (timelineUI != null && timelineUI.fileLoader != null) timelineUI.fileLoader.onFileLoaded.AddListener(OnTimelineFileLoaded);
            ResolveButtons();
        }

        void SubscribeRkslLoaders()
        {
            foreach (var fl in UnityEngine.Object.FindObjectsByType<FileLoader>(UnityEngine.FindObjectsSortMode.None))
            {
                if (fl == null) continue;
                if ((fl.AllowedTypes & AllowedFileTypes.Rksl) == 0) continue;
                fl.onRkslLoaded.RemoveListener(LoadRkslFromPath);
                fl.onRkslLoaded.AddListener(LoadRkslFromPath);
            }
        }

        void UnsubscribeRkslLoaders()
        {
            foreach (var fl in UnityEngine.Object.FindObjectsByType<FileLoader>(UnityEngine.FindObjectsSortMode.None))
            {
                if (fl == null) continue;
                fl.onRkslLoaded.RemoveListener(LoadRkslFromPath);
            }
        }

        void ValidateLoader(FileLoader loader, AllowedFileTypes expected, string field)
        {
            if (loader != null && (loader.AllowedTypes & expected) == 0)
                Debug.LogWarning($"[RkslEditor] {field} не разрешает {expected} — проверь Allowed Types.", this);
        }

        void EnsureVisualSettings()
        {

            if (visualSettings == null) Debug.LogWarning("[RkslEditor] LevelEditorVisualSettings не забинден — добавь в EditorInstaller.", this);
        }

        void ResolveButtons()
        {
            if (testPlayButton == null)
            {
                var go = GameObject.Find("ButtonTest");
                if (go != null) testPlayButton = go.GetComponent<Button>();
            }
            if (saveButton == null)
            {
                var go = GameObject.Find("ButtonSave");
                if (go != null) saveButton = go.GetComponent<Button>();
            }
            if (exitButton == null)
            {
                var go = GameObject.Find("ButtonExit");
                if (go != null) exitButton = go.GetComponent<Button>();
            }
            if (testPlayButton != null && testPlayButton.onClick.GetPersistentEventCount() == 0)
            {
                testPlayButton.onClick.RemoveListener(TestPlayLevel);
                testPlayButton.onClick.AddListener(TestPlayLevel);
            }
            if (exitButton != null && exitButton.onClick.GetPersistentEventCount() == 0)
            {
                exitButton.onClick.RemoveListener(RequestExit);
                exitButton.onClick.AddListener(RequestExit);
            }
            RefreshButtonStates();
        }

        void OnMetadataInputChanged(string _) => RefreshButtonStates();

        void OnTimelineFileLoaded(string _p, AudioClip _c) => RefreshButtonStates();

        void RefreshButtonStates()
        {
            if (timelineUI != null && timelineUI.levelData != null && levelData == null) levelData = timelineUI.levelData;
            string title = titleInput != null ? titleInput.text : (levelData != null ? levelData.fullTitle : "");
            var music = levelData != null ? levelData.music : null;
            if (music == null) music = currentAudioClip;
            bool ready = !string.IsNullOrEmpty(title) && music != null;
            if (testPlayButton != null) testPlayButton.interactable = ready;
            if (saveButton != null) saveButton.interactable = ready;
        }

        public void RequestExit()
        {
            SyncLevelFromUI();
            if (IsLevelEmpty() || !HasUnsavedChanges()) ExitNow();
            else if (exitConfirmWindow != null) exitConfirmWindow.SetActive(true);
            else ExitNow();
        }

        public void ConfirmExit()
        {
            ExitNow();
        }

        public void CancelExit()
        {
            if (exitConfirmWindow != null) exitConfirmWindow.SetActive(false);
        }

        void ExitNow()
        {
            if (exitConfirmWindow != null) exitConfirmWindow.SetActive(false);
            if (Transition != null) Transition.LoadScene(menuSceneName);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(menuSceneName);
        }

        void SyncLevelFromUI()
        {
            if (timelineUI != null && timelineUI.levelData != null) levelData = timelineUI.levelData;
            if (levelData == null) return;
            if (titleInput != null) levelData.fullTitle = titleInput.text;
            if (artistInput != null) levelData.songAuthor = artistInput.text;
            if (creatorInput != null) levelData.mapAuthor = creatorInput.text;
            if (levelData.music == null && currentAudioClip != null) levelData.music = currentAudioClip;
        }

        bool HasRequiredMetadata()
        {
            SyncLevelFromUI();
            if (levelData == null) return false;
            return !string.IsNullOrEmpty(levelData.fullTitle) && levelData.music != null;
        }

        bool IsLevelEmpty()
        {
            if (levelData == null) return true;
            return levelData.events.Count == 0 && levelData.music == null && currentAudioClip == null;
        }

        int ComputeLevelFingerprint()
        {
            unchecked
            {
                int h = 17;
                if (levelData == null) return h;
                h = h * 31 + (levelData.fullTitle ?? "").GetHashCode();
                h = h * 31 + (levelData.songAuthor ?? "").GetHashCode();
                h = h * 31 + (levelData.mapAuthor ?? "").GetHashCode();
                h = h * 31 + (levelData.audioPath ?? "").GetHashCode();
                h = h * 31 + (levelData.videoPath ?? "").GetHashCode();
                h = h * 31 + (levelData.music != null ? levelData.music.name.GetHashCode() : 0);
                h = h * 31 + levelData.events.Count;
                foreach (var e in levelData.events)
                {
                    h = h * 31 + e.time.GetHashCode();
                    h = h * 31 + e.beat.GetHashCode();
                    h = h * 31 + e.prefabIndex;
                    h = h * 31 + e.speed.GetHashCode();
                    h = h * 31 + e.triggerType + (e.isTrigger ? 100000 : 0) + e.triggerPrefabIndex * 10;
                }
                return h;
            }
        }

        void CaptureSavedState()
        {
            SyncLevelFromUI();
            savedFingerprint = ComputeLevelFingerprint();
            hasSavedState = true;
        }

        bool HasUnsavedChanges()
        {
            if (!hasSavedState) return !IsLevelEmpty();
            SyncLevelFromUI();
            return ComputeLevelFingerprint() != savedFingerprint;
        }



        void RestorePathsFromData()
        {
            if (levelData == null) return;
            if (levelData.music != null) currentAudioClip = levelData.music;
            if (!string.IsNullOrEmpty(levelData.audioPath) && File.Exists(levelData.audioPath))
                currentAudioPath = levelData.audioPath;
            if (!string.IsNullOrEmpty(levelData.videoPath) && File.Exists(levelData.videoPath))
                currentVideoPath = levelData.videoPath;
            if (levelData.cover != null)
            {
                currentCoverSprite = levelData.cover;
                if (coverPreviewImage != null) coverPreviewImage.sprite = levelData.cover;
            }
        }

        void RestoreTimelineAudio()
        {
            if (timelineUI == null || levelData == null) return;
            if (timelineUI.audioSource != null && levelData.music != null)
            {
                timelineUI.audioSource.clip = levelData.music;
                timelineUI.audioSource.Stop();
            }
            timelineUI.Seek(0);
        }

        public void TestPlayLevel()
        {
            if (timelineUI != null && timelineUI.levelData != null) levelData = timelineUI.levelData;
            if (levelData == null) { UpdateStatus("Нет данных уровня для теста"); return; }
            if (!HasRequiredMetadata()) { UpdateStatus("Заполните метаданные: название трека и аудио"); return; }
            if (string.IsNullOrEmpty(levelData.audioPath)) levelData.audioPath = currentAudioPath;
            if (string.IsNullOrEmpty(levelData.videoPath)) levelData.videoPath = currentVideoPath;
            if (timelineUI != null) timelineUI.levelData = levelData;
            if (previewManager != null) previewManager.levelData = levelData;
            if (transfer != null)
                transfer.SetLevel(levelData, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            UpdateStatus("Тест уровня — возврат в редактор: Esc");
            if (Transition != null) Transition.LoadScene(gameSceneName);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        }
        void PreviewVideo(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var vp = previewVideo;
            if (vp == null) return;
            vp.source = UnityEngine.Video.VideoSource.Url;
            vp.url = RkslStore.GetFileUri(path);
            vp.prepareCompleted += (v) => Debug.Log($"[RkslEditor] Video preview ready {path}", v);
            vp.Prepare();
        }

        protected override void OnDestroy()
        {
            if (titleInput != null) titleInput.onValueChanged.RemoveListener(OnMetadataInputChanged);
            if (artistInput != null) artistInput.onValueChanged.RemoveListener(OnMetadataInputChanged);
            if (creatorInput != null) creatorInput.onValueChanged.RemoveListener(OnMetadataInputChanged);
            if (timelineUI != null && timelineUI.fileLoader != null) timelineUI.fileLoader.onFileLoaded.RemoveListener(OnTimelineFileLoaded);
            UnsubscribeRkslLoaders();
            if (audioLoader != null) audioLoader.onFileLoaded.RemoveListener(OnAudioLoaded);
            if (testPlayButton != null) testPlayButton.onClick.RemoveListener(TestPlayLevel);
            if (exitButton != null) exitButton.onClick.RemoveListener(RequestExit);
            base.OnDestroy();
        }

        void OnAudioLoaded(string path, AudioClip clip)
        {
            currentAudioPath = path;
            currentAudioClip = clip;
            if (timelineUI != null && timelineUI.levelData != null)
            {

if (timelineUI.levelData.events.Count > 0 && string.IsNullOrEmpty(timelineUI.levelData.fullTitle) && timelineUI.levelData.music != clip)
                {

                    timelineUI.levelData.events.Clear();
                    timelineUI.RefreshNotes();
                }
            }
            UpdateStatus($"Аудио: {Path.GetFileName(path)} {clip.length:0.0}с");
            RefreshButtonStates();
        }

        void LoadCoverPreview(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            StartCoroutine(LoadCoverCoroutine(path));
        }
        IEnumerator LoadCoverCoroutine(string path)
        {
                using (var uwr = UnityWebRequestTexture.GetTexture(RkslStore.GetFileUri(path)))
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

            if (visualSettings != null) visualSettings.RefreshFromData();

            if (visual != null) visual.Apply(levelData, previewVideo, true);
            if (!string.IsNullOrEmpty(levelData.videoPath) && File.Exists(levelData.videoPath))
                PreviewVideo(levelData.videoPath);
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
            levelData.particlesEnabled = true;
            levelData.particleSprite = null;
            levelData.particleSpriteName = "";
            levelData.sphereRotates = true;
            levelData.sphereUseVideo = true;
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
            if (audioLoader != null) audioLoader.Clear();
            if (videoLoader != null) videoLoader.Clear();
            if (coverLoader != null) coverLoader.Clear();
            UpdateStatus("Новый уровень — загрузите аудио");
            CaptureSavedState();
            RefreshButtonStates();
        }

        public bool SaveRksl()
        {

            if (timelineUI != null && timelineUI.levelData != null) levelData = timelineUI.levelData;
            if (levelData == null && timelineUI != null) levelData = timelineUI.levelData;
            if (levelData == null) { UpdateStatus("Нет данных уровня"); return false; }

            if (timelineUI != null && timelineUI.levelData != levelData) timelineUI.levelData = levelData;

if (titleInput != null) levelData.fullTitle = titleInput.text;
            if (artistInput != null) levelData.songAuthor = artistInput.text;
            if (creatorInput != null) levelData.mapAuthor = creatorInput.text;

            if (!HasRequiredMetadata()) { UpdateStatus("Заполните метаданные: название трека и аудио"); return false; }

            if (string.IsNullOrEmpty(currentAudioPath) && audioLoader != null) currentAudioPath = audioLoader.CurrentPath;
            if (string.IsNullOrEmpty(currentVideoPath) && videoLoader != null) currentVideoPath = videoLoader.CurrentPath;
            if (string.IsNullOrEmpty(currentCoverPath) && coverLoader != null) currentCoverPath = coverLoader.CurrentPath;
            if (string.IsNullOrEmpty(levelData.audioPath)) levelData.audioPath = currentAudioPath;
            if (string.IsNullOrEmpty(levelData.videoPath)) levelData.videoPath = currentVideoPath;

            string defaultName = string.IsNullOrEmpty(levelData.fullTitle) ? "NewLevel" : levelData.fullTitle;
            defaultName = string.Join("_", defaultName.Split(Path.GetInvalidFileNameChars()));

#if UNITY_EDITOR
            string path = EditorUtility.SaveFilePanel("Сохранить .rksl", "", defaultName + ".rksl", "rksl");
            if (string.IsNullOrEmpty(path)) return false;
#else
            string dir = Path.Combine(Application.persistentDataPath, "Levels");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, defaultName + ".rksl");
#endif
            var manifest = Store.FromRuntimeData(levelData);
            manifest.title = levelData.fullTitle;
            manifest.artist = levelData.songAuthor;
            manifest.creator = levelData.mapAuthor;

            string audioSrc = !string.IsNullOrEmpty(currentAudioPath) ? currentAudioPath : levelData.audioPath;
            string videoSrc = !string.IsNullOrEmpty(currentVideoPath) ? currentVideoPath : levelData.videoPath;
            string coverSrc = !string.IsNullOrEmpty(currentCoverPath) ? currentCoverPath : null;
            bool ok = Store.Save(path, manifest, audioSrc, videoSrc, coverSrc, currentCoverSprite ?? levelData.cover);
            if (ok) { UpdateStatus($"Сохранено: {Path.GetFileName(path)}"); CaptureSavedState(); }
            else UpdateStatus("Ошибка сохранения");
            return ok;
        }

        public void SaveAndExit()
        {
            if (SaveRksl()) ExitNow();
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
            if (!Store.Extract(rkslPath, extractDir, out var manifest, out var audioPath, out var videoPath, out var coverPath))
            {
                UpdateStatus("Ошибка распаковки .rksl");
                yield break;
            }

AudioClip clip = null;
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
                string url = RkslStore.GetFileUri(audioPath);
                AudioType type = RkslStore.GetAudioType(audioPath);
                using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, type))
                {
                    yield return uwr.SendWebRequest();
                    if (uwr.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(uwr);
                    else UpdateStatus($"Ошибка аудио: {uwr.error} url={url}");
                }
            }

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

var data = Store.ToRuntimeData(manifest, clip, null, coverSpr);
            data.audioPath = audioPath;
            data.videoPath = videoPath;
            if (string.IsNullOrEmpty(data.fullTitle))
                data.fullTitle = Path.GetFileNameWithoutExtension(rkslPath);

levelData = data;
            if (timelineUI != null) timelineUI.levelData = data;
            if (previewManager != null) previewManager.levelData = data;

            currentAudioPath = audioPath; currentVideoPath = videoPath; currentCoverPath = coverPath;
            currentAudioClip = clip; currentCoverSprite = coverSpr;
            RestorePathsFromData();

            if (audioLoader != null) audioLoader.SetLoadedFile(audioPath);
            if (videoLoader != null) videoLoader.SetLoadedFile(videoPath);
            if (coverLoader != null) coverLoader.SetLoadedFile(coverPath);
            if (titleInput != null) titleInput.text = data.fullTitle;
            if (artistInput != null) artistInput.text = data.songAuthor;
            if (creatorInput != null) creatorInput.text = data.mapAuthor;
            if (coverPreviewImage != null && coverSpr != null) coverPreviewImage.sprite = coverSpr;

            RestoreTimelineAudio();
            if (timelineUI != null)
            {
                timelineUI.RefreshAll();
                timelineUI.Seek(0);
            }

            if (visualSettings != null) visualSettings.RefreshFromData();
            if (visual != null) visual.Apply(data, previewVideo, true);
            if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                PreviewVideo(videoPath);

            UpdateStatus($"Загружен: {data.fullTitle} ({manifest.events.Count} нот)");
            CaptureSavedState();
            RefreshButtonStates();

            if (Save != null)
            {
                if (Save.CurrentData == null) Save.Load();
                Save.CurrentData.lastRkslPath = rkslPath;
                Save.Write();
            }
        }

        AudioType GetAudioType(string path) => RkslStore.GetAudioType(path);

        void UpdateStatus(string msg) { if (statusText != null) statusText.text = msg; Debug.Log($"[RkslEditor] {msg}"); }
    }
}
