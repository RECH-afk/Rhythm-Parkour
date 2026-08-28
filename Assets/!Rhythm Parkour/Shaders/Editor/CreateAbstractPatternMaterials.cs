using UnityEngine;
using UnityEditor;
using System.IO;

public static class CreateAbstractPatternMaterials
{
    [MenuItem("Tools/Abstract Pattern/Create Demo Materials")]
    public static void Create()
    {
        string shaderName = "Custom/AbstractPatternURP";
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[AbstractPattern] Шейдер {shaderName} не найден! Убедись что файл AbstractPatternURP.shader скомпилировался без ошибок.");
            return;
        }

        string folder = "Assets/!Rhythm Parkour/Materials/AbstractPattern";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "!Rhythm Parkour/Materials/AbstractPattern"));
            AssetDatabase.Refresh();
        }

        CreatePreset(folder, shader, "Abstract_Waves_Neon", 1,
            new Color(0.05f, 0.05f, 0.08f), new Color(0.2f, 0.6f, 1f), new Color(1f, 0.2f, 0.6f), new Color(0.3f, 1f, 0.5f), new Color(1f, 0.9f, 0.2f),
            12f, 0.25f, 1f, 0f, 4f, 0.5f, 0.15f, 0);

        CreatePreset(folder, shader, "Abstract_Stripes_Synthwave", 0,
            new Color(0.08f, 0.02f, 0.12f), new Color(1f, 0f, 0.8f), new Color(0f, 0.9f, 1f), new Color(1f, 0.9f, 0.2f), new Color(0.5f, 0.2f, 1f),
            18f, 0.4f, 1.2f, 45f, 4f, 0.5f, 0.2f, 0);

        CreatePreset(folder, shader, "Abstract_Circles_Tech", 2,
            new Color(0.03f, 0.03f, 0.03f), new Color(0f, 1f, 0.6f), new Color(0f, 0.8f, 1f), new Color(1f, 1f, 1f), new Color(0f, 1f, 0.3f),
            8f, 0.35f, 1.5f, 0f, 3f, 0.3f, 0.05f, 4);

        CreatePreset(folder, shader, "Abstract_Truchet_Matrix", 5,
            new Color(0.02f, 0.08f, 0.02f), new Color(0.1f, 1f, 0.3f), new Color(0f, 0.6f, 0.2f), new Color(0.8f, 1f, 0.4f), new Color(0f, 0.4f, 0.1f),
            6f, 0.5f, 1f, 0f, 2f, 0.2f, 0.08f, 1);

        CreatePreset(folder, shader, "Abstract_Flow_Psycho", 6,
            new Color(0.1f, 0f, 0.15f), new Color(1f, 0.2f, 0.9f), new Color(0.2f, 0.8f, 1f), new Color(1f, 0.6f, 0.1f), new Color(0.6f, 0.1f, 1f),
            10f, 0.3f, 2f, 20f, 5f, 0.8f, 0.35f, 1);

        CreatePreset(folder, shader, "Abstract_Dots_Pop", 3,
            new Color(0.95f, 0.95f, 0.92f), new Color(1f, 0.15f, 0.15f), new Color(0.15f, 0.15f, 1f), new Color(1f, 0.9f, 0.1f), new Color(0.1f, 0.9f, 0.4f),
            15f, 0.6f, 1f, 0f, 2f, 0.2f, 0f, 0);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AbstractPattern] Готово! Материалы в {folder}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folder);
    }

    static void CreatePreset(string folder, Shader shader, string name, int pattern,
        Color bg, Color a, Color b, Color c, Color d,
        float scale, float thickness, float contrast, float angle, float waveFreq, float waveAmp, float warp, int uvMode)
    {
        string path = $"{folder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", bg);
        mat.SetColor("_ColorA", a);
        mat.SetColor("_ColorB", b);
        mat.SetColor("_ColorC", c);
        mat.SetColor("_ColorD", d);
        mat.SetFloat("_Pattern", pattern);
        mat.SetFloat("_Scale", scale);
        mat.SetFloat("_Thickness", thickness);
        mat.SetFloat("_Contrast", contrast);
        mat.SetFloat("_Angle", angle);
        mat.SetFloat("_WaveFreq", waveFreq);
        mat.SetFloat("_WaveAmp", waveAmp);
        mat.SetFloat("_WarpAmount", warp);
        mat.SetFloat("_WarpScale", 3f);
        mat.SetFloat("_WarpSpeed", 0.5f);
        mat.SetFloat("_UVMode", uvMode);
        mat.SetFloat("_Speed", 1f);
        mat.SetFloat("_SpeedX", 0f);
        mat.SetFloat("_SpeedY", 0.5f);
        mat.SetFloat("_Smoothness", 0.05f);
        mat.SetFloat("_EmissionStrength", 0.3f);
        mat.SetFloat("_Alpha", 1f);
        EditorUtility.SetDirty(mat);
        Debug.Log($"Created {path}");
    }
}
