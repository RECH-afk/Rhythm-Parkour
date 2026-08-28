using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class RhythmLevelEditorWindow : EditorWindow
{
    RhythmLevelData level;
    AudioSource previewSource;
    GameObject previewGO;
    double dspStart;
    float previewTime;
    bool isPlaying;
    ReorderableList eventList;
    Vector2 scroll; // для списка нот (вертикальный)
    Vector2 timelineScroll; // отдельно для таймлайна (горизонтальный)
    float zoom = 1f;
    bool showWaveform = true;
    bool snapHalf = true;

    // таймлайн-интеракция
    int hoverIndex = -1;
    int draggedIndex = -1;
    float dragStartBeat;
    Vector2 dragStartMouse;
    bool isScrubbing;
    float[] waveformCache;
    AudioClip waveformClip;
    int waveformFreq;

    // настройки авто-генерации — сбалансировано
    float genDensity = 0.72f;
    float genThreshold = 0.24f;
    float genMinSpeed = 10f;
    float genMaxSpeed = 20f;

    // фолдауты для понятности
    bool foldMusic = true;
    bool foldPalette = true;
    bool foldAuto = true;

    [MenuItem("Window/Rhythm Parkour/Level Editor")]
    public static void Open() { var w = GetWindow<RhythmLevelEditorWindow>("Rhythm Level"); w.minSize = new Vector2(780, 620); w.Show(); }

    [MenuItem("Assets/Create/Rhythm Parkour/Level Data", false, 0)]
    public static void CreateAsset() { var a = CreateInstance<RhythmLevelData>(); string p = "Assets/!Rhythm Parkour/Levels/NewRhythmLevel.asset"; p = AssetDatabase.GenerateUniqueAssetPath(p); AssetDatabase.CreateAsset(a, p); AssetDatabase.SaveAssets(); Selection.activeObject = a; Open(); }

    void OnEnable()
    {
        previewGO = new GameObject("~RhythmPreview"); previewGO.hideFlags = HideFlags.HideAndDontSave;
        previewSource = previewGO.AddComponent<AudioSource>();
        EditorApplication.update += Tick;
    }
    void OnDisable() { EditorApplication.update -= Tick; if (previewSource) previewSource.Stop(); if (previewGO) DestroyImmediate(previewGO); }
    void Tick()
    {
        if (isPlaying && level && level.music && previewSource)
        {
            previewTime = (float)(AudioSettings.dspTime - dspStart);
            if (previewTime > level.music.length) { isPlaying = false; previewSource.Stop(); }
            Repaint();
        }
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawLevelSelector();
        if (level == null) { DrawEmptyState(); return; }

        DrawHeader();
        DrawTimeline();
        DrawEvents();
        DrawFooterHelp();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button(new GUIContent(" + Создать Level", "Создать новый LevelData asset"), EditorStyles.toolbarButton, GUILayout.Width(150))) CreateAsset();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(new GUIContent("Rhythm Parkour — Редактор уровней", "Каждый уровень = музыка + BPM + список нот"), EditorStyles.miniLabel, GUILayout.Width(240));
            if (GUILayout.Button(new GUIContent("Док", "Открыть видео-гайд"), EditorStyles.toolbarButton, GUILayout.Width(50))) Application.OpenURL("https://www.youtube.com/watch?v=Oh_trUKWDTg");
        }
    }

    void DrawLevelSelector()
    {
        EditorGUILayout.Space(4);
        var newLevel = (RhythmLevelData)EditorGUILayout.ObjectField(new GUIContent(" Текущий Level", "Перетащи сюда .asset уровня"), level, typeof(RhythmLevelData), false);
        if (newLevel != level) { level = newLevel; BuildList(); }
        if (level == null) return;

        // статус-бар
        string info = $"{level.events.Count} нот";
        if (level.music != null) info += $" • {level.music.length:0.0}с • BPM {level.bpm:0} • {level.TimeToBeat(level.music.length):0} битов";
        else info += " • нет музыки";
        EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
        if (level.obstaclePrefabs.Count == 0) EditorGUILayout.HelpBox("Палитра пуста — перетащи префабы препятствий в секцию 2.", MessageType.Warning);
        if (level.music == null) EditorGUILayout.HelpBox("Назначь Music в секции 1 — без трека таймлайн и автогенерация не работают.", MessageType.Warning);
    }

    void DrawEmptyState()
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.HelpBox("Выбери LevelData выше, или нажми '+ Создать Level'.\n\nБыстрый старт:\n1. Создай Level\n2. Перетащи mp3 в Music\n3. Поставь BPM (напр. 82 для SWIPE)\n4. Перетащи 7 префабов из Assets/!Rhythm Parkour/Prefabs/Obstacles\n5. Нажми ★ Авто-постройка ★", MessageType.Info);
        if (GUILayout.Button("Создать первый уровень", GUILayout.Height(36))) CreateAsset();
    }

    void DrawHeader()
    {
        // ── Секция 1: Музыка и темп ──
        foldMusic = EditorGUILayout.Foldout(foldMusic, "1 — Музыка и темп  (обязательно)", true, EditorStyles.foldoutHeader);
        if (foldMusic)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("BPM = ударов в минуту. Offset = задержка первого бита (сек). Если не знаешь BPM — поставь 120 и подбери на слух по таймлайну (сильные доли = белые линии каждые 4 бита).", MessageType.None);
            EditorGUI.BeginChangeCheck();
            var music = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Music (mp3/wav)", "Перетащи AudioClip сюда. Должен быть Readable"), level.music, typeof(AudioClip), false);
            var video = (VideoClip)EditorGUILayout.ObjectField(new GUIContent("Video (опц.)", "Фоновое видео уровня"), level.video, typeof(VideoClip), false);
            float bpm = EditorGUILayout.FloatField(new GUIContent("BPM", "Темп трека. Влияет на сетку 0.5 бита. Неверный BPM = ноты мимо музыки"), level.bpm);
            float offset = EditorGUILayout.FloatField(new GUIContent("Offset (сек)", "Сдвиг сетки относительно начала трека. 0 = первый бит в 0с"), level.offset);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Header");
                level.music = music; level.video = video;
                level.bpm = Mathf.Max(1, bpm); level.offset = offset;
                EditorUtility.SetDirty(level);
                BuildList();
            }
            if (level.bpm < 60 || level.bpm > 220) EditorGUILayout.HelpBox($"BPM {level.bpm} выглядит подозрительно. Обычно 70-180.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        // ── Секция 2: Палитра ──
        foldPalette = EditorGUILayout.Foldout(foldPalette, "2 — Палитра препятствий  (перетащи префабы)", true, EditorStyles.foldoutHeader);
        if (foldPalette)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Сюда перетащи префабы из Prefabs/Obstacles. Порядок = индекс в списке нот. Каждый префаб уже центрирован (0,0,0) и имеет свою baseSpeed.", EditorStyles.wordWrappedMiniLabel);
            SerializedObject so = new SerializedObject(level);
            EditorGUILayout.PropertyField(so.FindProperty("obstaclePrefabs"), new GUIContent("Список префабов"), true);
            so.ApplyModifiedProperties();

            // подсказка по палитре
            if (level.obstaclePrefabs.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Что в палитре:", EditorStyles.miniBoldLabel);
                for (int i = 0; i < level.obstaclePrefabs.Count; i++)
                {
                    var go = level.obstaclePrefabs[i];
                    if (go == null) continue;
                    var ob = go.GetComponent<Obstacle>();
                    string spd = ob ? $"{ob.baseSpeed:0.0} м/с" : "нет Obstacle";
                    Color c = GetColor(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($" {i}: {go.name}", GUILayout.Width(200));
                        // цветной квадратик
                        Rect r = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                        EditorGUI.DrawRect(r, c);
                        EditorGUILayout.LabelField($"скорость {spd}", EditorStyles.miniLabel, GUILayout.Width(120));
                        if (ob) EditorGUILayout.LabelField($"шкала {go.transform.localScale.x:0}x", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.HelpBox("Цвета на таймлайне = цвет префаба (по индексу). Скорость можно переопределить в каждой ноте.", MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Sort по времени", "Отсортировать ноты по time"), GUILayout.Height(22))) { Undo.RecordObject(level, "Sort"); level.SortByTime(); EditorUtility.SetDirty(level); BuildList(); }
                if (GUILayout.Button(new GUIContent("Beats→Time", "Пересчитать time = BeatToTime(beat) по BPM"), GUILayout.Height(22))) { Undo.RecordObject(level, "Sync"); level.SyncBeatsToTime(); EditorUtility.SetDirty(level); }
                if (GUILayout.Button(new GUIContent("Time→Beats", "Пересчитать beat = TimeToBeat(time)"), GUILayout.Height(22))) { Undo.RecordObject(level, "Sync"); level.SyncTimeToBeats(); EditorUtility.SetDirty(level); }
                if (GUILayout.Button(new GUIContent("Норм. скорость", "Заполнить пустые speed (0) значением 12"), GUILayout.Height(22))) { Undo.RecordObject(level, "Speeds"); for (int i = 0; i < level.events.Count; i++) { var e = level.events[i]; if (e.speed < 0.1f) e.speed = 12f; level.events[i] = e; } EditorUtility.SetDirty(level); BuildList(); }
            }
            EditorGUILayout.EndVertical();
        }

        // ── Секция 3: Авто-генерация ──
        foldAuto = EditorGUILayout.Foldout(foldAuto, "3 — Авто-генерация по музыке  (1 кнопка)", true, EditorStyles.foldoutHeader);
        if (foldAuto)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("Анализирует громкость (flux+RMS+high), ставит ноты только на сетку 0.5 бита (квантизация под BPM). Сильный пик = быстрый префаб, слабый = медленный. После — заполняет большие паузы (>4 бита).", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                genDensity = EditorGUILayout.Slider(new GUIContent("Плотность", "0.2=редко, 1.0=каждый пик. 0.72 = сбалансировано"), genDensity, 0.2f, 1f);
                genThreshold = EditorGUILayout.Slider(new GUIContent("Порог", "Выше = меньше нот (строже). 0.24 = норма"), genThreshold, 0.1f, 0.6f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                genMinSpeed = EditorGUILayout.FloatField(new GUIContent("Мин скорость", "Самая медленная нота (м/с). Движется по дорожке, Y лок"), genMinSpeed);
                genMaxSpeed = EditorGUILayout.FloatField(new GUIContent("Макс скорость", "Самая быстрая нота. Сильные пики получают макс"), genMaxSpeed);
            }
            // превью количества
            if (level.music != null)
            {
                float estBeats = level.TimeToBeat(level.music.length);
                int estMin = Mathf.RoundToInt(estBeats * 0.35f);
                EditorGUILayout.LabelField($"Ожидается ~{estMin}–{Mathf.RoundToInt(estMin*1.4f)} нот (зависит от музыки). Сейчас: {level.events.Count}.", EditorStyles.miniLabel);
            }

            // большая зелёная кнопка
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            using (new EditorGUI.DisabledScope(level.music == null || level.obstaclePrefabs.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("★  АВТО-ПОСТРОЙКА УРОВНЯ ПО АУДИО  ★", "Удалит все старые ноты и создаст новые по анализу трека"), GUILayout.Height(34)))
                {
                    if (level.music == null) EditorUtility.DisplayDialog("Нет музыки", "Сначала назначь Music в секции 1", "Ок");
                    else if (level.obstaclePrefabs.Count == 0) EditorUtility.DisplayDialog("Нет префабов", "Сначала добавь префабы в секцию 2", "Ок");
                    else if (EditorUtility.DisplayDialog("Авто-генерация", $"Удалить {level.events.Count} старых нот и сгенерить новые?\n\nBPM={level.bpm} • Длина {(level.music? level.music.length:0):0.0}с\nСетка 0.5 бита, плотность {genDensity:0.00}, порог {genThreshold:0.00}\nКаждой ноте — своя скорость {genMinSpeed:0}-{genMaxSpeed:0} м/с", "Да, генерировать", "Отмена"))
                    {
                        Undo.RecordObject(level, "AutoGenerate");
                        RhythmAutoGenerator.Generate(level, 0, genDensity, genThreshold, genMinSpeed, genMaxSpeed);
                        BuildList();
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            if (level.events.Count > 0)
            {
                float minS = float.MaxValue, maxS = float.MinValue, avgS = 0f;
                foreach (var e in level.events) { if (e.speed < minS) minS = e.speed; if (e.speed > maxS) maxS = e.speed; avgS += e.speed; }
                avgS /= Mathf.Max(1, level.events.Count);
                EditorGUILayout.LabelField($"Скорости в уровне:  min {minS:0.0}  •  avg {avgS:0.0}  •  max {maxS:0.0}  м/с", EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox("Совет: если нот слишком мало — снизь Порог или повысь Плотность. Если слишком много — наоборот. BPM должен быть верным!", MessageType.Info);
            EditorGUILayout.EndVertical();
        }
    }

    void BuildList()
    {
        if (level == null) return;
        SerializedObject so = new SerializedObject(level);
        var prop = so.FindProperty("events");
        eventList = new ReorderableList(so, prop, true, true, true, true);
        eventList.drawHeaderCallback = (r) =>
        {
            // заголовок с подсказками
            EditorGUI.LabelField(r, $"Ноты — {level.events.Count} шт.   |   beat (такт)  |  time (сек)  |  префаб  |  speed  |  цвет");
            // подсказка при наведении
            if (r.Contains(Event.current.mousePosition))
                GUI.Label(r, new GUIContent("", "beat = позиция в музыке (0.5 = пол-бита). time = beat*60/BPM+offset. speed = индив. скорость по дорожке."));
        };
        eventList.drawElementCallback = (r, idx, active, focused) =>
        {
            var e = prop.GetArrayElementAtIndex(idx);
            r.height = 20; r.y += 2;
            float w = r.width;
            float beatW = 52, timeW = 56, prefW = 96, spdW = 52;
            var beatProp = e.FindPropertyRelative("beat");
            var timeProp = e.FindPropertyRelative("time");
            var prefProp = e.FindPropertyRelative("prefabIndex");
            var speedProp = e.FindPropertyRelative("speed");

            // beat
            EditorGUI.BeginChangeCheck();
            float newBeat = EditorGUI.FloatField(new Rect(r.x, r.y, beatW, 18), new GUIContent(beatProp.floatValue.ToString("0.##"), "Бит — привязан к сетке 0.5. 4 бита = 1 такт"), beatProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Beat");
                beatProp.floatValue = newBeat;
                timeProp.floatValue = level.BeatToTime(newBeat);
                so.ApplyModifiedProperties(); EditorUtility.SetDirty(level);
            }
            // time
            float newTime = EditorGUI.FloatField(new Rect(r.x + beatW + 4, r.y, timeW, 18), timeProp.floatValue);
            if (!Mathf.Approximately(newTime, timeProp.floatValue))
            {
                Undo.RecordObject(level, "Edit Time");
                timeProp.floatValue = newTime;
                beatProp.floatValue = level.TimeToBeat(newTime);
                so.ApplyModifiedProperties(); EditorUtility.SetDirty(level);
            }

            string[] names = GetNames(); int[] idxs = GetIndices();
            int cur = prefProp.intValue;
            int newIdx = EditorGUI.IntPopup(new Rect(r.x + beatW + timeW + 8, r.y, prefW, 18), cur, names, idxs);
            if (newIdx != cur) { prefProp.intValue = newIdx; so.ApplyModifiedProperties(); EditorUtility.SetDirty(level); }

            float newSpd = EditorGUI.FloatField(new Rect(r.x + beatW + timeW + prefW + 12, r.y, spdW, 18), speedProp.floatValue);
            if (!Mathf.Approximately(newSpd, speedProp.floatValue))
            {
                Undo.RecordObject(level, "Edit Speed");
                speedProp.floatValue = Mathf.Max(0f, newSpd);
                so.ApplyModifiedProperties(); EditorUtility.SetDirty(level);
            }

            // цветной индикатор
            Color c = GetColor(prefProp.intValue);
            EditorGUI.DrawRect(new Rect(r.x + w - 14, r.y, 10, 18), c);
            // маленькая подпись speed цветом
            if (speedProp.floatValue > 18f) GUI.Label(new Rect(r.x + w - 28, r.y, 12, 18), ">>", EditorStyles.miniLabel);
        };
        eventList.onAddCallback = (l) =>
        {
            Undo.RecordObject(level, "Add");
            float beat = 0; if (level.events.Count > 0) beat = level.events[level.events.Count - 1].beat + 2f;
            var ev = ObstacleEvent.Create(beat, 0, Vector3.zero, 12f);
            ev.time = level.BeatToTime(beat);
            if (level.obstaclePrefabs.Count > 0 && level.obstaclePrefabs[0] != null)
            {
                var ob = level.obstaclePrefabs[0].GetComponent<Obstacle>();
                if (ob != null) ev.speed = ob.baseSpeed;
            }
            level.events.Add(ev);
            EditorUtility.SetDirty(level);
        };
        eventList.onRemoveCallback = (l) =>
        {
            if (l.index < 0 || l.index >= level.events.Count) return;
            Undo.RecordObject(level, "Remove Note");
            // удаление через SerializedProperty чтобы корректно работал Undo/Redo
            var sp = l.serializedProperty;
            sp.DeleteArrayElementAtIndex(l.index);
            l.serializedProperty.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(level);
            l.index = Mathf.Clamp(l.index - 1, 0, level.events.Count - 1);
            ShowNotification(new GUIContent("Нота удалена (Ctrl+Z — отменить)"));
        };
        eventList.onReorderCallback = (l) =>
        {
            // порядок меняется перетаскиванием — сразу сортировать нельзя, но помечаем dirty
            EditorUtility.SetDirty(level);
        };
        eventList.elementHeight = 24;
        eventList.footerHeight = 18;
    }

    void EnsureWaveform()
    {
        if (level == null || level.music == null) return;
        if (waveformCache != null && waveformClip == level.music && waveformFreq == level.music.frequency) return;
        var clip = level.music;
        int ch = clip.channels;
        int samples = clip.samples;
        if (samples <= 0) { waveformCache = new float[1]; return; }
        // берём не весь массив для скорости — 1 семпл на ~4
        int step = Mathf.Max(1, samples / 8192);
        int count = samples / step;
        float[] data = new float[count * ch];
        // GetData с offset 0 может быть тяжёлым — берём с шагом через GetData блоками нельзя, поэтому берём весь но с прореживанием
        float[] all = new float[samples * ch];
        clip.GetData(all, 0);
        waveformCache = new float[count];
        float max = 0.001f;
        for (int i = 0; i < count; i++)
        {
            float sum = 0;
            for (int c = 0; c < ch; c++) sum += Mathf.Abs(all[(i * step) * ch + c]);
            sum /= ch;
            waveformCache[i] = sum;
            if (sum > max) max = sum;
        }
        for (int i = 0; i < count; i++) waveformCache[i] /= max;
        waveformClip = clip;
        waveformFreq = clip.frequency;
    }

    void DrawTimeline()
    {
        if (level == null || level.music == null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("Таймлайн появится когда назначишь Music в секции 1.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }
        EnsureWaveform();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent("4 — Таймлайн  (перетаскивай ноты мышкой, скролл — колесо, ПКМ — удалить)", "Зелёная линия = Playhead. Волна = громкость трека"), EditorStyles.boldLabel);

        // верхняя панель управления таймлайном
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = level.music != null;
            if (!isPlaying) {
                if (GUILayout.Button(new GUIContent("▶ Play", "С начала (0с)"), GUILayout.Width(62))) Play(0f);
                if (GUILayout.Button(new GUIContent("▶ С курсора", "С текущего playhead"), GUILayout.Width(88))) Play(previewTime);
            }
            else if (GUILayout.Button(new GUIContent("■ Stop", "Стоп превью"), GUILayout.Width(62))) Stop();
            GUI.enabled = true;
            GUILayout.Label($" {previewTime:0.00}с • біт {level.TimeToBeat(previewTime):0.0}", GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            snapHalf = EditorGUILayout.ToggleLeft(new GUIContent("Сетка 0.5", "Вкл=шаг 0.5 бита, выкл=1 бит"), snapHalf, GUILayout.Width(90));
            showWaveform = EditorGUILayout.ToggleLeft(new GUIContent("Волна", "Показать форму трека"), showWaveform, GUILayout.Width(70));
            zoom = EditorGUILayout.Slider(new GUIContent("Zoom", "Ctrl+колесо тоже"), zoom, 0.35f, 4f, GUILayout.Width(150));
            if (GUILayout.Button("⌖", GUILayout.Width(24))) { // центрировать на playhead
                float beat = level.TimeToBeat(previewTime);
                float viewW = position.width - 32f;
                float totalBeats = level.TimeToBeat(level.music.length) + 8;
                float totalW = totalBeats * 28f * zoom;
                timelineScroll.x = Mathf.Clamp(beat * 28f * zoom - viewW * 0.5f, 0, Mathf.Max(0, totalW - viewW));
            }
        }

        // легенда + быстрая смена префаба для выбранной ноты
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Легенда:", GUILayout.Width(52));
            for (int i = 0; i < Mathf.Min(level.obstaclePrefabs.Count, 7); i++)
            {
                Rect cr = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(cr, GetColor(i));
                string n = level.obstaclePrefabs[i] ? level.obstaclePrefabs[i].name : $"#{i}";
                // клик по легенде — назначить выбранной ноте этот префаб
                if (Event.current.type == EventType.MouseDown && cr.Contains(Event.current.mousePosition) && eventList != null && eventList.index >= 0)
                {
                    Undo.RecordObject(level, "Change Prefab");
                    var ev = level.events[eventList.index];
                    ev.prefabIndex = i;
                    level.events[eventList.index] = ev;
                    EditorUtility.SetDirty(level);
                    BuildList();
                    Event.current.Use();
                }
                EditorGUILayout.LabelField($"{i}:{n}", EditorStyles.miniLabel, GUILayout.Width(84));
            }
            if (level.obstaclePrefabs.Count == 0) EditorGUILayout.LabelField("— пусто —", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (eventList != null && eventList.index >= 0 && eventList.index < level.events.Count)
            {
                var sel = level.events[eventList.index];
                EditorGUILayout.LabelField($"выбрано: бит {sel.beat:0.##} • {sel.speed:0.0}м/с • Del=удалить, ←→=сдвиг", EditorStyles.miniLabel);
            }
        }

        float viewW2 = position.width - 32f;
        float totalBeats2 = level.TimeToBeat(level.music.length) + 8;
        float totalW2 = totalBeats2 * 28f * zoom;
        Rect r = GUILayoutUtility.GetRect(viewW2, 96, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.15f, 1f));
        Event e = Event.current;
        float startX = r.x - timelineScroll.x;

        // фон-волна
        if (showWaveform && waveformCache != null && waveformCache.Length > 4)
        {
            // рисуем полупрозрачную заливку по всей ширине
            int cols = Mathf.RoundToInt(r.width);
            float step = (float)waveformCache.Length / Mathf.Max(1, totalW2);
            for (int px = 0; px < cols; px++)
            {
                float wx = r.x + px;
                if (wx < r.x || wx > r.x + r.width) continue;
                float beatPos = (px + timelineScroll.x) / (28f * zoom);
                // сэмпл волны
                int idx = Mathf.Clamp(Mathf.RoundToInt((px + timelineScroll.x) / totalW2 * waveformCache.Length), 0, waveformCache.Length - 1);
                float amp = waveformCache[idx];
                float h = amp * r.height * 0.42f;
                Rect wr = new Rect(wx, r.y + r.height * 0.5f - h * 0.5f, 1, h);
                EditorGUI.DrawRect(wr, new Color(0.3f, 0.7f, 1f, 0.18f));
            }
            // тонкая линия центра
            EditorGUI.DrawRect(new Rect(r.x, r.y + r.height * 0.5f, r.width, 1), new Color(0.3f, 0.7f, 1f, 0.08f));
        }

        // сетка: линейка битов
        Rect ruler = new Rect(r.x, r.y, r.width, 16);
        EditorGUI.DrawRect(ruler, new Color(0.2f, 0.2f, 0.2f, 1f));
        for (int b = 0; b < totalBeats2; b++)
        {
            float x = startX + b * 28f * zoom; if (x < r.x - 4 || x > r.x + r.width + 4) continue;
            bool bar = b % 4 == 0;
            bool beat = (b % 1 == 0 && !bar);
            Color col = bar ? new Color(1, 1, 1, 0.26f) : beat ? new Color(1, 1, 1, 0.14f) : new Color(1, 1, 1, 0.06f);
            float h = bar ? r.height : beat ? r.height * 0.62f : r.height * 0.35f;
            float y0 = bar ? ruler.y : r.y + r.height - h;
            EditorGUI.DrawRect(new Rect(x, y0, bar ? 2 : 1, h), col);
            if (bar)
            {
                GUI.Label(new Rect(x + 3, ruler.y + 1, 36, 12), b.ToString(), EditorStyles.miniLabel);
                // секунда под битом
                float sec = level.BeatToTime(b);
                GUI.Label(new Rect(x + 3, r.y + r.height - 11, 36, 10), $"{sec:0.0}с", new GUIStyle(EditorStyles.miniLabel){fontSize=8, normal=new GUIStyleState{textColor=new Color(1,1,1,0.45f)}});
            }
            else if (beat && zoom > 0.9f) GUI.Label(new Rect(x + 2, ruler.y + 1, 20, 10), ".0", EditorStyles.miniLabel);
            else if (!beat && snapHalf && zoom > 1.4f) GUI.Label(new Rect(x + 1, ruler.y + 1, 14, 10), ".5", EditorStyles.miniLabel);
        }

        // ховер-инфо и ноты
        hoverIndex = -1;
        for (int i = 0; i < level.events.Count; i++)
        {
            var ev = level.events[i]; float x = startX + ev.beat * 28f * zoom; if (x < r.x - 14 || x > r.x + r.width + 14) continue;
            float h = Mathf.Clamp(14 + (ev.speed - 10f) * 1.3f, 12, 38);
            float y = r.y + 20 + (r.height - 20) * 0.5f - h * 0.5f + 6;
            Rect nr = new Rect(x - 7, y, 14, h);
            Color c = GetColor(ev.prefabIndex);
            bool isHover = nr.Contains(e.mousePosition);
            bool isSel = eventList != null && eventList.index == i;
            bool isDragged = draggedIndex == i;
            if (isHover) hoverIndex = i;
            // тень/обводка
            if (isSel) EditorGUI.DrawRect(new Rect(nr.x-2, nr.y-2, nr.width+4, nr.height+4), Color.white);
            else if (isHover) EditorGUI.DrawRect(new Rect(nr.x-1, nr.y-1, nr.width+2, nr.height+2), new Color(1,1,1,0.6f));
            if (isDragged) c = Color.Lerp(c, Color.white, 0.35f);
            EditorGUI.DrawRect(nr, c);
            // внутренняя полоска скорости
            EditorGUI.DrawRect(new Rect(nr.x, nr.y + nr.height - 3, nr.width, 2), new Color(0,0,0,0.35f));
            if (h > 18) GUI.Label(new Rect(nr.x-4, nr.y + h*0.5f - 6, 22, 12), $"{ev.speed:0}", new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter, fontSize=8});
            // лейбл префаба над блоком при ховере/выборе
            if (isHover || isSel)
            {
                string tip = $"{ev.beat:0.##}б • {ev.time:0.00}с • #{ev.prefabIndex} • {ev.speed:0.0}м/с";
                GUI.Label(new Rect(nr.x - 30, nr.y - 14, 74, 12), tip, new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter, fontSize=8, normal=new GUIStyleState{textColor=new Color(1,1,1,0.85f)}});
            }
        }

        // плейхед
        float playBeat = level.TimeToBeat(previewTime);
        float cx = startX + playBeat * 28f * zoom;
        // зона захвата плейхеда
        Rect headHit = new Rect(cx - 6, r.y, 12, r.height);
        bool hoverHead = headHit.Contains(e.mousePosition);
        Color headCol = hoverHead || isScrubbing ? new Color(0.2f, 1f, 0.6f, 1f) : new Color(0, 1, 0.5f, 0.9f);
        if (cx >= r.x && cx <= r.x + r.width)
        {
            EditorGUI.DrawRect(new Rect(cx, r.y, 2, r.height), headCol);
            // треугольник сверху
            Vector3[] tri = new Vector3[]{ new Vector3(cx-6, r.y,0), new Vector3(cx+6, r.y,0), new Vector3(cx, r.y+7,0)};
            Handles.color = headCol; Handles.DrawAAConvexPolygon(tri);
        }
        if (isPlaying)
        {
            float target = playBeat * 28f * zoom - viewW2 * 0.38f; timelineScroll.x = Mathf.Lerp(timelineScroll.x, Mathf.Clamp(target, 0, Mathf.Max(0, totalW2 - viewW2)), 0.08f); Repaint();
        }

        // ── события мыши ──
        // колесо — zoom / скролл
        if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
        {
            if (e.control || e.command)
            {
                float old = zoom;
                zoom = Mathf.Clamp(zoom - e.delta.y * 0.06f, 0.35f, 4f);
                // zoom к позиции мыши
                float mx = e.mousePosition.x - r.x + timelineScroll.x;
                float beatAtMouse = mx / (28f * old);
                timelineScroll.x = beatAtMouse * 28f * zoom - (e.mousePosition.x - r.x);
                timelineScroll.x = Mathf.Clamp(timelineScroll.x, 0, Mathf.Max(0, totalW2 - viewW2));
                e.Use(); Repaint();
            }
            else
            {
                timelineScroll.x = Mathf.Clamp(timelineScroll.x + e.delta.y * 12f, 0, Mathf.Max(0, totalW2 - viewW2));
                e.Use(); Repaint();
            }
        }
        // зажатие средней кнопки — панорамирование
        if (e.type == EventType.MouseDrag && e.button == 2 && r.Contains(e.mousePosition))
        {
            timelineScroll.x = Mathf.Clamp(timelineScroll.x - e.delta.x, 0, Mathf.Max(0, totalW2 - viewW2));
            e.Use(); Repaint();
        }

        if (e.type == EventType.MouseDown)
        {
            if (e.button == 0)
            {
                // клик по плейхеду — скраб
                if (headHit.Contains(e.mousePosition))
                {
                    isScrubbing = true;
                    float beat = Mathf.Clamp((e.mousePosition.x - startX) / (28f * zoom), 0, totalBeats2);
                    beat = snapHalf ? Mathf.Round(beat * 2f) / 2f : Mathf.Round(beat);
                    previewTime = level.BeatToTime(beat);
                    if (previewSource) previewSource.time = Mathf.Clamp(previewTime, 0, level.music.length - 0.05f);
                    e.Use(); Repaint();
                }
                else {
                    // клик по ноте — выбор + старт перетаскивания
                    bool hitNote = false;
                    for (int i = 0; i < level.events.Count; i++)
                    {
                        var ev = level.events[i]; float x = startX + ev.beat * 28f * zoom;
                        float h = Mathf.Clamp(14 + (ev.speed - 10f) * 1.3f, 12, 38);
                        float y = r.y + 20 + (r.height - 20) * 0.5f - h * 0.5f + 6;
                        Rect nr = new Rect(x - 7, y, 14, h);
                        if (nr.Contains(e.mousePosition))
                        {
                            eventList.index = i;
                            draggedIndex = i;
                            dragStartBeat = ev.beat;
                            dragStartMouse = e.mousePosition;
                            hitNote = true;
                            e.Use(); Repaint();
                            break;
                        }
                    }
                    if (!hitNote && r.Contains(e.mousePosition))
                    {
                        // клик по пустому — добавить ноту (если не скраб)
                        if (e.clickCount == 2)
                        {
                            // двойной клик — быстрый плей с этого места
                            float beat = (e.mousePosition.x - startX) / (28f * zoom);
                            beat = snapHalf ? Mathf.Round(beat * 2f) / 2f : Mathf.Round(beat);
                            Play(level.BeatToTime(Mathf.Max(0, beat)));
                            e.Use();
                        }
                        else if (!isScrubbing)
                        {
                            // одиночный клик по пустому — добавить (с задержкой, чтобы не мешать драгу)
                            // добавим сразу, но с возможностью отмены перетаскиванием
                            float beat = (e.mousePosition.x - startX) / (28f * zoom);
                            beat = snapHalf ? Mathf.Round(beat * 2f) / 2f : Mathf.Round(beat);
                            if (beat >= 0 && beat <= totalBeats2)
                            {
                                // проверка что не попали в существующую (уже проверено hitNote)
                                Undo.RecordObject(level, "Add");
                                var ev = ObstacleEvent.Create(Mathf.Max(0, beat), 0, Vector3.zero, 12f);
                                ev.time = level.BeatToTime(beat);
                                if (level.obstaclePrefabs.Count > 0 && level.obstaclePrefabs[0] != null) { var ob = level.obstaclePrefabs[0].GetComponent<Obstacle>(); if (ob) ev.speed = ob.baseSpeed; }
                                level.events.Add(ev); level.SortByTime(); EditorUtility.SetDirty(level); BuildList();
                                // сразу выбрать новую
                                for (int k = 0; k < level.events.Count; k++) if (Mathf.Abs(level.events[k].beat - beat) < 0.01f) { eventList.index = k; draggedIndex = k; dragStartBeat = beat; dragStartMouse = e.mousePosition; break; }
                                ShowNotification(new GUIContent($"+ нота {beat:0.##}б"));
                                e.Use(); Repaint();
                            }
                        }
                    }
                }
            }
            else if (e.button == 1) // ПКМ — контекстное меню
            {
                for (int i = 0; i < level.events.Count; i++)
                {
                    var ev = level.events[i]; float x = startX + ev.beat * 28f * zoom;
                    float h = Mathf.Clamp(14 + (ev.speed - 10f) * 1.3f, 12, 38);
                    float y = r.y + 20 + (r.height - 20) * 0.5f - h * 0.5f + 6;
                    Rect nr = new Rect(x - 7, y, 14, h);
                    if (nr.Contains(e.mousePosition))
                    {
                        eventList.index = i;
                        GenericMenu menu = new GenericMenu();
                        int idx = i;
                        menu.AddItem(new GUIContent("Удалить (Del)"), false, ()=>{ Undo.RecordObject(level,"Remove"); level.events.RemoveAt(idx); EditorUtility.SetDirty(level); BuildList(); });
                        menu.AddItem(new GUIContent("Дублировать (D)"), false, ()=>{ Undo.RecordObject(level,"Duplicate"); var c=level.events[idx]; c.beat+= snapHalf?0.5f:1f; c.time=level.BeatToTime(c.beat); level.events.Add(c); level.SortByTime(); EditorUtility.SetDirty(level); BuildList();});
                        menu.AddSeparator("");
                        for (int p=0;p<level.obstaclePrefabs.Count;p++) { int pp=p; string n=level.obstaclePrefabs[p]?level.obstaclePrefabs[p].name:$"#{p}"; menu.AddItem(new GUIContent($"Префаб/{pp}: {n}"), ev.prefabIndex==pp, ()=>{ Undo.RecordObject(level,"Change Prefab"); var c=level.events[idx]; c.prefabIndex=pp; level.events[idx]=c; EditorUtility.SetDirty(level);});}
                        menu.ShowAsContext(); e.Use(); break;
                    }
                }
            }
        }
        if (e.type == EventType.MouseUp)
        {
            draggedIndex = -1;
            isScrubbing = false;
        }
        if (e.type == EventType.MouseDrag)
        {
            if (draggedIndex >= 0 && draggedIndex < level.events.Count && e.button == 0)
            {
                float deltaBeats = (e.mousePosition.x - dragStartMouse.x) / (28f * zoom);
                float step = snapHalf ? 0.5f : 1f;
                float newBeat = Mathf.Round((dragStartBeat + deltaBeats) / step) * step;
                newBeat = Mathf.Clamp(newBeat, 0, totalBeats2);
                var ev = level.events[draggedIndex];
                if (!Mathf.Approximately(ev.beat, newBeat))
                {
                    Undo.RecordObject(level, "Drag Note");
                    ev.beat = newBeat;
                    ev.time = level.BeatToTime(newBeat);
                    level.events[draggedIndex] = ev;
                    level.SortByTime();
                    // обновить индекс после сортировки
                    for (int k=0;k<level.events.Count;k++) if (Mathf.Abs(level.events[k].beat - newBeat) < 0.01f && level.events[k].prefabIndex==ev.prefabIndex) { eventList.index=k; draggedIndex=k; break; }
                    EditorUtility.SetDirty(level);
                }
                e.Use(); Repaint();
            }
            else if (isScrubbing)
            {
                float beat = Mathf.Clamp((e.mousePosition.x - startX) / (28f * zoom), 0, totalBeats2);
                beat = snapHalf ? Mathf.Round(beat * 2f) / 2f : Mathf.Round(beat);
                previewTime = level.BeatToTime(beat);
                if (previewSource) previewSource.time = Mathf.Clamp(previewTime, 0, level.music.length - 0.05f);
                e.Use(); Repaint();
            }
        }
        // клавиатура
        if (e.type == EventType.KeyDown && eventList != null && eventList.index >=0)
        {
            bool handled=false;
            if (e.keyCode==KeyCode.Delete || e.keyCode==KeyCode.Backspace) { Undo.RecordObject(level,"Remove"); level.events.RemoveAt(eventList.index); EditorUtility.SetDirty(level); BuildList(); handled=true; }
            else if (e.keyCode==KeyCode.D && e.control) { Undo.RecordObject(level,"Duplicate"); var c=level.events[eventList.index]; c.beat+= snapHalf?0.5f:1f; c.time=level.BeatToTime(c.beat); level.events.Add(c); level.SortByTime(); EditorUtility.SetDirty(level); BuildList(); handled=true; }
            else if (e.keyCode==KeyCode.LeftArrow) { Undo.RecordObject(level,"Nudge"); var c=level.events[eventList.index]; c.beat-= snapHalf?0.5f:1f; c.beat=Mathf.Max(0,c.beat); c.time=level.BeatToTime(c.beat); level.events[eventList.index]=c; level.SortByTime(); EditorUtility.SetDirty(level); handled=true; }
            else if (e.keyCode==KeyCode.RightArrow) { Undo.RecordObject(level,"Nudge"); var c=level.events[eventList.index]; c.beat+= snapHalf?0.5f:1f; c.time=level.BeatToTime(c.beat); level.events[eventList.index]=c; level.SortByTime(); EditorUtility.SetDirty(level); handled=true; }
            if (handled) { e.Use(); Repaint(); }
        }

        // нижняя полоса прокрутки
        EditorGUILayout.BeginHorizontal(); float ns = GUILayout.HorizontalScrollbar(timelineScroll.x, viewW2, 0, totalW2); if (!Mathf.Approximately(ns, timelineScroll.x)) { timelineScroll.x = ns; Repaint(); }
        if (GUILayout.Button(new GUIContent("◀","В начало"), GUILayout.Width(22))) timelineScroll.x = 0;
        if (GUILayout.Button(new GUIContent("▶","В конец"), GUILayout.Width(22))) timelineScroll.x = Mathf.Max(0, totalW2 - viewW2);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Подсказки:  ЛКМ по ноте — тащи (прилипает к сетке)  •  Двойной ЛКМ — Play оттуда  •  ПКМ — меню  •  Del/←→/Ctrl+D  •  Колесо — скролл, Ctrl+колесо — zoom, Средняя кнопка — панорама  •  Клик по легенде — сменить префаб выбранной  •  Клик по плейхеду/линейке — скраб", MessageType.None);
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { if (isPlaying) Stop(); else Play(previewTime); e.Use(); }
        EditorGUILayout.EndVertical();
    }

    void DrawEvents()
    {
        if (level == null) return;
        if (eventList == null) BuildList();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent($"5 — Список нот  ({level.events.Count})", "Каждая строка = одно препятствие. Двойной клик — переименовать. Правый клик/⋮ — меню"), EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Столбцы:  beat = такт (0.5 шаг)  |  time = секунды (auto от BPM)  |  префаб = индекс+цвет  |  speed = м/с по дорожке", EditorStyles.miniLabel);
        if (level.events.Count == 0) EditorGUILayout.HelpBox("Список пуст. Нажми '+' внизу или используй ★ Авто-постройка ★ в секции 3.", MessageType.Info);
        // таблица
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        // шапка колонок
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("beat", EditorStyles.miniLabel, GUILayout.Width(52));
            EditorGUILayout.LabelField("time", EditorStyles.miniLabel, GUILayout.Width(56));
            EditorGUILayout.LabelField("префаб", EditorStyles.miniLabel, GUILayout.Width(96));
            EditorGUILayout.LabelField("speed", EditorStyles.miniLabel, GUILayout.Width(52));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("цвет", EditorStyles.miniLabel, GUILayout.Width(30));
        }
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
        eventList.DoLayoutList();
        EditorGUILayout.EndScrollView();

        // удобные кнопки — работают всегда, не только через «-» внизу списка
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = eventList.index >= 0 && eventList.index < level.events.Count;
            if (GUILayout.Button(new GUIContent("🗑 Удалить (Del)", "Удалить выбранную ноту. Также Del/Backspace когда строка выбрана"), GUILayout.Height(22)))
            {
                int idx = eventList.index;
                Undo.RecordObject(level, "Remove Note");
                level.events.RemoveAt(idx);
                EditorUtility.SetDirty(level);
                eventList.index = Mathf.Clamp(idx - 1, 0, level.events.Count - 1);
                BuildList();
                ShowNotification(new GUIContent("Удалено — Ctrl+Z чтобы вернуть"));
            }
            if (GUILayout.Button(new GUIContent("⎘ Дублировать", "Скопировать выбранную +0.5 бита"), GUILayout.Height(22)))
            {
                int idx = eventList.index;
                Undo.RecordObject(level, "Duplicate");
                var c = level.events[idx];
                c.beat += snapHalf ? 0.5f : 1f;
                c.time = level.BeatToTime(c.beat);
                level.events.Add(c);
                level.SortByTime();
                EditorUtility.SetDirty(level);
                BuildList();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Очистить всё", "Удалить все ноты (с подтверждением)"), GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("Очистить?", $"Удалить все {level.events.Count} нот? Отменить можно Ctrl+Z.", "Да", "Отмена"))
                {
                    Undo.RecordObject(level, "Clear All");
                    level.events.Clear();
                    EditorUtility.SetDirty(level);
                    BuildList();
                }
            }
        }

        // Delete/Backspace работает даже без фокуса на «-» — ловим тут
        var k = Event.current;
        if (k.type == EventType.KeyDown && eventList.index >= 0 && eventList.index < level.events.Count)
        {
            if (k.keyCode == KeyCode.Delete || k.keyCode == KeyCode.Backspace)
            {
                Undo.RecordObject(level, "Remove Note");
                level.events.RemoveAt(eventList.index);
                EditorUtility.SetDirty(level);
                int ni = Mathf.Clamp(eventList.index, 0, level.events.Count - 1);
                BuildList();
                eventList.index = ni;
                k.Use(); Repaint();
                ShowNotification(new GUIContent("Удалено"));
            }
        }

        EditorGUILayout.HelpBox("Удалить: выдели строку → Del/Backspace, или кнопка 🗑, или «-» внизу, или ПКМ → Удалить. Перетаскивай строки, тащи ноты на таймлайне, меняй префаб кликом по легенде.", MessageType.None);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    void DrawFooterHelp()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Как это работает?", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("• Дорожка = объект Ground (6м ширина). Препятствия спавнятся в SpawnPoint (зелёный куб) и едут по -Z к DespawnPoint (красный). X клампится, Y фиксируется — за пределы не выйдут.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("• Префабы уже центрированы в (0,0,0). При перетаскивании префаба на SpawnPoint он встанет ровно.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("• Автогенерация ставит ноты на пики громкости, квантует к 0.5 бита. Плотность/Порог регулируют количество.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    string[] GetNames() { if (level.obstaclePrefabs == null || level.obstaclePrefabs.Count == 0) return new[] { "0: empty" }; var a = new string[level.obstaclePrefabs.Count]; for (int i = 0; i < a.Length; i++) a[i] = $"{i}: {(level.obstaclePrefabs[i] ? level.obstaclePrefabs[i].name : "null")}"; return a; }
    int[] GetIndices() { if (level.obstaclePrefabs == null || level.obstaclePrefabs.Count == 0) return new[] { 0 }; var a = new int[level.obstaclePrefabs.Count]; for (int i = 0; i < a.Length; i++) a[i] = i; return a; }
    Color GetColor(int idx) { float h = (idx * 0.37f) % 1f; return Color.HSVToRGB(h, 0.75f, 0.9f); }
    void Play(float t) { if (level.music == null || previewSource == null) return; previewSource.clip = level.music; previewSource.time = Mathf.Clamp(t, 0, level.music.length - 0.1f); dspStart = AudioSettings.dspTime - previewSource.time; previewSource.Play(); isPlaying = true; previewTime = previewSource.time; }
    void Stop() { isPlaying = false; if (previewSource) previewSource.Stop(); }
}
