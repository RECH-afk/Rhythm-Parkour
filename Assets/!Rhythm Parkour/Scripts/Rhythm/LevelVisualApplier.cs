using System.Collections.Generic;
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
    public static class ParticleSpriteLibrary
    {
        public const string PresetPrefix = "preset:";
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static string[] PresetNames => new string[] { "SoftCircle", "Ring", "Spark", "Square", "Glow" };

        public static Sprite Get(string storedName)
        {
            if (string.IsNullOrEmpty(storedName)) return null;
            if (storedName.StartsWith(PresetPrefix))
            {
                string key = storedName.Substring(PresetPrefix.Length);
                if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
                var made = MakePreset(key);
                if (made != null) _cache[key] = made;
                return made;
            }
            return Resources.Load<Sprite>(storedName);
        }

        static Sprite MakePreset(string key)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = key;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float a = 0f;
                    if (key == "Ring")
                    {
                        float d = Mathf.Sqrt(u * u + v * v);
                        a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.62f) / 0.22f);
                    }
                    else if (key == "Spark")
                    {
                        float arm = Mathf.Min(Mathf.Abs(u) * 3f + Mathf.Abs(v) * 0.35f, Mathf.Abs(v) * 3f + Mathf.Abs(u) * 0.35f);
                        float d = Mathf.Sqrt(u * u + v * v);
                        a = Mathf.Clamp01(1f - arm) * Mathf.Clamp01(1f - d);
                    }
                    else if (key == "Square")
                    {
                        float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        a = Mathf.Clamp01(1f - (d - 0.55f) / 0.45f);
                    }
                    else if (key == "Glow")
                    {
                        float d = Mathf.Sqrt(u * u + v * v);
                        a = Mathf.Clamp01(1f - d * 1.6f);
                        if (d < 0.25f) a = 1f;
                    }
                    else
                    {
                        float d = Mathf.Sqrt(u * u + v * v);
                        a = Mathf.Clamp01(1f - d);
                        a *= a;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, false);
            var spr = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            spr.name = key;
            return spr;
        }
    }

    [System.Serializable]
    public class LevelVisualApplier
    {
        readonly GlobalObstacleCatalog _catalog;
        Transform _groundCache;

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

            var allPs = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ps in allPs)
            {
                var main = ps.main;
                try
                {
                    if (data.particleColor != default)
                        main.startColor = data.particleColor;
                }
                catch { }

                if (data.particleSprite == null && !string.IsNullOrEmpty(data.particleSpriteName))
                    data.particleSprite = ParticleSpriteLibrary.Get(data.particleSpriteName);

                if (data.particleSprite != null && data.particleSprite.texture != null)
                {
                    try
                    {
                        var pr = ps.GetComponent<ParticleSystemRenderer>();
                        if (pr != null)
                        {
                            var mat = pr.sharedMaterial;
                            if (mat != null && mat.mainTexture != data.particleSprite.texture)
                            {
                                if (!Application.isPlaying)
                                {
                                    var inst = new Material(mat);
                                    inst.mainTexture = data.particleSprite.texture;
                                    pr.sharedMaterial = inst;
                                }
                                else
                                {
                                    pr.material.mainTexture = data.particleSprite.texture;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (ps.gameObject.scene.IsValid())
                {
                    bool isSceneObj = ps.gameObject.scene.name != null;
                    if (isSceneObj)
                        ps.gameObject.SetActive(data.particlesEnabled);
                }
            }

            Transform ground = groundOverride;
            if (ground == null)
            {
                if (_groundCache == null)
                {
                    var go = GameObject.Find("Ground");
                    if (go != null) _groundCache = go.transform;
                }
                ground = _groundCache;
            }
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
                        try { rend.material.color = data.trackColor; } catch { }
                    }
                    rend.SetPropertyBlock(block);
                }
            }

            var sphere = sphereOverride;
            if (sphere == null)
            {
                sphere = Object.FindFirstObjectByType<SphereBeatRotator>();
            }
            if (sphere != null)
            {
                sphere.enabled = data.sphereRotates;
                var videoReactive = sphere.GetComponent<VideoReactiveController>();
                if (videoReactive != null) videoReactive.enabled = data.sphereUseVideo;
            }

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
                }
            }
        }
    }
}
