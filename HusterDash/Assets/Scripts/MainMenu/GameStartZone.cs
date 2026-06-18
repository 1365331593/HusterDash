using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 文件名: GameStartZone.cs
/// 作用: 触发区域核心控制器，协调悬浮文字、UI面板、相机缩放和交互逻辑
/// 主要功能:
///    1. 检测玩家进入/离开矩形触发区域
///    2. 进入时：隐藏悬浮文字 → 弹出 UI 面板 → 禁用相机滚轮缩放
///    3. 离开时：显示悬浮文字 → 隐藏 UI 面板 → 恢复相机滚轮缩放
///    4. 面板可见时：滚轮切换选项 → 按下交互键执行选中选项
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class GameStartZone : MonoBehaviour
{
    [Header("交互键")]
    [Tooltip("用于确认选项的按键（新版 Input System Key 枚举）")]
    public Key interactKey = Key.F;

    [Header("组件引用")]
    [Tooltip("悬浮提示文字脚本引用")]
    public FloatingPromptText floatingText;

    [Tooltip("UI 面板控制脚本引用")]
    public GameStartZoneUI uiPanel;

    [Tooltip("第三人称相机控制器引用")]
    public ThirdPersonCameraController thirdPersonCamera;

    [Tooltip("玩家标签，用于 OnTriggerEnter/Exit 过滤")]
    public string playerTag = "Player";

    [Header("发光边框")]
    [Tooltip("发光边框 Quad 的 Transform（子物体）。其 scale.x/z 在 Start 时自动匹配 BoxCollider 的 size")]
    public Transform borderQuad;

    [Tooltip("边框 Y 轴偏移（米），微调边框离地高度")]
    public float borderYOffset = 0.01f;

    private BoxCollider zoneCollider;
    private bool isPlayerInside = false;

    // 用于滚轮检测的累计值
    private float scrollAccumulator = 0f;
    [Tooltip("滚轮灵敏度阈值，累计超过此值触发一次选项切换")]
    private const float ScrollThreshold = 0.1f;

    // 一次性诊断标记
    private bool hasLoggedEntry = false;
    private bool hasLoggedKeyboard = false;

    /// <summary>
    /// 作用: 缓存组件引用，同步边框 Quad 尺寸
    /// </summary>
    void Start()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;

        // 自动调整边框 Quad 尺寸匹配 BoxCollider
        SyncBorderQuadSize();
    }

    /// <summary>
    /// 作用: 每帧处理交互逻辑（滚轮切换、按键确认）
    /// </summary>
    void Update()
    {
        // 未进入区域时静默，不刷日志
        if (!isPlayerInside)
            return;

        // 教程面板显示时跳过所有交互逻辑，防止重复触发
        if (TutorialPanelManager.IsShowing)
            return;

        // 一次性诊断：刚进入区域时输出完整状态
        if (!hasLoggedEntry)
        {
            hasLoggedEntry = true;
            Debug.Log($"GameStartZone: 玩家已进入。uiPanel={(uiPanel == null ? "null" : uiPanel.name)}, "
                    + $"thirdPersonCamera={(thirdPersonCamera == null ? "null" : thirdPersonCamera.name)}, "
                    + $"uiPanel.IsShowing()={(uiPanel != null ? uiPanel.IsShowing().ToString() : "N/A")}, "
                    + $"Keyboard.current={(Keyboard.current == null ? "null" : "有效")}, "
                    + $"interactKey={interactKey}");
        }

        // 检查 UI 面板引用
        if (uiPanel == null)
        {
            Debug.LogError("GameStartZone: uiPanel 引用为空！请在 Inspector 中拖入 GameStartPanel 的 GameStartZoneUI 组件。");
            return;
        }

        // 检查 UI 面板是否正在显示
        if (!uiPanel.IsShowing())
        {
            // 避免刷屏，5秒输出一次
            if (Time.frameCount % 300 == 0)
                Debug.LogWarning("GameStartZone: isPlayerInside=true 但 uiPanel.IsShowing()=false，面板引用可能未配置正确。");
            return;
        }

        // 检查键盘设备
        if (Keyboard.current == null)
        {
            if (!hasLoggedKeyboard)
            {
                hasLoggedKeyboard = true;
                Debug.LogError("GameStartZone: Keyboard.current 为 null！请确认 Input System Package 中键盘设备已正确配置。");
            }
            return;
        }

        // 处理滚轮输入切换选项
        HandleScrollInput();

        // 处理按键确认
        if (Keyboard.current[interactKey].wasPressedThisFrame)
        {
            Debug.Log($"GameStartZone: 检测到 {interactKey} 键按下，当前选项索引 = {uiPanel.GetCurrentIndex()}");
            uiPanel.ConfirmSelection();
        }
    }

    /// <summary>
    /// 作用: 处理滚轮输入，累积滚动量达到阈值后切换选项
    /// </summary>
    private void HandleScrollInput()
    {
        float scrollDelta = Mouse.current?.scroll?.y?.ReadValue() ?? 0f;
        if (Mathf.Abs(scrollDelta) < 0.001f) return;

        scrollAccumulator += scrollDelta;

        // 上滚（正）切换上一个
        while (scrollAccumulator >= ScrollThreshold)
        {
            uiPanel.SelectPrevious();
            scrollAccumulator -= ScrollThreshold;
        }

        // 下滚（负）切换下一个
        while (scrollAccumulator <= -ScrollThreshold)
        {
            uiPanel.SelectNext();
            scrollAccumulator += ScrollThreshold;
        }
    }

    /// <summary>
    /// 作用: 玩家进入触发区域
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = true;
        scrollAccumulator = 0f;

        // 隐藏悬浮提示文字
        if (floatingText != null) floatingText.Hide();

        // 弹出 UI 面板
        if (uiPanel != null) uiPanel.Show();

        // 禁用相机滚轮缩放
        if (thirdPersonCamera != null) thirdPersonCamera.SetZoomEnabled(false);

        Debug.Log("GameStartZone: 玩家进入触发区域");
    }

    /// <summary>
    /// 作用: 玩家离开触发区域
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // 教程面板显示时不处理离开事件（此时玩家已被冻结，此检查为防御性代码）
        if (TutorialPanelManager.IsShowing) return;

        isPlayerInside = false;
        scrollAccumulator = 0f;
        hasLoggedEntry = false;
        hasLoggedKeyboard = false;

        // 显示悬浮提示文字
        if (floatingText != null) floatingText.Show();

        // 隐藏 UI 面板
        if (uiPanel != null) uiPanel.Hide();

        // 恢复相机滚轮缩放
        if (thirdPersonCamera != null) thirdPersonCamera.SetZoomEnabled(true);

        Debug.Log("GameStartZone: 玩家离开触发区域");
    }

    /// <summary>
    /// 作用: 同步发光边框 Quad 的尺寸和位置，使其铺满 BoxCollider 的 XZ 平面
    /// </summary>
    private void SyncBorderQuadSize()
    {
        if (borderQuad == null || zoneCollider == null) return;

        // Quad 默认尺寸为 1×1（本地空间），映射到 BoxCollider 的 X 和 Z 轴
        Vector3 colliderSize = zoneCollider.size;
        Vector3 colliderCenter = zoneCollider.center;

        // 设置 Quad 缩放（XZ 平面展开）
        borderQuad.localScale = new Vector3(colliderSize.x, colliderSize.z, 1f);

        // 设置 Quad 位置到 Collider 中心高度（平放在地面，Y 可调）
        borderQuad.localPosition = new Vector3(colliderCenter.x, borderYOffset, colliderCenter.z);

        // 旋转 Quad 平放在地面（-90° 绕 X 轴）
        borderQuad.localRotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    /// <summary>
    /// 作用: 获取玩家是否在触发区域内
    /// </summary>
    public bool IsPlayerInside()
    {
        return isPlayerInside;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 作用: 编辑器下修改 Collider 尺寸或边框引用时自动同步
    /// </summary>
    void OnValidate()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<BoxCollider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
        if (Application.isPlaying) return;
        // 编辑器下延迟调用，确保所有组件就绪
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && borderQuad != null && zoneCollider != null)
                SyncBorderQuadSize();
        };
    }
#endif
}
