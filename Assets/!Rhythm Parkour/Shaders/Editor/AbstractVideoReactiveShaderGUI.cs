using UnityEngine;
using UnityEditor;

public class AbstractVideoReactiveShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        var headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        headerStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);

        MaterialProperty Find(string name) => FindProperty(name, properties);

        var videoTex = Find("_VideoTex");
        var videoInfluence = Find("_VideoInfluence");
        var videoColorMode = Find("_VideoColorMode");
        var videoSat = Find("_VideoSaturation");
        var videoCon = Find("_VideoContrast");
        var videoLum = Find("_VideoLumReactivity");
        var videoScale = Find("_VideoScaleReactivity");
        var videoWarp = Find("_VideoWarpReactivity");

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

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("ABSTRACT VIDEO REACTIVE — Видео → Абстракция", headerStyle);
        EditorGUILayout.HelpBox("Подключи VideoPlayer → RenderTexture → _VideoTex. Шейдер берет ключевые цвета видео и красит узоры. Работает на любом меше.", MessageType.Info);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Видео Вход", headerStyle);
        materialEditor.TexturePropertySingleLine(new GUIContent("Video Texture"), videoTex);
        EditorGUILayout.HelpBox("1) Создай RenderTexture (например 1920x1080)\n2) VideoPlayer → Target Texture = твоя RT\n3) Перетяни RT в это поле\nИЛИ оставь пустым — будут fallback цвета.", MessageType.None);
        materialEditor.ShaderProperty(videoInfluence, "Video Influence");
        string[] vmodeNames = new string[] { "Direct (пиксель под UV)", "Palette 4 (4 точки видео)", "Dominant + HueShift", "Average" };
        int vIdx = Mathf.Clamp((int)videoColorMode.floatValue, 0, vmodeNames.Length - 1);
        EditorGUI.BeginChangeCheck();
        vIdx = EditorGUILayout.Popup("Video Color Mode", vIdx, vmodeNames);
        if (EditorGUI.EndChangeCheck()) videoColorMode.floatValue = vIdx;
        materialEditor.ShaderProperty(videoSat, "Saturation Boost");
        materialEditor.ShaderProperty(videoCon, "Contrast");
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Реактивность к видео", headerStyle);
        materialEditor.ShaderProperty(videoLum, "Luminance → Толщина");
        materialEditor.ShaderProperty(videoScale, "Luminance → Масштаб");
        materialEditor.ShaderProperty(videoWarp, "Luminance → Warp/Движение");
        if (GUILayout.Button("Авто-настройка для VideoPlayer"))
        {
            // Quick setup helper
            foreach (var obj in materialEditor.targets)
            {
                var mat = obj as Material;
                var vp = GameObject.FindObjectOfType<UnityEngine.Video.VideoPlayer>();
                if (vp != null && vp.targetTexture != null)
                {
                    mat.SetTexture("_VideoTex", vp.targetTexture);
                    Debug.Log("[VideoReactive] Назначил " + vp.targetTexture.name + " из VideoPlayer");
                }
                else
                {
                    Debug.LogWarning("[VideoReactive] Не нашел VideoPlayer с Target Texture. Создай RenderTexture и назначь вручную.");
                }
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Fallback Цвета (когда видео нет)", headerStyle);
        materialEditor.ShaderProperty(baseColor, "Фон");
        materialEditor.ShaderProperty(colorA, "Цвет A");
        materialEditor.ShaderProperty(colorB, "Цвет B");
        materialEditor.ShaderProperty(colorC, "Цвет C");
        materialEditor.ShaderProperty(colorD, "Цвет D");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Узор", headerStyle);
        string[] patternNames = new string[] { "Stripes", "Waves", "Circles", "Dots Grid", "Grid Tech", "Truchet", "Flow Noise", "ZigZag", "Checker Melt" };
        int patIdx = Mathf.Clamp((int)pattern.floatValue, 0, patternNames.Length - 1);
        EditorGUI.BeginChangeCheck();
        patIdx = EditorGUILayout.Popup("Тип узора", patIdx, patternNames);
        if (EditorGUI.EndChangeCheck()) pattern.floatValue = patIdx;
        materialEditor.ShaderProperty(scale, "Плотность (Scale)");
        materialEditor.ShaderProperty(thickness, "Толщина");
        materialEditor.ShaderProperty(smoothness, "Мягкость");
        materialEditor.ShaderProperty(contrast, "Контраст");
        materialEditor.ShaderProperty(angle, "Поворот");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Анимация", headerStyle);
        materialEditor.ShaderProperty(speed, "Общая скорость");
        materialEditor.ShaderProperty(speedX, "Скорость X");
        materialEditor.ShaderProperty(speedY, "Скорость Y");
        materialEditor.ShaderProperty(waveFreq, "Частота волн");
        materialEditor.ShaderProperty(waveAmp, "Амплитуда");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Искажение", headerStyle);
        materialEditor.ShaderProperty(warpAmount, "Сила Warp");
        materialEditor.ShaderProperty(warpScale, "Масштаб шума");
        materialEditor.ShaderProperty(warpSpeed, "Скорость Warp");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Координаты", headerStyle);
        string[] uvNames = new string[] { "UV", "World XZ", "World XY", "Object XZ", "Triplanar World" };
        int uvIdx = Mathf.Clamp((int)uvMode.floatValue, 0, uvNames.Length - 1);
        EditorGUI.BeginChangeCheck();
        uvIdx = EditorGUILayout.Popup("Режим UV", uvIdx, uvNames);
        if (EditorGUI.EndChangeCheck()) uvMode.floatValue = uvIdx;
        if (uvMode.floatValue == 4) materialEditor.ShaderProperty(triplanarBlend, "Triplanar Sharpness");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Дополнительно", headerStyle);
        materialEditor.ShaderProperty(emission, "Свечение");
        materialEditor.ShaderProperty(alpha, "Alpha");

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Переключить Transparent / Opaque"))
        {
            foreach (var obj in materialEditor.targets)
            {
                var m = obj as Material;
                bool isTrans = m.GetFloat("_Surface") == 1;
                if (isTrans)
                {
                    m.SetFloat("_Surface", 0); m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 0); m.SetFloat("_ZWrite", 1); m.renderQueue = 2000; m.SetOverrideTag("RenderType", "Opaque");
                }
                else
                {
                    m.SetFloat("_Surface", 1); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent");
                }
            }
        }
        EditorGUILayout.Space(4);
        materialEditor.DoubleSidedGIField();
    }
}
