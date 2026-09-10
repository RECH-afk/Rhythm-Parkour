using UnityEngine;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Installers
{

public sealed class MenuInstaller : MonoInstaller
    {
        [SerializeField] private MenuController menuController;

        public override void InstallBindings()
        {
            if (menuController != null)
                Container.Bind<MenuController>().FromInstance(menuController).AsSingle();
            else
                Container.Bind<MenuController>().FromComponentInHierarchy().AsSingle();
        }
    }
}
