using UnityEngine;
using UnityEngine.UI;
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
        public GameObject[] panels;
        public Button[] buttons;
        public int defaultIndex = 0;
        int current = -1;
        protected override void OnReady()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                int idx = i;
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => Show(idx));
            }
            Show(defaultIndex);
        }
        public void Show(int index)
        {
            if (panels == null) return;
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null) panels[i].SetActive(i == index);
            for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].interactable = i != index;
            current = index;
        }
        public void ShowByName(string name)
        {
            if (panels == null) return;
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && panels[i].name == name) { Show(i); return; }
        }
    }
}
