using UnityEngine;

/// <summary>
/// 文件名: PortalVFX.cs
/// 作用: 控制装饰性传送门的视觉效果，包括上下浮动、Shader 参数驱动和粒子系统管理
/// 主要功能:
///     1. 驱动传送门上下正弦浮动（±0.2m）
///     2. 驱动 Shader 漩涡旋转速度等动态参数
///     3. 管理边缘光点粒子系统
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class PortalVFX : MonoBehaviour
{
    [Header("浮动动画")]
    [Tooltip("上下浮动的幅度（米）")]
    [SerializeField] private float floatAmplitude = 0.2f;

    [Tooltip("浮动频率（Hz），数值越大浮动越快")]
    [SerializeField] private float floatFrequency = 0.8f;

    [Tooltip("随机相位偏移，使多个传送门浮动不同步")]
    [SerializeField] private bool randomPhase = true;

    [Header("漩涡效果")]
    [Tooltip("漩涡旋转速度（每秒弧度）")]
    [SerializeField] private float vortexSpeed = 1.5f;

    [Tooltip("扭曲强度")]
    [SerializeField] private float distortStrength = 0.25f;

    [Tooltip("中心微光强度")]
    [SerializeField] private float centerGlow = 0.08f;

    [Header("粒子效果")]
    [Tooltip("边缘光点粒子系统（可选，不拖入则不启用）")]
    [SerializeField] private ParticleSystem edgeParticles;

    // 内部状态
    private float phaseOffset;
    private Vector3 baseLocalPosition;
    private Material materialInstance;
    private static readonly int VortexSpeedID = Shader.PropertyToID("_VortexSpeed");
    private static readonly int DistortStrengthID = Shader.PropertyToID("_DistortStrength");
    private static readonly int CenterGlowID = Shader.PropertyToID("_CenterGlow");

    private void Awake()
    {
        // 记录本地坐标作为浮动基准点。
        // 传送门是道路块的子物体，使用本地坐标可避免对象池复用
        // 时世界位置在 SetActive 之后才更新导致的错位问题。
        baseLocalPosition = transform.localPosition;

        // 为每个传送门生成随机相位，避免多个传送门浮动完全同步
        if (randomPhase)
        {
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        // 创建材质实例，避免修改共享材质影响其他传送门
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            materialInstance = renderer.material;
        }
    }

    private void Update()
    {
        // 驱动上下浮动动画
        HandleFloatingAnimation();

        // 驱动 Shader 动态参数
        HandleShaderParameters();
    }

    /// <summary>
    /// 作用: 使用正弦函数驱动传送门在 Y 轴上上下浮动。
    ///       操作本地坐标而非世界坐标，确保传送门跟随父物体（道路块）移动。
    /// </summary>
    private void HandleFloatingAnimation()
    {
        float floatOffset = Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f + phaseOffset) * floatAmplitude;
        Vector3 newLocal = baseLocalPosition;
        newLocal.y += floatOffset;
        transform.localPosition = newLocal;
    }

    /// <summary>
    /// 作用: 将漩涡速度、扭曲强度等参数实时写入 Shader 材质
    /// </summary>
    private void HandleShaderParameters()
    {
        if (materialInstance == null) return;

        materialInstance.SetFloat(VortexSpeedID, vortexSpeed);
        materialInstance.SetFloat(DistortStrengthID, distortStrength);
        materialInstance.SetFloat(CenterGlowID, centerGlow);
    }

    private void OnDestroy()
    {
        // 清理材质实例，避免内存泄漏
        if (materialInstance != null)
        {
            Destroy(materialInstance);
        }
    }

    /// <summary>
    /// 作用: 设置浮动幅度
    /// </summary>
    public void SetFloatAmplitude(float amplitude)
    {
        floatAmplitude = amplitude;
    }

    /// <summary>
    /// 作用: 设置浮动频率
    /// </summary>
    public void SetFloatFrequency(float frequency)
    {
        floatFrequency = frequency;
    }

    /// <summary>
    /// 作用: 设置漩涡旋转速度
    /// </summary>
    public void SetVortexSpeed(float speed)
    {
        vortexSpeed = speed;
    }

    #if UNITY_EDITOR
    /// <summary>
    /// 作用: 在编辑器中选中传送门时绘制 Gizmos，便于预览浮动范围
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.314f, 0.604f, 0.906f, 0.3f);

        // 绘制浮动范围的上下边界（使用本地坐标转换到世界空间）
        Vector3 worldCenter = transform.parent != null
            ? transform.parent.TransformPoint(baseLocalPosition)
            : transform.position;
        Vector3 top = worldCenter + Vector3.up * floatAmplitude;
        Vector3 bottom = worldCenter - Vector3.up * floatAmplitude;

        Gizmos.DrawLine(bottom, top);
        Gizmos.DrawWireSphere(top, 0.05f);
        Gizmos.DrawWireSphere(bottom, 0.05f);
    }
    #endif
}
