using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class VehiclePool : MonoBehaviour
{
    public static VehiclePool Instance { get; private set; }

    [SerializeField] private GameObject[] vehiclePrefabs;
    
    [Tooltip("对象池一开始准备多少个备用对象")]
    [SerializeField] private int defaultCapacity = 10;
    
    [Tooltip("对象池最多能存多少个对象")]
    [SerializeField] private int maxSize = 30;

    private Dictionary<GameObject, IObjectPool<GameObject>> pools = new Dictionary<GameObject, IObjectPool<GameObject>>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public GameObject GetRandomVehicle(out GameObject originalPrefab)
    {
        originalPrefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
        if (!pools.ContainsKey(originalPrefab)) CreatePool(originalPrefab);
        GameObject obj = pools[originalPrefab].Get();

        var id = obj.GetComponent<VehicleIdentifier>();
        if (id == null) id = obj.AddComponent<VehicleIdentifier>();
        id.originalPrefab = originalPrefab;
        
        return obj;
    }

    public void ReturnVehicle(GameObject vehicle, GameObject originalPrefab)
    {
        if (vehicle == null || originalPrefab == null) return;
        if (!pools.ContainsKey(originalPrefab))
        {
            Destroy(vehicle);
            return;
        }
        try
        {
            pools[originalPrefab].Release(vehicle);
        }
        catch (System.InvalidOperationException)
        {
            // 重复回收时静默忽略，避免异常打断游戏
            Debug.LogWarning($"重复回收车辆 {vehicle.name}，已忽略");
        }
    }

    private void CreatePool(GameObject prefab)
    {
        pools[prefab] = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(prefab),
            actionOnGet: obj => obj.SetActive(true),
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }
}