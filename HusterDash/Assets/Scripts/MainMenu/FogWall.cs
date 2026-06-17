using UnityEngine;

/*
 * 文件名: FogWall.cs
 * 作用: 雾墙边界组件，将 Inspector 中的雾视觉参数写入材质驱动 Shader
 * 主要功能:
 *     1. 在 Inspector 中配置雾颜色、密度、噪声和渐变参数
 *     2. 将参数写入材质，驱动 Shader 的雾效果
 *     3. 碰撞体尺寸和位置请直接在 BoxCollider 和 Transform 上手动调整
 */
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[ExecuteAlways]
public class FogWall : MonoBehaviour
{
    [Header("雾视觉效果 — 请在 Shader 材质面板调整")]

    [Tooltip("雾的颜色")]
    [SerializeField] private Color fogColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);

    [Tooltip("噪声纹理缩放，值越大雾团越细碎")]
    [Range(0.1f, 10.0f)]
    [SerializeField] private float noiseScale = 3.0f;

    [Tooltip("雾的密度，值越大越不透明（3.0以上基本全覆盖）")]
    [Range(0.5f, 5.0f)]
    [SerializeField] private float density = 1.5f;

    [Tooltip("噪声水平漂移速度")]
    [Range(0.0f, 0.5f)]
    [SerializeField] private float scrollSpeedX = 0.05f;

    [Tooltip("噪声垂直漂移速度")]
    [Range(0.0f, 0.5f)]
    [SerializeField] private float scrollSpeedY = 0.02f;

    [Tooltip("顶部开始虚化的位置（0=底部，1=顶部）")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float topFade = 0.6f;

    [Tooltip("左右边缘柔和过渡范围")]
    [Range(0.0f, 0.5f)]
    [SerializeField] private float edgeSoftness = 0.15f;

    // 缓存组件引用
    private MeshRenderer meshRenderer;
    private Material materialInstance;

    /*
     * 作用: 初始化缓存组件引用并创建材质实例
     */
    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        // 创建材质实例以避免修改资产文件
        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            materialInstance = new Material(meshRenderer.sharedMaterial);
            meshRenderer.material = materialInstance;
        }
    }

    /*
     * 作用: 启动时将参数写入材质
     */
    private void Start()
    {
        UpdateMaterialProperties();
    }

    /*
     * 作用: Inspector 中修改参数时自动同步（仅编辑器模式）
     */
    private void OnValidate()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                UpdateMaterialProperties();
        };
#endif
    }

    /*
     * 作用: 将 Inspector 参数写入材质
     */
    public void UpdateMaterialProperties()
    {
        if (meshRenderer == null) return;

        // 优先使用材质实例，其次使用 sharedMaterial
        Material mat = Application.isPlaying ? materialInstance : meshRenderer.sharedMaterial;
        if (mat == null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            WritePropertiesToBlock(block);
            meshRenderer.SetPropertyBlock(block);
            return;
        }

        WritePropertiesToMaterial(mat);
    }

    /*
     * 作用: 将参数写入 MaterialPropertyBlock
     */
    private void WritePropertiesToBlock(MaterialPropertyBlock block)
    {
        block.SetColor("_MainColor", fogColor);
        block.SetFloat("_NoiseScale", noiseScale);
        block.SetFloat("_Density", density);
        block.SetFloat("_ScrollSpeedX", scrollSpeedX);
        block.SetFloat("_ScrollSpeedY", scrollSpeedY);
        block.SetFloat("_TopFade", topFade);
        block.SetFloat("_EdgeSoftness", edgeSoftness);
    }

    /*
     * 作用: 将参数写入 Material
     */
    private void WritePropertiesToMaterial(Material mat)
    {
        mat.SetColor("_MainColor", fogColor);
        mat.SetFloat("_NoiseScale", noiseScale);
        mat.SetFloat("_Density", density);
        mat.SetFloat("_ScrollSpeedX", scrollSpeedX);
        mat.SetFloat("_ScrollSpeedY", scrollSpeedY);
        mat.SetFloat("_TopFade", topFade);
        mat.SetFloat("_EdgeSoftness", edgeSoftness);
    }

    /*
     * 作用: 销毁时清理材质实例
     */
    private void OnDestroy()
    {
        if (materialInstance != null)
        {
            if (Application.isPlaying)
                Destroy(materialInstance);
            else
                DestroyImmediate(materialInstance);

            materialInstance = null;
        }
    }

    /*
     * 作用: 在 Scene 视图中绘制碰撞体辅助 Gizmo（直接读取 BoxCollider 的实际尺寸）
     */
    private void OnDrawGizmosSelected()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) return;

        Gizmos.color = new Color(0.85f, 0.85f, 0.85f, 0.3f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(bc.center, bc.size);
    }
}
