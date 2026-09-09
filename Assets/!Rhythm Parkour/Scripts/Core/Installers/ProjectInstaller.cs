using RKS.HadalZone.Core.Dialogue;
using RKS.HadalZone.Core.Managers;
using UnityEngine;
using Zenject;

namespace RKS.HadalZone.Core.Installers
{
    sealed class ProjectInstaller : MonoInstaller
    {
        [Header("Managers")]
        [SerializeField] private LocalizationManager localizationPrefab;
        [SerializeField] private AudioManager audioPrefab;
        [SerializeField] private DiscordManager discordPrefab;
        [SerializeField] private SaveManager savePrefab;
        [SerializeField] private TransitionManager transitionPrefab;
        [SerializeField] private DialogueManager dialoguePrefab;

        public override void InstallBindings()
        {
            Container.Bind<LocalizationManager>().FromComponentInNewPrefab(localizationPrefab).AsSingle().NonLazy();
            Container.Bind<AudioManager>().FromComponentInNewPrefab(audioPrefab).AsSingle().NonLazy();
            Container.Bind<DiscordManager>().FromComponentInNewPrefab(discordPrefab).AsSingle().NonLazy();
            Container.Bind<SaveManager>().FromComponentInNewPrefab(savePrefab).AsSingle().NonLazy();
            Container.Bind<TransitionManager>().FromComponentInNewPrefab(transitionPrefab).AsSingle().NonLazy();
            Container.Bind<DialogueManager>().FromComponentInNewPrefab(dialoguePrefab).AsSingle().NonLazy();
        }
    }
}
