using System.Runtime.InteropServices;
using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core
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
