using System;
using UnityEngine;
using Zenject;
using RKS.HadalZone.Core.Managers;

namespace RKS.HadalZone.Core
{
    public abstract class RKSBehaviour : MonoBehaviour, IDisposable
    {
        protected LocalizationManager Localization { get; private set; }
        protected AudioManager Audio { get; private set; }
        protected DiscordManager DiscordRPC { get; private set; }
        protected SaveManager Save { get; private set; }
        protected TransitionManager Transition { get; private set; }

        private bool _isDisposed;

        [Inject]
        public virtual void Construct(
            LocalizationManager localization,
            AudioManager audio,
            DiscordManager discord,
            SaveManager save,
            TransitionManager transition)
        {
            Localization = localization;
            Audio = audio;
            DiscordRPC = discord;
            Save = save;
            Transition = transition;

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

        protected virtual void OnInjected() { } // вызывается до Awake, когда Zenject внедрил все зависимости. (пример использования: инициализация данных, регистрация в менеджерах. антипример: запуск анимаций, работа с UI)
        protected virtual void OnReady() { } // вызывается перед первым Update, когда Zenject и сцена уже загружена. (пример использования: запуск анимаций, работа с UI, геймплейная логика, запуск корутин, обращение к другим объектам. антипример:  инициализация данных, подписки на ивенты)
        protected virtual void OnDisposed() { } // вызывается при OnDestroy. (пример использования: ну крч идеально для отписки от ивентов или там закрытия UI, очистки ресурсов.)

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
