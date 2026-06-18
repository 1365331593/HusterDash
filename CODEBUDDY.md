# CODEBUDDY.md This file provides guidance to CodeBuddy when working with code in this repository.

## 常用命令

### 打开项目
在 Unity Hub 中添加项目文件夹 `HusterDash/`，使用 **Unity 2022.3.62f3c1** 版本打开。确保已通过 Package Manager 安装 manifest.json 中列出的所有依赖包（Cinemachine、Input System、TextMeshPro、Universal RP）。

### 运行游戏
在 Unity Editor 中打开 `Assets/Scenes/Splash.unity`，点击 Play 按钮即可从头运行完整流程。Splash 场景播放"Huster Dash"逐字飞入开屏动画后自动过渡到 MainMenu 场景。也可以直接打开 `MainMenu.unity` 或 `Game.unity` 单独运行某个场景进行调试。MainMenu 是第三人称自由视角场景，Game 是俯视角躲避车辆的核心玩法场景。

### 构建项目
构建场景顺序必须是 **Splash → MainMenu → Game**（已在 `EditorBuildSettings.asset` 中配置，索引 0/1/2）。Splash 作为首发场景确保 URP 管线在正确的渲染设置下初始化。URP 的渲染质量设置位于 `Assets/Settings/` 目录下的 URP-Balanced、URP-Performant、URP-HighFidelity 三个资产文件中，可在 Project Settings > Graphics 中切换。

### 运行测试
项目使用 `com.unity.test-framework` 1.1.33。在 Unity Editor 中通过 `Window > General > Test Runner` 打开测试运行器，选择 PlayMode 或 EditMode 标签页运行测试。目前项目中未包含自定义测试用例，Test Runner 会运行 Unity 默认的基础测试。

## 项目架构

### 整体概述

HusterDash 是一款无尽跑酷游戏，玩家在一条无限延伸的道路上向前奔跑，躲避不同方向驶来的电动车。项目基于 **Unity 2022.3.62f3c1 LTS**、**Universal Render Pipeline (URP)** 和 **Cinemachine 2.10.7** 构建。输入系统使用 Unity 新版 Input System（1.14.2），输入配置文件为 `Assets/PlayerControls.inputactions`。

项目包含三个场景，构建时按以下顺序排列（`EditorBuildSettings.asset` 索引 0/1/2）：

- **Splash.unity**：开屏动画场景，播放"Huster Dash"逐字飞入弹跳动画后自动通过 `SceneTransitionManager` 过渡到 MainMenu。场景非常轻量，仅包含一个 Camera、一个 Directional Light 和挂载 `SplashController` 的 GameObject。除 Splash 自己的 Canvas UI 外不渲染任何 3D 内容（背景由 Canvas 的纯色 Image 遮挡）。由于是 URP 管线的首发场景，其 `RenderSettings` 中的若干关键参数必须与 MainMenu/Game 保持一致，否则会导致后续场景渲染偏暗（详见关键注意事项）。
- **MainMenu.unity**：第三人称自由视角场景，玩家可在 3D 环境中自由行走、旋转视角，用于展示或调试角色。
- **Game.unity**：核心玩法场景，俯视固定视角，玩家沿 +Z 轴奔跑躲避车辆，碰撞即游戏结束，记录并持久化最佳里程。

### Splash 场景架构（开屏动画）

Splash 场景是游戏启动入口，负责播放"Huster Dash"逐字飞入弹跳开屏动画后过渡到 MainMenu。场景极其轻量，仅包含三个顶层对象：

**SplashController（`Assets/Scripts/Splash/SplashController.cs`）**：挂载在场景根 GameObject（`SplashController`）上，负责动画全流程：
- **Awake()** 中动态创建 Canvas（ScreenSpaceOverlay, `sortingOrder=0`）、全屏纯色背景 Image（#509AE7 蓝色）和文字容器。
- 为 "Huster Dash" 共 11 个字符（含 1 个空格）各自创建独立 TMP_Text。先用 HorizontalLayoutGroup 自动排列后捕获目标位置（`targetPositions`），计算居中偏移，然后销毁布局组使字符可独立移动。
- **Start()** 启动 `PlaySplashSequence()` 协程：逐字飞入（EaseOutBounce 缓动）→ 保持静止（`holdDuration`，默认 1s）→ 仅文字淡出（背景保持不透明）→ 调用 `SceneTransitionManager.LaunchTransition("MainMenu")`。
- **飞入方向智能分配**：将 360° 分为 N 个扇区（N=非空格字符数），每个扇区内随机取一个角度，然后 Fisher-Yates 洗牌确保视觉上方向均匀分散。飞入距离自动计算确保所有字符从屏幕外出发。
- **背景始终不透明**：文字通过独立 `CanvasGroup` 淡出，背景 Image 保持纯色，确保 MainMenu 场景异步加载期间画面不会闪现空场景内容。
- 使用 `Time.unscaledDeltaTime` 和 `WaitForSecondsRealtime`，动画不受 timeScale 影响。
- 可配置参数：`flyInDuration`（飞入时长）、`holdDuration`（静止保持）、`fadeOutDuration`（淡出时长）、`textFont`、`fontSize`、`characterSpacing`、`textColor`、`backgroundColor`。

**Main Camera**：场景中的唯一相机，附有 `UniversalAdditionalCameraData` 组件。关键配置：
- `m_ClearFlags: 1`（纯色清除，黑色背景，实际被 Canvas 背景遮挡）
- `m_RenderPostProcessing: 0`（Splash 无需后处理）
- `m_RendererIndex: -1`（使用默认 URP 渲染器）

**Directional Light**：温暖色调方向光（`#FFF6D6`），强度 1，开启软阴影（`m_Type: 2`），阴影强度 1。这是确保 URP 管线在首个场景正确初始化光源数据的关键——没有方向光会导致 URP 主光源阴影/光照系统以未定义状态启动。

### 输入系统（PlayerControls.inputactions）

所有输入通过单一 Action Map `Player` 管理，定义六个动作：

| 动作 | 类型 | 绑定 | 用途 |
|------|------|------|------|
| Move | Vector2 | WASD | 角色移动 |
| Look | Vector2 | Mouse Delta | 第三人称视角旋转 |
| ToggleRun | Button | 鼠标右键 | 切换行走/奔跑 |
| CursorUnlock | Button | 左 Alt | 临时解锁鼠标光标 |
| Zoom | Axis | 鼠标滚轮 Y | 相机缩放 |
| Pause | Button | Esc | 切换暂停/恢复 |

`PlayerControls` C# 类由 Unity 自动生成，位于 `Assets/PlayerControls.cs`（不要手动移入子目录，修改 `.inputactions` 后会重新生成到此位置）。Game 场景中的 `PlayerMove` 直接按名称查找 Action；MainMenu 场景使用 `PlayerInput` 组件通过事件绑定分发输入给 `PlayerMovementInput`。`PauseMenuManager` 通过 `InputActionReference` 引用 `Player/Pause` Action 接收 Esc 回调。

### Game 场景架构（核心玩法）

Game 场景采用"**道路无限生成 + 车辆对象池 + 俯视固定相机**"的设计模式。以下按系统拆解：

#### 道路生成系统（RoadGenerator + RoadLane）

`RoadGenerator` 挂载在场景根物体上，负责在玩家前方动态生成道路块、后方回收道路块。道路用 **Queue 结构**管理：`activeRoads`（已激活队列）和 `inactivePool`（回收对象池）。

- 初始化时从 Z 坐标起点（`startOffset`）开始，向前铺设 `spawnDistance` 范围的道路。
- 每帧 Update：检查玩家前方是否需要新道路块（`lastSpawnZ < playerZ + spawnDistance`），循环生成；同时检查最旧道路块是否落后于回收距离（`playerZ - despawnDistance`），若落后则 Dequeue 并 Deactivate。
- 车道方向在 `SpawnRoad()` 中按**每 2 米（2 个道路块）交替一次**的规律计算：根据道路块的世界坐标 Z 计算出 `groupIndex`，组号偶数时方向为 +X（正向），奇数时为 -X（负向）。
- 每个道路块生成时，自动附加 `RoadLane`（存储方向向量）和 `BlockVehicleSpawner`（车辆生成器）组件。

`RoadLane` 是一个极简的数据组件，仅有一个 `public Vector3 direction` 字段，用于向 `BlockVehicleSpawner` 传递当前道路块的车道方向。

#### 车辆系统（BlockVehicleSpawner + VehicleMovement + VehicleIdentifier + VehiclePool）

**BlockVehicleSpawner**：挂载在每个道路块上，当道路块 `OnEnable` 时启动协程 `SpawnVehicles()`，以随机间隔在该道路块上生成车辆。关键设计：

- 每个道路块在 `Awake()` 时随机一个**统一的行驶速度**（`minBlockSpeed` ~ `maxBlockSpeed`），该块内所有车辆共用此速度。
- 生成车辆时根据车道方向决定生成位置：正向车道（+X）车辆从左端（`-roadHalfWidth`）出发，负向车道（-X）从右端（`+roadHalfWidth`）出发。
- 当道路块 `OnDisable`（被回收）时，遍历所有生成的车辆并将其归还到 `VehiclePool`。

**VehicleMovement**：每辆车独立移动，从起点沿车道方向直线行驶。到达道路边界（`±roadHalfWidth`）或落后于玩家 `despawnZOffset` 米时自动归还到对象池。

**VehiclePool**：全局单例，基于 Unity 内置 `IObjectPool<GameObject>` 的车辆对象池。支持多个预制体（粉红色、黄色、紫色电动车），每种预制体维护独立的对象池。`GetRandomVehicle(out originalPrefab)` 随机选择一个预制体并从中取出车辆，`ReturnVehicle()` 归还车辆时会附加 `VehicleIdentifier` 组件标记原始预制体引用。

**VehicleIdentifier**：极简组件，仅保存 `originalPrefab` 引用，用于对象池归还时识别车辆属于哪个预制体的池。

#### 玩家系统（PlayerMove）

Game 场景中的玩家移动使用 `PlayerMove` 脚本，它通过 `[RequireComponent(typeof(PlayerInput))]` 依赖 PlayerInput 组件。移动逻辑：

- 在 `Awake()` 中通过名称查找 Move 和 ToggleRun Action。
- 每帧 Update 读取 Move Action 的 `Vector2` 值，转换为世界空间方向（XZ 平面）。
- 鼠标右键点击（ToggleRun 的 performed 回调）在行走和奔跑之间切换，速度分别为 `walkSpeed` (3 m/s) 和 `runSpeed` (6 m/s)。
- 碰撞检测在 `OnTriggerEnter` 中，若碰到 `Vehicle` Layer 的物体则调用 `GameManager.Instance.GameOver()`。
- 动画驱动通过 Animator 的 `Speed`（Float，控制 Idle/Walk 混合）、`isRunning`（Bool）和 `AnimSpeed`（Float，行走时二倍速播放）参数。
- **移动范围限制**：每帧计算目标位置后通过 `Mathf.Clamp`（X 轴）和 `Mathf.Max`（Z 轴）钳制到固定边界内。X 轴 `minX` ~ `maxX`（默认 -15 ~ +15），Z 轴 `minZ` 以上（默认 -2，正无穷不限）。碰到边界后被钳制轴停止，另一轴可继续移动，实现沿边界滑动效果。参数在 Inspector 的"移动范围限制"分组中可调。

#### 俯视相机系统（FixedTopDownCamera + IntroCameraController）

**FixedTopDownCamera**：固定俯视相机，每个 `LateUpdate` 将相机位置设为 `player.position + offset`，旋转设为固定角度（如 60° 俯视）。相机不跟随玩家旋转，视角绝对固定。

**IntroCameraController**：开场运镜，Game 场景启动后播放一段过场动画：
1. **PlayerWalking 阶段**：玩家从起点后方（Z=-1.5）行进到原点，Animator 驱动行走动画。
2. **CameraRotating 阶段**：相机从右侧面位置通过 Lerp/Slerp 平滑旋转到最终俯视角度。
3. **Finished**：过场结束，启用 `PlayerMove`、`FixedTopDownCamera`、`DistanceTracker`，然后销毁自身。

在过场期间，`PlayerMove`、`FixedTopDownCamera` 和 `DistanceTracker` 均被禁用，防止干扰。

#### 装饰性传送门系统（PortalVFX + PortalVortex.shader）

传送门是纯视觉效果，无实际玩法功能。视觉上表现为蓝白渐变（`#509AE7` → `#8D8DF0` → `#FFFFFF`）的圆形半透明能量漩涡，直径约 0.8 米，边缘有少量光点飘散，始终面朝相机。

**PortalVortex.shader（`Assets/Shaders/PortalVortex.shader`）**：URP HLSL 自定义 Shader，程序化生成漩涡纹理（不依赖贴图）。核心特性：
- 顶点着色器中实现 **Billboard**：以物体中心为基准在观察空间重建顶点，确保传送门始终面朝相机。
- 片元着色器中用极坐标 + 分形布朗运动（FBM）噪声生成多层漩涡扭曲效果，叠加 `_Time` 驱动持续旋转。
- 颜色渐变：半径方向上从内圈蓝 → 中圈淡紫 → 外圈白三色插值。
- 光环遮罩：通过 `smoothstep` 实现模糊环带（`_RingRadius`=0.38，`_RingThickness`=0.12），中心微光防止完全透明，外边缘柔和淡出。
- 片元着色器中超出圆范围的片段通过 `discard` 剔除，保证圆形外观。
- 双 Pass 结构（Forward + DepthOnly），`ZWrite Off`，`Cull Off`，`Blend SrcAlpha OneMinusSrcAlpha`。

关键 Shader 属性：`_InnerColor`（#509AE7）、`_MidColor`（#8D8DF0）、`_OuterColor`（#FFFFFF）、`_VortexSpeed`（1.5）、`_DistortStrength`（0.25）、`_RingRadius`（0.38）、`_RingThickness`（0.12）、`_EdgeSoftness`（0.15）、`_CenterGlow`（0.08）。

**PortalVFX.cs（`Assets/Scripts/Game/PortalVFX.cs`）**：挂载在传送门 GameObject 上，职责：
- **上下浮动**：使用正弦函数驱动 `transform.localPosition.y`，默认幅度 ±0.2m、频率 0.8Hz，支持随机相位偏移使多个传送门不同步。
- **材质参数驱动**：每帧将 `_VortexSpeed`、`_DistortStrength`、`_CenterGlow` 写入材质实例。
- **对象池兼容**：使用 `baseLocalPosition`（本地坐标）而非世界坐标作为浮动基准，避免对象池复用时 `SetActive` 先于位置设置导致的错位问题。`Awake()` 中记录一次本地坐标（Prefab 结构不变），`HandleFloatingAnimation()` 操作 `transform.localPosition`。

**边缘粒子系统**：可选的 ParticleSystem 子物体，配置为 Circle Shape（Radius=0.38）、每秒发射 10~20 个淡紫（#8D8DF0）到白色的小光点，具有 Radial 向外速度 0.15~0.35 m/s，生命周期中颜色渐变至透明消失。

**Prefab 结构建议**：`Portal` 根节点挂 `PortalVFX`，子节点 `VortexQuad`（Quad Mesh + PortalVortex 材质，Scale 0.8×0.8×1），可选子节点 `EdgeParticles`（ParticleSystem）。传送门作为道路块 Prefab 的子物体嵌入道路生成系统。

#### 场景过渡管理器（SceneTransitionManager）

**SceneTransitionManager（`Assets/Scripts/Common/SceneTransitionManager.cs`）**：全局单例跨场景过渡管理器，提供带淡入淡出的异步场景切换。核心特性：
- **预制体驱动**：`LaunchTransition()` 从 `Resources/SceneTransitionManager.prefab` 加载预制体实例化，所有参数（字体、字号、文本、颜色、时长）可在 Inspector 中可视化配置。若预制体缺失则降级为代码动态创建并使用默认值。
- **动态 UI**：运行时创建 Canvas（`ScreenSpaceOverlay`, `sortingOrder=9999`），包含 `#509AE7` 全屏遮罩 Image 和白色居中 "Loading..." 文字（TMP_Text）。
- **可配置项**：`loadingFont`（TMP 字体资产）、`loadingFontSize`（字号）、`loadingText`（文字内容）、`overlayColor`（遮罩颜色）、`transitionDuration`（总过渡时长）、`canvasSortingOrder`（渲染层级）。
- **使用 `Time.unscaledDeltaTime`**：过渡动画不受 `Time.timeScale` 影响，即使在暂停状态下淡入也能正常工作。
- **异步加载**：使用 `SceneManager.LoadSceneAsync` 加载目标场景，不阻塞主线程。
- **过渡流程**：显示遮罩 → 0.25s 淡入 → 异步加载场景 → 0.25s 淡出 → 隐藏遮罩 → 销毁自身。总过渡时长约 0.5 秒。
- **跨场景使用**：Game ↔ MainMenu 双向切换只需一行 `SceneTransitionManager.LaunchTransition("场景名")`。
- **防重复**：内部 `isTransitioning` 标记防止快速点击触发多次过渡。

**创建预制体步骤**：
1. 在 `Assets/` 下新建 `Resources` 文件夹。
2. Hierarchy 中创建空 GameObject，命名为 `SceneTransitionManager`，挂载 `SceneTransitionManager` 脚本。
3. 在 Inspector 中配置字体、字号等参数。
4. 拖入 `Assets/Resources/` 生成 `.prefab`，删除场景中的实例。

#### 游戏状态管理（GameManager + DistanceTracker）

**GameManager**：全局单例，管理游戏失败流程和光标状态。`GameOver()` 被 `PlayerMove.OnTriggerEnter()` 调用时：
1. 设置 `Time.timeScale = 0` 暂停游戏。
2. 通过 `DistanceTracker` 获取当前里程和历史最佳里程。
3. 判断是否刷新记录，更新失败界面文本（TMPro）。
4. 显示失败面板（含重新开始和返回主菜单按钮）。
5. 解锁并显示光标（方便点击 UI 按钮）。
6. 禁用 `PlayerMove` 阻止继续移动。

`BackToMenu()` 隐藏失败界面后调用 `SceneTransitionManager.LaunchTransition("MainMenu")` 带淡入淡出切换场景。`RestartGame()` 调用 `SceneTransitionManager.LaunchTransition(当前场景名)` 同样带过渡效果重新加载（由 `Start()` 绑定按钮事件）。

光标管理：`Start()` 时锁定并隐藏光标（`CursorLockMode.Locked` + `Cursor.visible = false`）；按住左 Alt 键（`Player/CursorUnlock` Action）时临时解锁光标，松开后重新锁定；游戏结束（`GameOver()`）或暂停（`IsPaused`）时阻止 Alt 光标操作，防止暂停期间错误锁定光标。`cursorUnlockAction` 字段需在 Inspector 中引用 `Player/CursorUnlock` 动作。

提供两个公开属性供外部查询：`IsGameOver`（只读，游戏是否结束）和 `IsPaused`（可读写，是否暂停中，由 PauseMenuManager 设置）。

**DistanceTracker**：里程记录器，从起点 Z=1 开始记录玩家前进距离。支持 `onlyIncrease` 模式（默认开启）防止后退导致里程减少。使用 `PlayerPrefs` 持久化历史最佳记录（键名 "BestDistance"）。提供 `GetCurrentDistance()`、`GetBestDistance()` 和 `ResetBestDistance()` 公开方法——其中 `ResetBestDistance()` 将内存缓存的 `bestDistance` 重置为 0，供 `PauseMenuManager` 清空成绩后调用，确保 GameOver 面板立即反映清空状态（仅删 PlayerPrefs 不会更新内存缓存）。

#### 暂停菜单系统（PauseMenuManager）

**PauseMenuManager（`Assets/Scripts/Game/PauseMenuManager.cs`）**：管理 Game 场景的暂停功能，所有 UI 放在场景中初始隐藏。核心特性：

- **触发方式**：Esc 键（`Player/Pause` Action 的 `InputActionReference`）或点击左上角暂停图标按钮。
- **暂停流程**：设置 `Time.timeScale = 0` 冻结游戏 → 设置 `GameManager.Instance.IsPaused = true` 阻止 Alt 光标干扰 → 隐藏暂停按钮、显示暂停面板 → 解锁光标、禁用 `PlayerMove`。
- **恢复流程**：先检查确认弹窗是否显示（若显示则优先关闭弹窗）→ 恢复 `timeScale = 1` → 设置 `IsPaused = false` → 显示暂停按钮、隐藏面板 → 锁定光标、启用 `PlayerMove`。
- **暂停按钮显隐**：`Update()` 中根据 `CanPause()` 实时动态控制按钮显隐——过场动画进行中或失败面板显示时隐藏按钮。暂停期间（`isPaused`）跳过此逻辑，防止覆盖 `PauseGame()`/`ResumeGame()` 设置的状态。
- **暂停前置条件（CanPause）**：失败面板显示中（`GameManager.IsGameOver`）或开场过场动画未结束（`IntroCameraController` 存在且 enabled）时不允许暂停。
- **三个面板按钮**：继续游戏（调用 `ResumeGame()`）、回主菜单（恢复 timeScale=1 后调用 `SceneTransitionManager.LaunchTransition("MainMenu")`）、清空历史成绩（显示确认弹窗）。
- **确认弹窗**：暂停面板上方的叠加对话框，包含"确定"/"取消"按钮。确认后删除 `PlayerPrefs.DeleteKey("BestDistance")` + 调用 `DistanceTracker.ResetBestDistance()` 同步重置内存缓存。
- **Inspector 配置**：9 个引用字段——`pauseButton`（GameObject）、`pausePanel`（GameObject）、`continueButton`/`backToMenuButton`/`clearRecordButton`（Button 组件）、`confirmDialog`（GameObject）、`confirmClearButton`/`cancelClearButton`（Button 组件）、`pauseAction`（`InputActionReference`，拖入 Player/Pause Action）。
- **与 GameManager 的协作**：通过 `GameManager.IsPaused` 告知暂停状态，GameManager 的 Alt 回调会检查此标记阻止错误锁定光标。

### MainMenu 场景架构

MainMenu 场景采用**第三人称角色控制器 + Cinemachine FreeLook 相机**设计，玩家的所有脚本挂载在同一个 GameObject 上。

#### 脚本职责分离（三层架构）

```
PlayerMovementInput（输入接收层）
    ↓ 调用 SetMoveInput(Vector2)
PlayerMovement（移动执行层）
    ↓ 调用 SetSpeed(float) / SetIsMoving(bool)
PlayerAnimatorController（动画控制层）
    ↓ 操作
Animator
```

**PlayerMovementInput**：通过 `PlayerInput` 组件的 SendMessage/UnityEvent 方式接收输入。`OnMove()` 持续将 WASD 输入传递给 `PlayerMovement.SetMoveInput()`；`OnToggleRun()` 在右键完整点击一次后触发 `PlayerMovement.ToggleWalkRun()`。

**PlayerMovement**：核心移动逻辑，通过 `CharacterController.Move()` 执行物理移动。特点：
- 将输入方向转换为**相对于相机**的世界方向（忽略 Y 轴，保持地面移动）。
- **重力系统**：`HandleMovement()` 每帧通过 `characterController.isGrounded` 检测地面状态，在地面时施加微小下压力（`groundedForce`，默认 -2）防止浮空，在空中时累加 `gravity`（默认 -9.81 m/s²）实现自然下落。
- **斜坡行走**：依赖 CharacterController 内置的 Slope Limit（默认 45°），`moveDirection` 始终保持在 XZ 平面，CharacterController.Move() 自动将水平移动投影到坡面上。移动速度保持恒定，不随坡度变化。
- **台阶跨越**：依赖 CharacterController 内置的 Step Offset（默认 0.5m），自动跨过低于此高度的台阶，无需玩家额外操作。
- `HandleMovement()` 合并水平移动与垂直速度后调用 `CharacterController.Move(motion)`，即使无输入时也会调用 Move 以保持贴地或下落。
- `UpdateAnimation()` 驱动 Animator 参数，Speed 值为 0（待机）/1（行走）/2（奔跑），用于 Blend Tree 混合。

**PlayerAnimatorController**：封装 Animator 参数设置，提供 `SetSpeed(float)` 和 `SetIsMoving(bool)` 统一接口。Blend Tree 中 Speed=0（Idle）、1（Walk）、2（Run）。

#### 第三人称相机控制（ThirdPersonCameraController）

`ThirdPersonCameraController` 控制 `CinemachineFreeLook` 虚拟相机：
- 启动时自动锁定光标（`CursorLockMode.Locked`）。
- Alt 键按下时临时解锁光标并将灵敏度降为 0；释放 Alt 后恢复光标锁定和正常灵敏度。
- 滚轮缩放通过 `HandleZoom()` 调整 Middle 轨道半径，同时按比例同步 Top/Bottom 轨道，缩放范围在 `minDistance` ~ `maxDistance` 之间。
- `ClampCameraDistance()` 作为安全兜底每帧强制钳制。
- 提供 `SetZoomEnabled(bool)` 公开方法，供 `GameStartZone` 在弹出 UI 面板时临时禁用滚轮缩放，防止玩家在菜单中滚轮时同时触发相机缩放和选项切换冲突。

#### 触发区域交互系统（GameStartZone + GameStartZoneUI + FloatingPromptText + GlowingBorder）

MainMenu 场景中新增了一套"走近触发区域 → 弹出选项面板 → 按 F 键开始游戏"的交互系统，由四个组件协作完成：

**GameStartZone（`Assets/Scripts/MainMenu/GameStartZone.cs`）**：挂载在 TriggerZone 根节点上的核心控制器，依赖 `BoxCollider`（IsTrigger=true）。职责：
- **进入/离开检测**：`OnTriggerEnter/Exit` 中过滤 `Player` 标签，进入时依次调用 `floatingText.Hide()` → `uiPanel.Show()` → `thirdPersonCamera.SetZoomEnabled(false)`；离开时反向恢复。
- **滚轮切换**：每帧 `HandleScrollInput()` 通过 `Mouse.current.scroll.y`（新版 Input System）读取滚轮增量，累积超过 `ScrollThreshold`（0.1）后调用 `uiPanel.SelectPrevious()/SelectNext()`。
- **按键确认**：使用新版 Input System 的 `Keyboard.current[interactKey].wasPressedThisFrame` 检测按键（**禁止使用旧版 `Input.GetKeyDown()`**），按下后调用 `uiPanel.ConfirmSelection()`。
- **边框同步**：`Start()` 中自动将子物体 `borderQuad` 的 scale 和位置匹配 `BoxCollider` 尺寸，平铺在 XZ 平面上，离地 `borderYOffset` 高度（默认 0.01m）。
- **诊断日志**：进入区域时一次性输出当前状态（uiPanel 引用、Keyboard 状态、interactKey 值等），方便排查配置问题。

**GameStartZoneUI（`Assets/Scripts/MainMenu/GameStartZoneUI.cs`）**：挂载在 GameStartPanel 上的 UI 逻辑组件，管理两个选项（"开始游戏" / "新手教学"）。职责：
- **Show() / Hide()**：通过 `panelRoot.SetActive()` 控制面板可见性。`Show()` 中强制调用 `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate()` 重建布局，确保 VerticalLayoutGroup 完成排列后再定位 F 图标和三角——这是解决滚轮切换后位置跳变的关键。
- **选项切换**：`SelectPrevious()/SelectNext()` 循环切换 `currentIndex`（0↔1），调用 `RefreshSelection()` 更新 UI。
- **RefreshSelection()**：使用选项背景 **Image** 的 `RectTransform`（而非 TMP_Text）作为定位基准，将世界坐标转为 `panelRoot` 本地坐标，计算左边缘后放置 F 图标和三角指示器。F 图标间距由 `iconToOptionsSpacing` 控制，三角间距由 `triangleOffset` 独立控制。
- **ConfirmSelection()**：检查 `IsShowing()` → 检查 `UnityEvent` 绑定状态 → 触发事件。未绑定时输出明确的红色错误日志指导修复。
- **字体样式**：`ApplySettings()` 将 Inspector 中的 `font`、`fontSize`、`optionSpacing` 写入选项 Text 和 VerticalLayoutGroup。文字内容来自硬编码的 `optionLabels` 数组。
- **UnityEvent 绑定**：`onStartGame` 和 `onTutorial` 两个 UnityEvent，Inspector 中需绑定到 `OnStartGameClicked()` / `OnTutorialClicked()`。其中 `OnStartGameClicked()` 调用 `SceneTransitionManager.LaunchTransition("Game")`。

**FloatingPromptText（`Assets/Scripts/MainMenu/FloatingPromptText.cs`）**：挂载在 TriggerZone 子物体（TMP_Text）上的悬浮提示。职责：
- **Billboard**：`LateUpdate()` 中将文字旋转朝向主摄像机，保持水平方向不倾斜。
- **Show() / Hide()**：通过 `tmpText.enabled` 控制显示，供 `GameStartZone` 在进入/离开时调用。
- **样式配置**：文字内容（默认 "进入此区域以开始游戏"）、颜色、字号、字体均可在 Inspector 中配置，`floatHeight` 控制离地高度。

**GlowingBorder.shader（`Assets/Shaders/GlowingBorder.shader`）**：URP 透明 Shader，在 Quad 上绘制脉冲发光的矩形边框。核心特性：
- 在 UV 空间中计算像素到矩形边缘的距离，通过 `_BorderWidth` 和 `_EdgeSoftness` 实现柔和渐变边框。
- 叠加 `_PulseSpeed` 和 `_PulseAmount` 通过 `sin(_Time.y)` 驱动脉冲动画。
- `_Color`（默认 #509AE7）和 `_EmissionStrength` 控制发光颜色和强度。
- `Blend SrcAlpha OneMinusSrcAlpha`、`ZWrite Off`、`Cull Off` 保证透明叠加。

**Prefab 结构**：
- **TriggerZone.prefab**：根节点挂 `GameStartZone` + `BoxCollider`（IsTrigger=true），子节点包含 `FloatingPromptText`（TMP_Text 悬浮提示）和 `BorderQuad`（Quad Mesh + GlowingBorder 材质）。
- **GameStartPanel.prefab**：Canvas 子物体，挂载 `GameStartZoneUI`，内部包含 F 键图标 Image、选项容器（VerticalLayoutGroup + 两个选项背景 Image 及其 TMP_Text 子物体）、三角指示器 RectTransform。

#### 新手教学面板系统（TutorialPanelManager + TutorialPageData）

MainMenu 场景中玩家通过触发区域选择"新手教学"选项后，弹出全屏遮罩教程面板，分页阅读游戏操作和规则，支持 Esc 键跳过。

**TutorialPageData（`Assets/Scripts/MainMenu/TutorialPageData.cs`）**：教程页面数据结构（Serializable），每个页面包含 `title`（标题）和 `content`（内容文本），均在 Inspector 中可编辑。使用 `[Tooltip]` 和 `[TextArea]` 特性增强 Inspector 编辑体验。

**TutorialPanelManager（`Assets/Scripts/MainMenu/TutorialPanelManager.cs`）**：教程面板核心控制器，静态 `IsShowing` 属性供外部查询面板显示状态。核心特性：

- **Show() 流程**：
  1. 若 `pages` 数组为空且 `useDefaultPagesIfEmpty=true`，自动调用 `GetDefaultPages()` 生成 6 页硬编码教程内容（基本移动、视角控制、行走与奔跑、游戏规则、暂停与菜单、音量与设置）。
  2. 调用 `FreezePlayer()` 冻结玩家输入：保存 `PlayerMovement.enabled`/`PlayerInput.enabled`/`CinemachineFreeLook.m_XAxis.m_MaxSpeed`/`m_YAxis.m_MaxSpeed` 状态 → 禁用移动和 PlayerInput → 设相机灵敏度为 0 冻结视角。
  3. 解锁并显示光标（`CursorLockMode.None` + `Cursor.visible = true`）方便点击翻页按钮。
  4. 设置 `panelRoot.SetActive(true)`，翻到第 1 页并刷新 UI。

- **Hide() 流程**：
  1. `panelRoot.SetActive(false)` 隐藏面板。
  2. `UnfreezePlayer()` 恢复之前保存的玩家输入和相机灵敏度状态。
  3. 重新锁定并隐藏光标（`CursorLockMode.Locked` + `Cursor.visible = false`）。

- **翻页逻辑**：`PrevPage()` 向前翻页（第 1 页时"上一页"按钮禁用）。`NextPage()` 向后翻页，最后一页点击变为关闭面板。
- **Esc 跳过**：`Update()` 中使用 `Keyboard.current.escapeKey.wasPressedThisFrame`（新版 Input System）检测 Esc 键，按下即关闭面板。**必须使用新版 Input System API，禁止使用 `Input.GetKeyDown()`**。
- **UpdatePageUI()**：刷新当前页的标题、内容文本、页码指示器（"第 X/Y 页"）、上一页/下一页按钮状态和文字。

**与 GameStartZone 的协作**：
- `GameStartZoneUI.OnTutorialClicked()`：通过 `FindObjectOfType<TutorialPanelManager>()` 查找管理器并调用 `Show()`。
- `GameStartZone.Update()` 和 `OnTriggerExit()`：检测 `TutorialPanelManager.IsShowing` 为 true 时跳过所有交互逻辑（滚轮切换、F 键确认），防止教程面板打开期间玩家走出触发区域或重复触发。
- UnityEvent 绑定：`GameStartZoneUI` 的 `onTutorial` 事件必须在 Inspector 中绑定到 `OnTutorialClicked()`。

**TutorialPanel 预制体结构**（Canvas, ScreenSpaceOverlay, sortingOrder=100）：
```
TutorialPanel（挂 TutorialPanelManager）
  └── PanelRoot（Image - 半透明遮罩，全屏拉伸）
        ├── TitleText（TMP_Text - 页面标题）
        ├── ContentText（TMP_Text - 页面内容）
        ├── PageIndicator（TMP_Text - "第 1/6 页"）
        ├── PrevButton（Button + TMP_Text "← 上一页"）
        ├── NextButton（Button + TMP_Text "下一页 →"）
        └── SkipHint（TMP_Text - "按 Esc 跳过教程"）
```

#### 退出游戏按钮（QuitGameButton）

**QuitGameButton（`Assets/Scripts/MainMenu/QuitGameButton.cs`）**：极简退出按钮组件，挂载到退出按钮 GameObject 上。职责：
- **自动注册**：`Start()` 中自动查找同物体 `Button` 组件并注册 `onClick` 事件，也可通过 `targetButton` 字段手动指定按钮引用。
- **退出逻辑**：点击后 Editor 中调用 `EditorApplication.isPlaying = false` 停止播放模式，打包后调用 `Application.Quit()` 退出应用程序。
- **事件注销**：`OnDestroy()` 中自动注销 `onClick` 事件，防止内存泄漏。

**使用方式**：在 Canvas 下创建退出 Button，将 `QuitGameButton.cs` 拖到该 Button 上即可，无需额外配置。

### 对象池设计模式

项目中实现了两种对象池：

1. **车辆对象池（VehiclePool）**：使用 Unity 内置 `ObjectPool<GameObject>`，支持多预制体（按预制体引用为键，每个预制体独立维护池），默认容量 10，最大 30。
2. **道路块对象池（RoadGenerator 内部）**：手动管理 `Queue<GameObject>`，`inactivePool` 存储已回收的道路块，生成时优先从池中取用而非重新 Instantiate。

两个池都遵循 **Get → Use → Release** 模式，避免频繁 Instantiate/Destroy 导致 GC 压力。

### 项目依赖

| 包名 | 版本 | 作用 |
|------|------|------|
| com.unity.cinemachine | 2.10.7 | 第三人称相机控制（FreeLook 虚拟相机） |
| com.unity.inputsystem | 1.14.2 | 统一输入管理，支持 WASD/鼠标/滚轮 |
| com.unity.render-pipelines.universal | 14.0.12 | URP 渲染管线，3D 场景光照后处理 |
| com.unity.textmeshpro | 3.0.7 | 高质量 UI 文本渲染 |
| com.unity.timeline | 1.7.7 | 时间线/过场动画（当前未直接使用） |
| com.unity.test-framework | 1.1.33 | 单元测试和 PlayMode 测试框架 |

### BOXOPHOBIC 第三方资源

`Assets/BOXOPHOBIC/` 目录包含 **Polyverse Skies** 天空盒插件，用于生成程序化天空效果。其包含 Runtime（运行时天空着色器）、Scripts（天空系统脚本）和 Editor（编辑器工具）三个模块，分别对应三个独立的 `.asmdef` 程序集。

### 关键注意事项

- **两个场景使用不同的玩家移动脚本**：Game 场景用 `PlayerMove`（直接输入读取 + Transform 移动），MainMenu 用 `PlayerMovement` + `PlayerMovementInput`（分离架构 + CharacterController 移动）。修改移动逻辑时需确认场景。
- **Game 场景的里程起点是 Z=1**：`DistanceTracker.startZ` 和 `RoadGenerator` 的 `startOffset`/`SpawnRoad()` 中的方向偏移均以此为基准。
- **车道方向每 2 米交替**：该逻辑硬编码在 `RoadGenerator.SpawnRoad()` 中的 `blockIndex` 和 `groupIndex` 计算中，修改道路块长度或方向规律需同步调整。
- **车辆回收是双向的**：车辆可被自身 `VehicleMovement` 回收（越界/落后玩家），也可在道路块被回收时由 `BlockVehicleSpawner.OnDisable()` 强制回收。两种路径均有 `isReturned` 标记防重复回收。
- **PlayerPrefs 键名 "BestDistance"**：最佳记录存储键，如需多个存档或重置功能，需在此处修改。
- **MainMenu 的三层脚本架构互有耦合**：`PlayerMovementInput` → `PlayerMovement` → `PlayerAnimatorController`，修改参数传递链时需同步更新三处。
- **MainMenu 场景的碰撞体要求**：角色使用 CharacterController 进行碰撞检测，因此场景中所有可行走地面（马路、人行道、台阶、斜坡）必须添加 `MeshCollider`，障碍物（楼房、汽车、路灯）建议添加 `BoxCollider` 或 `CapsuleCollider`。无碰撞体的物体会被角色直接穿越。
- **CharacterController 的关键参数**：`Slope Limit`（默认 45°）决定角色能行走的最大坡度，`Step Offset`（默认 0.5m）决定角色能自动跨过的台阶最大高度。这些值在 Inspector 中调整，不需要代码修改。
- **玩家 GameObject 不应有 Rigidbody 重力**：CharacterController 拥有独立的碰撞和移动系统，重力由 `PlayerMovement` 脚本自行管理。如果玩家挂有 Rigidbody，需设为 Kinematic 或移除，避免与 CharacterController 的物理发生冲突。
- **Game 场景的传送门使用本地坐标浮动**：`PortalVFX.cs` 通过 `transform.localPosition` 而非 `transform.position` 驱动浮动动画。这是因为 `RoadGenerator.SpawnRoad()` 中先 `SetActive(true)` 后设置世界位置，若使用世界坐标会在对象池复用时记录到旧位置。本地坐标相对父物体（道路块）不变，完全规避此问题。
- **Game 场景的 GameManager 需要手动配置光标控制**：`cursorUnlockAction` 字段需在 Inspector 中拖入 `PlayerControls` 资产的 `Player/CursorUnlock` Action，否则按住 Alt 呼出光标功能不生效。
- **Game 场景玩家移动范围由 PlayerMove 钳制**：X 轴 `minX`/`maxX`（默认 ±15），Z 轴 `minZ`（默认 -2）。边界为固定世界坐标，通过 `Mathf.Clamp`/`Mathf.Max` 实现，碰到边界后可沿边界滑动。无视觉提示，纯逻辑限制。
- **场景切换必须通过 SceneTransitionManager**：调用 `SceneTransitionManager.LaunchTransition("场景名")` 触发带淡入淡出的平滑过渡，不要在 GameManager 或其他地方直接调用 `SceneManager.LoadScene`。过渡管理器在首次调用时自动创建，过渡完成后自动销毁。
- **MainMenu 触发区域按键检测必须使用新版 Input System**：`GameStartZone.Update()` 中使用 `Keyboard.current[interactKey].wasPressedThisFrame` 检测按键，**禁止**使用旧版 `Input.GetKeyDown()`——在纯 Input System Package 模式下旧版 API 静默失效，按 F 键无反应。
- **EventSystem 必须使用 InputSystemUIInputModule**：场景中的 EventSystem GameObject 必须移除默认的 `StandaloneInputModule`，替换为 `InputSystemUIInputModule`。否则 UGUI 事件系统会尝试调用旧版 `Input.GetButtonDown()`，导致 `InvalidOperationException`。操作步骤：选中 EventSystem → 移除 StandaloneInputModule → Add Component → Input System UI Input Module。
- **GameStartZone 的 interactKey 字段类型为 `Key`（非 `KeyCode`）**：Unity 序列化可能导致旧值映射错误（如变成 F9），需在 Inspector 中重新选择 `F`。
- **GameStartZoneUI.Show() 中必须先强制重建布局再定位**：`Show()` 调用 `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate()` 确保 VerticalLayoutGroup 完成排列后才执行 `RefreshSelection()`，否则 F 图标和三角的定位坐标基于未完成的布局计算，导致滚轮切换后位置不一致。
- **RefreshSelection() 使用选项背景 Image 的 RectTransform 定位**：不再使用 TMP_Text 宽度（文字宽度随内容变化），改用 Image 的 rect（由 VerticalLayoutGroup 稳定控制），定位更可靠。
- **PlayerControls 自动生成类禁止手动移入子目录**：该类由 Input System 在 `Assets/PlayerControls.cs` 自动生成。如果存在旧副本（如 `Assets/Scripts/MainMenu/PlayerControls.cs`），必须删除，否则出现全类重复定义错误。修改 `.inputactions` 后 Unity 会在默认位置重新生成。
- **Game 场景暂停菜单需在 Inspector 中配置 Pause Action 引用**：`PauseMenuManager.pauseAction` 字段需拖入 `Player/Pause` Action 的 InputActionReference，否则 Esc 键不生效。其余按钮（继续/回菜单/清空/确认/取消）已通过代码绑定，无需 Inspector 配置。
- **清空历史成绩必须同时重置 DistanceTracker 缓存**：仅 `PlayerPrefs.DeleteKey("BestDistance")` 不会更新 `DistanceTracker` 内存中的 `bestDistance` 缓存，`GameManager.GameOver()` 调用 `GetBestDistance()` 仍返回旧值。必须额外调用 `DistanceTracker.ResetBestDistance()` 将缓存归零。
- **PauseMenuManager.Update() 在 timeScale=0 时仍然执行**：Unity 的 `Update()` 不受 `Time.timeScale` 影响（只有 `FixedUpdate()` 受影响）。暂停期间需用 `isPaused` 标记区分状态，防止 Update 逻辑覆盖暂停/恢复时设置的状态。
- **Game 场景暂停菜单与失败面板互斥**：`CanPause()` 检查 `GameManager.IsGameOver`，失败面板显示时不允许暂停，避免两个全屏面板重叠。同样，开场过场未结束时不允许暂停。
- **教程面板打开时 GameStartZone 交互被冻结**：`GameStartZone.Update()` 中通过 `TutorialPanelManager.IsShowing` 检测教程面板状态，面板显示时跳过滚轮切换和 F 键确认。`OnTriggerExit()` 同样跳过离开处理，防止面板打开期间意外关闭。`TutorialPanelManager` 显示时会禁用玩家的 `PlayerInput` 和 `PlayerMovement` 组件并将相机灵敏度设为 0。

- **Splash 场景 RenderSettings 必须与 MainMenu/Game 保持一致**：Splash 作为 URP 管线的首发场景，其 `m_DefaultReflectionMode`、`m_AmbientMode` 等关键渲染参数在 URP 初始化时被写入全局渲染状态。若这些值与后续场景不一致（例如 Splash 使用 `Custom` 反射模式而 MainMenu 使用 `Skybox`），URP 的环境光球谐探针和反射数据不会在场景切换时自动重新计算，导致 MainMenu/Game 渲染偏暗。当前三个场景的关键参数配置：
  - `m_DefaultReflectionMode`: 均为 **0**（Skybox）
  - `m_AmbientMode`: 均为 **0**（Skybox）
  - `m_AmbientIntensity`: Splash=**1**，MainMenu/Game=**2.5**（Splash 的 ambientIntensity 不影响后续场景，因为环境光 SH 探针在 URP 初始化后已固化）
  - Splash 的 Camera 必须带有 `UniversalAdditionalCameraData` 组件，且场景中必须有 Directional Light，确保 URP 光照管线以正确状态启动。

### 音乐系统（MusicManager + MusicVolumePanel + ButtonClickSound）\n\n**MusicManager（`Assets/Scripts/Common/MusicManager.cs`）** 是跨场景持久化音乐管理器，DontDestroyOnLoad 单例，永不销毁。核心特性：\n\n- **启动延迟**：`Start()` 中通过 `StartCoroutine(DelayedStartPlayback())` 启动延迟协程。等待 `startupDelay` 秒（默认 1 秒，Inspector 中"启动与淡入淡出"分组可调）后才开始播放音乐。使用显式 `StartCoroutine` 而非 `IEnumerator Start()` 语法糖，避免与 DontDestroyOnLoad 的潜在冲突。\n- **启动爆音防护**：`DelayedStartPlayback()` 中以 `sourceA.volume = musicVolume`（PlayerPrefs 保存值）直接启动，不依赖 AudioMixer 衰减。AudioMixer.SetFloat 是异步操作（发往音频线程），若 Mixer 参数尚未生效就开始输出音频，会导致启动爆音。播放后立即调用 `ApplyVolumeSettings()`，等待 `WaitForSecondsRealtime(0.2f)` 确保音频线程处理完毕，再从 `musicVolume` 平滑过渡到 `MusicSourceBaseVolume`（1f），过渡期间感知音量不变（AudioSource 增大补偿 Mixer 衰减）。\n- **首次场景加载拦截**：`OnActiveSceneChanged()` 顶部新增守卫 `if (string.IsNullOrEmpty(currentTrack)) return;`。Unity 的 `activeSceneChanged` 事件在场景首次加载完成后也会触发，若不跳过则 `FullCrossfade` 会绕过 `DelayedStartPlayback` 协程直接播放音乐，导致启动延迟不生效且产生爆音。\n- **ApplyVolumeSettings 参数验证**：写入 Mixer 前先用 `audioMixer.GetFloat()` 验证暴露参数是否存在（Unity 对不存在的参数静默忽略，不报错），存在才调用 `SetFloat`。不存在时输出明确的红色错误日志指导修复。\n- **自动场景音乐切换**：监听 `SceneManager.activeSceneChanged` 事件，当场景名变化时执行 0.5s 淡出旧音乐 + 0.5s 淡入新音乐。当场景名不变（如 Game 场景重新开始）时音乐不中断。\n- **双 AudioSource 交叉淡入淡出**：`sourceA` 和 `sourceB` 交替使用，`activeIndex` 在 0/1 之间切换，实现无缝过渡。\n- **与 SceneTransitionManager 协作**：`SceneTransitionManager.DoTransition()` 在加载目标场景前调用 `MusicManager.Instance.OnSceneTransitionStart(sceneName)` 提前开始淡出旧音乐，使音频过渡与视觉遮罩淡入淡出同步。\n- **特殊失败音乐**：`GameManager.GameOver()` 中调用 `MusicManager.Instance.OnGameOver()`。若玩家在设置中启用特殊失败音乐，则：保存当前 Game 音乐播放进度 → 淡出 Game 音乐并暂停 → 用闲置音源播放 `冬の花片段.m4a`（循环关闭）→ 播放完毕后从中断处恢复 Game 音乐并淡入。\n- **timeScale=0 兼容**：推荐使用 `AudioMixer` 资产。AudioMixer 在独立音频线程运行，不受 `Time.timeScale` 影响。若未指定 `audioMixer` 字段，降级为直接控制 `AudioSource.volume`（但 timeScale=0 时音频会暂停）。\n- **音量持久化**：`SetMusicVolume()`/`SetSFXVolume()`/`SetSpecialGameOverMusicEnabled()` 均通过 `PlayerPrefs` 持久化，键名分别为 `MusicVolume`、`SFXVolume`、`EnableSpecialGameOverMusic`。启动时 `ApplyVolumeSettings()` 从 PlayerPrefs 加载写入 AudioMixer。所有淡入淡出协程使用 `MusicSourceBaseVolume` 属性（有 Mixer 时 = 1f，无 Mixer 时 = musicVolume）作为目标值。\n- **音频片段**：通过 Inspector 公共字段赋值，不通过 Resources.Load 加载。\n  - `mainMenuMusic`：`60%的日常.m4a`（MainMenu 场景）\n  - `gameMusic`：`下一篇章.m4a`（Game 场景）\n  - `gameOverSpecialMusic`：`冬の花片段.m4a`（特殊失败音乐）\n  - `buttonSfx`：`dianji.mp3`（按钮音效）\n- **自动绑定 AudioMixer Group**：`CreateAudioSources()` 中通过 `audioMixer.FindMatchingGroups("")` 按名称查找 Music 和 SFX Group 并绑定到对应 AudioSource 的 `outputAudioMixerGroup`。绑定成功/失败均输出日志便于诊断。用户仍需在 Inspector 中拖入 AudioMixer 资产到 `audioMixer` 字段，并在 Mixer 中手动创建两个 Group 和暴露 Volume 参数。\n\n**MusicVolumePanel（`Assets/Scripts/Common/MusicVolumePanel.cs`）**：音量设置面板 UI 逻辑，独立组件，挂载在场景 Canvas 上。\n\n- **按钮 + 面板模式**：点击 `volumeButton` 打开/关闭 `volumePanel`，所有按钮交互自动播放 `dianji.mp3` 音效。\n- **滑条与勾选框**：`musicVolumeSlider`（0~1）→ `MusicManager.SetMusicVolume()`，`sfxVolumeSlider`（0~1）→ `MusicManager.SetSFXVolume()`，`specialMusicToggle` → `MusicManager.SetSpecialGameOverMusicEnabled()`。均在拖动/切换时实时生效并持久化。\n- **启动同步**：`Start()` 中调用 `LoadSettingsToUI()` 从 MusicManager 读取当前值并同步到 UI 控件（使用 `SetValueWithoutNotify` 避免触发回调循环）。\n- **外部控制**：提供 `SetVolumeButtonVisible(bool)`（暂停时隐藏按钮）、`OpenPanel()`/`ClosePanel()`/`TogglePanel()` 公开方法。\n\n**PauseMenuManager 中的音量按钮**：新增 `volumeButton`（GameObject）字段，在 `Update()` 中与 `pauseButton` 同步显隐，在 `PauseGame()`/`ResumeGame()` 中同步隐藏/显示。

**ButtonClickSound（`Assets/Scripts/Common/ButtonClickSound.cs`）**：通用按钮点击音效组件。挂载到任意含 Button 组件的 GameObject 上即可自动在 Start() 时将 `PlayClickSound()` 注册到 Button.onClick，播放 `dianji.mp3` 音效。使用方式：

1. 选中任意 Button GameObject → Add Component → 搜索 `ButtonClickSound` 添加
2. 保持 `autoRegister = true`（默认），无需额外配置
3. 若 Button 在其他 GameObject 上，可将引用拖入 `targetButton` 字段
4. 也支持将 `PlayClickSound()` 手动绑定到其他 UnityEvent\n\n**AudioMixer 创建步骤**（需在 Unity Editor 中手动操作）：\n1. 右键 `Assets/` → `Create` → `Audio Mixer`，命名为 `MusicMixer`\n2. 双击打开 Audio Mixer 窗口\n3. 在 Groups 面板点击 `+` 创建两个子 Group：**Music** 和 **SFX**\n4. 选中 Music Group → Inspector 中右键 Volume 标签 → `Expose 'Volume (of Music)' to script` → 参数名改为 `MusicVolume`\n5. 选中 SFX Group → 同上 → 参数名改为 `SFXVolume`\n6. 将 `MusicMixer.mixer` 拖入 MusicManager 预制体的 `audioMixer` 字段\n\n**MusicManager 预制体创建步骤**（用于确保音频片段在 Inspector 中正确配置）：\n1. 在 MainMenu 场景中创建空 GameObject，命名为 `MusicManager`\n2. 挂载 `MusicManager` 脚本\n3. 在 Inspector 中将四个音频片段（mainMenuMusic、gameMusic、gameOverSpecialMusic、buttonSfx）从 `Assets/Music/` 拖入对应字段\n4. 将 MusicMixer 拖入 `audioMixer` 字段\n5. 在"启动与淡入淡出"分组中可调整 `startupDelay`（游戏启动后等几秒才播音乐，默认 1 秒）\n6. 将此 GameObject 拖入 `Assets/Resources/` 文件夹生成 `.prefab`（也可保留在场景中，Awake 中的 DontDestroyOnLoad 确保跨场景存活）\n\n**快速启动检查清单**：\n- [ ] `Assets/Music/` 中存在四个音频文件（三首 m4a + 一个 mp3）\n- [ ] 已创建 `MusicMixer` 并配置 Music/SFX Group 和暴露参数（参数名必须为 `MusicVolume` 和 `SFXVolume`）\n- [ ] MusicManager 的 `startupDelay` 和 `crossfadeDuration` 已按需配置\n- [ ] MainMenu 场景的 Canvas 上已添加 `MusicVolumePanel` 组件并配置按钮、滑条、勾选框引用\n- [ ] Game 场景的 Canvas 上已添加 `MusicVolumePanel` 组件并配置引用\n- [ ] PauseMenuManager 的 `volumeButton` 字段已拖入音量按钮 GameObject\n- [ ] 所有重要按钮（暂停面板、GameStartZoneUI、MusicVolumePanel 等）均已添加 `ButtonClickSound` 组件\n- [ ] EventSystem 已使用 `InputSystemUIInputModule`（否则 UI 按钮不响应）
