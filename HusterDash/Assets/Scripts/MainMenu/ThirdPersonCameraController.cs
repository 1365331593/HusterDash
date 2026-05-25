using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

/// <summary>
/// 第三人称自由视角相机控制器（纯 Input System 版）
/// 负责：光标锁定/解锁、按住 Alt 临时解锁鼠标、滚轮缩放及距离限制。
/// </summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("场景中的 Cinemachine FreeLook 虚拟相机。")]
    public CinemachineFreeLook freeLookCamera;

    [Header("灵敏度设置")]
    [Tooltip("正常游戏时的鼠标灵敏度。X = 水平旋转速度，Y = 垂直旋转速度。")]
    public Vector2 normalSensitivity = new Vector2(300f, 2f);
    [Tooltip("鼠标解锁时的灵敏度，建议设为(0,0)禁止视角旋转。")]
    public Vector2 unlockedSensitivity = new Vector2(0f, 0f);

    [Header("光标设置")]
    [Tooltip("启动时是否自动锁定鼠标。")]
    public bool lockCursorOnStart = true;

    [Header("输入动作引用")]
    [Tooltip("Alt 键的 Input Action (Player/CursorUnlock)。")]
    public InputActionReference cursorUnlockAction;
    [Tooltip("鼠标滚轮的 Input Action (Player/Zoom)，Axis 类型，绑 Mouse/Scroll/Y。")]
    public InputActionReference zoomAction;

    [Header("缩放参数")]
    [Tooltip("每格滚轮缩放的距离变化量（米）。")]
    public float zoomStep = 0.5f;
    [Tooltip("相机距离角色的最小距离（米）。")]
    public float minDistance = 2.5f;
    [Tooltip("相机距离角色的最大距离（米）。")]
    public float maxDistance = 8f;

    // 保存三个轨道相对于 Middle 的初始半径比例
    private float topRatio;
    private float bottomRatio;
    private bool isCursorLocked;

    void Start()
    {
        if (lockCursorOnStart) LockCursor();
        else UnlockCursor();

        if (freeLookCamera != null)
        {
            SetSensitivity(normalSensitivity);
            CacheOrbitRatios();   // 记录比例
        }

        if (cursorUnlockAction != null)
        {
            cursorUnlockAction.action.performed += OnAltPressed;
            cursorUnlockAction.action.canceled += OnAltReleased;
            cursorUnlockAction.action.Enable();
        }

        if (zoomAction != null)
            zoomAction.action.Enable();
    }

    void Update()
    {
        HandleZoom();
        ClampCameraDistance();
    }

    void OnDestroy()
    {
        if (cursorUnlockAction != null)
        {
            cursorUnlockAction.action.performed -= OnAltPressed;
            cursorUnlockAction.action.canceled -= OnAltReleased;
        }
    }

    /// <summary>
    /// 缓存 Top 和 Bottom 轨道相对于 Middle 的初始半径比例。
    /// </summary>
    private void CacheOrbitRatios()
    {
        float midRadius = freeLookCamera.m_Orbits[1].m_Radius;
        if (midRadius > 0.001f)
        {
            topRatio = freeLookCamera.m_Orbits[0].m_Radius / midRadius;
            bottomRatio = freeLookCamera.m_Orbits[2].m_Radius / midRadius;
        }
        else
        {
            // 保底比例
            topRatio = freeLookCamera.m_Orbits[0].m_Radius;
            bottomRatio = freeLookCamera.m_Orbits[2].m_Radius;
        }
    }

    /// <summary>
    /// 处理滚轮输入：直接增减 Middle 轨道半径，并同步 Top/Bottom 保持比例。
    /// </summary>
    private void HandleZoom()
    {
        if (freeLookCamera == null || zoomAction == null)
            return;

        float scroll = zoomAction.action.ReadValue<float>();
        if (Mathf.Abs(scroll) < 0.001f)
            return;

        // 计算变化量（不使用 deltaTime，保证每格滚轮有固定步长）
        float delta = -scroll * zoomStep;
        float newMid = freeLookCamera.m_Orbits[1].m_Radius + delta;

        // 硬钳制到安全范围，防止负值
        newMid = Mathf.Clamp(newMid, minDistance, maxDistance);

        // 应用新半径，并按比例设置 Top/Bottom
        freeLookCamera.m_Orbits[1].m_Radius = newMid;
        freeLookCamera.m_Orbits[0].m_Radius = newMid * topRatio;
        freeLookCamera.m_Orbits[2].m_Radius = newMid * bottomRatio;
    }

    /// <summary>
    /// 二次保障：每帧强制将 Middle 半径钳制在 [minDistance, maxDistance] 内，
    /// 并同步 Top 与 Bottom 的比例。
    /// </summary>
    private void ClampCameraDistance()
    {
        if (freeLookCamera == null)
            return;

        float midRadius = freeLookCamera.m_Orbits[1].m_Radius;
        float clampedMid = Mathf.Clamp(midRadius, minDistance, maxDistance);

        if (!Mathf.Approximately(midRadius, clampedMid))
        {
            freeLookCamera.m_Orbits[1].m_Radius = clampedMid;
            freeLookCamera.m_Orbits[0].m_Radius = clampedMid * topRatio;
            freeLookCamera.m_Orbits[2].m_Radius = clampedMid * bottomRatio;
        }
    }

    // ----- Alt 键处理 -----
    private void OnAltPressed(InputAction.CallbackContext ctx)
    {
        UnlockCursor();
        SetSensitivity(unlockedSensitivity);
    }

    private void OnAltReleased(InputAction.CallbackContext ctx)
    {
        LockCursor();
        SetSensitivity(normalSensitivity);
    }

    // ----- 光标控制 -----
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isCursorLocked = false;
    }

    private void SetSensitivity(Vector2 sens)
    {
        if (freeLookCamera == null) return;
        freeLookCamera.m_XAxis.m_MaxSpeed = sens.x;
        freeLookCamera.m_YAxis.m_MaxSpeed = sens.y;
    }

    public void ForceUnlockCursor() { UnlockCursor(); SetSensitivity(unlockedSensitivity); }
    public void ForceLockCursor() { LockCursor(); SetSensitivity(normalSensitivity); }
}