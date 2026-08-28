using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Связывает VideoPlayer с AbstractVideoReactiveURP шейдером.
/// - Кидает текстуру видео в _VideoTex
/// - Опционально вытаскивает ключевые цвета на CPU и плавно красит fallback палитру
/// Вешай на тот же объект где VideoPlayer, или на любой GameObject и перетяни ссылки.
/// </summary>
[ExecuteAlways]
public class VideoReactiveController : MonoBehaviour
{
    [Header("Ссылки")]
    public VideoPlayer videoPlayer;
    public Renderer targetRenderer;
    [Tooltip("Имя текстуры в шейдере")] public string textureProperty = "_VideoTex";
    [Tooltip("Если пусто - возьмет материал с targetRenderer")] public Material targetMaterial;

    [Header("Видео → Шейдер")]
    [Range(0,1)] public float videoInfluence = 0.85f;
    public bool autoCreateRenderTexture = true;
    public Vector2Int renderTextureSize = new Vector2Int(512, 512);

    [Header("CPU Палитра (опционально)")]
    [Tooltip("Если вкл - скрипт сам считает 3-4 ключевых цвета с видео и пишет в _ColorA/B/C/D. Если выкл - шейдер сам сэмплит _VideoTex внутри.")]
    public bool useCPUPalette = false;
    [Range(0, 10)] public float colorLerpSpeed = 3f;
    [Range(0, 2)] public float cpuSaturationBoost = 1.2f;
    [Tooltip("Размер даунскейла для анализа (меньше = быстрее)")] public int downscaleSize = 32;

    private RenderTexture _autoRT;
    private Texture2D _readTex;
    private Material _instanceMat;
    private bool _hasVideoTexProperty;

    void OnEnable()
    {
        FindReferences();
        TrySetupRenderTexture();
        CacheMaterial();
    }

    void OnValidate()
    {
        FindReferences();
    }

    void FindReferences()
    {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null) videoPlayer = FindObjectOfType<VideoPlayer>();
        if (targetRenderer == null)
        {
            // пробуем найти LevelVideoMeshQuad
            var go = GameObject.Find("LevelVideoMeshQuad");
            if (go != null) targetRenderer = go.GetComponent<Renderer>();
        }
    }

    void CacheMaterial()
    {
        if (targetMaterial != null)
        {
            _instanceMat = targetMaterial;
        }
        else if (targetRenderer != null)
        {
            // В эдиторе используем sharedMaterial чтобы не плодить инстансы, в плеймоде - material
#if UNITY_EDITOR
            if (!Application.isPlaying) _instanceMat = targetRenderer.sharedMaterial;
            else _instanceMat = targetRenderer.material;
#else
            _instanceMat = targetRenderer.material;
#endif
        }
        if (_instanceMat != null && _instanceMat.HasProperty(textureProperty))
            _hasVideoTexProperty = true;
        else _hasVideoTexProperty = false;
    }

    void TrySetupRenderTexture()
    {
        if (videoPlayer == null) return;
        if (videoPlayer.targetTexture == null && autoCreateRenderTexture)
        {
            // создаем RT если не назначен
            _autoRT = new RenderTexture(renderTextureSize.x, renderTextureSize.y, 0, RenderTextureFormat.ARGB32);
            _autoRT.name = "VideoReactive_RT_Auto";
            _autoRT.Create();
            videoPlayer.targetTexture = _autoRT;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            Debug.Log($"[VideoReactive] Создал RenderTexture {_autoRT.name} {renderTextureSize.x}x{renderTextureSize.y} и назначил на VideoPlayer", this);
        }
    }

    void Update()
    {
        if (videoPlayer == null || _instanceMat == null)
        {
            // пробуем перекешировать если что-то появилось
            if (Time.frameCount % 60 == 0) { FindReferences(); CacheMaterial(); }
            return;
        }

        // 1) Пробрасываем текстуру видео в шейдер каждую секунду + каждый кадр если меняется
        if (_hasVideoTexProperty)
        {
            Texture vpTex = videoPlayer.targetTexture != null ? (Texture)videoPlayer.targetTexture : videoPlayer.texture;
            if (vpTex != null)
            {
                if (_instanceMat.GetTexture(textureProperty) != vpTex)
                    _instanceMat.SetTexture(textureProperty, vpTex);
                // также пробрасываем influence
                if (_instanceMat.HasProperty("_VideoInfluence"))
                    _instanceMat.SetFloat("_VideoInfluence", videoInfluence);
            }
        }

        // 2) Опционально считаем палитру на CPU
        if (useCPUPalette && videoPlayer.targetTexture != null && Time.frameCount % 3 == 0)
        {
            UpdateCPUPalette();
        }

        // В эдиторе помечаем материал грязным чтобы обновился
#if UNITY_EDITOR
        if (!Application.isPlaying && _instanceMat != null)
            UnityEditor.EditorUtility.SetDirty(_instanceMat);
#endif
    }

    void UpdateCPUPalette()
    {
        var rt = videoPlayer.targetTexture;
        if (rt == null) return;

        // даунскейлим в маленький RT для анализа
        RenderTexture tmp = RenderTexture.GetTemporary(downscaleSize, downscaleSize, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, tmp);

        if (_readTex == null || _readTex.width != downscaleSize)
        {
            _readTex = new Texture2D(downscaleSize, downscaleSize, TextureFormat.RGB24, false);
        }

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = tmp;
        _readTex.ReadPixels(new Rect(0, 0, downscaleSize, downscaleSize), 0, 0);
        _readTex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(tmp);

        // считаем палитру: берем 4 точки + среднее
        Color cA = _readTex.GetPixel(downscaleSize / 4, downscaleSize / 2);
        Color cB = _readTex.GetPixel(downscaleSize * 3 / 4, downscaleSize / 2);
        Color cC = _readTex.GetPixel(downscaleSize / 2, downscaleSize / 4);
        Color cD = _readTex.GetPixel(downscaleSize / 2, downscaleSize * 3 / 4);

        // средняя
        Color avg = AverageColor(_readTex);
        // если видео темное - используем avg
        if (avg.grayscale < 0.08f) { cA = cB = cC = cD = avg; }

        // бустим насыщенность
        cA = SaturationBoost(cA, cpuSaturationBoost);
        cB = SaturationBoost(cB, cpuSaturationBoost);
        cC = SaturationBoost(cC, cpuSaturationBoost);
        cD = SaturationBoost(cD, cpuSaturationBoost);

        // плавно лерпим к цели
        float t = Time.deltaTime * colorLerpSpeed;
        if (!Application.isPlaying) t = 0.2f;

        Color curA = _instanceMat.HasProperty("_ColorA") ? _instanceMat.GetColor("_ColorA") : cA;
        Color curB = _instanceMat.HasProperty("_ColorB") ? _instanceMat.GetColor("_ColorB") : cB;
        Color curC = _instanceMat.HasProperty("_ColorC") ? _instanceMat.GetColor("_ColorC") : cC;
        Color curD = _instanceMat.HasProperty("_ColorD") ? _instanceMat.GetColor("_ColorD") : cD;

        if (_instanceMat.HasProperty("_ColorA")) _instanceMat.SetColor("_ColorA", Color.Lerp(curA, cA, t));
        if (_instanceMat.HasProperty("_ColorB")) _instanceMat.SetColor("_ColorB", Color.Lerp(curB, cB, t));
        if (_instanceMat.HasProperty("_ColorC")) _instanceMat.SetColor("_ColorC", Color.Lerp(curC, cC, t));
        if (_instanceMat.HasProperty("_ColorD")) _instanceMat.SetColor("_ColorD", Color.Lerp(curD, cD, t));

        // фон чуть темнее среднего
        Color bg = avg * 0.35f;
        bg.a = 1;
        if (_instanceMat.HasProperty("_BaseColor"))
        {
            Color curBg = _instanceMat.GetColor("_BaseColor");
            _instanceMat.SetColor("_BaseColor", Color.Lerp(curBg, bg, t * 0.5f));
        }
    }

    Color AverageColor(Texture2D tex)
    {
        Color sum = Color.black;
        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++) sum += pixels[i];
        sum /= pixels.Length;
        return sum;
    }

    Color SaturationBoost(Color c, float sat)
    {
        float lum = c.grayscale;
        return Color.Lerp(new Color(lum, lum, lum), c, sat);
    }

    void OnDisable()
    {
        if (_autoRT != null && autoCreateRenderTexture)
        {
            // не удаляем если юзается где-то еще
            // _autoRT.Release();
        }
    }

    // Быстрый хелпер для кнопки в инспекторе
    [ContextMenu("Найти VideoPlayer и Renderer")]
    void ContextFind()
    {
        FindReferences();
        CacheMaterial();
        Debug.Log($"Found VP: {videoPlayer}, Renderer: {targetRenderer}, Mat: {_instanceMat}", this);
    }

    [ContextMenu("Создать Demo материал VideoReactive")]
    void CreateDemoMaterial()
    {
#if UNITY_EDITOR
        string shaderName = "Custom/AbstractVideoReactiveURP";
        var shader = Shader.Find(shaderName);
        if (shader == null) { Debug.LogError($"Шейдер {shaderName} не найден!"); return; }
        string path = "Assets/!Rhythm Parkour/Materials/AbstractPattern/VideoReactive_Demo.mat";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.Combine(Application.dataPath, "!Rhythm Parkour/Materials/AbstractPattern")));
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            UnityEditor.AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        // дефолты
        mat.SetTexture("_VideoTex", videoPlayer != null && videoPlayer.targetTexture != null ? videoPlayer.targetTexture : null);
        mat.SetFloat("_VideoInfluence", 0.85f);
        mat.SetFloat("_VideoColorMode", 1);
        mat.SetFloat("_Pattern", 1);
        mat.SetFloat("_Scale", 12);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Создал {path}", mat);
        UnityEditor.Selection.activeObject = mat;
#endif
    }
}
