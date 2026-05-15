using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadGenerator : MonoBehaviour
{
    [Header("对象设置")]
    public Transform player;            // 玩家对象
    public GameObject roadPrefab;       // 道路预制体 (1米长)

    [Header("生成参数")]
    [Tooltip("玩家身前多少米内铺满道路")]
    public float spawnDistance = 20f;   // 身前生成距离

    [Tooltip("玩家身后多少米外开始回收道路")]
    public float despawnDistance = 10f; // 身后回收距离

    [Tooltip("初始生成位置：玩家前方多少米开始")]
    public float startOffset = 1f;      // 初始偏移 (玩家前方1米)

    [Tooltip("道路块的长度（与预制体匹配）")]
    public float blockLength = 1f;

    // 内部数据
    private Queue<GameObject> activeRoads = new Queue<GameObject>();
    private Queue<GameObject> inactivePool = new Queue<GameObject>();
    private float lastSpawnZ;           // 最后一个生成的道路块的 Z 坐标
    private float initialPlayerZ;       // 用于计算方向索引的起始 Z 值

    void Start()
    {
        // 记录玩家初始位置，用于方向索引计算
        initialPlayerZ = player.position.z;

        // 初始生成：从玩家前方 startOffset 米开始
        // 假设玩家在 (0,0,0)，startOffset = 1，则从 z = 1 开始生成
        lastSpawnZ = player.position.z + startOffset;

        // 向前生成直到覆盖 spawnDistance 范围
        while (lastSpawnZ < player.position.z + startOffset + spawnDistance)
        {
            SpawnRoad(lastSpawnZ);
            lastSpawnZ += blockLength;
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
        // 使用 Peek() 查看队列中最先进入的道路块
        while (activeRoads.Count > 0 && activeRoads.Peek().transform.position.z < despawnLimit)
        {
            GameObject oldestRoad = activeRoads.Dequeue();
            oldestRoad.SetActive(false);
            inactivePool.Enqueue(oldestRoad);
        }
    }

    // SpawnRoad 函数
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

        // 计算车道方向
        // 计算当前道路块相对于初始生成起点的索引 (按1米单位)
        int blockIndex = Mathf.RoundToInt((zPos - initialPlayerZ) / blockLength);

        // 每 2 米交替一次方向
        float directionX = (blockIndex % 4 < 2) ? 1f : -1f;
        Vector3 laneDirection = new Vector3(directionX, 0, 0);

        // 确保道路块上有 RoadLane 组件并存储方向
        RoadLane laneInfo = road.GetComponent<RoadLane>();
        if (laneInfo == null)
        {
            laneInfo = road.AddComponent<RoadLane>();
        }
        laneInfo.direction = laneDirection;
    }
}