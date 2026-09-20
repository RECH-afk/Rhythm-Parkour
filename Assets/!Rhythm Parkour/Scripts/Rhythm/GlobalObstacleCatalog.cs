using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Rhythm
{
    [CreateAssetMenu(menuName = "Rhythm Parkour/Global Obstacle Catalog", fileName = "GlobalObstacleCatalog")]
    public class GlobalObstacleCatalog : ScriptableObject
    {
        [Tooltip("Общий пул объектов для всех уровней (как в GD). Порядок важен — индекс сохраняется в .rksl")]
        public List<GameObject> prefabs = new List<GameObject>();
        [Tooltip("Доступные материалы для препятствий. Первый = дефолтный")]
        public List<Material> obstacleMaterials = new List<Material>();

#if UNITY_EDITOR
        static void EnsureAssetExists()
        {
            string[] guids = AssetDatabase.FindAssets("t:GlobalObstacleCatalog");
            if (guids.Length > 0) return;

            string resourcesDir = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesDir))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string path = "Assets/Resources/GlobalObstacleCatalog.asset";
            if (System.IO.File.Exists(path)) return;

            var asset = CreateInstance<GlobalObstacleCatalog>();

            TryAutoFill(asset);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GlobalCatalog] Создан {path} — перетащи туда префабы препятствий (порядок = ID)");
        }

        static void TryAutoFill(GlobalObstacleCatalog catalog)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/!Rhythm Parkour/Prefabs/Obstacles" });
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/!Rhythm Parkour/Prefabs" });
            List<GameObject> found = new List<GameObject>();
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null && go.GetComponent<Obstacle>() != null)
                    found.Add(go);
            }

            found.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            catalog.prefabs = found;
            if (found.Count > 0)
                Debug.Log($"[GlobalCatalog] Авто-заполнено {found.Count} префабов из Obstacles/");

            var matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/!Rhythm Parkour/Materials/Obstacles" });
            List<Material> mats = new List<Material>();
            foreach (var g in matGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat != null) mats.Add(mat);
            }
            mats.Sort((a,b)=> string.Compare(a.name,b.name, System.StringComparison.Ordinal));
            catalog.obstacleMaterials = mats;
            if (mats.Count>0) Debug.Log($"[GlobalCatalog] Авто-заполнено {mats.Count} материалов");
        }

        [InitializeOnLoadMethod]
        static void EditorInit()
        {
            EditorApplication.delayCall += () =>
            {
                var catalog = AssetDatabase.LoadAssetAtPath<GlobalObstacleCatalog>(
                    "Assets/Resources/GlobalObstacleCatalog.asset");
                if (catalog == null) return;

                if (catalog.prefabs == null || catalog.prefabs.Count == 0)
                {

                    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/!Rhythm Parkour/Prefabs/Obstacles" });
                    if (guids.Length > 0)
                        Debug.LogWarning("[GlobalCatalog] Каталог пуст! Открой Assets/Resources/GlobalObstacleCatalog и перетащи префабы (или они заполнятся автоматически при создании). Пока уровни будут показывать 'нет префабов'.");
                }
            };
        }
#endif

public int Count => prefabs != null ? prefabs.Count : 0;

        public GameObject GetPrefab(int index)
        {

            if (prefabs == null || prefabs.Count == 0) return null;
            if (index < 0 || index >= prefabs.Count) return null;
            return prefabs[index];
        }

        public int ClampIndex(int index)
        {
            int c = Count;
            if (c == 0) return 0;
            return Mathf.Clamp(index, 0, c - 1);
        }

        public static Color GetColor(int index)
        {
            float h = (index * 0.37f) % 1f;
            return Color.HSVToRGB(h, 0.78f, 0.92f);
        }

        public List<GameObject> GetAll() => prefabs ?? new List<GameObject>();

        public Material GetMaterial(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (obstacleMaterials != null)
            {
                foreach (var m in obstacleMaterials)
                    if (m != null && m.name == name) return m;
            }
            return null;
        }
        public List<Material> GetAllMaterials() => obstacleMaterials ?? new List<Material>();
        public Material GetDefaultMaterial() =>
            obstacleMaterials != null && obstacleMaterials.Count > 0 ? obstacleMaterials[0] : null;
    }
}
