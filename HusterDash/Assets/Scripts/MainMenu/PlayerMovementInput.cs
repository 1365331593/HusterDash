using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家输入接收器（配合 Player Input 组件使用）
/// 将 WASD 移动和右键切换奔跑的事件传递给 PlayerMovement 脚本。
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovementInput : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("负责执行实际移动的 PlayerMovement 脚本。")]
    public PlayerMovement playerMovement;

    [Header("调试")]
    [Tooltip("当前帧的移动输入值（x=左右，y=前后）。")]
    [SerializeField] private Vector2 moveInput;

    void Awake()
    {
        // 自动获取同物体上的 PlayerMovement 组件
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    /// <summary>
    /// 由 Player Input 组件的 Move 事件调用（无需选择特定阶段）。
    /// 持续接收 WASD 输入并转发给移动脚本。
    /// </summary>
    /// <param name="context">Input System 回调上下文，包含 Vector2 输入值。</param>
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();

        if (playerMovement != null)
            playerMovement.SetMoveInput(moveInput);
    }

    /// <summary>
    /// 由 Player Input 组件的 ToggleRun 事件调用（触发阶段选 Performed）。
    /// 在鼠标右键完整点击一次后切换行走/奔跑状态。
    /// </summary>
    /// <param name="context">Input System 回调上下文。</param>
    public void OnToggleRun(InputAction.CallbackContext context)
    {
        // 只在动作完成的瞬间触发一次
        if (context.performed && playerMovement != null)
            playerMovement.ToggleWalkRun();
    }
}