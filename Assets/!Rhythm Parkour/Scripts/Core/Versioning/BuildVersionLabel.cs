using TMPro;
using UnityEngine;

namespace RKS.RhythmParkour.Core
{
    public class BuildVersionLabel : RKSBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] bool showHash = true;

        public void Bind(TMP_Text text) => label = text;

        protected override void OnReady()
        {
            Refresh();
        }

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
