using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Применяет визуальные настройки уровня (партиклы, цвета, сфера, видео).
/// Вызывается из IsGameSceneLoader / RhythmParkourManager после загрузки уровня.
/// </summary>
public static class LevelVisualApplier
{
    public static void Apply(RhythmLevelData data, VideoPlayer vp = null, bool isEditorPreview = false)
    {
        if (data == null) return;
        // Партиклы
        var allPs = Object.FindObjectsOfType<ParticleSystem>(true);
        foreach (var ps in allPs)
        {
            // Не трогаем партиклы которые на препятствиях (они в префабах, но в пуле)
            // Простой фильтр: если объект в сцене Environment или Particle System
            var main = ps.main;
            try
            {
                // Цвет
                if (data.particleColor != default)
                    main.startColor = data.particleColor;
            }
            catch {}
            // Вкл/выкл
            if (ps.gameObject.scene.IsValid())
            {
                // Не трогаем префабы в Project, только сценовые
                bool isSceneObj = ps.gameObject.scene.name != null;
                if (isSceneObj)
                    ps.gameObject.SetActive(data.particlesEnabled);
            }
        }
        // Пробуем найти через GameObject.Find если FindObjects не нашёл (неактивные)
        // Дорожка
        Transform ground = GameObject.Find("Ground")?.transform;
        if (ground == null)
        {
            var mgr = Object.FindObjectOfType<RhythmParkourManager>();
            if (mgr != null) ground = mgr.trackFloor;
        }
        if (ground != null)
        {
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
            {
                // Используем MaterialPropertyBlock чтобы не плодить инстансы
                var block = new MaterialPropertyBlock();
                rend.GetPropertyBlock(block);
                if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color"))
                    block.SetColor("_Color", data.trackColor);
                else if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_BaseColor"))
                    block.SetColor("_BaseColor", data.trackColor);
                else
                {
                    // fallback: создаём инстанс
                    try { rend.material.color = data.trackColor; } catch {}
                }
                rend.SetPropertyBlock(block);
            }
        }
        // Сфера
        var sphere = Object.FindObjectOfType<SphereBeatRotator>();
        if (sphere != null) sphere.enabled = data.sphereRotates;

        // Видео
        VideoPlayer player = vp;
        if (player == null)
            player = Object.FindObjectOfType<VideoPlayer>();
        if (player != null)
        {
            if (!string.IsNullOrEmpty(data.videoPath) && System.IO.File.Exists(data.videoPath))
            {
                string url = RkslFile.GetFileUri(data.videoPath);
                player.source = VideoSource.Url;
                player.url = url;
                player.isLooping = false;
                player.playOnAwake = false;
                Debug.Log($"[VisualApplier] Video url set {url}", player);
            }
            else if (data.video != null)
            {
                player.source = VideoSource.VideoClip;
                player.clip = data.video;
            }
            else
            {
                // нет видео — стопаем
                player.Stop();
                player.url = "";
                player.clip = null;
            }
        }
        // Материал по умолчанию — резолвим
        if (!string.IsNullOrEmpty(data.defaultObstacleMaterialName))
        {
            var mat = GlobalObstacleCatalog.GetMaterial(data.defaultObstacleMaterialName);
            if (mat != null)
            {
                data.defaultObstacleMaterial = mat;
                Debug.Log($"[VisualApplier] Default material resolved '{mat.name}'");
            }
            else Debug.LogWarning($"[VisualApplier] Material '{data.defaultObstacleMaterialName}' не найден в каталоге");
        }
    }
}
