using UnityEngine;
using UnityEngine.Video;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    [System.Serializable]
    public class LevelVisualApplier
    {
        readonly GlobalObstacleCatalog _catalog;

        [Inject]
        public LevelVisualApplier(GlobalObstacleCatalog catalog)
        {
            _catalog = catalog;
        }

        public void Apply(
            RhythmLevelData data,
            VideoPlayer vp = null,
            bool isEditorPreview = false,
            Transform groundOverride = null,
            SphereBeatRotator sphereOverride = null)
        {
            if (data == null) return;

            var allPs = Object.FindObjectsOfType<ParticleSystem>(true);
            foreach (var ps in allPs)
            {

var main = ps.main;
                try
                {

                    if (data.particleColor != default)
                        main.startColor = data.particleColor;
                }
                catch {}

                if (ps.gameObject.scene.IsValid())
                {

                    bool isSceneObj = ps.gameObject.scene.name != null;
                    if (isSceneObj)
                        ps.gameObject.SetActive(data.particlesEnabled);
                }
            }

Transform ground = groundOverride;
            if (ground != null)
            {
                var rend = ground.GetComponent<Renderer>();
                if (rend != null)
                {

                    var block = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(block);
                    if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_Color"))
                        block.SetColor("_Color", data.trackColor);
                    else if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_BaseColor"))
                        block.SetColor("_BaseColor", data.trackColor);
                    else
                    {

                        try { rend.material.color = data.trackColor; } catch {}
                    }
                    rend.SetPropertyBlock(block);
                }
            }

            var sphere = sphereOverride;
            if (sphere != null) sphere.enabled = data.sphereRotates;

VideoPlayer player = vp;
            if (player != null)
            {
                if (!string.IsNullOrEmpty(data.videoPath) && System.IO.File.Exists(data.videoPath))
                {
                    string url = RkslStore.GetFileUri(data.videoPath);
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

                    player.Stop();
                    player.url = "";
                    player.clip = null;
                }
            }

            if (!string.IsNullOrEmpty(data.defaultObstacleMaterialName))
            {
                var mat = _catalog != null ? _catalog.GetMaterial(data.defaultObstacleMaterialName) : null;
                if (mat != null)
                {
                    data.defaultObstacleMaterial = mat;
                    Debug.Log($"[VisualApplier] Default material resolved '{mat.name}'");
                }
                else Debug.LogWarning($"[VisualApplier] Material '{data.defaultObstacleMaterialName}' не найден в каталоге");
            }
        }
    }
}
