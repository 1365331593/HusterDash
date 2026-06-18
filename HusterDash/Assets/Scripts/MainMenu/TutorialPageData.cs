using System;
using UnityEngine;

/// <summary>
/// 文件名: TutorialPageData.cs
/// 作用: 定义新手教程单页的数据结构
/// 主要功能:
///    1. 存储每页的标题和内容文本
///    2. 标记为 Serializable 便于在 Inspector 中直接编辑
/// </summary>
[Serializable]
public class TutorialPageData
{
    [Tooltip("本页标题（如\"基本移动\"）")]
    public string title;

    [Tooltip("本页内容（支持换行，直接输入文本）")]
    [TextArea(5, 20)]
    public string content;
}
