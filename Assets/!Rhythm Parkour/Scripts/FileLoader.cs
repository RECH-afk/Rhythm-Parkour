using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour
{
    public enum FileType { Audio, Video, Photo, Rksl }

    [Flags]
    public enum AllowedFileTypes
    {
        None = 0,
        Audio = 1,
        Video = 2,
        Photo = 4,
        Rksl = 8
    }

    [Serializable]
    public class FileLoadedEvent : UnityEvent<string, AudioClip> { }
    [Serializable] public class RkslLoadedEvent : UnityEvent<string> { }

    [RequireComponent(typeof(Button))]
    public class FileLoader : RKSBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image dropZoneImage;

        [Header("Settings")]
        [SerializeField] private AllowedFileTypes allowedTypes = AllowedFileTypes.Audio;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color successColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        [SerializeField] private Color errorColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);

        [Header("Events")]
        public FileLoadedEvent onFileLoaded;
        public RkslLoadedEvent onRkslLoaded;

        private Button selectFileButton;
        private string currentFilePath;
        public string CurrentPath => currentFilePath;
        public AllowedFileTypes AllowedTypes => allowedTypes;

        private static readonly Dictionary<string, FileType> extensionMap = new()
        {
            { ".mp3", FileType.Audio }, { ".wav", FileType.Audio }, { ".ogg", FileType.Audio },
            { ".mp4", FileType.Video },
            { ".png", FileType.Photo }, { ".jpg", FileType.Photo },
            { ".jpeg", FileType.Photo }, { ".webp", FileType.Photo },
            { ".rksl", FileType.Rksl }
        };

        protected override void OnInjected()
        {
            selectFileButton = GetComponent<Button>();
            selectFileButton.onClick.AddListener(OpenFileSelectionDialog);

            if (dropZoneImage != null)
                dropZoneImage.color = normalColor;

            UpdateStatus("Нажмите кнопку, чтобы выбрать файл");
        }

        private void OpenFileSelectionDialog()
        {
#if UNITY_EDITOR
            string ext = GetFilterForAllowedTypes();
            string path = EditorUtility.OpenFilePanel("Выберите файл", "", ext);
            if (!string.IsNullOrEmpty(path))
            {
                ProcessSelectedFile(path);
            }
#else
            UpdateStatus("Для выбора файлов в билде подключите плагин StandaloneFileBrowser");
            Debug.LogWarning("Runtime file dialog requires a plugin.");
#endif
        }

        string GetFilterForAllowedTypes()
        {
            if ((allowedTypes & AllowedFileTypes.Rksl) != 0) return "rksl";
            if ((allowedTypes & AllowedFileTypes.Audio) != 0) return "mp3,wav,ogg";
            if ((allowedTypes & AllowedFileTypes.Video) != 0) return "mp4";
            if ((allowedTypes & AllowedFileTypes.Photo) != 0) return "png,jpg,jpeg,webp";
            return "";
        }

        public void ProcessSelectedFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Flash(errorColor);
                UpdateStatus("Файл не найден");
                return;
            }

            if (!extensionMap.TryGetValue(Path.GetExtension(path).ToLower(), out FileType type))
            {
                Flash(errorColor);
                UpdateStatus("Неподдерживаемый формат");
                return;
            }

            if (!IsTypeAllowed(type))
            {
                Flash(errorColor);
                UpdateStatus($"Тип {type} не разрешен");
                return;
            }

            currentFilePath = path;
            string fileName = Path.GetFileNameWithoutExtension(path);
            UpdateStatus($"Загрузка: {fileName} ({type})");
            StartCoroutine(LoadByType(path, type));
        }

        private bool IsTypeAllowed(FileType type)
        {
            return type switch
            {
                FileType.Audio => (allowedTypes & AllowedFileTypes.Audio) != 0,
                FileType.Video => (allowedTypes & AllowedFileTypes.Video) != 0,
                FileType.Photo => (allowedTypes & AllowedFileTypes.Photo) != 0,
                FileType.Rksl => (allowedTypes & AllowedFileTypes.Rksl) != 0,
                _ => false
            };
        }

        private IEnumerator LoadByType(string path, FileType type)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);

            switch (type)
            {
                case FileType.Audio:
                    using (var www = UnityWebRequestMultimedia.GetAudioClip(RkslFile.GetFileUri(path), GetAudioType(path)))
                    {
                        yield return www.SendWebRequest();
                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                            Flash(successColor);
                            UpdateStatus($"Аудио загружено: {fileName}");
                            onFileLoaded?.Invoke(path, clip);
                        }
                        else
                        {
                            Flash(errorColor);
                            UpdateStatus($"Ошибка загрузки: {www.error}");
                        }
                    }
                    break;

                case FileType.Video:
                    Flash(successColor);
                    UpdateStatus($"Видео готово: {fileName}");
                    onFileLoaded?.Invoke(path, null);
                    break;

                case FileType.Photo:
                    using (var www = UnityWebRequestTexture.GetTexture(RkslFile.GetFileUri(path)))
                    {
                        yield return www.SendWebRequest();
                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            Flash(successColor);
                            UpdateStatus($"Изображение загружено: {fileName}");
                            onFileLoaded?.Invoke(path, null);
                        }
                        else
                        {
                            Flash(errorColor);
                            UpdateStatus($"Ошибка загрузки: {www.error}");
                        }
                    }
                    break;
                case FileType.Rksl:
                    Flash(successColor);
                    UpdateStatus($"Уровень: {fileName}");
                    onRkslLoaded?.Invoke(path);
                    onFileLoaded?.Invoke(path, null);
                    break;
            }
        }

        private AudioType GetAudioType(string path) => RkslFile.GetAudioType(path);

        private void Flash(Color color)
        {
            if (dropZoneImage != null)
            {
                StopCoroutine(nameof(ResetColor));
                dropZoneImage.color = color;
                StartCoroutine(ResetColor());
            }
        }

        private IEnumerator ResetColor()
        {
            yield return new WaitForSeconds(1f);
            if (dropZoneImage != null)
                dropZoneImage.color = normalColor;
        }

        private void UpdateStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }

        public void Clear()
        {
            currentFilePath = null;
            UpdateStatus("Нет файла");
            if (dropZoneImage != null)
                dropZoneImage.color = normalColor;
        }

        protected override void OnDisposed()
        {
            if (selectFileButton != null)
                selectFileButton.onClick.RemoveListener(OpenFileSelectionDialog);
        }
    }
}
