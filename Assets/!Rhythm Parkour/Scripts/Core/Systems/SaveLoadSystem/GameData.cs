using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core
{
    [System.Serializable]
    public class GameData
    {
        public class Data
        {
            public bool isFirstRun = true;
            public bool isPlayerAgreedPlay = false;
            public bool isShadersCompiled = false;
            public string language = "en_US";
            public int frameRateIndex = 1;
            public int windowModeIndex = 0;
            public float volumeValue = 1.0f;
            public bool isVisualMoverEnabled = true;
            public string selectedLevelPath = "";
            public string lastRkslPath = "";
            public string transferRkslPath = "";
        }
    }
}
