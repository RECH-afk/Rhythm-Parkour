using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;

namespace RKS.RhythmParkour.UI.Timeline
{
    public class TabsUI : RKSBehaviour
    {
        [Header("Tabs")]
        public GameObject[] panels;
        public int defaultIndex = 0;

        int current = -1;

        protected override void OnReady()
        {
            Show(defaultIndex);
        }

        public virtual void Show(int index)
        {
            if (panels == null || panels.Length == 0) return;
            if (index < 0 || index >= panels.Length) return;
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null) panels[i].SetActive(i == index);
            current = index;
        }

        public void ShowByName(string name)
        {
            if (panels == null) return;
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && panels[i].name == name) { Show(i); return; }
        }

        public virtual void Next()
        {
            if (panels == null || panels.Length == 0) return;
            Show((current + 1) % panels.Length);
        }

        public virtual void Prev()
        {
            if (panels == null || panels.Length == 0) return;
            Show((current - 1 + panels.Length) % panels.Length);
        }

        public int CurrentIndex => current;
    }
}
