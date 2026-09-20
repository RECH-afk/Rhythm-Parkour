using System;
using System.Collections.Generic;
using UnityEngine;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    [Serializable]
    public class RkslManifest
    {
        public int version = 2;
        public string title = "";
        public string artist = "";
        public string creator = "";
        public float bpm = 128f;
        public float offset = 0f;
        public float duration = 0f;
        public List<ObstacleEvent> events = new List<ObstacleEvent>();
        public string audioFile = "";
        public string videoFile = "";
        public string coverFile = "";

        public bool particlesEnabled = true;
        public Color particleColor = new Color(0.2f, 0.7f, 1f, 1f);
        public string particleSpriteName = "";
        public Color obstacleColor = Color.white;
        public Color trackColor = new Color(0.2f, 0.6f, 1f, 1f);
        public bool sphereRotates = true;
        public bool sphereUseVideo = true;
        public string defaultObstacleMaterialName = "";

        public float BeatToTime(float beat) => offset + beat * 60f / Mathf.Max(1f, bpm);
        public float TimeToBeat(float time) => (time - offset) * Mathf.Max(1f, bpm) / 60f;
        public void SortByTime() => events.Sort((a, b) => a.time.CompareTo(b.time));
    }
}
