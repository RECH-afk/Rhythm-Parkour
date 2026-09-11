using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Storage
{
    public interface IRkslStore
    {
        bool Save(string rkslPath, RkslManifest manifest, string audioSourcePath, string videoSourcePath, string coverSourcePath, Sprite coverSprite = null);
        bool Extract(string rkslPath, string extractDir, out RkslManifest manifest, out string audioPath, out string videoPath, out string coverPath);
        List<string> FindAllRkslFiles(IEnumerable<string> extraPaths = null);
        bool LoadManifestOnly(string rkslPath, out RkslManifest manifest);
        bool TryReuseExtracted(string rkslPath, string extractDir, out string audioPath, out string videoPath, out string coverPath);
        RhythmLevelData ToRuntimeData(RkslManifest manifest, AudioClip audioClip, VideoClip videoClip, Sprite cover);
        RkslManifest FromRuntimeData(RhythmLevelData data);
    }
}
