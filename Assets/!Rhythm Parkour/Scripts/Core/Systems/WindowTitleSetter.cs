using System.Runtime.InteropServices;
using UnityEngine;

namespace RKS.HadalZone.Core
{
    public static class WindowTitle
    {
        [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(System.IntPtr hwnd, string title);

        [DllImport("user32.dll", EntryPoint = "GetActiveWindow")]
        private static extern System.IntPtr GetActiveWindow();

        public static void Set(string title)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            SetWindowText(GetActiveWindow(), title);
#else
            Debug.Log($"[WindowTitle] Would set: {title} (editor only)");
#endif
        }
    }
}
