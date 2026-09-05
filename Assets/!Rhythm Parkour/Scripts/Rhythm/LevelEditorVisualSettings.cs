using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI для визуальных настроек уровня в редакторе.
/// - Toggle партиклов + цвет
/// - Цвет дорожки / препятствий
/// - Toggle сферы
/// - Выбор дефолтного материала для препятствий
/// Всё привязано к RhythmLevelData и сохраняется в .rksl через RkslManifest.
/// Создаёт fallback UI если не настроено в сцене.
/// </summary>
public class LevelEditorVisualSettings : MonoBehaviour
{
    [Header("Ссылки (авто)")]
    public RkslEditorController editorController;
    public TimelineUI timelineUI;
    public RhythmLevelData levelData; // если пусто — берём из timeline/editor

    [Header("UI — Партиклы")]
    public Toggle particlesToggle;
    public Image particleColorPreview;
    public Button particleColorButton;
    public TextMeshProUGUI particleColorLabel;

    [Header("UI — Цвета")]
    public Image obstacleColorPreview;
    public Button obstacleColorButton;
    public Image trackColorPreview;
    public Button trackColorButton;

    [Header("UI — Сфера")]
    public Toggle sphereToggle;

    [Header("UI — Материалы")]
    public Button defaultMaterialButton;
    public TextMeshProUGUI defaultMaterialLabel;
    public GameObject materialGridPanel;
    public Transform materialGridContainer;

    [Header("Настройки")]
    public bool autoCreateFallbackUI = true;
    public Color[] presetColors = new Color[]
    {
        new Color(0.2f,0.7f,1f,1f),
        new Color(1f,0.3f,0.3f,1f),
        new Color(0.3f,1f,0.4f,1f),
        new Color(1f,0.9f,0.2f,1f),
        new Color(0.8f,0.4f,1f,1f),
        new Color(1f,0.5f,0.1f,1f),
        Color.white,
        new Color(0.2f,0.2f,0.2f,1f),
    };

    Canvas rootCanvas;
    bool isRefreshing;

    void Awake()
    {
        if (editorController == null) editorController = FindObjectOfType<RkslEditorController>();
        if (timelineUI == null) timelineUI = FindObjectOfType<TimelineUI>();
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();

        if (levelData == null && timelineUI != null) levelData = timelineUI.levelData;
        if (levelData == null && editorController != null) levelData = editorController.levelData;
        if (levelData == null) levelData = FindObjectOfType<RhythmParkourManager>()?.levelData;

        TryFindUI();
        if (autoCreateFallbackUI && (particlesToggle == null || sphereToggle == null))
            CreateFallbackUI();
        BindEvents();
    }

    void Start()
    {
        RefreshFromData();
        ApplyVisual();
    }

    void Update()
    {
        // Синхронизируем levelData если он сменился в редакторе (загрузка .rksl)
        RhythmLevelData cur = null;
        if (timelineUI != null) cur = timelineUI.levelData;
        else if (editorController != null) cur = editorController.levelData;
        if (cur != null && cur != levelData)
        {
            levelData = cur;
            RefreshFromData();
        }
    }

    void TryFindUI()
    {
        if (particlesToggle == null)
        {
            var go = GameObject.Find("ParticlesToggle");
            if (go) particlesToggle = go.GetComponent<Toggle>();
        }
        if (sphereToggle == null)
        {
            var go = GameObject.Find("SphereToggle");
            if (go) sphereToggle = go.GetComponent<Toggle>();
        }
        if (particleColorPreview == null)
        {
            var go = GameObject.Find("ParticleColorPreview");
            if (go) particleColorPreview = go.GetComponent<Image>();
        }
        if (particleColorButton == null)
        {
            var go = GameObject.Find("ParticleColorButton");
            if (go) particleColorButton = go.GetComponent<Button>();
        }
        if (obstacleColorPreview == null)
        {
            var go = GameObject.Find("ObstacleColorPreview");
            if (go) obstacleColorPreview = go.GetComponent<Image>();
        }
        if (obstacleColorButton == null)
        {
            var go = GameObject.Find("ObstacleColorButton");
            if (go) obstacleColorButton = go.GetComponent<Button>();
        }
        if (trackColorPreview == null)
        {
            var go = GameObject.Find("TrackColorPreview");
            if (go) trackColorPreview = go.GetComponent<Image>();
        }
        if (trackColorButton == null)
        {
            var go = GameObject.Find("TrackColorButton");
            if (go) trackColorButton = go.GetComponent<Button>();
        }
        if (defaultMaterialButton == null)
        {
            var go = GameObject.Find("DefaultMaterialButton");
            if (go) defaultMaterialButton = go.GetComponent<Button>();
        }
        if (defaultMaterialLabel == null && defaultMaterialButton != null)
            defaultMaterialLabel = defaultMaterialButton.GetComponentInChildren<TextMeshProUGUI>();
        if (materialGridPanel == null)
        {
            var go = GameObject.Find("MaterialGridPanel");
            if (go) materialGridPanel = go;
        }
        if (materialGridContainer == null && materialGridPanel != null)
        {
            var t = materialGridPanel.transform.Find("Grid");
            if (t) materialGridContainer = t;
            else materialGridContainer = materialGridPanel.transform;
        }
    }

    void CreateFallbackUI()
    {
        // Требует ручного Canvas — авто-создание Canvas удалено по запросу
        Transform parent = null;
        var settingsGO = GameObject.Find("SettingsWindow");
        if (settingsGO) parent = settingsGO.transform;
        else if (timelineUI != null && timelineUI.transform.parent != null) parent = timelineUI.transform.parent;
        else if (rootCanvas != null) parent = rootCanvas.transform;
        else
        {
            Debug.LogWarning("[VisualSettings] Canvas/SettingsWindow не найден — создай UI вручную, авто-создание отключено", this);
            return;
        }
        if (parent.GetComponentInParent<Canvas>() == null)
        {
            Debug.LogWarning("[VisualSettings] Parent не под Canvas — пропустил авто-создание панели", this);
            return;
        }

        // Создаём контейнер VisualSettings
        var panelGO = new GameObject("VisualSettingsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGO.transform.SetParent(parent, false);
        var rt = panelGO.GetComponent<RectTransform>();
        // Позиционируем внизу SettingsWindow или сбоку
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(12, -420); rt.sizeDelta = new Vector2(360, 320);
        var img = panelGO.GetComponent<Image>(); img.color = new Color(0.14f,0.14f,0.16f,0.95f);
        var vlg = panelGO.GetComponent<VerticalLayoutGroup>(); vlg.padding = new RectOffset(10,10,10,10); vlg.spacing = 6; vlg.childControlWidth = true; vlg.childControlHeight = false;
        var fitter = panelGO.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Заголовок
        CreateLabel(panelGO.transform, "ВИЗУАЛ УРОВНЯ", 14, FontStyles.Bold);

        // Партиклы toggle
        particlesToggle = CreateToggle(panelGO.transform, "Партиклы", true);
        // Цвет партиклов
        var row1 = CreateRow(panelGO.transform);
        CreateLabel(row1, "Цвет партиклов", 12);
        particleColorPreview = CreateColorPreview(row1, presetColors[0]);
        particleColorButton = CreateButton(row1, "Сменить", () => CycleColor(ref levelData.particleColor, particleColorPreview, true));

        // Цвет препятствий
        var row2 = CreateRow(panelGO.transform);
        CreateLabel(row2, "Цвет препятствий", 12);
        obstacleColorPreview = CreateColorPreview(row2, Color.white);
        obstacleColorButton = CreateButton(row2, "Сменить", () => CycleColor(ref levelData.obstacleColor, obstacleColorPreview, true));

        // Цвет дорожки
        var row3 = CreateRow(panelGO.transform);
        CreateLabel(row3, "Цвет дорожки", 12);
        trackColorPreview = CreateColorPreview(row3, presetColors[0]);
        trackColorButton = CreateButton(row3, "Сменить", () => CycleColor(ref levelData.trackColor, trackColorPreview, true));

        // Сфера toggle
        sphereToggle = CreateToggle(panelGO.transform, "Сфера крутится", true);

        // Материал
        var matRow = CreateRow(panelGO.transform);
        CreateLabel(matRow, "Материал препятствий", 12);
        defaultMaterialButton = CreateButton(matRow, "Выбрать...", ToggleMaterialGrid);
        if (defaultMaterialButton != null)
        {
            var lbl = defaultMaterialButton.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl) defaultMaterialLabel = lbl;
        }

        // Панель выбора материалов (скрыта)
        materialGridPanel = new GameObject("MaterialGridPanel", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup));
        materialGridPanel.transform.SetParent(panelGO.transform, false);
        var mrt = materialGridPanel.GetComponent<RectTransform>(); mrt.sizeDelta = new Vector2(0, 0);
        var mimg = materialGridPanel.GetComponent<Image>(); mimg.color = new Color(0.1f,0.1f,0.12f,0.9f);
        var glg = materialGridPanel.GetComponent<GridLayoutGroup>(); glg.cellSize = new Vector2(80, 30); glg.spacing = new Vector2(6,6); glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 3;
        materialGridPanel.SetActive(false);
        materialGridContainer = materialGridPanel.transform;

        Debug.Log("[VisualSettings] Fallback UI создан", panelGO);
    }

    // ——— Helpers для создания UI ———
    TextMeshProUGUI CreateLabel(Transform parent, string txt, int size, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = txt; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = Color.white;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = size + 6;
        return tmp;
    }
    Toggle CreateToggle(Transform parent, string label, bool def)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Toggle), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 24);
        var bg = go.GetComponent<Image>(); bg.color = new Color(0.2f,0.2f,0.2f,1f);
        var toggle = go.GetComponent<Toggle>(); toggle.isOn = def;
        // Checkmark
        var ckGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        ckGO.transform.SetParent(go.transform, false);
        var ckRT = ckGO.GetComponent<RectTransform>(); ckRT.anchorMin = new Vector2(0,0); ckRT.anchorMax = new Vector2(0,1); ckRT.offsetMin = new Vector2(2,2); ckRT.offsetMax = new Vector2(-2,2); ckRT.sizeDelta = new Vector2(20,0);
        ckGO.GetComponent<Image>().color = new Color(0.2f,0.8f,0.3f,1f);
        toggle.graphic = ckGO.GetComponent<Image>();
        toggle.targetGraphic = bg;
        // Label
        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = lblGO.GetComponent<RectTransform>(); lblRT.anchorMin = new Vector2(0,0); lblRT.anchorMax = new Vector2(1,1); lblRT.offsetMin = new Vector2(26,0); lblRT.offsetMax = Vector2.zero;
        var lbl = lblGO.AddComponent<TextMeshProUGUI>(); lbl.text = label; lbl.fontSize = 12; lbl.color = Color.white; lbl.alignment = TextAlignmentOptions.Left;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = 24;
        return toggle;
    }
    Transform CreateRow(Transform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var hlg = go.GetComponent<HorizontalLayoutGroup>(); hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleLeft; hlg.childControlWidth = false; hlg.childControlHeight = false;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = 28;
        return go.transform;
    }
    Image CreateColorPreview(Transform parent, Color c)
    {
        var go = new GameObject("ColorPreview", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(40, 20);
        var img = go.GetComponent<Image>(); img.color = c;
        var le = go.AddComponent<LayoutElement>(); le.minWidth = 40; le.minHeight = 20;
        return img;
    }
    Button CreateButton(Transform parent, string txt, UnityEngine.Events.UnityAction act)
    {
        var go = new GameObject("Btn_"+txt, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(80, 24);
        var img = go.GetComponent<Image>(); img.color = new Color(0.25f,0.45f,0.85f,1f);
        var btn = go.GetComponent<Button>();
        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var trt = tgo.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = tgo.AddComponent<TextMeshProUGUI>(); tmp.text = txt; tmp.fontSize = 11; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        var le = go.AddComponent<LayoutElement>(); le.minWidth = 70; le.minHeight = 24;
        if (act != null) btn.onClick.AddListener(act);
        return btn;
    }

    void BindEvents()
    {
        if (particlesToggle != null)
        {
            particlesToggle.onValueChanged.RemoveListener(OnParticlesToggle);
            particlesToggle.onValueChanged.AddListener(OnParticlesToggle);
        }
        if (sphereToggle != null)
        {
            sphereToggle.onValueChanged.RemoveListener(OnSphereToggle);
            sphereToggle.onValueChanged.AddListener(OnSphereToggle);
        }
        if (particleColorButton != null)
        {
            particleColorButton.onClick.RemoveListener(() => CycleColor(ref levelData.particleColor, particleColorPreview, true));
            // already bound in creation
        }
        if (defaultMaterialButton != null)
        {
            defaultMaterialButton.onClick.RemoveListener(ToggleMaterialGrid);
            defaultMaterialButton.onClick.AddListener(ToggleMaterialGrid);
        }
    }

    void OnParticlesToggle(bool v)
    {
        if (isRefreshing || levelData == null) return;
        levelData.particlesEnabled = v;
        ApplyVisual();
    }
    void OnSphereToggle(bool v)
    {
        if (isRefreshing || levelData == null) return;
        levelData.sphereRotates = v;
        ApplyVisual();
    }
    void CycleColor(ref Color c, Image preview, bool apply)
    {
        if (levelData == null) return;
        // Находим ближайший пресет и берём следующий
        int idx = 0;
        float best = float.MaxValue;
        for (int i=0;i<presetColors.Length;i++)
        {
            float d = Mathf.Abs(presetColors[i].r - c.r) + Mathf.Abs(presetColors[i].g - c.g) + Mathf.Abs(presetColors[i].b - c.b);
            if (d < best) { best = d; idx = i; }
        }
        int next = (idx + 1) % presetColors.Length;
        c = presetColors[next];
        if (preview) preview.color = c;
        if (apply) ApplyVisual();
        // Обновляем оба поля если это obstacle/track
        if (preview == particleColorPreview) levelData.particleColor = c;
        else if (preview == obstacleColorPreview) levelData.obstacleColor = c;
        else if (preview == trackColorPreview) levelData.trackColor = c;
    }

    public void RefreshFromData()
    {
        if (levelData == null) return;
        isRefreshing = true;
        if (particlesToggle) particlesToggle.isOn = levelData.particlesEnabled;
        if (sphereToggle) sphereToggle.isOn = levelData.sphereRotates;
        if (particleColorPreview) particleColorPreview.color = levelData.particleColor;
        if (obstacleColorPreview) obstacleColorPreview.color = levelData.obstacleColor;
        if (trackColorPreview) trackColorPreview.color = levelData.trackColor;
        if (defaultMaterialLabel)
        {
            string name = string.IsNullOrEmpty(levelData.defaultObstacleMaterialName) ? "Дефолт (префаб)" : levelData.defaultObstacleMaterialName;
            defaultMaterialLabel.text = name;
        }
        isRefreshing = false;
    }

    void ApplyVisual()
    {
        if (levelData == null) return;
        // Синхронизируем материал референс
        if (!string.IsNullOrEmpty(levelData.defaultObstacleMaterialName))
        {
            var mat = GlobalObstacleCatalog.GetMaterial(levelData.defaultObstacleMaterialName);
            if (mat) levelData.defaultObstacleMaterial = mat;
        }
        LevelVisualApplier.Apply(levelData, null, true);
        // Обновляем timeline preview
        var preview = FindObjectOfType<TimelinePreview>();
        if (preview) preview.ForceRefresh();
    }

    public void ToggleMaterialGrid()
    {
        if (materialGridPanel == null) return;
        bool show = !materialGridPanel.activeSelf;
        materialGridPanel.SetActive(show);
        if (show) RefreshMaterialGrid();
    }
    void RefreshMaterialGrid()
    {
        if (materialGridContainer == null) return;
        for (int i=materialGridContainer.childCount-1;i>=0;i--) Destroy(materialGridContainer.GetChild(i).gameObject);
        var mats = GlobalObstacleCatalog.GetAllMaterials();
        // Добавляем кнопку "Дефолт"
        CreateMatButton("Дефолт (префаб)", null, true);
        foreach (var m in mats)
        {
            if (m == null) continue;
            CreateMatButton(m.name, m, false);
        }
        // Также пробуем найти материалы в проекте если каталог пуст
        if (mats.Count==0)
        {
            // fallback: ищем в Resources
            var found = Resources.FindObjectsOfTypeAll<Material>();
            HashSet<string> seen = new HashSet<string>();
            foreach (var mat in found)
            {
                if (mat == null || seen.Contains(mat.name)) continue;
                // фильтр по имени Obstacle
                if (!mat.name.ToLower().Contains("obstacle") && !mat.name.ToLower().Contains("level")) continue;
                seen.Add(mat.name);
                CreateMatButton(mat.name, mat, false);
            }
        }
    }
    void CreateMatButton(string name, Material mat, bool isDefault)
    {
        var btn = CreateButton(materialGridContainer, name, null);
        btn.onClick.RemoveAllListeners();
        string n = name;
        Material m = mat;
        btn.onClick.AddListener(() => OnMaterialPicked(n, m, isDefault));
        // Превью цвета материала
        var img = btn.GetComponent<Image>();
        if (m != null && m.HasProperty("_Color")) img.color = m.color;
        else if (m != null && m.HasProperty("_BaseColor")) img.color = m.GetColor("_BaseColor");
        else if (isDefault) img.color = new Color(0.3f,0.3f,0.3f,1f);
    }
    void OnMaterialPicked(string name, Material mat, bool isDefault)
    {
        if (levelData == null) return;
        if (isDefault)
        {
            levelData.defaultObstacleMaterialName = "";
            levelData.defaultObstacleMaterial = null;
        }
        else
        {
            levelData.defaultObstacleMaterialName = mat ? mat.name : name;
            levelData.defaultObstacleMaterial = mat;
        }
        if (defaultMaterialLabel) defaultMaterialLabel.text = isDefault ? "Дефолт (префаб)" : name;
        materialGridPanel.SetActive(false);
        ApplyVisual();
        Debug.Log($"[VisualSettings] Материал выбран: {(isDefault ? "Дефолт" : name)}");
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void EditorAutoEnsure()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (FindObjectOfType<LevelEditorVisualSettings>() != null) return;
            // Не спамим в IsGameScene
            string cur = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (cur == "IsLevelEditorScene" || cur == "LevelEditor")
            {
                var go = new GameObject("LevelVisualSettings (AutoEditor)");
                go.AddComponent<LevelEditorVisualSettings>();
            }
        };
    }
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RuntimeAutoCreate()
    {
        string cur = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (cur != "IsLevelEditorScene" && cur != "LevelEditor" && cur != "IsMenuScene") return;
        if (FindObjectOfType<LevelEditorVisualSettings>() != null) return;
        var go = new GameObject("LevelVisualSettings (Auto)");
        go.AddComponent<LevelEditorVisualSettings>();
        Debug.Log("[VisualSettings] AutoCreated for " + cur, go);
    }
}
