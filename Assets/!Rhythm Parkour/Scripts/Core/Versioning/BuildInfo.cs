using System;
using UnityEngine;

namespace RKS.RhythmParkour.Core
{
    [Serializable]
    public sealed class BuildInfoData
    {
        public string version = "";
        public string shortVersion = "";
        public string commitHash = "";
        public string commitCount = "";
        public string branch = "";
        public string buildDate = "";
        public string tag = "";
        public bool dirty;
    }

    public static class BuildInfo
    {
        const string FallbackVersion = "0.1.0-dev";

        static BuildInfoData _data;
        static bool _loaded;

        static BuildInfoData Data
        {
            get
            {
                if (_loaded) return _data;
                _loaded = true;
                _data = new BuildInfoData { version = FallbackVersion, shortVersion = FallbackVersion };
                try
                {
                    TextAsset asset = Resources.Load<TextAsset>("BuildInfo");
                    if (asset != null && !string.IsNullOrEmpty(asset.text))
                    {
                        BuildInfoData parsed = JsonUtility.FromJson<BuildInfoData>(asset.text);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.version))
                            _data = parsed;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BuildInfo] " + e.Message);
                }
                return _data;
            }
        }

        public static string Version => Data.version;
        public static string ShortVersion => Data.shortVersion;
        public static string CommitHash => Data.commitHash;
        public static string CommitCount => Data.commitCount;
        public static string Branch => Data.branch;
        public static string BuildDate => Data.buildDate;
        public static string Tag => Data.tag;
        public static bool IsDirty => Data.dirty;
    }
}
