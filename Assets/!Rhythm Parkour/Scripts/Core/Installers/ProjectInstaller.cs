using RKS.RhythmParkour.Core.Managers;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Installers
{
    public sealed class ProjectInstaller : MonoInstaller
    {
        [Header("Managers")]
        [SerializeField] private LocalizationManager localizationPrefab;
        [SerializeField] private AudioManager audioPrefab;
        [SerializeField] private DiscordManager discordPrefab;
        [SerializeField] private SaveManager savePrefab;
        [SerializeField] private TransitionManager transitionPrefab;

        [Header("Rhythm shared")]
        [SerializeField] private GlobalObstacleCatalog obstacleCatalog;

        public override void InstallBindings()
        {
            Container.Bind<LocalizationManager>().FromComponentInNewPrefab(localizationPrefab).AsSingle().NonLazy();
            Container.Bind<AudioManager>().FromComponentInNewPrefab(audioPrefab).AsSingle().NonLazy();
            Container.Bind<DiscordManager>().FromComponentInNewPrefab(discordPrefab).AsSingle().NonLazy();
            Container.Bind<SaveManager>().FromComponentInNewPrefab(savePrefab).AsSingle().NonLazy();
            Container.Bind<TransitionManager>().FromComponentInNewPrefab(transitionPrefab).AsSingle().NonLazy();


            Container.Bind<LevelTransfer>().AsSingle().NonLazy();
            Container.Bind<LevelVisualApplier>().AsSingle();

            if (obstacleCatalog != null)
                Container.Bind<GlobalObstacleCatalog>().FromInstance(obstacleCatalog).AsSingle();
            else
                Container.Bind<GlobalObstacleCatalog>().FromResource("GlobalObstacleCatalog").AsSingle();
        }
    }
}
