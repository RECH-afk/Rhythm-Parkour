using TMPro;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour.Core.Settings;

namespace RKS.RhythmParkour.Core
{
    public class BuildVersionLabel : RKSBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] bool showHash = true;

        [HideInInspector]
        [InjectOptional] public SettingsService settings;

        bool _applyPending = true;

        public void Bind(TMP_Text text) => label = text;

        protected override void OnReady()
        {
            Refresh();
            if (settings != null) settings.OnVersionLabelChanged += ApplyVisibility;
        }

        protected override void OnDisposed()
        {
            if (settings != null) settings.OnVersionLabelChanged -= ApplyVisibility;
        }

        protected override void Update()
        {
            if (!_applyPending) return;
            _applyPending = false;
            ApplyVisibility(settings == null || settings.Data == null || settings.Data.showVersionLabel);
        }

        void ApplyVisibility(bool visible) => gameObject.SetActive(visible);

        public void Refresh()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            string text = BuildInfo.ShortVersion;
            if (showHash && !string.IsNullOrEmpty(BuildInfo.CommitHash))
                text += " (" + BuildInfo.CommitHash + ")";
            label.text = text;
        }
    }
}
