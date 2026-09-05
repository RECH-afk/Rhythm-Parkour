using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 3D превью пролёта на сцене — показывает как препятствия будут ехать в уровне.
/// Привязан к времени TimelineUI: двигаешь таймлайн — препятствия едут вперёд/назад.
/// Вешай на тот же GameObject где TimelineUI, или на отдельный PreviewRoot в сцене IsLevelEditorScene.
/// Требует: TimelineUI + RhythmParkourManager (для spawn/despawn точек)
/// </summary>
public class TimelinePreview : MonoBehaviour
{
    [Header("Ссылки")]
    public TimelineUI timelineUI;
    public RhythmParkourManager manager;
    [Tooltip("Куда спавнить гостов. Если пусто — создастся автоматически под manager.spawnParent")]
    public Transform previewRoot;

    [Header("Настройки превью")]
    [Tooltip("Включено ли превью сразу при старте (можно включить кнопкой)")]
    public bool previewEnabled = false;
    [Header("Кнопка")]
    public Button previewToggleButton;
    public TextMeshProUGUI previewToggleLabel;
    public Color enabledColor = new Color(0.2f, 0.7f, 0.3f, 1f);
    public Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    bool buttonSetupDone = false;
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

    // внутр
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

    void Awake()
    {
        if (timelineUI == null) timelineUI = FindObjectOfType<TimelineUI>();
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        EnsureRoot();
        EnsureDirection();
    }

    void Start()
    {
        SetupButton();
        // пробуем ещё через кадр если не нашли (Canvas может создаваться позже)
        if (previewToggleButton == null) StartCoroutine(SetupButtonDelayed());
        UpdateToggleVisual();
        // если превью выключено — прячем гостов сразу
        if (!previewEnabled) SetAllVisible(false);
    }

    void OnEnable() => EnsureRoot();
    void OnDisable()
    {
        if (hideWhenPlaying) ClearGhosts();
        if (previewToggleButton != null) previewToggleButton.onClick.RemoveListener(TogglePreview);
    }

    void SetupButton()
    {
        if (buttonSetupDone && previewToggleButton != null) return;
        if (previewToggleButton == null)
        {
            // пробуем найти уже существующую кнопку Preview
            var go = GameObject.Find("ButtonPreview");
            if (go != null) previewToggleButton = go.GetComponent<Button>();
            if (previewToggleButton == null)
            {
                var go2 = GameObject.Find("Button_Preview");
                if (go2 != null) previewToggleButton = go2.GetComponent<Button>();
            }
            // поиск по всем кнопкам с текстом Autoscroll — более надёжно чем по имени
            Button autoBtn = null;
            if (previewToggleButton == null)
            {
                var allBtns = FindObjectsOfType<Button>(true);
                foreach (var b in allBtns)
                {
                    var txt = b.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null && txt.text.ToLower().Contains("autoscroll"))
                    {
                        autoBtn = b;
                        break;
                    }
                }
                if (autoBtn == null)
                    allBtns = FindObjectsOfType<Button>(false);
            }
            // если не нашли — клонируем ButtonAutoscroll
            if (previewToggleButton == null)
            {
                GameObject autoBtnGO = null;
                if (autoBtn != null) autoBtnGO = autoBtn.gameObject;
                else autoBtnGO = GameObject.Find("ButtonAutoscroll");
                if (autoBtnGO != null)
                {
                    var parent = autoBtnGO.transform.parent;
                    var clone = Instantiate(autoBtnGO, parent);
                    clone.name = "ButtonPreview";
                    clone.transform.SetSiblingIndex(autoBtnGO.transform.GetSiblingIndex() + 1);
                    previewToggleButton = clone.GetComponent<Button>();
                    // чистим старые слушатели
                    previewToggleButton.onClick.RemoveAllListeners();
                    var lbl = clone.GetComponentInChildren<TextMeshProUGUI>();
                    if (lbl != null) lbl.text = "Preview OFF";
                    clone.SetActive(true);
                    Debug.Log("[Preview] Создана кнопка ButtonPreview", clone);
                }
                else
                {
                    Debug.LogWarning("[Preview] Не найден ButtonAutoscroll — создай кнопку вручную и назначь previewToggleButton", this);
                }
            }
            if (previewToggleButton != null && previewToggleLabel == null)
                previewToggleLabel = previewToggleButton.GetComponentInChildren<TextMeshProUGUI>();
        }
        if (previewToggleButton != null)
        {
            previewToggleButton.onClick.RemoveListener(TogglePreview);
            previewToggleButton.onClick.AddListener(TogglePreview);
            buttonSetupDone = true;
            UpdateToggleVisual();
            Debug.Log($"[Preview] Кнопка подключена: {previewToggleButton.name} -> TogglePreview", previewToggleButton);
        }
        if (previewToggleLabel == null && previewToggleButton != null)
            previewToggleLabel = previewToggleButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    System.Collections.IEnumerator SetupButtonDelayed()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (previewToggleButton == null) { SetupButton(); UpdateToggleVisual(); }
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
            lastEventCount = -1; // форсим пересоздание
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
        if (previewToggleButton != null && previewToggleButton.image != null)
            previewToggleButton.image.color = previewEnabled ? enabledColor : disabledColor;
    }

    public void ForceRefresh()
    {
        lastEventCount = -1;
        lastPrefabIndices.Clear();
        lastTime = float.NaN;
    }

    void OnValidate()
    {
        // в эдиторе чтобы кнопка сразу отражала состояние
        if (previewToggleButton != null && previewToggleLabel != null)
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
            // копируем логику из Manager
            if (manager.spawnPoint != null) spawnPoint = manager.spawnPoint;
            else spawnPoint = GameObject.Find("SpawnPoint")?.transform;
            if (manager.despawnPoint != null) despawnPoint = manager.despawnPoint;
            else despawnPoint = GameObject.Find("DespawnPoint")?.transform;
            if (manager.hitTrigger != null) hitTrigger = manager.hitTrigger;
            else hitTrigger = GameObject.Find("HitTrigger")?.transform ?? GameObject.Find("Trigger")?.transform;

            Vector3 dir;
            if (spawnPoint != null && despawnPoint != null) dir = despawnPoint.position - spawnPoint.position;
            else dir = manager.moveDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = new Vector3(0,0,-1);
            dirNormalized = dir.normalized;
        }
        else
        {
            spawnPoint = GameObject.Find("SpawnPoint")?.transform;
            despawnPoint = GameObject.Find("DespawnPoint")?.transform;
            hitTrigger = GameObject.Find("HitTrigger")?.transform;
            if (spawnPoint != null && despawnPoint != null)
                dirNormalized = (despawnPoint.position - spawnPoint.position).normalized;
            else dirNormalized = new Vector3(0,0,-1);
            dirNormalized.y = 0f;
            dirNormalized.Normalize();
            if (dirNormalized.sqrMagnitude < 0.001f) dirNormalized = Vector3.forward;
        }
    }

    void Update()
    {
        // ленивая привязка кнопки если не нашлась в Start
        if (!buttonSetupDone && previewToggleButton == null && Time.frameCount % 30 == 0) SetupButton();

        if (!previewEnabled) { if (ghosts.Count>0) SetAllVisible(false); return; }
        if (timelineUI == null) timelineUI = FindObjectOfType<TimelineUI>();
        if (manager == null) manager = FindObjectOfType<RhythmParkourManager>();
        if (previewRoot == null) EnsureRoot();
        if (timelineUI == null) return;

        // если идёт реальная игра — прячем гостов чтобы не мешали
        if (hideWhenPlaying && manager != null && manager.isPlaying)
        {
            if (ghosts.Count>0) SetAllVisible(false);
            return;
        }

        var level = timelineUI.levelData;
        if (level == null)
        {
            // пробуем взять из manager
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
                // также проверяем имя госта на случай ручной смены префаба в каталоге
                if (i < ghosts.Count && ghosts[i] != null)
                {
                    var pf = level.GetPrefab(level.events[i].prefabIndex);
                    if (pf == null) pf = GlobalObstacleCatalog.GetPrefab(level.events[i].prefabIndex);
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
            var prefab = level.GetPrefab(ev.prefabIndex);
            if (prefab == null) prefab = GlobalObstacleCatalog.GetPrefab(ev.prefabIndex);
            if (prefab == null) continue;

            var go = Instantiate(prefab, previewRoot);
            go.name = $"Preview_{i:000}_{prefab.name}";

            // отключаем логику Obstacle чтобы не летали сами
            var obs = go.GetComponent<Obstacle>();
            if (obs != null) Destroy(obs);
            foreach (var o in go.GetComponentsInChildren<Obstacle>()) Destroy(o);

            // убираем коллайдеры/триггеры чтобы не наносили урон в превью
            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.detectCollisions = false; }

            // делаем полупрозрачным + пер-нотный цвет
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
                            if (targetCol != Color.clear) baseC.a = ghostAlpha; // если кастом — полный alpha * ghostAlpha уже
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
        // Y как у спавна
        float lockedY = spawnPos.y;
        // центр дорожки X — как в Manager
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
                var pf = level.GetPrefab(ev.prefabIndex);
                if (pf == null) pf = GlobalObstacleCatalog.GetPrefab(ev.prefabIndex);
                if (pf != null) { var ob = pf.GetComponent<Obstacle>(); if (ob) speed = ob.baseSpeed; }
                if (speed < 0.1f) speed = manager != null ? manager.defaultObstacleSpeed : 12f;
            }

            float spawnT = ev.time;
            // travel до хита
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

            // видимость: показываем только если вблизи текущего времени (чтобы не спамить всю трассу)
            bool visible = curTime >= spawnT - visibleBehind && curTime <= despawnT + 0.5f;
            // но для превью достаточно показать в окне ±visibleAhead/Behind от hit
            // альтернативно: visible = Mathf.Abs(hitT - curTime) <= visibleAhead || (curTime >= spawnT && curTime <= despawnT)
            // делаем мягкое: если hit далеко — скрываем
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
            // lane offset (position.x) — как в Manager.Spawn
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

            // поворот вдоль дорожки + rotation из ивента
            if (dir.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (ev.rotation != Vector3.zero)
                go.transform.localRotation *= Quaternion.Euler(ev.rotation);
            // скейл
            var prefabForScale = level.GetPrefab(ev.prefabIndex);
            if (prefabForScale == null) prefabForScale = GlobalObstacleCatalog.GetPrefab(ev.prefabIndex);
            Vector3 baseScale = prefabForScale != null ? prefabForScale.transform.localScale : Vector3.one;
            if (ev.scale != Vector3.zero && ev.scale != Vector3.one)
                go.transform.localScale = Vector3.Scale(baseScale, ev.scale);
            else go.transform.localScale = baseScale;

            // подсветка выбранной ноты — делаем ярче
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
