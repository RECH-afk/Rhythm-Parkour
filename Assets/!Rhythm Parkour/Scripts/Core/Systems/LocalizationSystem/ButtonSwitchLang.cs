using RKS.HadalZone.Core;
using UnityEngine;

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
