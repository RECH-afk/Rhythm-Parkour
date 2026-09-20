using System;
using System.Collections.Generic;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    public class LevelEditorVisualSettings : RKSBehaviour
    {
    #region Inspector References

        [Header("Dependencies")]
        [HideInInspector]
        [InjectOptional] public RkslEditorController editorController;
        [HideInInspector]
        [InjectOptional] public TimelineUI timelineUI;
        [HideInInspector]
        [InjectOptional] public RhythmParkourManager previewManager;
        [HideInInspector]
        [InjectOptional] public GlobalObstacleCatalog catalog;
        [HideInInspector]
        [InjectOptional] public LevelVisualApplier visual;
        [HideInInspector]
        [InjectOptional] public TimelinePreview preview;

        [Header("Particles")]
        [SerializeField] private Toggle _particlesToggle;
        [SerializeField] private Button _particlesColorButton;
        [SerializeField] private Button _particlesSpriteButton;
        [SerializeField] private TextMeshProUGUI _particleSpriteLabel;
        [SerializeField] private Image _particleColorPreview;

        [Header("Colors")]
        [SerializeField] private Button _obstacleColorButton;
        [SerializeField] private Image _obstacleColorPreview;
        [SerializeField] private Button _trackColorButton;
        [SerializeField] private Image _trackColorPreview;

        [Header("Sphere")]
        [SerializeField] private Toggle _sphereToggle;
        [SerializeField] private Toggle _sphereUseVideoToggle;

        [Header("Color Picker")]
        [Tooltip("Общий FlexibleColorPicker из сцены (неактивный). Если пусто — найдётся автоматически")]
        [SerializeField] private FlexibleColorPicker _colorPicker;

        [Header("Materials")]
        [SerializeField] private TextMeshProUGUI _defaultMaterialLabel;
        [SerializeField] private GameObject _materialGridPanel;
        [SerializeField] private Transform _materialGridContainer;

        [Header("Particle Sprites")]
        [SerializeField] private GameObject _spriteGridPanel;
        [SerializeField] private Transform _spriteGridContainer;

        [Header("Settings")]
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
        private enum PickerTarget { None, Particle, Obstacle, Track }
        private PickerTarget _pickerTarget = PickerTarget.None;

    #endregion

    #region Unity Lifecycle

        protected override void OnInjected()
        {
            ResolveDependencies();
            AutoFindUI();
            ValidateReferences();
        }

        protected override void OnReady()
        {

            ResolveDependencies();
            AutoFindUI();
            Subscribe();
            RefreshFromData();
            ApplyVisual();
        }

        protected override void OnDisposed()
        {
            Unsubscribe();
        }

    #endregion

    #region Initialization

        private void ResolveDependencies()
        {
            if (timelineUI == null) timelineUI = FindFirstObjectByType<TimelineUI>();
            if (editorController == null) editorController = FindFirstObjectByType<RkslEditorController>();
            if (previewManager == null) previewManager = FindFirstObjectByType<RhythmParkourManager>();
            if (preview == null) preview = FindFirstObjectByType<TimelinePreview>();

            _levelData = timelineUI?.levelData
                         ?? editorController?.levelData
                         ?? previewManager?.levelData;
        }

        private static T FindByName<T>(string objectName) where T : Component
        {
            var go = GameObject.Find(objectName);
            if (go == null) return null;
            return go.GetComponent<T>();
        }

        private void AutoFindUI()
        {

            if (_particlesToggle == null)
            {
                _particlesToggle = FindByName<Toggle>("ToggleParticlesEnabled");
                if (_particlesToggle == null)
                {
                    var all = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var t in all)
                        if (t.name.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0) { _particlesToggle = t; break; }
                }
            }
            if (_sphereToggle == null)
            {
                _sphereToggle = FindByName<Toggle>("ToggleSphereRotating");
                if (_sphereToggle == null)
                {
                    var all = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var t in all)
                        if (t.name.IndexOf("SphereRotat", StringComparison.OrdinalIgnoreCase) >= 0) { _sphereToggle = t; break; }
                }
            }
            if (_sphereUseVideoToggle == null)
                _sphereUseVideoToggle = FindByName<Toggle>("ToggleSphereUseVideo");
            if (_particlesColorButton == null)
                _particlesColorButton = FindByName<Button>("ButtonParticlesColor");
            if (_particlesSpriteButton == null)
                _particlesSpriteButton = FindByName<Button>("ButtonParticlesSprite");
            if (_trackColorButton == null)
                _trackColorButton = FindByName<Button>("ButtonTrackColor");
            if (_obstacleColorButton == null)
                _obstacleColorButton = FindByName<Button>("ButtonObstaclesColor");
            if (_colorPicker == null)
            {
                var pickers = FindObjectsByType<FlexibleColorPicker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (pickers != null && pickers.Length > 0) _colorPicker = pickers[0];
            }
            if (_particleColorPreview == null)
            {
                var go = GameObject.Find("ParticleColorPreview");
                if (go != null) _particleColorPreview = go.GetComponent<Image>();
            }
            if (_trackColorPreview == null)
            {
                var go = GameObject.Find("TrackColorPreview");
                if (go != null) _trackColorPreview = go.GetComponent<Image>();
            }
            if (_obstacleColorPreview == null)
            {
                var go = GameObject.Find("ObstacleColorPreview");
                if (go != null) _obstacleColorPreview = go.GetComponent<Image>();
            }
        }

        private void ValidateReferences()
        {

            if (_levelData == null)
                Debug.LogWarning("[VisualSettings] RhythmLevelData пока нет — подхватим позже из TimelineUI/Transfer.", this);
            if (_particlesToggle == null)
                Debug.LogWarning("[VisualSettings] ToggleParticlesEnabled не найден — тоггл частиц недоступен.", this);
            if (_sphereToggle == null)
                Debug.LogWarning("[VisualSettings] ToggleSphereRotating не найден — тоггл сферы недоступен.", this);
        }

        private void Subscribe()
        {
            if (_particlesToggle != null) { _particlesToggle.onValueChanged.RemoveListener(OnParticlesChanged); _particlesToggle.onValueChanged.AddListener(OnParticlesChanged); }
            if (_sphereToggle != null) { _sphereToggle.onValueChanged.RemoveListener(OnSphereChanged); _sphereToggle.onValueChanged.AddListener(OnSphereChanged); }
            if (_sphereUseVideoToggle != null) { _sphereUseVideoToggle.onValueChanged.RemoveListener(OnSphereVideoChanged); _sphereUseVideoToggle.onValueChanged.AddListener(OnSphereVideoChanged); }
            if (_particlesColorButton != null) { _particlesColorButton.onClick.RemoveListener(OpenParticleColorPicker); _particlesColorButton.onClick.AddListener(OpenParticleColorPicker); }
            if (_trackColorButton != null) { _trackColorButton.onClick.RemoveListener(OpenTrackColorPicker); _trackColorButton.onClick.AddListener(OpenTrackColorPicker); }
            if (_obstacleColorButton != null) { _obstacleColorButton.onClick.RemoveListener(OpenObstacleColorPicker); _obstacleColorButton.onClick.AddListener(OpenObstacleColorPicker); }
            if (_particlesSpriteButton != null) { _particlesSpriteButton.onClick.RemoveListener(ToggleSpriteGrid); _particlesSpriteButton.onClick.AddListener(ToggleSpriteGrid); }
        }

        private void Unsubscribe()
        {
            if (_particlesToggle != null) _particlesToggle.onValueChanged.RemoveListener(OnParticlesChanged);
            if (_sphereToggle != null) _sphereToggle.onValueChanged.RemoveListener(OnSphereChanged);
            if (_sphereUseVideoToggle != null) _sphereUseVideoToggle.onValueChanged.RemoveListener(OnSphereVideoChanged);
            if (_particlesColorButton != null) _particlesColorButton.onClick.RemoveListener(OpenParticleColorPicker);
            if (_trackColorButton != null) _trackColorButton.onClick.RemoveListener(OpenTrackColorPicker);
            if (_obstacleColorButton != null) _obstacleColorButton.onClick.RemoveListener(OpenObstacleColorPicker);
            if (_particlesSpriteButton != null) _particlesSpriteButton.onClick.RemoveListener(ToggleSpriteGrid);
            if (_colorPicker != null) _colorPicker.onColorChange.RemoveListener(OnPickerColorChanged);
        }

    #endregion

    #region Public API

public void SetLevelData(RhythmLevelData data)
        {
            if (data == null) return;
            _levelData = data;
            RefreshFromData();
        }

public void RefreshFromData()
        {
            if (_levelData == null)
            {
                ResolveDependencies();
                if (_levelData == null) return;
            }

            _isRefreshing = true;
            try
            {
                if (_particlesToggle != null) _particlesToggle.SetIsOnWithoutNotify(_levelData.particlesEnabled);
                if (_sphereToggle != null) _sphereToggle.SetIsOnWithoutNotify(_levelData.sphereRotates);
                if (_sphereUseVideoToggle != null) _sphereUseVideoToggle.SetIsOnWithoutNotify(_levelData.sphereUseVideo);

                if (_particleColorPreview != null) _particleColorPreview.color = _levelData.particleColor;
                if (_obstacleColorPreview != null) _obstacleColorPreview.color = _levelData.obstacleColor;
                if (_trackColorPreview != null) _trackColorPreview.color = _levelData.trackColor;
                UpdateSpriteLabel();

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
            if (_isRefreshing) return;
            if (_levelData == null) ResolveDependencies();
            if (_levelData == null) return;
            _levelData.particlesEnabled = value;
            ApplyVisual();
        }

        private void OnSphereChanged(bool value)
        {
            if (_isRefreshing) return;
            if (_levelData == null) ResolveDependencies();
            if (_levelData == null) return;
            _levelData.sphereRotates = value;
            ApplyVisual();
        }

        private void OnSphereVideoChanged(bool value)
        {
            if (_isRefreshing) return;
            if (_levelData == null) ResolveDependencies();
            if (_levelData == null) return;
            _levelData.sphereUseVideo = value;
            ApplyVisual();
        }

        public void OpenParticleColorPicker() => OpenPicker(PickerTarget.Particle);

        public void OpenParticleSettings() => OpenPicker(PickerTarget.Particle);
        public void OpenTrackColorPicker() => OpenPicker(PickerTarget.Track);
        public void OpenObstacleColorPicker() => OpenPicker(PickerTarget.Obstacle);

        private void OpenPicker(PickerTarget target)
        {
            if (_levelData == null) ResolveDependencies();
            if (_levelData == null) { Debug.LogWarning("[VisualSettings] Нет LevelData для пикера.", this); return; }
            if (_colorPicker == null)
            {
                var pickers = FindObjectsByType<FlexibleColorPicker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (pickers != null && pickers.Length > 0) _colorPicker = pickers[0];
            }
            if (_colorPicker == null) { Debug.LogWarning("[VisualSettings] FlexibleColorPicker не найден в сцене.", this); return; }

            if (_pickerTarget == target && _colorPicker.gameObject.activeSelf)
            {
                _colorPicker.gameObject.SetActive(false);
                _pickerTarget = PickerTarget.None;
                return;
            }
            _pickerTarget = target;
            Color current = target switch
            {
                PickerTarget.Particle => _levelData.particleColor,
                PickerTarget.Obstacle => _levelData.obstacleColor,
                PickerTarget.Track => _levelData.trackColor,
                _ => Color.white
            };
            _colorPicker.gameObject.SetActive(true);
            _colorPicker.SetColor(current);
            _colorPicker.onColorChange.RemoveListener(OnPickerColorChanged);
            _colorPicker.onColorChange.AddListener(OnPickerColorChanged);

            OnPickerColorChanged(current);
        }

        private void OnPickerColorChanged(Color c)
        {
            if (_levelData == null) return;
            c.a = 1f;
            switch (_pickerTarget)
            {
                case PickerTarget.Particle:
                    _levelData.particleColor = c;
                    if (_particleColorPreview != null) _particleColorPreview.color = c;
                    break;
                case PickerTarget.Obstacle:
                    _levelData.obstacleColor = c;
                    if (_obstacleColorPreview != null) _obstacleColorPreview.color = c;
                    break;
                case PickerTarget.Track:
                    _levelData.trackColor = c;
                    if (_trackColorPreview != null) _trackColorPreview.color = c;
                    break;
                default: return;
            }
            ApplyVisual();
        }

        public void CloseColorPicker()
        {
            if (_colorPicker != null) _colorPicker.gameObject.SetActive(false);
            _pickerTarget = PickerTarget.None;
        }

        public void ToggleSpriteGrid()
        {
            if (_levelData == null) ResolveDependencies();
            if (_levelData == null) return;
            EnsureSpriteGrid();
            if (_spriteGridPanel == null) return;
            bool show = !_spriteGridPanel.activeSelf;
            _spriteGridPanel.SetActive(show);
            if (show) BuildSpriteOptions();
        }

        public void HideSpriteGrid()
        {
            if (_spriteGridPanel != null) _spriteGridPanel.SetActive(false);
        }

        private void EnsureSpriteGrid()
        {
            if (_spriteGridPanel != null && _spriteGridContainer != null) return;
            if (_spriteGridPanel != null && _spriteGridContainer == null)
            {
                var t = _spriteGridPanel.transform.Find("Grid");
                if (t != null) _spriteGridContainer = t;
                else _spriteGridContainer = _spriteGridPanel.transform;
            }
            if (_spriteGridPanel != null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var panelGO = new GameObject("SpriteGridPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panelGO.transform.SetParent(canvas.transform, false);
            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(560, 420);
            panelGO.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            var vlg = panelGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12); vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperCenter; vlg.childControlWidth = true; vlg.childControlHeight = false;
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(panelGO.transform, false);
            var ttmp = titleGO.AddComponent<TextMeshProUGUI>();
            ttmp.text = "Спрайт частиц"; ttmp.fontSize = 16;
            ttmp.alignment = TextAlignmentOptions.Center; ttmp.color = Color.white;
            var tle = titleGO.AddComponent<LayoutElement>(); tle.minHeight = 24;
            var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGO.transform.SetParent(panelGO.transform, false);
            var sle = scrollGO.AddComponent<LayoutElement>(); sle.flexibleHeight = 1; sle.minHeight = 200;
            scrollGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);
            scrollGO.GetComponent<Mask>().showMaskGraphic = false;
            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(scrollGO.transform, false);
            var grt = gridGO.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0, 1); grt.anchorMax = new Vector2(1, 1);
            grt.pivot = new Vector2(0.5f, 1); grt.anchoredPosition = Vector2.zero;
            var glg = gridGO.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(80, 80); glg.spacing = new Vector2(8, 8);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; glg.constraintCount = 6;
            glg.childAlignment = TextAnchor.UpperCenter;
            var csf = gridGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = grt;
            scroll.viewport = scrollGO.GetComponent<RectTransform>();
            var closeGO = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGO.transform.SetParent(panelGO.transform, false);
            closeGO.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 32);
            closeGO.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f, 1);
            closeGO.GetComponent<Button>().onClick.AddListener(HideSpriteGrid);
            var ctxt = new GameObject("Text", typeof(RectTransform));
            ctxt.transform.SetParent(closeGO.transform, false);
            var ctrt = ctxt.GetComponent<RectTransform>();
            ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = Vector2.one;
            ctrt.offsetMin = Vector2.zero; ctrt.offsetMax = Vector2.zero;
            var ctmp = ctxt.AddComponent<TextMeshProUGUI>();
            ctmp.text = "Закрыть"; ctmp.fontSize = 14;
            ctmp.alignment = TextAlignmentOptions.Center; ctmp.color = Color.white;
            _spriteGridPanel = panelGO;
            _spriteGridContainer = grt.transform;
            _spriteGridPanel.SetActive(false);
        }

        private void BuildSpriteOptions()
        {
            if (_spriteGridContainer == null) return;
            for (int i = _spriteGridContainer.childCount - 1; i >= 0; i--)
                Destroy(_spriteGridContainer.GetChild(i).gameObject);
            AddSpriteOption("Дефолт", "", null, true);
            foreach (var preset in ParticleSpriteLibrary.PresetNames)
                AddSpriteOption(preset, ParticleSpriteLibrary.PresetPrefix + preset, ParticleSpriteLibrary.Get(ParticleSpriteLibrary.PresetPrefix + preset), false);
            var resourcesSprites = Resources.LoadAll<Sprite>("");
            foreach (var spr in resourcesSprites)
            {
                if (spr == null) continue;
                AddSpriteOption(spr.name, spr.name, spr, false);
            }
        }

        private void AddSpriteOption(string name, string storedName, Sprite spr, bool isDefault)
        {
            var btnGO = new GameObject("SprBtn_" + name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_spriteGridContainer, false);
            btnGO.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
            btnGO.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.24f, 1f);
            if (spr != null)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(btnGO.transform, false);
                var irt = iconGO.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.1f, 0.22f); irt.anchorMax = new Vector2(0.9f, 0.9f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iimg = iconGO.GetComponent<Image>();
                iimg.sprite = spr; iimg.color = Color.white; iimg.preserveAspect = true;
            }
            var lblGO = new GameObject("Label", typeof(RectTransform));
            lblGO.transform.SetParent(btnGO.transform, false);
            var lrt = lblGO.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0.22f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var ltmp = lblGO.AddComponent<TextMeshProUGUI>();
            ltmp.text = name; ltmp.fontSize = 8;
            ltmp.alignment = TextAlignmentOptions.Center; ltmp.color = new Color(1, 1, 1, 0.9f);
            ltmp.enableWordWrapping = false; ltmp.overflowMode = TextOverflowModes.Ellipsis;
            string capturedName = storedName;
            Sprite capturedSpr = spr;
            bool capturedDefault = isDefault;
            btnGO.GetComponent<Button>().onClick.AddListener(() => OnSpritePicked(capturedName, capturedSpr, capturedDefault));
            if (!isDefault && _levelData != null && _levelData.particleSpriteName == capturedName)
            {
                var ol = btnGO.AddComponent<Outline>();
                ol.effectColor = Color.green; ol.effectDistance = new Vector2(3, 3);
            }
        }

        private void OnSpritePicked(string storedName, Sprite spr, bool isDefault)
        {
            if (_levelData == null) return;
            if (isDefault)
            {
                _levelData.particleSpriteName = "";
                _levelData.particleSprite = null;
            }
            else
            {
                _levelData.particleSpriteName = storedName;
                _levelData.particleSprite = spr;
            }
            UpdateSpriteLabel();
            HideSpriteGrid();
            ApplyVisual();
        }

        private void UpdateSpriteLabel()
        {
            if (_particleSpriteLabel == null || _levelData == null) return;
            string n = _levelData.particleSpriteName;
            if (string.IsNullOrEmpty(n)) _particleSpriteLabel.text = "Спрайт: дефолт";
            else if (n.StartsWith(ParticleSpriteLibrary.PresetPrefix)) _particleSpriteLabel.text = "Спрайт: " + n.Substring(ParticleSpriteLibrary.PresetPrefix.Length);
            else _particleSpriteLabel.text = "Спрайт: " + n;
        }

        public void ClearParticleSprite()
        {
            if (_levelData == null) return;
            _levelData.particleSprite = null;
            _levelData.particleSpriteName = "";
            UpdateSpriteLabel();
            ApplyVisual();
        }

    #endregion

    #region Color Cycling (legacy, оставлено для совместимости)

        public void CycleParticleColor() => OpenParticleColorPicker();
        public void CycleObstacleColor() => OpenObstacleColorPicker();
        public void CycleTrackColor() => OpenTrackColorPicker();

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

    #region Material Selection (без fallback)

        public void ToggleMaterialGrid()
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

            var catalogMaterials = catalog != null ? catalog.GetAllMaterials() : new List<Material>();

            if (catalogMaterials.Count == 0)
            {
                Debug.LogWarning("[VisualSettings] Каталог материалов пуст — доступен только дефолт. Заполни GlobalObstacleCatalog.", this);
                return;
            }
            foreach (var mat in catalogMaterials)
            {
                if (mat != null) AddMaterialOption(mat.name, mat, isDefault: false);
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
            if (_defaultMaterialLabel == null || _levelData == null) return;

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
                var mat = catalog != null ? catalog.GetMaterial(_levelData.defaultObstacleMaterialName) : null;
                if (mat != null) _levelData.defaultObstacleMaterial = mat;
            }

            if (_levelData.particleSprite == null && !string.IsNullOrEmpty(_levelData.particleSpriteName))
                _levelData.particleSprite = ParticleSpriteLibrary.Get(_levelData.particleSpriteName);

            if (visual != null) visual.Apply(_levelData, null, true);

            if (preview != null && !preview.previewEnabled) preview.SetPreviewEnabled(true);
            preview?.ForceRefresh();
        }

    #endregion
    }
}
