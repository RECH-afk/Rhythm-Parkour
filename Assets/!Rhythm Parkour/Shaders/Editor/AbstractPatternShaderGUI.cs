using UnityEngine;
using UnityEditor;

public class AbstractPatternShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Style
        var headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        headerStyle.normal.textColor = new Color(0.4f, 0.7f, 1f);

        MaterialProperty Find(string name) => FindProperty(name, properties);

        var baseColor = Find("_BaseColor");
        var colorA = Find("_ColorA");
        var colorB = Find("_ColorB");
        var colorC = Find("_ColorC");
        var colorD = Find("_ColorD");

        var pattern = Find("_Pattern");
        var scale = Find("_Scale");
        var thickness = Find("_Thickness");
        var smoothness = Find("_Smoothness");
        var contrast = Find("_Contrast");
        var angle = Find("_Angle");

        var speed = Find("_Speed");
        var speedX = Find("_SpeedX");
        var speedY = Find("_SpeedY");
        var waveFreq = Find("_WaveFreq");
        var waveAmp = Find("_WaveAmp");

        var warpAmount = Find("_WarpAmount");
        var warpScale = Find("_WarpScale");
        var warpSpeed = Find("_WarpSpeed");

        var uvMode = Find("_UVMode");
        var triplanarBlend = Find("_TriplanarBlend");

        var emission = Find("_EmissionStrength");
        var alpha = Find("_Alpha");

        // Header
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("ABSTRACT PATTERN URP  —  Универсальный шейдер", headerStyle);
        EditorGUILayout.HelpBox("Кидай на любой меш. Работает без UV (выбери World/Triplanar). Все цвета и узоры настраиваются.", MessageType.None);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Цвета", headerStyle);
        materialEditor.ShaderProperty(baseColor, "Фон (Background)");
        materialEditor.ShaderProperty(colorA, "Цвет A");
        materialEditor.ShaderProperty(colorB, "Цвет B");
        materialEditor.ShaderProperty(colorC, "Цвет C");
        materialEditor.ShaderProperty(colorD, "Цвет D");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Узор", headerStyle);
        string[] patternNames = new string[] { "Stripes (Полосы)", "Waves (Волны)", "Circles", "Dots Grid (Точки)", "Grid Tech (Сетка)", "Truchet", "Flow Noise (Поток)", "ZigZag", "Checker Melt" };
        int patIdx = Mathf.Clamp((int)pattern.floatValue, 0, patternNames.Length - 1);
        EditorGUI.BeginChangeCheck();
        patIdx = EditorGUILayout.Popup("Тип узора", patIdx, patternNames);
        if (EditorGUI.EndChangeCheck()) pattern.floatValue = patIdx;
        materialEditor.ShaderProperty(scale, "Плотность (Scale)");
        materialEditor.ShaderProperty(thickness, "Толщина линий");
        materialEditor.ShaderProperty(smoothness, "Мягкость краев");
        materialEditor.ShaderProperty(contrast, "Контраст");
        materialEditor.ShaderProperty(angle, "Поворот");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Анимация", headerStyle);
        materialEditor.ShaderProperty(speed, "Общая скорость");
        materialEditor.ShaderProperty(speedX, "Скорость X");
        materialEditor.ShaderProperty(speedY, "Скорость Y");
        materialEditor.ShaderProperty(waveFreq, "Частота волн (для Waves/ZigZag)");
        materialEditor.ShaderProperty(waveAmp, "Амплитуда волн");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Искажение (Warp)", headerStyle);
        materialEditor.ShaderProperty(warpAmount, "Сила искажения");
        materialEditor.ShaderProperty(warpScale, "Масштаб шума");
        materialEditor.ShaderProperty(warpSpeed, "Скорость искажения");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Координаты", headerStyle);
        EditorGUILayout.HelpBox("UV = по развертке меша. World/Object = по позиции в мире (работает даже без UV). Triplanar = без швов на любом меше.", MessageType.None);
        string[] uvNames = new string[] { "UV", "World XZ", "World XY", "Object XZ", "Triplanar World" };
        int uvIdx = Mathf.Clamp((int)uvMode.floatValue, 0, uvNames.Length - 1);
        EditorGUI.BeginChangeCheck();
        uvIdx = EditorGUILayout.Popup("Режим UV", uvIdx, uvNames);
        if (EditorGUI.EndChangeCheck()) uvMode.floatValue = uvIdx;
        if (uvMode.floatValue == 4)
            materialEditor.ShaderProperty(triplanarBlend, "Четкость Triplanar");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Дополнительно", headerStyle);
        materialEditor.ShaderProperty(emission, "Свечение");
        materialEditor.ShaderProperty(alpha, "Прозрачность (если включишь Transparent)");

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Сделать материал Transparent / Opaque"))
        {
            foreach (var mat in materialEditor.targets)
            {
                var m = mat as Material;
                bool isTransparent = m.GetFloat("_Surface") == 1;
                if (isTransparent)
                {
                    m.SetFloat("_Surface", 0);
                    m.SetFloat("_Blend", 0);
                    m.SetFloat("_SrcBlend", 1);
                    m.SetFloat("_DstBlend", 0);
                    m.SetFloat("_ZWrite", 1);
                    m.renderQueue = 2000;
                    m.SetOverrideTag("RenderType", "Opaque");
                }
                else
                {
                    m.SetFloat("_Surface", 1);
                    m.SetFloat("_Blend", 0);
                    m.SetFloat("_SrcBlend", 5); // SrcAlpha
                    m.SetFloat("_DstBlend", 10); // OneMinusSrcAlpha
                    m.SetFloat("_ZWrite", 0);
                    m.renderQueue = 3000;
                    m.SetOverrideTag("RenderType", "Transparent");
                }
            }
        }

        EditorGUILayout.Space(4);
        // Render default queue/cull etc hidden props if needed
        // materialEditor.RenderQueueField();
        // materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }
}
