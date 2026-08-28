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
    Vector2 scroll;
    float zoom = 1f;

    [MenuItem("Window/Rhythm Parkour/Level Editor")]
    public static void Open() { var w = GetWindow<RhythmLevelEditorWindow>("Rhythm Level"); w.minSize = new Vector2(720, 500); w.Show(); }

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
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Создать Level", EditorStyles.toolbarButton, GUILayout.Width(130))) CreateAsset();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Док", EditorStyles.toolbarButton, GUILayout.Width(50))) Application.OpenURL("https://www.youtube.com/watch?v=Oh_trUKWDTg");
        }

        var newLevel = (RhythmLevelData)EditorGUILayout.ObjectField("Level Data", level, typeof(RhythmLevelData), false);
        if (newLevel != level) { level = newLevel; BuildList(); }
        if (level == null) { EditorGUILayout.HelpBox("Выбери LevelData. Create → Rhythm Parkour → Level Data", MessageType.Info); return; }

        DrawHeader();
        DrawTimeline();
        DrawEvents();
    }

    void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        var music = (AudioClip)EditorGUILayout.ObjectField("Music", level.music, typeof(AudioClip), false);
        var video = (UnityEngine.Video.VideoClip)EditorGUILayout.ObjectField("Video", level.video, typeof(UnityEngine.Video.VideoClip), false);
        float bpm = EditorGUILayout.FloatField("BPM", level.bpm);
        float offset = EditorGUILayout.FloatField("Offset", level.offset);
        if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(level, "Header"); level.music = music; level.video = video; level.bpm = Mathf.Max(1, bpm); level.offset = offset; EditorUtility.SetDirty(level); }

        EditorGUILayout.LabelField("Палитра", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(level);
        EditorGUILayout.PropertyField(so.FindProperty("obstaclePrefabs"), true);
        so.ApplyModifiedProperties();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sort")) { Undo.RecordObject(level, "Sort"); level.SortByTime(); EditorUtility.SetDirty(level); BuildList(); }
            if (GUILayout.Button("Beats→Time")) { Undo.RecordObject(level, "Sync"); level.SyncBeatsToTime(); EditorUtility.SetDirty(level); }
            if (GUILayout.Button("Time→Beats")) { Undo.RecordObject(level, "Sync"); level.SyncTimeToBeats(); EditorUtility.SetDirty(level); }
        }

        // Авто-постройка одной кнопкой — анализ аудио
        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);
        if (GUILayout.Button("★ АВТО-ПОСТРОЙКА УРОВНЯ ПО АУДИО (1 КНОПКА) ★", GUILayout.Height(32)))
        {
            if (level.music == null) { EditorUtility.DisplayDialog("Нет музыки", "Сначала назначь Music в LevelData", "Ок"); }
            else if (level.obstaclePrefabs.Count == 0) { EditorUtility.DisplayDialog("Нет префабов", "Сначала добавь префабы в палитру", "Ок"); }
            else if (EditorUtility.DisplayDialog("Авто-генерация", $"Удалить {level.events.Count} старых нот и сгенерить новые по анализу аудио?\nBPM={level.bpm} будет использован для квантизации.", "Да", "Отмена"))
            {
                Undo.RecordObject(level, "AutoGenerate");
                RhythmAutoGenerator.Generate(level);
                BuildList();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.HelpBox("Анализирует весь mp3 (GetData), находит пики энергии и расставляет префабы по лейнам -2/0/2. Работает на любом треке.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    void BuildList()
    {
        if (level == null) return;
        SerializedObject so = new SerializedObject(level);
        var prop = so.FindProperty("events");
        eventList = new ReorderableList(so, prop, true, true, true, true);
        eventList.drawHeaderCallback = (r) => EditorGUI.LabelField(r, $"Ноты — {level.events.Count} шт. | ЛКМ по таймлайну чтобы добавить");
        eventList.drawElementCallback = (r, idx, active, focused) =>
        {
            var e = prop.GetArrayElementAtIndex(idx);
            r.height = 20; r.y += 2;
            float w = r.width;
            // beat | time | prefab | X | Y
            float beatW = 55, timeW = 60, prefW = 90, xyW = 45;
            var beatProp = e.FindPropertyRelative("beat");
            var timeProp = e.FindPropertyRelative("time");
            var prefProp = e.FindPropertyRelative("prefabIndex");

            EditorGUI.BeginChangeCheck();
            float newBeat = EditorGUI.FloatField(new Rect(r.x, r.y, beatW, 18), beatProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Beat");
                beatProp.floatValue = newBeat;
                timeProp.floatValue = level.BeatToTime(newBeat);
                so.ApplyModifiedProperties(); EditorUtility.SetDirty(level);
            }
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

            // цвет
            Color c = GetColor(prefProp.intValue);
            EditorGUI.DrawRect(new Rect(r.x + w - 14, r.y, 10, 18), c);
        };
        eventList.onAddCallback = (l) =>
        {
            Undo.RecordObject(level, "Add");
            float beat = 0; if (level.events.Count > 0) beat = level.events[level.events.Count - 1].beat + 2f;
            var ev = ObstacleEvent.Create(beat, 0, Vector3.zero);
            ev.time = level.BeatToTime(beat);
            level.events.Add(ev);
            EditorUtility.SetDirty(level);
        };
        eventList.elementHeight = 24;
    }

    void DrawTimeline()
    {
        if (level == null || level.music == null) { EditorGUILayout.HelpBox("Назначь Music", MessageType.Warning); return; }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = level.music != null;
            if (!isPlaying) { if (GUILayout.Button("▶ Play", GUILayout.Width(70))) Play(0f); if (GUILayout.Button("▶ С курсора", GUILayout.Width(90))) Play(previewTime); }
            else if (GUILayout.Button("■ Stop", GUILayout.Width(70))) Stop();
            GUI.enabled = true;
            GUILayout.Label($"Time {previewTime:0.00}  Beat {level.TimeToBeat(previewTime):0.0}", GUILayout.Width(180));
            zoom = EditorGUILayout.Slider("Zoom", zoom, 0.5f, 3f, GUILayout.Width(170));
        }

        float viewW = position.width - 32f;
        float totalBeats = level.TimeToBeat(level.music.length) + 8;
        float totalW = totalBeats * 28f * zoom;
        Rect r = GUILayoutUtility.GetRect(viewW, 70, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.15f, 1f));
        Event e = Event.current;
        float startX = r.x - scroll.x;

        for (int b = 0; b < totalBeats; b++)
        {
            float x = startX + b * 28f * zoom; if (x < r.x - 5 || x > r.x + r.width + 5) continue;
            bool bar = b % 4 == 0; EditorGUI.DrawRect(new Rect(x, r.y, bar ? 2 : 1, r.height), bar ? new Color(1, 1, 1, 0.22f) : new Color(1, 1, 1, 0.07f));
            if (bar) GUI.Label(new Rect(x + 3, r.y + 2, 30, 12), b.ToString(), EditorStyles.miniLabel);
        }
        for (int i = 0; i < level.events.Count; i++)
        {
            var ev = level.events[i]; float x = startX + ev.beat * 28f * zoom; if (x < r.x - 12 || x > r.x + r.width + 12) continue;
            Rect nr = new Rect(x - 7, r.y + 18, 14, r.height - 26); Color c = GetColor(ev.prefabIndex); EditorGUI.DrawRect(nr, c);
            if (e.type == EventType.MouseDown && e.button == 0 && nr.Contains(e.mousePosition)) { eventList.index = i; e.Use(); Repaint(); }
        }
        if (isPlaying)
        {
            float beat = level.TimeToBeat(previewTime); float cx = startX + beat * 28f * zoom;
            if (cx >= r.x && cx <= r.x + r.width) EditorGUI.DrawRect(new Rect(cx, r.y, 2, r.height), new Color(0, 1, 0.5f, 0.9f));
            float target = beat * 28f * zoom - viewW * 0.4f; scroll.x = Mathf.Lerp(scroll.x, Mathf.Clamp(target, 0, Mathf.Max(0, totalW - viewW)), 0.1f); Repaint();
        }
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition) && !isPlaying)
        {
            bool hit = false; for (int i = 0; i < level.events.Count; i++) { float x = startX + level.events[i].beat * 28f * zoom; if (new Rect(x - 7, r.y + 18, 14, r.height - 26).Contains(e.mousePosition)) { hit = true; break; } }
            if (!hit) { float beat = (e.mousePosition.x - startX) / (28f * zoom); beat = Mathf.Round(beat * 2f) / 2f; Undo.RecordObject(level, "Add"); var ev = ObstacleEvent.Create(Mathf.Max(0, beat), 0, Vector3.zero); ev.time = level.BeatToTime(beat); level.events.Add(ev); level.SortByTime(); EditorUtility.SetDirty(level); BuildList(); e.Use(); }
        }
        EditorGUILayout.BeginHorizontal(); float ns = GUILayout.HorizontalScrollbar(scroll.x, viewW, 0, totalW); if (!Mathf.Approximately(ns, scroll.x)) { scroll.x = ns; Repaint(); } EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("ЛКМ по таймлайну — добавить (0.5 бита). Пробел — Play/Stop.", MessageType.None);
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { if (isPlaying) Stop(); else Play(previewTime); e.Use(); }
        EditorGUILayout.EndVertical();
    }

    void DrawEvents()
    {
        if (level == null) return;
        if (eventList == null) BuildList();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
        eventList.DoLayoutList();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    string[] GetNames() { if (level.obstaclePrefabs == null || level.obstaclePrefabs.Count == 0) return new[] { "0: empty" }; var a = new string[level.obstaclePrefabs.Count]; for (int i = 0; i < a.Length; i++) a[i] = $"{i}: {(level.obstaclePrefabs[i] ? level.obstaclePrefabs[i].name : "null")}"; return a; }
    int[] GetIndices() { if (level.obstaclePrefabs == null || level.obstaclePrefabs.Count == 0) return new[] { 0 }; var a = new int[level.obstaclePrefabs.Count]; for (int i = 0; i < a.Length; i++) a[i] = i; return a; }
    Color GetColor(int idx) { float h = (idx * 0.37f) % 1f; return Color.HSVToRGB(h, 0.75f, 0.9f); }
    void Play(float t) { if (level.music == null || previewSource == null) return; previewSource.clip = level.music; previewSource.time = Mathf.Clamp(t, 0, level.music.length - 0.1f); dspStart = AudioSettings.dspTime - previewSource.time; previewSource.Play(); isPlaying = true; previewTime = previewSource.time; }
    void Stop() { isPlaying = false; if (previewSource) previewSource.Stop(); }
}
