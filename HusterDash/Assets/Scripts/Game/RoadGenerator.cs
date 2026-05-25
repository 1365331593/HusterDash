using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadGenerator : MonoBehaviour
{
    [Header("对象设置")]
    [Tooltip("玩家对象，用于获取位置和动态生成道路")]
    public Transform player;

    [Tooltip("道路块预制体（长度应为 1 米）")]
    public GameObject roadPrefab;

    [Header("生成参数")]
    [Tooltip("玩家身前多少米内必须铺满道路")]
    public float spawnDistance = 20f;

    [Tooltip("玩家身后多少米外的道路块会被回收（对象池）")]
    public float despawnDistance = 10f;

    [Tooltip("初始生成位置：玩家前方多少米开始（当 initialFromOrigin = false 时生效）")]
    public float startOffset = 1f;

    [Tooltip("道路块的长度（必须与预制体匹配，默认 1 米）")]
    public float blockLength = 1f;

    [Header("初始化设置")]
    [Tooltip("启用：游戏开始时从原点前方 startOffset 米处开始生成道路（忽略玩家初始位置）。\n禁用：从玩家当前位置前方 startOffset 米处开始生成。")]
    public bool initialFromOrigin = true;   // 新增开关，默认从原点开始

    // 内部数据
    private Queue<GameObject> activeRoads = new Queue<GameObject>();   // 当前激活的道路块队列
    private Queue<GameObject> inactivePool = new Queue<GameObject>();  // 回收的道路块对象池
    private float lastSpawnZ;               // 最后一个生成的道路块的 Z 坐标

    void Start()
    {
        if (initialFromOrigin)
        {
            // 从原点前方 startOffset 米处开始生成，忽略玩家初始位置
            lastSpawnZ = startOffset;
            // 向前生成直到覆盖 spawnDistance 范围
            while (lastSpawnZ < startOffset + spawnDistance)
            {
                SpawnRoad(lastSpawnZ);
                lastSpawnZ += blockLength;
            }
        }
        else
        {
            // 原逻辑：从玩家前方 startOffset 米处开始生成
            lastSpawnZ = player.position.z + startOffset;
            while (lastSpawnZ < player.position.z + startOffset + spawnDistance)
            {
                SpawnRoad(lastSpawnZ);
                lastSpawnZ += blockLength;
            }
        }
    }

    void Update()
    {
        // 获取玩家当前 Z 轴位置
        float playerZ = player.position.z;

        // 计算生成边界和回收边界
        float spawnLimit = playerZ + startOffset + spawnDistance; // 前方极限
        float despawnLimit = playerZ - despawnDistance;           // 后方回收极限

        // 1. 【生成】如果前方还没有铺满，继续生成新道路
        while (lastSpawnZ < spawnLimit)
        {
            SpawnRoad(lastSpawnZ);
            lastSpawnZ += blockLength;
        }

        // 2. 【回收】如果最旧的道路块已经落后于 despawnLimit，则回收
        while (activeRoads.Count > 0 && activeRoads.Peek().transform.position.z < despawnLimit)
        {
            GameObject oldestRoad = activeRoads.Dequeue();
            oldestRoad.SetActive(false);
            inactivePool.Enqueue(oldestRoad);
        }
    }

    /// <summary>
    /// 在指定 Z 坐标生成一个道路块，并为其设置车道方向和车辆生成器
    /// 方向规律：每2米（2个道路块）改变一次方向。
    /// 由于道路块长度为1米，因此：
    ///   Z坐标范围 [0,2)  -> 正向 (x=1)
    ///   [2,4) -> 负向 (x=-1)
    ///   [4,6) -> 正向 ...
    /// 如果希望从第一米开始正向（即 Z=1~3 正向，3~5 负向...），
    /// 只需将计算索引时的基准偏移调整为 1 即可。
    /// </summary>
    void SpawnRoad(float zPos)
    {
        GameObject road;

        // 从对象池取，如果没有则实例化
        if (inactivePool.Count > 0)
        {
            road = inactivePool.Dequeue();
            road.SetActive(true);
        }
        else
        {
            road = Instantiate(roadPrefab);
        }

        road.transform.position = new Vector3(0, 0, zPos);
        activeRoads.Enqueue(road);

        // ========== 修正后的方向计算（与玩家位置无关） ==========
        // 使用道路块自身的世界坐标 Z 计算组索引，确保每2米交替一次
        // 为了让第一米（z=1 附近）为正向，引入偏移 1 米
        float offset = 1f;   // 使 Z=1~3 为正向，3~5 为负向...
        int blockIndex = Mathf.FloorToInt((zPos - offset) / blockLength);
        int groupIndex = blockIndex / 2;           // 每2个块为一组
        float directionX = (groupIndex % 2 == 0) ? 1f : -1f;   // 组号偶数正向，奇数负向
        Vector3 laneDirection = new Vector3(directionX, 0, 0);
        Debug.Log($"[RoadGen] 生成道路块 z={zPos}, groupIndex={groupIndex}, 方向={(directionX > 0 ? "正向(→)" : "负向(←)")}");
        // ========================================================

        // 获取或添加 RoadLane 组件，并存储方向信息
        RoadLane laneInfo = road.GetComponent<RoadLane>();
        if (laneInfo == null) laneInfo = road.AddComponent<RoadLane>();
        laneInfo.direction = laneDirection;

        // 获取或添加车辆生成器组件（生成器会自动读取 RoadLane 的方向）
        BlockVehicleSpawner spawner = road.GetComponent<BlockVehicleSpawner>();
        if (spawner == null) spawner = road.AddComponent<BlockVehicleSpawner>();
    }
}