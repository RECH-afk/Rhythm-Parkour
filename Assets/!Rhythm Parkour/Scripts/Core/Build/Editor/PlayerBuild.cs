using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RKS.RhythmParkour.Core.Build
{
    public static class PlayerBuild
    {
        public static void BuildWindows64()
        {
            string version = GetArg("-rksVersion");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            string phase = GetArg("-rksPhase");
            if (!string.IsNullOrEmpty(phase)) VersionToolWindow.WritePhase(phase.Trim());
            BuildInfoGenerator.Refresh();

            if (string.IsNullOrEmpty(VersionToolWindow.ReadPhase()))
                throw new Exception("Suffix is required. Set it in Tools -> Version Tool, or pass -rksPhase (for example -rksPhase alpha).");

            string projectRoot = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
            string output = GetArg("-rksBuildPath");
            if (string.IsNullOrEmpty(output)) output = Path.Combine(projectRoot, "Build");
            Directory.CreateDirectory(output);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new Exception("No enabled scenes in Build Settings.");

            string exePath = Path.Combine(output, PlayerSettings.productName + ".exe");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Build failed: " + report.summary.result);
            Debug.Log($"[PlayerBuild] OK: {exePath} ({report.summary.totalSize} bytes), version={BuildInfo.Version}");
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }

    public sealed class BuildInfoRefreshHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildInfoGenerator.Refresh();

            if (Application.isBatchMode) return;

            var gate = VersionToolWindow.OpenBuildGate();
            if (gate == null || !gate.Confirmed)
                throw new Exception("Build cancelled: the Version Tool window was closed without Start Build.");

            BuildInfoGenerator.Refresh();
        }
    }

    public sealed class BuildPackageHook : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var cfg = UpdatePackager.LoadConfig();
            if (!cfg.autoPack) return;

            try
            {
                UpdatePackager.Pack(report.summary.outputPath);
                if (cfg.openFolderAfterBuild)
                    EditorUtility.RevealInFinder(Path.Combine(UpdatePackager.ServerDir, "update.zip"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayerBuild] Failed to pack the update: " + e.Message);
            }
        }
    }

    public static class UpdatePackager
    {
        public const string ServerDirName = "Server";
        const string ConfigRelativePath = "ProjectSettings/RksUpdateServer.json";

        [Serializable]
        public sealed class ServerConfig
        {
            public string downloadUrl = "";
            public string notes = "";
            public bool autoPack = true;
            public bool openFolderAfterBuild = false;
        }

        [Serializable]
        sealed class UpdateManifestForServer
        {
            public string latestVersion;
            public string downloadUrl;
            public string notes;
        }

        public static string ProjectRoot => Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
        public static string ServerDir => Path.Combine(ProjectRoot, ServerDirName);
        public static string DefaultBuildDir => Path.Combine(ProjectRoot, "Build");

        static void CreateZip(string buildDir, string zip)
        {
            string[] files = Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories);
            int total = files.Length;
            using (var archive = ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            {
                for (int i = 0; i < total; i++)
                {
                    string file = files[i];
                    string relative = file.Substring(buildDir.Length).TrimStart('\\', '/');
                    if (IsExcludedFromZip(relative.Split('\\', '/')[0])) continue;

                    if ((i & 31) == 0)
                        EditorUtility.DisplayProgressBar("Update", "Packing update.zip... " + relative, total == 0 ? 1f : (float)i / total);

                    ZipArchiveEntry entry = archive.CreateEntry(relative.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                    using (Stream input = File.OpenRead(file))
                    using (Stream output = entry.Open())
                        input.CopyTo(output);
                }
            }
        }

        static bool IsExcludedFromZip(string topLevelName)
        {
            if (string.IsNullOrEmpty(topLevelName)) return false;
            if (topLevelName.Equals("update_tmp", StringComparison.OrdinalIgnoreCase)) return true;
            if (topLevelName.Equals("rks_apply_update.bat", StringComparison.OrdinalIgnoreCase)) return true;
            if (topLevelName.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase)) return true;
            if (topLevelName.EndsWith("_BurstDebugInformation_DoNotShip", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static ServerConfig LoadConfig()
        {
            var cfg = new ServerConfig();
            string path = Path.Combine(ProjectRoot, ConfigRelativePath);
            if (!File.Exists(path)) return cfg;
            try { cfg = JsonUtility.FromJson<ServerConfig>(File.ReadAllText(path)) ?? cfg; }
            catch { }
            return cfg;
        }

        public static void SaveConfig(ServerConfig cfg)
        {
            string path = Path.Combine(ProjectRoot, ConfigRelativePath);
            File.WriteAllText(path, JsonUtility.ToJson(cfg, true));
        }

        public static void Pack()
        {
            string exe = Path.Combine(DefaultBuildDir, PlayerSettings.productName + ".exe");
            if (!File.Exists(exe))
            {
                Debug.LogWarning($"[PlayerBuild] Build not found: {exe}. Build the game first.");
                return;
            }
            Pack(exe);
        }

        public static void Pack(string exePath)
        {
            string buildDir = Path.GetDirectoryName(Path.GetFullPath(exePath));
            string root = ProjectRoot.TrimEnd('\\', '/');
            if (buildDir.TrimEnd('\\', '/') == root || Directory.Exists(Path.Combine(buildDir, "Assets")))
            {
                Debug.LogWarning("[PlayerBuild] Build is inside the project - update packing skipped.");
                return;
            }

            Directory.CreateDirectory(ServerDir);
            string zip = Path.Combine(ServerDir, "update.zip");
            if (File.Exists(zip)) File.Delete(zip);

            EditorUtility.DisplayProgressBar("Update", "Packing update.zip...", 0.1f);
            try
            {
                CreateZip(buildDir, zip);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var cfg = LoadConfig();
            string downloadUrl = string.IsNullOrEmpty(cfg.downloadUrl) ? new Uri(zip).AbsoluteUri : cfg.downloadUrl.Trim();
            var manifest = new UpdateManifestForServer
            {
                latestVersion = BuildInfo.Version,
                downloadUrl = downloadUrl,
                notes = cfg.notes ?? ""
            };
            string jsonPath = Path.Combine(ServerDir, "update.json");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(manifest, false), new UTF8Encoding(false));

            Debug.Log($"[PlayerBuild] Update package ready (version {BuildInfo.Version}):\n{zip}\n{jsonPath}");
        }
    }

    public sealed class VersionToolWindow : EditorWindow
    {
        static readonly string[] Presets = { "indev", "alpha", "beta", "rc", "stable" };

        string baseVersion;
        string phase;
        string releaseNotes;
        string serverUrl;
        bool autoPack;
        bool openFolderAfterBuild;
        bool isBuildGate;
        bool confirmed;

        public bool Confirmed => confirmed;

        [MenuItem("Tools/Version Tool")]
        public static void Open() => GetWindow<VersionToolWindow>("Version Tool");

        public static VersionToolWindow OpenBuildGate()
        {
            var win = CreateInstance<VersionToolWindow>();
            win.minSize = new Vector2(430f, 300f);
            win.isBuildGate = true;
            win.SyncFields();
            win.position = new Rect((Screen.currentResolution.width - 430f) * 0.5f, (Screen.currentResolution.height - 300f) * 0.5f, 430f, 300f);
            win.ShowModal();
            return win;
        }

        void OnEnable()
        {
            SyncFields();
        }

        void OnFocus()
        {
            SyncFields();
            BuildInfo.Reload();
            Repaint();
        }

        void SyncFields()
        {
            baseVersion = PlayerSettings.bundleVersion;
            phase = ReadPhase();
            var cfg = UpdatePackager.LoadConfig();
            serverUrl = cfg.downloadUrl;
            releaseNotes = cfg.notes;
            autoPack = cfg.autoPack;
            openFolderAfterBuild = cfg.openFolderAfterBuild;
        }

        void OnGUI()
        {
            if (isBuildGate)
            {
                DrawGate();
                return;
            }

            EditorGUILayout.HelpBox(
                $"Full version: {BuildInfo.Version}\nGame label: {BuildInfo.ShortVersion}\nCommit: {BuildInfo.CommitHash} ({BuildInfo.CommitCount}), branch: {BuildInfo.Branch}" + (BuildInfo.IsDirty ? "\nGit: unsaved changes (dirty)" : ""),
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                baseVersion = EditorGUILayout.TextField(baseVersion);
                if (GUILayout.Button("Apply", GUILayout.Width(80))) ApplyVersion();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Suffix", EditorStyles.boldLabel);
            DrawSuffixButtons();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Release notes", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                releaseNotes = EditorGUILayout.TextField(releaseNotes);
                if (GUILayout.Button("Save", GUILayout.Width(80))) SaveConfigFromFields();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Archive URL");
                serverUrl = EditorGUILayout.TextField(serverUrl);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Auto-pack");
                bool ap = EditorGUILayout.Toggle(autoPack);
                if (ap != autoPack) { autoPack = ap; SaveConfigFromFields(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Open folder after build");
                bool of = EditorGUILayout.Toggle(openFolderAfterBuild);
                if (of != openFolderAfterBuild) { openFolderAfterBuild = of; SaveConfigFromFields(); }
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pack Now")) PackNow();
                if (GUILayout.Button("Open Server Folder")) RevealServer();
            }
        }

        void DrawGate()
        {
            EditorGUILayout.HelpBox(
                $"Build label: {GatePreview()}\nFull version: {GateFullVersion()}",
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Version", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                baseVersion = EditorGUILayout.TextField(baseVersion);
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                {
                    string v = SanitizeVersion(baseVersion);
                    if (string.IsNullOrEmpty(v))
                        ShowNotification(new GUIContent("Version: digits and dots only"));
                    else
                    {
                        baseVersion = v;
                        ApplyVersionInternal();
                        ShowNotification(new GUIContent("Applied: " + GatePreview()));
                    }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Suffix", EditorStyles.boldLabel);
            DrawSuffixButtons();
            if (string.IsNullOrEmpty((phase ?? "").Trim()))
                EditorGUILayout.HelpBox("Pick a suffix to continue.", MessageType.Error);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Release notes", EditorStyles.boldLabel);
            releaseNotes = EditorGUILayout.TextField(releaseNotes);

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Build", GUILayout.Height(30))) ConfirmAndBuild();
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) { confirmed = false; Close(); }
            }
        }

        void DrawSuffixButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (string p in Presets)
                {
                    Color old = GUI.backgroundColor;
                    if (phase == p) GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                    if (GUILayout.Button(p)) { phase = p; if (!isBuildGate) ApplyPhase(); }
                    GUI.backgroundColor = old;
                }
            }
        }

        string GatePreview()
        {
            string v = SanitizeVersion(baseVersion);
            string label = string.IsNullOrEmpty(v) ? "v?" : "v" + v;
            if (!string.IsNullOrEmpty(phase)) label += " - " + phase;
            if (!string.IsNullOrEmpty(BuildInfo.CommitHash)) label += " (" + BuildInfo.CommitHash + ")";
            return label;
        }

        string GateFullVersion()
        {
            string v = SanitizeVersion(baseVersion);
            string count = string.IsNullOrEmpty(BuildInfo.CommitCount) ? "0" : BuildInfo.CommitCount;
            string hash = string.IsNullOrEmpty(BuildInfo.CommitHash) ? "" : BuildInfo.CommitHash;
            string s = string.IsNullOrEmpty(v) ? "?" : v + "." + count;
            if (!string.IsNullOrEmpty(hash)) s += "+" + hash;
            return s;
        }

        void ConfirmAndBuild()
        {
            string p = (phase ?? "").Trim();
            if (string.IsNullOrEmpty(p))
            {
                ShowNotification(new GUIContent("Suffix is required"));
                return;
            }
            if (string.IsNullOrEmpty(SanitizeVersion(baseVersion)))
            {
                ShowNotification(new GUIContent("Version: digits and dots only"));
                return;
            }
            ApplyVersionInternal();
            WritePhase(p);
            SaveConfigFromFields();
            confirmed = true;
            Close();
        }

        void ApplyVersion()
        {
            string v = SanitizeVersion(baseVersion);
            if (string.IsNullOrEmpty(v))
            {
                ShowNotification(new GUIContent("Version: digits and dots only"));
                return;
            }
            baseVersion = v;
            ApplyVersionInternal();
            BuildInfoGenerator.Refresh();
            ShowNotification(new GUIContent(BuildInfo.ShortVersion));
        }

        void ApplyVersionInternal()
        {
            string v = SanitizeVersion(baseVersion);
            if (string.IsNullOrEmpty(v)) return;
            baseVersion = v;
            PlayerSettings.bundleVersion = v;
        }

        void ApplyPhase()
        {
            string p = (phase ?? "").Trim();
            if (string.IsNullOrEmpty(p))
            {
                ShowNotification(new GUIContent("Suffix is required"));
                return;
            }
            phase = p;
            WritePhase(p);
            BuildInfoGenerator.Refresh();
            ShowNotification(new GUIContent(BuildInfo.ShortVersion));
        }

        void SaveConfigFromFields()
        {
            var cfg = UpdatePackager.LoadConfig();
            cfg.downloadUrl = (serverUrl ?? "").Trim();
            cfg.notes = releaseNotes ?? "";
            cfg.autoPack = autoPack;
            cfg.openFolderAfterBuild = openFolderAfterBuild;
            UpdatePackager.SaveConfig(cfg);
        }

        void PackNow()
        {
            SaveConfigFromFields();
            BuildInfoGenerator.Refresh();
            UpdatePackager.Pack();
            Repaint();
        }

        void RevealServer()
        {
            SaveConfigFromFields();
            string zip = Path.Combine(UpdatePackager.ServerDir, "update.zip");
            EditorUtility.RevealInFinder(File.Exists(zip) ? zip : UpdatePackager.ServerDir);
        }

        static string SanitizeVersion(string input)
        {
            return new string((input ?? "").Trim().Where(c => char.IsDigit(c) || c == '.').ToArray());
        }

        public static string ReadPhase()
        {
            string path = Path.Combine(UpdatePackager.ProjectRoot, "Assets/Resources/BuildInfo.json");
            if (!File.Exists(path)) return "";
            try { return JsonUtility.FromJson<BuildInfoData>(File.ReadAllText(path)).phase ?? ""; }
            catch { return ""; }
        }

        public static void WritePhase(string phase)
        {
            string path = Path.Combine(UpdatePackager.ProjectRoot, "Assets/Resources/BuildInfo.json");
            var dto = new BuildInfoData();
            if (File.Exists(path))
            {
                try { dto = JsonUtility.FromJson<BuildInfoData>(File.ReadAllText(path)); }
                catch { dto = new BuildInfoData(); }
            }
            dto.phase = phase;
            File.WriteAllText(path, JsonUtility.ToJson(dto, true));
        }
    }
}
