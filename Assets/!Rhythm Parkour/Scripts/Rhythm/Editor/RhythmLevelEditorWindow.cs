using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
#if false // старый редактор на ScriptableObject — отключён, теперь используется .rksl + TimelineUI


// ДИЗАЙН ТОТ ЖЕ (helpBox/foldoutHeader 240px таймлайн), НО С НУЛЯ — максимально просто.
// Таймлайн — ОГРОМНЫЙ, ХИТ = сплошной (у игрока). Перемотка — один большой слайдер.
public class RhythmLevelEditorWindow : EditorWindow
{
    RhythmLevelData level;
    AudioSource previewSource;
    GameObject previewGO;
    double dspStart;
    float previewTime;
    bool isPlaying;
    ReorderableList eventList;
    Vector2 listScroll, leftScroll;
    Vector2 timelineScroll;
    Vector2 scroll;
    float zoom = 1.2f;
    bool showWaveform = true;
    bool snapHalf = true;
    bool showHit = true, showGhost = false;

    int hoverIdx = -1, dragIdx = -1;
    float dragBeat0;
    Vector2 dragMouse0;
    bool scrubbing;
    float[] wave; AudioClip waveClip; int waveFreq;
    bool loop, follow = true;

    float genDens = 0.72f, genThr = 0.24f, genGap = 1.0f, genQuant = 0.5f, genMinS = 10f, genMaxS = 20f;
    int genMaxIn4 = 3; bool genStrict = true;
    int brush = 0;

    bool f1 = true, f2 = true, f3 = true;

    [MenuItem("Window/Rhythm Parkour/Level Editor")]
    public static void Open(){ var w=GetWindow<RhythmLevelEditorWindow>("Rhythm Level"); w.minSize=new Vector2(1240,860); w.Show(); }
    [MenuItem("Assets/Create/Rhythm Parkour/Level Data",false,0)]
    public static void CreateAsset(){ var a=new RhythmLevelData(); string p="Assets/!Rhythm Parkour/Levels/NewRhythmLevel.rksl"; p=AssetDatabase.GenerateUniqueAssetPath(p); var m=new RkslManifest{ title="New Level", bpm=128, offset=0, events=new System.Collections.Generic.List<ObstacleEvent>()}; string dir=Path.GetDirectoryName(p); if(!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir); RkslFile.Save(p,m,null,null,null,null); AssetDatabase.Refresh(); Selection.activeObject=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p); Open(); }

    void OnEnable(){ previewGO=new GameObject("~RhythmPreview"); previewGO.hideFlags=HideFlags.HideAndDontSave; previewSource=previewGO.AddComponent<AudioSource>(); EditorApplication.update+=Tick; }
    void OnDisable(){ EditorApplication.update-=Tick; if(previewSource) previewSource.Stop(); if(previewGO) DestroyImmediate(previewGO); }
    void Tick(){ if(isPlaying && level && level.music && previewSource){ previewTime=(float)(AudioSettings.dspTime-dspStart); if(previewTime>=level.music.length){ if(loop){ previewTime%=level.music.length; dspStart=AudioSettings.dspTime-previewTime; previewSource.time=previewTime; if(!previewSource.isPlaying) previewSource.Play(); } else { previewTime=level.music.length; isPlaying=false; previewSource.Stop(); } } if(!previewSource.isPlaying && isPlaying && !loop) isPlaying=false; Repaint(); } }

    float TravelForSpeed(float speed){
        if(speed<1f) speed=12f;
        Transform sp=null, hit=null;
        var mgr = RhythmParkourManager.Instance;
        if(mgr==null) mgr = FindObjectOfType<RhythmParkourManager>();
        if(mgr){ sp=mgr.spawnPoint; hit=mgr.hitTrigger; if(hit==null) hit=mgr.despawnPoint; }
        if(sp==null){ var go=GameObject.Find("SpawnPoint"); if(go) sp=go.transform; }
        if(hit==null){ var go=GameObject.Find("HitTrigger"); if(!go) go=GameObject.Find("Trigger"); if(go) hit=go.transform; }
        float d=52f;
        if(sp && hit){
            Vector3 toHit = hit.position - sp.position; toHit.y=0;
            Vector3 dir = (mgr && mgr.spawnPoint && mgr.despawnPoint) ? (mgr.despawnPoint.position - mgr.spawnPoint.position) : new Vector3(0,0,-1);
            dir.y=0; if(dir.sqrMagnitude<0.001f) dir=new Vector3(0,0,-1);
            dir.Normalize();
            d = Mathf.Abs(Vector3.Dot(toHit, dir));
            if(d<1f) d=52f;
        } else if(mgr && mgr.spawnPoint && mgr.despawnPoint){
            d = Vector3.Distance(mgr.spawnPoint.position, mgr.despawnPoint.position)*0.85f;
        }
        return d / Mathf.Max(1f, speed);
    }
    float Travel(ObstacleEvent ev){
        float s=ev.speed;
        if(s<0.1f && level){ var pf=level.GetPrefab(ev.prefabIndex); if(pf){var ob=pf.GetComponent<Obstacle>(); if(ob) s=ob.baseSpeed;}}
        return TravelForSpeed(s);
    }
    float HitTime(ObstacleEvent ev)=> ev.time + Travel(ev);
    float HitBeat(ObstacleEvent ev)=> level? level.TimeToBeat(HitTime(ev)) : ev.beat;

    void OnGUI(){
        DrawTopBar();
        DrawLevelRow();
        if(level==null){ DrawEmpty(); return; }
        EditorGUILayout.BeginHorizontal();
        leftScroll=EditorGUILayout.BeginScrollView(leftScroll, GUILayout.Width(340), GUILayout.ExpandHeight(true));
        DrawSteps();
        EditorGUILayout.EndScrollView();
        GUILayout.Box("",GUILayout.Width(2),GUILayout.ExpandHeight(true));
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawTimelineBig();
        GUILayout.Space(6);
        DrawList();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        DrawFooterHelp();
    }

    void DrawTopBar(){
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
            if(GUILayout.Button(" + Создать",EditorStyles.toolbarButton,GUILayout.Width(90))) CreateAsset();
            if(GUILayout.Button("Сохранить",EditorStyles.toolbarButton,GUILayout.Width(75))){ EditorUtility.SetDirty(level); AssetDatabase.SaveAssets(); ShowNotification(new GUIContent("Сохранено")); }
            GUILayout.FlexibleSpace();
            GUILayout.Label("● ХИТ сплошной  •  ○ призрак",EditorStyles.miniLabel,GUILayout.Width(180));
            if(GUILayout.Button("Док",EditorStyles.toolbarButton,GUILayout.Width(40))) Application.OpenURL("https://www.youtube.com/watch?v=Oh_trUKWDTg");
        }
    }
    void DrawLevelRow(){
        EditorGUILayout.Space(3);
        var nl=(RhythmLevelData)EditorGUILayout.ObjectField(new GUIContent("Уровень"),level,typeof(RhythmLevelData),false);
        if(nl!=level){ level=nl; wave=null; BuildList(); }
        if(level==null) return;
        string inf=$"{level.events.Count} нот"; if(level.music) inf+=$"  •  {level.music.length:0.0}с  •  BPM {level.bpm:0}"; else inf+="  •  нет музыки";
        EditorGUILayout.LabelField(inf,EditorStyles.miniLabel);
        if(level.obstaclePrefabs.Count==0) EditorGUILayout.HelpBox("Шаг 2 → перетащи префабы",MessageType.Warning);
        if(level.music==null) EditorGUILayout.HelpBox("Шаг 1 → назначь Music",MessageType.Warning);
    }
    void DrawEmpty(){
        EditorGUILayout.Space(16);
        EditorGUILayout.HelpBox("1 → Создай Level  2 → Music + BPM  3 → 7 префабов → Авто-постройка\nТаймлайн ниже — ХИТ = когда препятствие УЖЕ у игрока.",MessageType.Info);
        if(GUILayout.Button("Создать уровень",GUILayout.Height(36))) CreateAsset();
    }

    void DrawSteps(){
        f1=EditorGUILayout.Foldout(f1,"① Музыка и темп",true,EditorStyles.foldoutHeader);
        if(f1){
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            var m=(AudioClip)EditorGUILayout.ObjectField(new GUIContent("Music*"),level.music,typeof(AudioClip),false);
            var v=(VideoClip)EditorGUILayout.ObjectField("Video",level.video,typeof(VideoClip),false);
            float b=EditorGUILayout.FloatField(new GUIContent("BPM*"),level.bpm);
            float o=EditorGUILayout.FloatField(new GUIContent("Offset"),level.offset);
            if(EditorGUI.EndChangeCheck()){ Undo.RecordObject(level,"Header"); level.music=m; level.video=v; level.bpm=Mathf.Max(1,b); level.offset=o; EditorUtility.SetDirty(level); wave=null; BuildList(); }
            EditorGUILayout.EndVertical();
        }
        f2=EditorGUILayout.Foldout(f2,"② Палитра — кликни цвет чтобы красить",true,EditorStyles.foldoutHeader);
        if(f2){
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var so=new SerializedObject(level);
            EditorGUILayout.PropertyField(so.FindProperty("obstaclePrefabs"),new GUIContent("Префабы (7)"),true);
            so.ApplyModifiedProperties();
            for(int i=0;i<level.obstaclePrefabs.Count;i++){
                var go=level.obstaclePrefabs[i]; if(!go) continue;
                var ob=go.GetComponent<Obstacle>();
                using(new EditorGUILayout.HorizontalScope()){
                    // кисть — выбор чем рисовать
                    GUI.backgroundColor = (level.events.Count>0 && eventList!=null && eventList.index>=0 && level.events[eventList.index].prefabIndex==i) ? GetColor(i) : Color.white;
                    if(GUILayout.Button($"{i}",GUILayout.Width(28),GUILayout.Height(18))) {
                        if(eventList!=null && eventList.index>=0 && eventList.index<level.events.Count){
                            Undo.RecordObject(level,"Brush");
                            var ev=level.events[eventList.index]; ev.prefabIndex=i; level.events[eventList.index]=ev; EditorUtility.SetDirty(level); BuildList();
                        }
                    }
                    GUI.backgroundColor=Color.white;
                    EditorGUILayout.LabelField($"{go.name}",GUILayout.Width(120));
                    Rect cr=GUILayoutUtility.GetRect(12,12,GUILayout.Width(12)); EditorGUI.DrawRect(cr,GetColor(i));
                    EditorGUILayout.LabelField(ob?$"{ob.baseSpeed:0}м/с":"",EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.HelpBox("Клик по цифре — перекрасить выбранную ноту. Перетащи префабы выше.",MessageType.None);
            EditorGUILayout.EndVertical();
        }
        f3=EditorGUILayout.Foldout(f3,"③ Авто-генерация",true,EditorStyles.foldoutHeader);
        if(f3){
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using(new EditorGUILayout.HorizontalScope()){
                if(GUILayout.Button("Мало")){ genDens=0.45f; genThr=0.32f; genGap=1.5f; genQuant=1f; genMaxIn4=2; genStrict=true; }
                if(GUILayout.Button("Норма")){ var d=RhythmAutoGenerator.FlexibleSettings.Default; genDens=d.density; genThr=d.threshold; genGap=d.minGapBeats; genQuant=d.quantStep; genMaxIn4=d.maxIn4Beats; genStrict=d.strictSnap; }
                if(GUILayout.Button("Много")){ var d=RhythmAutoGenerator.FlexibleSettings.Many; genDens=d.density; genThr=d.threshold; genGap=d.minGapBeats; genQuant=d.quantStep; genMaxIn4=d.maxIn4Beats; genStrict=d.strictSnap; }
                if(GUILayout.Button("Экстрем")){ var d=RhythmAutoGenerator.FlexibleSettings.Extreme; genDens=d.density; genThr=d.threshold; genGap=d.minGapBeats; genQuant=d.quantStep; genMaxIn4=d.maxIn4Beats; genStrict=d.strictSnap; }
            }
            genDens=EditorGUILayout.Slider("Плотность",genDens,0.15f,1f);
            genThr=EditorGUILayout.Slider("Порог",genThr,0.05f,0.6f);
            // продвинутые скрыты по умолчанию
            if(EditorGUILayout.Foldout(false,"Дополнительно",true)){}
            GUI.backgroundColor=new Color(0.25f,0.85f,0.45f);
            using(new EditorGUI.DisabledScope(level.music==null||level.obstaclePrefabs.Count==0)){
                if(GUILayout.Button("★ АВТО-ПОСТРОЙКА ★",GUILayout.Height(32))){
                    if(EditorUtility.DisplayDialog("Генерация",$"Удалить {level.events.Count} и создать новые?","Да","Отмена")){
                        Undo.RecordObject(level,"Auto");
                        var cfg=new RhythmAutoGenerator.FlexibleSettings{density=genDens,threshold=genThr,minGapBeats=genGap,quantStep=genQuant,maxIn4Beats=genMaxIn4,minSpeed=genMinS,maxSpeed=genMaxS,strictSnap=genStrict};
                        RhythmAutoGenerator.Generate(level,0,cfg); BuildList();
                    }
                }
            }
            GUI.backgroundColor=Color.white;
            EditorGUILayout.EndVertical();
        }
        if(eventList!=null && eventList.index>=0 && eventList.index<level.events.Count){
            var ev=level.events[eventList.index];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Выбрано #{eventList.index}  ХИТ {HitBeat(ev):0.##}б",EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Спавн {ev.beat:0.##}б • {ev.speed:0.0}м/с",EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
    }

    void BuildList(){
        if(level==null) return;
        SerializedObject so=new SerializedObject(level);
        var pr=so.FindProperty("events");
        eventList=new ReorderableList(so,pr,true,true,true,true);
        eventList.drawHeaderCallback=(r)=> EditorGUI.LabelField(r,$"Ноты — {level.events.Count}  |  HIT бит | спавн | префаб | скор");
        eventList.drawElementCallback=(r,idx,act,foc)=>{
            var e=pr.GetArrayElementAtIndex(idx);
            r.y+=2; float w=r.width;
            var bP=e.FindPropertyRelative("beat");
            var pP=e.FindPropertyRelative("prefabIndex");
            var sP=e.FindPropertyRelative("speed");
            var ev=level.events[idx];
            float hb=HitBeat(ev);
            GUI.color=new Color(0.5f,1f,0.5f,1f); GUI.Label(new Rect(r.x,r.y,44,16),hb.ToString("0.##"),EditorStyles.miniLabel); GUI.color=Color.white;
            float nb=EditorGUI.FloatField(new Rect(r.x+44,r.y,52,16),bP.floatValue);
            if(!Mathf.Approximately(nb,bP.floatValue)){ Undo.RecordObject(level,"Edit"); bP.floatValue=nb; e.FindPropertyRelative("time").floatValue=level.BeatToTime(nb); so.ApplyModifiedProperties(); EditorUtility.SetDirty(level); }
            int ni=EditorGUI.IntPopup(new Rect(r.x+100,r.y,88,16),pP.intValue,GetNames(),GetIndices());
            if(ni!=pP.intValue){ pP.intValue=ni; so.ApplyModifiedProperties(); EditorUtility.SetDirty(level); }
            float ns=EditorGUI.FloatField(new Rect(r.x+192,r.y,42,16),sP.floatValue);
            if(!Mathf.Approximately(ns,sP.floatValue)){ sP.floatValue=Mathf.Max(0,ns); so.ApplyModifiedProperties(); EditorUtility.SetDirty(level); }
            EditorGUI.DrawRect(new Rect(r.x+w-10,r.y,9,16),GetColor(pP.intValue));
        };
        eventList.onAddCallback=(l)=>{ Undo.RecordObject(level,"Add"); float b=level.events.Count>0?HitBeat(level.events[level.events.Count-1])+2f:0; float defSpd=12f; if(level.obstaclePrefabs.Count>0&&level.obstaclePrefabs[0]){var ob2=level.obstaclePrefabs[0].GetComponent<Obstacle>(); if(ob2) defSpd=ob2.baseSpeed; } float travel=TravelForSpeed(defSpd); float spawnB=level.TimeToBeat(level.BeatToTime(b)-travel); var ev=ObstacleEvent.Create(spawnB,Mathf.Clamp(brush,0,Mathf.Max(0,level.obstaclePrefabs.Count-1)),Vector3.zero,defSpd); ev.time=level.BeatToTime(spawnB); level.events.Add(ev); EditorUtility.SetDirty(level); };
        eventList.onRemoveCallback=(l)=>{ if(l.index<0||l.index>=level.events.Count) return; Undo.RecordObject(level,"Remove"); l.serializedProperty.DeleteArrayElementAtIndex(l.index); l.serializedProperty.serializedObject.ApplyModifiedProperties(); EditorUtility.SetDirty(level); l.index=Mathf.Clamp(l.index-1,0,level.events.Count-1); };
        eventList.onReorderCallback=(l)=> EditorUtility.SetDirty(level);
        eventList.elementHeight=20;
    }

    void EnsureWaveform(){ if(level==null||level.music==null) return; if(wave!=null&&waveClip==level.music&&waveFreq==level.music.frequency) return; var clip=level.music; int ch=clip.channels; int samples=clip.samples; if(samples<=0){wave=new float[1]; return;} int step=Mathf.Max(1,samples/8192); int cnt=samples/step; float[] all=new float[samples*ch]; clip.GetData(all,0); wave=new float[cnt]; float max=0.001f; for(int i=0;i<cnt;i++){ float s=0; for(int c=0;c<ch;c++) s+=Mathf.Abs(all[(i*step)*ch+c]); s/=ch; wave[i]=s; if(s>max) max=s; } for(int i=0;i<cnt;i++) wave[i]/=max; waveClip=clip; waveFreq=clip.frequency; }

    void DrawTimelineBig(){
        if(level==null||level.music==null){ EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.HelpBox("Таймлайн — назначь Music (слева ①)",MessageType.Info); EditorGUILayout.EndVertical(); return; }
        EnsureWaveform();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent("ТАЙМЛАЙН — ХИТ у игрока  (перетаскивай ХИТ)"),EditorStyles.boldLabel);
        // транспорт
        using(new EditorGUILayout.HorizontalScope()){
            if(GUILayout.Button("⏮ 0",GUILayout.Width(38))){ previewTime=0; if(previewSource) previewSource.time=0; dspStart=AudioSettings.dspTime; }
            if(GUILayout.Button("◀ -1",GUILayout.Width(38))){ previewTime=Mathf.Max(0,previewTime-1); if(previewSource) previewSource.time=previewTime; dspStart=AudioSettings.dspTime-previewTime; }
            if(!isPlaying){ if(GUILayout.Button("▶ Play",GUILayout.Width(60))) Play(previewTime); if(GUILayout.Button("▶ 0",GUILayout.Width(36))) Play(0); } else if(GUILayout.Button("■",GUILayout.Width(36))) Stop();
            if(GUILayout.Button("+1 ▶",GUILayout.Width(38))){ previewTime=Mathf.Min(level.music.length,previewTime+1); if(previewSource) previewSource.time=previewTime; dspStart=AudioSettings.dspTime-previewTime; }
            GUILayout.Space(6);
            loop=EditorGUILayout.ToggleLeft(new GUIContent("Loop"),loop,GUILayout.Width(50));
            follow=EditorGUILayout.ToggleLeft(new GUIContent("След."),follow,GUILayout.Width(54));
        }
        // ПЕРЕМОТКА — ОГРОМНЫЙ слайдер
        using(new EditorGUILayout.HorizontalScope()){
            EditorGUILayout.LabelField($"{previewTime:0.0}с",GUILayout.Width(44));
            EditorGUI.BeginChangeCheck();
            float nt=GUILayout.HorizontalSlider(previewTime,0,level.music.length, GUILayout.Height(18));
            if(EditorGUI.EndChangeCheck()){ previewTime=nt; if(previewSource){ previewSource.time=Mathf.Clamp(previewTime,0,level.music.length-0.01f); dspStart=AudioSettings.dspTime-previewSource.time; } if(isPlaying) dspStart=AudioSettings.dspTime-previewTime; }
            EditorGUILayout.LabelField($"{level.music.length:0.0}с  {level.TimeToBeat(previewTime):0.0}б",GUILayout.Width(104));
            if(GUILayout.Button("⌖",GUILayout.Width(22))){ float b=level.TimeToBeat(previewTime); float vw=position.width-360; float tb=level.TimeToBeat(level.music.length)+8; float tw=tb*28f*zoom; timelineScroll.x=Mathf.Clamp(b*28f*zoom - vw*0.5f,0,Mathf.Max(0,tw-vw)); }
            GUILayout.Label("Zoom",GUILayout.Width(32)); zoom=EditorGUILayout.Slider(zoom,0.35f,4f,GUILayout.Width(110));
        }
        using(new EditorGUILayout.HorizontalScope()){
            snapHalf=EditorGUILayout.ToggleLeft(new GUIContent("Сетка 0.5"),snapHalf,GUILayout.Width(78));
            showWaveform=EditorGUILayout.ToggleLeft(new GUIContent("Волна"),showWaveform,GUILayout.Width(58));
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Квантовать",GUILayout.Width(110)) && eventList!=null && eventList.index>=0){ Undo.RecordObject(level,"Quant"); var ev=level.events[eventList.index]; float hb=HitBeat(ev); hb=Mathf.Round(hb/(snapHalf?0.5f:1f))*(snapHalf?0.5f:1f); float nt2=level.BeatToTime(hb)-Travel(ev); ev.beat=level.TimeToBeat(nt2); ev.time=nt2; level.events[eventList.index]=ev; level.SortByTime(); EditorUtility.SetDirty(level); BuildList(); }
        }
        // легенда — клик меняет кисть
        using(new EditorGUILayout.HorizontalScope()){
            EditorGUILayout.LabelField("Кисть:",GUILayout.Width(44));
            for(int i=0;i<Mathf.Min(level.obstaclePrefabs.Count,7);i++){
                Rect cr=GUILayoutUtility.GetRect(12,12,GUILayout.Width(12)); EditorGUI.DrawRect(cr,GetColor(i));
                string n=level.obstaclePrefabs[i]?level.obstaclePrefabs[i].name:$"#{i}";
                bool isBrush = (i==0 && level.events.Count==0) || (eventList!=null && eventList.index>=0 && level.events[eventList.index].prefabIndex==i);
                if(Event.current.type==EventType.MouseDown && cr.Contains(Event.current.mousePosition)){ brush=i;
                    if(eventList!=null && eventList.index>=0){ Undo.RecordObject(level,"Brush"); var ev=level.events[eventList.index]; ev.prefabIndex=i; level.events[eventList.index]=ev; EditorUtility.SetDirty(level); BuildList(); Event.current.Use(); }
                }
                EditorGUILayout.LabelField($"{i}:{n}",EditorStyles.miniLabel,GUILayout.Width(78));
            }
        }

        float viewW=position.width-360; if(viewW<480) viewW=480;
        float totalBeats=level.TimeToBeat(level.music.length)+8;
        float totalW=totalBeats*28f*zoom;
        Rect r=GUILayoutUtility.GetRect(viewW, 280, GUILayout.ExpandWidth(true)); // ОГРОМНЫЙ
        EditorGUI.DrawRect(r,new Color(0.13f,0.13f,0.15f,1f));
        Event e=Event.current;
        float sx=r.x - timelineScroll.x;

        if(showWaveform && wave!=null && wave.Length>4){
            int cols=Mathf.RoundToInt(r.width);
            for(int px=0;px<cols;px++){
                int idx=Mathf.Clamp(Mathf.RoundToInt((px+timelineScroll.x)/totalW*wave.Length),0,wave.Length-1);
                float amp=wave[idx];
                float h=amp*r.height*0.42f;
                Rect wr=new Rect(r.x+px, r.y+r.height*0.5f-h*0.5f,1,h);
                EditorGUI.DrawRect(wr,new Color(0.3f,0.7f,1f,0.18f));
            }
            EditorGUI.DrawRect(new Rect(r.x, r.y+r.height*0.5f, r.width,1), new Color(0.3f,0.7f,1f,0.08f));
        }
        Rect ruler=new Rect(r.x,r.y,r.width,16);
        EditorGUI.DrawRect(ruler,new Color(0.2f,0.2f,0.2f,1f));
        for(int b=0;b<totalBeats;b++){
            float x=sx+b*28f*zoom; if(x<r.x-4||x>r.x+r.width+4) continue;
            bool bar=b%4==0; bool beat=(b%1==0&&!bar);
            Color col=bar?new Color(1,1,1,0.26f):beat?new Color(1,1,1,0.14f):new Color(1,1,1,0.06f);
            float h=bar?r.height:beat?r.height*0.62f:r.height*0.35f;
            float y0=bar?ruler.y: r.y+r.height-h;
            EditorGUI.DrawRect(new Rect(x,y0,bar?2:1,h),col);
            if(bar){ GUI.Label(new Rect(x+3,ruler.y+1,36,12),b.ToString(),EditorStyles.miniLabel); float sec=level.BeatToTime(b); GUI.Label(new Rect(x+3,r.y+r.height-11,36,10),$"{sec:0.0}с",new GUIStyle(EditorStyles.miniLabel){fontSize=8,normal=new GUIStyleState{textColor=new Color(1,1,1,0.45f)}}); }
            else if(beat&&zoom>0.9f) GUI.Label(new Rect(x+2,ruler.y+1,20,10),".0",EditorStyles.miniLabel);
            else if(!beat&&snapHalf&&zoom>1.4f) GUI.Label(new Rect(x+1,ruler.y+1,14,10),".5",EditorStyles.miniLabel);
        }

        hoverIdx=-1;
        for(int i=0;i<level.events.Count;i++){
            var ev=level.events[i];
            float hb=HitBeat(ev), sb=ev.beat;
            float hx=sx+hb*28f*zoom, sx2=sx+sb*28f*zoom;
            if(hx<r.x-20 && sx2<r.x-20) continue;
            if(hx>r.x+r.width+20 && sx2>r.x+r.width+20) continue;
            float h=Mathf.Clamp(16+(ev.speed-10)*1.4f,14,42);
            float y=r.y+22+(r.height-22)*0.5f - h*0.5f +6;
            Rect hr=new Rect(hx-9,y,18,h);
            bool hov=hr.Contains(e.mousePosition);
            bool sel=eventList!=null&&eventList.index==i;
            bool drag=dragIdx==i;
            if(hov) hoverIdx=i;
            if(showHit && showGhost){
                float lx=Mathf.Min(sx2,hx), rx=Mathf.Max(sx2,hx);
                EditorGUI.DrawRect(new Rect(lx,y+h*0.5f-1,rx-lx,2), new Color(1,1,1, sel?0.32f:0.16f));
                Rect sr=new Rect(sx2-5,y+3,10,h-6);
                Color g=GetColor(ev.prefabIndex); g.a=sel?0.32f:0.18f;
                EditorGUI.DrawRect(sr,g);
            }
            Color c=GetColor(ev.prefabIndex); if(drag) c=Color.Lerp(c,Color.white,0.35f);
            if(sel) EditorGUI.DrawRect(new Rect(hr.x-2,hr.y-2,hr.width+4,hr.height+4),Color.white);
            else if(hov) EditorGUI.DrawRect(new Rect(hr.x-1,hr.y-1,hr.width+2,hr.height+2),new Color(1,1,1,0.6f));
            EditorGUI.DrawRect(hr,c);
            EditorGUI.DrawRect(new Rect(hr.x,hr.y+hr.height-3,hr.width,2),new Color(0,0,0,0.35f));
            if(h>20) GUI.Label(new Rect(hr.x-4,hr.y+h*0.5f-6,26,12),$"{ev.speed:0}",new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter,fontSize=8});
            if(hov||sel){
                string tip=$"ХИТ {hb:0.##}б  #{ev.prefabIndex}";
                GUI.Label(new Rect(hr.x-30,hr.y-14,76,12),tip,new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter,fontSize=7,normal=new GUIStyleState{textColor=new Color(1,1,1,0.92f)}});
            }
        }

        float pb=level.TimeToBeat(previewTime);
        float cx=sx+pb*28f*zoom;
        Rect hit2=new Rect(cx-6,r.y,12,r.height);
        bool hovH=hit2.Contains(e.mousePosition);
        Color hc=hovH||scrubbing?new Color(0.2f,1f,0.6f,1f):new Color(0,1,0.5f,0.9f);
        if(cx>=r.x&&cx<=r.x+r.width){
            EditorGUI.DrawRect(new Rect(cx,r.y,2,r.height),hc);
            Vector3[] tri=new Vector3[]{new Vector3(cx-7,r.y,0),new Vector3(cx+7,r.y,0),new Vector3(cx,r.y+8,0)};
            Handles.color=hc; Handles.DrawAAConvexPolygon(tri);
        }
        if(isPlaying && follow){ float t=pb*28f*zoom - viewW*0.38f; timelineScroll.x=Mathf.Lerp(timelineScroll.x,Mathf.Clamp(t,0,Mathf.Max(0,totalW-viewW)),0.08f); Repaint(); } else if(isPlaying) Repaint();

        if(e.type==EventType.ScrollWheel && r.Contains(e.mousePosition)){
            if(e.control||e.command){
                float old=zoom; zoom=Mathf.Clamp(zoom-e.delta.y*0.06f,0.35f,4f);
                float mx=e.mousePosition.x - r.x + timelineScroll.x;
                float bam=mx/(28f*old);
                timelineScroll.x=bam*28f*zoom - (e.mousePosition.x - r.x);
                timelineScroll.x=Mathf.Clamp(timelineScroll.x,0,Mathf.Max(0,totalW-viewW)); e.Use(); Repaint();
            } else { timelineScroll.x=Mathf.Clamp(timelineScroll.x+e.delta.y*12f,0,Mathf.Max(0,totalW-viewW)); e.Use(); Repaint(); }
        }
        if(e.type==EventType.MouseDrag && e.button==2 && r.Contains(e.mousePosition)){ timelineScroll.x=Mathf.Clamp(timelineScroll.x - e.delta.x,0,Mathf.Max(0,totalW-viewW)); e.Use(); Repaint(); }

        if(e.type==EventType.MouseDown){
            if(e.button==0){
                if(hit2.Contains(e.mousePosition)){
                    scrubbing=true;
                    float b=Mathf.Clamp((e.mousePosition.x - sx)/(28f*zoom),0,totalBeats);
                    b=snapHalf?Mathf.Round(b*2)/2:Mathf.Round(b);
                    previewTime=level.BeatToTime(b);
                    if(previewSource) previewSource.time=Mathf.Clamp(previewTime,0,level.music.length-0.05f);
                    e.Use(); Repaint();
                } else {
                    bool hit=false;
                    for(int i=0;i<level.events.Count;i++){
                        var ev=level.events[i];
                        float hb=HitBeat(ev);
                        float x=sx+hb*28f*zoom;
                        float h=Mathf.Clamp(16+(ev.speed-10)*1.4f,14,42);
                        float y=r.y+22+(r.height-22)*0.5f - h*0.5f+6;
                        Rect hr=new Rect(x-9,y,18,h);
                        if(hr.Contains(e.mousePosition)){
                            eventList.index=i; dragIdx=i; dragBeat0=hb; dragMouse0=e.mousePosition; hit=true; e.Use(); Repaint(); break;
                        }
                    }
                    if(!hit && r.Contains(e.mousePosition)){
                        if(e.clickCount==2){
                            float b=(e.mousePosition.x - sx)/(28f*zoom);
                            b=snapHalf?Mathf.Round(b*2)/2:Mathf.Round(b);
                            Play(level.BeatToTime(Mathf.Max(0,b))); e.Use();
                        } else if(!scrubbing){
                            float b=(e.mousePosition.x - sx)/(28f*zoom);
                            b=snapHalf?Mathf.Round(b*2)/2:Mathf.Round(b);
                            if(b>=0&&b<=totalBeats){
                                float hitB=b;
                                float hitT=level.BeatToTime(hitB);
                                float defSpd=12f;
                                if(level.obstaclePrefabs.Count>0&&level.obstaclePrefabs[0]){var ob0=level.obstaclePrefabs[0].GetComponent<Obstacle>(); if(ob0) defSpd=ob0.baseSpeed; }
                                float travel=TravelForSpeed(defSpd); float spawnT=hitT-travel; float spawnB=level.TimeToBeat(spawnT);
                                Undo.RecordObject(level,"Add");
                                var ev=ObstacleEvent.Create(Mathf.Max(0,spawnB),Mathf.Clamp(brush,0,Mathf.Max(0,level.obstaclePrefabs.Count-1)),Vector3.zero,defSpd);
                                ev.time=spawnT;
                                level.events.Add(ev); level.SortByTime(); EditorUtility.SetDirty(level); BuildList();
                                for(int k=0;k<level.events.Count;k++) if(Mathf.Abs(HitBeat(level.events[k]) - hitB)<0.01f){ eventList.index=k; dragIdx=k; dragBeat0=hitB; dragMouse0=e.mousePosition; break; }
                                ShowNotification(new GUIContent($"+ ХИТ {hitB:0.##}б"));
                                e.Use(); Repaint();
                            }
                        }
                    }
                }
            } else if(e.button==1){
                for(int i=0;i<level.events.Count;i++){
                    var ev=level.events[i]; float hb=HitBeat(ev); float x=sx+hb*28f*zoom;
                    float h=Mathf.Clamp(16+(ev.speed-10)*1.4f,14,42); float y=r.y+22+(r.height-22)*0.5f - h*0.5f+6;
                    Rect hr=new Rect(x-9,y,18,h);
                    if(hr.Contains(e.mousePosition)){
                        eventList.index=i;
                        GenericMenu menu=new GenericMenu(); int idx=i;
                        menu.AddItem(new GUIContent("Удалить (Del)"),false,()=>{ Undo.RecordObject(level,"Remove"); level.events.RemoveAt(idx); EditorUtility.SetDirty(level); BuildList(); });
                        menu.AddItem(new GUIContent("Дублировать (D)"),false,()=>{ Undo.RecordObject(level,"Duplicate"); var c=level.events[idx]; float hb2=HitBeat(c); hb2+=snapHalf?0.5f:1f; float nt=level.BeatToTime(hb2)-Travel(c); c.beat=level.TimeToBeat(nt); c.time=nt; level.events.Add(c); level.SortByTime(); EditorUtility.SetDirty(level); BuildList();});
                        menu.AddSeparator("");
                        for(int p=0;p<level.obstaclePrefabs.Count;p++){ int pp=p; string n=level.obstaclePrefabs[p]?level.obstaclePrefabs[p].name:$"#{p}"; menu.AddItem(new GUIContent($"Префаб/{pp}: {n}"),ev.prefabIndex==pp,()=>{ Undo.RecordObject(level,"Change Prefab"); var c=level.events[idx]; c.prefabIndex=pp; level.events[idx]=c; EditorUtility.SetDirty(level);});}
                        menu.ShowAsContext(); e.Use(); break;
                    }
                }
            }
        }
        if(e.type==EventType.MouseUp){ dragIdx=-1; scrubbing=false; }
        if(e.type==EventType.MouseDrag){
            if(dragIdx>=0&&dragIdx<level.events.Count&&e.button==0){
                float dB=(e.mousePosition.x - dragMouse0.x)/(28f*zoom);
                float step=snapHalf?0.5f:1f;
                float newHit=Mathf.Round((dragBeat0+dB)/step)*step;
                newHit=Mathf.Clamp(newHit,0,totalBeats);
                var ev=level.events[dragIdx];
                float curHit=HitBeat(ev);
                if(!Mathf.Approximately(curHit,newHit)){
                    Undo.RecordObject(level,"Drag");
                    float hitT=level.BeatToTime(newHit);
                    float travel=Travel(ev);
                    float spawnT=hitT - travel;
                    ev.beat=level.TimeToBeat(spawnT); ev.time=spawnT;
                    level.events[dragIdx]=ev;
                    level.SortByTime();
                    for(int k=0;k<level.events.Count;k++) if(Mathf.Abs(HitBeat(level.events[k])-newHit)<0.01f) { eventList.index=k; dragIdx=k; break; }
                    EditorUtility.SetDirty(level);
                }
                e.Use(); Repaint();
            } else if(scrubbing){
                float b=Mathf.Clamp((e.mousePosition.x - sx)/(28f*zoom),0,totalBeats);
                b=snapHalf?Mathf.Round(b*2)/2:Mathf.Round(b);
                previewTime=level.BeatToTime(b);
                if(previewSource) previewSource.time=Mathf.Clamp(previewTime,0,level.music.length-0.05f);
                e.Use(); Repaint();
            }
        }
        if(e.type==EventType.KeyDown && eventList!=null && eventList.index>=0){
            bool h=false;
            if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace){ Undo.RecordObject(level,"Remove"); level.events.RemoveAt(eventList.index); EditorUtility.SetDirty(level); BuildList(); h=true; }
            else if(e.keyCode==KeyCode.D&&e.control){ Undo.RecordObject(level,"Duplicate"); var c=level.events[eventList.index]; float hb=HitBeat(c); hb+=snapHalf?0.5f:1f; float nt=level.BeatToTime(hb)-Travel(c); c.beat=level.TimeToBeat(nt); c.time=nt; level.events.Add(c); level.SortByTime(); EditorUtility.SetDirty(level); BuildList(); h=true; }
            else if(e.keyCode==KeyCode.LeftArrow){ Undo.RecordObject(level,"Nudge"); var c=level.events[eventList.index]; float hb=HitBeat(c); hb-=snapHalf?0.5f:1f; hb=Mathf.Max(0,hb); float nt=level.BeatToTime(hb)-Travel(c); c.beat=level.TimeToBeat(nt); c.time=nt; level.events[eventList.index]=c; level.SortByTime(); EditorUtility.SetDirty(level); h=true; }
            else if(e.keyCode==KeyCode.RightArrow){ Undo.RecordObject(level,"Nudge"); var c=level.events[eventList.index]; float hb=HitBeat(c); hb+=snapHalf?0.5f:1f; float nt=level.BeatToTime(hb)-Travel(c); c.beat=level.TimeToBeat(nt); c.time=nt; level.events[eventList.index]=c; level.SortByTime(); EditorUtility.SetDirty(level); h=true; }
            if(h){ e.Use(); Repaint(); }
        }

        EditorGUILayout.BeginHorizontal(); float ns=GUILayout.HorizontalScrollbar(timelineScroll.x,viewW,0,totalW); if(!Mathf.Approximately(ns,timelineScroll.x)){ timelineScroll.x=ns; Repaint(); }
        if(GUILayout.Button(new GUIContent("◀","В начало"),GUILayout.Width(22))) timelineScroll.x=0;
        if(GUILayout.Button(new GUIContent("▶","В конец"),GUILayout.Width(22))) timelineScroll.x=Mathf.Max(0,totalW-viewW);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("ХИТ тащи — призрак сам. Клик пусто = +ХИТ. Колесо/Ctrl+колесо.",EditorStyles.miniLabel);
        if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Space){ if(isPlaying) Stop(); else Play(previewTime); e.Use(); }
        EditorGUILayout.EndVertical();
    }

    void DrawList(){
        if(level==null) return;
        if(eventList==null) BuildList();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent($"Список — ХИТ  ({level.events.Count})"),EditorStyles.boldLabel);
        if(level.events.Count==0) EditorGUILayout.HelpBox("Пусто — кликни по таймлайну.",MessageType.Info);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using(new EditorGUILayout.HorizontalScope()){
            EditorGUILayout.LabelField("HIT",EditorStyles.miniLabel,GUILayout.Width(44));
            EditorGUILayout.LabelField("префаб",EditorStyles.miniLabel,GUILayout.Width(88));
            EditorGUILayout.LabelField("скор",EditorStyles.miniLabel,GUILayout.Width(42));
            GUILayout.FlexibleSpace();
        }
        listScroll=EditorGUILayout.BeginScrollView(listScroll,GUILayout.Height(180));
        eventList.DoLayoutList();
        EditorGUILayout.EndScrollView();
        using(new EditorGUILayout.HorizontalScope()){
            GUI.enabled=eventList.index>=0&&eventList.index<level.events.Count;
            if(GUILayout.Button("🗑 Удалить",GUILayout.Height(22))){
                int idx=eventList.index; Undo.RecordObject(level,"Remove"); level.events.RemoveAt(idx); EditorUtility.SetDirty(level); eventList.index=Mathf.Clamp(idx-1,0,level.events.Count-1); BuildList();
            }
            if(GUILayout.Button("⎘ Дублировать",GUILayout.Height(22))){
                int idx=eventList.index; Undo.RecordObject(level,"Duplicate");
                var c=level.events[idx]; float hb=HitBeat(c)+ (snapHalf?0.5f:1f); float nt=level.BeatToTime(hb)-Travel(c); c.beat=level.TimeToBeat(nt); c.time=nt;
                level.events.Add(c); level.SortByTime(); EditorUtility.SetDirty(level); BuildList();
            }
            GUI.enabled=true; GUILayout.FlexibleSpace();
            if(GUILayout.Button("Очистить",GUILayout.Height(22))){
                if(EditorUtility.DisplayDialog("Очистить?", $"Удалить все {level.events.Count}?", "Да","Отмена")){ Undo.RecordObject(level,"Clear"); level.events.Clear(); EditorUtility.SetDirty(level); BuildList(); }
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    void DrawFooterHelp(){
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Подсказка: ХИТ ставь на удар — игра сама вычтет полёт.",EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    string[] GetNames(){ if(level.obstaclePrefabs==null||level.obstaclePrefabs.Count==0) return new[]{"0: empty"}; var a=new string[level.obstaclePrefabs.Count]; for(int i=0;i<a.Length;i++) a[i]=$"{i}: {(level.obstaclePrefabs[i]?level.obstaclePrefabs[i].name:"null")}"; return a; }
    int[] GetIndices(){ if(level.obstaclePrefabs==null||level.obstaclePrefabs.Count==0) return new[]{0}; var a=new int[level.obstaclePrefabs.Count]; for(int i=0;i<a.Length;i++) a[i]=i; return a; }
    Color GetColor(int idx){ float h=(idx*0.37f)%1f; return Color.HSVToRGB(h,0.75f,0.9f); }
    void Play(float t){ if(level.music==null||previewSource==null) return; previewSource.clip=level.music; previewSource.time=Mathf.Clamp(t,0,level.music.length-0.1f); dspStart=AudioSettings.dspTime-previewSource.time; previewSource.Play(); isPlaying=true; previewTime=previewSource.time; }
    void Stop(){ isPlaying=false; if(previewSource) previewSource.Stop(); }
}
#endif