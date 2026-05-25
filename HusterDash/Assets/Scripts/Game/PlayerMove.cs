using UnityEngine;
using UnityEngine.InputSystem;  // 必须引用新输入系统命名空间

/// <summary>
/// 玩家移动控制脚本 - 适配新版 Input System（复用已有 PlayerControls 资产）
/// 需要挂载到玩家角色上，并且场景中需要有一个 PlayerInput 组件。
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerMove : MonoBehaviour
{
    [Header("移动速度")]
    [Tooltip("玩家移动速度（米/秒）")]
    public float speed = 3f;

    [Header("动画混合参数")]
    [Tooltip("Animator 中控制动画混合（Idle/Walk/Run）的参数名，默认 Speed")]
    public string blendParam = "Speed";

    [Header("动画播放速度参数")]
    [Tooltip("Animator 中控制动画剪辑播放速度的参数名（需与状态机中绑定的 Float 参数一致）")]
    public string animSpeedParam = "AnimSpeed";

    [Tooltip("游戏中行走时的动画播放倍数（例如 2 表示二倍速）")]
    public float gameAnimSpeed = 2f;

    [Header("输入系统设置（可选）")]
    [Tooltip("Action Map 名称（如果你使用了非默认的 Map，可以在这里指定；留空则自动搜索）")]
    public string actionMapName = "Player";   // 改为你已有的 Map 名称 "Player"
    
    [Tooltip("移动 Action 名称（必须与 Input Action Asset 中定义的一致）")]
    public string moveActionName = "Move";    // 已有的 Move 动作

    // 输入系统相关
    private PlayerInput playerInput;
    private InputAction moveAction;          // 移动输入动作
    private Vector2 moveInput;               // 当前帧的移动输入值 (x, z)

    private Animator anim;
    private Vector3 moveDirection;           // 实际移动方向（世界坐标系）

    private void Awake()
    {
        // 获取 PlayerInput 组件
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PlayerMove: 未找到 PlayerInput 组件！请为玩家对象添加 PlayerInput 组件并关联 PlayerControls 资产。");
            return;
        }

        // 尝试通过 Action Map 名称和 Action 名称获取输入动作
        // 方法1：直接通过 actions 字典查找（推荐，因为 Move 是全局唯一的）
        moveAction = playerInput.actions[moveActionName];
        if (moveAction == null && !string.IsNullOrEmpty(actionMapName))
        {
            // 方法2：先获取 Map，再获取 Action（适用于有多个同名 Action 在不同 Map 中的情况）
            var actionMap = playerInput.actions.FindActionMap(actionMapName);
            if (actionMap != null)
                moveAction = actionMap.FindAction(moveActionName);
        }

        if (moveAction == null)
        {
            Debug.LogError($"PlayerMove: 在 Input Action Asset 中找不到名为 '{moveActionName}' 的 Action！请检查 PlayerControls 资产，确保存在该 Action，且当前使用的 Action Map 已激活。");
        }
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
            Debug.LogError("PlayerMove: 未找到 Animator 组件！");
    }

    private void OnEnable()
    {
        // 启用输入动作（确保可以接收输入）
        if (moveAction != null)
            moveAction.Enable();
    }

    private void OnDisable()
    {
        // 禁用输入动作（避免在游戏结束或其他状态下误接收输入）
        if (moveAction != null)
            moveAction.Disable();
    }

    private void Update()
    {
        if (moveAction == null) return;

        // 从 Input System 读取移动输入值（Vector2，x=左右，y=前后）
        moveInput = moveAction.ReadValue<Vector2>();

        // 构建移动向量 (x, 0, z)
        moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        // 面向移动方向（只有当有有效输入时才转向）
        if (moveDirection.magnitude > 0.01f)
            transform.LookAt(transform.position + moveDirection);

        // 执行移动（使用世界坐标系，不受相机旋转影响）
        transform.position += moveDirection * speed * Time.deltaTime;

        // 更新动画参数
        UpdateAnim();
    }

    /// <summary>
    /// 更新 Animator 中的混合参数和动画速度参数
    /// </summary>
    private void UpdateAnim()
    {
        if (anim == null) return;

        // 1. 动画混合值：移动输入的大小（范围 0~1，最大为 1）
        float blendValue = moveDirection.magnitude;
        anim.SetFloat(blendParam, blendValue);

        // 2. 动画播放速度：移动时使用 gameAnimSpeed，静止时为 0
        float speedValue = (moveDirection.magnitude > 0.01f) ? gameAnimSpeed : 0f;
        anim.SetFloat(animSpeedParam, speedValue);
    }

    // 碰撞检测（与原来保持一致）
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Vehicle"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
    }
}