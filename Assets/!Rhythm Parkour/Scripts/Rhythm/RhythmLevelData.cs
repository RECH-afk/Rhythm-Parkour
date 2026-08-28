using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public struct ObstacleEvent
{
    public float time;          // секунды
    public float beat;          // биты
    public int prefabIndex;     // индекс из палитры
    [HideInInspector] public Vector3 position;    // оффсет — игнорируется, всегда спавн в точке
    public Vector3 rotation;    // Euler
    public Vector3 scale;
    public string comment;

    public static ObstacleEvent Create(float beat, int prefabIndex, Vector3 pos)
    {
        return new ObstacleEvent
        {
            beat = beat,
            prefabIndex = prefabIndex,
            position = pos,
            rotation = Vector3.zero,
            scale = Vector3.one
        };
    }
}

[CreateAssetMenu(fileName = "NewRhythmLevel", menuName = "Rhythm Parkour/Level Data", order = 0)]
public class RhythmLevelData : ScriptableObject
{
    [Header("Музыка")]
    public AudioClip music;
    public VideoClip video;
    public float bpm = 128f;
    public float offset = 0f;

    [Header("Палитра префабов")]
    [Tooltip("Сюда перетяни префабы препятствий")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("Ноты")]
    public List<ObstacleEvent> events = new List<ObstacleEvent>();

    public float BeatToTime(float beat) => offset + beat * 60f / bpm;
    public float TimeToBeat(float time) => (time - offset) * bpm / 60f;

    public void SortByTime() => events.Sort((a, b) => a.time.CompareTo(b.time));

    public void SyncBeatsToTime()
    {
        for (int i = 0; i < events.Count; i++) { var e = events[i]; e.time = BeatToTime(e.beat); events[i] = e; }
        SortByTime();
    }
    public void SyncTimeToBeats()
    {
        for (int i = 0; i < events.Count; i++) { var e = events[i]; e.beat = TimeToBeat(e.time); events[i] = e; }
    }

#if UNITY_EDITOR
    void OnValidate() { bpm = Mathf.Max(1f, bpm); }
#endif

    public GameObject GetPrefab(int index)
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Count == 0) return null;
        if (index < 0 || index >= obstaclePrefabs.Count) return obstaclePrefabs[0];
        return obstaclePrefabs[index];
    }
}
