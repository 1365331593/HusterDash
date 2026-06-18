using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 文件名: ButtonClickSound.cs
/// 作用: 挂载到含有 Button 组件的 GameObject 上，自动为按钮点击添加音效
/// 主要功能:
///    1. 自动注册到同物体 Button.onClick 事件，播放 dianji.mp3 音效
///    2. 也可通过独立方法 PlayClickSound() 供其他入口手动调用
///    3. OnDestroy 时自动注销事件，防止内存泄漏
/// </summary>
public class ButtonClickSound : MonoBehaviour
{
    [Header("自动注册")]
    [Tooltip("勾选后，Start() 时自动将 PlayClickSound 注册到同物体 Button 的 onClick 事件末尾")]
    public bool autoRegister = true;

    [Tooltip("若 autoRegister 为 true 但 Button 不在本物体上，可手动赋值")]
    public Button targetButton;

    private void Start()
    {
        if (!autoRegister) return;

        // 优先使用手动赋值的 targetButton，否则从同物体查找
        Button btn = targetButton;
        if (btn == null)
            btn = GetComponent<Button>();

        if (btn != null)
            btn.onClick.AddListener(PlayClickSound);
        else
            Debug.LogWarning($"[ButtonClickSound] GameObject \"{gameObject.name}\" 上未找到 Button 组件，" +
                             $"autoRegister 已启用但无法自动注册。请手动拖入 targetButton 字段。");
    }

    /// <summary>
    /// 作用: 播放按钮点击音效（供 Button.onClick 事件绑定）
    /// </summary>
    public void PlayClickSound()
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayButtonSfx();
    }

    private void OnDestroy()
    {
        Button btn = targetButton;
        if (btn == null)
            btn = GetComponent<Button>();

        if (btn != null)
            btn.onClick.RemoveListener(PlayClickSound);
    }
}
