using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家移动控制脚本 - 适配新版 Input System（复用已有 PlayerControls 资产）
/// 新增功能：鼠标右键切换行走/奔跑，对应的动画由 Animator 中的 Bool 参数控制。
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerMove : MonoBehaviour
{
    [Header("移动速度")]
    [Tooltip("行走速度（米/秒）")]
    public float walkSpeed = 3f;
    [Tooltip("奔跑速度（米/秒）")]
    public float runSpeed = 6f;

    [Header("动画参数")]
    [Tooltip("Animator 中控制动画混合（Idle/Walk）的 Float 参数名，默认 Speed")]
    public string blendParam = "Speed";
    [Tooltip("Animator 中控制奔跑状态的 Bool 参数名（需在状态机中创建）")]
    public string isRunningParam = "isRunning";
    [Tooltip("Animator 中控制动画剪辑播放速度的 Float 参数名（仅行走时使用）")]
    public string animSpeedParam = "AnimSpeed";
    [Tooltip("行走时动画的播放倍数（例如 2 表示二倍速）")]
    public float gameAnimSpeed = 2f;

    [Header("移动范围限制")]
    [Tooltip("X 轴左边界（世界坐标）")]
    public float minX = -15f;
    [Tooltip("X 轴右边界（世界坐标）")]
    public float maxX = 15f;
    [Tooltip("Z 轴后边界（世界坐标）")]
    public float minZ = -2f;

    [Header("输入系统设置")]
    [Tooltip("Action Map 名称（与 PlayerControls 资产中的 Map 名一致）")]
    public string actionMapName = "Player";
    [Tooltip("移动 Action 名称（默认 Move）")]
    public string moveActionName = "Move";
    [Tooltip("切换奔跑 Action 名称（默认 ToggleRun，需在 PlayerControls 中定义）")]
    public string toggleRunActionName = "ToggleRun";

    // 组件引用
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction toggleRunAction;
    private Animator anim;

    // 输入状态
    private Vector2 moveInput;
    private Vector3 moveDirection;
    private bool isRunning = false;          // 当前是否奔跑

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerMove: 未找到 PlayerInput 组件！请为玩家对象添加 PlayerInput 组件并关联 PlayerControls 资产。");
            return;
        }

        // 获取移动 Action
        moveAction = playerInput.actions.FindAction(moveActionName);
        if (moveAction == null && !string.IsNullOrEmpty(actionMapName))
        {
            var actionMap = playerInput.actions.FindActionMap(actionMapName);
            if (actionMap != null)
                moveAction = actionMap.FindAction(moveActionName);
        }
        if (moveAction == null)
            Debug.LogError($"PlayerMove: 找不到 Action '{moveActionName}'，请检查 PlayerControls 资产。");

        // 获取切换奔跑 Action
        toggleRunAction = playerInput.actions.FindAction(toggleRunActionName);
        if (toggleRunAction == null)
            Debug.LogWarning($"PlayerMove: 找不到 Action '{toggleRunActionName}'，右键切换奔跑功能将不可用。");
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
            Debug.LogError("PlayerMove: 未找到 Animator 组件！");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        toggleRunAction?.Enable();

        // 注册右键点击事件（仅在动作完成时触发一次）
        if (toggleRunAction != null)
            toggleRunAction.performed += OnToggleRun;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        toggleRunAction?.Disable();

        if (toggleRunAction != null)
            toggleRunAction.performed -= OnToggleRun;
    }

    private void Update()
    {
        if (moveAction == null) return;

        // 读取移动输入
        moveInput = moveAction.ReadValue<Vector2>();
        moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        // 面向移动方向
        if (moveDirection.magnitude > 0.01f)
            transform.LookAt(transform.position + moveDirection);

        // 根据状态选择速度，计算目标位置并钳制到边界内
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 targetPos = transform.position + moveDirection * currentSpeed * Time.deltaTime;
        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.z = Mathf.Max(targetPos.z, minZ);
        transform.position = targetPos;

        // 更新动画
        UpdateAnim();
    }

    /// <summary>
    /// 更新 Animator 参数
    /// </summary>
    private void UpdateAnim()
    {
        if (anim == null) return;

        // 1. 移动量（0~1），用于 Idle / Walk 切换
        float blendValue = moveDirection.magnitude;
        anim.SetFloat(blendParam, blendValue);

        // 2. 是否奔跑（Bool 参数，需在 Animator 中创建）
        anim.SetBool(isRunningParam, isRunning);

        // 3. 动画播放速度：仅在行走移动时使用 gameAnimSpeed，奔跑和静止都不修改（保持 1 倍速）
        if (blendValue > 0.01f && !isRunning)
            anim.SetFloat(animSpeedParam, gameAnimSpeed);
        else
            anim.SetFloat(animSpeedParam, 1f);   // 静止或奔跑时恢复默认速度
    }

    /// <summary>
    /// 右键点击回调：切换行走/奔跑
    /// </summary>
    private void OnToggleRun(InputAction.CallbackContext context)
    {
        if (context.performed)
            isRunning = !isRunning;
    }

    // 原有碰撞检测逻辑保持不变
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Vehicle"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
    }
}