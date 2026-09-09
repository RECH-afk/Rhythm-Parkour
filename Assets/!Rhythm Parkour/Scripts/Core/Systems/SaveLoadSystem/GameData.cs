using UnityEngine;

namespace RKS.HadalZone.Core
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
        }
    }
}
