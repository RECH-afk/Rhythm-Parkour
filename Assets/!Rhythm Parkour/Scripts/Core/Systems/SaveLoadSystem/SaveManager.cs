using System;
using System.IO;
using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Core.Storage;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Managers
{
    public class SaveManager : RKSBehaviour, ISaveStore
    {
        [Header("File Configuration")]
        [SerializeField] private string fileName = "DD_Data.rkst";

        private string filePath;
        private IDataCodec _codec;

        public GameData.Data CurrentData { get; private set; }

        protected override void OnInjected()
        {
            filePath = Path.Combine(Application.persistentDataPath, fileName);
            Debug.Log($"[SaveManager] Initialized at {filePath}");

            if (Container != null) _codec = Container.TryResolve<IDataCodec>();
            if (_codec == null) _codec = new RechCodec();
            CurrentData = LoadInternal();
        }
        public void Write(GameData.Data data)
        {
            if (data == null)
            {
                Debug.LogWarning("[SaveManager] Save(data) called with null -> creating default.");
                data = new GameData.Data();
            }

            CurrentData = data;
            WriteToFile(CurrentData);
            Debug.Log("[SaveManager] Data saved successfully (via parameter).");
        }

        public void Write()
        {
            if (CurrentData == null)
            {
                Debug.LogWarning("[SaveManager] No data found, creating default...");
                CurrentData = new GameData.Data();
            }

            WriteToFile(CurrentData);
            Debug.Log("[SaveManager] Data saved successfully.");
        }

        private void WriteToFile(GameData.Data data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(filePath, _codec.Encode(json));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save data: {ex.Message}");
            }
        }


        public GameData.Data Load()
        {
            CurrentData = LoadInternal();
            return CurrentData;
        }

        public void ResetToDefault()
        {
            CurrentData = new GameData.Data();
            Write();
            Debug.Log("[SaveManager] Reset to default.");
        }

        private GameData.Data LoadInternal()
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning("[SaveManager] No save file found, creating default...");
                var def = new GameData.Data();
                SaveDefault(def);
                return def;
            }

            try
            {
                string json = _codec.Decode(File.ReadAllText(filePath));
                var data = JsonUtility.FromJson<GameData.Data>(json);
                Debug.Log("[SaveManager] Data loaded successfully.");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SaveManager] Failed to load data: " + ex.Message);
                var def = new GameData.Data();
                SaveDefault(def);
                return def;
            }
        }

        private void SaveDefault(GameData.Data data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, _codec.Encode(json));
        }
    }
}
