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

        public override void InstallBindings()
        {
            BindFromHierarchyOrInstance(timelineUI);
            BindFromHierarchyOrInstance(editorController);
            BindFromHierarchyOrInstance(timelinePreview);
            BindFromHierarchyOrInstance(visualSettings);
            BindFromHierarchyOrInstance(parkourManager);
            BindFromHierarchyOrInstance(conductor);
            Container.Bind<TimelineNotesController>().FromMethod(EnsureTimelineComponent<TimelineNotesController>).AsSingle();
            Container.Bind<TimelineWaveformView>().FromMethod(EnsureTimelineComponent<TimelineWaveformView>).AsSingle();
            Container.Bind<TimelineGridView>().FromMethod(EnsureTimelineComponent<TimelineGridView>).AsSingle();
            Container.Bind<TimelineTransport>().FromMethod(EnsureTimelineComponent<TimelineTransport>).AsSingle();
            Container.Bind<TimelinePropertiesController>().FromMethod(EnsureTimelineComponent<TimelinePropertiesController>).AsSingle();
        }

        static T EnsureTimelineComponent<T>(InjectContext ctx) where T : Component
        {
            var ui = Object.FindFirstObjectByType<TimelineUI>();
            if (ui == null) return null;
            var c = ui.GetComponent<T>();
            if (c == null) c = ui.gameObject.AddComponent<T>();
            return c;
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
