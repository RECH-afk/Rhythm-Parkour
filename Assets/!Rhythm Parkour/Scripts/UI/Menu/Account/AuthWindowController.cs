using System;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Backend;

namespace RKS.RhythmParkour.UI
{
    public class AuthWindowController : RKSBehaviour
    {
        [Header("Window")]
        [SerializeField, Tooltip("Root object of the account window that gets shown and hidden")]
        private GameObject _windowRoot;
        [SerializeField, Tooltip("Window title text")]
        private TMP_Text _titleText;

        [Header("Fields")]
        [SerializeField] private TMP_InputField _usernameInput;
        [SerializeField] private TMP_InputField _passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button _signInButton;
        [SerializeField] private Button _signUpButton;
        [SerializeField] private Button _signOutButton;
        [SerializeField] private Button _closeButton;

        [Header("Status")]
        [SerializeField, Tooltip("Optional status bar for operation feedback")]
        private OptionStatusBar _statusBar;

        [Header("Signed In View")]
        [SerializeField, Tooltip("Text with the account name when signed in")]
        private TMP_Text _signedInText;
        [SerializeField, Tooltip("Object holding raw username and password inputs; hidden when signed in")]
        private GameObject _signInFields;

        [Header("Animation")]
        [SerializeField, Tooltip("Window scale at the moment it is hidden")]
        private float _rollScale = 0.92f;
        [SerializeField, Tooltip("Seconds for open and close animations")]
        private float _animDuration = 0.12f;

        [InjectOptional] private BackendService _backendService;

        BackendService _backend;
        bool _working;

        protected override void OnInjected()
        {
            _backend = _backendService;
        }

        protected override void OnReady()
        {
            if (_signInButton != null) _signInButton.onClick.AddListener(OnSignIn);
            if (_signUpButton != null) _signUpButton.onClick.AddListener(OnSignUp);
            if (_signOutButton != null) _signOutButton.onClick.AddListener(OnSignOut);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);

            if (_backend != null && _backend.Account != null)
                _backend.Account.onChanged += OnAccountChanged;

            if (_passwordInput != null)
                _passwordInput.contentType = TMP_InputField.ContentType.Password;

            HideImmediate();
            Refresh();
        }

        protected override void OnDisposed()
        {
            if (_signInButton != null) _signInButton.onClick.RemoveListener(OnSignIn);
            if (_signUpButton != null) _signUpButton.onClick.RemoveListener(OnSignUp);
            if (_signOutButton != null) _signOutButton.onClick.RemoveListener(OnSignOut);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);

            if (_backend != null && _backend.Account != null)
                _backend.Account.onChanged -= OnAccountChanged;
        }

        public void Show()
        {
            if (_windowRoot == null)
                return;
            Refresh();
            _windowRoot.SetActive(true);
            AnimateIn();
        }

        public void Hide()
        {
            if (_windowRoot == null)
                return;
            _windowRoot.transform.DOScale(_rollScale, _animDuration).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(HideImmediate);
        }

        void OnAccountChanged()
        {
            Refresh();
        }

        void Refresh()
        {
            bool signed = _backend != null && _backend.Account != null && _backend.Account.IsSignedIn;

            if (_titleText != null)
                _titleText.text = Loc(signed ? "backend.auth.title" : "backend.auth.signInOrSignUp");

            if (_signInFields != null)
                _signInFields.SetActive(!signed);
            if (_signInButton != null)
                _signInButton.gameObject.SetActive(!signed);
            if (_signUpButton != null)
                _signUpButton.gameObject.SetActive(!signed);
            if (_signOutButton != null)
                _signOutButton.gameObject.SetActive(signed);

            if (_signedInText != null)
                _signedInText.text = signed
                    ? Loc("backend.auth.signedInPrefix") + " " + _backend.Account.AccountName
                    : "";
        }

        void OnSignIn()
        {
            Run(() => _backend.Account.LoginAsync(ReadUsername(), ReadPassword()));
        }

        void OnSignUp()
        {
            Run(() => _backend.Account.RegisterAsync(ReadUsername(), ReadPassword()));
        }

        async void OnSignOut()
        {
            if (_working || _backend == null || _backend.Account == null)
                return;

            _working = true;
            ToggleButtons(false);
            try
            {
                await _backend.Account.SignOutAsync();
            }
            catch (Exception)
            {
            }
            _working = false;
            ToggleButtons(true);
            SetStatus(Loc("backend.auth.signedOut"), OptionStatusState.Info);
            Refresh();
        }

        async void Run(Func<Task<string>> call)
        {
            if (_working)
                return;

            if (_backend == null || _backend.Account == null || string.IsNullOrEmpty(_backend.BaseUrl))
            {
                SetStatus(Loc("backend.err.server"), OptionStatusState.Error);
                return;
            }

            _working = true;
            ToggleButtons(false);
            SetStatus(Loc("backend.auth.working"), OptionStatusState.Busy);

            string error;
            try
            {
                error = await call();
            }
            catch (Exception)
            {
                error = "server";
            }

            _working = false;
            ToggleButtons(true);

            if (string.IsNullOrEmpty(error))
                SetStatus(Loc("backend.auth.success"), OptionStatusState.Success);
            else
                SetStatus(Loc("backend.err." + error), OptionStatusState.Error);

            Refresh();
        }

        string ReadUsername()
        {
            return _usernameInput != null && _usernameInput.text != null ? _usernameInput.text.Trim() : "";
        }

        string ReadPassword()
        {
            return _passwordInput != null && _passwordInput.text != null ? _passwordInput.text : "";
        }

        void ToggleButtons(bool interactive)
        {
            if (_signInButton != null) _signInButton.interactable = interactive;
            if (_signUpButton != null) _signUpButton.interactable = interactive;
            if (_signOutButton != null) _signOutButton.interactable = interactive;
        }

        void SetStatus(string message, OptionStatusState state)
        {
            if (_statusBar != null)
                _statusBar.SetStatus(message, state);
        }

        void AnimateIn()
        {
            _windowRoot.transform.localScale = Vector3.one * _rollScale;
            _windowRoot.transform.DOScale(1f, _animDuration).SetEase(Ease.OutBack).SetUpdate(true);
        }

        void HideImmediate()
        {
            _windowRoot.SetActive(false);
        }

        string Loc(string key)
        {
            return Localization != null ? Localization.GetLocalizedValue(key) : key;
        }
    }
}
