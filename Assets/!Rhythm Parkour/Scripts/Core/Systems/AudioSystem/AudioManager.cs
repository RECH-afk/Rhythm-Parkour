using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Zenject;
using RKS.HadalZone.Core;

namespace RKS.HadalZone.Core.Managers
{
    public class AudioManager : RKSBehaviour
    {
        [Header("Settings")]
        [Tooltip("Number of AudioSources to pool for simultaneous sounds.")]
        [SerializeField] private int poolSize = 10;

        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;

        [Header("Sound Libraries")]
        [Tooltip("List of sound libraries that contain sound data.")]
        [SerializeField] private SoundLibrary[] libraries;

        private readonly Dictionary<string, SoundData> soundMap = new Dictionary<string, SoundData>();
        private readonly List<AudioSource> audioPool = new List<AudioSource>();
        private int poolIndex = 0;

        private AudioSource musicSourceA;
        private AudioSource musicSourceB;
        private bool isPlayingMusicA = true;

        [Inject]
        public void Construct()
        {
            Initialize();
        }

        private void Initialize()
        {
            CreatePool();
            LoadLibraries();
            CreateMusicSources();

            Debug.Log("[AudioManager] Initialized successfully via Zenject.");
        }

        #region Initialization
        private void CreatePool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new GameObject($"AudioSource_{i}");
                go.transform.parent = transform;
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                audioPool.Add(source);
            }
        }

        private void LoadLibraries()
        {
            soundMap.Clear();

            foreach (var lib in libraries)
            {
                if (lib == null || lib.sounds == null)
                    continue;

                foreach (var sound in lib.sounds)
                {
                    if (sound == null || string.IsNullOrEmpty(sound.soundName))
                        continue;

                    if (!soundMap.ContainsKey(sound.soundName))
                        soundMap.Add(sound.soundName, sound);
                    else
                        Debug.LogWarning($"[AudioManager] Duplicate sound name detected: {sound.soundName}");
                }
            }
        }

        private void CreateMusicSources()
        {
            musicSourceA = gameObject.AddComponent<AudioSource>();
            musicSourceB = gameObject.AddComponent<AudioSource>();
            musicSourceA.loop = true;
            musicSourceB.loop = true;
        }
        #endregion

        #region Core Play Methods
        private AudioSource GetPooledSource()
        {
            AudioSource source = audioPool[poolIndex];
            poolIndex = (poolIndex + 1) % audioPool.Count;
            return source;
        }

        public AudioSource Play(string soundName)
        {
            return PlayAt(soundName, Camera.main ? Camera.main.transform.position : Vector3.zero);
        }

        public AudioSource PlayAt(string soundName, Vector3 position)
        {
            if (!soundMap.TryGetValue(soundName, out var data))
            {
                Debug.LogWarning($"[AudioManager] Sound not found: {soundName}");
                return null;
            }

            AudioSource source = GetPooledSource();
            if (data.clips == null || data.clips.Length == 0)
            {
                Debug.LogWarning($"[AudioManager] Sound has no clips: {soundName}");
                return null;
            }

            source.clip = data.clips[Random.Range(0, data.clips.Length)];
            source.volume = data.volume * masterVolume;
            source.pitch = data.pitch;
            source.loop = data.loop;
            source.spatialBlend = data.spatial ? 1f : 0f;
            source.transform.position = position;
            source.Play();

            return source;
        }

        public void Stop(string soundName)
        {
            foreach (var source in audioPool)
            {
                if (source.isPlaying && source.clip != null && source.clip.name == soundName)
                    source.Stop();
            }
        }

        public void StopAll()
        {
            foreach (var source in audioPool)
                source.Stop();

            musicSourceA.Stop();
            musicSourceB.Stop();
        }

        public bool IsPlaying(string soundName)
        {
            foreach (var source in audioPool)
            {
                if (source.isPlaying && source.clip != null && source.clip.name == soundName)
                    return true;
            }
            return false;
        }
        #endregion

        #region Volume Controls
        public void SetVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }

        public float GetVolume() => masterVolume;
        #endregion

        #region Extra Play Methods
        public void PlayOneShot(string soundName)
        {
            if (!soundMap.TryGetValue(soundName, out var data)) return;
            if (data.clips == null || data.clips.Length == 0) return;

            AudioClip clip = data.clips[Random.Range(0, data.clips.Length)];

            GameObject go = new GameObject($"OneShot_{soundName}");
            go.transform.SetParent(transform);
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = data.volume * masterVolume;
            src.pitch = data.pitch;
            src.spatialBlend = 0f;
            src.loop = false;

            src.Play();

            Destroy(go, clip.length / Mathf.Abs(src.pitch));
        }


        public void PlayAndForget(string soundName, Vector3? position = null)
        {
            if (!soundMap.TryGetValue(soundName, out var data)) return;

            Vector3 pos = position ?? (Camera.main ? Camera.main.transform.position : Vector3.zero);
            GameObject temp = new GameObject($"TempAudio_{soundName}");
            temp.transform.position = pos;

            AudioSource src = temp.AddComponent<AudioSource>();
            src.clip = data.clips[Random.Range(0, data.clips.Length)];
            src.volume = data.volume * masterVolume;
            src.pitch = data.pitch;
            src.loop = false;
            src.spatialBlend = data.spatial ? 1f : 0f;
            src.Play();

            Destroy(temp, src.clip.length / src.pitch);
        }

        public IEnumerator PlayWithDelay(string soundName, float delay, Vector3 pos)
        {
            yield return new WaitForSeconds(delay);
            PlayAt(soundName, pos);
        }

        public float GetClipLength(string soundName)
        {
            return soundMap.TryGetValue(soundName, out var data) && data.clips.Length > 0
                ? data.clips[0].length
                : 0f;
        }

        public bool HasSound(string soundName) => soundMap.ContainsKey(soundName);
        #endregion

        #region Pause / Resume
        public void PauseAll()
        {
            foreach (var s in audioPool) s.Pause();
            musicSourceA.Pause();
            musicSourceB.Pause();
        }

        public void ResumeAll()
        {
            foreach (var s in audioPool) s.UnPause();
            musicSourceA.UnPause();
            musicSourceB.UnPause();
        }
        #endregion

        #region Fading
        public void FadeIn(string soundName, float duration)
        {
            StartCoroutine(FadeInCoroutine(soundName, duration));
        }

        public void FadeOut(string soundName, float duration)
        {
            StartCoroutine(FadeOutCoroutine(soundName, duration));
        }

        private IEnumerator FadeInCoroutine(string soundName, float duration)
        {
            AudioSource src = Play(soundName);
            if (src == null) yield break;

            src.volume = 0f;
            float target = soundMap[soundName].volume * masterVolume;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(0f, target, t / duration);
                yield return null;
            }
        }

        private IEnumerator FadeOutCoroutine(string soundName, float duration)
        {
            foreach (var src in audioPool)
            {
                if (src.isPlaying && src.clip != null && src.clip.name == soundName)
                {
                    float startVol = src.volume;
                    float t = 0f;

                    while (t < duration)
                    {
                        t += Time.deltaTime;
                        src.volume = Mathf.Lerp(startVol, 0f, t / duration);
                        yield return null;
                    }

                    src.Stop();
                }
            }
        }
        #endregion

        #region Music System
        public void PlayMusic(AudioClip clip, float fadeTime = 1f)
        {
            StartCoroutine(CrossfadeMusic(clip, fadeTime));
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip, float fadeTime)
        {
            AudioSource active = isPlayingMusicA ? musicSourceA : musicSourceB;
            AudioSource next = isPlayingMusicA ? musicSourceB : musicSourceA;

            next.clip = newClip;
            next.volume = 0f;
            next.loop = true;
            next.Play();

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                active.volume = Mathf.Lerp(1f, 0f, t / fadeTime);
                next.volume = Mathf.Lerp(0f, 1f, t / fadeTime);
                yield return null;
            }

            active.Stop();
            isPlayingMusicA = !isPlayingMusicA;
        }
        #endregion
    }
}
