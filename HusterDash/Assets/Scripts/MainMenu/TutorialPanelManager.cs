using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Cinemachine;

/// <summary>
/// 文件名: TutorialPanelManager.cs
/// 作用: 新手教学面板核心控制器，管理翻页、Esc 跳过和玩家输入冻结
/// 主要功能:
///    1. 显示/隐藏教学面板（全屏遮罩 + 分页内容）
///    2. 上一页/下一页翻页，最后一页显示"关闭"按钮
///    3. Esc 键跳过教程
///    4. 显示时冻结玩家移动和相机控制，解锁光标方便点击按钮
///    5. 提供静态 IsShowing 属性供外部查询
/// </summary>
public class TutorialPanelManager : MonoBehaviour
{
    [Header("教程页面内容")]
    [Tooltip("教程各页数据（标题 + 内容文本），可在 Inspector 中直接编辑")]
    public TutorialPageData[] pages;

    [Header("UI 组件引用")]
    [Tooltip("面板根节点（Canvas），Show/Hide 通过 SetActive 控制")]
    public GameObject panelRoot;

    [Tooltip("页码指示器（TMP_Text），格式如\"第 1/6 页\"")]
    public TMP_Text pageIndicator;

    [Tooltip("页面标题文本（TMP_Text）")]
    public TMP_Text titleText;

    [Tooltip("页面内容文本（TMP_Text）")]
    public TMP_Text contentText;

    [Tooltip("跳过提示文本（TMP_Text），内容如\"按 Esc 跳过教程\"")]
    public TMP_Text skipHint;

    [Header("翻页按钮")]
    [Tooltip("上一页按钮")]
    public Button prevButton;

    [Tooltip("下一页/关闭按钮")]
    public Button nextButton;

    [Tooltip("上一页按钮上的文字（TMP_Text）")]
    public TMP_Text prevButtonLabel;

    [Tooltip("下一页按钮上的文字（TMP_Text）")]
    public TMP_Text nextButtonLabel;

    [Header("默认教程内容")]
    [Tooltip("是否使用下方硬编码的默认教程内容（仅当 pages 数组为空时生效）")]
    [SerializeField] private bool useDefaultPagesIfEmpty = true;

    // 当前页码（0 起始）
    private int currentPage = 0;

    // 冻结前保存的玩家状态
    private PlayerMovement savedPlayerMovement;
    private PlayerInput savedPlayerInput;
    private CinemachineFreeLook savedFreeLookCamera;
    private float savedXSpeed;
    private float savedYSpeed;

    // 静态标记
    private static TutorialPanelManager instance;
    public static bool IsShowing => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

    /// <summary>
    /// 作用: 初始化按钮事件、查找玩家组件、隐藏面板
    /// </summary>
    void Awake()
    {
        instance = this;

        // 初始时隐藏面板
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // 绑定按钮事件
        if (prevButton != null)
            prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);
    }

    /// <summary>
    /// 作用: 每帧检测 Esc 键跳过教程
    /// </summary>
    void Update()
    {
        if (!IsShowing) return;

        // 检测 Esc 键关闭教学面板（使用新版 Input System）
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Hide();
        }
    }

    /// <summary>
    /// 作用: 清理按钮监听
    /// </summary>
    void OnDestroy()
    {
        if (prevButton != null)
            prevButton.onClick.RemoveListener(PrevPage);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextPage);

        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// 作用: 显示教程面板，冻结玩家输入，解锁光标
    /// </summary>
    public void Show()
    {
        if (panelRoot == null) return;

        // 如果未配置页面数据，使用默认内容填充
        if ((pages == null || pages.Length == 0) && useDefaultPagesIfEmpty)
            pages = GetDefaultPages();

        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("TutorialPanelManager: 没有教程页面数据！请在 Inspector 中配置 pages 数组或开启 useDefaultPagesIfEmpty。");
            return;
        }

        // 冻结玩家输入
        FreezePlayer();

        // 显示面板
        panelRoot.SetActive(true);
        currentPage = 0;
        UpdatePageUI();

        Debug.Log($"TutorialPanelManager: 教程面板已打开，共 {pages.Length} 页");
    }

    /// <summary>
    /// 作用: 隐藏教程面板，恢复玩家输入，重新锁定光标
    /// </summary>
    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // 恢复玩家输入
        UnfreezePlayer();

        Debug.Log("TutorialPanelManager: 教程面板已关闭");
    }

    /// <summary>
    /// 作用: 翻到上一页
    /// </summary>
    public void PrevPage()
    {
        if (currentPage <= 0) return;
        currentPage--;
        UpdatePageUI();
    }

    /// <summary>
    /// 作用: 翻到下一页（最后一页时变为关闭）
    /// </summary>
    public void NextPage()
    {
        if (currentPage >= pages.Length - 1)
        {
            // 最后一页，关闭面板
            Hide();
            return;
        }
        currentPage++;
        UpdatePageUI();
    }

    /// <summary>
    /// 作用: 刷新当前页的标题、内容、页码指示器及按钮状态
    /// </summary>
    private void UpdatePageUI()
    {
        if (pages == null || currentPage >= pages.Length) return;

        TutorialPageData page = pages[currentPage];

        // 更新标题
        if (titleText != null)
            titleText.text = page.title;

        // 更新内容
        if (contentText != null)
            contentText.text = page.content;

        // 更新页码指示器
        if (pageIndicator != null)
            pageIndicator.text = $"第 {currentPage + 1}/{pages.Length} 页";

        // 第一页时禁用"上一页"按钮
        if (prevButton != null)
            prevButton.interactable = currentPage > 0;

        if (prevButtonLabel != null)
            prevButtonLabel.text = "← 上一页";

        // 最后一页时"下一页"变为"关闭"
        bool isLastPage = currentPage >= pages.Length - 1;
        if (nextButtonLabel != null)
            nextButtonLabel.text = isLastPage ? "关闭" : "下一页 →";
    }

    /// <summary>
    /// 作用: 冻结玩家输入（禁用 PlayerInput、PlayerMovement，冻结相机旋转）
    /// </summary>
    private void FreezePlayer()
    {
        // 查找玩家
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            savedPlayerInput = player.GetComponent<PlayerInput>();
            savedPlayerMovement = player.GetComponent<PlayerMovement>();

            if (savedPlayerInput != null)
                savedPlayerInput.enabled = false;

            if (savedPlayerMovement != null)
                savedPlayerMovement.enabled = false;
        }

        // 冻结第三方相机旋转
        ThirdPersonCameraController camCtrl = FindObjectOfType<ThirdPersonCameraController>();
        if (camCtrl != null)
        {
            savedFreeLookCamera = camCtrl.freeLookCamera;
            if (savedFreeLookCamera != null)
            {
                savedXSpeed = savedFreeLookCamera.m_XAxis.m_MaxSpeed;
                savedYSpeed = savedFreeLookCamera.m_YAxis.m_MaxSpeed;
                savedFreeLookCamera.m_XAxis.m_MaxSpeed = 0f;
                savedFreeLookCamera.m_YAxis.m_MaxSpeed = 0f;
            }
        }

        // 解锁光标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 作用: 恢复玩家输入
    /// </summary>
    private void UnfreezePlayer()
    {
        if (savedPlayerInput != null)
            savedPlayerInput.enabled = true;

        if (savedPlayerMovement != null)
            savedPlayerMovement.enabled = true;

        // 恢复相机灵敏度
        if (savedFreeLookCamera != null)
        {
            savedFreeLookCamera.m_XAxis.m_MaxSpeed = savedXSpeed;
            savedFreeLookCamera.m_YAxis.m_MaxSpeed = savedYSpeed;
        }

        // 重新锁定光标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 清理引用
        savedPlayerMovement = null;
        savedPlayerInput = null;
        savedFreeLookCamera = null;
    }

    /// <summary>
    /// 作用: 生成默认的 6 页教程内容（当 Inspector 中未配置 pages 时使用）
    /// </summary>
    private TutorialPageData[] GetDefaultPages()
    {
        return new TutorialPageData[]
        {
            new TutorialPageData
            {
                title = "基本移动",
                content = "使用 W、A、S、D 键控制角色移动。\n\n" +
                          "在主菜单场景中，你可以自由地在 3D 环境中行走，探索地图。\n\n" +
                          "在游戏场景中，角色将沿道路向前奔跑，你需要躲避障碍。"
            },
            new TutorialPageData
            {
                title = "视角控制",
                content = "移动鼠标可以旋转视角，观察周围环境。\n\n" +
                          "滚动鼠标滚轮可以拉近或拉远相机距离（2.5m ~ 8m）。\n\n" +
                          "按住左 Alt 键可以临时显示鼠标光标，方便点击界面元素。释放后自动恢复为视角控制模式。"
            },
            new TutorialPageData
            {
                title = "行走与奔跑",
                content = "点击鼠标右键可以在行走和奔跑之间切换。\n" +
                          "行走状态适合探索地图，奔跑状态速度更快。\n\n" +
                          "在主菜单中的\"开始游戏\"触发区域内：\n" +
                          "· 滚动鼠标滚轮切换选项\n" +
                          "· 按下 F 键确认当前选中的选项"
            },
            new TutorialPageData
            {
                title = "游戏规则",
                content = "进入游戏后，你将沿着一条无限延伸的道路向前奔跑。\n\n" +
                          "道路上会有从不同方向驶来的电动车，你需要躲避它们。\n" +
                          "一旦与电动车碰撞，游戏即告结束。\n\n" +
                          "你的奔跑里程会被实时记录，系统会自动保存你的最佳记录。"
            },
            new TutorialPageData
            {
                title = "暂停与菜单",
                content = "在游戏进行中按下 Esc 键可以暂停游戏。\n\n" +
                          "暂停面板提供了以下功能：\n" +
                          "· 继续游戏 —— 关闭面板，恢复游戏\n" +
                          "· 回主菜单 —— 带过渡动画返回主菜单\n" +
                          "· 清空历史成绩 —— 删除历史最佳里程记录\n\n" +
                          "游戏失败后，面板会显示你的里程和历史最佳成绩。"
            },
            new TutorialPageData
            {
                title = "音量与声音设置",
                content = "暂停游戏后，点击左上角的音量按钮可以打开设置面板。\n\n" +
                          "你可以分别调整：\n" +
                          "· 音乐音量 —— 控制背景音乐的大小\n" +
                          "· 音效音量 —— 控制按钮点击等音效的大小\n\n" +
                          "特殊失败语音：\n" +
                          "勾选\"特殊失败音乐\"开关后，游戏失败时将播放特殊的失败音乐，为失败增添趣味。"
            }
        };
    }
}
