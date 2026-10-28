using System.Collections.Generic;
using System.Linq;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Settings;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.UI
{
    public sealed class SettingsController : RKSBehaviour
    {
        #region Inspector References

        [Header("Update Server")]
        [Tooltip("URL JSON-манифеста обновлений. Пусто — берётся UpdateChecker.manifestUrl")]
        [SerializeField] private string _updateManifestUrl = "";
        [Tooltip("Пауза между проверками обновлений, сек")]
        [SerializeField] private float updateCheckCooldownSeconds = 10f;

        [Header("Game Tab")]
        [SerializeField] private TMP_Dropdown _languageDropdown;
        [SerializeField] private OptionStatusBar _languageStatus;
        [SerializeField] private OptionStatusBar _updatesStatus;
        [SerializeField] private Button _checkUpdatesButton;

        [Header("Graphics Tab")]
        [SerializeField] private TMP_Dropdown _windowModeDropdown;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private TMP_Dropdown _textureDropdown;
        [SerializeField] private TMP_Dropdown _shadowsDropdown;
        [SerializeField] private Toggle _bloomToggle;
        [SerializeField] private Toggle _chromaticAberrationToggle;
        [SerializeField] private Toggle _versionToggle;

        [Header("Audio Tab")]
        [SerializeField] private Slider _generalVolumeSlider;
        [SerializeField] private TMP_Text _generalVolumeLabel;
        [SerializeField] private Slider _soundVolumeSlider;
        [SerializeField] private TMP_Text _soundVolumeLabel;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private TMP_Text _musicVolumeLabel;
        [SerializeField] private Toggle _volumeMixerToggle;

        [HideInInspector]
        [InjectOptional] public SettingsService settings;
        [HideInInspector]
        [InjectOptional] public MenuController menu;
        [HideInInspector]
        [InjectOptional] public UpdateChecker updateChecker;

        #endregion

        #region Private Fields

        static readonly (string code, string label)[] Languages =
        {
            ("en_US", "English"),
            ("ru_RU", "Русский")
        };

        readonly List<ResolutionOption> _resolutions = new();
        LocalizationManager _localization;
        string _pendingLanguage;
        bool _checkOnCooldown;
        float _nextCheckAllowedAt;
        bool _settingsWasVisible;

        #endregion

        #region Unity Lifecycle

        protected override void OnInjected()
        {
            ValidateReferences();
        }

        protected override void OnReady()
        {
            _localization = Localization;

            if (_localization != null)
            {
                _localization.OnLanguageChanged += OnLanguageChanged;
                _localization.OnLanguageLoadFailed += OnLanguageLoadFailed;
            }

            if (settings != null)
            {
                settings.Initialize();
                settings.OnChanged += OnSettingsChanged;
            }

            PopulateAll();
            SyncValues();
            RefreshStatusBars();
        }

        void OnSettingsChanged() => SyncVolumeControls();

        protected override void OnDisposed()
        {
            if (_localization != null)
            {
                _localization.OnLanguageChanged -= OnLanguageChanged;
                _localization.OnLanguageLoadFailed -= OnLanguageLoadFailed;
            }
            if (updateChecker != null) updateChecker.OnResult -= OnUpdateResult;
            if (settings != null) settings.OnChanged -= OnSettingsChanged;
        }

        protected override void Update()
        {
            UpdateCheckCooldown();
            UpdateSettingsVisibility();
        }

        void UpdateCheckCooldown()
        {
            if (!_checkOnCooldown) return;
            if (updateChecker != null && updateChecker.isChecking) return;
            if (Time.unscaledTime < _nextCheckAllowedAt) return;
            _checkOnCooldown = false;
            if (_checkUpdatesButton != null) _checkUpdatesButton.interactable = true;
        }

        void UpdateSettingsVisibility()
        {
            if (menu == null) return;
            bool visible = menu.IsSettingsVisible;
            if (visible && !_settingsWasVisible) RefreshStatusBars();
            _settingsWasVisible = visible;
        }

        void RefreshStatusBars()
        {
            if (_localization != null && _localization.isReady)
                _languageStatus?.SetStatus(
                    string.Format(Loc("settings.language.status.current"), LanguageLabel(_localization.currentLanguage)),
                    OptionStatusState.Info);
            else
                _languageStatus?.SetStatus(string.Empty, OptionStatusState.Idle);

            RefreshUpdatesStatus();
        }

        void RefreshUpdatesStatus()
        {
            if (_localization == null || !_localization.isReady)
            {
                _updatesStatus?.SetStatus(string.Empty, OptionStatusState.Idle);
                return;
            }
            _updatesStatus?.SetStatus(
                string.Format(Loc("settings.updates.status.current"), ShortVersion(BuildInfo.Version)),
                OptionStatusState.Info);
        }

        static string ShortVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "v0.0";

            int major = 0;
            int minor = 0;
            int part = 0;
            int value = 0;

            for (int i = 0; i < version.Length && part < 2; i++)
            {
                char c = version[i];
                if (c >= '0' && c <= '9')
                {
                    value = value * 10 + (c - '0');
                    continue;
                }
                if (c != '.') break;
                if (part == 0) major = value;
                else minor = value;
                part++;
                value = 0;
            }

            if (part == 0) major = value;
            else if (part == 1) minor = value;

            return "v" + major + "." + minor;
        }

        void ValidateReferences()
        {
            if (_languageDropdown == null) Debug.LogWarning("[Settings] Language dropdown not assigned.", this);
            if (_windowModeDropdown == null) Debug.LogWarning("[Settings] Window mode dropdown not assigned.", this);
            if (_resolutionDropdown == null) Debug.LogWarning("[Settings] Resolution dropdown not assigned.", this);
            if (_textureDropdown == null) Debug.LogWarning("[Settings] Texture quality dropdown not assigned.", this);
            if (_shadowsDropdown == null) Debug.LogWarning("[Settings] Shadow quality dropdown not assigned.", this);
            if (_bloomToggle == null) Debug.LogWarning("[Settings] Bloom toggle not assigned.", this);
            if (_chromaticAberrationToggle == null) Debug.LogWarning("[Settings] Chromatic aberration toggle not assigned.", this);
            if (_versionToggle == null) Debug.LogWarning("[Settings] Version toggle not assigned.", this);
            if (_generalVolumeSlider == null) Debug.LogWarning("[Settings] General volume slider not assigned.", this);
            if (_soundVolumeSlider == null) Debug.LogWarning("[Settings] Sound volume slider not assigned.", this);
            if (_musicVolumeSlider == null) Debug.LogWarning("[Settings] Music volume slider not assigned.", this);
            if (_volumeMixerToggle == null) Debug.LogWarning("[Settings] Volume mixer toggle not assigned.", this);
            if (_languageStatus == null) Debug.LogWarning("[Settings] Language status bar not assigned.", this);
            if (_updatesStatus == null) Debug.LogWarning("[Settings] Updates status bar not assigned.", this);
            if (_checkUpdatesButton == null) Debug.LogWarning("[Settings] Check updates button not assigned.", this);
        }

        #endregion

        #region Populate

        void PopulateAll()
        {
            PopulateWindowMode();
            PopulateResolutions();
            PopulateTextureQuality();
            PopulateShadowQuality();
            PopulateLanguages();
        }

        void PopulateWindowMode()
        {
            if (_windowModeDropdown == null) return;
            _windowModeDropdown.ClearOptions();
            _windowModeDropdown.AddOptions(new List<string>
            {
                Loc("settings.windowMode.borderless"),
                Loc("settings.windowMode.exclusive"),
                Loc("settings.windowMode.windowed"),
            });
            int current = settings != null && settings.Data != null ? settings.Data.windowModeIndex : 0;
            _windowModeDropdown.SetValueWithoutNotify(Mathf.Clamp(current, 0, SettingsService.WindowModeCount - 1));
        }

        void PopulateResolutions()
        {
            if (_resolutionDropdown == null) return;

            _resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                int hz = CurrentRefreshHz(r);
                var existing = _resolutions.FirstOrDefault(x => x.width == r.width && x.height == r.height);
                if (existing != null)
                {
                    if (hz > existing.refreshHz)
                    {
                        existing.refreshHz = hz;
                        existing.label = FormatResolutionLabel(r.width, r.height, hz);
                    }
                    continue;
                }
                _resolutions.Add(new ResolutionOption
                {
                    width = r.width,
                    height = r.height,
                    refreshHz = hz,
                    label = FormatResolutionLabel(r.width, r.height, hz),
                });
            }
            _resolutions.Sort((a, b) => (a.width, a.height).CompareTo((b.width, b.height)));
            _resolutionDropdown.ClearOptions();
            _resolutionDropdown.AddOptions(_resolutions.Select(x => x.label).ToList());

            if (_resolutions.Count == 0)
                _resolutions.Add(new ResolutionOption
                {
                    width = Screen.currentResolution.width,
                    height = Screen.currentResolution.height,
                    refreshHz = CurrentRefreshHz(Screen.currentResolution),
                    label = FormatResolutionLabel(Screen.currentResolution.width, Screen.currentResolution.height, CurrentRefreshHz(Screen.currentResolution)),
                });
        }

        static string FormatResolutionLabel(int width, int height, int refreshHz) => $"{width} × {height}  ({refreshHz} Hz)";

        void PopulateTextureQuality()
        {
            if (_textureDropdown == null) return;
            _textureDropdown.ClearOptions();
            _textureDropdown.AddOptions(new List<string>
            {
                Loc("settings.textureQuality.full"),
                Loc("settings.textureQuality.half"),
                Loc("settings.textureQuality.quarter"),
                Loc("settings.textureQuality.eighth"),
            });
        }

        void PopulateShadowQuality()
        {
            if (_shadowsDropdown == null) return;
            _shadowsDropdown.ClearOptions();
            _shadowsDropdown.AddOptions(new List<string>
            {
                Loc("settings.shadows.off"),
                Loc("settings.shadows.low"),
                Loc("settings.shadows.medium"),
                Loc("settings.shadows.high"),
            });
        }

        void PopulateLanguages()
        {
            if (_languageDropdown == null) return;
            _languageDropdown.ClearOptions();
            _languageDropdown.AddOptions(Languages.Select(x => x.label).ToList());
        }

        void SyncValues()
        {
            var d = settings != null ? settings.Data : null;

            if (_windowModeDropdown != null)
                _windowModeDropdown.SetValueWithoutNotify(Mathf.Clamp(d != null ? d.windowModeIndex : 0, 0, SettingsService.WindowModeCount - 1));

            if (_resolutionDropdown != null)
            {
                int idx = _resolutions.FindIndex(x => d != null
                    ? x.width == d.resolutionWidth && x.height == d.resolutionHeight
                    : x.width == Screen.currentResolution.width && x.height == Screen.currentResolution.height);
                _resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, idx));
            }

            if (_textureDropdown != null) _textureDropdown.SetValueWithoutNotify(d != null ? d.textureQualityIndex : 0);
            if (_shadowsDropdown != null) _shadowsDropdown.SetValueWithoutNotify(d != null ? d.shadowQualityIndex : SettingsService.ShadowQualityCount - 1);

            SyncVolumeControls();

            SyncLanguageValue();

            if (_bloomToggle != null) _bloomToggle.SetIsOnWithoutNotify(d == null || d.bloomEnabled);
            if (_chromaticAberrationToggle != null) _chromaticAberrationToggle.SetIsOnWithoutNotify(d != null && d.chromaticAberrationEnabled);
            if (_versionToggle != null) _versionToggle.SetIsOnWithoutNotify(d == null || d.showVersionLabel);
        }

        void SyncVolumeControls()
        {
            var d = settings != null ? settings.Data : null;
            float generalVolume = d != null ? Mathf.Clamp01(d.volumeValue) : 1f;
            float soundVolume = d != null ? Mathf.Clamp01(d.soundsVolumeValue) : 1f;
            float musicVolume = d != null ? Mathf.Clamp01(d.musicVolumeValue) : 1f;

            if (_generalVolumeSlider != null) _generalVolumeSlider.SetValueWithoutNotify(generalVolume);
            if (_soundVolumeSlider != null) _soundVolumeSlider.SetValueWithoutNotify(soundVolume);
            if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            UpdateVolumeLabel(_generalVolumeLabel, generalVolume);
            UpdateVolumeLabel(_soundVolumeLabel, soundVolume);
            UpdateVolumeLabel(_musicVolumeLabel, musicVolume);

            if (_volumeMixerToggle != null)
                _volumeMixerToggle.SetIsOnWithoutNotify(settings == null || settings.IsVolumeMixerEnabled());
        }

        void SyncLanguageValue()
        {
            if (_languageDropdown == null) return;
            string lang = _localization != null ? _localization.currentLanguage : null;
            int idx = System.Array.FindIndex(Languages, x => x.code == lang);
            _languageDropdown.SetValueWithoutNotify(Mathf.Max(0, idx));
        }

        #endregion

        #region Event Handlers

        public void OnWindowModeChanged(int index) => settings?.SetWindowMode(index);

        public void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;
            var option = _resolutions[index];
            settings?.SetResolution(option.width, option.height, option.refreshHz);
        }

        public void OnTextureChanged(int index) => settings?.SetTextureQuality(index);
        public void OnShadowsChanged(int index) => settings?.SetShadowQuality(index);
        public void OnBloomChanged(bool value) => settings?.SetBloom(value);
        public void OnChromaticAberrationChanged(bool value) => settings?.SetChromaticAberration(value);
        public void OnVersionLabelChanged(bool value) => settings?.SetVersionLabel(value);

        public void OnGeneralVolumeChanged(float value)
        {
            settings?.SetSoundVolume(value);
            UpdateVolumeLabel(_generalVolumeLabel, value);
        }

        public void OnSoundVolumeChanged(float value)
        {
            settings?.SetSoundsVolume(value);
            UpdateVolumeLabel(_soundVolumeLabel, value);
        }

        public void OnMusicVolumeChanged(float value)
        {
            settings?.SetMusicVolume(value);
            UpdateVolumeLabel(_musicVolumeLabel, value);
        }

        public void OnVolumeMixerChanged(bool value) => settings?.SetVolumeMixerEnabled(value);

        static void UpdateVolumeLabel(TMP_Text label, float value)
        {
            if (label != null) label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        public void OnLanguageDropdownChanged(int index)
        {
            if (index < 0 || index >= Languages.Length) return;
            ApplyLanguage(Languages[index].code);
        }

        void ApplyLanguage(string code)
        {
            if (_localization == null) return;
            if (code == _localization.currentLanguage) return;

            _pendingLanguage = code;
            _languageStatus?.SetStatus(Loc("settings.language.status.loading"), OptionStatusState.Busy);
            _localization.SetLanguage(code);
        }

        void OnLanguageChanged()
        {
            PopulateWindowMode();
            PopulateTextureQuality();
            PopulateShadowQuality();
            SyncValues();

            if (!string.IsNullOrEmpty(_pendingLanguage)
                && _localization != null
                && _pendingLanguage == _localization.currentLanguage)
            {
                string label = LanguageLabel(_pendingLanguage);
                _languageStatus?.SetStatus(string.Format(Loc("settings.language.status.loaded"), label), OptionStatusState.Success);
                _pendingLanguage = null;
                if (!_checkOnCooldown) RefreshUpdatesStatus();
                return;
            }

            if (_pendingLanguage == null) RefreshStatusBars();
        }

        void OnLanguageLoadFailed(string langName)
        {
            if (_pendingLanguage != langName) return;
            _pendingLanguage = null;
            _languageStatus?.SetStatus(Loc("settings.language.status.error"), OptionStatusState.Error);
            SyncValues();
        }

        public void OnCheckUpdatesClick()
        {
            if (updateChecker == null)
            {
                _updatesStatus?.SetStatus(Loc("settings.updates.status.error"), OptionStatusState.Error);
                return;
            }
            if (updateChecker.isChecking) return;
            if (_checkOnCooldown) return;

            string url = !string.IsNullOrEmpty(_updateManifestUrl) ? _updateManifestUrl : updateChecker.manifestUrl;
            if (string.IsNullOrEmpty(url))
            {
                _updatesStatus?.SetStatus(Loc("settings.updates.status.noUrl"), OptionStatusState.Error);
                return;
            }

            _updatesStatus?.SetStatus(Loc("settings.updates.status.checking"), OptionStatusState.Busy);
            if (_checkUpdatesButton != null) _checkUpdatesButton.interactable = false;
            _checkOnCooldown = true;
            _nextCheckAllowedAt = Time.unscaledTime + Mathf.Max(0f, updateCheckCooldownSeconds);

            updateChecker.OnResult -= OnUpdateResult;
            updateChecker.OnResult += OnUpdateResult;
            updateChecker.CheckForUpdates(url);
        }

        void OnUpdateResult(UpdateCheckState state, string version)
        {
            updateChecker.OnResult -= OnUpdateResult;

            switch (state)
            {
                case UpdateCheckState.UpToDate:
                    _updatesStatus?.SetStatus(
                        string.Format(Loc("settings.updates.status.upToDate"), ShortVersion(string.IsNullOrEmpty(version) ? BuildInfo.Version : version)),
                        OptionStatusState.Success);
                    break;

                case UpdateCheckState.UpdateAvailable:
                    {
                        string latestSource = string.IsNullOrEmpty(version) ? BuildInfo.Version : version;
                        string installedShort = ShortVersion(BuildInfo.Version);
                        string latestShort = ShortVersion(latestSource);
                        string latestDisplay = latestShort != installedShort ? latestShort : latestSource;
                        _updatesStatus?.SetStatus(
                            string.Format(Loc("settings.updates.status.available"), latestDisplay, installedShort),
                            OptionStatusState.Success);
                        break;
                    }

                default:
                    _updatesStatus?.SetStatus(Loc("settings.updates.status.error"), OptionStatusState.Error);
                    break;
            }
        }

        #endregion

        #region Public API

        public string LanguageLabel(string code)
        {
            int idx = System.Array.FindIndex(Languages, x => x.code == code);
            return idx >= 0 ? Languages[idx].label : code;
        }

        #endregion

        #region Helpers

        string Loc(string key) => _localization != null ? _localization.GetLocalizedValue(key) : key;

        static int CurrentRefreshHz(Resolution r)
        {
            return Mathf.RoundToInt(r.refreshRateRatio.numerator / (float)Mathf.Max(1u, r.refreshRateRatio.denominator));
        }

        class ResolutionOption
        {
            public int width;
            public int height;
            public int refreshHz;
            public string label;
        }

        #endregion
    }
}
