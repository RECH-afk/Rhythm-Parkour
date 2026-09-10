using RKS.RhythmParkour.Core;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public class SphereBeatRotator : RKSBehaviour
    {
        [InjectOptional] public Conductor conductor;
        public Vector3 axis = Vector3.up;
        public float degreesPerBeat = 90f;
        [Range(0.02f, 0.4f)] public float smooth = 0.08f;

        float cur, tgt, vel;

        protected override void OnInjected()
        {
            if (conductor == null) conductor = GetComponent<Conductor>();
            cur = transform.eulerAngles.y;
            tgt = cur;
        }

        protected override void Update()
        {
            if (!conductor || !conductor.isPlaying) return;
            tgt = conductor.songPositionBeats * degreesPerBeat;
            cur = Mathf.SmoothDampAngle(cur, tgt, ref vel, smooth);
            Vector3 e = transform.eulerAngles;
            e.y = cur;

            if (axis == Vector3.up) transform.eulerAngles = e;
            else transform.rotation = Quaternion.AngleAxis(cur, axis.normalized);
        }
    }
}
