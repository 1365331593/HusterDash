using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("移动速度")]
    [Tooltip("玩家移动速度（米/秒）")]
    public float speed = 3f;

    [Header("动画混合参数")]
    [Tooltip("Animator 中控制动画混合（Idle/Walk/Run）的参数名，默认 Speed")]
    public string blendParam = "Speed";

    [Header("动画播放速度参数")]
    [Tooltip("Animator 中控制动画剪辑播放速度的参数名（需与状态机中绑定的 Float 参数一致）")]
    public string animSpeedParam = "AnimSpeed";

    [Tooltip("游戏中行走时的动画播放倍数（例如 2 表示二倍速）")]
    public float gameAnimSpeed = 2f;

    private Animator anim;
    private Vector3 move;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
            Debug.LogError("PlayerMove: 未找到 Animator 组件！");
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        move = new Vector3(x, 0, z);

        // 面向移动方向
        if (move.magnitude > 0.01f)
            transform.LookAt(transform.position + move);

        // 移动
        transform.position += move * speed * Time.deltaTime;

        UpdateAnim();
    }

    void UpdateAnim()
    {
        if (anim == null) return;

        // 1. 原有动画混合参数（控制 Idle/Walk/Run 过渡），保持不变
        float blendValue = move.magnitude;          // 范围 0~1
        anim.SetFloat(blendParam, blendValue);

        // 2. 新增动画播放速度参数（控制动画片段的播放速度）
        float speedValue = (move.magnitude > 0.01f) ? gameAnimSpeed : 0f;
        anim.SetFloat(animSpeedParam, speedValue);
    }

    // 碰撞检测（原有功能，完全保留）
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Vehicle"))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
    }
}