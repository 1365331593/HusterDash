using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 文件名: QuitGameButton.cs
/// 作用: 退出游戏按钮组件，挂载到退出按钮上，点击后退出应用程序
/// 主要功能:
///    1. 自动查找同物体 Button 组件并注册退出事件
///    2. Editor 中点击停止播放模式，打包后点击退出应用程序
///    3. OnDestroy 时自动注销事件，防止内存泄漏
/// </summary>
public class QuitGameButton : MonoBehaviour
{
    [Header("按钮引用")]
    [Tooltip("目标按钮组件，留空则自动查找同物体上的 Button 组件")]
    public Button targetButton;

    private void Start()
    {
        // 优先使用手动赋值的 targetButton，否则从同物体查找
        Button btn = targetButton;
        if (btn == null)
            btn = GetComponent<Button>();

        if (btn != null)
            btn.onClick.AddListener(OnQuitClicked);
        else
            Debug.LogWarning($"[QuitGameButton] GameObject \"{gameObject.name}\" 上未找到 Button 组件，" +
                             "请手动拖入 targetButton 字段或将脚本挂载在带 Button 组件的物体上。");
    }

    /// <summary>
    /// 作用: 退出按钮点击回调，立即退出应用程序
    /// </summary>
    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        Button btn = targetButton;
        if (btn == null)
            btn = GetComponent<Button>();

        if (btn != null)
            btn.onClick.RemoveListener(OnQuitClicked);
    }
}
