using System.Collections.Generic;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Core.Storage;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    [System.Serializable]
    public class LevelTransfer
    {
        [HideInInspector]
        [InjectOptional] public ISaveStore save;

        public RhythmLevelData levelData;
        public string rkslPath = "";
        public string levelName = "";
        public string sourceScene = "";
        public bool fromEditor = false;

public bool hasLevel
        {
            get
            {
                if (levelData != null) return true;
                if (!string.IsNullOrEmpty(rkslPath) && System.IO.File.Exists(rkslPath)) return true;
                foreach (var p in GetSavedPaths())
                    if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) return true;
                return false;
            }
        }

        public List<string> GetSavedPaths()
        {
            var paths = new List<string>();
            var data = save != null ? save.CurrentData : null;
            if (data == null) return paths;
            if (!string.IsNullOrEmpty(data.selectedLevelPath)) paths.Add(data.selectedLevelPath);
            if (!string.IsNullOrEmpty(data.lastRkslPath)) paths.Add(data.lastRkslPath);
            if (!string.IsNullOrEmpty(data.transferRkslPath)) paths.Add(data.transferRkslPath);
            return paths;
        }

        public string GetEffectivePath()
        {
            if (!string.IsNullOrEmpty(rkslPath) && System.IO.File.Exists(rkslPath)) return rkslPath;
            foreach (var p in GetSavedPaths())
                if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) return p;
            return rkslPath;
        }

        public void SetLevel(RhythmLevelData data, string srcScene)
        {
            if (data == null) { levelData = null; rkslPath = ""; return; }
            levelData = data.CloneDeep();
            levelName = string.IsNullOrEmpty(data.fullTitle) ? System.IO.Path.GetFileNameWithoutExtension(data.audioPath) : data.fullTitle;
            sourceScene = srcScene;
            fromEditor = srcScene == "LevelEditor" || srcScene == "IsLevelEditorScene";
            rkslPath = "";
            UnityEngine.Debug.Log($"[LevelTransfer] SetLevel '{levelName}' from {srcScene} events={data.events?.Count ?? 0}");
        }

        public void SetRkslPath(string path, string srcScene)
        {
            rkslPath = path;
            levelName = System.IO.Path.GetFileNameWithoutExtension(path);
            sourceScene = srcScene;
            fromEditor = false;
            levelData = null;
            if (save != null)
            {
                if (save.CurrentData == null) save.Load();
                save.CurrentData.selectedLevelPath = path;
                save.CurrentData.lastRkslPath = path;
                save.Write();
            }
            UnityEngine.Debug.Log($"[LevelTransfer] SetRkslPath '{path}' from {srcScene}");
        }

        public void ClearSelectedPath(string path)
        {
            if (save == null || string.IsNullOrEmpty(path)) return;
            if (save.CurrentData == null) save.Load();
            if (save.CurrentData.selectedLevelPath == path)
            {
                save.CurrentData.selectedLevelPath = "";
                save.Write();
            }
        }

        public void Clear()
        {
            levelData = null;
            rkslPath = "";
            levelName = "";
            sourceScene = "";
            fromEditor = false;
        }
    }
}
