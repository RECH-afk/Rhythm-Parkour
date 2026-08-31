using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>
/// Формат .rksl — zip архив с level.json + media файлами
/// level.json содержит метаданные, bpm, offset, ноты и имена файлов внутри архива
/// </summary>
[Serializable]
public class RkslManifest
{
    public int version = 1;
    public string title = "";
    public string artist = "";      // исполнитель трека
    public string creator = "";     // создатель уровня
    public float bpm = 128f;
    public float offset = 0f;
    public List<ObstacleEvent> events = new List<ObstacleEvent>();
    public string audioFile = "";   // имя файла внутри архива, напр. "audio.mp3"
    public string videoFile = "";   // опционально
    public string coverFile = "";   // опционально, напр. "cover.png"
    // доп поля для совместимости со старым RhythmLevelData
    public bool particlesEnabled = true;
    public Color particleColor = new Color(0.2f, 0.7f, 1f, 1f);
    public Color obstacleColor = Color.white;
    public Color trackColor = new Color(0.2f, 0.6f, 1f, 1f);
    public bool sphereRotates = true;

    public float BeatToTime(float beat) => offset + beat * 60f / Mathf.Max(1f, bpm);
    public float TimeToBeat(float time) => (time - offset) * Mathf.Max(1f, bpm) / 60f;
    public void SortByTime() => events.Sort((a, b) => a.time.CompareTo(b.time));
}

public static class RkslFile
{
    const string ManifestName = "level.json";

    public static bool Save(string rkslPath, RkslManifest manifest, string audioSourcePath, string videoSourcePath, string coverSourcePath, Sprite coverSprite = null)
    {
        try
        {
            // подготовка временных данных
            if (string.IsNullOrEmpty(manifest.audioFile) && !string.IsNullOrEmpty(audioSourcePath))
                manifest.audioFile = Path.GetFileName(audioSourcePath);
            if (string.IsNullOrEmpty(manifest.videoFile) && !string.IsNullOrEmpty(videoSourcePath))
                manifest.videoFile = Path.GetFileName(videoSourcePath);

            // cover: если есть Sprite — кодируем его, иначе копируем файл
            byte[] coverBytes = null;
            string coverFileName = manifest.coverFile;
            if (coverSprite != null && coverSprite.texture != null)
            {
                try
                {
                    Texture2D tex = coverSprite.texture;
                    // если текстура нечитаемая — делаем копию через RenderTexture
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
            }

            string json = JsonUtility.ToJson(manifest, true);
            string dir = Path.GetDirectoryName(rkslPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(rkslPath)) File.Delete(rkslPath);

            using (var zip = ZipFile.Open(rkslPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(ManifestName, System.IO.Compression.CompressionLevel.Optimal);
                using (var s = entry.Open())
                using (var w = new StreamWriter(s)) w.Write(json);

                if (!string.IsNullOrEmpty(audioSourcePath) && File.Exists(audioSourcePath))
                {
                    string name = Path.GetFileName(audioSourcePath);
                    zip.CreateEntryFromFile(audioSourcePath, name, System.IO.Compression.CompressionLevel.Optimal);
                    manifest.audioFile = name;
                }
                if (!string.IsNullOrEmpty(videoSourcePath) && File.Exists(videoSourcePath))
                {
                    string name = Path.GetFileName(videoSourcePath);
                    zip.CreateEntryFromFile(videoSourcePath, name, System.IO.Compression.CompressionLevel.Optimal);
                    manifest.videoFile = name;
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
                    manifest.coverFile = name;
                }
                // перезаписываем манифест с финальными именами файлов (если они изменились)
                // удаляем старый и создаём новый — проще пересоздать
                // Но ZipFile не позволяет легко перезаписать — поэтому сначала создавали манифест с предварительными именами,
                // а теперь файлы уже добавлены, манифест уже финальный — если имя изменилось, перезапишем через временный поток
                // Для простоты: если имя изменилось, обновим json и перезапишем entry (удалим и создадим заново)
                // Но в нашем коде имена уже финальные до создания, так что ок
            }
            // переоткроем и обновим манифест если имена файлов изменились после добавления (на случай если audioFile был пустой)
            // перезапишем манифест окончательно
            using (var zip = ZipFile.Open(rkslPath, ZipArchiveMode.Update))
            {
                var old = zip.GetEntry(ManifestName);
                if (old != null) old.Delete();
                var entry = zip.CreateEntry(ManifestName, System.IO.Compression.CompressionLevel.Optimal);
                using (var s = entry.Open())
                using (var w = new StreamWriter(s)) w.Write(JsonUtility.ToJson(manifest, true));
            }

            Debug.Log($"[Rksl] Saved {rkslPath} ({manifest.events.Count} нот)");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Rksl] Save failed {rkslPath}: {e}");
            return false;
        }
    }

    public static bool LoadManifestOnly(string rkslPath, out RkslManifest manifest)
    {
        manifest = null;
        try
        {
            using (var zip = ZipFile.OpenRead(rkslPath))
            {
                var entry = zip.GetEntry(ManifestName);
                if (entry == null) return false;
                using (var s = entry.Open())
                using (var r = new StreamReader(s))
                {
                    string json = r.ReadToEnd();
                    manifest = JsonUtility.FromJson<RkslManifest>(json);
                    return manifest != null;
                }
            }
        }
        catch (Exception e) { Debug.LogError($"[Rksl] LoadManifest {rkslPath}: {e}"); return false; }
    }

    /// <summary>
    /// Распаковывает .rksl во временную папку и возвращает пути к медиа + манифест
    /// </summary>
    public static bool Extract(string rkslPath, string extractDir, out RkslManifest manifest, out string audioPath, out string videoPath, out string coverPath)
    {
        manifest = null; audioPath = null; videoPath = null; coverPath = null;
        try
        {
            if (!File.Exists(rkslPath)) return false;
            Directory.CreateDirectory(extractDir);
            // чистим старую распаковку
            if (Directory.Exists(extractDir))
            {
                foreach (var f in Directory.GetFiles(extractDir)) try { File.Delete(f); } catch {}
                foreach (var d in Directory.GetDirectories(extractDir)) try { Directory.Delete(d, true); } catch {}
            }
            else Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(rkslPath, extractDir);
            string manifestPath = Path.Combine(extractDir, ManifestName);
            if (!File.Exists(manifestPath)) return false;
            string json = File.ReadAllText(manifestPath);
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

    public static RhythmLevelData ToRuntimeData(RkslManifest m, AudioClip audioClip, UnityEngine.Video.VideoClip videoClip, Sprite cover)
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
        data.music = audioClip;
        data.video = videoClip;
        data.cover = cover;
        data.audioPath = m.audioFile;
        data.videoPath = m.videoFile;
        return data;
    }

    public static RkslManifest FromRuntimeData(RhythmLevelData data)
    {
        return new RkslManifest
        {
            title = data.fullTitle,
            artist = data.songAuthor,
            creator = data.mapAuthor,
            bpm = data.bpm,
            offset = data.offset,
            events = new List<ObstacleEvent>(data.events),
            particlesEnabled = data.particlesEnabled,
            particleColor = data.particleColor,
            obstacleColor = data.obstacleColor,
            trackColor = data.trackColor,
            sphereRotates = data.sphereRotates,
            audioFile = !string.IsNullOrEmpty(data.audioPath) ? Path.GetFileName(data.audioPath) : "",
            videoFile = !string.IsNullOrEmpty(data.videoPath) ? Path.GetFileName(data.videoPath) : "",
            coverFile = data.cover != null ? "cover.png" : ""
        };
    }
}
