using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Меню загрузки уровней и входа в редактор.
/// Вешай на Canvas в IsGameScene / IsLevelEditorScene или в отдельной MainMenu сцене.
/// Если UI не назначен — создаст простой fallback через код.
/// Esc в игре открывает меню, Esc в меню закрывает.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Ссылки (опционально — создаст fallback)")]
    public GameObject menuPanel;
    public Transform levelListContainer;
    public GameObject levelButtonPrefab;
    public Button editorButton;
    public Button refreshButton;
    public Button closeButton;
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI statusLabel;

    [Header("Настройки")]
    public bool showOnEscInGame = true;
    public bool autoCreateFallbackUI = true;
    public string editorSceneName = "IsLevelEditorScene";
    public string gameSceneName = "IsGameScene";

    List<string> foundPaths = new List<string>();

    void Awake()
    {
        if (menuPanel == null)
        {
            var go = GameObject.Find("MenuPanel");
            if (go != null) menuPanel = go;
        }
        if (levelListContainer == null && menuPanel != null)
        {
            var t = menuPanel.transform.Find("Scroll/Viewport/Content");
            if (t != null) levelListContainer = t;
            else
            {
                var f = menuPanel.GetComponentInChildren<ScrollRect>();
                if (f != null && f.content != null) levelListContainer = f.content;
            }
        }
        if (autoCreateFallbackUI && menuPanel == null)
            CreateFallbackUI();
    }

    void Start()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        RefreshList();
        // бинды
        if (editorButton != null)
        {
            editorButton.onClick.RemoveListener(OpenEditor);
            editorButton.onClick.AddListener(OpenEditor);
        }
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshList);
            refreshButton.onClick.AddListener(RefreshList);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HideMenu);
            closeButton.onClick.AddListener(HideMenu);
        }
        // если пришли из редактора и нажали Esc в игре — меню не показываем сразу, только по Esc
    }

    void Update()
    {
        if (!showOnEscInGame) return;
        // в IsGameScene Esc — либо возврат в редактор (если fromEditor) либо меню
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            string cur = SceneManager.GetActiveScene().name;
            if (cur == gameSceneName && LevelTransfer.fromEditor)
            {
                // возврат уже обрабатывается в RhythmParkourManager, не мешаем
                return;
            }
            if (menuPanel != null)
            {
                if (menuPanel.activeSelf) HideMenu();
                else ShowMenu();
            }
        }
    }

    void CreateFallbackUI()
    {
        // гарантируем EventSystem
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Debug.Log("[Menu] Создан EventSystem", esGO);
        }
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            cgo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cgo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920,1080);
            Debug.Log("[Menu] Создан MenuCanvas", cgo);
        }
        Debug.Log($"[Menu] CreateFallbackUI canvas={canvas.name} panel={(menuPanel!=null?menuPanel.name:"null")}", this);

        // панель
        var panelGO = new GameObject("MenuPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGO.transform.SetParent(canvas.transform, false);
        var rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f,0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(720, 620);
        var img = panelGO.GetComponent<Image>(); img.color = new Color(0.08f,0.08f,0.10f,0.96f);
        var vlg = panelGO.GetComponent<VerticalLayoutGroup>(); vlg.padding = new RectOffset(16,16,16,16); vlg.spacing = 10; vlg.childAlignment = TextAnchor.UpperCenter; vlg.childControlWidth = true; vlg.childControlHeight = false;

        // заголовок
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panelGO.transform, false);
        var ttmp = titleGO.AddComponent<TextMeshProUGUI>(); ttmp.text = "МЕНЮ УРОВНЕЙ"; ttmp.fontSize = 22; ttmp.alignment = TextAlignmentOptions.Center; ttmp.color = Color.white;
        var tle = titleGO.AddComponent<LayoutElement>(); tle.minHeight = 30;

        // кнопки верхние
        var topRow = new GameObject("TopRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        topRow.transform.SetParent(panelGO.transform, false);
        var trh = topRow.GetComponent<HorizontalLayoutGroup>(); trh.spacing = 8; trh.childAlignment = TextAnchor.MiddleCenter; trh.childControlWidth = false;
        var trle = topRow.AddComponent<LayoutElement>(); trle.minHeight = 36;
        editorButton = CreateBtn(topRow.transform, "В РЕДАКТОР", new Color(0.2f,0.5f,0.9f));
        editorButton.onClick.AddListener(OpenEditor);
        refreshButton = CreateBtn(topRow.transform, "ОБНОВИТЬ", new Color(0.3f,0.3f,0.3f));
        refreshButton.onClick.AddListener(RefreshList);
        closeButton = CreateBtn(topRow.transform, "ЗАКРЫТЬ", new Color(0.5f,0.2f,0.2f));
        closeButton.onClick.AddListener(HideMenu);

        // статус
        var statusGO = new GameObject("Status", typeof(RectTransform));
        statusGO.transform.SetParent(panelGO.transform, false);
        statusLabel = statusGO.AddComponent<TextMeshProUGUI>(); statusLabel.text = ""; statusLabel.fontSize = 11; statusLabel.alignment = TextAlignmentOptions.Center; statusLabel.color = new Color(1,1,1,0.6f);
        var sle2 = statusGO.AddComponent<LayoutElement>(); sle2.minHeight = 18;

        // скролл
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGO.transform.SetParent(panelGO.transform, false);
        var srt = scrollGO.GetComponent<RectTransform>(); srt.sizeDelta = new Vector2(0, 400);
        var sle = scrollGO.AddComponent<LayoutElement>(); sle.flexibleHeight = 1; sle.minHeight = 300;
        var sImg = scrollGO.GetComponent<Image>(); sImg.color = new Color(0,0,0,0.2f);
        scrollGO.GetComponent<Mask>().showMaskGraphic = false;
        var scroll = scrollGO.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(scrollGO.transform, false);
        var crt = contentGO.GetComponent<RectTransform>(); crt.anchorMin = new Vector2(0,1); crt.anchorMax = new Vector2(1,1); crt.pivot = new Vector2(0.5f,1); crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(0,0);
        var cvlg = contentGO.GetComponent<VerticalLayoutGroup>(); cvlg.spacing = 6; cvlg.padding = new RectOffset(4,4,4,4); cvlg.childAlignment = TextAnchor.UpperCenter; cvlg.childControlWidth = true; cvlg.childControlHeight = false;
        var csf = contentGO.GetComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = crt;
        scroll.viewport = srt;
        levelListContainer = crt.transform;

        // префаб кнопки уровня — храним как неактивный child MenuController, не DontDestroy
        var btnGO = new GameObject("LevelButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
        btnGO.transform.SetParent(transform, false);
        var brt = btnGO.GetComponent<RectTransform>(); brt.sizeDelta = new Vector2(0, 54);
        var bhlg = btnGO.GetComponent<HorizontalLayoutGroup>(); bhlg.spacing = 8; bhlg.padding = new RectOffset(10,10,6,6); bhlg.childAlignment = TextAnchor.MiddleLeft;
        var bimg = btnGO.GetComponent<Image>(); bimg.color = new Color(0.18f,0.18f,0.20f,1f);
        var btn = btnGO.GetComponent<Button>();
        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(btnGO.transform, false);
        var trt = txtGO.GetComponent<RectTransform>(); trt.anchorMin = new Vector2(0,0); trt.anchorMax = new Vector2(1,1); trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var ttmp2 = txtGO.AddComponent<TextMeshProUGUI>(); ttmp2.text = "Level"; ttmp2.fontSize = 13; ttmp2.alignment = TextAlignmentOptions.Left; ttmp2.color = Color.white;
        levelButtonPrefab = btnGO;
        btnGO.SetActive(false);

        menuPanel = panelGO;
        menuPanel.transform.SetAsLastSibling();
        titleLabel = ttmp;
        // statusLabel уже есть
        menuPanel.SetActive(false);
        Debug.Log("[Menu] Fallback UI создан", menuPanel);
    }

    Button CreateBtn(Transform parent, string txt, Color col)
    {
        var go = new GameObject("Btn_"+txt, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(160, 36);
        var img = go.GetComponent<Image>(); img.color = col;
        var btn = go.GetComponent<Button>();
        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var trt = tgo.GetComponent<RectTransform>(); trt.anchorMin=Vector2.zero; trt.anchorMax=Vector2.one; trt.offsetMin=Vector2.zero; trt.offsetMax=Vector2.zero;
        var tmp = tgo.AddComponent<TextMeshProUGUI>(); tmp.text=txt; tmp.fontSize=14; tmp.alignment=TextAlignmentOptions.Center; tmp.color=Color.white;
        var le = go.AddComponent<LayoutElement>(); le.minWidth=120; le.minHeight=36;
        return btn;
    }

    public void ShowMenu()
    {
        if (menuPanel == null) CreateFallbackUI();
        if (menuPanel == null) { Debug.LogError("[Menu] menuPanel всё ещё null!"); return; }
        menuPanel.SetActive(true);
        menuPanel.transform.SetAsLastSibling();
        RefreshList();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[Menu] ShowMenu", menuPanel);
    }
    public void HideMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        Time.timeScale = 1f;
        // курсор вернет игра сама
        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void RefreshList()
    {
        if (levelListContainer == null) return;
        for (int i=levelListContainer.childCount-1;i>=0;i--) Destroy(levelListContainer.GetChild(i).gameObject);
        foundPaths.Clear();
        var dirs = new List<string>();
        dirs.Add(Path.Combine(Application.persistentDataPath, "Levels"));
        dirs.Add(Path.Combine(Application.persistentDataPath, "LevelTransfer"));
        dirs.Add(Path.Combine(Application.temporaryCachePath, "LevelTransfer"));
        // StreamingAssets
        string sa = Path.Combine(Application.streamingAssetsPath, "GameLevels");
        dirs.Add(sa);
        // также папка Levels в проекте (для эдитора)
        dirs.Add(Path.Combine(Application.dataPath, "!Rhythm Parkour/Levels"));
        dirs.Add(Path.Combine(Application.dataPath, "Levels"));

        HashSet<string> seen = new HashSet<string>();
        foreach (var d in dirs)
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
            var files = Directory.GetFiles(d, "*.rksl");
            foreach (var f in files)
            {
                if (seen.Contains(f)) continue;
                seen.Add(f);
                foundPaths.Add(f);
                AddLevelButton(f);
            }
        }
        // также файлы сохраненные через SaveFilePanel вне сканируемых папок (PlayerPrefs)
        string lastPath = PlayerPrefs.GetString("LastRkslPath", "");
        if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath) && !seen.Contains(lastPath))
        {
            seen.Add(lastPath); foundPaths.Add(lastPath); AddLevelButton(lastPath);
        }
        string transPath = PlayerPrefs.GetString("TransferRkslPath", "");
        if (!string.IsNullOrEmpty(transPath) && File.Exists(transPath) && !seen.Contains(transPath))
        {
            seen.Add(transPath); foundPaths.Add(transPath); AddLevelButton(transPath);
        }
        // если есть трансфер-уровень — показать первым
        if (LevelTransfer.hasLevel)
        {
            string name = string.IsNullOrEmpty(LevelTransfer.levelName) ? "Текущий (не сохранен)" : LevelTransfer.levelName;
            AddTransferButton(name);
        }
        if (foundPaths.Count==0 && !LevelTransfer.hasLevel)
        {
            var go = new GameObject("Empty", typeof(RectTransform));
            go.transform.SetParent(levelListContainer, false);
            var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.text = "Нет .rksl уровней\nСохрани из редактора (SAVE .RKSL)\nили закинь .rksl в " + Path.Combine(Application.persistentDataPath,"Levels"); tmp.fontSize=11; tmp.alignment=TextAlignmentOptions.Center; tmp.color=new Color(1,1,1,0.5f);
            var le = go.AddComponent<LayoutElement>(); le.minHeight=60;
        }
        if (statusLabel != null) statusLabel.text = $"{foundPaths.Count} уровней • Esc закрыть";
    }

    void AddLevelButton(string path)
    {
        RkslManifest man = null;
        RkslFile.LoadManifestOnly(path, out man);
        string title = man != null ? man.title : Path.GetFileNameWithoutExtension(path);
        string artist = man != null ? man.artist : "";
        string info = man != null ? $"{man.events.Count} нот • BPM {man.bpm:0}" : "";
        string label = string.IsNullOrEmpty(artist) ? title : $"{title} — {artist}";
        var btnGO = CreateLevelButtonGO(label, info, path);
        // клик — играть
        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(()=> LoadAndPlay(path));
        // ПКМ — редактировать? добавим вторую кнопку
        // найдем кнопку Edit внутри
        var editBtn = btnGO.transform.Find("EditBtn")?.GetComponent<Button>();
        if (editBtn != null) editBtn.onClick.AddListener(()=> LoadAndEdit(path));
    }
    void AddTransferButton(string name)
    {
        var btnGO = CreateLevelButtonGO($"[ТЕКУЩИЙ] {name}", "из редактора • не сохранен", "__transfer__");
        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(()=> {
            if (LevelTransfer.hasLevel)
            {
                // играть трансфер
                SceneManager.LoadScene(gameSceneName);
            }
        });
        var editBtn = btnGO.transform.Find("EditBtn")?.GetComponent<Button>();
        if (editBtn != null) { editBtn.GetComponentInChildren<TextMeshProUGUI>().text = "В редактор"; editBtn.onClick.AddListener(()=> OpenEditor()); }
        btnGO.GetComponent<Image>().color = new Color(0.15f,0.35f,0.18f,1f);
    }

    GameObject CreateLevelButtonGO(string label, string info, string path)
    {
        GameObject go;
        if (levelButtonPrefab != null)
        {
            go = Instantiate(levelButtonPrefab, levelListContainer);
            go.SetActive(true);
            // пробуем найти текст
            var txts = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (txts.Length>0) txts[0].text = label;
            if (txts.Length>1) txts[1].text = info;
            // путь в имени
            go.name = Path.GetFileName(path);
        }
        else
        {
            go = new GameObject(Path.GetFileName(path), typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(levelListContainer, false);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(0, 56);
            var img = go.GetComponent<Image>(); img.color = new Color(0.18f,0.18f,0.20f,1f);
            var btn = go.GetComponent<Button>();
            var hlg = go.AddComponent<HorizontalLayoutGroup>(); hlg.spacing=8; hlg.padding=new RectOffset(12,6,12,6); hlg.childAlignment=TextAnchor.MiddleLeft;
            var le = go.AddComponent<LayoutElement>(); le.minHeight=56;
            // текст
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var trt = txtGO.GetComponent<RectTransform>(); trt.anchorMin=new Vector2(0,0); trt.anchorMax=new Vector2(1,1); trt.offsetMin=Vector2.zero; trt.offsetMax=Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>(); tmp.text = $"<b>{label}</b>\n<size=10><color=#AAAAAA>{info} • {Path.GetFileName(path)}</color></size>"; tmp.fontSize=13; tmp.alignment=TextAlignmentOptions.Left; tmp.color=Color.white;
            // кнопка Edit
            var editGO = new GameObject("EditBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            editGO.transform.SetParent(go.transform, false);
            var ert = editGO.GetComponent<RectTransform>(); ert.sizeDelta = new Vector2(80, 32);
            var eimg = editGO.GetComponent<Image>(); eimg.color = new Color(0.22f,0.45f,0.85f,1f);
            var ebtn = editGO.GetComponent<Button>();
            var etxtGO = new GameObject("Text", typeof(RectTransform));
            etxtGO.transform.SetParent(editGO.transform, false);
            var etrt = etxtGO.GetComponent<RectTransform>(); etrt.anchorMin=Vector2.zero; etrt.anchorMax=Vector2.one; etrt.offsetMin=Vector2.zero; etrt.offsetMax=Vector2.zero;
            var etmp = etxtGO.AddComponent<TextMeshProUGUI>(); etmp.text="Edit"; etmp.fontSize=12; etmp.alignment=TextAlignmentOptions.Center; etmp.color=Color.white;
            ebtn.onClick.AddListener(()=> LoadAndEdit(path));
            return go;
        }
        // добавляем Edit кнопку если нет
        if (go.transform.Find("EditBtn")==null)
        {
            var editGO = new GameObject("EditBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            editGO.transform.SetParent(go.transform, false);
            var ert = editGO.GetComponent<RectTransform>(); ert.sizeDelta = new Vector2(80, 32);
            var eimg = editGO.GetComponent<Image>(); eimg.color = new Color(0.22f,0.45f,0.85f,1f);
            var etxtGO = new GameObject("Text", typeof(RectTransform));
            etxtGO.transform.SetParent(editGO.transform, false);
            var etrt = etxtGO.GetComponent<RectTransform>(); etrt.anchorMin=Vector2.zero; etrt.anchorMax=Vector2.one; etrt.offsetMin=Vector2.zero; etrt.offsetMax=Vector2.zero;
            var etmp = etxtGO.AddComponent<TextMeshProUGUI>(); etmp.text="Edit"; etmp.fontSize=12; etmp.alignment=TextAlignmentOptions.Center; etmp.color=Color.white;
            editGO.GetComponent<Button>().onClick.AddListener(()=> LoadAndEdit(path));
        }
        return go;
    }

    public void LoadAndPlay(string path)
    {
        if (path == "__transfer__")
        {
            Time.timeScale = 1f;
            if (menuPanel != null) menuPanel.SetActive(false);
            SceneManager.LoadScene(gameSceneName);
            return;
        }
        StartCoroutine(LoadAndPlayCo(path));
    }
    System.Collections.IEnumerator LoadAndPlayCo(string path)
    {
        LevelTransfer.SetRkslPath(path, "Menu");
        PlayerPrefs.SetString("LastRkslPath", path);
        PlayerPrefs.SetString("SelectedLevelPath", path);
        PlayerPrefs.Save();
        Debug.Log($"[Menu] LoadAndPlay {path} -> LevelTransfer.rkslPath set", this);
        Time.timeScale = 1f;
        if (menuPanel != null) menuPanel.SetActive(false);
        yield return null;
        SceneManager.LoadScene(gameSceneName);
    }
    public void LoadAndEdit(string path)
    {
        Time.timeScale = 1f;
        if (menuPanel != null) menuPanel.SetActive(false);
        StartCoroutine(LoadAndEditCo(path));
    }
    System.Collections.IEnumerator LoadAndEditCo(string path)
    {
        string extractDir = Path.Combine(Application.temporaryCachePath, "RkslExtract_" + Path.GetFileNameWithoutExtension(path));
        if (!RkslFile.Extract(path, extractDir, out var man, out var audioPath, out var videoPath, out var coverPath))
        {
            if (statusLabel!=null) statusLabel.text = "Ошибка .rksl";
            yield break;
        }
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            string url = "file://" + audioPath;
            AudioType type = GetAudioType(audioPath);
            using (var uwr = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success) clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(uwr);
            }
        }
        Sprite cover=null;
        if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
        {
            byte[] bytes = File.ReadAllBytes(coverPath);
            Texture2D tex=new Texture2D(2,2);
            if (tex.LoadImage(bytes)) cover=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),Vector2.one*0.5f);
        }
        var data = RkslFile.ToRuntimeData(man, clip, null, cover);
        data.audioPath = audioPath; data.videoPath = videoPath;
        LevelTransfer.SetLevel(data, "Menu");
        LevelTransfer.fromEditor = true;
        Time.timeScale = 1f;
        if (menuPanel != null) menuPanel.SetActive(false);
        SceneManager.LoadScene(editorSceneName);
    }

    public void OpenEditor()
    {
        Time.timeScale = 1f;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (!LevelTransfer.hasLevel)
        {
            var data = new RhythmLevelData();
            data.fullTitle = "New Level";
            data.bpm = 128f;
            LevelTransfer.SetLevel(data, "Menu");
        }
        LevelTransfer.fromEditor = true;
        LevelTransfer.sourceScene = editorSceneName;
        SceneManager.LoadScene(editorSceneName);
    }

    AudioType GetAudioType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch(ext){ case ".mp3": return AudioType.MPEG; case ".wav": return AudioType.WAV; case ".ogg": return AudioType.OGGVORBIS; default: return AudioType.UNKNOWN; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<MenuController>() != null) return;
        // создаем только в игровых сценах
        string cur = SceneManager.GetActiveScene().name;
        if (cur == "IsGameScene" || cur == "IsLevelEditorScene" || cur == "LevelEditor" || cur == "MainMenu")
        {
            var go = new GameObject("MenuController (Auto)");
            go.AddComponent<MenuController>();
        }
    }
}
