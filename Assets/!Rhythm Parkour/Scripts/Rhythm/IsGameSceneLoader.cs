using System.Collections;
using System.IO;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public class IsGameSceneLoader : RKSBehaviour
    {
        [InjectOptional] public RhythmParkourManager manager;
        [InjectOptional] public LevelTransfer transfer;
        [InjectOptional] public LevelVisualApplier visual;
        [InjectOptional] public MusicInfoUI musicInfo;
        bool hasLoaded;

        protected override void OnInjected()
        {
            Debug.Log($"[IsGameSceneLoader] Injected scene={SceneManager.GetActiveScene().name} manager={(manager ? manager.name : "null")} hasLevel={(transfer != null && transfer.hasLevel)} rkslPath='{transfer?.rkslPath}' Sel='{transfer?.GetEffectivePath()}'", this);
        }

        protected override void OnReady()
        {
            if (manager == null) { Debug.LogError("[IsGameSceneLoader] manager не забинден! Добавь GameInstaller+SceneContext.", this); return; }
            LoadLevel();
        }

        void LoadLevel()
        {
            if (hasLoaded) return;

            if (transfer != null && transfer.levelData != null && (transfer.levelData.events.Count > 0 || transfer.levelData.music != null))
            {
                Debug.Log($"[IsGameSceneLoader] Использую LevelTransfer in-memory '{transfer.levelName}' events={transfer.levelData.events.Count}", this);
                ApplyLevel(transfer.levelData);
                hasLoaded = true;
                return;
            }

            string path = transfer != null ? transfer.GetEffectivePath() : "";
            if (string.IsNullOrEmpty(path) && Save != null && Save.CurrentData != null) path = Save.CurrentData.selectedLevelPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                Debug.Log($"[IsGameSceneLoader] Гружу rksl '{path}'", this);
                StartCoroutine(LoadFromRksl(path));
                hasLoaded = true;
                return;
            }

            var all = RkslFile.FindAllRkslFiles(transfer != null ? transfer.GetSavedPaths() : null);
            if (all.Count > 0)
            {
                Debug.Log($"[IsGameSceneLoader] Нашел {all.Count} rksl, беру первый: {all[0]}", this);
                StartCoroutine(LoadFromRksl(all[0]));
                hasLoaded = true;
                return;
            }

            string fallbackMp3 = TryFindFirstMp3();
            if (!string.IsNullOrEmpty(fallbackMp3) && File.Exists(fallbackMp3))
            {
                Debug.Log($"[IsGameSceneLoader] Нет .rksl, пробую создать уровень из mp3 '{fallbackMp3}'", this);
                StartCoroutine(LoadFromMp3Fallback(fallbackMp3));
                hasLoaded = true;
                return;
            }

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

            try
            {
                var det = BpmDetector.Detect(clip);
                data.bpm = Mathf.Clamp(det.bpm, 70f, 200f);
                Debug.Log($"[IsGameSceneLoader] Fallback BPM {data.bpm:0} conf {det.confidence:0.##}");
            }
            catch {}
            if (transfer != null) transfer.levelData = data.CloneDeep();
            ApplyLevel(data);
        }

        void ApplyLevel(RhythmLevelData data)
        {
            if (manager == null) { Debug.LogError("[IsGameSceneLoader] manager null в ApplyLevel"); return; }
            RhythmLevelData toApply = data;
            try { toApply = data.CloneDeep(); } catch { }
            manager.levelData = toApply;
            manager.PrepareLevel(toApply);
            if (toApply.music != null && manager.musicSource != null)
            {
                manager.musicSource.clip = toApply.music;
                manager.musicSource.Stop();
            }

            if (visual != null) visual.Apply(toApply, manager.videoPlayer, false, manager.trackFloor, null);
            if (musicInfo != null) musicInfo.Show(toApply);

            Debug.Log($"[IsGameSceneLoader] Применен '{toApply.fullTitle}' bpm={toApply.bpm} events={toApply.events.Count} music={(toApply.music?toApply.music.name:"null")} video={(toApply.videoPath??"null")}", this);

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
            if (transfer != null)
            {
                transfer.levelData = data.CloneDeep();
                transfer.rkslPath = rkslPath;
                transfer.levelName = man.title;
            }
            if (Save != null)
            {
                if (Save.CurrentData == null) Save.Load();
                Save.CurrentData.selectedLevelPath = rkslPath;
                Save.CurrentData.lastRkslPath = rkslPath;
                Save.Write();
            }
            ApplyLevel(data);
        }
    }
}
