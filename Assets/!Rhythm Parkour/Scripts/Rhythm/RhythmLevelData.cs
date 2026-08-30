using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public struct ObstacleEvent
{
    public float time;       // спавн (сек) — считается автоматом из beat
    public float beat;       // хит-бит (когда у игрока) — главное поле
    public int prefabIndex;
    [HideInInspector] public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
    public float speed;      // 0 = baseSpeed префаба
    public string comment;

    public static ObstacleEvent Create(float beat, int prefabIndex, Vector3 pos, float speed = 0f) => new ObstacleEvent
    {
        beat = beat,
        prefabIndex = prefabIndex,
        position = pos,
        rotation = Vector3.zero,
        scale = Vector3.one,
        speed = speed
    };
}

[CreateAssetMenu(fileName = "NewRhythmLevel", menuName = "Rhythm Parkour/Level Data", order = 0)]
public class RhythmLevelData : ScriptableObject
{
    [Header("Музыка")]
    public AudioClip music;
    public VideoClip video;
    public Sprite cover;
    public string fullTitle;      // полное название как в UI
    public string songAuthor;     // автор песни
    public string mapAuthor;      // автор карты
    [Min(1)] public float bpm = 128f;
    public float offset = 0f;
    public string audioPath;      // путь к файлу на диске (для рантайма, file://)
    public string videoPath;

    [Header("Визуал карты")]
    public bool particlesEnabled = true;
    public Color particleColor = new Color(0.2f, 0.7f, 1f, 1f);
    public Color obstacleColor = Color.white;
    public Color trackColor = new Color(0.2f, 0.6f, 1f, 1f);
    public bool sphereRotates = true;

    [Header("Префабы")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("Ноты — ХИТ у игрока")]
    public List<ObstacleEvent> events = new List<ObstacleEvent>();

    // ── удобно ──
    public float BeatToTime(float beat) => offset + beat * 60f / bpm;
    public float TimeToBeat(float time) => (time - offset) * bpm / 60f;
    public float Duration => music ? music.length : 0f;
    public float TotalBeats => music ? TimeToBeat(music.length) : 0f;

    public void SortByTime() => events.Sort((a, b) => a.time.CompareTo(b.time));

    public GameObject GetPrefab(int index)
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Count == 0) return null;
        if (index < 0 || index >= obstaclePrefabs.Count) return obstaclePrefabs[0];
        return obstaclePrefabs[index];
    }

#if UNITY_EDITOR
    void OnValidate() => bpm = Mathf.Max(1f, bpm);
#endif
}
