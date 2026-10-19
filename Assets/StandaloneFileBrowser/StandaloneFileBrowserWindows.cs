#if UNITY_STANDALONE_WIN

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace SFB {
    public class StandaloneFileBrowserWindows : IStandaloneFileBrowser {
        private const uint OFN_OVERWRITEPROMPT = 0x00000002;
        private const uint OFN_NOCHANGEDIR = 0x00000008;
        private const uint OFN_ALLOWMULTISELECT = 0x00000200;
        private const uint OFN_PATHMUSTEXIST = 0x00000800;
        private const uint OFN_FILEMUSTEXIST = 0x00001000;
        private const uint OFN_EXPLORER = 0x00080000;
        private const uint BIF_RETURNONLYFSDIRS = 0x00000001;
        private const uint BIF_NEWDIALOGSTYLE = 0x00000040;
        private const int FILE_BUFFER_SIZE = 32768;

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileNameW(ref OpenFileName ofn);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHBrowseForFolderW(ref BrowseInfo bi);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFilter;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrInitialDir;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrTitle;
            public uint Flags;
            public short nFileOffset;
            public short nFileExtension;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public uint FlagsEx;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BrowseInfo {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        public string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect) {
            IntPtr buffer = Marshal.AllocHGlobal(FILE_BUFFER_SIZE * 2);
            try {
                Marshal.WriteInt16(buffer, 0);
                var ofn = new OpenFileName();
                ofn.lStructSize = Marshal.SizeOf(typeof(OpenFileName));
                ofn.hwndOwner = GetActiveWindow();
                ofn.lpstrFilter = GetFilterFromFileExtensionList(extensions);
                ofn.lpstrFile = buffer;
                ofn.nMaxFile = FILE_BUFFER_SIZE;
                if (!string.IsNullOrEmpty(directory)) ofn.lpstrInitialDir = GetDirectoryPath(directory);
                ofn.lpstrTitle = title;
                ofn.Flags = OFN_NOCHANGEDIR | OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST;
                if (multiselect) ofn.Flags |= OFN_ALLOWMULTISELECT;
                return GetOpenFileNameW(ref ofn) ? ParseMultiSelect(buffer) : new string[0];
            }
            finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void OpenFilePanelAsync(string title, string directory, ExtensionFilter[] extensions, bool multiselect, Action<string[]> cb) {
            cb.Invoke(OpenFilePanel(title, directory, extensions, multiselect));
        }

        public string[] OpenFolderPanel(string title, string directory, bool multiselect) {
            IntPtr rootPidl = IntPtr.Zero;
            bool hasRoot = false;
            if (!string.IsNullOrEmpty(directory)) {
                uint sfgao;
                if (SHParseDisplayName(directory, IntPtr.Zero, out rootPidl, 0, out sfgao) == 0 && rootPidl != IntPtr.Zero) hasRoot = true;
            }
            try {
                var bi = new BrowseInfo();
                bi.hwndOwner = GetActiveWindow();
                bi.pidlRoot = hasRoot ? rootPidl : IntPtr.Zero;
                bi.lpszTitle = title;
                bi.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
                IntPtr pidl = SHBrowseForFolderW(ref bi);
                if (pidl == IntPtr.Zero) return new string[0];
                var sb = new StringBuilder(1024);
                string path = SHGetPathFromIDListW(pidl, sb) ? sb.ToString() : string.Empty;
                CoTaskMemFree(pidl);
                return string.IsNullOrEmpty(path) ? new string[0] : new [] { path };
            }
            finally {
                if (hasRoot) CoTaskMemFree(rootPidl);
            }
        }

        public void OpenFolderPanelAsync(string title, string directory, bool multiselect, Action<string[]> cb) {
            cb.Invoke(OpenFolderPanel(title, directory, multiselect));
        }

        public string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions) {
            var finalFilename = "";
            if (!string.IsNullOrEmpty(directory)) {
                finalFilename = GetDirectoryPath(directory);
            }
            if (!string.IsNullOrEmpty(defaultName)) {
                finalFilename += defaultName;
            }
            IntPtr buffer = Marshal.AllocHGlobal(FILE_BUFFER_SIZE * 2);
            try {
                byte[] nameBytes = Encoding.Unicode.GetBytes(finalFilename);
                if (nameBytes.Length > FILE_BUFFER_SIZE * 2 - 2) {
                    nameBytes = new byte[FILE_BUFFER_SIZE * 2 - 2];
                }
                Marshal.Copy(nameBytes, 0, buffer, nameBytes.Length);
                Marshal.WriteInt16(buffer, nameBytes.Length, 0);
                var ofn = new OpenFileName();
                ofn.lStructSize = Marshal.SizeOf(typeof(OpenFileName));
                ofn.hwndOwner = GetActiveWindow();
                ofn.lpstrFilter = GetFilterFromFileExtensionList(extensions);
                ofn.lpstrFile = buffer;
                ofn.nMaxFile = FILE_BUFFER_SIZE;
                ofn.lpstrTitle = title;
                ofn.Flags = OFN_NOCHANGEDIR | OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_OVERWRITEPROMPT;
                if (extensions != null && extensions.Length > 0 && extensions[0].Extensions != null && extensions[0].Extensions.Length > 0) {
                    ofn.lpstrDefExt = extensions[0].Extensions[0];
                }
                return GetSaveFileNameW(ref ofn) ? Marshal.PtrToStringUni(buffer) : "";
            }
            finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void SaveFilePanelAsync(string title, string directory, string defaultName, ExtensionFilter[] extensions, Action<string> cb) {
            cb.Invoke(SaveFilePanel(title, directory, defaultName, extensions));
        }

        private static string GetFilterFromFileExtensionList(ExtensionFilter[] extensions) {
            if (extensions == null) return null;
            var sb = new StringBuilder();
            foreach (var filter in extensions) {
                if (filter.Extensions == null || filter.Extensions.Length == 0) continue;
                var patterns = new string[filter.Extensions.Length];
                for (int i = 0; i < filter.Extensions.Length; i++) {
                    patterns[i] = "*." + filter.Extensions[i];
                }
                sb.Append(filter.Name).Append(" (").Append(string.Join("; ", patterns)).Append(')').Append('\0');
                sb.Append(string.Join(";", patterns)).Append('\0');
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        private static string[] ParseMultiSelect(IntPtr buffer) {
            var list = new List<string>(4);
            long address = buffer.ToInt64();
            int index = 0;
            while (index < FILE_BUFFER_SIZE) {
                string item = Marshal.PtrToStringUni(new IntPtr(address + index * 2));
                if (string.IsNullOrEmpty(item)) break;
                list.Add(item);
                index += item.Length + 1;
            }
            if (list.Count == 0) return new string[0];
            if (list.Count == 1) return new [] { list[0] };
            var result = new string[list.Count - 1];
            for (int i = 1; i < list.Count; i++) {
                result[i - 1] = Path.Combine(list[0], list[i]);
            }
            return result;
        }

        private static string GetDirectoryPath(string directory) {
            var directoryPath = Path.GetFullPath(directory);
            if (!directoryPath.EndsWith("\\")) {
                directoryPath += "\\";
            }
            if (Path.GetPathRoot(directoryPath) == directoryPath) {
                return directory;
            }
            return Path.GetDirectoryName(directoryPath) + Path.DirectorySeparatorChar;
        }
    }
}

#endif
