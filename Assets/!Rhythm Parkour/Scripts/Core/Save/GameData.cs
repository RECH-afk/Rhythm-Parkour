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
            public float musicVolumeValue = 1.0f;
            public bool isVisualMoverEnabled = true;
            public string selectedLevelPath = "";
            public string lastRkslPath = "";
            public string transferRkslPath = "";

            public int textureQualityIndex = 0;
            public int shadowQualityIndex = 3;
            public int resolutionWidth = 0;
            public int resolutionHeight = 0;
            public int resolutionRefreshHz = 0;
            public bool bloomEnabled = true;
            public bool chromaticAberrationEnabled = false;
            public bool showVersionLabel = true;
        }
    }
}
