using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 文件名: MusicVolumePanel.cs
/// 作用: 音量设置面板 UI 逻辑，提供音乐/音效音量滑条和特殊失败音乐开关
/// 主要功能:
///    1. 点击音量按钮打开/关闭设置面板
///    2. 音乐音量滑条（0~1），拖动时实时生效并持久化
///    3. 音效音量滑条（0~1），拖动时实时生效并持久化
///    4. 特殊失败音乐勾选框，勾选后游戏失败时播放第三首音乐
///    5. 按钮交互时自动播放点击音效
///    6. 启动时从 PlayerPrefs 加载设置并同步到 UI
/// </summary>
public class MusicVolumePanel : MonoBehaviour
{
    [Header("按钮")]
    [Tooltip("音量调节按钮图标（GameObject）")]
    public GameObject volumeButton;

    [Tooltip("按钮的 Button 组件（用于监听点击事件）")]
    public Button volumeButtonComponent;

    [Header("面板")]
    [Tooltip("音量设置面板根节点（GameObject），初始隐藏")]
    public GameObject volumePanel;

    [Tooltip("关闭面板按钮")]
    public Button closeButton;

    [Header("音乐音量")]
    [Tooltip("音乐音量滑条（范围 0~1）")]
    public Slider musicVolumeSlider;

    [Tooltip("音乐音量标签文字（如 \"音乐音量\"），可空")]
    public TMP_Text musicVolumeLabel;

    [Header("音效音量")]
    [Tooltip("音效音量滑条（范围 0~1）")]
    public Slider sfxVolumeSlider;

    [Tooltip("音效音量标签文字（如 \"音效音量\"），可空")]
    public TMP_Text sfxVolumeLabel;

    [Header("特殊失败音乐")]
    [Tooltip("特殊失败音乐勾选框（Toggle）")]
    public Toggle specialMusicToggle;

    [Tooltip("特殊失败音乐标签文字，可空")]
    public TMP_Text specialMusicLabel;

    [Header("滑条范围")]
    [Tooltip("滑条最小值（默认 0）")]
    public float sliderMinValue = 0f;

    [Tooltip("滑条最大值（默认 1）")]
    public float sliderMaxValue = 1f;

    /// <summary>
    /// 面板当前是否打开
    /// </summary>
    private bool isPanelOpen = false;

    /// <summary>
    /// 音量按钮是否允许显示（外部控制，如暂停时隐藏）
    /// </summary>
    private bool allowVolumeButton = true;

    private void Start()
    {
        // 初始化滑条范围
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = sliderMinValue;
            musicVolumeSlider.maxValue = sliderMaxValue;
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = sliderMinValue;
            sfxVolumeSlider.maxValue = sliderMaxValue;
        }

        // 从 MusicManager 加载当前设置到 UI
        LoadSettingsToUI();

        // 注册按钮点击事件
        if (volumeButtonComponent != null)
            volumeButtonComponent.onClick.AddListener(OnVolumeButtonClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseButtonClicked);

        // 注册滑条值变化事件
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // 注册勾选框值变化事件
        if (specialMusicToggle != null)
            specialMusicToggle.onValueChanged.AddListener(OnSpecialMusicToggled);

        // 初始隐藏面板
        if (volumePanel != null)
            volumePanel.SetActive(false);

        // 初始显示按钮
        if (volumeButton != null)
            volumeButton.SetActive(allowVolumeButton);
    }

    private void OnDestroy()
    {
        // 注销事件，防止内存泄漏
        if (volumeButtonComponent != null)
            volumeButtonComponent.onClick.RemoveListener(OnVolumeButtonClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        if (specialMusicToggle != null)
            specialMusicToggle.onValueChanged.RemoveListener(OnSpecialMusicToggled);
    }

    #region 公开方法

    /// <summary>
    /// 作用: 外部控制音量按钮的显隐（如 PauseMenuManager 暂停时隐藏）
    /// </summary>
    /// <param name="visible">是否显示按钮</param>
    public void SetVolumeButtonVisible(bool visible)
    {
        allowVolumeButton = visible;
        if (volumeButton != null)
            volumeButton.SetActive(visible);
        // 按钮隐藏时自动关闭面板
        if (!visible && isPanelOpen)
            ClosePanel();
    }

    /// <summary>
    /// 作用: 外部直接调用以打开面板（如暂停面板中的入口按钮）
    /// </summary>
    public void OpenPanel()
    {
        PlayButtonSound();
        isPanelOpen = true;
        if (volumePanel != null)
            volumePanel.SetActive(true);
    }

    /// <summary>
    /// 作用: 外部直接调用以关闭面板
    /// </summary>
    public void ClosePanel()
    {
        isPanelOpen = false;
        if (volumePanel != null)
            volumePanel.SetActive(false);
    }

    /// <summary>
    /// 作用: 切换面板开关状态
    /// </summary>
    public void TogglePanel()
    {
        if (isPanelOpen)
            ClosePanel();
        else
            OpenPanel();
    }

    #endregion

    #region UI 事件回调

    /// <summary>
    /// 作用: 音量按钮点击 → 播放音效 → 切换面板
    /// </summary>
    private void OnVolumeButtonClicked()
    {
        PlayButtonSound();
        TogglePanel();
    }

    /// <summary>
    /// 作用: 关闭按钮点击 → 播放音效 → 关闭面板
    /// </summary>
    private void OnCloseButtonClicked()
    {
        PlayButtonSound();
        ClosePanel();
    }

    /// <summary>
    /// 作用: 音乐音量滑条拖动 → 实时更新音量并持久化
    /// </summary>
    private void OnMusicVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetMusicVolume(value);
    }

    /// <summary>
    /// 作用: 音效音量滑条拖动 → 实时更新音量并持久化
    /// </summary>
    private void OnSFXVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetSFXVolume(value);
    }

    /// <summary>
    /// 作用: 特殊失败音乐勾选框切换 → 持久化
    /// </summary>
    private void OnSpecialMusicToggled(bool value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetSpecialGameOverMusicEnabled(value);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 作用: 从 MusicManager 加载当前设置，同步到 UI 控件（不触发回调）
    /// </summary>
    private void LoadSettingsToUI()
    {
        if (MusicManager.Instance == null) return;

        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(MusicManager.Instance.GetMusicVolume());

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(MusicManager.Instance.GetSFXVolume());

        if (specialMusicToggle != null)
            specialMusicToggle.SetIsOnWithoutNotify(MusicManager.Instance.GetSpecialGameOverMusicEnabled());
    }

    /// <summary>
    /// 作用: 播放按钮点击音效
    /// </summary>
    private void PlayButtonSound()
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayButtonSfx();
    }

    #endregion
}
