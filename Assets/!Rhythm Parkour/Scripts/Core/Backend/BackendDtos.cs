using System;

namespace RKS.RhythmParkour.Core.Backend
{
    [Serializable]
    public sealed class CredentialsDto
    {
        public string username = "";
        public string password = "";
    }

    [Serializable]
    public sealed class StatsSessionDto
    {
        public string sessionId = "";
        public string appVersion = "";
        public string platform = "";
        public string os = "";
        public int cpuCores;
        public int memoryMb;
        public int screenW;
        public int screenH;
        public string language = "";
    }

    [Serializable]
    public sealed class StatsLevelDto
    {
        public string sessionId = "";
        public string levelTitle = "";
        public int totalNotes;
        public int count300;
        public int count100;
        public int count50;
        public int countMiss;
        public int maxCombo;
        public float accuracy;
        public float flow;
        public string rank = "";
        public bool failed;
    }

    [Serializable]
    public sealed class BackendResponseDto
    {
        public bool ok;
        public string error = "";
        public string serverTime = "";
        public string minVersion = "";
        public string token = "";
        public string accountName = "";
        public string created = "";
    }
}
