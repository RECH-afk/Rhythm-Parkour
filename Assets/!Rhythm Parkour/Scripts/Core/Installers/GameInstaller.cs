using EasyPeasyFirstPersonController;
using RKS.HadalZone.Core.Dialogue;
using RKS.HadalZone.Core.Managers;
using UnityEngine;
using Zenject;

namespace RKS.HadalZone.Core.Installers
{
    sealed class GameInstaller : MonoInstaller
    {
        [Header("Player")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Canvas playerCanvas;

        public override void InstallBindings()
        {
            Container.Bind<PlayerController>().FromInstance(playerController).AsSingle();
            Container.Bind<Canvas>().FromInstance(playerCanvas).AsSingle();
        }
    }
}
