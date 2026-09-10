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
    public class TimelinePreview : RKSBehaviour
    {
        [Header("References")]
        [HideInInspector]
        [InjectOptional] public TimelineUI timelineUI;
        [HideInInspector]
        [InjectOptional] public RhythmParkourManager manager;
        [HideInInspector]
        [InjectOptional] public GlobalObstacleCatalog catalog;
        [Tooltip("Куда спавнить гостов. Если пусто — создастся автоматически под manager.spawnParent")]
        public Transform previewRoot;

        [Header("Preview Settings")]
        [Tooltip("Включено ли превью сразу при старте (можно включить кнопкой)")]
        public bool previewEnabled = false;
        [Header("Indicator")]
        public TextMeshProUGUI previewToggleLabel;
        public Color enabledColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        public Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [Tooltip("Показывать препятствия за N секунд до хита и после")]
        public float visibleAhead = 10f;
        public float visibleBehind = 2f;
        [Tooltip("Скрывать гостов когда уровень реально играет (manager.isPlaying) чтобы не дублировать")]
        public bool hideWhenPlaying = true;
        [Tooltip("Полупрозрачные госты")]
        public bool useGhostMaterial = true;
        [Range(0.1f,1f)] public float ghostAlpha = 0.55f;
        [Tooltip("Обновлять каждый кадр (иначе только при изменении времени)")]
        public bool updateEveryFrame = true;

List<GameObject> ghosts = new List<GameObject>();
        List<Material[]> ghostMats = new List<Material[]>();
        List<float> ghostBaseAlpha = new List<float>();
        float lastTime = float.NaN;
        int lastEventCount = -1;
        List<int> lastPrefabIndices = new List<int>();
        Vector3 dirNormalized = Vector3.forward;
        Transform spawnPoint;
        Transform despawnPoint;
        Transform hitTrigger;

        protected override void OnInjected()
        {
            EnsureRoot();
            EnsureDirection();
        }

        protected override void OnReady()
        {
            UpdateToggleVisual();

            if (!previewEnabled) SetAllVisible(false);
        }

        protected override void OnDisposed()
        {
            if (hideWhenPlaying) ClearGhosts();
        }

        public void TogglePreview()
        {
            previewEnabled = !previewEnabled;
            UpdateToggleVisual();
            if (!previewEnabled)
            {
                SetAllVisible(false);
                ClearGhosts();
            }
            else
            {
                lastEventCount = -1;
            }
            Debug.Log($"[Preview] {(previewEnabled ? "ON" : "OFF")}", this);
        }

        public void SetPreviewEnabled(bool v)
        {
            if (previewEnabled == v) return;
            previewEnabled = v;
            UpdateToggleVisual();
            if (!v) { SetAllVisible(false); ClearGhosts(); }
            else lastEventCount = -1;
        }

        void UpdateToggleVisual()
        {
            if (previewToggleLabel != null)
                previewToggleLabel.text = previewEnabled ? "Preview ON" : "Preview OFF";
        }

        public void ForceRefresh()
        {
            lastEventCount = -1;
            lastPrefabIndices.Clear();
            lastTime = float.NaN;
        }

        void OnValidate()
        {

            if (previewToggleLabel != null)
                UpdateToggleVisual();
        }

        void EnsureRoot()
        {
            if (previewRoot != null) return;
            if (manager != null && manager.spawnParent != null)
            {
                var go = new GameObject("~PreviewRoot");
                go.transform.SetParent(manager.spawnParent.parent != null ? manager.spawnParent.parent : manager.transform, false);
                previewRoot = go.transform;
            }
            else
            {
                var go = new GameObject("~PreviewRoot");
                previewRoot = go.transform;
            }
        }

        void EnsureDirection()
        {

            if (manager != null)
            {

                spawnPoint = manager.spawnPoint;
                despawnPoint = manager.despawnPoint;
                hitTrigger = manager.hitTrigger;

                Vector3 dir;
                if (spawnPoint != null && despawnPoint != null) dir = despawnPoint.position - spawnPoint.position;
                else dir = manager.moveDirection;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = new Vector3(0,0,-1);
                dirNormalized = dir.normalized;
            }
            else
            {

                dirNormalized = Vector3.forward;
            }
        }

        protected override void Update()
        {
            if (!previewEnabled) { if (ghosts.Count>0) SetAllVisible(false); return; }
            if (previewRoot == null) EnsureRoot();
            if (timelineUI == null) return;

if (hideWhenPlaying && manager != null && manager.isPlaying)
            {
                if (ghosts.Count>0) SetAllVisible(false);
                return;
            }

            var level = timelineUI.levelData;
            if (level == null)
            {

                if (manager != null) level = manager.levelData;
                if (level == null) { ClearGhosts(); return; }
            }

            float cur = timelineUI.GetCurrentTime();

            bool needRebuild = false;
            if (level.events.Count != lastEventCount) needRebuild = true;
            else if (lastPrefabIndices.Count != level.events.Count) needRebuild = true;
            else
            {
                for (int i=0;i<level.events.Count;i++)
                {
                    if (lastPrefabIndices[i] != level.events[i].prefabIndex) { needRebuild = true; break; }

                    if (i < ghosts.Count && ghosts[i] != null)
                    {
                        var pf = level.GetPrefab(level.events[i].prefabIndex, catalog);
                        if (pf == null) pf = catalog.GetPrefab(level.events[i].prefabIndex);
                        if (pf != null && !ghosts[i].name.Contains(pf.name)) { needRebuild = true; break; }
                    }
                }
            }
            if (needRebuild) RebuildGhosts(level);

            if (!updateEveryFrame && Mathf.Abs(cur - lastTime) < 0.015f && !needRebuild) return;
            lastTime = cur;
            lastEventCount = level.events.Count;
            lastPrefabIndices.Clear();
            for (int i=0;i<level.events.Count;i++) lastPrefabIndices.Add(level.events[i].prefabIndex);

            EnsureDirection();
            UpdateGhostPositions(level, cur);
        }

        void SetAllVisible(bool v)
        {
            foreach (var g in ghosts) if (g!=null) g.SetActive(v);
        }

        void ClearGhosts()
        {
            foreach (var g in ghosts) if (g!=null) Destroy(g);
            ghosts.Clear();
            ghostMats.Clear();
            ghostBaseAlpha.Clear();
            lastEventCount = -1;
        }

        void RebuildGhosts(RhythmLevelData level)
        {
            ClearGhosts();
            if (level == null || level.events.Count==0) return;
            EnsureRoot();
            for (int i=0;i<level.events.Count;i++)
            {
                var ev = level.events[i];
                var prefab = level.GetPrefab(ev.prefabIndex, catalog);
                if (prefab == null) prefab = catalog.GetPrefab(ev.prefabIndex);
                if (prefab == null) continue;

                var go = Instantiate(prefab, previewRoot);
                go.name = $"Preview_{i:000}_{prefab.name}";

var obs = go.GetComponent<Obstacle>();
                if (obs != null) Destroy(obs);
                foreach (var o in go.GetComponentsInChildren<Obstacle>()) Destroy(o);

foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.detectCollisions = false; }

if (useGhostMaterial)
                {
                    var rends = go.GetComponentsInChildren<Renderer>();
                    List<Material> mats = new List<Material>();
                    Color targetCol = Color.clear;
                    if (ev.HasCustomColor) targetCol = ev.color;
                    else if (level.obstacleColor != Color.white) targetCol = level.obstacleColor;
                    foreach (var r in rends)
                    {
                        var newMats = r.materials;
                        for (int m=0;m<newMats.Length;m++)
                        {
                            if (newMats[m].HasProperty("_Color"))
                            {
                                Color baseC = targetCol != Color.clear ? targetCol : newMats[m].color;
                                baseC.a *= ghostAlpha;
                                if (targetCol != Color.clear) baseC.a = ghostAlpha;
                                if (ev.HasCustomColor) baseC = new Color(targetCol.r, targetCol.g, targetCol.b, ghostAlpha);
                                else if (targetCol != Color.clear) baseC = new Color(targetCol.r, targetCol.g, targetCol.b, ghostAlpha * 0.9f);
                                else { Color c = newMats[m].color; c.a *= ghostAlpha; baseC = c; }
                                newMats[m] = new Material(newMats[m]);
                                newMats[m].color = baseC;
                                if (newMats[m].HasProperty("_Surface")) newMats[m].SetFloat("_Surface", 1);
                                newMats[m].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                newMats[m].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                newMats[m].SetInt("_ZWrite", 0);
                                newMats[m].DisableKeyword("_ALPHATEST_ON");
                                newMats[m].EnableKeyword("_ALPHABLEND_ON");
                                newMats[m].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                                newMats[m].renderQueue = 3000;
                            }
                            else if (newMats[m].HasProperty("_BaseColor"))
                            {
                                Color baseC = targetCol != Color.clear ? targetCol : (Color)newMats[m].GetColor("_BaseColor");
                                baseC.a = ghostAlpha;
                                newMats[m] = new Material(newMats[m]);
                                newMats[m].SetColor("_BaseColor", baseC);
                                newMats[m].renderQueue = 3000;
                            }
                        }
                        r.materials = newMats;
                        mats.AddRange(newMats);
                    }
                    ghostMats.Add(mats.ToArray());
                }
                else ghostMats.Add(null);

                go.SetActive(false);
                ghosts.Add(go);
            }
        }

        void UpdateGhostPositions(RhythmLevelData level, float curTime)
        {
            if (spawnPoint == null) EnsureDirection();
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : (manager != null ? manager.transform.position : previewRoot.position);

            float lockedY = spawnPos.y;

            float centerX = 0f;
            if (manager != null) centerX = (manager.trackMinX + manager.trackMaxX)*0.5f;
            else if (spawnPoint != null) centerX = spawnPoint.position.x;

            Vector3 dir = dirNormalized;
            float spawnToDespawnDist = 60f;
            if (spawnPoint != null && despawnPoint != null) spawnToDespawnDist = Vector3.Distance(spawnPoint.position, despawnPoint.position);

            for (int i=0;i<ghosts.Count && i<level.events.Count;i++)
            {
                var go = ghosts[i];
                if (go == null) continue;
                var ev = level.events[i];
                float speed = ev.speed;
                if (speed < 0.1f)
                {
                    var pf = level.GetPrefab(ev.prefabIndex, catalog);
                    if (pf == null) pf = catalog.GetPrefab(ev.prefabIndex);
                    if (pf != null) { var ob = pf.GetComponent<Obstacle>(); if (ob) speed = ob.baseSpeed; }
                    if (speed < 0.1f) speed = manager != null ? manager.defaultObstacleSpeed : 12f;
                }

                float spawnT = ev.time;

                float travelToHit = 0f;
                if (manager != null) travelToHit = manager.GetTravelTime(speed);
                else
                {
                    float d = 52f;
                    if (spawnPoint != null && hitTrigger != null) d = Vector3.Distance(spawnPoint.position, hitTrigger.position);
                    travelToHit = d / Mathf.Max(1f, speed);
                }
                float hitT = spawnT + travelToHit;
                float despawnT = spawnT + spawnToDespawnDist / Mathf.Max(1f, speed);

bool visible = curTime >= spawnT - visibleBehind && curTime <= despawnT + 0.5f;

if (Mathf.Abs(hitT - curTime) > visibleAhead + 1f && !(curTime >= spawnT && curTime <= despawnT))
                    visible = false;

                if (!visible)
                {
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                if (!go.activeSelf) go.SetActive(true);

                float elapsed = curTime - spawnT;
                if (elapsed < 0) elapsed = 0;
                Vector3 basePos = spawnPos + dir * speed * elapsed;

                float laneOff = ev.position.x;
                if (manager != null)
                {
                    basePos.x = Mathf.Clamp(centerX + laneOff, manager.trackMinX, manager.trackMaxX);
                    basePos.y = lockedY;
                }
                else
                {
                    basePos.x = centerX + laneOff;
                    basePos.y = lockedY;
                }
                go.transform.position = basePos;

if (dir.sqrMagnitude > 0.001f)
                    go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                if (ev.rotation != Vector3.zero)
                    go.transform.localRotation *= Quaternion.Euler(ev.rotation);

                var prefabForScale = level.GetPrefab(ev.prefabIndex, catalog);
                if (prefabForScale == null) prefabForScale = catalog.GetPrefab(ev.prefabIndex);
                Vector3 baseScale = prefabForScale != null ? prefabForScale.transform.localScale : Vector3.one;
                if (ev.scale != Vector3.zero && ev.scale != Vector3.one)
                    go.transform.localScale = Vector3.Scale(baseScale, ev.scale);
                else go.transform.localScale = baseScale;

bool isSelected = timelineUI.GetSelectedIndex() == i;
                if (useGhostMaterial && isSelected)
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        foreach (var m in r.materials) if (m.HasProperty("_Color")) m.color = Color.Lerp(m.color, Color.yellow, 0.35f);
                    }
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!previewEnabled) return;
            EnsureDirection();
            if (spawnPoint && despawnPoint)
            {
                Gizmos.color = new Color(0,1,0.6f,0.25f);
                Gizmos.DrawLine(spawnPoint.position, despawnPoint.position);
                if (hitTrigger)
                {
                    Gizmos.color = new Color(1,0.85f,0.15f,0.5f);
                    Gizmos.DrawLine(spawnPoint.position, hitTrigger.position);
                }
            }
        }
#endif
    }
}
