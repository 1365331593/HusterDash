using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedTopDownCamera : MonoBehaviour
{
    [Header("要跟随的目标")]
    public Transform target;

    [Header("相机的固定偏移 (世界坐标系)")]
    // Y代表高度，X/Z代表前后的偏移
    // 例子1：(0, 10, -5) -> 斜上方45度俯视
    // 例子2：(0, 15, 0)  -> 正上方垂直90度俯视
    public Vector3 offset = new Vector3(0, 10, -5);

    [Header("相机的固定旋转角度")]
    // 俯视角通常设置为 (45, 0, 0) 或 (90, 0, 0)
    // 45度看起来比较立体，90度是完全平面
    public Vector3 fixedRotation = new Vector3(45, 0, 0);

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 直接计算相机位置：玩家位置 + 固定的偏移量
        // 这样相机永远不会跟着玩家转向
        transform.position = target.position + offset;

        // 2. 强制设置相机的旋转角度（不要LookAt！）
        // 这样视角绝对固定，玩家只能在画面内移动，相机不会转
        transform.rotation = Quaternion.Euler(fixedRotation);
    }
}