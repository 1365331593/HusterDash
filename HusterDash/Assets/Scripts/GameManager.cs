using UnityEngine;
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
        // 注册按钮事件
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (menuButton != null)
            menuButton.onClick.AddListener(BackToMenu);

        // 确保开始时失败界面隐藏
        if (failPanel != null)
            failPanel.SetActive(false);
    }

    /// <summary>
    /// 游戏失败时调用
    /// </summary>
    public void GameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        // 暂停游戏时间（可选）
        Time.timeScale = 0f;

        // 显示失败界面
        if (failPanel != null)
            failPanel.SetActive(true);

        // 可以禁用玩家输入（通过 PlayerMove 的 enabled 属性）
        PlayerMove playerMove = FindObjectOfType<PlayerMove>();
        if (playerMove != null)
            playerMove.enabled = false;
    }

    /// <summary>
    /// 重新开始：重载当前场景
    /// </summary>
    private void RestartGame()
    {
        // 恢复时间（避免重载后仍为0）
        Time.timeScale = 1f;
        // 重新加载当前场景
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// 返回主菜单（暂未实现）
    /// </summary>
    private void BackToMenu()
    {
        // 这里可以加载主菜单场景，如果没有主菜单则提示
        Debug.Log("尚未实现主菜单功能");
        // 如果希望返回一个简易菜单，可以自行扩展
        // 简单起见，可恢复时间并隐藏面板，但通常返回主菜单会切换场景
    }
}