using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RKS.HadalZone.Core.Managers
{
    public class SaveManager : RKSBehaviour
    {
        [Header("File Configuration")]
        [SerializeField] private string fileName = "DD_Data.rkst";

        private string filePath;
        private readonly char[] rechAlphabet = { 'R', 'E', 'C', 'H' };

        public GameData.Data CurrentData { get; private set; }

        protected override void OnInjected()
        {
            filePath = Path.Combine(Application.persistentDataPath, fileName);
            Debug.Log($"[SaveManager] Initialized at {filePath}");

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
                string rechData = ConvertJsonToRech(json);
                File.WriteAllText(filePath, rechData);
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
                string rechData = File.ReadAllText(filePath);
                string json = ConvertRechToJson(rechData);
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
            string rechData = ConvertJsonToRech(json);
            File.WriteAllText(filePath, rechData);
        }

        private string ConvertJsonToRech(string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            StringBuilder sb = new StringBuilder(bytes.Length * 4);

            foreach (byte b in bytes)
            {
                int highBits = (b >> 6) & 0b11;
                int midBits = (b >> 4) & 0b11;
                int lowBits1 = (b >> 2) & 0b11;
                int lowBits2 = b & 0b11;

                sb.Append(rechAlphabet[highBits]);
                sb.Append(rechAlphabet[midBits]);
                sb.Append(rechAlphabet[lowBits1]);
                sb.Append(rechAlphabet[lowBits2]);
            }

            return sb.ToString();
        }

        private string ConvertRechToJson(string rechData)
        {
            if (rechData.Length % 4 != 0)
                throw new FormatException("Invalid RECH format");

            byte[] bytes = new byte[rechData.Length / 4];
            for (int i = 0; i < rechData.Length; i += 4)
            {
                int highBits = Array.IndexOf(rechAlphabet, rechData[i]);
                int midBits = Array.IndexOf(rechAlphabet, rechData[i + 1]);
                int lowBits1 = Array.IndexOf(rechAlphabet, rechData[i + 2]);
                int lowBits2 = Array.IndexOf(rechAlphabet, rechData[i + 3]);

                bytes[i / 4] = (byte)((highBits << 6) | (midBits << 4) | (lowBits1 << 2) | lowBits2);
            }

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
