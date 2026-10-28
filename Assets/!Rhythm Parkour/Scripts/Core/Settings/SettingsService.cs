using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RKS.RhythmParkour.Core.Managers;

namespace RKS.RhythmParkour.Core.Settings
{
    public sealed class SettingsService : IDisposable
    {
        public const int WindowModeCount = 3;
        public const int TextureQualityCount = 4;
        public const int ShadowQualityCount = 4;

        readonly SaveManager _save;
        readonly AudioManager _audio;
        bool _initialized;

        public event Action OnChanged;
        public event Action<bool> OnBloomChanged;
        public event Action<bool> OnChromaticAberrationChanged;
        public event Action<bool> OnVersionLabelChanged;
        public event Action<bool> OnVolumeMixerChanged;

        public GameData.Data Data => _save != null ? _save.CurrentData : null;

        public SettingsService(SaveManager save, AudioManager audio)
        {
            _save = save;
            _audio = audio;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void Initialize()
        {
            if (_initialized || _save == null || _save.CurrentData == null) return;
            _initialized = true;

            var d = _save.CurrentData;

            if (d.windowModeIndex < 0 || d.windowModeIndex >= WindowModeCount) d.windowModeIndex = 0;
            if (d.textureQualityIndex < 0 || d.textureQualityIndex >= TextureQualityCount) d.textureQualityIndex = 0;
            if (d.shadowQualityIndex < 0 || d.shadowQualityIndex >= ShadowQualityCount) d.shadowQualityIndex = ShadowQualityCount - 1;

            if (d.resolutionWidth <= 0 || d.resolutionHeight <= 0)
            {
                d.resolutionWidth = Screen.currentResolution.width;
                d.resolutionHeight = Screen.currentResolution.height;
                d.resolutionRefreshHz = CurrentRefreshHz(Screen.currentResolution);
                _save.Write();
            }

            ApplyDisplay();
            ApplyTextureQuality(d.textureQualityIndex);
            ApplyShadowQuality(d.shadowQualityIndex);
            ApplyAudio();
            ApplyPostFx();
            NotifyChanged();
        }

        #region Public Setters (UI)

        public void SetWindowMode(int index)
        {
            var d = Data; if (d == null) return;
            index = Mathf.Clamp(index, 0, WindowModeCount - 1);
            if (d.windowModeIndex == index) return;
            d.windowModeIndex = index;
            _save.Write();
            ApplyDisplay();
            NotifyChanged();
        }

        public void SetResolution(int width, int height, int refreshHz)
        {
            var d = Data; if (d == null) return;
            if (d.resolutionWidth == width && d.resolutionHeight == height && d.resolutionRefreshHz == refreshHz) return;
            d.resolutionWidth = width;
            d.resolutionHeight = height;
            d.resolutionRefreshHz = refreshHz;
            _save.Write();
            ApplyDisplay();
            NotifyChanged();
        }

        public void SetTextureQuality(int index)
        {
            var d = Data; if (d == null) return;
            index = Mathf.Clamp(index, 0, TextureQualityCount - 1);
            if (d.textureQualityIndex == index) return;
            d.textureQualityIndex = index;
            _save.Write();
            ApplyTextureQuality(index);
            NotifyChanged();
        }

        public void SetShadowQuality(int index)
        {
            var d = Data; if (d == null) return;
            index = Mathf.Clamp(index, 0, ShadowQualityCount - 1);
            if (d.shadowQualityIndex == index) return;
            d.shadowQualityIndex = index;
            _save.Write();
            ApplyShadowQuality(index);
            NotifyChanged();
        }

        public void SetSoundVolume(float value, bool write = true)
        {
            var d = Data; if (d == null) return;
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(d.volumeValue, value)) return;
            d.volumeValue = value;
            if (write) _save.Write();
            ApplyAudio();
            NotifyChanged();
        }

        public void SetMusicVolume(float value, bool write = true)
        {
            var d = Data; if (d == null) return;
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(d.musicVolumeValue, value)) return;
            d.musicVolumeValue = value;
            if (write) _save.Write();
            ApplyAudio();
            NotifyChanged();
        }

        public void SetSoundsVolume(float value, bool write = true)
        {
            var d = Data; if (d == null) return;
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(d.soundsVolumeValue, value)) return;
            d.soundsVolumeValue = value;
            if (write) _save.Write();
            ApplyAudio();
            NotifyChanged();
        }

        public void Write()
        {
            _save?.Write();
        }

        public float GetSoundsVolume()
        {
            var d = Data;
            return d != null ? Mathf.Clamp01(d.soundsVolumeValue) : 1f;
        }

        public void SetBloom(bool enabled)
        {
            var d = Data; if (d == null) return;
            if (d.bloomEnabled == enabled) return;
            d.bloomEnabled = enabled;
            _save.Write();
            OnBloomChanged?.Invoke(enabled);
            ApplyPostFx();
            NotifyChanged();
        }

        public void SetChromaticAberration(bool enabled)
        {
            var d = Data; if (d == null) return;
            if (d.chromaticAberrationEnabled == enabled) return;
            d.chromaticAberrationEnabled = enabled;
            _save.Write();
            OnChromaticAberrationChanged?.Invoke(enabled);
            ApplyPostFx();
            NotifyChanged();
        }

        public void SetVersionLabel(bool enabled)
        {
            var d = Data; if (d == null) return;
            if (d.showVersionLabel == enabled) return;
            d.showVersionLabel = enabled;
            _save.Write();
            OnVersionLabelChanged?.Invoke(enabled);
            NotifyChanged();
        }

        public void SetVolumeMixerEnabled(bool enabled)
        {
            var d = Data; if (d == null) return;
            if (d.volumeMixerEnabled == enabled) return;
            d.volumeMixerEnabled = enabled;
            _save.Write();
            OnVolumeMixerChanged?.Invoke(enabled);
            NotifyChanged();
        }

        public bool IsVolumeMixerEnabled()
        {
            var d = Data;
            return d == null || d.volumeMixerEnabled;
        }

        #endregion

        #region Applying

        void ApplyDisplay()
        {
            var d = Data; if (d == null) return;

            FullScreenMode mode = MapWindowMode(d.windowModeIndex);
            int w = d.resolutionWidth > 0 ? d.resolutionWidth : Screen.currentResolution.width;
            int h = d.resolutionHeight > 0 ? d.resolutionHeight : Screen.currentResolution.height;
            int hz = d.resolutionRefreshHz > 0 ? d.resolutionRefreshHz : CurrentRefreshHz(Screen.currentResolution);

            var refresh = new RefreshRate
            {
                numerator = (uint)Mathf.Max(1, hz),
                denominator = 1u,
            };

            if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            Screen.SetResolution(w, h, mode, refresh);
        }

        public static FullScreenMode MapWindowMode(int index) => index switch
        {
            0 => FullScreenMode.FullScreenWindow,
            1 => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.Windowed,
        };

        void ApplyTextureQuality(int index)
        {
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(index, 0, TextureQualityCount - 1);
        }

        void ApplyShadowQuality(int index)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Debug.LogWarning("[Settings] URP asset not found, shadow quality not applied.");
                return;
            }

            switch (Mathf.Clamp(index, 0, ShadowQualityCount - 1))
            {
                case 0:
                    urp.shadowDistance = 0f;
                    break;
                case 1:
                    urp.mainLightShadowmapResolution = 1024;
                    urp.shadowCascadeCount = 1;
                    urp.shadowDistance = 20f;
                    break;
                case 2:
                    urp.mainLightShadowmapResolution = 2048;
                    urp.shadowCascadeCount = 2;
                    urp.shadowDistance = 40f;
                    break;
                default:
                    urp.mainLightShadowmapResolution = 4096;
                    urp.shadowCascadeCount = 4;
                    urp.shadowDistance = 60f;
                    break;
            }
        }

        void ApplyAudio()
        {
            var d = Data; if (d == null || _audio == null) return;
            _audio.SetVolume(Mathf.Clamp01(d.volumeValue));
            _audio.SetMusicVolume(Mathf.Clamp01(d.musicVolumeValue));
            _audio.SetSoundsVolume(Mathf.Clamp01(d.soundsVolumeValue));
        }

        public void ApplyPostFx()
        {
            var d = Data; if (d == null) return;

            bool bloomOn = d.bloomEnabled;
            bool caOn = d.chromaticAberrationEnabled;

            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var volume in volumes)
            {
                if (volume == null || volume.sharedProfile == null) continue;

                VolumeProfile profile = volume.profile;
                if (profile == null) continue;

                if (profile.TryGet<Bloom>(out var bloom))
                    bloom.active = bloomOn;

                if (profile.TryGet<ChromaticAberration>(out var ca))
                    ca.active = caOn;
            }
        }

        void NotifyChanged() => OnChanged?.Invoke();

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_initialized) ApplyPostFx();
        }

        static int CurrentRefreshHz(Resolution r)
        {
            return Mathf.RoundToInt(r.refreshRateRatio.numerator / (float)Mathf.Max(1u, r.refreshRateRatio.denominator));
        }

        #endregion
    }
}
