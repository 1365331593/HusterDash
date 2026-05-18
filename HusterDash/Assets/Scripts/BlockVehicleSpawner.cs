using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RoadLane))]
public class BlockVehicleSpawner : MonoBehaviour
{
    [Header("生成参数")]
    [SerializeField] private float minSpawnInterval = 0.5f;
    [SerializeField] private float maxSpawnInterval = 2.5f;
    [SerializeField] private float roadHalfWidth = 15f;
    [SerializeField] private float vehicleYSpawn = 0f;
    
    [Header("速度区间（每个道路块随机）")]
    [SerializeField] private float minBlockSpeed = 3f;
    [SerializeField] private float maxBlockSpeed = 8f;

    private RoadLane lane;
    private List<GameObject> spawnedVehicles = new List<GameObject>();
    private Coroutine spawnCoroutine;
    private float blockSpeed;  // 本道路块统一速度

    private void Awake()
    {
        lane = GetComponent<RoadLane>();
        // 为当前道路块随机一个速度（块内所有车辆共用）
        blockSpeed = Random.Range(minBlockSpeed, maxBlockSpeed);
    }

    private void OnEnable()
    {
        Debug.Log($"[Spawner] {name} OnEnable, 车道方向={lane.direction}");
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnVehicles());
    }

    private void OnDisable()
    {
        Debug.Log($"[Spawner] {name} OnDisable");
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        
        // 回收本块所有车辆（注意：此时车辆可能已经被回收过，所以需要检查状态）
        // 遍历副本，避免在回收过程中修改原列表导致异常
        List<GameObject> vehiclesToRemove = new List<GameObject>(spawnedVehicles);
        foreach (var vehicle in vehiclesToRemove)
        {
            if (vehicle != null && vehicle.activeInHierarchy)
            {
                var id = vehicle.GetComponent<VehicleIdentifier>();
                if (id != null && id.originalPrefab != null)
                    VehiclePool.Instance.ReturnVehicle(vehicle, id.originalPrefab);
                else
                    Destroy(vehicle);
            }
        }
        spawnedVehicles.Clear();
    }

    private IEnumerator SpawnVehicles()
    {
        Debug.Log($"[Spawner] {name} 开始协程，blockSpeed={blockSpeed}");
        while (true)
        {
            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(wait);
            SpawnOneVehicle();
        }
    }

    private void SpawnOneVehicle()
    {
        // 获取车道方向（必须是 ±X 方向）
        Vector3 moveDir = lane.direction;
        if (Mathf.Approximately(moveDir.x, 0))
        {
            Debug.LogWarning($"道路块 {name} 的方向为 {moveDir}，已强制改为右向");
            moveDir = Vector3.right;
        }
        
        // 根据移动方向决定生成在左端还是右端
        float spawnX = moveDir.x > 0 ? -roadHalfWidth : roadHalfWidth;
        Vector3 spawnPos = new Vector3(spawnX, vehicleYSpawn, transform.position.z);
        Debug.Log($"[Spawner] {name} 生成: dir={moveDir}, roadHalfWidth={roadHalfWidth}, spawnX={spawnX}, spawnPos={spawnPos}");

        // 从池中获取车辆
        GameObject originalPrefab;
        GameObject vehicle = VehiclePool.Instance.GetRandomVehicle(out originalPrefab);
        if (vehicle == null)
        {
            Debug.LogError($"[Spawner] {name} 从池中获取车辆失败！");
            return;
        }
        
        vehicle.transform.position = spawnPos;
        vehicle.transform.rotation = Quaternion.identity;  // 先重置旋转

        // 获取玩家引用
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) player = Camera.main?.transform;

        VehicleMovement moveScript = vehicle.GetComponent<VehicleMovement>();
        if (moveScript != null)
        {
            // 传递本 spawner 的引用，以便车辆回收时通知移除
            moveScript.Initialize(moveDir, originalPrefab, player, roadHalfWidth, this);
            moveScript.SetSpeed(blockSpeed);   // 使用本道路块的统一速度
        }

        vehicle.SetActive(true);
        spawnedVehicles.Add(vehicle);
        Debug.Log($"[Spawner] {name} 车辆生成完毕: {vehicle.name}, 初始位置={vehicle.transform.position}, active={vehicle.activeSelf}");
    }

    /// <summary>
    /// 由车辆在回收时调用，从本道路块的车辆列表中移除该车辆
    /// </summary>
    public void RemoveVehicle(GameObject vehicle)
    {
        if (spawnedVehicles.Contains(vehicle))
            spawnedVehicles.Remove(vehicle);
    }
}