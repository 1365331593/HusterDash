using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 文件名: GameStartZoneUI.cs
/// 作用: 控制"开始游戏"交互面板的显示、选项切换和确认
/// 主要功能:
///    1. 显示/隐藏面板（玩家进入/离开触发区域时调用）
///    2. 滚轮切换选项（"开始游戏" / "新手教学"），三角形指示器跟随当前选项
///    3. 按下交互键执行当前选中选项的回调
///    4. 所有文字样式、间距、背景色均可在 Inspector 中配置
/// </summary>
public class GameStartZoneUI : MonoBehaviour
{
    [Header("全局容器")]
    [Tooltip("面板根节点（Canvas 或其子节点），Show/Hide 通过 SetActive 控制")]
    public GameObject panelRoot;

    [Header("F 按键图标")]
    [Tooltip("左侧 F 键图标的 Image 组件")]
    public Image fKeyIcon;
    [Tooltip("F 键图标精灵图")]
    public Sprite fKeySprite;

    [Header("选项列表")]
    [Tooltip("选项背景色（卡其色，30% 透明）")]
    public Color optionBackgroundColor = new Color(0.766f, 0.694f, 0.514f, 0.3f);

    [Tooltip("选项容器（VerticalLayoutGroup 所在的 RectTransform）")]
    public RectTransform optionsContainer;

    [Tooltip("三角形指示器的 RectTransform")]
    public RectTransform triangleIndicator;

    [Header("文字样式")]
    [Tooltip("选项文字的字体（TMP_FontAsset），留空则使用 TMP 默认字体")]
    public TMP_FontAsset font;

    [Tooltip("选项文字的字号")]
    public float fontSize = 36f;

    [Tooltip("选项文字的间距")]
    public float optionSpacing = 20f;

    [Tooltip("F 键图标到选项文字的水平间距")]
    public float iconToOptionsSpacing = 15f;

    [Tooltip("三角指示器到选项背景的水平间距（通常比 F 图标稍近）")]
    public float triangleOffset = 4f;

    [Header("选项事件")]
    [Tooltip("选中\"开始游戏\"并按 F 时触发")]
    public UnityEvent onStartGame;

    [Tooltip("选中\"新手教学\"并按 F 时触发")]
    public UnityEvent onTutorial;

    [Header("调试")]
    [Tooltip("当前选中的选项索引（0 = 开始游戏, 1 = 新手教学）")]
    [SerializeField] private int currentIndex = 0;

    // 两个选项的 UI 元素引用
    private Image[] optionBackgrounds;
    private TMP_Text[] optionTexts;

    // 两个选项的文字内容
    private readonly string[] optionLabels = { "开始游戏", "新手教学" };

    // VerticalLayoutGroup 组件引用
    private VerticalLayoutGroup layoutGroup;

    // 是否正在显示面板
    private bool isShowing = false;

    /// <summary>
    /// 作用: 初始化 UI 元素引用并应用配置参数
    /// </summary>
    void Awake()
    {
        // 初始时隐藏面板
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // 缓存 VerticalLayoutGroup
        if (optionsContainer != null)
            layoutGroup = optionsContainer.GetComponent<VerticalLayoutGroup>();

        // 缓存选项子物体上的 Image 和 TMP_Text
        if (optionsContainer != null)
        {
            int childCount = optionsContainer.childCount;
            optionBackgrounds = new Image[Mathf.Min(childCount, optionLabels.Length)];
            optionTexts = new TMP_Text[Mathf.Min(childCount, optionLabels.Length)];

            for (int i = 0; i < optionLabels.Length && i < childCount; i++)
            {
                Transform child = optionsContainer.GetChild(i);
                optionBackgrounds[i] = child.GetComponent<Image>();
                optionTexts[i] = child.GetComponentInChildren<TMP_Text>();
            }
        }
    }

    /// <summary>
    /// 作用: 显示面板并恢复选项状态
    /// </summary>
    public void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        isShowing = true;

        // 应用 Inspector 配置
        ApplySettings();

        // 强制重建布局，确保选项位置已确定后再刷新 F 图标和三角位置
        Canvas.ForceUpdateCanvases();
        if (optionsContainer != null)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(optionsContainer);

        // 恢复选中状态
        RefreshSelection();
    }

    /// <summary>
    /// 作用: 隐藏面板
    /// </summary>
    public void Hide()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        isShowing = false;
    }

    /// <summary>
    /// 作用: 当前面板是否正在显示
    /// </summary>
    public bool IsShowing()
    {
        return isShowing && panelRoot != null && panelRoot.activeSelf;
    }

    /// <summary>
    /// 作用: 切换到上一个选项（滚轮上滚）
    /// </summary>
    public void SelectPrevious()
    {
        if (optionLabels.Length == 0) return;
        currentIndex = (currentIndex - 1 + optionLabels.Length) % optionLabels.Length;
        RefreshSelection();
    }

    /// <summary>
    /// 作用: 切换到下一个选项（滚轮下滚）
    /// </summary>
    public void SelectNext()
    {
        if (optionLabels.Length == 0) return;
        currentIndex = (currentIndex + 1) % optionLabels.Length;
        RefreshSelection();
    }

    /// <summary>
    /// 作用: 执行当前选中选项的回调
    /// </summary>
    public void ConfirmSelection()
    {
        if (!IsShowing())
        {
            Debug.LogWarning($"GameStartZoneUI.ConfirmSelection: 面板未显示，跳过。isShowing={isShowing}, panelRoot={(panelRoot == null ? "null" : panelRoot.name)}");
            return;
        }

        Debug.Log($"GameStartZoneUI.ConfirmSelection: 执行选项索引 {currentIndex} ({optionLabels[currentIndex]})");

        switch (currentIndex)
        {
            case 0:
                if (onStartGame != null && onStartGame.GetPersistentEventCount() > 0)
                {
                    Debug.Log("GameStartZoneUI: 触发 onStartGame 事件");
                    onStartGame.Invoke();
                }
                else
                {
                    Debug.LogError("GameStartZoneUI: onStartGame 事件未在 Inspector 中绑定！请将 GameStartPanel 拖入事件槽位，选择 GameStartZoneUI → OnStartGameClicked()");
                }
                break;
            case 1:
                if (onTutorial != null && onTutorial.GetPersistentEventCount() > 0)
                {
                    Debug.Log("GameStartZoneUI: 触发 onTutorial 事件");
                    onTutorial.Invoke();
                }
                else
                {
                    Debug.LogWarning("GameStartZoneUI: onTutorial 事件未在 Inspector 中绑定（当前为占位选项）");
                }
                break;
        }
    }

    /// <summary>
    /// 作用: 刷新选项列表的 UI 状态（F 图标位置、三角形指示器位置、背景颜色和精灵）
    ///       F 图标和三角号始终跟在当前选中选项的左侧
    /// </summary>
    private void RefreshSelection()
    {
        // 更新 F 键图标精灵
        if (fKeyIcon != null && fKeySprite != null)
            fKeyIcon.sprite = fKeySprite;

        // 刷新选项背景色（选中项稍微更不透明以示区分）
        for (int i = 0; i < optionBackgrounds.Length; i++)
        {
            if (optionBackgrounds[i] == null) continue;
            Color bgColor = optionBackgroundColor;
            if (i == currentIndex)
                bgColor.a = Mathf.Clamp01(optionBackgroundColor.a + 0.15f);
            optionBackgrounds[i].color = bgColor;
        }

        // 没有选中项或缺少必要引用时，停止后续定位
        if (optionBackgrounds == null || currentIndex >= optionBackgrounds.Length || panelRoot == null)
            return;
        RectTransform selectedBg = optionBackgrounds[currentIndex]?.rectTransform;
        if (selectedBg == null) return;

        // ---------- 将选中选项背景的世界坐标转换为 panelRoot 的本地坐标 ----------
        // 使用选项背景 Image（而不是 TMP_Text），其 rect 由 VerticalLayoutGroup 控制，尺寸稳定
        Vector3 optionWorldPos = selectedBg.position;
        Vector3 localPos = panelRoot.transform.InverseTransformPoint(optionWorldPos);

        // option 背景的左右边缘（世界空间 → panelRoot 本地空间）
        float bgLeftEdge = localPos.x - selectedBg.rect.width * 0.5f;

        // ---------- 定位 F 键图标（选项背景左侧，间距由 iconToOptionsSpacing 控制）----------
        if (fKeyIcon != null)
        {
            RectTransform fkRect = fKeyIcon.rectTransform;
            float fkX = bgLeftEdge - fkRect.rect.width * 0.5f - iconToOptionsSpacing;
            fkRect.localPosition = new Vector3(fkX, localPos.y, 0f);
            fkRect.gameObject.SetActive(true);
        }

        // ---------- 定位三角指示器（比 F 图标更靠近选项，间距由 triangleOffset 控制）----------
        if (triangleIndicator != null)
        {
            float triX = bgLeftEdge - triangleIndicator.rect.width * 0.5f - triangleOffset;
            triangleIndicator.localPosition = new Vector3(triX, localPos.y, 0f);
            triangleIndicator.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 作用: 将 Inspector 中的配置参数应用到 UI 元素
    /// </summary>
    public void ApplySettings()
    {
        // 设置字体和字号
        if (optionTexts != null)
        {
            for (int i = 0; i < optionTexts.Length; i++)
            {
                if (optionTexts[i] == null) continue;
                if (font != null)
                    optionTexts[i].font = font;
                optionTexts[i].fontSize = fontSize;
                if (i < optionLabels.Length)
                    optionTexts[i].text = optionLabels[i];
            }
        }

        // 设置选项间距
        if (layoutGroup != null)
            layoutGroup.spacing = optionSpacing;
    }

    /// <summary>
    /// 作用: 获取当前选中的选项索引
    /// </summary>
    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    /// <summary>
    /// 作用: Inspector 可绑定的包装方法 —— 开始游戏（调用 SceneTransitionManager 跳转到 Game 场景）
    /// </summary>
    public void OnStartGameClicked()
    {
        SceneTransitionManager.LaunchTransition("Game");
    }

    /// <summary>
    /// 作用: Inspector 可绑定的包装方法 —— 新手教学（当前为占位，无实际逻辑）
    /// </summary>
    public void OnTutorialClicked()
    {
        Debug.Log("GameStartZoneUI: 新手教学 - 占位选项，暂无功能");
    }

#if UNITY_EDITOR
    /// <summary>
    /// 作用: 编辑器下修改参数时实时预览
    /// </summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;
        // 编辑器模式下尝试应用
        if (optionsContainer != null)
            layoutGroup = optionsContainer.GetComponent<VerticalLayoutGroup>();
        if (isShowing)
            ApplySettings();
    }
#endif
}
