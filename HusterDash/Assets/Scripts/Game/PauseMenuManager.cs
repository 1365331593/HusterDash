using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 文件名: PauseMenuManager.cs
/// 作用: 管理 Game 场景的暂停菜单，响应 Esc 键和左上角暂停按钮
/// 主要功能:
///    1. 监听 Esc 键（Player/Pause Action）切换暂停/恢复
///    2. 暂停时冻结游戏（Time.timeScale = 0），显示暂停面板并解锁光标
///    3. 恢复时解冻游戏，隐藏暂停面板并锁定光标
///    4. 提供"继续游戏""回主菜单""清空历史成绩"三个按钮
///    5. 清空历史成绩需要二次确认弹窗
///    6. 失败面板显示中或开场过场动画未结束时不允许暂停
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("左上角暂停按钮图标（GameObject）")]
    public GameObject pauseButton;

    [Tooltip("暂停面板根节点（GameObject）")]
    public GameObject pausePanel;

    [Tooltip("继续游戏按钮")]
    public Button continueButton;

    [Tooltip("回主菜单按钮")]
    public Button backToMenuButton;

    [Tooltip("清空历史成绩按钮")]
    public Button clearRecordButton;

    [Header("二次确认弹窗")]
    [Tooltip("确认弹窗根节点（GameObject），初始隐藏")]
    public GameObject confirmDialog;

    [Tooltip("确认清空按钮")]
    public Button confirmClearButton;

    [Tooltip("取消清空按钮")]
    public Button cancelClearButton;

    [Header("输入")]
    [Tooltip("Player/Pause Action 的 InputActionReference（绑定 Esc 键）")]
    public InputActionReference pauseAction;

    [Header("历史成绩存储")]
    [Tooltip("存储最佳里程的 PlayerPrefs 键名，需与 DistanceTracker.bestKey 一致")]
    public string bestRecordKey = "BestDistance";

    // 暂停状态
    private bool isPaused = false;

    // 缓存的 IntroCameraController 引用（过场结束后会 Destroy 自身）
    private IntroCameraController cachedIntroController;

    private void Start()
    {
        // 缓存开场运镜控制器引用
        cachedIntroController = FindObjectOfType<IntroCameraController>();

        // 注册 Pause Action 的 performed 回调（事件驱动，不受 Time.timeScale 影响）
        if (pauseAction != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }
        else
        {
            Debug.LogError("PauseMenuManager: 未设置 pauseAction 引用！请在 Inspector 中拖入 Player/Pause Action。");
        }

        // 注册按钮点击事件
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        if (clearRecordButton != null)
            clearRecordButton.onClick.AddListener(OnClearRecordClicked);
        if (confirmClearButton != null)
            confirmClearButton.onClick.AddListener(OnConfirmClearClicked);
        if (cancelClearButton != null)
            cancelClearButton.onClick.AddListener(OnCancelClearClicked);

        // 初始隐藏所有暂停相关 UI
        if (pauseButton != null)
            pauseButton.SetActive(true);   // 游戏正常时显示暂停按钮
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (confirmDialog != null)
            confirmDialog.SetActive(false);
    }

    private void OnDestroy()
    {
        // 注销 Pause Action 回调
        if (pauseAction != null)
            pauseAction.action.performed -= OnPausePerformed;
    }

    private void Update()
    {
        // 仅在非暂停状态下更新 IntroCameraController 引用
        // 过场结束时会 Destroy 自身，引用自动变 null
        if (!isPaused && cachedIntroController == null)
        {
            cachedIntroController = FindObjectOfType<IntroCameraController>();
        }

        // 根据是否允许暂停动态控制暂停按钮的显隐（仅在非暂停状态下管理）
        if (!isPaused && pauseButton != null)
        {
            bool shouldShow = CanPause();
            if (pauseButton.activeSelf != shouldShow)
                pauseButton.SetActive(shouldShow);
        }
    }

    /// <summary>
    /// 判断当前是否允许暂停
    /// </summary>
    private bool CanPause()
    {
        // 失败面板正在显示 → 不允许暂停，避免两个面板重叠
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            return false;

        // 开场过场动画未结束 → 不允许暂停
        if (cachedIntroController != null && cachedIntroController.enabled)
            return false;

        return true;
    }

    /// <summary>
    /// Esc 键回调：根据当前状态切换暂停/恢复
    /// 注意：此回调由 InputAction 事件驱动，不受 Time.timeScale = 0 影响
    /// </summary>
    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (isPaused)
                ResumeGame();
            else if (CanPause())
                PauseGame();
        }
    }

    /// <summary>
    /// 执行暂停：冻结游戏、显示面板、解锁光标
    /// </summary>
    public void PauseGame()
    {
        // 前置检查：不允许暂停的情况直接忽略
        if (!CanPause()) return;

        isPaused = true;

        // 冻结游戏逻辑
        Time.timeScale = 0f;

        // 通知 GameManager 进入暂停状态（阻止 Alt 光标操作）
        if (GameManager.Instance != null)
            GameManager.Instance.IsPaused = true;

        // 隐藏暂停按钮，显示暂停面板
        if (pauseButton != null)
            pauseButton.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(true);
        if (confirmDialog != null)
            confirmDialog.SetActive(false);

        // 解锁并显示光标，方便玩家操作暂停面板按钮
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 禁用玩家移动（防御性编程，Update 已因 timeScale=0 不执行）
        PlayerMove playerMove = FindObjectOfType<PlayerMove>();
        if (playerMove != null)
            playerMove.enabled = false;
    }

    /// <summary>
    /// 执行恢复：解冻游戏、隐藏面板、锁定光标
    /// </summary>
    private void ResumeGame()
    {
        // 如果确认弹窗正在显示，优先关闭弹窗而非恢复游戏
        if (confirmDialog != null && confirmDialog.activeSelf)
        {
            confirmDialog.SetActive(false);
            return;
        }

        isPaused = false;

        // 恢复游戏逻辑
        Time.timeScale = 1f;

        // 通知 GameManager 退出暂停状态
        if (GameManager.Instance != null)
            GameManager.Instance.IsPaused = false;

        // 显示暂停按钮，隐藏暂停面板和确认弹窗
        if (pauseButton != null)
            pauseButton.SetActive(true);
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (confirmDialog != null)
            confirmDialog.SetActive(false);

        // 锁定并隐藏光标，恢复游戏沉浸感
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 重新启用玩家移动
        PlayerMove playerMove = FindObjectOfType<PlayerMove>();
        if (playerMove != null)
            playerMove.enabled = true;
    }

    // ----- 按钮点击回调 -----

    /// <summary>
    /// "继续游戏"按钮点击：恢复游戏
    /// </summary>
    private void OnContinueClicked()
    {
        ResumeGame();
    }

    /// <summary>
    /// "回主菜单"按钮点击：恢复时间缩放后带淡入淡出过渡切换到 MainMenu 场景
    /// </summary>
    private void OnBackToMenuClicked()
    {
        // 恢复时间缩放，避免 MainMenu 场景加载后保持冻结
        Time.timeScale = 1f;

        // 清除暂停标记（过渡后本对象会随场景销毁）
        if (GameManager.Instance != null)
            GameManager.Instance.IsPaused = false;

        SceneTransitionManager.LaunchTransition("MainMenu");
    }

    /// <summary>
    /// "清空历史成绩"按钮点击：显示二次确认弹窗
    /// </summary>
    private void OnClearRecordClicked()
    {
        if (confirmDialog != null)
            confirmDialog.SetActive(true);
    }

    /// <summary>
    /// 确认清空按钮点击：删除 PlayerPrefs 中的最佳记录、重置 DistanceTracker 缓存，并关闭弹窗
    /// </summary>
    private void OnConfirmClearClicked()
    {
        // 删除 PlayerPrefs 中的持久化记录
        PlayerPrefs.DeleteKey(bestRecordKey);
        PlayerPrefs.Save();

        // 同步重置 DistanceTracker 的内存缓存，确保 GameOver 面板立即反映清空状态
        DistanceTracker tracker = FindObjectOfType<DistanceTracker>();
        if (tracker != null)
            tracker.ResetBestDistance();

        Debug.Log($"PauseMenuManager: 已清空历史最佳成绩（键名: {bestRecordKey}）。");

        // 关闭确认弹窗
        if (confirmDialog != null)
            confirmDialog.SetActive(false);
    }

    /// <summary>
    /// 取消清空按钮点击：关闭确认弹窗
    /// </summary>
    private void OnCancelClearClicked()
    {
        if (confirmDialog != null)
            confirmDialog.SetActive(false);
    }
}
