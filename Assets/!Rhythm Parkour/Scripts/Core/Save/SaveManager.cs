using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
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

        const string IntegrityPrefix = "RKST-HMAC:";
        const string IntegrityKeyBase64 = "BReH+Ba816quXKHa7XnKtk+IESw3jHZs2dcUB8XMx5E=";

        private string filePath;
        private IDataCodec _codec;

        public GameData.Data CurrentData { get; private set; }

        static string ComputeSignature(string payload)
        {
            using (var hmac = new HMACSHA256(Convert.FromBase64String(IntegrityKeyBase64)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return Convert.ToBase64String(hash);
            }
        }

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
                string payload = _codec.Encode(json);
                File.WriteAllText(filePath, IntegrityPrefix + ComputeSignature(payload) + "\n" + payload);
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
                string raw = File.ReadAllText(filePath);
                string json;

                if (raw.StartsWith(IntegrityPrefix, StringComparison.Ordinal))
                {
                    int separator = raw.IndexOf('\n');
                    if (separator < 0) throw new FormatException("Missing payload after integrity header");

                    string signature = raw.Substring(IntegrityPrefix.Length, separator - IntegrityPrefix.Length);
                    string payload = raw.Substring(separator + 1);

                    if (!string.Equals(ComputeSignature(payload), signature, StringComparison.Ordinal))
                    {
                        Debug.LogWarning("[SaveManager] Save integrity check failed - file was modified. Loading default.");
                        var tamperedDef = new GameData.Data();
                        SaveDefault(tamperedDef);
                        return tamperedDef;
                    }

                    json = _codec.Decode(payload);
                }
                else
                {
                    json = _codec.Decode(raw);
                }

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
            WriteToFile(data);
        }
    }
}
