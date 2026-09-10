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

public sealed class EditorInstaller : MonoInstaller
    {
        [SerializeField] private TimelineUI timelineUI;
        [SerializeField] private RkslEditorController editorController;
        [SerializeField] private TimelinePreview timelinePreview;
        [SerializeField] private LevelEditorVisualSettings visualSettings;
        [SerializeField] private RhythmParkourManager parkourManager;
        [SerializeField] private Conductor conductor;
        [SerializeField] private FileLoader audioLoader;
        [SerializeField] private FileLoader videoLoader;
        [SerializeField] private FileLoader coverLoader;

        public override void InstallBindings()
        {
            BindFromHierarchyOrInstance(timelineUI);
            BindFromHierarchyOrInstance(editorController);
            BindFromHierarchyOrInstance(timelinePreview);
            BindFromHierarchyOrInstance(visualSettings);
            BindFromHierarchyOrInstance(parkourManager);
            BindFromHierarchyOrInstance(conductor);
            BindLoader(audioLoader, AllowedFileTypes.Audio);
            BindLoader(videoLoader, AllowedFileTypes.Video);
            BindLoader(coverLoader, AllowedFileTypes.Photo);
        }

        void BindFromHierarchyOrInstance<T>(T instance) where T : Component
        {
            if (instance != null)
                Container.Bind<T>().FromInstance(instance).AsSingle();
            else
                Container.Bind<T>().FromComponentInHierarchy().AsSingle();
        }

        void BindLoader(FileLoader loader, AllowedFileTypes id)
        {
            if (loader != null)
                Container.Bind<FileLoader>().WithId(id).FromInstance(loader).AsSingle();
        }
    }
}
