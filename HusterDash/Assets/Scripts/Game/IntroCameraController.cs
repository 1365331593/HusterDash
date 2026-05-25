using UnityEngine;
using System.Collections;

/// <summary>
/// 开场运镜：玩家从起点后方步入画面，摄像机从右侧面平滑旋转至固定俯视视角。
/// 支持分离的动画速度与移动速度，过场动画可按一倍速播放，同时保持实际移动速度为 1 米/秒。
/// </summary>
public class IntroCameraController : MonoBehaviour
{
    [Header("玩家设置")]
    [Tooltip("玩家角色 Transform")]
    public Transform player;

    [Tooltip("玩家的 Animator 组件")]
    public Animator playerAnimator;

    [Header("移动与动画参数")]
    [Tooltip("实际移动速度（米/秒），过场中建议 1.0")]
    public float walkSpeed = 1f;

    [Tooltip("Animator 中控制动画混合的参数名（需与 PlayerMove 中的 blendParam 一致）")]
    public string blendParam = "Speed";

    [Tooltip("Animator 中控制动画播放速度的参数名（需与 PlayerMove 中的 animSpeedParam 一致）")]
    public string animSpeedParam = "AnimSpeed";

    [Tooltip("过场动画中使用的动画播放倍数（1 表示一倍速，与慢速行走匹配）")]
    public float introAnimSpeed = 1f;

    [Header("摄像机初始设置（右侧面）")]
    [Tooltip("摄像机起始位置（世界坐标），高度建议与玩家眼睛齐平")]
    public Vector3 startCameraPosition = new Vector3(5f, 1.6f, 0f);

    [Header("摄像机最终设置（与 FixedTopDownCamera 一致）")]
    [Tooltip("最终相对于玩家的偏移，必须与 FixedTopDownCamera 中的 offset 相同")]
    public Vector3 finalOffset = new Vector3(0f, 8f, -4f);

    [Tooltip("最终固定旋转角度，必须与 FixedTopDownCamera 中的 fixedRotation 相同")]
    public Vector3 finalRotation = new Vector3(60f, 0f, 0f);

    [Header("运镜时长")]
    [Tooltip("摄像机从初始位置旋转到最终位置的时间（秒）")]
    public float cameraRotateDuration = 2.5f;

    // 内部状态
    private Vector3 playerStartPos = new Vector3(0f, 0f, -1.5f);
    private Vector3 playerEndPos = Vector3.zero;
    private float walkDistance;
    private float walkDuration;
    private Camera mainCamera;
    private FixedTopDownCamera fixedCam;
    private PlayerMove playerMove;
    private DistanceTracker distanceTracker;

    private enum State { PlayerWalking, CameraRotating, Finished }
    private State currentState = State.PlayerWalking;
    private float walkTimer = 0f;
    private float rotateTimer = 0f;

    private Vector3 startArcPos;
    private Quaternion startArcRot;
    private Vector3 endArcPos;
    private Quaternion endArcRot;

    private void Start()
    {
        walkDistance = Vector3.Distance(playerStartPos, playerEndPos);
        walkDuration = walkDistance / walkSpeed;

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("未找到主相机！");
            enabled = false;
            return;
        }

        fixedCam = mainCamera.GetComponent<FixedTopDownCamera>();
        playerMove = player.GetComponent<PlayerMove>();
        distanceTracker = FindObjectOfType<DistanceTracker>();

        // 禁用玩家控制和原有相机跟随
        if (playerMove != null) playerMove.enabled = false;
        if (fixedCam != null) fixedCam.enabled = false;
        if (distanceTracker != null) distanceTracker.enabled = false;

        // 设置玩家起始位置
        player.position = playerStartPos;

        // 设置动画：过场中，强制进入行走状态（blendParam = 1），且动画速度为一倍速
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat(blendParam, 1f);           // 行走状态混合值
            playerAnimator.SetFloat(animSpeedParam, introAnimSpeed); // 动画速度一倍速
        }

        // 摄像机起始状态
        mainCamera.transform.position = startCameraPosition;
        Vector3 lookTarget = new Vector3(0f, 1f, 0f);
        mainCamera.transform.LookAt(lookTarget);
        startArcPos = startCameraPosition;
        startArcRot = mainCamera.transform.rotation;

        endArcPos = finalOffset;
        endArcRot = Quaternion.Euler(finalRotation);
    }

    private void Update()
    {
        switch (currentState)
        {
            case State.PlayerWalking:
                WalkPlayer();
                break;
            case State.CameraRotating:
                RotateCamera();
                break;
            case State.Finished:
                break;
        }
    }

    private void WalkPlayer()
    {
        walkTimer += Time.deltaTime;
        float t = Mathf.Clamp01(walkTimer / walkDuration);
        player.position = Vector3.Lerp(playerStartPos, playerEndPos, t);

        if (t >= 1f)
        {
            currentState = State.CameraRotating;
            rotateTimer = 0f;

            // 行走结束，停止动画（混合值归零，动画速度归零）
            if (playerAnimator != null)
            {
                playerAnimator.SetFloat(blendParam, 0f);
                playerAnimator.SetFloat(animSpeedParam, 0f);
            }
        }
    }

    private void RotateCamera()
    {
        rotateTimer += Time.deltaTime;
        float t = Mathf.Clamp01(rotateTimer / cameraRotateDuration);
        mainCamera.transform.position = Vector3.Lerp(startArcPos, endArcPos, t);
        mainCamera.transform.rotation = Quaternion.Slerp(startArcRot, endArcRot, t);

        if (t >= 1f)
            FinishIntro();
    }

    private void FinishIntro()
    {
        currentState = State.Finished;
        mainCamera.transform.position = endArcPos;
        mainCamera.transform.rotation = endArcRot;

        // 恢复玩家移动脚本（PlayerMove 会重新接管动画参数，自动将混合值和动画速度设为游戏中的值）
        if (playerMove != null) playerMove.enabled = true;

        // 恢复固定俯视相机
        if (fixedCam != null)
        {
            fixedCam.enabled = true;
            fixedCam.offset = finalOffset;
            fixedCam.fixedRotation = finalRotation;
        }

        // 恢复里程记录
        if (distanceTracker != null) distanceTracker.enabled = true;

        Destroy(this);
    }
}