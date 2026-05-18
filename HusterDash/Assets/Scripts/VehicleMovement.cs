using UnityEngine;

public class VehicleMovement : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 5f;
    private float currentSpeed;
    private Vector3 moveDirection;
    private GameObject originalPrefab;
    private Transform player;
    private float roadHalfWidth;
    private float despawnZOffset = 15f;
    private bool isReturned = false;
    private BlockVehicleSpawner ownerSpawner;

    public void Initialize(Vector3 direction, GameObject prefab, Transform playerTransform, float halfWidth, BlockVehicleSpawner spawner)
    {
        moveDirection = direction.normalized;
        originalPrefab = prefab;
        player = playerTransform;
        roadHalfWidth = halfWidth;
        ownerSpawner = spawner;
        currentSpeed = baseSpeed;
        isReturned = false;

        if (moveDirection == Vector3.zero)
        {
            Debug.LogError($"Vehicle {name} 初始化方向为零向量！");
            moveDirection = Vector3.right;
        }

        // ========== 车辆朝向设置（使用 Rotate 方法保证顺序和符号） ==========
        // 模型原始姿态假设：平躺（例如前向为 +Z，上向为 +Y，需要先绕 X 轴 -90° 使其站立）
        // 1. 重置旋转为单位旋转
        transform.rotation = Quaternion.identity;

        // 2. 先执行公共旋转：绕局部 X 轴旋转 -90°，让模型从“平躺”变为“站立”
        transform.Rotate(-90, 0, 0, Space.Self);

        // 3. 根据移动方向决定是否需要额外旋转
        if (moveDirection.x > 0)  // 正向车（向右行驶）
        {
            // 再绕局部 Z 轴旋转 -180°，使车头指向右方（+X 方向）
            // 注意：旋转顺序是先 X 后 Z，所以这里是在已经站立的基础上绕 Z 轴旋转
            transform.Rotate(0, 0, -180, Space.Self);
        }
        else  // 负向车（向左行驶，moveDirection.x < 0）
        {
            // 无需额外旋转，站立后的默认朝向即为左方（-X 方向）
            // 如果默认朝向不是左方，可以微调，但一般绕 X -90° 后，模型原来的前向会变成上向，
            // 具体朝向取决于原始模型。如果发现负向车朝向不对，可以增加 transform.Rotate(0, 0, 0) 或调整。
        }
        // ================================================================

        Debug.Log($"[Vehicle] {name} Initialize: direction={moveDirection}, speed={currentSpeed}, finalRotation={transform.rotation.eulerAngles}, ownerSpawner={ownerSpawner?.name}");
    }

    public void SetSpeed(float speed)
    {
        currentSpeed = speed;
    }

    private void Update()
    {
        if (isReturned) return;

        transform.Translate(moveDirection * currentSpeed * Time.deltaTime, Space.World);

        // 每隔 60 帧打印一次负向车辆的位置（调试用）
        if (moveDirection.x < 0 && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Vehicle] 负向车辆 {name} 位置: {transform.position.x}, 速度={currentSpeed}, 距左边界={(transform.position.x + roadHalfWidth)}");
        }

        bool outOfXBounds = false;
        if (moveDirection.x > 0)
            outOfXBounds = transform.position.x > roadHalfWidth;
        else if (moveDirection.x < 0)
            outOfXBounds = transform.position.x < -roadHalfWidth;

        bool behindPlayer = player != null && transform.position.z < player.position.z - despawnZOffset;

        if (outOfXBounds || behindPlayer)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (isReturned) return;

        Debug.Log($"[Vehicle] {name} 准备回收，原因：{(Mathf.Abs(transform.position.x) > roadHalfWidth ? "超出X边界" : "落后玩家")}，当前X={transform.position.x}, roadHalfWidth={roadHalfWidth}");

        isReturned = true;

        if (ownerSpawner != null)
            ownerSpawner.RemoveVehicle(gameObject);

        if (VehiclePool.Instance != null && originalPrefab != null)
            VehiclePool.Instance.ReturnVehicle(gameObject, originalPrefab);
        else
            Destroy(gameObject);
    }
}