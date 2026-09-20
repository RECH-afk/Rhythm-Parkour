using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    [System.Serializable]
    public struct ObstacleEvent
    {
        public float time;
        public float beat;
        public int prefabIndex;
        [HideInInspector] public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public float speed;
        public Color color;
        public string comment;

        public bool HasCustomColor => color.a > 0.01f;
        public Color EffectiveColor(Color fallback) => HasCustomColor ? color : fallback;

        public static ObstacleEvent Create(float beat, int prefabIndex, Vector3 pos, float speed = 0f) => new ObstacleEvent
        {
            beat = beat,
            prefabIndex = prefabIndex,
            position = pos,
            rotation = Vector3.zero,
            scale = Vector3.one,
            speed = speed,
            color = new Color(0,0,0,0)
        };
    }

    [System.Serializable]
    public class RhythmLevelData
    {
        [Header("Music")]
        public AudioClip music;
        public VideoClip video;
        public Sprite cover;
        public string fullTitle;
        public string songAuthor;
        public string mapAuthor;
        [Min(1)] public float bpm = 128f;
        public float offset = 0f;
        public string audioPath;
        public string videoPath;

        [Header("Level Visual")]
        public bool particlesEnabled = true;
        public Color particleColor = new Color(0.2f, 0.7f, 1f, 1f);
        [Tooltip("Имя спрайта частиц (сохраняется в .rksl). Пусто = спрайт по умолчанию из сцены")]
        public string particleSpriteName = "";
        [Tooltip("Рантайм-спрайт частиц (не сериализуется в .rksl, резолвится по имени или из файла)")]
        public Sprite particleSprite;
        public Color obstacleColor = Color.white;
        public Color trackColor = new Color(0.2f, 0.6f, 1f, 1f);
        public bool sphereRotates = true;
        [Tooltip("Использует ли сфера видео-текстуру (VideoReactive). Выкл = обычный материал сферы")]
        public bool sphereUseVideo = true;
        [Tooltip("Имя материала для препятствий по умолчанию (пусто = материал префаба). Сохраняется в .rksl")]
        public string defaultObstacleMaterialName = "";
        [Tooltip("Опционально прямой референс для редактора (не сериализуется в .rksl, синхронизируется по имени)")]
        public Material defaultObstacleMaterial;

        [Header("Notes")]
        public List<ObstacleEvent> events = new List<ObstacleEvent>();

        public float BeatToTime(float beat) => offset + beat * 60f / Mathf.Max(1f, bpm);
        public float TimeToBeat(float time) => (time - offset) * Mathf.Max(1f, bpm) / 60f;
        public float Duration => music ? music.length : 0f;
        public float TotalBeats => music ? TimeToBeat(music.length) : 0f;
        public void SortByTime() => events.Sort((a, b) => a.time.CompareTo(b.time));

        public GameObject GetPrefab(int index, GlobalObstacleCatalog catalog = null)
        {
            if (catalog == null) return null;
            return catalog.GetPrefab(index);
        }

        public int PrefabCount(GlobalObstacleCatalog catalog = null)
        {
            if (catalog == null) return 0;
            return catalog.Count;
        }

        public void OnValidate() => bpm = Mathf.Max(1f, bpm);
        public RhythmLevelData Clone()
        {
            return (RhythmLevelData)MemberwiseClone();
        }
        public RhythmLevelData CloneDeep()
        {
            var c = (RhythmLevelData)MemberwiseClone();
            c.events = new System.Collections.Generic.List<ObstacleEvent>(events ?? new System.Collections.Generic.List<ObstacleEvent>());
            return c;
        }
    }
}
