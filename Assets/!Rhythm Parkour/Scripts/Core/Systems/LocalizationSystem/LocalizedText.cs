using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core
{
    public class LocalizedText : RKSBehaviour
    {
        [SerializeField]
        public string key;

        private TMP_Text text;

        protected override void OnInjected()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            UpdateText();
            if (Localization != null) Localization.OnLanguageChanged += UpdateText;
        }

        protected override void OnDisposed()
        {
            if (Localization != null) Localization.OnLanguageChanged -= UpdateText;
        }

        public virtual void UpdateText()
        {
            if (gameObject == null) return;
            if (text == null) text = GetComponent<TMP_Text>();
            if (Localization == null || text == null) return;
            text.text = Localization.GetLocalizedValue(key);
        }
    }
}
