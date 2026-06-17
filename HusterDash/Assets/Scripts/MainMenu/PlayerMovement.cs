using UnityEngine;

/// <summary>
/// 玩家角色移动脚本
/// 职责：
/// 1. 执行实际的物理移动（通过CharacterController）
/// 2. 管理重力和垂直速度，支持斜坡行走和从高处跌落
/// 3. 将输入方向转换为相对相机的世界方向
/// 4. 平滑旋转角色面向移动方向
/// 5. 驱动动画参数（Speed、IsMoving）
/// 6. 管理行走/奔跑状态的切换
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("Unity的角色控制器组件，处理碰撞和移动")]
    public CharacterController characterController;
    
    [Tooltip("动画控制器脚本，管理动画参数与状态")]
    public PlayerAnimatorController animController;

    [Header("相机")]
    [Tooltip("主相机Transform，用于将输入方向转换为世界方向")]
    public Transform playerCamera;

    [Header("移动速度")]
    [Tooltip("行走模式下的移动速度（米/秒）")]
    public float walkSpeed = 3f;
    
    [Tooltip("奔跑模式下的移动速度（米/秒）")]
    public float runSpeed = 6f;

    [Header("转向")]
    [Tooltip("角色面朝方向平滑过渡的时间（秒），越小转向越灵敏")]
    public float rotationSmoothTime = 0.1f;

    [Header("重力")]
    [Tooltip("重力加速度（米/秒²），角色在空中时每帧累加")]
    public float gravity = -9.81f;
    
    [Tooltip("角色在地面时施加的向下力，防止浮空抖动")]
    public float groundedForce = -2f;

    [Header("调试信息")]
    [Tooltip("当前是否处于奔跑模式")]
    [SerializeField] private bool isRunning = false;
    
    [Tooltip("当前计算出的世界空间移动方向")]
    [SerializeField] private Vector3 moveDirection;

    /// <summary>当前帧的WASD输入值（x=水平，y=垂直）</summary>
    private Vector2 currentInput;
    
    /// <summary>角色转向平滑插值所需的角速度变量</summary>
    private float rotationVelocity;
    
    /// <summary>角色垂直方向的速度（Y轴），用于重力累加和跌落</summary>
    private float verticalVelocity;

    void Awake()
    {
        // 自动获取组件引用
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (animController == null)
            animController = GetComponent<PlayerAnimatorController>();

        // 如果未手动指定相机，尝试查找场景中的主相机
        if (playerCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                playerCamera = mainCam.transform;
        }
    }

    void Update()
    {
        // 每帧执行移动逻辑
        HandleMovement();
        // 更新动画参数
        UpdateAnimation();
    }

    /// <summary>
    /// 由PlayerMovementInput调用，存储当前帧的WASD输入
    /// </summary>
    /// <param name="input">WASD输入向量（x=左右，y=前后）</param>
    public void SetMoveInput(Vector2 input)
    {
        currentInput = input;
    }

    /// <summary>
    /// 核心移动逻辑：
    /// 1. 检测是否在地面并计算重力（垂直速度）
    /// 2. 将输入方向转换为以相机为参考的世界方向
    /// 3. 合并水平移动与垂直速度，通过CharacterController移动角色
    /// 4. 平滑旋转角色朝向移动方向
    /// CharacterController内置的Step Offset会自动跨过低矮台阶，
    /// Slope Limit会自动处理斜坡行走。
    /// </summary>
    private void HandleMovement()
    {
        // ----- 地面检测与重力计算 -----
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            // 在地面时保持一个微小的向下力，确保持续贴地
            verticalVelocity = groundedForce;
        }
        else
        {
            // 在空中时累加重力加速度
            verticalVelocity += gravity * Time.deltaTime;
        }

        // ----- 计算水平移动方向 -----
        // 获取输入的大小（0~1之间，表示移动强度）
        float inputMagnitude = currentInput.magnitude;
        
        // 归一化输入方向（防止斜向移动速度过快）
        Vector3 inputDirection = new Vector3(currentInput.x, 0f, currentInput.y).normalized;

        // 如果玩家有输入，计算相对相机的世界方向
        if (inputMagnitude > 0f && playerCamera != null)
        {
            // 获取相机的正前方和正右方（忽略Y轴分量，保证角色在地面移动）
            Vector3 cameraForward = playerCamera.forward;
            Vector3 cameraRight = playerCamera.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // 将输入方向转换为世界空间方向：前方 * 输入Y + 右方 * 输入X
            moveDirection = cameraForward * inputDirection.z + cameraRight * inputDirection.x;
        }
        else
        {
            // 没有输入时，不移动
            moveDirection = Vector3.zero;
        }

        // ----- 执行移动（合并水平移动与垂直速度）-----
        if (moveDirection.magnitude > 0f)
        {
            // 根据当前模式选择速度
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            // 合并水平移动方向和垂直速度（重力）
            Vector3 motion = moveDirection.normalized * currentSpeed * Time.deltaTime;
            motion.y = verticalVelocity * Time.deltaTime;
            // CharacterController.Move自动处理斜坡行走和台阶跨越
            characterController.Move(motion);

            // ----- 平滑旋转角色朝向移动方向 -----
            // 计算目标朝向角度
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            // 使用SmoothDampAngle平滑插值当前角度到目标角度
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, 
                ref rotationVelocity, rotationSmoothTime);
            // 设置角色旋转
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
        else
        {
            // 无输入时仅应用重力，保持角色贴地或下落
            Vector3 motion = new Vector3(0f, verticalVelocity * Time.deltaTime, 0f);
            characterController.Move(motion);
        }
    }

    /// <summary>
    /// 更新动画控制器的参数：
    /// - Speed：0=待机，1=行走，2=奔跑（Blend Tree混合使用）
    /// - IsMoving：是否有移动输入
    /// </summary>
    private void UpdateAnimation()
    {
        if (animController == null) return;

        // 计算当前实际移动速度（米/秒），用于驱动Blend Tree
        float currentSpeed = 0f;
        if (currentInput.magnitude > 0f)
        {
            // 速度为Vector3的水平长度，反映实际世界移动速度
            currentSpeed = new Vector3(
                characterController.velocity.x, 
                0f, 
                characterController.velocity.z
            ).magnitude;
        }

        // 根据当前模式确定Speed参数值
        // Blend Tree中：0=待机，1=行走动画，2=奔跑动画
        float animSpeed = 0f;
        if (currentInput.magnitude > 0f)
        {
            animSpeed = isRunning ? 2f : 1f;
        }

        animController.SetSpeed(animSpeed);
        animController.SetIsMoving(currentInput.magnitude > 0f);
    }

    /// <summary>
    /// 切换行走/奔跑状态（由右键点击触发）
    /// </summary>
    public void ToggleWalkRun()
    {
        isRunning = !isRunning;
    }

    /// <summary>
    /// 公开属性：当前是否在奔跑
    /// </summary>
    public bool IsRunning => isRunning;
}