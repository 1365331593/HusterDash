using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("失败界面")]
    public GameObject failPanel;          // 失败界面根物体
    public Button restartButton;          // 重新开始按钮
    public Button menuButton;             // 返回主菜单按钮（暂未实现）

    [Header("成绩显示")]
    [Tooltip("用于显示本次成绩/历史最佳的 TextMeshPro 组件")]
    public TMP_Text recordText;           // 拖入失败界面中的文本（可以是原来的最佳文本）

    [Tooltip("新纪录时显示的文本模板，{0} 会被替换为里程数值")]
    public string newRecordFormat = "新纪录！\n{0:F2} m";

    [Tooltip("非新纪录时显示的文本模板，{0} 会被替换为历史最佳数值")]
    public string bestRecordFormat = "历史最佳：{0:F2} m";

    [Header("光标控制")]
    [Tooltip("Alt 键的 Input Action 引用 (Player/CursorUnlock)，按住时临时呼出光标。")]
    public InputActionReference cursorUnlockAction;

    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // 锁定并隐藏光标（游戏过程中不需要鼠标光标）
        LockCursor();

        // 注册 Alt 键按住/释放事件，用于临时呼出光标
        if (cursorUnlockAction != null)
        {
            cursorUnlockAction.action.performed += OnAltPressed;
            cursorUnlockAction.action.canceled += OnAltReleased;
            cursorUnlockAction.action.Enable();
        }

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (menuButton != null)
            menuButton.onClick.AddListener(BackToMenu);

        if (failPanel != null)
            failPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (cursorUnlockAction != null)
        {
            cursorUnlockAction.action.performed -= OnAltPressed;
            cursorUnlockAction.action.canceled -= OnAltReleased;
        }
    }

    /// <summary>
    /// 游戏失败时调用
    /// </summary>
    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        Time.timeScale = 0f;

        // 获取本次里程和历史最佳
        DistanceTracker tracker = FindObjectOfType<DistanceTracker>();
        float currentDist = 0f;
        float bestDist = 0f;
        if (tracker != null)
        {
            currentDist = tracker.GetCurrentDistance();
            bestDist = tracker.GetBestDistance();
        }

        // 判断是否为新纪录（当前里程 >= 历史最佳，且大于 0 避免空记录）
        bool isNewRecord = (currentDist >= bestDist && currentDist > 0.01f);

        // 更新失败界面的文本
        if (recordText != null)
        {
            if (isNewRecord)
                recordText.text = string.Format(newRecordFormat, currentDist);
            else
                recordText.text = string.Format(bestRecordFormat, bestDist);
        }

        if (failPanel != null)
            failPanel.SetActive(true);

        // 解锁并显示光标，方便玩家点击失败界面的按钮
        UnlockCursor();

        PlayerMove playerMove = FindObjectOfType<PlayerMove>();
        if (playerMove != null)
            playerMove.enabled = false;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void BackToMenu()
    {
        Debug.Log("尚未实现主菜单功能");
    }

    // ----- Alt 键临时呼出光标 -----

    /// <summary>
    /// 按住 Alt 键时：临时解锁光标。
    /// </summary>
    private void OnAltPressed(InputAction.CallbackContext ctx)
    {
        if (!isGameOver)
            UnlockCursor();
    }

    /// <summary>
    /// 释放 Alt 键时：重新锁定光标。
    /// </summary>
    private void OnAltReleased(InputAction.CallbackContext ctx)
    {
        if (!isGameOver)
            LockCursor();
    }

    // ----- 光标控制 -----

    /// <summary>
    /// 锁定并隐藏鼠标光标（游戏中调用）。
    /// </summary>
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// 解锁并显示鼠标光标（失败界面或 Alt 键中调用）。
    /// </summary>
    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}