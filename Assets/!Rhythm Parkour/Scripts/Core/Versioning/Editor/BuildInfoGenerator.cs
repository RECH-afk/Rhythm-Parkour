using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RKS.RhythmParkour.Core
{
    public static class BuildInfoGenerator
    {
        const string RelativePath = "Assets/Resources/BuildInfo.json";

        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {
            try { Refresh(); }
            catch (Exception e) { Debug.LogWarning("[BuildInfo] " + e.Message); }
        }

        public static void Refresh()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string hash = RunGit(root, "rev-parse --short HEAD");
            string count = RunGit(root, "rev-list --count HEAD");
            string branch = RunGit(root, "branch --show-current");
            string describe = RunGit(root, "describe --tags --long --always");
            bool dirty = RunGit(root, "status --porcelain") != "";

            if (string.IsNullOrEmpty(hash)) hash = "unknown";
            if (string.IsNullOrEmpty(count)) count = "0";
            if (string.IsNullOrEmpty(branch)) branch = "unknown";

            string baseVersion = PlayerSettings.bundleVersion;
            if (string.IsNullOrEmpty(baseVersion)) baseVersion = "0.1";
            foreach (char c in baseVersion)
            {
                if (!char.IsDigit(c) && c != '.') { baseVersion = "0.1"; break; }
            }

            string tag = ParseTag(describe);
            string suffix = dirty ? ".dirty" : "";
            string version = $"{baseVersion}.{count}+{hash}{suffix}";

            var dto = new BuildInfoData
            {
                version = version,
                shortVersion = "v" + baseVersion + " - indev",
                commitHash = hash,
                commitCount = count,
                branch = branch,
                buildDate = DateTime.UtcNow.ToString("o"),
                tag = tag,
                dirty = dirty
            };

            string json = JsonUtility.ToJson(dto, true);
            string full = Path.Combine(root, RelativePath);
            string old = File.Exists(full) ? File.ReadAllText(full) : null;
            if (old != json)
            {
                File.WriteAllText(full, json);
                AssetDatabase.Refresh();
            }
        }

        static string ParseTag(string describe)
        {
            if (string.IsNullOrEmpty(describe)) return "";
            int g = describe.LastIndexOf("-g", StringComparison.Ordinal);
            if (g > 0)
            {
                string rest = describe.Substring(0, g);
                int dash = rest.LastIndexOf('-');
                if (dash > 0 && long.TryParse(rest.Substring(dash + 1), out _))
                    return rest.Substring(0, dash);
            }
            return "";
        }

        static string RunGit(string root, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "-C \"" + root + "\" " + args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    return p.ExitCode == 0 ? output.Trim() : "";
                }
            }
            catch
            {
                return "";
            }
        }

        sealed class Preprocess : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                Refresh();
            }
        }
    }
}
