using System.Collections.Generic;
using System.IO;
using RKS.RhythmParkour.Core;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using Zenject;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;









namespace RKS.RhythmParkour.Rhythm
{
    public class RhythmParkourManager : RKSBehaviour
    {
        [Header("Level")]
        public RhythmLevelData levelData;

        [HideInInspector]
        [InjectOptional] public Conductor conductor;
        public AudioSource musicSource;
        public VideoPlayer videoPlayer;

        [HideInInspector]
        [InjectOptional] public RhythmScoreManager score;
        [HideInInspector]
        [InjectOptional] public GlobalObstacleCatalog catalog;
        [HideInInspector]
        [InjectOptional] public LevelTransfer transfer;
        [HideInInspector]
        [InjectOptional] public LevelVisualApplier visual;

        [Header("Track")]
        [Tooltip("Пол дорожки (Ground). Если пусто — найдётся объект с именем Ground)")]
        public Transform trackFloor;
        [Tooltip("Ширина дорожки. 0 = взять из trackFloor.localScale.x")]
        public float trackWidth = 0f;
        [HideInInspector] public float trackMinX;
        [HideInInspector] public float trackMaxX;
        [HideInInspector] public float trackY;

        [Header("Spawn Points")]
        [Tooltip("Откуда появляются препятствия")]
        public Transform spawnPoint;
        [Tooltip("Где удаляются (за экраном)")]
        public Transform despawnPoint;
        [HideInInspector] public Transform hitTrigger;
        [Tooltip("Куда двигаются — от spawn к despawn. Если despawn пусто — вперёд по -Z")]
        public Vector3 moveDirection = Vector3.forward;
        [Tooltip("Родитель для спавна (для порядка в иерархии)")]
        public Transform spawnParent;

        [Header("Default Speed")]
        [Tooltip("Дефолт если у префаба и у ивента speed=0")]
        public float defaultObstacleSpeed = 12f;

        [HideInInspector] public float obstacleSpeed = 12f;

        [Header("Pool")]
        public int poolSizePerPrefab = 14;

        [Header("Auto Start")]
        public bool autoPlayOnStart = true;
        public float autoPlayDelay = 0.5f;

        [Header("Hit Debug")]
        [Tooltip("Логировать спавн/деспанн/удары препятствий (мин. дистанция, касания)")]
        public bool debugHits = false;

        [Header("State")]
        public int nextEventIndex;
        public bool isPlaying;
        public float currentTime;
        [Tooltip("Уровень уже завершён (показан экран результатов)")]
        public bool levelFinished;

        private List<ObstacleEvent> sortedEvents;
        private readonly List<Obstacle> active = new List<Obstacle>();
        private readonly Dictionary<int, Queue<Obstacle>> pools = new Dictionary<int, Queue<Obstacle>>();
        private Vector3 dirNormalized;

        [HideInInspector]
        [InjectOptional] public EasyPeasyFirstPersonController.FirstPersonController playerController;

        protected override void OnInjected()
        {
            if (conductor == null) conductor = GetComponent<Conductor>();
            if (musicSource == null) musicSource = GetComponent<AudioSource>();
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (conductor != null) conductor.musicSource = musicSource;
            if (spawnParent == null) spawnParent = transform;
            UpdateTrackBounds();
            UpdateDirection();
        }

        void OnValidate()
        {
            UpdateTrackBounds();
            UpdateDirection();
            if (spawnPoint != null && despawnPoint != null)
                moveDirection = (despawnPoint.position - spawnPoint.position);

            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude < 0.001f) moveDirection = new Vector3(0, 0, -1);
        }

        void UpdateTrackBounds()
        {


            float width = trackWidth;
            if (width <= 0.01f)
            {
                if (trackFloor != null) width = Mathf.Abs(trackFloor.localScale.x);
                else width = 6f;
            }
            float centerX = trackFloor != null ? trackFloor.position.x : 0f;

            float half = width * 0.5f - 0.05f;
            trackMinX = centerX - half;
            trackMaxX = centerX + half;
            trackY = trackFloor != null ? trackFloor.position.y + trackFloor.localScale.y * 0.5f : 0.5f;

            if (defaultObstacleSpeed < 0.1f) defaultObstacleSpeed = obstacleSpeed > 0.1f ? obstacleSpeed : 12f;
            else obstacleSpeed = defaultObstacleSpeed;
        }

        void UpdateDirection()
        {
            Vector3 dir;
            if (spawnPoint != null && despawnPoint != null)
                dir = despawnPoint.position - spawnPoint.position;
            else
                dir = moveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = new Vector3(0, 0, -1);
            dirNormalized = dir.normalized;
        }

        void EnsureHitTrigger()
        {


            if (hitTrigger != null) return;
            var fpc = playerController;
            if (fpc != null)
            {
                var t = new GameObject("HitTrigger (Auto)").transform;
                t.position = fpc.transform.position + fpc.transform.forward * 4f;
                t.position = new Vector3(Mathf.Clamp(t.position.x, trackMinX, trackMaxX), 0, t.position.z);
                hitTrigger = t;
            }
        }

        public float GetSpawnToHitDistance()
        {
            if (spawnPoint == null) return 52f;
            Vector3 targetPos;
            if (hitTrigger != null) targetPos = hitTrigger.position;
            else if (despawnPoint != null) targetPos = Vector3.Lerp(spawnPoint.position, despawnPoint.position, 0.88f);
            else targetPos = spawnPoint.position + dirNormalized * 52f;
            Vector3 toHit = targetPos - spawnPoint.position;
            toHit.y = 0f;
            float d = Mathf.Abs(Vector3.Dot(toHit, dirNormalized));
            if (d < 1f) d = 52f;
            return d;
        }
        public float GetTravelTime(float speed)
        {
            if (speed < 0.1f) speed = defaultObstacleSpeed;
            return GetSpawnToHitDistance() / Mathf.Max(1f, speed);
        }
        public float GetTravelTime(ObstacleEvent ev)
        {
            float s = ev.speed;
            if (s < 0.1f && levelData != null)
            {
                var pf = levelData.GetPrefab(ev.prefabIndex, catalog);
                if (pf) { var ob = pf.GetComponent<Obstacle>(); if (ob) s = ob.baseSpeed; }
            }
            return GetTravelTime(s);
        }

        protected override void OnReady()
        {

            if (spawnPoint != null && (spawnPoint.name == "Obstacle" || spawnPoint.name == "Colider"))
                spawnPoint = null;
            if (despawnPoint != null && (despawnPoint.name == "Obstacle" || despawnPoint.name == "Colider"))
                despawnPoint = null;

            UpdateTrackBounds();
            EnsureHitTrigger();

            if (spawnPoint == null) spawnPoint = EnsurePoint("SpawnPoint", new Vector3(0f, 0f, 52f));
            if (despawnPoint == null) despawnPoint = EnsurePoint("DespawnPoint", new Vector3(0f, 0f, -8f));
            if (hitTrigger == null)
            {

                Vector3 hp = spawnPoint.position + dirNormalized * (GetSpawnToHitDistance() > 1f ? GetSpawnToHitDistance() : 38f);

                if (Vector3.Distance(hp, spawnPoint.position) < 5f) hp = new Vector3(0, 0, 8f);
                hitTrigger = EnsurePoint("HitTrigger", hp);
                hitTrigger.gameObject.tag = "Untagged";
                var col = hitTrigger.GetComponent<BoxCollider>();
                if (col == null) col = hitTrigger.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(6f, 3f, 1f);
                col.center = new Vector3(0, 1f, 0);
            }

            AlignPointToTrack(spawnPoint);
            AlignPointToTrack(despawnPoint);
            AlignPointToTrack(hitTrigger);
            hitTrigger.rotation = Quaternion.LookRotation(dirNormalized, Vector3.up);
            if (spawnParent == null) spawnParent = spawnPoint;

            if (musicSource == null) musicSource = GetComponent<AudioSource>();
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            if (conductor != null) conductor.musicSource = musicSource;



            if (transfer != null && transfer.levelData != null)
            {
                if (transfer.fromEditor || levelData == null)
                {
                    levelData = transfer.levelData;
                    Debug.Log($"[Transfer] IsGameScene взял уровень '{levelData.fullTitle}' из редактора ({transfer.sourceScene})");
                }
            }

            if (levelData != null) PrepareLevel(levelData);
            else Debug.LogWarning("[Rhythm] Нет LevelData — ждём IsGameSceneLoader или создайте .rksl", this);

            if (autoPlayOnStart && levelData != null && levelData.music != null)
                Invoke(nameof(Play), autoPlayDelay);
        }

        void AlignPointToTrack(Transform t)
        {
            if (t == null) return;
            Vector3 p = t.position;
            p.x = Mathf.Clamp(p.x, trackMinX, trackMaxX);

            p.y = 0f;
            t.position = p;

            t.rotation = Quaternion.LookRotation(dirNormalized != Vector3.zero ? dirNormalized : Vector3.forward, Vector3.up);
        }

        Transform EnsurePoint(string pointName, Vector3 pos)
        {

            var existing = transform.Find(pointName);
            if (existing != null) return existing;
            var go = new GameObject(pointName);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            return go.transform;
        }

        public void PrepareLevel(RhythmLevelData data)
        {

            foreach (var kv in pools)
            {
                while (kv.Value.Count > 0)
                {
                    var ob = kv.Value.Dequeue();
                    if (ob != null) Destroy(ob.gameObject);
                }
            }
            foreach (var o in active) if (o != null) Destroy(o.gameObject);
            active.Clear();
            pools.Clear();

            levelData = data;
            if (data == null) return;
            data.SortByTime();

            MigrateSpeeds(data);
            sortedEvents = new List<ObstacleEvent>(data.events);
            nextEventIndex = 0;
            UpdateTrackBounds();
            UpdateDirection();


            int prefabCount = catalog != null ? catalog.Count : 0;
            List<GameObject> sourcePrefabs = null;
            if (prefabCount > 0) sourcePrefabs = catalog.GetAll();
            else if (data.obstaclePrefabs != null && data.obstaclePrefabs.Count > 0) { sourcePrefabs = data.obstaclePrefabs; prefabCount = sourcePrefabs.Count; }

            if (sourcePrefabs != null)
            {
                for (int i = 0; i < prefabCount; i++)
                {
                    var prefab = i < sourcePrefabs.Count ? sourcePrefabs[i] : null;
                    if (prefab == null) prefab = data.GetPrefab(i, catalog);
                    if (prefab == null) continue;
                    var q = new Queue<Obstacle>();
                    for (int k = 0; k < poolSizePerPrefab; k++)
                    {
                        GameObject go;
                        if (Container != null)
                            go = Container.InstantiatePrefab(prefab, spawnParent);
                        else
                            go = Instantiate(prefab, spawnParent, false);
                        go.transform.localPosition = prefab.transform.localPosition;
                        go.transform.localRotation = prefab.transform.localRotation;
                        go.transform.localScale = prefab.transform.localScale;
                        go.SetActive(false);
                        var ob = go.GetComponent<Obstacle>();
                        if (ob == null) ob = go.AddComponent<Obstacle>();
                        if (Container != null) Container.InjectGameObject(go);
                        q.Enqueue(ob);
                    }
                    pools[i] = q;
                }
            }


            if (visual != null) visual.Apply(data, videoPlayer, false, trackFloor, null);
        }

        void MigrateSpeeds(RhythmLevelData data)
        {
            for (int i = 0; i < data.events.Count; i++)
            {
                var e = data.events[i];
                if (e.speed < 0.01f)
                {
                    var prefab = data.GetPrefab(e.prefabIndex, catalog);
                    float prefSpeed = 0f;
                    if (prefab != null)
                    {
                        var ob = prefab.GetComponent<Obstacle>();
                        if (ob != null) prefSpeed = ob.baseSpeed;
                    }
                    if (prefSpeed < 0.1f) prefSpeed = defaultObstacleSpeed > 0.1f ? defaultObstacleSpeed : 12f;
                    e.speed = prefSpeed;
                    data.events[i] = e;
                }
            }

        }

        public void Play()
        {
            if (levelData == null || levelData.music == null) { Debug.LogWarning("[Rhythm] Нет LevelData/music", this); return; }
            PrepareLevel(levelData);
            if (conductor == null) { Debug.LogWarning("[Rhythm] Нет Conductor — бинд через GameInstaller", this); return; }
            conductor.Play(levelData, musicSource);
            if (videoPlayer != null)
            {

                if (!string.IsNullOrEmpty(levelData.videoPath) && System.IO.File.Exists(levelData.videoPath))
                {
                    videoPlayer.source = UnityEngine.Video.VideoSource.Url;
                    videoPlayer.url = RkslStore.GetFileUri(levelData.videoPath);
                    videoPlayer.Play();
                }
                else if (levelData.video != null) { videoPlayer.source = UnityEngine.Video.VideoSource.VideoClip; videoPlayer.clip = levelData.video; videoPlayer.Play(); }
                else { videoPlayer.Stop(); }
            }
            nextEventIndex = 0;
            isPlaying = true;
            currentTime = 0f;
            levelFinished = false;
            foreach (var o in active.ToArray()) ReturnToPool(o);
            active.Clear();

            var sm = score;
            if (sm == null) { Debug.LogWarning("[Rhythm] Нет Score — бинд через GameInstaller", this); }
            else
            {
                int total = sortedEvents != null ? sortedEvents.Count : (levelData != null ? levelData.events.Count : 0);
                sm.BeginLevel(levelData != null ? levelData.fullTitle : "", total);
            }

            if (playerController != null) playerController.SetControl(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }


        public void FailLevel()
        {
            if (levelFinished) return;
            if (score != null) score.Fail();
            FinishLevel(true);
        }

        public void Stop()
        {
            isPlaying = false;
            conductor?.Stop();
            if (videoPlayer != null) videoPlayer.Stop();
            foreach (var o in active.ToArray()) ReturnToPool(o);
            active.Clear();
            nextEventIndex = 0;
        }





        public void FinishLevel(bool failed = false)
        {
            if (levelFinished) return;
            levelFinished = true;
            isPlaying = false;
            if (failed)
            {
                conductor?.Stop();
                if (videoPlayer != null) videoPlayer.Stop();
            }
            var sm = score;
            if (sm != null && sm.isLevelActive && !sm.isFinished)
            {
                if (!failed)
                {

                    foreach (var o in active.ToArray())
                    {
                        if (o == null || !o.gameObject.activeSelf || o.countedAsMiss) continue;
                        if (o.touchedPlayer) { o.countedAsMiss = true; sm.RegisterMiss(); }
                        else sm.RegisterPerfect();
                    }
                }
            }
            foreach (var o in active.ToArray()) ReturnToPool(o);
            active.Clear();
            if (sm != null && !sm.isFinished) sm.Finish(failed);
            else if (sm == null) Debug.Log("[Rhythm] Уровень завершён (нет RhythmScoreManager)", this);
        }

        protected override void Update()
        {

            if (Input.GetKeyDown(KeyCode.Escape) && transfer != null && transfer.fromEditor && transfer.hasLevel)
            {
                string cur = SceneManager.GetActiveScene().name;
                if (cur == "IsGameScene")
                {
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    string target = transfer.sourceScene;
                    if (string.IsNullOrEmpty(target)) target = "IsLevelEditorScene";

                    bool canLoad = false;
                    string found = target;
                    for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    {
                        string p = SceneUtility.GetScenePathByBuildIndex(i);
                        string n = Path.GetFileNameWithoutExtension(p);
                        if (n == target || n == "IsLevelEditorScene" || n == "LevelEditor")
                        { found = n; canLoad = true; break; }
                    }
                    if (canLoad) SceneManager.LoadScene(found);
                    else
                    {

                        try { SceneManager.LoadScene(target); } catch { SceneManager.LoadScene("IsLevelEditorScene"); }
                    }
                    return;
                }
            }

            if (!isPlaying || levelData == null || conductor == null) return;

            if (!conductor.isPlaying && !levelFinished) { FinishLevel(false); return; }
            if (!conductor.isPlaying) return;
            currentTime = conductor.songPosition;

            while (nextEventIndex < sortedEvents.Count)
            {
                var evt = sortedEvents[nextEventIndex];
                if (currentTime >= evt.time)
                {
                    Spawn(evt);
                    nextEventIndex++;
                }
                else break;
            }
        }

        void Spawn(ObstacleEvent evt)
        {
            GameObject prefab = levelData.GetPrefab(evt.prefabIndex, catalog);
            if (prefab == null) return;

            Obstacle ob = GetFromPool(evt.prefabIndex, prefab);
            if (ob == null) return;
            if (Container != null) Container.InjectGameObject(ob.gameObject);

            Transform sp = spawnPoint != null ? spawnPoint : transform;
            float centerX = (trackMinX + trackMaxX) * 0.5f;


            ob.transform.SetParent(sp, false);
            ob.transform.localPosition = Vector3.zero;
            ob.transform.localRotation = Quaternion.identity;
            ob.transform.localScale = prefab.transform.localScale;


            if (dirNormalized.sqrMagnitude > 0.001f)
                ob.transform.rotation = Quaternion.LookRotation(dirNormalized, Vector3.up);


            if (evt.rotation != Vector3.zero) ob.transform.localRotation *= Quaternion.Euler(evt.rotation);
            if (evt.scale != Vector3.zero && evt.scale != Vector3.one) ob.transform.localScale = Vector3.Scale(ob.transform.localScale, evt.scale);


            Vector3 wpos = sp.position;
            float lane = evt.position.x;
            wpos.x = Mathf.Clamp(centerX + lane, trackMinX, trackMaxX);
            wpos.y = sp.position.y;
            ob.transform.position = wpos;


            Collider col = ob.GetComponent<Collider>();
            if (col != null)
            {
                Physics.SyncTransforms();
                Bounds wb = col.bounds;
                float half = wb.extents.x;
                if (half > (trackMaxX - trackMinX) * 0.5f + 0.01f)
                {
                    ob.transform.position = new Vector3(centerX, wpos.y, wpos.z);
                }
                else
                {

                    wb = col.bounds;
                    float left = wb.min.x, right = wb.max.x;
                    if (left < trackMinX - 0.02f) wpos.x += (trackMinX - left);
                    else if (right > trackMaxX + 0.02f) wpos.x -= (right - trackMaxX);
                    ob.transform.position = wpos;
                }
            }


            {
                Material defMat = null;
                if (levelData != null)
                {
                    if (!string.IsNullOrEmpty(levelData.defaultObstacleMaterialName))
                        defMat = catalog != null ? catalog.GetMaterial(levelData.defaultObstacleMaterialName) : null;
                    if (defMat == null && levelData.defaultObstacleMaterial != null)
                        defMat = levelData.defaultObstacleMaterial;
                }
                if (defMat != null)
                {
                    foreach (var r in ob.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.materials;
                        for (int i=0;i<mats.Length;i++)
                        {
                            mats[i] = new Material(defMat);
                        }
                        r.materials = mats;
                    }
                }
            }

            {
                Color toApply = Color.clear;
                if (evt.HasCustomColor) toApply = evt.color;
                else if (levelData != null && levelData.obstacleColor != Color.white) toApply = levelData.obstacleColor;
                else if (levelData != null && levelData.obstacleColor == Color.white) toApply = Color.white;

                if (toApply != Color.clear)
                {
                    foreach (var r in ob.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.materials;
                        for (int i=0;i<mats.Length;i++)
                        {
                            if (mats[i].HasProperty("_Color"))
                            {
                                mats[i] = new Material(mats[i]);
                                mats[i].color = toApply;
                            }
                            else if (mats[i].HasProperty("_BaseColor"))
                            {
                                mats[i] = new Material(mats[i]);
                                mats[i].SetColor("_BaseColor", toApply);
                            }
                        }
                        r.materials = mats;
                    }
                }
            }

            ob.gameObject.SetActive(true);
            ob.noteId = nextEventIndex;
            float spd = evt.speed > 0.01f ? evt.speed : (ob.baseSpeed > 0.01f ? ob.baseSpeed : defaultObstacleSpeed);
            ob.Init(this, dirNormalized, spd, despawnPoint, evt.time);
            active.Add(ob);
        }

        Obstacle GetFromPool(int index, GameObject prefab)
        {
            if (pools.TryGetValue(index, out var q) && q.Count > 0)
            {
                var ob = q.Dequeue();
                if (ob == null) return CreateNew(prefab);
                return ob;
            }
            return CreateNew(prefab);
        }

        Obstacle CreateNew(GameObject prefab)
        {
            GameObject go = Container != null
                ? Container.InstantiatePrefab(prefab, spawnParent)
                : Instantiate(prefab, spawnParent, false);
            go.transform.localPosition = prefab.transform.localPosition;
            go.transform.localRotation = prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale;
            var ob = go.GetComponent<Obstacle>();
            if (ob == null) ob = go.AddComponent<Obstacle>();
            if (Container != null) Container.InjectGameObject(go);
            return ob;
        }

        public void ReturnToPool(Obstacle ob)
        {
            if (ob == null) return;
            active.Remove(ob);
            ob.gameObject.SetActive(false);
            ob.transform.SetParent(spawnParent != null ? spawnParent : transform);

            int idx = 0;

            int gCount = catalog != null ? catalog.Count : 0;
            if (gCount > 0)
            {
                var all = catalog.GetAll();
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && ob.name.Contains(all[i].name)) { idx = i; break; }
            }
            else if (levelData != null && levelData.obstaclePrefabs != null)
            {
                for (int i = 0; i < levelData.obstaclePrefabs.Count; i++)
                    if (levelData.obstaclePrefabs[i] != null && ob.name.Contains(levelData.obstaclePrefabs[i].name)) { idx = i; break; }
            }
            if (!pools.ContainsKey(idx)) pools[idx] = new Queue<Obstacle>();
            pools[idx].Enqueue(ob);
        }


        public void PreviewSpawnAtBeat(float beat, int prefabIndex)
        {
            if (levelData == null) return;
            var evt = ObstacleEvent.Create(beat, prefabIndex, Vector3.zero);
            evt.time = levelData.BeatToTime(beat);

            var prefab = levelData.GetPrefab(prefabIndex, catalog);
            if (prefab != null)
            {
                var obc = prefab.GetComponent<Obstacle>();
                if (obc != null) evt.speed = obc.baseSpeed;
            }
            if (evt.speed < 0.1f) evt.speed = defaultObstacleSpeed;
            Spawn(evt);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            UpdateTrackBounds();

            if (trackFloor != null)
            {
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
                Vector3 center = trackFloor.position;
                Vector3 size = new Vector3(Mathf.Abs(trackMaxX - trackMinX) + 0.1f, 0.2f, trackFloor.localScale.z);
                Gizmos.DrawCube(center, size);
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
                Gizmos.DrawWireCube(center, size);
            }
            else
            {
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
                float w = Mathf.Abs(trackMaxX - trackMinX);
                Gizmos.DrawCube(new Vector3((trackMinX + trackMaxX) * 0.5f, 0f, 25f), new Vector3(w, 0.2f, 60f));
            }
            if (spawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(spawnPoint.position, Vector3.one * 1.2f);
                Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + dirNormalized * 3f);
                UnityEditor.Handles.Label(spawnPoint.position + Vector3.up * 1.5f, "SPAWN");

                Gizmos.color = new Color(0, 1, 0, 0.25f);
                Gizmos.DrawLine(new Vector3(trackMinX, spawnPoint.position.y, spawnPoint.position.z), new Vector3(trackMaxX, spawnPoint.position.y, spawnPoint.position.z));
            }
            if (despawnPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(despawnPoint.position, Vector3.one * 1.2f);
                UnityEditor.Handles.Label(despawnPoint.position + Vector3.up * 1.5f, "DESPAWN");
            }
            if (hitTrigger != null)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.9f);
                Gizmos.DrawWireCube(hitTrigger.position, new Vector3(6f, 2f, 1f));
                UnityEditor.Handles.Label(hitTrigger.position + Vector3.up * 2.2f, "HIT TRIGGER — тут нота в момент бита");
                Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.25f);
                Gizmos.DrawLine(hitTrigger.position + Vector3.left * 3f, hitTrigger.position + Vector3.right * 3f);
            }
            if (spawnPoint != null && despawnPoint != null)
            {
                Gizmos.color = new Color(0, 1, 0.6f, 0.35f);
                Gizmos.DrawLine(spawnPoint.position, despawnPoint.position);
                if (hitTrigger != null)
                {
                    Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.5f);
                    Gizmos.DrawLine(spawnPoint.position, hitTrigger.position);
                    UnityEditor.Handles.Label(Vector3.Lerp(spawnPoint.position, hitTrigger.position, 0.5f) + Vector3.up * 0.5f, $"{GetSpawnToHitDistance():0.0}м");
                }
            }
        }
#endif
    }
}
