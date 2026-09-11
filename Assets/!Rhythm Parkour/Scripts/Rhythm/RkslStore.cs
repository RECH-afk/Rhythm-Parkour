using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;
using RKS.RhythmParkour.Core.Storage;

namespace RKS.RhythmParkour.Rhythm
{
    public sealed class RkslStore : IRkslStore
    {
        public const string ManifestName = "level.rkst";
        public const string LegacyManifestName = "level.json";
        public const int ManifestVersion = 2;

        public static IRkslStore Shared { get; } = new RkslStore(new RechCodec());

        private readonly IDataCodec _codec;
        private readonly Dictionary<string, (DateTime stamp, RkslManifest manifest)> _manifestCache =
            new Dictionary<string, (DateTime stamp, RkslManifest manifest)>(StringComparer.OrdinalIgnoreCase);

        [Inject]
        public RkslStore(IDataCodec codec)
        {
            _codec = codec ?? new RechCodec();
        }

        public static string GetFileUri(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string full = Path.GetFullPath(path).Replace("\\", "/");
            if (!full.StartsWith("/")) full = "/" + full;
            return "file://" + full;
        }

        public static AudioType GetAudioType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".mp3": return AudioType.MPEG;
                case ".wav": return AudioType.WAV;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".aiff": return AudioType.AIFF;
                case ".m4a": return AudioType.MPEG;
                default: return AudioType.UNKNOWN;
            }
        }

        public bool Save(string rkslPath, RkslManifest manifest, string audioSourcePath, string videoSourcePath, string coverSourcePath, Sprite coverSprite = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(audioSourcePath) && File.Exists(audioSourcePath))
                    manifest.audioFile = Path.GetFileName(audioSourcePath);
                else if (string.IsNullOrEmpty(manifest.audioFile) && !string.IsNullOrEmpty(audioSourcePath))
                    manifest.audioFile = Path.GetFileName(audioSourcePath);
                if (!string.IsNullOrEmpty(videoSourcePath) && File.Exists(videoSourcePath))
                    manifest.videoFile = Path.GetFileName(videoSourcePath);
                else if (string.IsNullOrEmpty(manifest.videoFile) && !string.IsNullOrEmpty(videoSourcePath))
                    manifest.videoFile = Path.GetFileName(videoSourcePath);

                byte[] coverBytes = null;
                string coverFileName = manifest.coverFile;
                if (coverSprite != null && coverSprite.texture != null)
                {
                    try
                    {
                        Texture2D tex = coverSprite.texture;
                        if (!tex.isReadable)
                        {
                            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                            Graphics.Blit(tex, rt);
                            RenderTexture prev = RenderTexture.active;
                            RenderTexture.active = rt;
                            Texture2D copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                            copy.Apply();
                            RenderTexture.active = prev;
                            RenderTexture.ReleaseTemporary(rt);
                            coverBytes = copy.EncodeToPNG();
                            UnityEngine.Object.Destroy(copy);
                        }
                        else coverBytes = tex.EncodeToPNG();
                        if (coverBytes != null && coverBytes.Length > 0)
                        {
                            if (string.IsNullOrEmpty(coverFileName)) coverFileName = "cover.png";
                            manifest.coverFile = coverFileName;
                        }
                    }
                    catch (Exception e) { Debug.LogWarning($"[Rksl] cover encode failed: {e.Message}"); }
                }
                else if (!string.IsNullOrEmpty(coverSourcePath) && File.Exists(coverSourcePath))
                {
                    if (string.IsNullOrEmpty(coverFileName)) coverFileName = Path.GetFileName(coverSourcePath);
                    manifest.coverFile = coverFileName;
                    coverFileName = manifest.coverFile;
                }
                else if (!string.IsNullOrEmpty(coverFileName) && coverBytes == null)
                {
                    coverFileName = manifest.coverFile;
                }

                manifest.version = ManifestVersion;
                string payload = _codec.Encode(JsonUtility.ToJson(manifest, false));
                string dir = Path.GetDirectoryName(rkslPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(rkslPath)) File.Delete(rkslPath);

                using (var zip = ZipFile.Open(rkslPath, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry(ManifestName, System.IO.Compression.CompressionLevel.Optimal);
                    using (var s = entry.Open())
                    using (var w = new StreamWriter(s)) w.Write(payload);

                    if (!string.IsNullOrEmpty(audioSourcePath) && File.Exists(audioSourcePath))
                    {
                        string name = Path.GetFileName(audioSourcePath);
                        zip.CreateEntryFromFile(audioSourcePath, name, System.IO.Compression.CompressionLevel.Optimal);
                    }
                    if (!string.IsNullOrEmpty(videoSourcePath) && File.Exists(videoSourcePath))
                    {
                        string name = Path.GetFileName(videoSourcePath);
                        zip.CreateEntryFromFile(videoSourcePath, name, System.IO.Compression.CompressionLevel.Optimal);
                    }
                    if (coverBytes != null && coverBytes.Length > 0)
                    {
                        var ce = zip.CreateEntry(coverFileName, System.IO.Compression.CompressionLevel.Optimal);
                        using (var cs = ce.Open()) cs.Write(coverBytes, 0, coverBytes.Length);
                    }
                    else if (!string.IsNullOrEmpty(coverSourcePath) && File.Exists(coverSourcePath))
                    {
                        string name = Path.GetFileName(coverSourcePath);
                        zip.CreateEntryFromFile(coverSourcePath, name, System.IO.Compression.CompressionLevel.Optimal);
                    }
                }

                Debug.Log($"[Rksl] Saved {rkslPath} ({manifest.events.Count})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rksl] Save failed {rkslPath}: {e}");
                return false;
            }
        }

        public bool LoadManifestOnly(string rkslPath, out RkslManifest manifest)
        {
            manifest = null;
            try
            {
                if (!File.Exists(rkslPath)) return false;
                DateTime stamp = File.GetLastWriteTimeUtc(rkslPath);
                if (_manifestCache.TryGetValue(rkslPath, out var cached) && cached.stamp == stamp)
                {
                    manifest = cached.manifest;
                    return manifest != null;
                }
                using (var zip = ZipFile.OpenRead(rkslPath))
                {
                    if (!TryReadManifest(zip, out string json)) return false;
                    manifest = JsonUtility.FromJson<RkslManifest>(json);
                    if (_manifestCache.Count > 511) _manifestCache.Clear();
                    _manifestCache[rkslPath] = (stamp, manifest);
                    return manifest != null;
                }
            }
            catch (Exception e) { Debug.LogError($"[Rksl] LoadManifest {rkslPath}: {e}"); return false; }
        }

        public bool TryReuseExtracted(string rkslPath, string extractDir, out string audioPath, out string videoPath, out string coverPath)
        {
            audioPath = null; videoPath = null; coverPath = null;
            try
            {
                if (string.IsNullOrEmpty(rkslPath) || !File.Exists(rkslPath)) return false;
                if (string.IsNullOrEmpty(extractDir) || !Directory.Exists(extractDir)) return false;
                if (!TryReadManifestFile(extractDir, out string json)) return false;
                var manifest = JsonUtility.FromJson<RkslManifest>(json);
                if (manifest == null) return false;
                if (!string.IsNullOrEmpty(manifest.audioFile))
                {
                    string p = Path.Combine(extractDir, manifest.audioFile);
                    if (!File.Exists(p)) return false;
                    audioPath = p;
                }
                else return false;
                if (!string.IsNullOrEmpty(manifest.videoFile))
                {
                    string p = Path.Combine(extractDir, manifest.videoFile);
                    if (!File.Exists(p)) return false;
                    videoPath = p;
                }
                if (!string.IsNullOrEmpty(manifest.coverFile))
                {
                    string p = Path.Combine(extractDir, manifest.coverFile);
                    if (!File.Exists(p)) return false;
                    coverPath = p;
                }
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[Rksl] Reuse check failed: {e.Message}"); return false; }
        }

        public List<string> FindAllRkslFiles(IEnumerable<string> extraPaths = null)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirs = new string[]
            {
                Path.Combine(UnityEngine.Application.persistentDataPath, "Levels"),
                Path.Combine(UnityEngine.Application.persistentDataPath, "LevelTransfer"),
                Path.Combine(UnityEngine.Application.temporaryCachePath, "LevelTransfer"),
                Path.Combine(UnityEngine.Application.streamingAssetsPath, "GameLevels"),
                Path.Combine(UnityEngine.Application.dataPath, "!Rhythm Parkour/Levels"),
                Path.Combine(UnityEngine.Application.dataPath, "Levels"),
            };
            foreach (var d in dirs)
            {
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
                try
                {
                    foreach (var f in Directory.GetFiles(d, "*.rksl"))
                    {
                        if (seen.Add(f)) result.Add(f);
                    }
                }
                catch {}
            }

            if (extraPaths != null)
                foreach (var p in extraPaths)
                    if (!string.IsNullOrEmpty(p) && File.Exists(p) && seen.Add(p)) result.Add(p);
            return result;
        }

        public bool Extract(string rkslPath, string extractDir, out RkslManifest manifest, out string audioPath, out string videoPath, out string coverPath)
        {
            manifest = null; audioPath = null; videoPath = null; coverPath = null;
            try
            {
                if (!File.Exists(rkslPath)) return false;

                if (Directory.Exists(extractDir))
                {
                    foreach (var f in Directory.GetFiles(extractDir)) try { File.Delete(f); } catch {}
                    foreach (var d in Directory.GetDirectories(extractDir)) try { Directory.Delete(d, true); } catch {}
                }
                else Directory.CreateDirectory(extractDir);
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(rkslPath, extractDir);
                if (!TryReadManifestFile(extractDir, out string json)) return false;
                manifest = JsonUtility.FromJson<RkslManifest>(json);
                if (manifest == null) return false;

                if (!string.IsNullOrEmpty(manifest.audioFile))
                {
                    string p = Path.Combine(extractDir, manifest.audioFile);
                    if (File.Exists(p)) audioPath = p;
                }
                if (!string.IsNullOrEmpty(manifest.videoFile))
                {
                    string p = Path.Combine(extractDir, manifest.videoFile);
                    if (File.Exists(p)) videoPath = p;
                }
                if (!string.IsNullOrEmpty(manifest.coverFile))
                {
                    string p = Path.Combine(extractDir, manifest.coverFile);
                    if (File.Exists(p)) coverPath = p;
                }
                return true;
            }
            catch (Exception e) { Debug.LogError($"[Rksl] Extract {rkslPath}: {e}"); return false; }
        }

        public RhythmLevelData ToRuntimeData(RkslManifest m, AudioClip audioClip, VideoClip videoClip, Sprite cover)
        {
            var data = new RhythmLevelData();
            data.fullTitle = m.title;
            data.songAuthor = m.artist;
            data.mapAuthor = m.creator;
            data.bpm = Mathf.Max(1f, m.bpm);
            data.offset = m.offset;
            data.events = new List<ObstacleEvent>(m.events ?? new List<ObstacleEvent>());
            data.particlesEnabled = m.particlesEnabled;
            data.particleColor = m.particleColor;
            data.obstacleColor = m.obstacleColor;
            data.trackColor = m.trackColor;
            data.sphereRotates = m.sphereRotates;
            data.defaultObstacleMaterialName = m.defaultObstacleMaterialName ?? "";
            data.music = audioClip;
            data.video = videoClip;
            data.cover = cover;
            data.audioPath = m.audioFile;
            data.videoPath = m.videoFile;
            return data;
        }

        public RkslManifest FromRuntimeData(RhythmLevelData data)
        {
            if (data.defaultObstacleMaterial != null && string.IsNullOrEmpty(data.defaultObstacleMaterialName))
                data.defaultObstacleMaterialName = data.defaultObstacleMaterial.name;
            else if (data.defaultObstacleMaterial != null && data.defaultObstacleMaterial.name != data.defaultObstacleMaterialName)
                data.defaultObstacleMaterialName = data.defaultObstacleMaterial.name;

            return new RkslManifest
            {
                version = ManifestVersion,
                title = data.fullTitle,
                artist = data.songAuthor,
                creator = data.mapAuthor,
                bpm = data.bpm,
                offset = data.offset,
                duration = data.music != null ? data.music.length : 0f,
                events = new List<ObstacleEvent>(data.events),
                particlesEnabled = data.particlesEnabled,
                particleColor = data.particleColor,
                obstacleColor = data.obstacleColor,
                trackColor = data.trackColor,
                sphereRotates = data.sphereRotates,
                defaultObstacleMaterialName = data.defaultObstacleMaterialName ?? "",
                audioFile = !string.IsNullOrEmpty(data.audioPath) ? Path.GetFileName(data.audioPath) : "",
                videoFile = !string.IsNullOrEmpty(data.videoPath) ? Path.GetFileName(data.videoPath) : "",
                coverFile = data.cover != null ? "cover.png" : ""
            };
        }

        private bool TryReadManifest(ZipArchive zip, out string json)
        {
            json = null;
            try
            {
                var entry = zip.GetEntry(ManifestName);
                bool encoded = true;
                if (entry == null)
                {
                    entry = zip.GetEntry(LegacyManifestName);
                    encoded = false;
                }
                if (entry == null) return false;
                using (var s = entry.Open())
                using (var r = new StreamReader(s))
                {
                    string raw = r.ReadToEnd();
                    json = encoded ? _codec.Decode(raw) : raw;
                    return true;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Rksl] manifest read failed: {e.Message}"); return false; }
        }

        private bool TryReadManifestFile(string extractDir, out string json)
        {
            json = null;
            try
            {
                string rkst = Path.Combine(extractDir, ManifestName);
                if (File.Exists(rkst))
                {
                    json = _codec.Decode(File.ReadAllText(rkst));
                    return true;
                }
                string legacy = Path.Combine(extractDir, LegacyManifestName);
                if (File.Exists(legacy))
                {
                    json = File.ReadAllText(legacy);
                    return true;
                }
                return false;
            }
            catch (Exception e) { Debug.LogWarning($"[Rksl] manifest read failed: {e.Message}"); return false; }
        }
    }
}
