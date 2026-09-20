using System.Collections;
using System.Collections.Generic;
using System.IO;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using System.Diagnostics;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.Core.Storage;
using RKS.RhythmParkour.UI.Timeline;

using Debug = UnityEngine.Debug;




namespace RKS.RhythmParkour.UI
{
    public class MenuController : RKSBehaviour
    {
        #region Inspector References

        [Header("Main Menu")]
        [SerializeField] private GameObject _mainMenuRoot;
        [SerializeField] private RectTransform _logoTransform;
        [SerializeField] private RectTransform _mainMenuButtonsContainer;

        [Header("Logo Pulse")]
        [SerializeField] private RectTransform _logoPulse;
        [SerializeField] private float _pulseThreshold = 0.3f;
        [SerializeField] private float _pulseScaleAmount = 0.18f;
        [SerializeField] private float _pulseFadeDuration = 0.6f;
        [SerializeField] private float _pulseMaxAlpha = 0.55f;
        [SerializeField] private float _pulseCooldown = 0.18f;

        [Header("Level List Menu")]
        [SerializeField] private GameObject _levelListMenuRoot;
        [SerializeField] private Transform _levelListContainer;
        [SerializeField] private GameObject _levelButtonPrefab;

        [Header("Level Selection")]
        [SerializeField] private Color _selectedButtonColor = new Color(0.45f, 0.85f, 1f, 1f);

        [Header("Level Details Panel")]
        [SerializeField] private GameObject _levelDetailsRoot;
        [SerializeField] private Image _levelCoverImage;
        [SerializeField] private TextMeshProUGUI _detailBpmText;
        [SerializeField] private TextMeshProUGUI _detailDurationText;
        [SerializeField] private TextMeshProUGUI _detailNotesText;
        [SerializeField] private TextMeshProUGUI _detailAuthorText;
        [SerializeField] private TextMeshProUGUI _detailArtistText;
        [SerializeField] private TextMeshProUGUI _detailTrackText;
        [SerializeField] private Button _editLevelButton;
        [SerializeField] private Button _deleteLevelButton;

        [Header("No Levels Window")]
        [SerializeField] private GameObject _noLevelsWindow;

        [Header("Delete Confirmation Window")]
        [SerializeField] private GameObject _deleteConfirmationWindow;

        [Header("Quit Confirmation Window")]
        [SerializeField] private GameObject _quitConfirmationWindow;

        [Header("Settings")]
        [SerializeField] private string _editorSceneName = "IsLevelEditorScene";
        [SerializeField] private string _gameSceneName = "IsGameScene";
        [SerializeField] private float _animDuration = 0.5f;

        #endregion

        #region Private Fields

        private readonly List<string> _foundPaths = new();
        private string _selectedLevelPath;
        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.35f;

        private bool _isTransitioning = false;
        private Image _pulseImage;
        private Vector3 _pulseBaseScale = Vector3.one;
        private float _pulseT = 999f;
        private float _pulseCooldownT;
        private float _prevBassEnergy;
        private float _pulseSearchT;

        private Sequence _transitionSequence;
        private Tween _detailsRefreshTween;

        private readonly Dictionary<GameObject, Vector2> _originalPositions = new();
        private readonly Dictionary<string, GameObject> _levelButtonGOs = new();
        private readonly Dictionary<GameObject, Color> _buttonBaseColors = new();
        private GameObject _selectedButtonGO;

        [HideInInspector]
        [InjectOptional] public LevelTransfer transfer;
        [HideInInspector]
        [InjectOptional] public IRkslStore rksl;

        private IRkslStore Store => rksl ?? RkslStore.Shared;

        [Header("Level Preview")]
        public VideoPlayer previewVideo;
        public Image backgroundImage;
        public float blendFadeDuration = 1.2f;
        public float previewFadeTime = 1f;
        public float resyncThreshold = 0.35f;

        private Material _bgMat;
        private Coroutine _previewRoutine;
        private Coroutine _layoutRebuildRoutine;
        private int _previewSeq;
        private bool _previewVideoReady;
        private Coroutine _autoFinishRoutine;
        private double _lastVideoTime;
        private float _lastResyncTime;
        private int _detailsSeq;
        private Sprite _coverSprite;
        private AudioClip _previewClip;
        private string _previewClipPath = "";
        private string _lastPreviewRkslPath = "";
        private string _lastPreviewAudioPath = "";
        private string _lastPreviewVideoPath = "";
        private string _lastPreviewCoverPath = "";

        #endregion

        #region Compatibility Shims

        public bool IsMenuActive => (_mainMenuRoot != null && _mainMenuRoot.activeSelf) ||
                                    (_levelListMenuRoot != null && _levelListMenuRoot.activeSelf) ||
                                    (_levelDetailsRoot != null && _levelDetailsRoot.activeSelf) ||
                                    (_noLevelsWindow != null && _noLevelsWindow.activeSelf);

        public void ShowMenu() => ShowMainMenu();
        public GameObject MenuPanel => _mainMenuRoot;

        #endregion

        #region Unity Lifecycle

        protected override void OnInjected()
        {
            ValidateReferences();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateDetailsButtonsState();

            if (previewVideo == null)
            {
                previewVideo = GetComponent<VideoPlayer>();
                if (previewVideo == null) previewVideo = FindFirstObjectByType<VideoPlayer>();
                if (previewVideo == null)
                    Debug.LogWarning("[MenuController] Preview VideoPlayer не найден — назначь поле previewVideo.", this);
            }
            if (backgroundImage == null)
            {
                var bgGO = GameObject.Find("Background");
                if (bgGO != null) backgroundImage = bgGO.GetComponent<Image>();
                if (backgroundImage == null)
                    Debug.LogWarning("[MenuController] Фон не найден — назначь поле backgroundImage.", this);
            }
            if (previewVideo != null)
            {
                previewVideo.playOnAwake = false;
                previewVideo.isLooping = true;
                previewVideo.errorReceived -= OnPreviewVideoError;
                previewVideo.errorReceived += OnPreviewVideoError;
            }

            CacheOriginalPosition(_mainMenuRoot);
            CacheOriginalPosition(_levelListMenuRoot);
            CacheOriginalPosition(_levelDetailsRoot);
            CacheOriginalPosition(_noLevelsWindow);
            CacheOriginalPosition(_deleteConfirmationWindow);
            CacheOriginalPosition(_quitConfirmationWindow);
            if (_logoTransform != null) CacheOriginalPosition(_logoTransform.gameObject);
            if (_mainMenuButtonsContainer != null) CacheOriginalPosition(_mainMenuButtonsContainer.gameObject);
            if (GetComponent<TopPanelController>() == null) gameObject.AddComponent<TopPanelController>();
        }

        protected override void OnReady()
        {
            HideAllMenusImmediate();
            if (_mainMenuRoot != null) _mainMenuRoot.SetActive(true);
            PlayStartupAnimations();
        }

        protected override void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_quitConfirmationWindow != null && _quitConfirmationWindow.activeSelf)
                {
                    HideQuitConfirmation();
                }
                else if (_mainMenuRoot != null && _mainMenuRoot.activeSelf && !_isTransitioning)
                {
                    ShowQuitConfirmation();
                }
            }
            SyncPreviewVideo();
            TickLogoPulse();
        }

        private void ResolveLogoPulse()
        {
            if (_logoPulse == null)
            {
                var go = GameObject.Find("Logo pulse");
                if (go == null) go = GameObject.Find("LogoPulse");
                if (go != null) _logoPulse = go.GetComponent<RectTransform>();
            }
            if (_logoPulse == null) return;
            _pulseImage = _logoPulse.GetComponent<Image>();
            if (_logoPulse.localScale.sqrMagnitude > 0.001f) _pulseBaseScale = _logoPulse.localScale;
            _logoPulse.gameObject.SetActive(false);
            _pulseT = 999f;
            _pulseCooldownT = 0f;
            _prevBassEnergy = 0f;
        }

        private void TickLogoPulse()
        {
            if (_logoPulse == null)
            {
                _pulseSearchT -= Time.unscaledDeltaTime;
                if (_pulseSearchT > 0f) return;
                _pulseSearchT = 2f;
                ResolveLogoPulse();
            }
            if (_logoPulse == null) return;
            float energy = 0f;
            bool music = Audio != null && Audio.IsMusicPlaying();
            if (music) energy = Audio.GetMusicLevel();
            float dt = Time.unscaledDeltaTime;
            _pulseCooldownT -= dt;
            if (music && _pulseCooldownT <= 0f && energy >= _pulseThreshold && _prevBassEnergy < _pulseThreshold)
            {
                _pulseCooldownT = Mathf.Max(0.05f, _pulseCooldown);
                _pulseT = 0f;
                if (!_logoPulse.gameObject.activeSelf) _logoPulse.gameObject.SetActive(true);
                _logoPulse.localScale = _pulseBaseScale * (1f + Mathf.Max(0f, _pulseScaleAmount));
                SetPulseAlpha(Mathf.Max(0f, Mathf.Min(1f, _pulseMaxAlpha)));
            }
            _prevBassEnergy = energy;
            if (!_logoPulse.gameObject.activeSelf) return;
            if (!music)
            {
                _logoPulse.gameObject.SetActive(false);
                _pulseT = 999f;
                return;
            }
            _pulseT += dt;
            float dur = Mathf.Max(0.05f, _pulseFadeDuration);
            float k = Mathf.Clamp01(_pulseT / dur);
            float e = (1f - k) * (1f - k);
            _logoPulse.localScale = _pulseBaseScale * (1f + Mathf.Max(0f, _pulseScaleAmount) * e);
            SetPulseAlpha(Mathf.Max(0f, Mathf.Min(1f, _pulseMaxAlpha)) * e);
            if (k >= 1f) _logoPulse.gameObject.SetActive(false);
        }

        private void SetPulseAlpha(float a)
        {
            if (_pulseImage == null) _pulseImage = _logoPulse.GetComponent<Image>();
            if (_pulseImage == null) return;
            Color c = _pulseImage.color;
            c.a = a;
            _pulseImage.color = c;
        }

        private void SyncPreviewVideo()
        {
            if (!_previewVideoReady || Audio == null || previewVideo == null) return;
            if (!previewVideo.isPlaying || !previewVideo.isPrepared) return;
            float musicTime = Audio.GetMusicTime();
            if (musicTime < 0f) return;
            double vt = previewVideo.time;
            if (Mathf.Abs((float)(vt - _lastVideoTime)) < 0.0001f) return;
            _lastVideoTime = vt;
            if (Time.unscaledTime - _lastResyncTime < 1f) return;
            if (Mathf.Abs((float)(vt - musicTime)) > resyncThreshold)
            {
                previewVideo.time = musicTime;
                _lastVideoTime = musicTime;
                _lastResyncTime = Time.unscaledTime;
            }
        }

        protected override void OnDisposed()
        {
            StopLevelPreview();
            if (_previewClip != null)
            {
                Destroy(_previewClip);
                _previewClip = null;
            }
            if (_coverSprite != null)
            {
                if (_coverSprite.texture != null) Destroy(_coverSprite.texture);
                Destroy(_coverSprite);
                _coverSprite = null;
            }
            if (_bgMat != null && _bgMat.HasProperty("_BaseStrength"))
                _bgMat.SetFloat("_BaseStrength", 0f);
            if (previewVideo != null) previewVideo.errorReceived -= OnPreviewVideoError;
            if (_detailsRefreshTween != null && _detailsRefreshTween.IsActive()) _detailsRefreshTween.Kill();
            DOTween.Kill(this);
            _transitionSequence?.Kill(true);
        }

        #endregion

        #region Initialization & Bindings

        private void CacheOriginalPosition(GameObject obj)
        {
            if (obj != null)
            {
                var rt = obj.GetComponent<RectTransform>();
                if (rt != null) _originalPositions[obj] = rt.anchoredPosition;
            }
        }

        private void ValidateReferences()
        {
            if (_mainMenuRoot == null || _levelListContainer == null || _levelButtonPrefab == null)
                Debug.LogError("[MenuController] Критические ссылки UI не назначены в инспекторе!", this);
        }

        private float GetScreenOffsetX() => Screen.width + 100f;
        private float GetScreenOffsetY() => Screen.height + 100f;

        private void PlayStartupAnimations()
        {
            if (_logoTransform == null || _mainMenuButtonsContainer == null) return;

            _logoTransform.DOKill(true);
            _mainMenuButtonsContainer.DOKill(true);

            Vector2 origLogoPos = _originalPositions.TryGetValue(_logoTransform.gameObject, out var oL) ? oL : _logoTransform.anchoredPosition;
            Vector2 origBtnsPos = _originalPositions.TryGetValue(_mainMenuButtonsContainer.gameObject, out var oB) ? oB : _mainMenuButtonsContainer.anchoredPosition;

            _logoTransform.anchoredPosition = origLogoPos + new Vector2(-GetScreenOffsetX(), 0f);
            _logoTransform.localScale = Vector3.one * 0.98f;
            _logoTransform.localRotation = Quaternion.Euler(0, 0, -1.5f);

            var logoSeq = DOTween.Sequence()
                .Join(_logoTransform.DOAnchorPosX(origLogoPos.x, _animDuration * 1.6f).SetEase(Ease.OutQuint))
                .Join(_logoTransform.DORotate(Vector3.zero, _animDuration * 1.6f, RotateMode.Fast))
                .Join(_logoTransform.DOScale(1f, _animDuration * 1.6f).SetEase(Ease.OutBack))
                .SetTarget(this);

            _mainMenuButtonsContainer.anchoredPosition = origBtnsPos + new Vector2(GetScreenOffsetX(), 0f);
            _mainMenuButtonsContainer.localScale = Vector3.one * 0.98f;
            _mainMenuButtonsContainer.localRotation = Quaternion.Euler(0, 0, 1.5f);

            var btnsSeq = DOTween.Sequence()
                .Join(_mainMenuButtonsContainer.DOAnchorPosX(origBtnsPos.x, _animDuration * 1.6f).SetEase(Ease.OutQuint).SetDelay(0.2f))
                .Join(_mainMenuButtonsContainer.DORotate(Vector3.zero, _animDuration * 1.6f, RotateMode.Fast).SetDelay(0.2f))
                .Join(_mainMenuButtonsContainer.DOScale(1f, _animDuration * 1.6f).SetEase(Ease.OutBack).SetDelay(0.2f))
                .SetTarget(this);

            Sequence startupSeq = DOTween.Sequence();
            startupSeq.Join(logoSeq);
            startupSeq.Join(btnsSeq);
        }

        private void HideAllMenusImmediate()
        {
            if (_mainMenuRoot != null) _mainMenuRoot.SetActive(false);
            if (_levelListMenuRoot != null) _levelListMenuRoot.SetActive(false);
            if (_levelDetailsRoot != null) _levelDetailsRoot.SetActive(false);
            if (_noLevelsWindow != null) _noLevelsWindow.SetActive(false);
            if (_deleteConfirmationWindow != null) _deleteConfirmationWindow.SetActive(false);
            if (_quitConfirmationWindow != null) _quitConfirmationWindow.SetActive(false);
        }

        #endregion

        #region Navigation & Transitions

        private void TransitionTo(GameObject targetWindow)
        {
            if (_isTransitioning || targetWindow == null || targetWindow.activeSelf) return;

            _isTransitioning = true;
            _transitionSequence?.Kill(true);
            _transitionSequence = DOTween.Sequence().SetTarget(this);

            List<GameObject> windowsToHide = new List<GameObject>();

            bool isDetails = targetWindow == _levelDetailsRoot;

            if (isDetails && _levelListMenuRoot != null && !_levelListMenuRoot.activeSelf)
            {
                var listRt = _levelListMenuRoot.GetComponent<RectTransform>();
                listRt.DOKill(true);
                Vector2 listOrig = _originalPositions.TryGetValue(_levelListMenuRoot, out var lo) ? lo : listRt.anchoredPosition;

                listRt.anchoredPosition = listOrig + new Vector2(GetScreenOffsetX(), 0f);
                listRt.localScale = Vector3.one * 0.97f;
                listRt.localRotation = Quaternion.Euler(0, 0, 1.5f);
                _levelListMenuRoot.SetActive(true);

                var listAppearSeq = DOTween.Sequence()
                    .Join(listRt.DOAnchorPos(listOrig, _animDuration * 1.2f).SetEase(Ease.OutQuint))
                    .Join(listRt.DORotate(Vector3.zero, _animDuration * 1.2f, RotateMode.Fast))
                    .Join(listRt.DOScale(1f, _animDuration * 1.2f).SetEase(Ease.OutBack))
                    .SetTarget(_levelListMenuRoot);

                _transitionSequence.Join(listAppearSeq);
            }

            if (_mainMenuRoot != null && _mainMenuRoot.activeSelf && _mainMenuRoot != targetWindow)
                windowsToHide.Add(_mainMenuRoot);

            if (_levelListMenuRoot != null && _levelListMenuRoot.activeSelf && _levelListMenuRoot != targetWindow && !isDetails)
                windowsToHide.Add(_levelListMenuRoot);

            if (_levelDetailsRoot != null && _levelDetailsRoot.activeSelf && _levelDetailsRoot != targetWindow)
                windowsToHide.Add(_levelDetailsRoot);

            if (_noLevelsWindow != null && _noLevelsWindow.activeSelf && _noLevelsWindow != targetWindow)
                windowsToHide.Add(_noLevelsWindow);

            if (_deleteConfirmationWindow != null && _deleteConfirmationWindow.activeSelf && _deleteConfirmationWindow != targetWindow)
                windowsToHide.Add(_deleteConfirmationWindow);

            if (_quitConfirmationWindow != null && _quitConfirmationWindow.activeSelf && _quitConfirmationWindow != targetWindow)
                windowsToHide.Add(_quitConfirmationWindow);

            foreach (var win in windowsToHide)
            {
                var rt = win.GetComponent<RectTransform>();
                rt.DOKill(true);
                Vector2 origPos = _originalPositions.TryGetValue(win, out var o) ? o : rt.anchoredPosition;

                rt.anchoredPosition = origPos;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;

                Vector2 hideTarget = origPos;
                float rotationZ = 0f;

                if (win == _mainMenuRoot)
                {
                    hideTarget = origPos + new Vector2(-GetScreenOffsetX(), 0f);
                    rotationZ = -1.5f;
                }
                else if (win == _levelListMenuRoot)
                {
                    hideTarget = origPos + new Vector2(GetScreenOffsetX(), 0f);
                    rotationZ = 1.5f;
                }
                else if (win == _levelDetailsRoot)
                {
                    hideTarget = origPos + new Vector2(0f, GetScreenOffsetY());
                    rotationZ = 1f;
                }
                else if (win == _noLevelsWindow)
                {
                    hideTarget = origPos + new Vector2(0f, -GetScreenOffsetY());
                    rotationZ = -1f;
                }
                else if (win == _deleteConfirmationWindow)
                {
                    hideTarget = origPos + new Vector2(0f, -GetScreenOffsetY());
                    rotationZ = -1f;
                }
                else if (win == _quitConfirmationWindow)
                {
                    hideTarget = origPos + new Vector2(0f, -GetScreenOffsetY());
                    rotationZ = -1f;
                }

                var hideSeq = DOTween.Sequence()
                    .Join(rt.DOAnchorPos(hideTarget, _animDuration).SetEase(Ease.InQuart).SetTarget(win))
                    .Join(rt.DORotate(new Vector3(0, 0, rotationZ), _animDuration, RotateMode.Fast))
                    .Join(rt.DOScale(0.97f, _animDuration).SetEase(Ease.InQuart))
                    .SetTarget(win);

                _transitionSequence.Append(hideSeq);
            }

            var rtTarget = targetWindow.GetComponent<RectTransform>();
            rtTarget.DOKill(true);
            Vector2 origTarget = _originalPositions.TryGetValue(targetWindow, out var ot) ? ot : rtTarget.anchoredPosition;

            Vector2 startPos = origTarget;
            float startRotZ = 0f;

            if (targetWindow == _mainMenuRoot)
            {
                startPos = origTarget + new Vector2(-GetScreenOffsetX(), 0f);
                startRotZ = -1.5f;
            }
            else if (targetWindow == _levelListMenuRoot)
            {
                startPos = origTarget + new Vector2(GetScreenOffsetX(), 0f);
                startRotZ = 1.5f;
            }
            else if (targetWindow == _levelDetailsRoot)
            {
                startPos = origTarget + new Vector2(0f, GetScreenOffsetY());
                startRotZ = 1f;
            }
            else if (targetWindow == _noLevelsWindow)
            {
                startPos = origTarget + new Vector2(0f, -GetScreenOffsetY() * 0.8f);
                startRotZ = -1f;
            }
            else if (targetWindow == _deleteConfirmationWindow)
            {
                startPos = origTarget + new Vector2(0f, -GetScreenOffsetY() * 0.8f);
                startRotZ = -1f;
            }
            else if (targetWindow == _quitConfirmationWindow)
            {
                startPos = origTarget + new Vector2(0f, -GetScreenOffsetY() * 0.8f);
                startRotZ = -1f;
            }

            rtTarget.anchoredPosition = startPos;
            rtTarget.localScale = Vector3.one * 0.97f;
            rtTarget.localRotation = Quaternion.Euler(0, 0, startRotZ);

            targetWindow.SetActive(true);

            var appearSeq = DOTween.Sequence()
                .Join(rtTarget.DOAnchorPos(origTarget, _animDuration * 1.2f).SetEase(Ease.OutQuint).SetTarget(targetWindow))
                .Join(rtTarget.DORotate(Vector3.zero, _animDuration * 1.2f, RotateMode.Fast))
                .Join(rtTarget.DOScale(1f, _animDuration * 1.2f).SetEase(Ease.OutBack))
                .SetTarget(targetWindow);

            _transitionSequence.Append(appearSeq);

            _transitionSequence.AppendCallback(() =>
            {
                foreach (var win in windowsToHide)
                {
                    win.SetActive(false);
                }
            });

            _transitionSequence.OnComplete(() =>
            {
                _isTransitioning = false;
                if (targetWindow == _levelListMenuRoot) RefreshLevelList();
            });
        }

        private void ShowMainMenu()
        {
            if (_isTransitioning) return;
            _selectedLevelPath = null;
            SetSelectedButton(null);
            UpdateDetailsButtonsState();
            HideDeleteConfirmationImmediate();
            HideQuitConfirmationImmediate();
            TransitionTo(_mainMenuRoot);
        }

        public void ShowLevelList()
        {
            if (_isTransitioning) return;
            _selectedLevelPath = null;
            SetSelectedButton(null);
            UpdateDetailsButtonsState();
            HideDeleteConfirmationImmediate();
            HideQuitConfirmationImmediate();

            _foundPaths.Clear();
            _foundPaths.AddRange(Store.FindAllRkslFiles(transfer != null ? transfer.GetSavedPaths() : null));

            if (_foundPaths.Count == 0)
                TransitionTo(_noLevelsWindow);
            else
                TransitionTo(_levelListMenuRoot);
        }

        private void ShowLevelDetails(string path)
        {
            if (_isTransitioning) return;
            if (_selectedLevelPath == path && _levelDetailsRoot != null && _levelDetailsRoot.activeSelf) return;
            bool switchWhileOpen = _selectedLevelPath != path
                && _levelDetailsRoot != null
                && _levelDetailsRoot.activeSelf;
            _selectedLevelPath = path;
            _detailsSeq++;
            SetSelectedButton(path);
            UpdateDetailsButtonsState();
            PopulateDetails(path);
            StartCoroutine(DetailsMediaRoutine(path, _detailsSeq));
            if (switchWhileOpen)
                PlayDetailsRefreshAnimation();
            else
                TransitionTo(_levelDetailsRoot);
        }

        private void PlayDetailsRefreshAnimation()
        {
            if (_levelDetailsRoot == null) return;
            var rt = _levelDetailsRoot.GetComponent<RectTransform>();
            if (rt == null) return;
            if (_detailsRefreshTween != null && _detailsRefreshTween.IsActive()) _detailsRefreshTween.Kill();
            Vector2 origPos = _originalPositions.TryGetValue(_levelDetailsRoot, out var o) ? o : rt.anchoredPosition;
            rt.anchoredPosition = origPos;
            _detailsRefreshTween = DOTween.Sequence()
                .Append(rt.DOAnchorPosY(origPos.y - 120f, 0.18f).SetEase(Ease.InQuad))
                .Append(rt.DOAnchorPosY(origPos.y, 0.35f).SetEase(Ease.OutCubic));
        }

        private void HideLevelDetails()
        {
            if (_isTransitioning) return;
            if (_levelDetailsRoot == null || !_levelDetailsRoot.activeSelf) return;

            _isTransitioning = true;
            _selectedLevelPath = null;
            SetSelectedButton(null);
            UpdateDetailsButtonsState();

            var rt = _levelDetailsRoot.GetComponent<RectTransform>();
            rt.DOKill(true);
            Vector2 origPos = _originalPositions.TryGetValue(_levelDetailsRoot, out var o) ? o : rt.anchoredPosition;

            Sequence hideSeq = DOTween.Sequence()
                .Join(rt.DOAnchorPos(origPos + new Vector2(0f, GetScreenOffsetY()), _animDuration * 1.2f).SetEase(Ease.InQuart).SetTarget(_levelDetailsRoot))
                .Join(rt.DORotate(new Vector3(0, 0, 1f), _animDuration * 1.2f, RotateMode.Fast))
                .Join(rt.DOScale(0.97f, _animDuration * 1.2f).SetEase(Ease.InQuart))
                .SetTarget(_levelDetailsRoot);

            hideSeq.OnComplete(() =>
            {
                _levelDetailsRoot.SetActive(false);
                rt.anchoredPosition = origPos;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                _isTransitioning = false;
            });
        }

        private void HideDeleteConfirmationImmediate()
        {
            if (_deleteConfirmationWindow != null && _deleteConfirmationWindow.activeSelf)
            {
                _deleteConfirmationWindow.GetComponent<RectTransform>()?.DOKill(true);
                _deleteConfirmationWindow.SetActive(false);
            }
        }

        private void HideLevelDetailsImmediate()
        {
            StopLevelPreview();
            if (_levelDetailsRoot != null && _levelDetailsRoot.activeSelf)
            {
                _levelDetailsRoot.GetComponent<RectTransform>()?.DOKill(true);
                _levelDetailsRoot.SetActive(false);
            }
        }

        #endregion

        #region Quit Confirmation

        public void ShowQuitConfirmation()
        {
            if (_isTransitioning) return;
            TransitionTo(_quitConfirmationWindow);
        }

        public void HideQuitConfirmation()
        {
            if (_quitConfirmationWindow == null || !_quitConfirmationWindow.activeSelf) return;

            _isTransitioning = true;
            var rt = _quitConfirmationWindow.GetComponent<RectTransform>();

            rt.DOKill(true);
            Vector2 origPos = _originalPositions.TryGetValue(_quitConfirmationWindow, out var o) ? o : rt.anchoredPosition;

            Sequence seq = DOTween.Sequence()
                .Join(rt.DOAnchorPos(origPos + new Vector2(0f, -GetScreenOffsetY()), _animDuration * 0.5f).SetEase(Ease.InQuart))
                .Join(rt.DORotate(new Vector3(0, 0, -1f), _animDuration * 0.5f, RotateMode.Fast))
                .Join(rt.DOScale(0.97f, _animDuration * 0.5f).SetEase(Ease.InQuart))
                .SetTarget(_quitConfirmationWindow);

            seq.OnComplete(() => {
                _quitConfirmationWindow.SetActive(false);
                rt.anchoredPosition = origPos;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                _isTransitioning = false;

                TransitionTo(_mainMenuRoot);
            });
        }

        private void HideQuitConfirmationImmediate()
        {
            if (_quitConfirmationWindow != null && _quitConfirmationWindow.activeSelf)
            {
                _quitConfirmationWindow.GetComponent<RectTransform>()?.DOKill(true);
                _quitConfirmationWindow.SetActive(false);
            }
        }

        public void ConfirmQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Level List Logic

        private void RefreshLevelList()
        {
            if (_levelListContainer == null) return;

            ClearSelectedButtonVisual();
            _levelButtonGOs.Clear();
            _buttonBaseColors.Clear();
            for (int i = _levelListContainer.childCount - 1; i >= 0; i--)
            {
                var child = _levelListContainer.GetChild(i).gameObject;
                DOTween.Kill(child);
                child.SetActive(false);
                Destroy(child);
            }

            _foundPaths.Clear();
            _foundPaths.AddRange(Store.FindAllRkslFiles(transfer != null ? transfer.GetSavedPaths() : null));

            for (int i = 0; i < _foundPaths.Count; i++) CreateLevelListItem(_foundPaths[i], i);

            RequestLayoutRefresh();
        }

        private void RequestLayoutRefresh()
        {
            var containerRT = _levelListContainer as RectTransform;
            if (containerRT == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRT);
            if (_layoutRebuildRoutine != null) StopCoroutine(_layoutRebuildRoutine);
            _layoutRebuildRoutine = StartCoroutine(RebuildLayoutNextFrame(containerRT));
        }

        private IEnumerator RebuildLayoutNextFrame(RectTransform containerRT)
        {
            yield return new WaitForEndOfFrame();
            _layoutRebuildRoutine = null;
            if (containerRT == null) yield break;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRT);
        }

        private void CreateLevelListItem(string path, int order)
        {
            RkslManifest man = null;
            if (!Store.LoadManifestOnly(path, out man) || man == null)
                Debug.LogWarning($"[MenuController] Не удалось прочитать манифест уровня: {path}", this);

            string title = (man != null && !string.IsNullOrEmpty(man.title)) ? man.title : Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(title)) title = "Без названия";

            if (_levelButtonPrefab == null)
            {
                Debug.LogError("[MenuController] _levelButtonPrefab не назначен — кнопка уровня не создана.", this);
                return;
            }
            var btnGO = Instantiate(_levelButtonPrefab, _levelListContainer, false);

            var rect = btnGO.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;

                var le = btnGO.GetComponent<LayoutElement>();
                if (le == null) le = btnGO.AddComponent<LayoutElement>();
                le.minHeight = 80f;
                le.flexibleWidth = 1f;
            }

            btnGO.SetActive(true);

            TextMeshProUGUI btnText = btnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = title;

            Button btn = btnGO.GetComponent<Button>();
            if (btn == null) btn = btnGO.GetComponentInChildren<Button>(true);
            if (btn == null)
            {
                Debug.LogError("[MenuController] В префабе кнопки уровня нет компонента Button.", btnGO);
                return;
            }
            btn.interactable = true;
            btn.onClick.AddListener(() => HandleLevelClick(path));
            _levelButtonGOs[path] = btnGO;

            AnimateLevelButtonIn(btnGO, order, man == null);
        }

        private void SetSelectedButton(string path)
        {
            ClearSelectedButtonVisual();
            if (string.IsNullOrEmpty(path)) return;
            if (!_levelButtonGOs.TryGetValue(path, out var btnGO) || btnGO == null) return;
            var img = btnGO.GetComponent<Image>();
            if (img == null) img = btnGO.GetComponentInChildren<Image>(true);
            if (img == null) return;
            _selectedButtonGO = btnGO;
            _buttonBaseColors[btnGO] = img.color;
            img.color = _selectedButtonColor;
        }

        private void ClearSelectedButtonVisual()
        {
            if (_selectedButtonGO == null) return;
            var img = _selectedButtonGO.GetComponent<Image>();
            if (img == null) img = _selectedButtonGO.GetComponentInChildren<Image>(true);
            if (img != null && _buttonBaseColors.TryGetValue(_selectedButtonGO, out var baseColor))
                img.color = baseColor;
            _buttonBaseColors.Remove(_selectedButtonGO);
            _selectedButtonGO = null;
        }

        private void AnimateLevelButtonIn(GameObject btnGO, int order, bool invalid)
        {
            var feedback = btnGO.GetComponent<UIInteractionFeedback>();
            if (feedback != null) feedback.enabled = false;
            btnGO.transform.localScale = Vector3.zero;
            btnGO.transform
                .DOScale(Vector3.one, 0.35f)
                .SetDelay(Mathf.Min(order * 0.06f, 0.6f))
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    if (feedback != null) feedback.enabled = !invalid;
                });
            if (invalid) AddCollapseOnHover(btnGO);
        }

        private void AddCollapseOnHover(GameObject btnGO)
        {
            var trigger = btnGO.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btnGO.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ =>
            {
                btnGO.transform.DOKill();
                btnGO.transform.DOScale(Vector3.zero, 0.12f).SetEase(Ease.OutQuad);
            });
            trigger.triggers.Add(entry);
        }

        private void HandleLevelClick(string path)
        {
            if (_isTransitioning) return;

            float timeSinceLastClick = Time.unscaledTime - _lastClickTime;

            if (timeSinceLastClick < DoubleClickThreshold && _selectedLevelPath == path)
            {
                LoadAndPlay(path);
            }
            else
            {
                ShowLevelDetails(path);
            }

            _lastClickTime = Time.unscaledTime;
        }

        #endregion

        #region Level Details Logic

        private void UpdateDetailsButtonsState()
        {
            bool hasSelection = !string.IsNullOrEmpty(_selectedLevelPath);
            if (_editLevelButton != null) _editLevelButton.interactable = hasSelection;
            if (_deleteLevelButton != null) _deleteLevelButton.interactable = hasSelection;
        }

        private void PopulateDetails(string path)
        {
            RkslManifest man = null;
            if (!Store.LoadManifestOnly(path, out man) || man == null)
                Debug.LogWarning($"[MenuController] Не удалось прочитать манифест уровня: {path}", this);

            if (_detailBpmText) _detailBpmText.text = man != null ? $"{man.bpm:0}" : "N/A";
            if (_detailDurationText) _detailDurationText.text = man != null && man.duration > 0f ? FormatDuration(man.duration) : "N/A";
            if (_detailNotesText) _detailNotesText.text = man != null ? $"{man.events.Count}" : "N/A";
            if (_detailAuthorText) _detailAuthorText.text = (man != null && !string.IsNullOrEmpty(man.creator)) ? man.creator : "N/A";
            if (_detailArtistText) _detailArtistText.text = (man != null && !string.IsNullOrEmpty(man.artist)) ? man.artist : "N/A";
            if (_detailTrackText) _detailTrackText.text = (man != null && !string.IsNullOrEmpty(man.title)) ? man.title : "N/A";
            string topTitle = (man != null && !string.IsNullOrEmpty(man.title)) ? man.title : Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(topTitle)) topTitle = "Без названия";
            CurrentPreviewTitle = topTitle;
            CurrentPreviewArtist = (man != null && !string.IsNullOrEmpty(man.artist)) ? man.artist : "Unknown";
        }

        private IEnumerator DetailsMediaRoutine(string rkslPath, int seq)
        {
            yield return null;
            if (seq != _detailsSeq) yield break;

            string extractDir = Path.Combine(Application.temporaryCachePath, "RkslCover_" + Path.GetFileNameWithoutExtension(rkslPath));

            string audioPath;
            string videoPath;
            string coverPath;
            if (rkslPath == _lastPreviewRkslPath && PreviewCacheValid())
            {
                audioPath = _lastPreviewAudioPath;
                videoPath = _lastPreviewVideoPath;
                coverPath = _lastPreviewCoverPath;
            }
            else if (Store.TryReuseExtracted(rkslPath, extractDir, out audioPath, out videoPath, out coverPath))
            {
                _lastPreviewRkslPath = rkslPath;
                _lastPreviewAudioPath = audioPath;
                _lastPreviewVideoPath = videoPath;
                _lastPreviewCoverPath = coverPath;
            }
            else if (!Store.Extract(rkslPath, extractDir, out RkslManifest man, out audioPath, out videoPath, out coverPath))
            {
                yield break;
            }
            else
            {
                _lastPreviewRkslPath = rkslPath;
                _lastPreviewAudioPath = audioPath;
                _lastPreviewVideoPath = videoPath;
                _lastPreviewCoverPath = coverPath;
            }
            if (seq != _detailsSeq) yield break;

            yield return null;
            if (seq != _detailsSeq) yield break;

            PlayLevelPreview(audioPath, videoPath);
            SetCoverSprite(coverPath);
        }

        private void SetCoverSprite(string coverPath)
        {
            if (_levelCoverImage == null) return;
            if (_coverSprite != null)
            {
                if (_coverSprite.texture != null) Destroy(_coverSprite.texture);
                Destroy(_coverSprite);
                _coverSprite = null;
            }
            _levelCoverImage.sprite = null;
            if (string.IsNullOrEmpty(coverPath) || !File.Exists(coverPath)) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(coverPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    _coverSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                    _levelCoverImage.sprite = _coverSprite;
                }
                else
                {
                    Destroy(tex);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MenuController] Не удалось загрузить обложку: {e.Message}");
            }
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.RoundToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }

        private bool PreviewCacheValid()
        {
            if (string.IsNullOrEmpty(_lastPreviewAudioPath) || !File.Exists(_lastPreviewAudioPath)) return false;
            if (!string.IsNullOrEmpty(_lastPreviewVideoPath) && !File.Exists(_lastPreviewVideoPath)) return false;
            return true;
        }

        #region Level Preview

        public bool IsPreviewPlaying => (Audio != null && Audio.IsMusicPlaying())
            || (previewVideo != null && previewVideo.isPlaying);

        public string CurrentPreviewTitle { get; private set; } = "";
        public string CurrentPreviewArtist { get; private set; } = "";
        public bool PreviewPaused { get; private set; }
        public bool HasPreview => !string.IsNullOrEmpty(_lastPreviewAudioPath);
        public bool IsDetailsVisible => _levelDetailsRoot != null && _levelDetailsRoot.activeSelf;
        public bool IsMainMenuVisible => _mainMenuRoot != null && _mainMenuRoot.activeSelf;

        public void TogglePreviewPlayback()
        {
            if (!HasPreview) return;
            if (PreviewPaused)
            {
                PreviewPaused = false;
                if (Audio != null) Audio.ResumeAll();
                if (previewVideo != null) previewVideo.Play();
            }
            else if (IsPreviewPlaying)
            {
                PreviewPaused = true;
                if (Audio != null) Audio.PauseAll();
                if (previewVideo != null) previewVideo.Pause();
            }
        }

        public bool RepeatEnabled { get; private set; } = true;

        public void ToggleRepeat()
        {
            if (!HasPreview) return;
            RepeatEnabled = !RepeatEnabled;
            if (Audio != null) Audio.SetMusicLoop(RepeatEnabled);
            if (previewVideo != null) previewVideo.isLooping = RepeatEnabled;
        }

        public float PreviewTime => Audio != null ? Audio.GetMusicTime() : -1f;
        public float PreviewTrackLength => _previewClip != null ? _previewClip.length : -1f;

        public void PlayLevelPreview(string audioPath, string videoPath)
        {
            PreviewPaused = false;
            if (previewVideo != null && !previewVideo.gameObject.activeSelf)
                previewVideo.gameObject.SetActive(true);
            bool wasShowingVideo = _previewVideoReady;
            StopLevelPreviewInternal();
            _previewSeq++;
            if (wasShowingVideo) FadeBackgroundBlend(0f, 0.35f);
            _previewRoutine = StartCoroutine(PreviewRoutine(audioPath, videoPath, _previewSeq));
            StartAutoFinishWatcher();
        }

        public void StopLevelPreview()
        {
            StopLevelPreviewInternal();
            _previewSeq++;
            _detailsSeq++;
            FadeBackgroundBlend(0f);
            PreviewPaused = false;
            CurrentPreviewTitle = "";
            CurrentPreviewArtist = "";
        }

        private void StopLevelPreviewInternal()
        {
            _previewVideoReady = false;
            if (_previewRoutine != null)
            {
                StopCoroutine(_previewRoutine);
                _previewRoutine = null;
            }
            StopAutoFinishWatcher();
            if (Audio != null) Audio.StopMusic();
            if (previewVideo != null) previewVideo.Stop();
            if (_bgMat != null) _bgMat.DOKill();
        }

        private void StartAutoFinishWatcher()
        {
            StopAutoFinishWatcher();
            _autoFinishRoutine = StartCoroutine(AutoFinishWatcher());
        }

        private void StopAutoFinishWatcher()
        {
            if (_autoFinishRoutine != null)
            {
                StopCoroutine(_autoFinishRoutine);
                _autoFinishRoutine = null;
            }
        }

        private IEnumerator AutoFinishWatcher()
        {
            bool heard = false;
            while (true)
            {
                while (_previewRoutine != null) yield return null;
                if (IsPreviewPlaying) heard = true;
                if (!heard)
                {
                    yield return null;
                    continue;
                }
                yield return new WaitUntil(() => !IsPreviewPlaying);
                if (_previewRoutine != null) continue;
                if (PreviewPaused || RepeatEnabled)
                {
                    yield return new WaitUntil(() => IsPreviewPlaying || _previewRoutine != null);
                    continue;
                }
                StopLevelPreview();
                yield break;
            }
        }

        private IEnumerator PreviewRoutine(string audioPath, string videoPath, int seq)
        {
            yield return null;

            AudioClip clip = null;
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
                if (_previewClip != null && _previewClipPath == audioPath)
                {
                    clip = _previewClip;
                }
                else
                {
                    if (_previewClip != null)
                    {
                        Destroy(_previewClip);
                        _previewClip = null;
                        _previewClipPath = "";
                    }
                    using (var uwr = UnityWebRequestMultimedia.GetAudioClip(RkslStore.GetFileUri(audioPath), RkslStore.GetAudioType(audioPath)))
                    {
                        yield return uwr.SendWebRequest();
                        if (seq != _previewSeq) yield break;
                        if (uwr.result == UnityWebRequest.Result.Success)
                            clip = DownloadHandlerAudioClip.GetContent(uwr);
                        else
                            Debug.LogWarning($"[MenuController] Preview audio failed: {uwr.error}", this);
                    }
                    yield return null;
                    if (seq != _previewSeq) yield break;
                    _previewClip = clip;
                    _previewClipPath = clip != null ? audioPath : "";
                }
            }
            if (seq != _previewSeq) yield break;

            bool wantVideo = !string.IsNullOrEmpty(videoPath) && File.Exists(videoPath) && previewVideo != null;
            float start = clip != null && clip.length > 1f ? Random.Range(0f, clip.length - 0.5f) : 0f;

            if (wantVideo)
            {
                ClearVideoTarget();
                previewVideo.source = VideoSource.Url;
                previewVideo.url = RkslStore.GetFileUri(videoPath);
                previewVideo.isLooping = RepeatEnabled;
                bool prepared = false;
                VideoPlayer.EventHandler onPrepared = vp => prepared = true;
                previewVideo.prepareCompleted += onPrepared;
                previewVideo.Prepare();
                float wait = 0f;
                while (!prepared && wait < 15f && seq == _previewSeq)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                previewVideo.prepareCompleted -= onPrepared;
                if (seq != _previewSeq) yield break;
                if (!prepared)
                {
                    Debug.LogWarning("[MenuController] Preview video prepare timed out, audio only.", this);
                    FadeBackgroundBlend(0f);
                    wantVideo = false;
                }
            }
            else
            {
                if (previewVideo != null) previewVideo.Stop();
                FadeBackgroundBlend(0f);
            }

            if (seq != _previewSeq) yield break;
            if (clip != null && Audio != null)
            {
                Audio.PlayMusic(clip, previewFadeTime, start);
                Audio.SetMusicLoop(RepeatEnabled);
            }
            if (wantVideo)
            {
                previewVideo.Play();
                float waitFrames = 0f;
                while (previewVideo.frame <= 0 && waitFrames < 5f && seq == _previewSeq)
                {
                    waitFrames += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (seq != _previewSeq) yield break;
                if (previewVideo.frame > 0)
                {
                    float anchor = Audio != null ? Audio.GetMusicTime() : -1f;
                    if (anchor < 0f) anchor = start;
                    double vlen = previewVideo.length;
                    if (vlen > 0.5) previewVideo.time = anchor % vlen;
                    _lastVideoTime = previewVideo.time;
                    _lastResyncTime = Time.unscaledTime;
                    ApplyVideoTexture();
                    FadeBackgroundBlend(1f);
                    _previewVideoReady = clip != null;
                }
                else
                {
                    Debug.LogWarning("[MenuController] Preview video produced no frames.", this);
                    previewVideo.Stop();
                    FadeBackgroundBlend(0f);
                    _previewVideoReady = false;
                }
            }
            else
            {
                _previewVideoReady = false;
            }
            _previewRoutine = null;
        }

        private void ClearVideoTarget()
        {
            if (previewVideo == null) return;
            RenderTexture rt = previewVideo.targetTexture;
            if (rt == null || !rt.IsCreated()) return;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        private void ApplyVideoTexture()
        {
            if (_bgMat == null)
            {
                if (backgroundImage == null) return;
                _bgMat = backgroundImage.material;
            }
            if (previewVideo == null || !_bgMat.HasProperty("_BaseTex")) return;
            Texture vt = previewVideo.targetTexture != null ? (Texture)previewVideo.targetTexture : previewVideo.texture;
            if (vt != null) _bgMat.SetTexture("_BaseTex", vt);
        }

        private void FadeBackgroundBlend(float target, float? durationOverride = null)
        {
            if (_bgMat == null)
            {
                if (backgroundImage == null) return;
                _bgMat = backgroundImage.material;
            }
            if (!_bgMat.HasProperty("_BaseStrength")) return;
            _bgMat.DOKill();
            _bgMat.DOFloat(target, "_BaseStrength", durationOverride ?? blendFadeDuration).SetEase(Ease.InOutSine);
        }

        private void OnPreviewVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[MenuController] Preview video error: {message}", this);
            FadeBackgroundBlend(0f);
        }

        #endregion

        public void DeleteSelectedLevel()
        {
            if (_isTransitioning || string.IsNullOrEmpty(_selectedLevelPath)) return;
            _detailsSeq++;
            ShowDeleteConfirmation();
        }

        private void ShowDeleteConfirmation()
        {
            if (_isTransitioning) return;
            TransitionTo(_deleteConfirmationWindow);
        }

        public void HideDeleteConfirmation()
        {
            if (_deleteConfirmationWindow == null || !_deleteConfirmationWindow.activeSelf) return;

            _isTransitioning = true;
            var rt = _deleteConfirmationWindow.GetComponent<RectTransform>();

            rt.DOKill(true);
            Vector2 origPos = _originalPositions.TryGetValue(_deleteConfirmationWindow, out var o) ? o : rt.anchoredPosition;

            Sequence seq = DOTween.Sequence()
                .Join(rt.DOAnchorPos(origPos + new Vector2(0f, -GetScreenOffsetY()), _animDuration * 0.5f).SetEase(Ease.InQuart))
                .Join(rt.DORotate(new Vector3(0, 0, -1f), _animDuration * 0.5f, RotateMode.Fast))
                .Join(rt.DOScale(0.97f, _animDuration * 0.5f).SetEase(Ease.InQuart))
                .SetTarget(_deleteConfirmationWindow);

            seq.OnComplete(() => {
                _deleteConfirmationWindow.SetActive(false);
                rt.anchoredPosition = origPos;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                _isTransitioning = false;

                if (!string.IsNullOrEmpty(_selectedLevelPath))
                    TransitionTo(_levelDetailsRoot);
                else
                    TransitionTo(_levelListMenuRoot);
            });
        }

        public void ConfirmDelete()
        {
            if (string.IsNullOrEmpty(_selectedLevelPath)) return;

            if (File.Exists(_selectedLevelPath))
            {
                File.Delete(_selectedLevelPath);
                Debug.Log($"[MenuController] Уровень удален: {_selectedLevelPath}");
            }

            if (transfer != null) transfer.ClearSelectedPath(_selectedLevelPath);
            else if (Save != null && Save.CurrentData != null && Save.CurrentData.selectedLevelPath == _selectedLevelPath)
            {
                Save.CurrentData.selectedLevelPath = "";
                Save.Write();
            }

            _selectedLevelPath = null;
            SetSelectedButton(null);
            UpdateDetailsButtonsState();

            HideDeleteConfirmationImmediate();
            HideLevelDetailsImmediate();

            _foundPaths.Clear();
            _foundPaths.AddRange(Store.FindAllRkslFiles(transfer != null ? transfer.GetSavedPaths() : null));

            if (_foundPaths.Count == 0)
                TransitionTo(_noLevelsWindow);
            else
                TransitionTo(_levelListMenuRoot);
        }

        public void OpenLevelsFolder()
        {
            string folderPath = "";
            string possiblePath1 = Path.Combine(Application.streamingAssetsPath, "Levels");
            string possiblePath2 = Path.Combine(Application.persistentDataPath, "Levels");

            if (Directory.Exists(possiblePath1)) folderPath = possiblePath1;
            else if (Directory.Exists(possiblePath2)) folderPath = possiblePath2;

            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"Папка с уровнями не найдена: {folderPath}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Не удалось открыть папку: {e.Message}");
            }
        }

        #endregion

        #region Actions (Play / Edit)

        private void LoadAndPlay(string path)
        {
            StopLevelPreview();
            if (transfer != null) transfer.SetRkslPath(path, "Menu");
            else if (Save != null)
            {
                if (Save.CurrentData == null) Save.Load();
                Save.CurrentData.selectedLevelPath = path;
                Save.CurrentData.lastRkslPath = path;
                Save.Write();
            }

            if (Transition != null) Transition.LoadScene(_gameSceneName);
            else SceneManager.LoadScene(_gameSceneName);
        }

        public void EditSelectedLevel()
        {
            if (string.IsNullOrEmpty(_selectedLevelPath)) return;
            StartCoroutine(LoadAndEditRoutine(_selectedLevelPath));
        }

        public void OpenNewLevelInEditor()
        {
            StopLevelPreview();
            if (transfer != null)
            {
                transfer.SetLevel(new RhythmLevelData { fullTitle = "New Level", bpm = 128f }, "Menu");
                transfer.fromEditor = true;
                transfer.sourceScene = _editorSceneName;
            }
            if (Transition != null) Transition.LoadScene(_editorSceneName);
            else SceneManager.LoadScene(_editorSceneName);
        }

        private IEnumerator LoadAndEditRoutine(string path)
        {
            StopLevelPreview();
            string extractDir = Path.Combine(Application.temporaryCachePath, "RkslExtract_" + Path.GetFileNameWithoutExtension(path));

            if (!Store.Extract(path, extractDir, out RkslManifest man, out string audioPath, out string videoPath, out string coverPath))
            {
                Debug.LogError("[MenuController] Ошибка извлечения .rksl");
                yield break;
            }

            AudioClip clip = null;
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
                using var uwr = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(
                    RkslStore.GetFileUri(audioPath), RkslStore.GetAudioType(audioPath));
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(uwr);
            }

            Sprite cover = null;
            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(File.ReadAllBytes(coverPath)))
                    cover = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }

            var data = Store.ToRuntimeData(man, clip, null, cover);
            data.audioPath = audioPath;
            data.videoPath = videoPath;

            if (transfer != null)
            {
                transfer.SetLevel(data, "Menu");
                transfer.fromEditor = true;
            }
            if (Save != null)
            {
                if (Save.CurrentData == null) Save.Load();
                Save.CurrentData.selectedLevelPath = path;
                Save.Write();
            }

            if (Transition != null) Transition.LoadScene(_editorSceneName);
            else SceneManager.LoadScene(_editorSceneName);
        }

        #endregion
    }
}