using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 里程记录器：从起点 Z=1 开始，记录玩家前进的总距离（米），并实时显示在 UI 上。
/// 同时自动保存历史最佳里程到 PlayerPrefs。
/// </summary>
public class DistanceTracker : MonoBehaviour
{
    [Header("引用设置")]
    [Tooltip("玩家对象（自动查找带 Player 标签的对象）")]
    public Transform player;

    [Tooltip("用于显示里程的 UI 文本组件（TextMeshPro）")]
    public TMP_Text distanceText;

    [Header("参数设置")]
    [Tooltip("起点的 Z 坐标（米）")]
    public float startZ = 1f;

    [Tooltip("是否限制里程只增不减（防止玩家后退导致里程减少）")]
    public bool onlyIncrease = true;

    [Header("历史最佳")]
    [Tooltip("存储最佳里程的 PlayerPrefs 键名")]
    public string bestKey = "BestDistance";
    [Tooltip("是否在控制台输出最佳里程更新")]
    public bool logBestUpdate = true;

    // 当前最大到达的 Z 坐标（用于 onlyIncrease = true）
    private float maxReachedZ;
    // 缓存的玩家 Transform
    private Transform cachedPlayer;
    // 缓存的最佳里程（从 PlayerPrefs 读取）
    private float bestDistance;

    void Start()
    {
        // 如果未手动指定玩家，尝试通过标签查找
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
                Debug.LogError("DistanceTracker: 未找到玩家对象！请手动指定或确保玩家有 'Player' 标签。");
        }

        cachedPlayer = player;

        // 初始化最大到达位置
        if (player != null)
            maxReachedZ = player.position.z;
        else
            maxReachedZ = startZ;

        // 读取历史最佳里程
        bestDistance = PlayerPrefs.GetFloat(bestKey, 0f);
        if (logBestUpdate)
            Debug.Log($"历史最佳里程: {bestDistance:F2} m");

        // 立即更新一次显示
        UpdateDistanceDisplay();
    }

    void Update()
    {
        if (cachedPlayer == null) return;

        float currentZ = cachedPlayer.position.z;
        float displayDistance;

        if (onlyIncrease)
        {
            // 记录玩家到达过的最大 Z 值
            if (currentZ > maxReachedZ)
                maxReachedZ = currentZ;
            displayDistance = maxReachedZ - startZ;
        }
        else
        {
            displayDistance = currentZ - startZ;
        }

        if (displayDistance < 0f) displayDistance = 0f;

        // 检查是否刷新最佳记录
        if (displayDistance > bestDistance)
        {
            bestDistance = displayDistance;
            PlayerPrefs.SetFloat(bestKey, bestDistance);
            PlayerPrefs.Save();  // 立即写入磁盘
            if (logBestUpdate)
                Debug.Log($"新纪录！最佳里程: {bestDistance:F2} m");
        }

        // 更新 UI 文本
        UpdateDistanceDisplay(displayDistance);
    }

    private void UpdateDistanceDisplay(float distance)
    {
        if (distanceText == null) return;
        distanceText.text = distance.ToString("F2") + " m";
    }

    private void UpdateDistanceDisplay()
    {
        if (cachedPlayer == null) return;
        float currentZ = cachedPlayer.position.z;
        float dist = (onlyIncrease ? Mathf.Max(currentZ, maxReachedZ) : currentZ) - startZ;
        if (dist < 0) dist = 0;
        UpdateDistanceDisplay(dist);
    }

    /// <summary>
    /// 获取当前本次游戏的里程（米）
    /// </summary>
    public float GetCurrentDistance()
    {
        if (cachedPlayer == null) return 0f;
        float currentZ = cachedPlayer.position.z;
        float effectiveZ = onlyIncrease ? Mathf.Max(currentZ, maxReachedZ) : currentZ;
        float dist = effectiveZ - startZ;
        return dist < 0 ? 0f : dist;
    }

    /// <summary>
    /// 获取历史最佳里程（米）
    /// </summary>
    public float GetBestDistance()
    {
        return bestDistance;
    }
}