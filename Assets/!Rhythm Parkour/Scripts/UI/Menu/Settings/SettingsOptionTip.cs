using System;
using TMPro;
using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.UI
{
    public class SettingsOptionTip : RKSBehaviour
    {
        public const string DefaultTitleKey = "settings.tip.defaultTitle";
        public const string DefaultDescriptionKey = "settings.tip.defaultDescription";

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;

        string _titleKey = DefaultTitleKey;
        string _descriptionKey = DefaultDescriptionKey;

        protected override void OnInjected()
        {
            if (Localization != null) Localization.OnLanguageChanged += Refresh;
        }

        protected override void OnReady()
        {
            ShowDefault();
        }

        protected override void OnDisposed()
        {
            if (Localization != null) Localization.OnLanguageChanged -= Refresh;
        }

        public void Show(string titleKey, string descriptionKey)
        {
            _titleKey = titleKey;
            _descriptionKey = descriptionKey;
            Apply();
        }

        public void ShowDefault() => Show(DefaultTitleKey, DefaultDescriptionKey);

        public void Refresh() => Apply();

        void Apply()
        {
            if (titleText != null) titleText.text = Resolve(_titleKey, "Settings");
            if (descriptionText != null) descriptionText.text = Resolve(_descriptionKey, string.Empty);
        }

        string Resolve(string key, string fallback)
        {
            string value = Localization != null ? Localization.GetLocalizedValue(key) : key;
            if (string.IsNullOrEmpty(value) || value == key) value = fallback;
            return value;
        }
    }
}
