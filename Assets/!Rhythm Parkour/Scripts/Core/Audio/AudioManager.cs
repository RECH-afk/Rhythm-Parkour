using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Zenject;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Managers
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
            PlayMusic(clip, fadeTime, 0f);
        }

        public void PlayMusic(AudioClip clip, float fadeTime, float startTime)
        {
            StartCoroutine(CrossfadeMusic(clip, fadeTime, startTime));
        }

        public void StopMusic()
        {
            if (musicSourceA != null) musicSourceA.Stop();
            if (musicSourceB != null) musicSourceB.Stop();
        }

        public void SetMusicLoop(bool loop)
        {
            if (musicSourceA != null) musicSourceA.loop = loop;
            if (musicSourceB != null) musicSourceB.loop = loop;
        }

        public bool IsMusicPlaying()
        {
            return (musicSourceA != null && musicSourceA.isPlaying)
                || (musicSourceB != null && musicSourceB.isPlaying);
        }

        public float GetMusicTime()
        {
            AudioSource next = isPlayingMusicA ? musicSourceB : musicSourceA;
            if (next != null && next.isPlaying && next.clip != null) return next.time;
            AudioSource active = isPlayingMusicA ? musicSourceA : musicSourceB;
            if (active != null && active.isPlaying && active.clip != null) return active.time;
            return -1f;
        }

        private readonly float[] _musicSpectrum = new float[512];
        private readonly float[] _fluxHistory = new float[64];
        private int _fluxIndex;
        private float _prevBassEnergy;
        private float _musicLevel;
        private float _beatCooldownT;
        private int _bassFrame = -1;
        private float _gridLastBpm = -1f;
        private float _gridLastOffset;
        private int _gridLastBeat = -1;
        private bool _beatArmed = true;

        public float GetMusicLevel() => GetBassLevel();

        public float GetBassLevel()
        {
            EnsureBassFrame();
            return _musicLevel;
        }

        public bool PollBassBeat(out float intensity)
        {
            return PollBassBeat(1.4f, 0.12f, out intensity);
        }

        public bool PollBassBeat(float sensitivity, float cooldown, out float intensity)
        {
            const float floor = 0.004f;
            intensity = 0f;
            EnsureBassFrame();
            _beatCooldownT -= Time.unscaledDeltaTime;
            if (_beatCooldownT > 0f) return false;
            FluxStats(out float mean, out float std, out float latest);
            float threshold = mean + Mathf.Max(0.5f, sensitivity) * (std + 1e-4f);
            float triggerAt = Mathf.Max(threshold, floor);
            if (latest > triggerAt)
            {
                intensity = Mathf.Clamp01((latest - triggerAt) / Mathf.Max(triggerAt, 1e-6f));
                _beatCooldownT = Mathf.Max(0.05f, cooldown);
                return true;
            }
            return false;
        }

        public bool PollBassBeat(float bpm, float offset, float sensitivity, float cooldown, out float intensity)
        {
            const float floor = 0.004f;
            intensity = 0f;
            EnsureBassFrame();
            float t = GetMusicTime();
            if (t < 0f) return false;
            if (Mathf.Abs(bpm - _gridLastBpm) > 0.01f || Mathf.Abs(offset - _gridLastOffset) > 0.001f)
            {
                _gridLastBpm = bpm;
                _gridLastOffset = offset;
                _gridLastBeat = -1;
                _beatArmed = true;
            }
            _beatCooldownT -= Time.unscaledDeltaTime;
            FluxStats(out float mean, out float std, out float latest);
            float threshold = mean + Mathf.Max(0.5f, sensitivity) * (std + 1e-4f);
            float triggerAt = Mathf.Max(threshold, floor);
            float spb = 60f / Mathf.Max(1f, bpm);
            float beatFloat = (t - offset) / spb;
            int beatIdx = Mathf.FloorToInt(beatFloat);
            float frac = beatFloat - beatIdx;
            float dist = Mathf.Min(frac, 1f - frac);
            bool crossed = beatIdx != _gridLastBeat;
            if (crossed) _gridLastBeat = beatIdx;
            float onset = latest > triggerAt ? latest - triggerAt : 0f;
            if (latest < triggerAt * 0.8f) _beatArmed = true;
            bool fire = false;
            float strength = 0f;
            if (_beatCooldownT <= 0f && _beatArmed)
            {
                if (onset > 0f && dist <= 0.12f)
                {
                    fire = true;
                    strength = Mathf.Clamp01(onset / Mathf.Max(triggerAt, 1e-6f)) * (1f - dist / 0.12f);
                }
                else if (onset > 0f && latest > triggerAt * 1.6f)
                {
                    fire = true;
                    strength = Mathf.Clamp01(onset / Mathf.Max(triggerAt, 1e-6f));
                }
                else if (crossed && latest > triggerAt * 0.6f)
                {
                    fire = true;
                    strength = 0.25f * Mathf.Clamp01(latest / Mathf.Max(triggerAt, 1e-6f));
                }
            }
            if (fire)
            {
                intensity = Mathf.Clamp01(strength);
                _beatCooldownT = Mathf.Max(0.05f, cooldown);
                _beatArmed = false;
                return true;
            }
            return false;
        }

        private void FluxStats(out float mean, out float std, out float latest)
        {
            mean = 0f;
            for (int i = 0; i < _fluxHistory.Length; i++) mean += _fluxHistory[i];
            mean /= _fluxHistory.Length;
            float variance = 0f;
            for (int i = 0; i < _fluxHistory.Length; i++)
            {
                float d = _fluxHistory[i] - mean;
                variance += d * d;
            }
            variance /= _fluxHistory.Length;
            std = Mathf.Sqrt(variance);
            latest = _fluxHistory[(_fluxIndex + _fluxHistory.Length - 1) % _fluxHistory.Length];
        }

        private void EnsureBassFrame()
        {
            if (_bassFrame == Time.frameCount) return;
            _bassFrame = Time.frameCount;
            AudioSource src = null;
            AudioSource next = isPlayingMusicA ? musicSourceB : musicSourceA;
            if (next != null && next.isPlaying && next.clip != null) src = next;
            else
            {
                AudioSource active = isPlayingMusicA ? musicSourceA : musicSourceB;
                if (active != null && active.isPlaying && active.clip != null) src = active;
            }
            float energy = 0f;
            if (src != null)
            {
                src.GetSpectrumData(_musicSpectrum, 0, FFTWindow.BlackmanHarris);
                float binHz = (float)AudioSettings.outputSampleRate * 0.5f / _musicSpectrum.Length;
                int from = Mathf.Clamp(Mathf.FloorToInt(50f / Mathf.Max(1f, binHz)), 1, _musicSpectrum.Length - 1);
                int to = Mathf.Clamp(Mathf.CeilToInt(200f / Mathf.Max(1f, binHz)), from, _musicSpectrum.Length - 1);
                float sumSq = 0f;
                int count = 0;
                for (int i = from; i <= to; i++) { sumSq += _musicSpectrum[i] * _musicSpectrum[i]; count++; }
                if (count > 0) energy = Mathf.Sqrt(sumSq / count);
            }
            float flux = Mathf.Max(0f, energy - _prevBassEnergy);
            _prevBassEnergy = energy;
            _fluxHistory[_fluxIndex] = flux;
            _fluxIndex = (_fluxIndex + 1) % _fluxHistory.Length;
            _musicLevel = Mathf.Lerp(_musicLevel, Mathf.Clamp01(energy * 8f), Time.unscaledDeltaTime * 8f);
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip, float fadeTime, float startTime)
        {
            AudioSource active = isPlayingMusicA ? musicSourceA : musicSourceB;
            AudioSource next = isPlayingMusicA ? musicSourceB : musicSourceA;

            next.clip = newClip;
            next.volume = 0f;
            next.loop = true;
            if (startTime > 0f && newClip != null && startTime < newClip.length)
                next.time = startTime;
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
