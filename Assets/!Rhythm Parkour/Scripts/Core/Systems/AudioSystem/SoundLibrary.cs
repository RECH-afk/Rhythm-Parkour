using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core
{
    [CreateAssetMenu(menuName = "Hadal Zone/Audio/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [Tooltip("All sounds included in this library.")]
        public SoundData[] sounds;
    }
}
