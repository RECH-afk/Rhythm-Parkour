using UnityEngine;

public class Conductor : MonoBehaviour
{
    public static Conductor Instance { get; private set; }
    public AudioSource musicSource;
    public RhythmLevelData levelData;
    public double dspSongStartTime;
    public float songPosition;
    public float songPositionBeats;
    public bool isPlaying;
    public float bpm => levelData != null ? levelData.bpm : 120f;
    public float offset => levelData != null ? levelData.offset : 0f;
    public float secPerBeat => 60f / bpm;
    public System.Action onBeat;
    public System.Action<float> onBeatFloat;
    public System.Action onSongFinished;
    private float _lastBeat = -1f;
    private bool _finishSent;
    [Tooltip("Пауза после конца трека перед экраном результатов (сек). 0.1–0.2 = итог сразу")]
    public float finishDelay = 0.15f;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Play(RhythmLevelData data, AudioSource src = null)
    {
        levelData = data;
        if (src != null) musicSource = src;
        if (musicSource == null) musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (data != null && data.music != null) { musicSource.clip = data.music; musicSource.playOnAwake = false; musicSource.loop = false; }
        dspSongStartTime = AudioSettings.dspTime + 0.1;
        if (musicSource.clip != null) musicSource.PlayScheduled(dspSongStartTime);
        isPlaying = true; _lastBeat = -1f; songPosition = -0.1f; _finishSent = false;
    }
    public void Stop() { isPlaying = false; if (musicSource != null) musicSource.Stop(); songPosition = 0f; songPositionBeats = 0f; }

    void Update()
    {
        if (!isPlaying || levelData == null || musicSource == null || musicSource.clip == null) return;
        songPosition = (float)(AudioSettings.dspTime - dspSongStartTime);
        if (songPosition < 0f) songPosition = musicSource.time;
        songPositionBeats = (songPosition - offset) / secPerBeat;
        float curBeat = Mathf.Floor(songPositionBeats);
        if (curBeat != _lastBeat && curBeat >= 0) { _lastBeat = curBeat; onBeat?.Invoke(); onBeatFloat?.Invoke(curBeat); }
        if (!musicSource.isPlaying && songPosition > musicSource.clip.length + Mathf.Max(0f, finishDelay))
        {
            isPlaying = false;
            if (!_finishSent) { _finishSent = true; try { onSongFinished?.Invoke(); } catch {} }
        }
    }
    public float GetTimeAtBeat(float beat) => offset + beat * secPerBeat;
}