using UnityEngine;
using TMPro;

/// <summary>
/// 文件名: FloatingPromptText.cs
/// 作用: 控制在触发区域上方悬浮的提示文字，支持 Billboard（始终面朝玩家）、Inspector 可配置
/// 主要功能:
///    1. 在 LateUpdate 中使文字始终面朝主摄像机
///    2. 提供 Show/Hide 公开方法，供 GameStartZone 调用
///    3. 文字内容、颜色、字号、字体、悬浮高度均可在 Inspector 中配置
/// </summary>
public class FloatingPromptText : MonoBehaviour
{
    [Header("文字样式")]
    [Tooltip("提示文字内容")]
    public string promptText = "进入此区域以开始游戏";

    [Tooltip("文字颜色")]
    public Color textColor = Color.white;

    [Tooltip("文字字号")]
    public float fontSize = 80f;

    [Tooltip("文字字体（TMP_FontAsset），留空则使用 TMP 默认字体")]
    public TMP_FontAsset font;

    [Header("位置")]
    [Tooltip("文字距离地面的悬浮高度（米），基于父物体的 Y 坐标偏移")]
    public float floatHeight = 1.0f;

    private TMP_Text tmpText;
    private Transform cameraTransform;

    /// <summary>
    /// 作用: 初始化 TMP_Text 组件引用并应用配置参数
    /// </summary>
    void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
        if (tmpText == null)
        {
            Debug.LogError("FloatingPromptText: 需要挂载在带有 TMP_Text 组件的 GameObject 上", this);
            return;
        }

        ApplySettings();
    }

    void Start()
    {
        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
        {
            Debug.LogWarning("FloatingPromptText: 未找到主摄像机", this);
        }
    }

    /// <summary>
    /// 作用: 每帧在 LateUpdate 中执行 Billboard 朝向，使文字始终面对摄像机
    /// </summary>
    void LateUpdate()
    {
        if (cameraTransform != null && tmpText != null && tmpText.enabled)
        {
            // Billboard：面朝摄像机，但文字不上下颠倒
            Vector3 direction = transform.position - cameraTransform.position;
            direction.y = 0f; // 保持垂直方向不倾斜
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    /// <summary>
    /// 作用: 应用 Inspector 中配置的文字样式参数到 TMP_Text 组件
    /// </summary>
    private void ApplySettings()
    {
        if (tmpText == null) return;

        tmpText.text = promptText;
        tmpText.color = textColor;
        tmpText.fontSize = fontSize;
        if (font != null)
            tmpText.font = font;
    }

    /// <summary>
    /// 作用: 显示提示文字
    /// </summary>
    public void Show()
    {
        if (tmpText != null) tmpText.enabled = true;
    }

    /// <summary>
    /// 作用: 隐藏提示文字
    /// </summary>
    public void Hide()
    {
        if (tmpText != null) tmpText.enabled = false;
    }

    /// <summary>
    /// 作用: 更新悬浮高度偏移（将文字定位在父物体上方指定高度处）
    /// </summary>
    public void UpdateFloatHeight()
    {
        Vector3 localPos = transform.localPosition;
        localPos.y = floatHeight;
        transform.localPosition = localPos;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 作用: 编辑器下修改参数时实时预览
    /// </summary>
    void OnValidate()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();
        if (tmpText != null)
            ApplySettings();
    }
#endif
}
