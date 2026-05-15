using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadLane : MonoBehaviour
{
    [HideInInspector] // 在 Inspector 隐藏，但代码可访问
    public Vector3 direction = Vector3.forward; // 默认方向
}
