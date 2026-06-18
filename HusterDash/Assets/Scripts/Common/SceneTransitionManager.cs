using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 文件名: SceneTransitionManager.cs
/// 作用: 全局单例场景过渡管理器，提供带淡入淡出的跨场景切换功能
/// 主要功能:
///    1. 懒加载自动创建 Canvas 遮罩 UI（#509AE7 颜色、Loading 文字）
///    2. 支持自由配置字体资产（TMP_FontAsset）、字号大小和文字颜色
///    3. 使用 DontDestroyOnLoad 跨场景存活
///    4. 异步加载目标场景，配合 0.5 秒淡入/淡出动画
///    5. 使用 unscaledDeltaTime，不受 Time.timeScale 影响
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    /// <summary>
    /// 全局单例引用
    /// </summary>
    public static SceneTransitionManager Instance { get; private set; }

    /// <summary>
    /// 预制体在 Resources 文件夹下的路径（不带扩展名）
    /// </summary>
    private const string PrefabResourcePath = "SceneTransitionManager";

    [Header("过渡配置")]
    [Tooltip("过渡动画时长（秒），淡入和淡出各用一半")]
    public float transitionDuration = 0.5f;

    [Tooltip("遮罩颜色（默认 #509AE7）")]
    public Color overlayColor = new Color(0.314f, 0.604f, 0.906f, 1f); // #509AE7

    [Header("文字样式")]
    [Tooltip("Loading 文字的字体资产（TMP_FontAsset），留空则使用 TMP 默认字体")]
    public TMP_FontAsset loadingFont;

    [Tooltip("Loading 文字的字号大小")]
    public float loadingFontSize = 36f;

    [Tooltip("Loading 文字显示的内容")]
    public string loadingText = "Loading...";

    [Tooltip("Canvas 的渲染排序层级")]
    public int canvasSortingOrder = 9999;

    private Canvas canvas;
    private Image overlayImage;
    private TMP_Text loadingLabel;
    private bool isTransitioning = false;

    /// <summary>
    /// 作用: 懒加载创建过渡管理器并启动场景切换。
    ///       从 Resources 文件夹加载预制体实例化，可在预制体上通过 Inspector 自由配置所有参数。
    /// </summary>
    /// <param name="sceneName">目标场景名称</param>
    public static void LaunchTransition(string sceneName)
    {
        if (Instance != null)
        {
            Instance.TransitionToScene(sceneName);
            return;
        }

        // 从 Resources 加载预制体（可在 Inspector 中自由配置字体、字号、颜色等）
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
        }
        else
        {
            // 降级：Resources 中找不到预制体时动态创建
            Debug.LogWarning($"SceneTransitionManager: 未在 Resources/{PrefabResourcePath} 找到预制体，"
                           + "使用默认配置。请创建预制体以获得 Inspector 可视化配置能力。");
            go = new GameObject("SceneTransitionManager");
            go.AddComponent<SceneTransitionManager>();
        }

        go.GetComponent<SceneTransitionManager>().TransitionToScene(sceneName);
    }

    /// <summary>
    /// 作用: 获取或创建全局单例，确保跨场景存活
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateUI();
            HideOverlay(); // 初始隐藏遮罩
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 作用: 动态创建遮罩 Canvas、Image 和 Loading 文字
    /// </summary>
    private void CreateUI()
    {
        // 创建 Canvas（ScreenSpaceOverlay，最高渲染层级）
        GameObject canvasGo = new GameObject("TransitionCanvas");
        canvasGo.transform.SetParent(transform);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // 创建全屏遮罩 Image
        GameObject imageGo = new GameObject("OverlayImage");
        imageGo.transform.SetParent(canvasGo.transform, false);
        overlayImage = imageGo.AddComponent<Image>();
        overlayImage.color = overlayColor;
        RectTransform imageRect = overlayImage.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        // 创建居中的 Loading 文字
        GameObject textGo = new GameObject("LoadingText");
        textGo.transform.SetParent(canvasGo.transform, false);
        loadingLabel = textGo.AddComponent<TextMeshProUGUI>();
        loadingLabel.text = loadingText;
        loadingLabel.fontSize = loadingFontSize;
        loadingLabel.font = loadingFont; // null 时自动使用 TMP 默认字体
        loadingLabel.alignment = TextAlignmentOptions.Center;
        loadingLabel.color = Color.white;
        RectTransform textRect = loadingLabel.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(400f, 100f);
        textRect.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 作用: 隐藏遮罩和文字（设置 alpha 为 0）
    /// </summary>
    private void HideOverlay()
    {
        if (overlayImage != null)
        {
            Color c = overlayImage.color;
            c.a = 0f;
            overlayImage.color = c;
        }
        if (loadingLabel != null)
        {
            Color c = loadingLabel.color;
            c.a = 0f;
            loadingLabel.color = c;
        }
    }

    /// <summary>
    /// 作用: 对外唯一入口，触发带过渡动画的场景切换
    /// </summary>
    /// <param name="sceneName">目标场景名称</param>
    public void TransitionToScene(string sceneName)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("SceneTransitionManager: 已有正在进行的过渡，忽略重复请求");
            return;
        }
        StartCoroutine(DoTransition(sceneName));
    }

    /// <summary>
    /// 作用: 执行完整的场景过渡流程：
    ///       淡入（0→1）→ 异步加载场景 → 淡出（1→0）→ 销毁自身
    /// </summary>
    private IEnumerator DoTransition(string sceneName)
    {
        isTransitioning = true;
        float halfDuration = transitionDuration * 0.5f;

        // 第一阶段：淡入（遮罩逐渐变为不透明，Loading 文字逐渐显现）
        yield return StartCoroutine(FadeOverlay(0f, 1f, halfDuration));

        // 通知音乐管理器准备场景切换（提前淡出当前场景音乐）
        if (MusicManager.Instance != null)
            MusicManager.Instance.OnSceneTransitionStart(sceneName);

        // 第二阶段：异步加载目标场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (asyncLoad != null)
        {
            asyncLoad.allowSceneActivation = true;

            // 等待场景加载完成
            while (!asyncLoad.isDone)
                yield return null;
        }

        // 第三阶段：淡出（遮罩逐渐变为透明，Loading 文字逐渐消失）
        yield return StartCoroutine(FadeOverlay(1f, 0f, halfDuration));

        // 过渡完成，销毁管理器
        isTransitioning = false;
        Instance = null;
        Destroy(gameObject);
    }

    /// <summary>
    /// 作用: 平滑过渡遮罩和 Loading 文字的透明度
    /// </summary>
    /// <param name="fromAlpha">起始透明度</param>
    /// <param name="toAlpha">目标透明度</param>
    /// <param name="duration">动画时长</param>
    private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 更新遮罩透明度
            if (overlayImage != null)
            {
                Color oc = overlayImage.color;
                oc.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                overlayImage.color = oc;
            }

            // 更新 Loading 文字透明度
            if (loadingLabel != null)
            {
                Color lc = loadingLabel.color;
                lc.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                loadingLabel.color = lc;
            }

            yield return null;
        }

        // 确保最终值精确到位
        if (overlayImage != null)
        {
            Color oc = overlayImage.color;
            oc.a = toAlpha;
            overlayImage.color = oc;
        }
        if (loadingLabel != null)
        {
            Color lc = loadingLabel.color;
            lc.a = toAlpha;
            loadingLabel.color = lc;
        }
    }
}
