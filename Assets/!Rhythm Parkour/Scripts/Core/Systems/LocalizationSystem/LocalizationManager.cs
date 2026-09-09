using RKS.HadalZone.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace RKS.HadalZone.Core.Managers 
{
    public class LocalizationManager : RKSBehaviour
    {
        public string currentLanguage { get; private set; }
        private Dictionary<string, string> localizedText = new Dictionary<string, string>();
        public static bool isReady = false;

        public delegate void ChangeLangText();
        public event ChangeLangText OnLanguageChanged;

        protected override void OnReady()
        {
            Save.Load();

            if (string.IsNullOrEmpty(Save.CurrentData.language))
            {
                switch (Application.systemLanguage)
                {
                    case SystemLanguage.Russian:
                    case SystemLanguage.Ukrainian:
                    case SystemLanguage.Belarusian:
                        Save.CurrentData.language = "ru_RU";
                        break;

                    case SystemLanguage.German:
                        Save.CurrentData.language = "de_DE";
                        break;

                    case SystemLanguage.Spanish:
                        Save.CurrentData.language = "es_ES";
                        break;

                    default:
                        Save.CurrentData.language = "en_US";
                        break;
                }

                Save.Write();
            }

            currentLanguage = Save.CurrentData.language;
            StartCoroutine(LoadLocalizedTextCoroutine(currentLanguage));
        }

        private IEnumerator<UnityWebRequestAsyncOperation> LoadLocalizedTextCoroutine(string langName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Languages", langName + ".json");
            string dataAsJson = "";

            if (Application.platform == RuntimePlatform.Android)
            {
                using (UnityWebRequest www = UnityWebRequest.Get(path))
                {
                    yield return www.SendWebRequest();

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[LocalizationManager] Failed to load {langName}: {www.error}");
                        yield break;
                    }

                    dataAsJson = www.downloadHandler.text;
                }
            }
            else
            {
                if (!File.Exists(path))
                {
                    Debug.LogError($"[LocalizationManager] Missing language file: {path}");
                    yield break;
                }

                dataAsJson = File.ReadAllText(path);
            }

            LocalizationData loadedData = JsonUtility.FromJson<LocalizationData>(dataAsJson);
            localizedText.Clear();

            foreach (var item in loadedData.items)
            {
                localizedText[item.key] = item.value;
            }

            currentLanguage = langName;
            isReady = true;
            OnLanguageChanged?.Invoke();

            Debug.Log($"[LocalizationManager] Language loaded: {langName}");
        }

        public void SetLanguage(string langName)
        {
            if (langName == currentLanguage) return;

            Save.CurrentData.language = langName;
            Save.Write();
            StartCoroutine(LoadLocalizedTextCoroutine(langName));
        }

        public string GetLocalizedValue(string key)
        {
            if (localizedText.TryGetValue(key, out string value))
                return value;

            Debug.LogWarning($"[LocalizationManager] Missing key: {key}");
            return key;
        }
    }

}