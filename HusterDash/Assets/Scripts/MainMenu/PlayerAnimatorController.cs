using UnityEngine;

/// <summary>
/// 角色动画控制器
/// 职责：
/// 1. 封装Animator动画参数的设置逻辑
/// 2. 提供统一的接口给外部脚本调用
/// 3. 便于后续扩展更复杂的动画逻辑
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorController : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("Unity Animator组件的引用")]
    public Animator animator;

    [Header("动画参数名")]
    [Tooltip("Animator中用于控制移动动画混合的Float参数名，默认Speed")]
    public string speedParamName = "Speed";
    
    [Tooltip("Animator中用于判断是否移动的Bool参数名，默认IsMoving")]
    public string isMovingParamName = "IsMoving";

    /// <summary>当前Speed参数值，用于调试查看</summary>
    [SerializeField] private float currentSpeed;
    
    /// <summary>当前IsMoving参数值，用于调试查看</summary>
    [SerializeField] private bool currentIsMoving;

    void Awake()
    {
        // 自动获取Animator组件
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 设置移动动画的Speed参数
    /// 0 = 待机（Idle），1 = 行走动画，2 = 奔跑动画
    /// 数值在0到1或1到2之间时会由Blend Tree自动混合相邻动画
    /// </summary>
    /// <param name="value">速度值，范围0~2</param>
    public void SetSpeed(float value)
    {
        currentSpeed = value;
        animator.SetFloat(speedParamName, value);
    }

    /// <summary>
    /// 设置是否正在移动
    /// true时从Idle切换到Locomotion Blend Tree
    /// false时从Locomotion切换回Idle
    /// </summary>
    /// <param name="value">是否正在移动</param>
    public void SetIsMoving(bool value)
    {
        currentIsMoving = value;
        animator.SetBool(isMovingParamName, value);
    }
}