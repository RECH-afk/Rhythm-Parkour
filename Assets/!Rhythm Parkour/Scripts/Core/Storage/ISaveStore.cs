using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Storage
{
    public interface ISaveStore
    {
        GameData.Data CurrentData { get; }
        void Write();
        void Write(GameData.Data data);
        GameData.Data Load();
        void ResetToDefault();
    }
}
