using System;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core
{





    public abstract class RKSBehaviour : MonoBehaviour, IDisposable
    {
        [InjectOptional] protected LocalizationManager Localization { get; private set; }
        [InjectOptional] protected AudioManager Audio { get; private set; }
        [InjectOptional] protected DiscordManager DiscordRPC { get; private set; }
        [InjectOptional] protected SaveManager Save { get; private set; }
        [InjectOptional] protected TransitionManager Transition { get; private set; }

        [InjectOptional] protected DiContainer Container { get; private set; }

        private bool _isDisposed;
        private bool _injected;

        [Inject]
        public virtual void Construct(
            [InjectOptional] LocalizationManager localization,
            [InjectOptional] AudioManager audio,
            [InjectOptional] DiscordManager discord,
            [InjectOptional] SaveManager save,
            [InjectOptional] TransitionManager transition)
        {
            Localization = localization;
            Audio = audio;
            DiscordRPC = discord;
            Save = save;
            Transition = transition;

            if (_injected) return;
            _injected = true;

            try { OnInjected(); }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Error in OnInjected: {ex}");
            }
        }

        protected virtual void Awake() { }

        protected virtual void Start()
        {
            try { OnReady(); }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Error in OnReady: {ex}");
            }
        }

        protected virtual void Update() { }

        protected virtual void OnDestroy()
        {
            Dispose();
        }


        protected virtual void OnInjected() { }

        protected virtual void OnReady() { }

        protected virtual void OnDisposed() { }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try { OnDisposed(); }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Dispose exception: {ex}");
            }
        }

        protected void SafeInvoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception ex)
            {
                Debug.LogError($"[{GetType().Name}] Exception: {ex}");
            }
        }
    }
}
