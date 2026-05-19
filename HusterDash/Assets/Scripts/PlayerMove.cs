using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float speed = 3;

    Animator anim;

    Vector3 move;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        move = new Vector3(x, 0, z);

        transform.LookAt(transform.position + new Vector3(x, 0, z));
        transform.position += new Vector3(x, 0, z) * speed * Time.deltaTime;

        UpdateAnim();
    }

    void UpdateAnim()
    {
        anim.SetFloat("Speed", move.magnitude);
    }

    //碰撞检测
    private void OnTriggerEnter(Collider other)
    {
        // 检测是否碰到车辆（车辆所在的 Layer 为 "Vehicle"）
        if (other.gameObject.layer == LayerMask.NameToLayer("Vehicle"))
        {
            // 触发游戏失败
            if (GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
    }
}
