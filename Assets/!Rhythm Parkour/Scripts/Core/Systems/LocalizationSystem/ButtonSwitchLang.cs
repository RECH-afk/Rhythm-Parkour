using RKS.RhythmParkour.Core;
using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core
{
    [DisallowMultipleComponent]
    public sealed class ButtonSwitchLang : RKSBehaviour
    {
        [SerializeField] private string languageCode;

        public void OnButtonClick()
        {
            if (Localization == null)
            {
                return;
            }

            string targetLang = string.IsNullOrWhiteSpace(languageCode) ? gameObject.name : languageCode;

            if (string.IsNullOrWhiteSpace(targetLang))
            {
                return;
            }

            if (Localization.currentLanguage == targetLang)
            {
                return;
            }

            Localization.SetLanguage(targetLang);
        }
    }
}
