using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class LevelResultsUI : MonoBehaviour
{
    public static LevelResultsUI Instance { get; private set; }

    [SerializeField] private string _gameSceneName = "IsGameScene";
    [SerializeField] private int _minComboToShow = 2;

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _accText;
    [SerializeField] private TextMeshProUGUI _comboText;
    [SerializeField] private TextMeshProUGUI _judgeText;

    [Header("Results")]
    [SerializeField] private GameObject _resultsRoot;
    [SerializeField] private TextMeshProUGUI _resTitle;
    [SerializeField] private TextMeshProUGUI _resRank;
    [SerializeField] private TextMeshProUGUI _resAcc;
    [SerializeField] private TextMeshProUGUI _resScore;
    [SerializeField] private TextMeshProUGUI _resCounts;
    [SerializeField] private TextMeshProUGUI _resCombo;
    [SerializeField] private TextMeshProUGUI _resGroove;
    [SerializeField] private TextMeshProUGUI _resFailed;

    [Header("Buttons")]
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _menuButton;
    [SerializeField] private Button _editorButton;

    private RhythmScoreManager _bound;
    private bool _resultsVisible;
    private MenuController _menuCache;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _retryButton?.onClick.AddListener(Retry);
        _menuButton?.onClick.AddListener(OpenMenu);
        _editorButton?.onClick.AddListener(OpenEditor);

        HideResultsImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unbind();
        DOTween.Kill(this);
    }

    private void OnEnable() => BindIfNeeded();
    private void OnDisable() => Unbind();

    private void BindIfNeeded()
    {
        if (_bound != null) return;
        if (RhythmScoreManager.Instance != null)
        {
            _bound = RhythmScoreManager.Instance;
            _bound.onJudgement += OnJudgement;
            _bound.onScoreChanged += OnScoreChanged;
            _bound.onLevelFinished += OnLevelFinished;
            RefreshHUD();
        }
    }

    private void Unbind()
    {
        if (_bound == null) return;
        _bound.onJudgement -= OnJudgement;
        _bound.onScoreChanged -= OnScoreChanged;
        _bound.onLevelFinished -= OnLevelFinished;
        _bound = null;
    }

    private void Update()
    {
        BindIfNeeded();

        if (_resultsVisible)
        {
            if (_menuCache == null) _menuCache = FindObjectOfType<MenuController>();

            // Используем публичное свойство IsMenuActive вместо прямого доступа к полю
            if (_menuCache != null && _menuCache.IsMenuActive)
            {
                HideResultsImmediate();
            }

            if (Input.GetKeyDown(KeyCode.R)) Retry();
        }
    }

    private void OnJudgement(HitJudgement j, int comboAfter)
    {
        RefreshHUD();
        ShowJudgePopup(j);
        if (comboAfter >= _minComboToShow) PulseCombo();
    }

    private void OnScoreChanged()
    {
        if (_bound != null && _bound.isLevelActive && !_bound.isFinished && _resultsVisible)
            HideResultsImmediate();
        RefreshHUD();
    }

    private void OnLevelFinished(LevelResults r) => ShowResults(r);

    private void RefreshHUD()
    {
        if (_bound == null) return;

        if (_scoreText) _scoreText.text = $"{_bound.flow:0}";

        if (_accText)
        {
            string rank = _bound.Rank;
            _accText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(RhythmScoreManager.RankColor(rank))}><b>{rank}</b></color>  {_bound.Accuracy * 100f:0.00}%";
        }

        if (_comboText)
        {
            bool show = _bound.combo >= _minComboToShow && !_resultsVisible;
            _comboText.gameObject.SetActive(show);
            if (show) _comboText.text = $"{_bound.combo}x";
        }
    }

    private void ShowJudgePopup(HitJudgement j)
    {
        if (_judgeText == null) return;

        DOTween.Kill(_judgeText);
        _judgeText.text = RhythmScoreManager.JudgementText(j);
        _judgeText.color = RhythmScoreManager.JudgementColor(j);
        _judgeText.transform.localScale = Vector3.one * 1.35f;
        _judgeText.alpha = 1f;

        _judgeText.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutCubic);
        _judgeText.DOFade(0f, 0.25f).SetDelay(0.35f).SetEase(Ease.Linear).SetTarget(_judgeText);
    }

    private void PulseCombo()
    {
        if (_comboText == null) return;
        RefreshHUD();

        _comboText.transform.DOKill();
        _comboText.transform.localScale = Vector3.one * 1.18f;
        _comboText.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutCubic).SetTarget(_comboText);
    }

    private void ShowResults(LevelResults r)
    {
        _resultsVisible = true;
        _resultsRoot?.SetActive(true);
        _resultsRoot?.transform.SetAsLastSibling();
        _comboText?.gameObject.SetActive(false);
        _judgeText?.DOFade(0f, 0.1f);

        if (_resTitle) _resTitle.text = string.IsNullOrEmpty(r.levelTitle) ? "Уровень пройден" : r.levelTitle;

        if (_resRank)
        {
            _resRank.text = r.rank;
            _resRank.color = RhythmScoreManager.RankColor(r.rank);
            _resRank.transform.DOKill();
            _resRank.transform.localScale = Vector3.one * 2.4f;
            _resRank.transform.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack).SetTarget(_resRank);
        }

        if (_resAcc) _resAcc.text = $"{r.accuracy * 100f:0.00}%";
        if (_resFailed) _resFailed.gameObject.SetActive(r.failed);

        if (_resCounts)
        {
            _resCounts.text =
                $"<color=#5FD4FF><b>300</b></color> x{r.count300}    " +
                $"<color=#5FFF7A><b>100</b></color> x{r.count100}    " +
                $"<color=#FFD94D><b>50</b></color> x{r.count50}    " +
                $"<color=#FF5A5A><b>Miss</b></color> x{r.countMiss}";
        }

        if (_resCombo) _resCombo.text = $"Max серия: {r.maxCombo}";
        if (_resGroove) _resGroove.text = $"В ритме: <color=#5FD4FF>{r.groovePerfect} perfect</color>  <color=#5FFF7A>{r.grooveGood} good</color>";

        if (_editorButton) _editorButton.gameObject.SetActive(LevelTransfer.fromEditor);

        if (_resScore)
        {
            DOTween.To(() => 0f, x => _resScore.text = $"{x:0}", r.flow, 0.9f).SetEase(Ease.OutCubic).SetTarget(_resScore);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HideResultsImmediate()
    {
        _resultsVisible = false;
        _resultsRoot?.SetActive(false);
        DOTween.Kill(this);
        RefreshHUD();
    }

    public void Retry()
    {
        HideResultsImmediate();
        var ph = FindObjectOfType<PlayerHealth>();

        if (ph != null && ph.IsDead) ph.Respawn();
        else RhythmParkourManager.Instance?.Play();

        if (SceneManager.GetActiveScene().name == _gameSceneName)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void OpenMenu()
    {
        HideResultsImmediate();
        Time.timeScale = 1f;
        _menuCache ??= FindObjectOfType<MenuController>();
        _menuCache?.ShowMenu();
    }

    public void OpenEditor()
    {
        HideResultsImmediate();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string target = string.IsNullOrEmpty(LevelTransfer.sourceScene) ? "IsLevelEditorScene" : LevelTransfer.sourceScene;
        SceneManager.LoadScene(target);
    }
}