using System.Collections;
using System.Collections.Generic;
using System.IO;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

        [Header("Level List Menu")]
        [SerializeField] private GameObject _levelListMenuRoot;
        [SerializeField] private Transform _levelListContainer;
        [SerializeField] private GameObject _levelButtonPrefab;

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

        private Sequence _transitionSequence;

        private readonly Dictionary<GameObject, Vector2> _originalPositions = new();

        [HideInInspector]
        [InjectOptional] public LevelTransfer transfer;
        [HideInInspector]
        [InjectOptional] public IRkslStore rksl;

        private IRkslStore Store => rksl ?? RkslStore.Shared;

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

            CacheOriginalPosition(_mainMenuRoot);
            CacheOriginalPosition(_levelListMenuRoot);
            CacheOriginalPosition(_levelDetailsRoot);
            CacheOriginalPosition(_noLevelsWindow);
            CacheOriginalPosition(_deleteConfirmationWindow);
            CacheOriginalPosition(_quitConfirmationWindow);
            if (_logoTransform != null) CacheOriginalPosition(_logoTransform.gameObject);
            if (_mainMenuButtonsContainer != null) CacheOriginalPosition(_mainMenuButtonsContainer.gameObject);
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
        }

        protected override void OnDisposed()
        {
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
            UpdateDetailsButtonsState();
            HideDeleteConfirmationImmediate();
            HideQuitConfirmationImmediate();
            TransitionTo(_mainMenuRoot);
        }

        public void ShowLevelList()
        {
            if (_isTransitioning) return;
            _selectedLevelPath = null;
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
            _selectedLevelPath = path;
            UpdateDetailsButtonsState();
            PopulateDetails(path);
            TransitionTo(_levelDetailsRoot);
        }

        private void HideLevelDetails()
        {
            if (_isTransitioning) return;
            if (_levelDetailsRoot == null || !_levelDetailsRoot.activeSelf) return;

            _isTransitioning = true;
            _selectedLevelPath = null;
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

            for (int i = _levelListContainer.childCount - 1; i >= 0; i--)
            {
                var child = _levelListContainer.GetChild(i).gameObject;
                DOTween.Kill(child);
                child.SetActive(false);
                Destroy(child);
            }

            _foundPaths.Clear();
            _foundPaths.AddRange(Store.FindAllRkslFiles(transfer != null ? transfer.GetSavedPaths() : null));

            if (_foundPaths.Count == 0) return;

            for (int i = 0; i < _foundPaths.Count; i++) CreateLevelListItem(_foundPaths[i], i);

            var containerRT = _levelListContainer as RectTransform;
            if (containerRT != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRT);
            }
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

            AnimateLevelButtonIn(btnGO, order, man == null);
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

            if (_detailBpmText) _detailBpmText.text = man != null ? $"{man.bpm:0} BPM" : "N/A";
            if (_detailDurationText) _detailDurationText.text = man != null && man.duration > 0f ? FormatDuration(man.duration) : "N/A";
            if (_detailNotesText) _detailNotesText.text = man != null ? $"{man.events.Count} нот" : "0 нот";
            if (_detailAuthorText) _detailAuthorText.text = (man != null && !string.IsNullOrEmpty(man.creator)) ? man.creator : "Неизвестен";
            if (_detailArtistText) _detailArtistText.text = (man != null && !string.IsNullOrEmpty(man.artist)) ? man.artist : "Неизвестен";
            if (_detailTrackText) _detailTrackText.text = (man != null && !string.IsNullOrEmpty(man.title)) ? man.title : "Без названия";

            LoadCoverImage(path);
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.RoundToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }

        private void LoadCoverImage(string rkslPath)
        {
            if (_levelCoverImage == null) return;

            _levelCoverImage.sprite = null;

            string extractDir = Path.Combine(Application.temporaryCachePath, "RkslCover_" + Path.GetFileNameWithoutExtension(rkslPath));

            if (!Store.Extract(rkslPath, extractDir, out RkslManifest man, out string audioPath, out string videoPath, out string coverPath))
                return;

            if (string.IsNullOrEmpty(coverPath) || !File.Exists(coverPath))
                return;

            try
            {
                byte[] bytes = File.ReadAllBytes(coverPath);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                    _levelCoverImage.sprite = sprite;
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

        public void DeleteSelectedLevel()
        {
            if (_isTransitioning || string.IsNullOrEmpty(_selectedLevelPath)) return;
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