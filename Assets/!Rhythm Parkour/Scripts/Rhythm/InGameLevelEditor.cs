using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_STANDALONE || UNITY_EDITOR
// Для UnityWebRequest
using UnityEngine.Networking;
#endif

/// <summary>
/// Внутриигровой редактор на ОТДЕЛЬНОЙ сцене LevelEditor.unity (копия IsGameScene).
/// Простой UI: клик под бит + выбор префаба. Игра сама считает spawn = hit - travel (невидимая стена HitTrigger).
/// </summary>
public class InGameLevelEditor : MonoBehaviour
{
    [Header("Ссылки")]
    public RhythmLevelData levelData;
    public RhythmParkourManager manager;
    public Conductor conductor;
    public Transform hitTrigger; // невидимая стена-ориентир (жёлтый куб в IsGameScene)
    public FreeCamera freeCamera;
    public Camera editorCamera;

    [Header("UI")]
    public bool showUI = true;
    public KeyCode toggleKey = KeyCode.Tab;

    // внутреннее
    RhythmLevelData editingData; // клон для редактирования
    string songTitle = "";
    string songAuthor = "";
    string mapAuthor = "";
    float previewTime = 0f;
    bool isPreviewPlaying = false;
    double previewDspStart;
    int brushIndex = 0;
    float quant = 0.5f;
    Vector2 listScroll;
    string status = "";
    float statusTimer;

    // кэш волны для таймлайна
    float[] waveCache;
    float[] wave { get => waveCache; set => waveCache = value; }
    AudioClip waveClip;
    int waveFreq;

    void Awake()
    {
        if (!manager) manager = FindObjectOfType<RhythmParkourManager>();
        if (!conductor) conductor = FindObjectOfType<Conductor>();
        if (!levelData && manager) levelData = manager.levelData;
        if (levelData) editingData = Instantiate(levelData);
        else editingData = levelData;

        if (levelData)
        {
            songTitle = levelData.fullTitle;
            songAuthor = levelData.songAuthor;
            mapAuthor = levelData.mapAuthor;
        }
        if (!hitTrigger && manager) hitTrigger = manager.hitTrigger;
        if (!hitTrigger) hitTrigger = GameObject.Find("HitTrigger")?.transform ?? GameObject.Find("Trigger")?.transform;
        if (!editorCamera) editorCamera = Camera.main;

        // в LevelEditor сцене — свободная камера, игрок выкл
        if (SceneManager.GetActiveScene().name == "LevelEditor")
        {
            var fpc = FindObjectOfType<EasyPeasyFirstPersonController.FirstPersonController>();
            if (fpc) fpc.gameObject.SetActive(false);
            var cam = editorCamera ? editorCamera : Camera.main;
            if (cam)
            {
                freeCamera = cam.GetComponent<FreeCamera>();
                if (!freeCamera) freeCamera = cam.gameObject.AddComponent<FreeCamera>();
                freeCamera.enabled = true;
            }
        }
    }

    void Start()
    {
        // в LevelEditor сцене — сразу свободная камера, игрок выключен
        var fpc = FindObjectOfType<EasyPeasyFirstPersonController.FirstPersonController>();
        if (fpc) fpc.gameObject.SetActive(false);
        if (freeCamera) freeCamera.enabled = true;
        if (editorCamera) editorCamera.gameObject.SetActive(true);

        // примени визуал из editingData
        ApplyMapVisual();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) showUI = !showUI;

        // превью тайм
        if (isPreviewPlaying && editingData && editingData.music)
        {
            previewTime = (float)(AudioSettings.dspTime - previewDspStart);
            if (previewTime >= editingData.music.length)
            {
                isPreviewPlaying = false;
                previewTime = editingData.music.length;
            }
        }

        // хоткеи в редакторе
        if (showUI && Input.GetKeyDown(KeyCode.Space))
        {
            if (isPreviewPlaying) PausePreview(); else PlayPreview(previewTime);
        }
    }

    float TravelForSpeed(float speed)
    {
        if (speed < 1f) speed = 12f;
        Transform sp = manager ? manager.spawnPoint : null;
        Transform hit = hitTrigger ? hitTrigger : (manager ? manager.hitTrigger : null);
        float d = 52f;
        if (sp && hit)
        {
            Vector3 toHit = hit.position - sp.position; toHit.y = 0;
            Vector3 dir = (manager && manager.despawnPoint) ? (manager.despawnPoint.position - sp.position) : Vector3.forward;
            dir.y = 0; dir.Normalize();
            d = Mathf.Abs(Vector3.Dot(toHit, dir));
            if (d < 1f) d = 52f;
        }
        return d / Mathf.Max(1f, speed);
    }
    float GetHitBeat(ObstacleEvent ev) => editingData ? editingData.TimeToBeat(ev.time + TravelForSpeed(ev.speed)) : ev.beat;

    // ── UI ──
    void OnGUI()
    {
        if (!showUI)
        {
            GUI.Label(new Rect(12, 12, 320, 20), "Tab — редактор  •  FreeCamera: ПКМ+WASD Q/E Shift колесо", new GUIStyle(GUI.skin.label){fontSize=11, normal=new GUIStyleState{textColor=new Color(1,1,1,0.7f)}});
            return;
        }

        // затемняем фон
        GUI.color = new Color(0, 0, 0, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float leftW = 380f;
        float rightW = Screen.width - leftW - 24f;
        // левая панель — настройки карты
        GUILayout.BeginArea(new Rect(12, 12, leftW, Screen.height - 24));
        GUILayout.Label("РЕДАКТОР УРОВНЯ — ХИТ = у игрока", new GUIStyle(GUI.skin.label){fontSize=15, fontStyle=FontStyle.Bold, normal=new GUIStyleState{textColor=Color.white}});
        GUILayout.Label("Ставь ХИТ туда где удар. Спавн = хит - полёт (считает игра).", new GUIStyle(GUI.skin.label){fontSize=10, normal=new GUIStyleState{textColor=new Color(1,1,1,0.55f)}, wordWrap=true});

        GUILayout.Space(6);
        // 1. Песня
        GUILayout.Box("  Песня / файлы  ", GUILayout.Height(20));
        songTitle = LabeledTextField("Название*", songTitle);
        songAuthor = LabeledTextField("Автор песни", songAuthor);
        mapAuthor = LabeledTextField("Автор карты", mapAuthor);
        if (editingData) { editingData.fullTitle = songTitle; editingData.songAuthor = songAuthor; editingData.mapAuthor = mapAuthor; }

        GUILayout.Space(4);
        if (GUILayout.Button("Выбрать аудио файл (mp3/wav/ogg) с диска", GUILayout.Height(26)))
            PickAudio();
        if (editingData && editingData.music) GUILayout.Label($"Аудио: {editingData.music.name}  {editingData.music.length:0.0}с", new GUIStyle(GUI.skin.label){fontSize=10, normal=new GUIStyleState{textColor=new Color(0.7f,1f,0.7f)}});
        else if (!string.IsNullOrEmpty(editingData?.audioPath)) GUILayout.Label($"Путь: {editingData.audioPath}", new GUIStyle(GUI.skin.label){fontSize=9, normal=new GUIStyleState{textColor=new Color(1,1,0.7f,1f)}});

        if (GUILayout.Button("Выбрать видео файл (mp4) с диска", GUILayout.Height(22)))
            PickVideo();
        if (editingData && editingData.video) GUILayout.Label($"Видео: {editingData.video.name}", new GUIStyle(GUI.skin.label){fontSize=10, normal=new GUIStyleState{textColor=new Color(0.7f,1f,0.7f)}});

        if (editingData && editingData.cover) GUILayout.Label($"Обложка: {editingData.cover.name}", new GUIStyle(GUI.skin.label){fontSize=10, normal=new GUIStyleState{textColor=new Color(0.7f,1f,0.7f)}});
        if (GUILayout.Button("Выбрать обложку (png/jpg)", GUILayout.Height(22))) PickCover();

        GUILayout.Space(4);
        if (editingData)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("BPM", GUILayout.Width(40));
            float.TryParse(GUILayout.TextField(editingData.bpm.ToString("0.##"), GUILayout.Width(70)), out float nbpm);
            if (nbpm >= 1) editingData.bpm = nbpm;
            GUILayout.Label("Offset", GUILayout.Width(50));
            float.TryParse(GUILayout.TextField(editingData.offset.ToString("0.00"), GUILayout.Width(60)), out float noff);
            editingData.offset = noff;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        // 2. Визуал карты
        GUILayout.Box("  Визуал карты  ", GUILayout.Height(20));
        if (editingData)
        {
            bool pe = editingData.particlesEnabled;
            bool np = GUILayout.Toggle(pe, " Партиклы на сцене");
            if (np != pe) { editingData.particlesEnabled = np; ApplyMapVisual(); }
            editingData.particleColor = ColorField("Цвет партиклов", editingData.particleColor);
            editingData.obstacleColor = ColorField("Цвет препятствий", editingData.obstacleColor);
            editingData.trackColor = ColorField("Цвет дорожки", editingData.trackColor);
            bool sr = editingData.sphereRotates;
            bool nsr = GUILayout.Toggle(sr, " Сфера крутится под бит");
            if (nsr != sr) editingData.sphereRotates = nsr;
            if (GUILayout.Button("Применить цвета")) ApplyMapVisual();
        }

        GUILayout.Space(8);
        // кисть
        GUILayout.Box("  Кисть — чем ставить  ", GUILayout.Height(20));
        if (editingData && editingData.obstaclePrefabs.Count > 0)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Mathf.Min(7, editingData.obstaclePrefabs.Count); i++)
            {
                var go = editingData.obstaclePrefabs[i];
                if (!go) continue;
                GUI.backgroundColor = brushIndex == i ? Color.green : Color.white;
                if (GUILayout.Button($"{i}", GUILayout.Width(32), GUILayout.Height(26))) brushIndex = i;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            if (editingData.obstaclePrefabs.Count > brushIndex && editingData.obstaclePrefabs[brushIndex])
                GUILayout.Label($"→ {brushIndex}: {editingData.obstaclePrefabs[brushIndex].name}", new GUIStyle(GUI.skin.label){fontSize=11, normal=new GUIStyleState{textColor=Color.cyan}});
        }

        GUILayout.Space(8);
        GUILayout.Label("Пресеты генерации:", new GUIStyle(GUI.skin.label){fontSize=11, normal=new GUIStyleState{textColor=new Color(1,1,1,0.7f)}});
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Мало")) ApplyPreset(0);
        if (GUILayout.Button("Норма")) ApplyPreset(1);
        if (GUILayout.Button("Много")) ApplyPreset(2);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("★ Авто-постройка по музыке ★", GUILayout.Height(28)))
        {
            if (editingData && editingData.music) { var cfg = RhythmAutoGenerator.FlexibleSettings.Default; RhythmAutoGenerator.Generate(editingData, 0, cfg); status = $"Сгенерировано {editingData.events.Count} нот"; statusTimer = 3f; }
            else status = "Нет музыки!";
        }

        GUILayout.Space(8);
        // сохранение/запуск
        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
        if (GUILayout.Button("💾 Экспорт в файл (JSON) + Запуск", GUILayout.Height(34)))
        {
            string path = ExportToFile();
            if (!string.IsNullOrEmpty(path)) { status = $"Экспортировано: {path}"; statusTimer = 4f; LaunchLevel(path); }
        }
        GUI.backgroundColor = Color.white;
        if (!string.IsNullOrEmpty(status) && statusTimer > 0) { GUILayout.Label(status, new GUIStyle(GUI.skin.label){fontSize=11, normal=new GUIStyleState{textColor=Color.yellow}}); statusTimer -= Time.deltaTime; }
        GUILayout.Label("Tab — скрыть редактор  •  FreeCamera: ПКМ+WASD", new GUIStyle(GUI.skin.label){fontSize=9, wordWrap=true, normal=new GUIStyleState{textColor=new Color(1,1,1,0.4f)}});

        GUILayout.EndArea();

        // правая панель — таймлайн
        float rx = leftW + 24;
        float rw = rightW;
        GUILayout.BeginArea(new Rect(rx, 12, rw, Screen.height - 24));
        DrawTimelineInGame(rw);
        GUILayout.Space(8);
        DrawNotesList(rw);
        GUILayout.EndArea();

        // ESC — закрыть редактор и вернуть игру
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) showUI = false;
    }

    string LabeledTextField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110));
        string nv = GUILayout.TextField(value);
        GUILayout.EndHorizontal();
        return nv;
    }
    Color ColorField(string label, Color c)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(140));
        // Unity не имеет ColorField в OnGUI — делаем через picker кнопку
        if (GUILayout.Button("", GUILayout.Width(40), GUILayout.Height(16)))
        {
            // простой — случайный цвет для демо, в реале нужен ColorPicker
            c = new Color(Random.value, Random.value, Random.value);
        }
        GUI.color = c; GUILayout.Label("■■■", GUILayout.Width(30)); GUI.color = Color.white;
        GUILayout.EndHorizontal();
        return c;
    }

    void ApplyPreset(int idx)
    {
        if (!editingData) return;
        RhythmAutoGenerator.FlexibleSettings s = idx == 0 ? RhythmAutoGenerator.FlexibleSettings.Few : idx == 2 ? RhythmAutoGenerator.FlexibleSettings.Many : RhythmAutoGenerator.FlexibleSettings.Default;
        RhythmAutoGenerator.Generate(editingData, Random.Range(0, 9999), s);
        status = $"Пресет {(idx==0?"Мало":idx==1?"Норма":"Много")}: {editingData.events.Count} нот";
        statusTimer = 3f;
    }

    void PickAudio()
    {
        string path = PickFile("Аудио", "mp3,wav,ogg,aiff");
        if (string.IsNullOrEmpty(path)) return;
        editingData.audioPath = path;
        StartCoroutine(LoadAudio(path));
    }
    void PickVideo()
    {
        string path = PickFile("Видео", "mp4,mov,avi");
        if (string.IsNullOrEmpty(path)) return;
        editingData.videoPath = path;
#if UNITY_EDITOR
        // в эдиторе можно сразу загрузить VideoClip по пути Assets/...
        string assetPath = path.Replace(Application.dataPath, "Assets");
        var vc = AssetDatabase.LoadAssetAtPath<VideoClip>(assetPath);
        if (vc) editingData.video = vc;
        else status = "Видео вне Assets — будет file://";
#else
        status = "Видео: " + Path.GetFileName(path);
#endif
        statusTimer = 3f;
    }
    void PickCover()
    {
        string path = PickFile("Обложка", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path)) return;
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        editingData.cover = spr;
        status = "Обложка загружена";
        statusTimer = 2f;
    }

    string PickFile(string title, string exts)
    {
#if UNITY_EDITOR
        string ext = exts.Split(',')[0];
        string path = EditorUtility.OpenFilePanel(title, "", ext);
        return path;
#else
        // в билде — пробуем StandaloneFileBrowser через рефлексию, иначе Input
        try {
            var t = System.Type.GetType("SFB.StandaloneFileBrowser, Assembly-CSharp");
            if (t != null) {
                var m = t.GetMethod("OpenFilePanel", new System.Type[]{typeof(string), typeof(string), typeof(string), typeof(bool)});
                if (m != null) {
                    var res = m.Invoke(null, new object[]{title, "", exts, false}) as string[];
                    if (res != null && res.Length>0) return res[0];
                }
            }
        } catch {}
        // фолбэк — диалог не доступен, просим ввести путь руками
        return "";
#endif
    }

    System.Collections.IEnumerator LoadAudio(string path)
    {
        string url = "file://" + path;
        using (var uwr = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(uwr);
                clip.name = Path.GetFileNameWithoutExtension(path);
                editingData.music = clip;
                editingData.fullTitle = clip.name;
                wave = null;
                status = $"Аудио загружено: {clip.name} {clip.length:0.0}с";
            }
            else status = $"Ошибка аудио: {uwr.error}";
            statusTimer = 3f;
        }
    }

    void ApplyMapVisual()
    {
        if (!editingData) return;
        // дорожка
        var ground = GameObject.Find("Ground");
        if (ground)
        {
            var rend = ground.GetComponent<Renderer>();
            if (rend) rend.material.color = editingData.trackColor;
            // можно через MaterialPropertyBlock
        }
        // препятствия — цвет через sharedMaterial (в рантайме инстанцируем)
        // партиклы
        var ps = FindObjectsOfType<ParticleSystem>();
        foreach (var p in ps)
        {
            var main = p.main;
            main.startColor = editingData.particleColor;
            p.gameObject.SetActive(editingData.particlesEnabled);
        }
        // сфера
        var sphere = FindObjectOfType<SphereBeatRotator>();
        if (sphere) sphere.enabled = editingData.sphereRotates;
    }

    // ── таймлайн в игре ──
    void DrawTimelineInGame(float width)
    {
        if (!editingData || !editingData.music)
        {
            GUILayout.Box("Таймлайн — нет музыки", GUILayout.Height(180));
            return;
        }
        GUILayout.Box("  Таймлайн — ХИТ (у игрока) — клик +кнопка ставит ноту  ", GUILayout.Height(18));
        // транспорт
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("⏮ 0", GUILayout.Width(44))) { previewTime = 0; }
        if (GUILayout.Button(isPreviewPlaying ? "■" : "▶", GUILayout.Width(44))) { if (isPreviewPlaying) PausePreview(); else PlayPreview(previewTime); }
        GUILayout.Label($"{previewTime:0.0}/{editingData.music.length:0.0}с  {editingData.TimeToBeat(previewTime):0.0}б", GUILayout.Width(120));
        quant = GUILayout.HorizontalSlider(quant, 0.25f, 1f, GUILayout.Width(60));
        GUILayout.Label($"квант {quant:0.00}", GUILayout.Width(70));
        GUILayout.EndHorizontal();
        // слайдер времени — двигает время уровня
        float nt = GUILayout.HorizontalSlider(previewTime, 0, editingData.music.length, GUILayout.Height(16));
        if (!Mathf.Approximately(nt, previewTime))
        {
            previewTime = nt;
            if (isPreviewPlaying) { previewDspStart = AudioSettings.dspTime - previewTime; }
        }

        // волна + сетка + ноты
        Rect r = GUILayoutUtility.GetRect(width, 140);
        GUI.Box(r, "");
        GUI.color = new Color(0.13f, 0.13f, 0.15f, 1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // волна
        if (wave == null || waveClip != editingData.music) EnsureWaveInGame();
        if (wave != null && wave.Length > 4)
        {
            int cols = Mathf.RoundToInt(r.width);
            float totalW = r.width; // в игре totalW = r.width (нет зума)
            for (int px = 0; px < cols; px++)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt((float)px / cols * wave.Length), 0, wave.Length - 1);
                float amp = wave[idx];
                float h = amp * r.height * 0.42f;
                Rect wr = new Rect(r.x + px, r.y + r.height * 0.5f - h * 0.5f, 1, h);
                GUI.color = new Color(0.3f, 0.7f, 1f, 0.18f);
                GUI.DrawTexture(wr, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        // сетка
        float totalBeats = editingData.TimeToBeat(editingData.music.length);
        for (int b = 0; b < totalBeats; b++)
        {
            float x = r.x + (b / totalBeats) * r.width;
            bool bar = b % 4 == 0;
            GUI.color = bar ? new Color(1, 1, 1, 0.22f) : new Color(1, 1, 1, 0.06f);
            GUI.DrawTexture(new Rect(x, r.y, bar ? 2 : 1, r.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // ноты — ХИТ
        for (int i = 0; i < editingData.events.Count; i++)
        {
            var ev = editingData.events[i];
            float hb = GetHitBeat(ev);
            float x = r.x + (hb / totalBeats) * r.width;
            Rect hr = new Rect(x - 5, r.y + r.height * 0.5f - 10, 10, 20);
            Color c = Color.HSVToRGB((i * 0.37f) % 1f, 0.75f, 0.9f);
            GUI.color = c;
            GUI.DrawTexture(hr, Texture2D.whiteTexture);
            if (Event.current.type == EventType.MouseDown && hr.Contains(Event.current.mousePosition))
            {
                // выбор
                // удалить на ПКМ — упростим
            }
        }

        // плейхед
        float cx = r.x + (editingData.TimeToBeat(previewTime) / totalBeats) * r.width;
        GUI.color = new Color(0, 1, 0.5f, 0.9f);
        GUI.DrawTexture(new Rect(cx, r.y, 2, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // клик по таймлайну — добавить ноту на ХИТ
        if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition) && Event.current.button == 0)
        {
            float beat = ((Event.current.mousePosition.x - r.x) / r.width) * totalBeats;
            beat = Mathf.Round(beat / quant) * quant;
            float hitT = editingData.BeatToTime(beat);
            float travel = TravelForSpeed(12f);
            float spawnT = hitT - travel;
            float spawnB = editingData.TimeToBeat(spawnT);
            var ev = ObstacleEvent.Create(spawnB, brushIndex, Vector3.zero, 12f);
            ev.time = spawnT;
            if (editingData.obstaclePrefabs.Count > brushIndex && editingData.obstaclePrefabs[brushIndex])
            {
                var ob = editingData.obstaclePrefabs[brushIndex].GetComponent<Obstacle>();
                if (ob) ev.speed = ob.baseSpeed;
            }
            editingData.events.Add(ev);
            editingData.SortByTime();
            Event.current.Use();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Нота на текущем ХИТе", GUILayout.Height(26)))
        {
            float hitBeat = Mathf.Round(editingData.TimeToBeat(previewTime) / quant) * quant;
            float hitT = editingData.BeatToTime(hitBeat);
            float travel = TravelForSpeed(12f);
            float spawnT = hitT - travel;
            float spawnB = editingData.TimeToBeat(spawnT);
            var ev = ObstacleEvent.Create(spawnB, brushIndex, Vector3.zero, 12f);
            ev.time = spawnT;
            if (editingData.obstaclePrefabs.Count > brushIndex && editingData.obstaclePrefabs[brushIndex])
            {
                var ob = editingData.obstaclePrefabs[brushIndex].GetComponent<Obstacle>();
                if (ob) ev.speed = ob.baseSpeed;
            }
            editingData.events.Add(ev);
            editingData.SortByTime();
        }
        if (GUILayout.Button("Квантовать всё", GUILayout.Width(110)))
        {
            float q = quant;
            for (int i = 0; i < editingData.events.Count; i++)
            {
                var ev = editingData.events[i];
                float hb = GetHitBeat(ev);
                hb = Mathf.Round(hb / q) * q;
                float nt2 = editingData.BeatToTime(hb) - TravelForSpeed(ev.speed);
                ev.beat = editingData.TimeToBeat(nt2);
                ev.time = nt2;
                editingData.events[i] = ev;
            }
            editingData.SortByTime();
        }
        GUILayout.EndHorizontal();
    }

    void EnsureWaveInGame()
    {
        if (!editingData || !editingData.music) return;
        var clip = editingData.music;
        int ch = clip.channels;
        int samples = clip.samples;
        if (samples <= 0) { wave = new float[1]; return; }
        int step = Mathf.Max(1, samples / 2048);
        int cnt = samples / step;
        float[] all = new float[samples * ch];
        clip.GetData(all, 0);
        wave = new float[cnt];
        float max = 0.001f;
        for (int i = 0; i < cnt; i++)
        {
            float s = 0;
            for (int c = 0; c < ch; c++) s += Mathf.Abs(all[(i * step) * ch + c]);
            s /= ch;
            wave[i] = s;
            if (s > max) max = s;
        }
        for (int i = 0; i < cnt; i++) wave[i] /= max;
        waveClip = clip;
        waveFreq = clip.frequency;
    }

    void DrawNotesList(float width)
    {
        GUILayout.Label($"Ноты — {editingData.events.Count}  (HIT у игрока)", new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold, normal=new GUIStyleState{textColor=Color.white}});
        listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(160));
        for (int i = 0; i < editingData.events.Count; i++)
        {
            var ev = editingData.events[i];
            float hb = GetHitBeat(ev);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"#{i}  HIT {hb:0.##}б  спавн {ev.beat:0.##}б  #{ev.prefabIndex} {ev.speed:0}m/s", GUILayout.Width(width - 60));
            if (GUILayout.Button("×", GUILayout.Width(22))) { editingData.events.RemoveAt(i); break; }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    // экспорт
    [System.Serializable]
    class LevelSave
    {
        public string fullTitle, songAuthor, mapAuthor, audioPath, videoPath;
        public float bpm, offset;
        public bool particlesEnabled;
        public Color particleColor, obstacleColor, trackColor;
        public bool sphereRotates;
        public List<ObstacleEvent> events;
        public string coverBase64;
    }

    string ExportToFile()
    {
        if (!editingData) return "";
        var save = new LevelSave
        {
            fullTitle = editingData.fullTitle,
            songAuthor = editingData.songAuthor,
            mapAuthor = editingData.mapAuthor,
            audioPath = editingData.audioPath,
            videoPath = editingData.videoPath,
            bpm = editingData.bpm,
            offset = editingData.offset,
            particlesEnabled = editingData.particlesEnabled,
            particleColor = editingData.particleColor,
            obstacleColor = editingData.obstacleColor,
            trackColor = editingData.trackColor,
            sphereRotates = editingData.sphereRotates,
            events = new List<ObstacleEvent>(editingData.events),
            coverBase64 = editingData.cover ? System.Convert.ToBase64String(editingData.cover.texture.EncodeToPNG()) : ""
        };
        string json = JsonUtility.ToJson(save, true);
        string dir = Path.Combine(Application.persistentDataPath, "Levels");
        Directory.CreateDirectory(dir);
        string safeName = string.IsNullOrEmpty(save.fullTitle) ? "NewLevel" : string.Join("_", save.fullTitle.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(dir, safeName + ".json");
#if UNITY_EDITOR
        string assetPath = "Assets/!Rhythm Parkour/Levels/" + safeName + ".json";
        File.WriteAllText(assetPath, json);
        AssetDatabase.Refresh();
        return assetPath;
#else
        File.WriteAllText(path, json);
        return path;
#endif
    }

    void LaunchLevel(string path)
    {
        // в эдиторе — просто Play
        if (manager) { manager.levelData = editingData; manager.PrepareLevel(editingData); }
        // в билде — загрузка IsGameScene с этим LevelData
        // Для простоты — меняем уровень менеджера и стартуем
        showUI = false;
        Time.timeScale = 1f;
        var fpc = FindObjectOfType<EasyPeasyFirstPersonController.FirstPersonController>();
        if (fpc) fpc.SetControl(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (manager) manager.Play();
        else if (conductor) conductor.Play(editingData, FindObjectOfType<AudioSource>());
    }

    void PlayPreview(float t)
    {
        previewTime = t;
        isPreviewPlaying = true;
        previewDspStart = AudioSettings.dspTime - previewTime;
        // найти AudioSource превью (если есть) или взять manager.musicSource
        AudioSource src = manager ? manager.musicSource : FindObjectOfType<AudioSource>();
        if (src && editingData && editingData.music)
        {
            src.clip = editingData.music;
            src.time = Mathf.Clamp(t, 0, editingData.music.length - 0.01f);
            src.Play();
        }
    }
    void PausePreview()
    {
        isPreviewPlaying = false;
        var src = manager ? manager.musicSource : FindObjectOfType<AudioSource>();
        if (src) src.Pause();
    }
}

public static class LevelEditorBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LevelEditor"
            && Object.FindObjectOfType<InGameLevelEditor>() == null)
        {
            var go = new GameObject("InGameLevelEditor (Auto)");
            go.AddComponent<InGameLevelEditor>();
            Debug.Log("[LevelEditor] InGameLevelEditor авто-создан для сцены LevelEditor");
        }
    }
}
