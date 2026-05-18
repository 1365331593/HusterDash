using UnityEngine;
using TMPro;   

/// <summary>
/// 里程记录器：从起点 Z=1 开始，记录玩家前进的总距离（米），并实时显示在 UI 上。
/// </summary>
public class DistanceTracker : MonoBehaviour
{
    [Header("引用设置")]
    [Tooltip("玩家对象（自动查找带 Player 标签的对象）")]
    public Transform player;

    [Tooltip("用于显示里程的 UI 文本组件（UGUI Text）")]
    public TMP_Text distanceText;           // 如果用 TextMeshPro，改为 TMP_Text

    [Header("参数设置")]
    [Tooltip("起点的 Z 坐标（米）")]
    public float startZ = 1f;

    [Tooltip("是否限制里程只增不减（防止玩家后退导致里程减少）")]
    public bool onlyIncrease = true;

    // 当前最大到达的 Z 坐标（用于 onlyIncrease = true）
    private float maxReachedZ;

    // 缓存的玩家 Transform（若未手动赋值则自动查找）
    private Transform cachedPlayer;

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

        // 缓存玩家引用
        cachedPlayer = player;

        // 初始化最大到达位置
        if (player != null)
            maxReachedZ = player.position.z;
        else
            maxReachedZ = startZ;

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
            // 直接根据当前 Z 计算（允许后退时里程减少）
            displayDistance = currentZ - startZ;
        }

        // 防止负数（比如玩家还没到达起点）
        if (displayDistance < 0f) displayDistance = 0f;

        // 更新 UI 文本
        UpdateDistanceDisplay(displayDistance);
    }

    /// <summary>
    /// 更新 UI 文本（带参数）
    /// </summary>
    private void UpdateDistanceDisplay(float distance)
    {
        if (distanceText == null) return;

        // 格式：保留一位小数或两位小数，加上单位 "m"
        distanceText.text = distance.ToString("F2") + " m";
    }

    /// <summary>
    /// 无参数版本，从当前状态重新计算并刷新（用于初始化）
    /// </summary>
    private void UpdateDistanceDisplay()
    {
        if (cachedPlayer == null) return;
        float currentZ = cachedPlayer.position.z;
        float dist = (onlyIncrease ? Mathf.Max(currentZ, maxReachedZ) : currentZ) - startZ;
        if (dist < 0) dist = 0;
        UpdateDistanceDisplay(dist);
    }

    /// <summary>
    /// 外部可调用：获取当前里程（米）
    /// </summary>
    public float GetCurrentDistance()
    {
        if (cachedPlayer == null) return 0f;
        float currentZ = cachedPlayer.position.z;
        float effectiveZ = onlyIncrease ? Mathf.Max(currentZ, maxReachedZ) : currentZ;
        float dist = effectiveZ - startZ;
        return dist < 0 ? 0f : dist;
    }
}