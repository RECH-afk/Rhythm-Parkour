using EasyPeasyFirstPersonController;
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




    public sealed class GameInstaller : MonoInstaller
    {
        [Header("Player")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private Canvas playerCanvas;

        [Header("Rhythm (из иерархии сцены, опционально)")]
        [SerializeField] private Conductor conductor;
        [SerializeField] private RhythmParkourManager parkourManager;
        [SerializeField] private RhythmScoreManager scoreManager;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private LevelResultsUI resultsUI;
        [SerializeField] private IsGameSceneLoader sceneLoader;
        [SerializeField] private MusicInfoUI musicInfoUI;
        [SerializeField] private SphereBeatRotator sphereRotator;
        [SerializeField] private CameraUISway cameraSway;

        public override void InstallBindings()
        {
            if (playerController != null)
                Container.Bind<FirstPersonController>().FromInstance(playerController).AsSingle();
            else
                Container.Bind<FirstPersonController>().FromComponentInHierarchy().AsSingle();

            if (playerCanvas != null)
                Container.Bind<Canvas>().FromInstance(playerCanvas).AsSingle();
            else
                Container.Bind<Canvas>().FromComponentInHierarchy().AsSingle();

            BindFromHierarchyOrInstance(conductor);
            BindFromHierarchyOrInstance(parkourManager);
            BindFromHierarchyOrInstance(scoreManager);
            BindFromHierarchyOrInstance(playerHealth);
            BindFromHierarchyOrInstance(resultsUI);
            BindFromHierarchyOrInstance(sceneLoader);
            BindFromHierarchyOrInstance(musicInfoUI);
            BindFromHierarchyOrInstance(sphereRotator);
            BindFromHierarchyOrInstance(cameraSway);
        }

        void BindFromHierarchyOrInstance<T>(T instance) where T : Component
        {
            if (instance != null)
                Container.Bind<T>().FromInstance(instance).AsSingle();
            else
                Container.Bind<T>().FromComponentInHierarchy().AsSingle();
        }
    }
}
