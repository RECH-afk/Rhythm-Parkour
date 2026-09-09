using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Контроллер визуальных настроек уровня в редакторе.
/// Все ссылки на UI назначаются через Inspector — никакого автоспавна и GameObject.Find.
/// </summary>
public class LevelEditorVisualSettings : MonoBehaviour
{
    #region Inspector References

    [Header("Dependencies (обязательные)")]
    [SerializeField] private RkslEditorController _editorController;
    [SerializeField] private TimelineUI _timelineUI;

    [Header("Партиклы")]
    [SerializeField] private Toggle _particlesToggle;
    [SerializeField] private Image _particleColorPreview;
    [SerializeField] private Button _particleColorButton;

    [Header("Цвета")]
    [SerializeField] private Image _obstacleColorPreview;
    [SerializeField] private Button _obstacleColorButton;
    [SerializeField] private Image _trackColorPreview;
    [SerializeField] private Button _trackColorButton;

    [Header("Сфера")]
    [SerializeField] private Toggle _sphereToggle;

    [Header("Материалы")]
    [SerializeField] private Button _defaultMaterialButton;
    [SerializeField] private TextMeshProUGUI _defaultMaterialLabel;
    [SerializeField] private GameObject _materialGridPanel;
    [SerializeField] private Transform _materialGridContainer;

    [Header("Настройки")]
    [SerializeField]
    private Color[] _presetColors = new Color[]
    {
        new Color(0.2f, 0.7f, 1f),
        new Color(1f, 0.3f, 0.3f),
        new Color(0.3f, 1f, 0.4f),
        new Color(1f, 0.9f, 0.2f),
        new Color(0.8f, 0.4f, 1f),
        new Color(1f, 0.5f, 0.1f),
        Color.white,
        new Color(0.2f, 0.2f, 0.2f),
    };

    #endregion

    #region Private State

    private RhythmLevelData _levelData;
    private bool _isRefreshing;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveDependencies();
        ValidateReferences();
    }

    private void Start()
    {
        Subscribe();
        RefreshFromData();
        ApplyVisual();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    #endregion

    #region Initialization

    private void ResolveDependencies()
    {
        if (_editorController == null) _editorController = FindObjectOfType<RkslEditorController>();
        if (_timelineUI == null) _timelineUI = FindObjectOfType<TimelineUI>();

        _levelData = _timelineUI?.levelData
                     ?? _editorController?.levelData
                     ?? FindObjectOfType<RhythmParkourManager>()?.levelData;
    }

    private void ValidateReferences()
    {
        if (_levelData == null)
        {
            Debug.LogError("[VisualSettings] RhythmLevelData не найден. " +
                           "Убедитесь, что в сцене есть RkslEditorController или TimelineUI.", this);
            enabled = false;
            return;
        }

        if (_particlesToggle == null || _sphereToggle == null)
        {
            Debug.LogError("[VisualSettings] Обязательные UI-ссылки не назначены в Inspector. " +
                           "Настройте VisualSettings вручную в сцене.", this);
            enabled = false;
        }
    }

    private void Subscribe()
    {
        if (_particlesToggle != null) _particlesToggle.onValueChanged.AddListener(OnParticlesChanged);
        if (_sphereToggle != null) _sphereToggle.onValueChanged.AddListener(OnSphereChanged);

        if (_particleColorButton != null)
            _particleColorButton.onClick.AddListener(() => CycleColor(
                c => _levelData.particleColor = c,
                () => _levelData.particleColor,
                _particleColorPreview));

        if (_obstacleColorButton != null)
            _obstacleColorButton.onClick.AddListener(() => CycleColor(
                c => _levelData.obstacleColor = c,
                () => _levelData.obstacleColor,
                _obstacleColorPreview));

        if (_trackColorButton != null)
            _trackColorButton.onClick.AddListener(() => CycleColor(
                c => _levelData.trackColor = c,
                () => _levelData.trackColor,
                _trackColorPreview));

        if (_defaultMaterialButton != null)
            _defaultMaterialButton.onClick.AddListener(ToggleMaterialGrid);
    }

    private void Unsubscribe()
    {
        if (_particlesToggle != null) _particlesToggle.onValueChanged.RemoveListener(OnParticlesChanged);
        if (_sphereToggle != null) _sphereToggle.onValueChanged.RemoveListener(OnSphereChanged);

        _particleColorButton?.onClick.RemoveAllListeners();
        _obstacleColorButton?.onClick.RemoveAllListeners();
        _trackColorButton?.onClick.RemoveAllListeners();
        _defaultMaterialButton?.onClick.RemoveAllListeners();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Обновляет UI из текущего levelData. Вызывается извне при смене уровня.
    /// </summary>
    public void SetLevelData(RhythmLevelData data)
    {
        if (data == null) return;
        _levelData = data;
        RefreshFromData();
    }

    /// <summary>
    /// Перечитывает значения из levelData в UI.
    /// </summary>
    public void RefreshFromData()
    {
        if (_levelData == null) return;

        _isRefreshing = true;
        try
        {
            if (_particlesToggle != null) _particlesToggle.isOn = _levelData.particlesEnabled;
            if (_sphereToggle != null) _sphereToggle.isOn = _levelData.sphereRotates;

            if (_particleColorPreview != null) _particleColorPreview.color = _levelData.particleColor;
            if (_obstacleColorPreview != null) _obstacleColorPreview.color = _levelData.obstacleColor;
            if (_trackColorPreview != null) _trackColorPreview.color = _levelData.trackColor;

            UpdateMaterialLabel();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    #endregion

    #region Event Handlers

    private void OnParticlesChanged(bool value)
    {
        if (_isRefreshing || _levelData == null) return;
        _levelData.particlesEnabled = value;
        ApplyVisual();
    }

    private void OnSphereChanged(bool value)
    {
        if (_isRefreshing || _levelData == null) return;
        _levelData.sphereRotates = value;
        ApplyVisual();
    }

    #endregion

    #region Color Cycling

    private void CycleColor(Action<Color> setter, Func<Color> getter, Image preview)
    {
        if (_levelData == null || _presetColors.Length == 0) return;

        Color current = getter();
        int idx = FindNearestColorIndex(current);
        int next = (idx + 1) % _presetColors.Length;
        Color newColor = _presetColors[next];

        setter(newColor);
        if (preview != null) preview.color = newColor;
        ApplyVisual();
    }

    private int FindNearestColorIndex(Color target)
    {
        int bestIdx = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < _presetColors.Length; i++)
        {
            float dist = ColorDistance(_presetColors[i], target);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    private static float ColorDistance(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }

    #endregion

    #region Material Selection

    private void ToggleMaterialGrid()
    {
        if (_materialGridPanel == null) return;

        bool show = !_materialGridPanel.activeSelf;
        _materialGridPanel.SetActive(show);

        if (show) BuildMaterialOptions();
    }

    private void BuildMaterialOptions()
    {
        if (_materialGridContainer == null) return;

        ClearMaterialGrid();

        AddMaterialOption("Дефолт (префаб)", null, isDefault: true);

        var catalogMaterials = GlobalObstacleCatalog.GetAllMaterials();
        if (catalogMaterials.Count > 0)
        {
            foreach (var mat in catalogMaterials)
            {
                if (mat != null) AddMaterialOption(mat.name, mat, isDefault: false);
            }
        }
        else
        {
            AddDiscoveredMaterialsFallback();
        }
    }

    private void AddDiscoveredMaterialsFallback()
    {
        var discovered = Resources.FindObjectsOfTypeAll<Material>();
        var seen = new HashSet<string>();

        foreach (var mat in discovered)
        {
            if (mat == null) continue;
            if (seen.Contains(mat.name)) continue;

            string lower = mat.name.ToLowerInvariant();
            if (!lower.Contains("obstacle") && !lower.Contains("level")) continue;

            seen.Add(mat.name);
            AddMaterialOption(mat.name, mat, isDefault: false);
        }
    }

    private void ClearMaterialGrid()
    {
        for (int i = _materialGridContainer.childCount - 1; i >= 0; i--)
            Destroy(_materialGridContainer.GetChild(i).gameObject);
    }

    private void AddMaterialOption(string name, Material mat, bool isDefault)
    {
        var btnGo = new GameObject("MatBtn_" + name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(_materialGridContainer, false);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 24);

        var img = btnGo.GetComponent<Image>();
        img.color = GetMaterialPreviewColor(mat, isDefault);

        var btn = btnGo.GetComponent<Button>();
        string capturedName = name;
        Material capturedMat = mat;
        bool capturedDefault = isDefault;
        btn.onClick.AddListener(() => OnMaterialPicked(capturedName, capturedMat, capturedDefault));

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(btnGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = name;
        tmp.fontSize = 10;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    private static Color GetMaterialPreviewColor(Material mat, bool isDefault)
    {
        if (mat != null)
        {
            if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color")) return mat.color;
        }
        return isDefault ? new Color(0.3f, 0.3f, 0.3f) : Color.gray;
    }

    private void OnMaterialPicked(string name, Material mat, bool isDefault)
    {
        if (_levelData == null) return;

        if (isDefault)
        {
            _levelData.defaultObstacleMaterialName = string.Empty;
            _levelData.defaultObstacleMaterial = null;
        }
        else
        {
            _levelData.defaultObstacleMaterialName = mat != null ? mat.name : name;
            _levelData.defaultObstacleMaterial = mat;
        }

        UpdateMaterialLabel();

        if (_materialGridPanel != null) _materialGridPanel.SetActive(false);
        ApplyVisual();

        Debug.Log($"[VisualSettings] Материал: {(isDefault ? "Дефолт" : name)}", this);
    }

    private void UpdateMaterialLabel()
    {
        if (_defaultMaterialLabel == null) return;

        string name = string.IsNullOrEmpty(_levelData.defaultObstacleMaterialName)
            ? "Дефолт (префаб)"
            : _levelData.defaultObstacleMaterialName;

        _defaultMaterialLabel.text = name;
    }

    #endregion

    #region Visual Application

    private void ApplyVisual()
    {
        if (_levelData == null) return;

        if (!string.IsNullOrEmpty(_levelData.defaultObstacleMaterialName))
        {
            var mat = GlobalObstacleCatalog.GetMaterial(_levelData.defaultObstacleMaterialName);
            if (mat != null) _levelData.defaultObstacleMaterial = mat;
        }

        LevelVisualApplier.Apply(_levelData, null, true);

        var preview = FindObjectOfType<TimelinePreview>();
        preview?.ForceRefresh();
    }

    #endregion
}