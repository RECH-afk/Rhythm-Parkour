using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace RKS.HadalZone.Core.Managers
{
    public class TransitionManager : RKSBehaviour
    {
        [SerializeField] private GameObject transitionPrefab;
        [SerializeField] private float delayBeforeLoad = 0f;

        private readonly string triggerIn = "TransitionIn";
        private readonly string triggerOut = "TransitionOut";

        private Animator _animator;
        private bool _isTransitioning;

        protected override void OnReady()
        {
            var instance = Instantiate(transitionPrefab, transform);
            var canvas = instance.GetComponentInChildren<Canvas>(true);
            _animator = instance.GetComponent<Animator>();
        }

        public void LoadScene(string sceneName)
        {
            _ = LoadSceneAsync(sceneName);
        }

        public async Task LoadSceneAsync(string sceneName)
        {
            if (_isTransitioning)
                return;

            _isTransitioning = true;

            if (_animator)
                _animator.SetTrigger(triggerIn);

            if (delayBeforeLoad > 0)
                await Task.Delay(TimeSpan.FromSeconds(delayBeforeLoad));

            var async = SceneManager.LoadSceneAsync(sceneName);
            async.allowSceneActivation = false;

            while (async.progress < 0.9f)
                await Task.Yield();

            async.allowSceneActivation = true;
            await Task.Yield();

            if (_animator)
                _animator.SetTrigger(triggerOut);

            _isTransitioning = false;
        }
    }
}
