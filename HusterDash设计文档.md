# HusterDash 游戏设计文档

## 1. 游戏概述

### 1.1 核心概念

HusterDash 是一款**无尽跑酷**游戏。玩家操控角色在一条无限延伸的道路上向前奔跑，躲避从左右两侧交替驶来的电动车。游戏以反应速度、观察车流规律和快速决策为核心挑战，每次成功穿越车流间隙都带来紧张刺激的成就感。

### 1.2 目标平台

- **PC（Windows）**，基础分辨率 1920×1080，支持窗口/全屏切换。
- 构建输出：Windows 独立版（.exe）。

### 1.3 核心体验

紧张、快节奏的见缝插针式生存体验。玩家需要冷静观察车流规律，在间隙中果断移动，尽可能前进更远距离。最终以"竞速"心态不断挑战自己的历史最佳成绩。

---

## 2. 核心玩法机制

### 2.1 基础循环

```
观察车流 → 移动角色 → 躲避车辆 → 继续前进 → 重复循环，直到被撞游戏结束
```

### 2.2 玩家操作（Game 场景）

| 操作 | 按键 | 说明 |
|------|------|------|
| 前后左右移动 | WASD | 平滑连续移动，非网格跳跃 |
| 切换行走/奔跑 | 鼠标右键（点击） | 行走速度 3 m/s，奔跑速度 6 m/s |
| 暂停/恢复 | Esc | 弹出暂停菜单 |
| 临时解锁鼠标 | 左 Alt（按住） | 按住期间释放光标，松开后重新锁定 |

### 2.3 失败条件

角色与任何车辆发生**碰撞** → 游戏立即结束（`OnTriggerEnter` 检测 Vehicle 层）。

> **注意**：角色移动范围通过 `minX`/`maxX`/`minZ` 边界钳制，无法移出道路边界，因此不存在"掉落出界"失败情况。被钳制后仅该轴停止移动，另一轴仍可继续滑动沿边界移动。

### 2.4 成绩系统

成绩以**里程（米）**计算，从起点 Z=1 开始记录玩家前进距离：
- `DistanceTracker` 全程追踪当前里程，实时显示在 HUD 左上角。
- 使用 `PlayerPrefs`（键名 `"BestDistance"`）持久化历史最佳记录。
- 支持 `onlyIncrease` 模式（默认开启），防止后退导致里程减少。
- 游戏结束时自动判断是否刷新记录并在结算面板展示。若刷新则高亮提示"新纪录！"。

### 2.5 游戏流程

```
Splash（开屏动画）
    ↓ 自动过渡
MainMenu（3D 自由探索 / 触发区域交互）
    ↓ F 键选择"开始游戏"或"新手教学"
Game（核心玩法）
    ├→ 游戏结束 → 结算面板 → 重新开始 / 回主菜单
    └→ 暂停菜单 → 继续游戏 / 回主菜单 / 清空历史成绩
```

---

## 3. 场景架构

### 3.1 Splash — 开屏动画场景

**场景索引**：Build Settings 第 0 位（首发场景）。

**核心脚本**：`SplashController`（`Assets/Scripts/Splash/SplashController.cs`）

场景极其轻量，仅包含三个顶层对象：Main Camera、Directional Light 和挂载 `SplashController` 的 GameObject。除 Splash 自己的 Canvas UI 外不渲染任何 3D 内容（背景由 Canvas 的纯色 Image 遮挡）。

#### 动画流程

1. **Awake() 中动态创建 UI**：Canvas（ScreenSpaceOverlay，`sortingOrder=0`）、全屏纯色背景 Image（#509AE7 蓝色）和文字容器。
2. **字符布局捕获**：为 "Huster Dash" 共 11 个字符（含 1 个空格）各自创建独立 TMP_Text。先用 `HorizontalLayoutGroup` 自动排列后捕获目标位置（`targetPositions`），计算居中偏移，然后**销毁布局组**使字符可独立移动。
3. **Start() 启动 PlaySplashSequence() 协程**：
   - **逐字飞入**（EaseOutBounce 缓动）
   - **保持静止**（`holdDuration`，默认 1s）
   - **仅文字淡出**（背景保持不透明）——文字通过独立 `CanvasGroup` 淡出，背景 Image 保持纯色，确保 MainMenu 场景异步加载期间画面不会闪现空场景内容
   - 调用 `SceneTransitionManager.LaunchTransition("MainMenu")` 过渡到主菜单
4. **飞入方向智能分配**：将 360° 分为 N 个扇区（N=非空格字符数），每个扇区内随机取一个角度，然后 Fisher-Yates 洗牌确保视觉上方向均匀分散。飞入距离自动计算确保所有字符从屏幕外出发。
5. 使用 `Time.unscaledDeltaTime` 和 `WaitForSecondsRealtime`，动画不受 timeScale 影响。

#### 可配置参数（Inspector 中调整）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `flyInDuration` | float | — | 字符飞入动画时长 |
| `holdDuration` | float | 1.0 | 飞入后静止保持时长 |
| `fadeOutDuration` | float | — | 文字淡出时长 |
| `textFont` | TMP_FontAsset | — | 文字字体 |
| `fontSize` | float | — | 字号大小 |
| `characterSpacing` | float | — | 字符间距 |
| `textColor` | Color | — | 文字颜色 |
| `backgroundColor` | Color | #509AE7 | 背景遮罩颜色 |

#### 场景构成与 URP 管线初始化

- **Main Camera**：附有 `UniversalAdditionalCameraData` 组件。`m_ClearFlags: 1`（纯色清除，黑色背景，实际被 Canvas 背景遮挡），`m_RenderPostProcessing: 0`（Splash 无需后处理），`m_RendererIndex: -1`（使用默认 URP 渲染器）。
- **Directional Light**：温暖色调方向光（`#FFF6D6`），强度 1，开启软阴影（`m_Type: 2`），阴影强度 1。这是确保 URP 管线在首个场景正确初始化光源数据的关键——没有方向光会导致 URP 主光源阴影/光照系统以未定义状态启动。

> **关键注意事项**：Splash 作为 URP 管线的首发场景，其 `RenderSettings` 中的 `m_DefaultReflectionMode`（均为 0：Skybox）和 `m_AmbientMode`（均为 0：Skybox）必须与 MainMenu/Game 保持一致，否则 URP 的环境光球谐探针和反射数据不会在场景切换时自动重新计算，导致 MainMenu/Game 渲染偏暗。

---

### 3.2 MainMenu — 主菜单场景

**场景索引**：Build Settings 第 1 位。

**场景定位**：第三人称自由探索场景，玩家可在 3D 环境中自由行走、旋转视角，用于展示或调试角色。

#### 3.2.1 玩家控制（三层架构）

玩家的所有脚本挂载在同一个 GameObject 上，职责分离为三层：

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
- **重力系统**：每帧通过 `characterController.isGrounded` 检测地面状态，在地面时施加微小下压力（`groundedForce`，默认 -2）防止浮空，在空中时累加 `gravity`（默认 -9.81 m/s²）实现自然下落。即使无输入时也会调用 Move 以保持贴地或下落。
- **斜坡行走**：依赖 CharacterController 内置的 Slope Limit（默认 45°），`moveDirection` 始终保持在 XZ 平面，CharacterController.Move() 自动将水平移动投影到坡面上，移动速度保持恒定不随坡度变化。
- **台阶跨越**：依赖 CharacterController 内置的 Step Offset（默认 0.5m），自动跨过低于此高度的台阶，无需玩家额外操作。
- `UpdateAnimation()` 驱动 Animator 参数：Speed 值为 0（待机）/1（行走）/2（奔跑），用于 Blend Tree 混合。

**PlayerAnimatorController**：封装 Animator 参数设置，提供 `SetSpeed(float)` 和 `SetIsMoving(bool)` 统一接口。

#### 3.2.2 相机控制

**核心脚本**：`ThirdPersonCameraController`（`Assets/Scripts/MainMenu/ThirdPersonCameraController.cs`）

使用 **Cinemachine FreeLook** 虚拟相机：
- 启动时自动锁定光标（`CursorLockMode.Locked`）。
- 鼠标移动旋转视角。
- Alt 键按下时临时解锁光标并将灵敏度降为 0；释放 Alt 后恢复光标锁定和正常灵敏度。
- 滚轮缩放通过 `HandleZoom()` 调整 Middle 轨道半径，同时按比例同步 Top/Bottom 轨道，缩放范围在 `minDistance` ~ `maxDistance` 之间。
- `ClampCameraDistance()` 作为安全兜底每帧强制钳制。
- 提供 `SetZoomEnabled(bool)` 公开方法，供 `GameStartZone` 在弹出 UI 面板时临时禁用滚轮缩放，防止玩家在菜单中滚轮时同时触发相机缩放和选项切换冲突。

#### 3.2.3 触发区域交互系统

由四个组件协作完成"走近触发区域 → 弹出选项面板 → 选择并开始游戏/查看教程"的交互流程：

| 组件 | 脚本路径 | 职责 |
|------|----------|------|
| `GameStartZone` | `Assets/Scripts/MainMenu/GameStartZone.cs` | 触发区域核心控制器，依赖 `BoxCollider`（IsTrigger=true） |
| `GameStartZoneUI` | `Assets/Scripts/MainMenu/GameStartZoneUI.cs` | 选项面板 UI 逻辑，管理"开始游戏"/"新手教学"两个选项 |
| `FloatingPromptText` | `Assets/Scripts/MainMenu/FloatingPromptText.cs` | 未进入区域时的浮动提示文字（Billboard 朝向相机） |
| `GlowingBorder` | `Assets/Shaders/GlowingBorder.shader` | 触发区域的脉冲发光边框，匹配 BoxCollider 尺寸 |

**GameStartZone 详细职责**：
- **进入/离开检测**：`OnTriggerEnter/Exit` 中过滤 `Player` 标签，进入时依次调用 `floatingText.Hide()` → `uiPanel.Show()` → `thirdPersonCamera.SetZoomEnabled(false)`；离开时反向恢复。
- **滚轮切换**：每帧通过 `Mouse.current.scroll.y`（新版 Input System）读取滚轮增量，累积超过 `ScrollThreshold`（0.1）后调用 `uiPanel.SelectPrevious()/SelectNext()`。
- **按键确认**：使用新版 Input System 的 `Keyboard.current[interactKey].wasPressedThisFrame` 检测按键（**禁止使用旧版 `Input.GetKeyDown()`**），按下后调用 `uiPanel.ConfirmSelection()`。
- **边框同步**：`Start()` 中自动将子物体 `borderQuad` 的 scale 和位置匹配 `BoxCollider` 尺寸，平铺在 XZ 平面上。
- **诊断日志**：进入区域时一次性输出当前状态，方便排查配置问题。

**GameStartZoneUI 详细职责**：
- **Show()/Hide()**：控制面板可见性。`Show()` 中强制调用 `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate()` 重建布局，确保 VerticalLayoutGroup 完成排列后再定位 F 图标和三角指示器——这是解决滚轮切换后位置跳变的关键。
- **选项切换**：`SelectPrevious()/SelectNext()` 循环切换 `currentIndex`（0↔1）。
- **RefreshSelection()**：使用选项背景 **Image** 的 `RectTransform`（而非 TMP_Text）作为定位基准，将世界坐标转为 `panelRoot` 本地坐标后放置 F 图标和三角指示器。
- **ConfirmSelection()**：检查 `UnityEvent` 绑定状态后触发事件，未绑定时输出明确的红色错误日志指导修复。
- **UnityEvent 绑定**：`onStartGame` 和 `onTutorial` 两个 UnityEvent，Inspector 中绑定。`OnStartGameClicked()` 调用 `SceneTransitionManager.LaunchTransition("Game")`。

**GlowingBorder.shader**：URP 透明 Shader，在 Quad 上绘制脉冲发光的矩形边框：
- 在 UV 空间中计算像素到矩形边缘的距离，通过 `_BorderWidth` 和 `_EdgeSoftness` 实现柔和渐变边框。
- 叠加 `_PulseSpeed` 和 `_PulseAmount` 通过 `sin(_Time.y)` 驱动脉冲动画。
- `_Color`（默认 #509AE7）和 `_EmissionStrength` 控制发光颜色和强度。
- `Blend SrcAlpha OneMinusSrcAlpha`、`ZWrite Off`、`Cull Off` 保证透明叠加。

**预制体结构**：
- **TriggerZone.prefab**：根节点挂 `GameStartZone` + `BoxCollider`（IsTrigger=true），子节点包含 `FloatingPromptText`（TMP_Text 悬浮提示，默认文字"进入此区域以开始游戏"）和 `BorderQuad`（Quad Mesh + GlowingBorder 材质）。
- **GameStartPanel.prefab**：Canvas 子物体，挂载 `GameStartZoneUI`，内部包含 F 键图标 Image、选项容器（VerticalLayoutGroup + 两个选项背景 Image 及其 TMP_Text 子物体）、三角指示器 RectTransform。

#### 3.2.4 新手教学面板系统

玩家通过触发区域选择"新手教学"选项后，弹出全屏遮罩教程面板，分页阅读游戏操作和规则，支持 Esc 键跳过。

| 组件 | 脚本路径 | 职责 |
|------|----------|------|
| `TutorialPanelManager` | `Assets/Scripts/MainMenu/TutorialPanelManager.cs` | 教程面板核心控制器，静态 `IsShowing` 属性供外部查询 |
| `TutorialPageData` | `Assets/Scripts/MainMenu/TutorialPageData.cs` | 教程页面数据结构（Serializable），每页含标题和内容文本 |

**TutorialPageData**：每个页面包含 `title`（标题）和 `content`（内容文本），均在 Inspector 中可编辑。使用 `[Tooltip]` 和 `[TextArea]` 特性增强 Inspector 编辑体验。

**TutorialPanelManager 核心特性**：

- **Show() 流程**：
  1. 若 `pages` 数组为空且 `useDefaultPagesIfEmpty=true`，自动调用 `GetDefaultPages()` 生成 6 页硬编码教程内容（基本移动、视角控制、行走与奔跑、游戏规则、暂停与菜单、音量与设置）。
  2. 调用 `FreezePlayer()` 冻结玩家输入：保存 `PlayerMovement.enabled`/`PlayerInput.enabled`/`CinemachineFreeLook.m_XAxis.m_MaxSpeed`/`m_YAxis.m_MaxSpeed` 状态 → 禁用移动和 PlayerInput → 设相机灵敏度为 0 冻结视角。
  3. 解锁并显示光标（`CursorLockMode.None` + `Cursor.visible = true`）方便点击翻页按钮。
  4. 设置 `panelRoot.SetActive(true)`，翻到第 1 页并刷新 UI。

- **Hide() 流程**：`panelRoot.SetActive(false)` → `UnfreezePlayer()` 恢复之前保存的玩家输入和相机灵敏度状态 → 重新锁定并隐藏光标。

- **翻页逻辑**：`PrevPage()` 向前翻页（第 1 页时"上一页"按钮禁用）；`NextPage()` 向后翻页，最后一页点击变为关闭面板。

- **Esc 跳过**：`Update()` 中使用 `Keyboard.current.escapeKey.wasPressedThisFrame`（新版 Input System）检测 Esc 键，按下即关闭面板。

- **与 GameStartZone 的协作**：`GameStartZoneUI.OnTutorialClicked()` 通过 `FindObjectOfType<TutorialPanelManager>()` 查找管理器并调用 `Show()`。`GameStartZone.Update()` 和 `OnTriggerExit()` 检测 `TutorialPanelManager.IsShowing` 为 true 时跳过所有交互逻辑，防止教程面板打开期间误操作。

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

#### 3.2.5 退出游戏按钮

**核心脚本**：`QuitGameButton`（`Assets/Scripts/MainMenu/QuitGameButton.cs`）

极简退出按钮组件：
- **自动注册**：`Start()` 中自动查找同物体 `Button` 组件并注册 `onClick` 事件。
- **退出逻辑**：点击后 Editor 中调用 `EditorApplication.isPlaying = false` 停止播放模式，打包后调用 `Application.Quit()` 退出应用程序。
- **事件注销**：`OnDestroy()` 中自动注销 `onClick` 事件，防止内存泄漏。

#### 3.2.6 场景碰撞体要求

角色使用 CharacterController 进行碰撞检测，因此场景中：
- 所有可行走地面（马路、人行道、台阶、斜坡）必须添加 `MeshCollider`。
- 障碍物（楼房、汽车、路灯）建议添加 `BoxCollider` 或 `CapsuleCollider`。
- 无碰撞体的物体会被角色直接穿越。
- 玩家 GameObject 不应有 Rigidbody 重力——CharacterController 拥有独立的碰撞和移动系统，重力由 `PlayerMovement` 脚本自行管理。

> **CharacterController 关键参数**：`Slope Limit`（默认 45°）决定角色能行走的最大坡度，`Step Offset`（默认 0.5m）决定角色能自动跨过的台阶最大高度。

#### 3.2.7 场景构成

- 3D 地形/建筑/道路布景（`场景.fbx`、`人行道.fbx`、`红绿灯.fbx` 等）
- Cinemachine FreeLook 虚拟相机
- Polyverse Skies 天空系统（第三方插件）
- 触发区域交互对象（TriggerZone 预制体 + GameStartPanel 预制体）
- 教程面板（TutorialPanel）
- 音乐音量面板 Canvas UI
- 退出游戏按钮

---

### 3.3 Game — 核心玩法场景

**场景索引**：Build Settings 第 2 位。

**场景定位**：俯视固定视角的无尽跑酷玩法场景，采用"**道路无限生成 + 车辆对象池 + 俯视固定相机**"的设计模式。

---

## 4. 系统模块详解

### 4.1 道路生成系统

**核心脚本**：`RoadGenerator`（`Assets/Scripts/Game/RoadGenerator.cs`）+ `RoadLane`

`RoadGenerator` 挂载在场景根物体上，负责在玩家前方动态生成道路块、后方回收道路块。道路用 **Queue 结构**管理：`activeRoads`（已激活队列）和 `inactivePool`（回收对象池）。

- **初始化**：从 Z 坐标起点（`startOffset`）开始，向前铺设 `spawnDistance` 范围的道路。
- **动态生成**：每帧 Update 检查玩家前方是否需要新道路块（`lastSpawnZ < playerZ + spawnDistance`），循环生成直到覆盖范围。
- **后方回收**：当最旧道路块落后于玩家 `despawnDistance` 米时，Dequeue 并 Deactivate 回收。
- **车道方向**：在 `SpawnRoad()` 中按**每 2 米（2 个道路块）交替一次**的规律计算——根据道路块的世界坐标 Z 计算出 `groupIndex`，组号偶数时方向为 +X（正向），奇数时为 -X（负向）。
- **组件附加**：每个道路块生成时自动添加 `RoadLane`（存储方向向量）和 `BlockVehicleSpawner`（车辆生成器）组件。

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `roadPrefab` | GameObject | — | 道路块预制体 |
| `spawnDistance` | float | — | 前方生成距离（米） |
| `despawnDistance` | float | — | 后方回收距离（米） |
| `startOffset` | float | — | 初始生成偏移 |
| `blockLength` | float | 1.0 | 单个道路块长度（米） |
| `player` | Transform | — | 玩家 Transform 引用 |

**RoadLane**：极简数据组件，仅有一个 `public Vector3 direction` 字段，用于向 `BlockVehicleSpawner` 传递当前道路块的车道方向。

---

### 4.2 车辆系统

车辆系统由四个组件协作：BlockVehicleSpawner + VehicleMovement + VehiclePool + VehicleIdentifier。

#### 4.2.1 车辆生成器

**核心脚本**：`BlockVehicleSpawner`（`Assets/Scripts/Game/BlockVehicleSpawner.cs`）

- 挂载在每个道路块上，当道路块 `OnEnable` 时启动协程 `SpawnVehicles()`，以随机间隔生成车辆。
- 每个道路块在 `Awake()` 时随机一个**统一的行驶速度**（`minBlockSpeed` ~ `maxBlockSpeed`），该块内所有车辆共用此速度。
- 正向车道（+X）车辆从左端（`-roadHalfWidth`）生成，负向车道（-X）从右端（`+roadHalfWidth`）生成。
- 道路块 `OnDisable`（被回收）时，遍历所有生成的车辆并将其归还到 `VehiclePool`。

#### 4.2.2 车辆移动

**核心脚本**：`VehicleMovement`（`Assets/Scripts/Game/VehicleMovement.cs`）

- 每辆车独立移动，从起点沿车道方向直线行驶。
- 到达道路边界（`±roadHalfWidth`）或落后于玩家 `despawnZOffset` 米时，自动归还到对象池。
- 车辆回收是**双向的**：可被自身 `VehicleMovement` 回收，也可在道路块被回收时由 `BlockVehicleSpawner.OnDisable()` 强制回收。两种路径均有 `isReturned` 标记防重复回收。

#### 4.2.3 车辆对象池

**核心脚本**：`VehiclePool`（`Assets/Scripts/Game/VehiclePool.cs`）

- 全局单例，基于 Unity 内置 `ObjectPool<GameObject>`。
- 支持**三种电动车预制体**（粉红色、黄色、紫色），每种预制体维护独立对象池，默认容量 10，最大 30。
- `GetRandomVehicle(out originalPrefab)` 随机选择一种预制体并取出车辆。
- `ReturnVehicle()` 归还车辆时会附加 `VehicleIdentifier` 组件标记原始预制体引用。

#### 4.2.4 车辆标识

**核心脚本**：`VehicleIdentifier`（`Assets/Scripts/Game/VehicleIdentifier.cs`）

极简组件，仅保存 `originalPrefab` 引用，用于对象池归还时识别车辆属于哪个预制体的池。

---

### 4.3 玩家系统

**核心脚本**：`PlayerMove`（`Assets/Scripts/Game/PlayerMove.cs`）

通过 `[RequireComponent(typeof(PlayerInput))]` 依赖 PlayerInput 组件。

**移动机制**：
- 在 `Awake()` 中通过名称查找 Move 和 ToggleRun Action。
- 每帧 Update 读取 Move Action 的 `Vector2` 值，转换为世界空间方向（XZ 平面）。
- 鼠标右键点击（ToggleRun 的 performed 回调）在行走和奔跑之间切换：`walkSpeed`（3 m/s）/ `runSpeed`（6 m/s）。
- **移动范围限制**：每帧计算目标位置后通过 `Mathf.Clamp`（X 轴，`minX` ~ `maxX`，默认 -15 ~ +15）和 `Mathf.Max`（Z 轴，`minZ` 以上，默认 -2）钳制到固定边界内。碰到边界后被钳制轴停止，另一轴可继续移动，实现沿边界滑动效果。

**碰撞检测**：`OnTriggerEnter` 检测 Vehicle 层的碰撞 → 调用 `GameManager.Instance.GameOver()`。

**动画驱动**：通过 Animator 的 `Speed`（Float，控制 Idle/Walk 混合）、`isRunning`（Bool）和 `AnimSpeed`（Float，行走时二倍速播放）参数。

**可配置参数**：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `walkSpeed` | float | 3 | 行走速度（m/s） |
| `runSpeed` | float | 6 | 奔跑速度（m/s） |
| `minX` | float | -15 | X 轴移动下限 |
| `maxX` | float | +15 | X 轴移动上限 |
| `minZ` | float | -2 | Z 轴移动下限（防止后退过多） |
| `gameAnimSpeed` | float | 2 | 行走时动画播放倍数 |

---

### 4.4 相机系统

#### 4.4.1 俯视固定相机

**核心脚本**：`FixedTopDownCamera`（`Assets/Scripts/Game/FixedTopDownCamera.cs`）

- 固定俯视相机，每个 `LateUpdate` 将相机位置设为 `player.position + offset`，旋转设为固定角度（约 60° 俯视）。
- 相机不跟随玩家旋转，视角绝对固定。
- 开场过场期间被禁用，过场结束后启用。

#### 4.4.2 开场运镜

**核心脚本**：`IntroCameraController`（`Assets/Scripts/Game/IntroCameraController.cs`）

Game 场景启动后播放一段过场动画，三阶段状态机：

| 阶段 | 说明 |
|------|------|
| **PlayerWalking** | 玩家从起点后方（Z=-1.5）行走到原点，Animator 驱动行走动画 |
| **CameraRotating** | 相机从右侧面位置通过 Lerp/Slerp 平滑旋转到最终俯视角度 |
| **Finished** | 启用 `PlayerMove`、`FixedTopDownCamera`、`DistanceTracker`，销毁自身 |

过场期间 `PlayerMove`、`FixedTopDownCamera` 和 `DistanceTracker` 均被禁用，防止干扰。暂停菜单也在此期间不可用。

**可配置参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| `walkSpeed` | float | 过场行走速度 |
| `startCameraPosition` | Vector3 | 摄像机起始位置 |
| `finalOffset` | Vector3 | 最终偏移（需与 FixedTopDownCamera 一致） |
| `finalRotation` | Vector3 | 最终旋转角度 |
| `cameraRotateDuration` | float | 运镜时长 |

---

### 4.5 游戏状态管理

**核心脚本**：`GameManager`（`Assets/Scripts/Game/GameManager.cs`）+ `DistanceTracker`（`Assets/Scripts/Game/DistanceTracker.cs`）

#### GameManager

全局单例，管理游戏失败流程和光标状态。

**`GameOver()` 操作链**（被 `PlayerMove.OnTriggerEnter()` 调用）：
1. 设置 `Time.timeScale = 0` 冻结游戏。
2. 通过 `DistanceTracker` 获取当前里程和历史最佳里程。
3. 判断是否刷新记录，更新失败面板 TMPro 文本。
4. 显示失败面板（含重新开始和返回主菜单按钮）。
5. 解锁并显示光标（方便点击 UI 按钮）。
6. 禁用 `PlayerMove` 阻止继续移动。
7. 通知 `MusicManager` 播放特殊失败音乐。

**光标管理**：
- `Start()` 时锁定并隐藏光标（`CursorLockMode.Locked` + `Cursor.visible = false`）。
- 按住左 Alt 键（`Player/CursorUnlock` Action）时临时解锁光标，松开后重新锁定。
- 游戏结束（`GameOver()`）或暂停（`IsPaused`）时阻止 Alt 光标操作，防止暂停期间错误锁定光标。
- `cursorUnlockAction` 字段需在 Inspector 中引用 `Player/CursorUnlock` 动作。

**属性与方法**：

| 属性/方法 | 类型 | 说明 |
|-----------|------|------|
| `IsGameOver` | bool（只读） | 游戏是否已结束 |
| `IsPaused` | bool（可读写） | 是否暂停中（由 PauseMenuManager 设置） |
| `failPanel` | GameObject | 失败界面根物体 |
| `recordText` | TMP_Text | 成绩显示文本 |
| `BackToMenu()` | 方法 | 调用 `SceneTransitionManager.LaunchTransition("MainMenu")` |
| `RestartGame()` | 方法 | 调用 `SceneTransitionManager.LaunchTransition(当前场景名)` 重新加载 |

#### DistanceTracker

里程记录器，从起点 Z=1 开始记录玩家前进距离。

- 支持 `onlyIncrease` 模式（默认开启）防止后退导致里程减少。
- 使用 `PlayerPrefs` 持久化历史最佳记录（键名 `"BestDistance"`）。
- 提供 `GetCurrentDistance()`、`GetBestDistance()` 和 `ResetBestDistance()` 公开方法。
- `ResetBestDistance()` 将内存缓存的 `bestDistance` 重置为 0，供 `PauseMenuManager` 清空成绩后调用，确保 GameOver 面板立即反映清空状态（仅删 PlayerPrefs 不会更新内存缓存）。

---

### 4.6 暂停菜单系统

**核心脚本**：`PauseMenuManager`（`Assets/Scripts/Game/PauseMenuManager.cs`）

所有 UI 放在场景中初始隐藏。

**触发方式**：Esc 键（`Player/Pause` Action 的 `InputActionReference`）或点击左上角暂停图标按钮。

**暂停流程**：
1. `Time.timeScale = 0` 冻结游戏。
2. 设置 `GameManager.Instance.IsPaused = true` 阻止 Alt 光标干扰。
3. 隐藏暂停按钮、显示暂停面板。
4. 解锁光标、禁用 `PlayerMove`。

**恢复流程**：
1. 若确认弹窗显示则优先关闭弹窗。
2. `timeScale = 1` 恢复游戏。
3. 设置 `IsPaused = false`。
4. 显示暂停按钮、隐藏面板。
5. 锁定光标、启用 `PlayerMove`。

**面板按钮**：

| 按钮 | 行为 |
|------|------|
| 继续游戏 | 调用 `ResumeGame()` |
| 回主菜单 | 恢复 timeScale=1 后调用 `SceneTransitionManager.LaunchTransition("MainMenu")` |
| 清空历史成绩 | 弹出确认弹窗 → 确认后 `PlayerPrefs.DeleteKey("BestDistance")` + `DistanceTracker.ResetBestDistance()` 同步重置内存缓存 |

**暂停前置条件**（`CanPause`）：
- `GameManager.IsGameOver` 为 false（失败面板未显示）。
- `IntroCameraController` 不存在或已 disabled（过场已结束）。

**暂停按钮显隐**：`Update()` 中根据 `CanPause()` 动态控制——过场中或失败时自动隐藏。暂停期间（`isPaused`）跳过此逻辑，防止覆盖 `PauseGame()`/`ResumeGame()` 设置的状态。

> **注意**：`PauseMenuManager.Update()` 在 `timeScale=0` 时仍然执行（Unity 的 `Update()` 不受 `Time.timeScale` 影响，仅 `FixedUpdate()` 受影响）。暂停期间需用 `isPaused` 标记区分状态。

**Inspector 配置**：9 个引用字段——`pauseButton`（GameObject）、`pausePanel`（GameObject）、`continueButton`/`backToMenuButton`/`clearRecordButton`（Button 组件）、`confirmDialog`（GameObject）、`confirmClearButton`/`cancelClearButton`（Button 组件）、`pauseAction`（`InputActionReference`，拖入 Player/Pause Action）。`pauseAction` 字段必须配置，否则 Esc 键不生效。

---

### 4.7 场景过渡系统

**核心脚本**：`SceneTransitionManager`（`Assets/Scripts/Common/SceneTransitionManager.cs`）

全局单例跨场景过渡管理器，提供带淡入淡出的异步场景切换。

**核心特性**：
- **预制体驱动**：`LaunchTransition()` 从 `Resources/SceneTransitionManager.prefab` 加载预制体实例化，所有参数可在 Inspector 中可视化配置。若预制体缺失则降级为代码动态创建并使用默认值。
- **动态 UI**：运行时创建 Canvas（`ScreenSpaceOverlay`，`sortingOrder=9999`），包含 `#509AE7` 全屏遮罩 Image 和白色居中 "Loading..." 文字（TMP_Text）。
- **异步加载**：使用 `SceneManager.LoadSceneAsync` 加载目标场景，不阻塞主线程。
- **防重复**：`isTransitioning` 标记防止快速点击触发多次过渡。
- **使用 `Time.unscaledDeltaTime`**：过渡动画不受 `Time.timeScale` 影响，即使在暂停状态下淡入也能正常工作。
- **跨场景使用**：Game ↔ MainMenu 双向切换只需一行 `SceneTransitionManager.LaunchTransition("场景名")`。

**过渡流程**：
```
显示遮罩 → 0.25s 淡入 → 异步加载目标场景 → 0.25s 淡出 → 隐藏遮罩 → 销毁自身
```

**可配置参数**：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `transitionDuration` | float | 0.5 | 过渡动画总时长 |
| `loadingFont` | TMP_FontAsset | — | Loading 文字字体 |
| `loadingFontSize` | float | — | 字号 |
| `loadingText` | string | "Loading..." | 加载提示文字 |
| `overlayColor` | Color | #509AE7 | 遮罩颜色 |
| `canvasSortingOrder` | int | 9999 | UI 渲染层级 |

> **重要约束**：所有场景切换必须通过 `SceneTransitionManager.LaunchTransition()` 触发，不要在 GameManager 或其他地方直接调用 `SceneManager.LoadScene`。

---

### 4.8 音乐音效系统

#### 4.8.1 MusicManager

**核心脚本**：`MusicManager`（`Assets/Scripts/Common/MusicManager.cs`）

跨场景持久化音乐管理器，DontDestroyOnLoad 单例，永不销毁。

**双 AudioSource 交叉淡入淡出**：`sourceA` 和 `sourceB` 交替使用，`activeIndex` 在 0/1 之间切换，实现跨场景无缝音乐过渡。

**启动延迟与爆音防护**：
1. `Start()` 中通过 `StartCoroutine(DelayedStartPlayback())` 启动延迟协程。等待 `startupDelay` 秒（默认 1 秒，Inspector 中"启动与淡入淡出"分组可调）后才开始播放音乐。
2. `DelayedStartPlayback()` 中以 `sourceA.volume = musicVolume`（PlayerPrefs 保存值）直接启动，不依赖 AudioMixer 衰减——AudioMixer.SetFloat 是异步操作（发往音频线程），若 Mixer 参数尚未生效就开始输出音频，会导致启动爆音。
3. 播放后立即调用 `ApplyVolumeSettings()`，等待 0.2 秒确保音频线程处理完毕，再从 `musicVolume` 平滑过渡到 `MusicSourceBaseVolume`（1f），过渡期间感知音量不变（AudioSource 增大补偿 Mixer 衰减）。

**首次场景加载拦截**：`OnActiveSceneChanged()` 顶部有守卫 `if (string.IsNullOrEmpty(currentTrack)) return;`——Unity 的 `activeSceneChanged` 事件在场景首次加载完成后也会触发，若不加守卫则 `FullCrossfade` 会绕过 `DelayedStartPlayback` 协程直接播放音乐，导致启动延迟不生效且产生爆音。

**自动场景音乐切换**：监听 `SceneManager.activeSceneChanged` 事件，当场景名变化时执行 0.5s 淡出旧音乐 + 0.5s 淡入新音乐。当场景名不变（如 Game 场景重新开始）时音乐不中断。

**与 SceneTransitionManager 协作**：`SceneTransitionManager.DoTransition()` 在加载目标场景前调用 `MusicManager.Instance.OnSceneTransitionStart(sceneName)` 提前开始淡出旧音乐，使音频过渡与视觉遮罩淡入淡出同步。

**特殊失败音乐**：`GameManager.GameOver()` 调用 `MusicManager.Instance.OnGameOver()`：
- 若玩家启用了特殊失败音乐：保存当前 Game 音乐播放进度 → 淡出 Game 音乐并暂停 → 用闲置音源播放特殊失败音乐（循环关闭）→ 播放完毕后从中断处恢复 Game 音乐并淡入。

**timeScale=0 兼容**：推荐使用 `AudioMixer` 资产。AudioMixer 在独立音频线程运行，不受 `Time.timeScale` 影响。若未指定 `audioMixer` 字段，降级为直接控制 `AudioSource.volume`（但 timeScale=0 时音频会暂停）。

**音量持久化**：`SetMusicVolume()`/`SetSFXVolume()`/`SetSpecialGameOverMusicEnabled()` 均通过 `PlayerPrefs` 持久化，键名分别为 `MusicVolume`、`SFXVolume`、`EnableSpecialGameOverMusic`。启动时 `ApplyVolumeSettings()` 从 PlayerPrefs 加载写入 AudioMixer。

**ApplyVolumeSettings 参数验证**：写入 Mixer 前先用 `audioMixer.GetFloat()` 验证暴露参数是否存在（Unity 对不存在的参数静默忽略，不报错），存在才调用 `SetFloat`。不存在时输出明确的红色错误日志指导修复。

**自动绑定 AudioMixer Group**：`CreateAudioSources()` 中通过 `audioMixer.FindMatchingGroups("")` 按名称查找 Music 和 SFX Group 并绑定到对应 AudioSource 的 `outputAudioMixerGroup`。绑定成功/失败均输出日志便于诊断。

**音频资源配置**：

| 资源 | 文件名 | 用途 |
|------|--------|------|
| 主菜单 BGM | `60%的日常.ogg` | MainMenu 场景背景音乐 |
| 游戏 BGM | `下一篇章.ogg` | Game 场景背景音乐 |
| 特殊失败音乐 | `冬の花片段.ogg` | 游戏结束特殊音乐 |
| 按钮音效 | `dianji.ogg` | UI 按钮点击反馈 |

**公开方法**：
- `SetMusicVolume(float)` / `SetSFXVolume(float)` — 音量设置并持久化
- `GetMusicVolume()` / `GetSFXVolume()` — 音量获取
- `SetSpecialGameOverMusicEnabled(bool)` — 特殊失败音乐开关
- `PlayButtonSfx()` — 播放按钮音效
- `OnSceneTransitionStart(string sceneName)` — 场景过渡时提前淡出
- `OnGameOver()` — 播放失败音乐

#### 4.8.2 AudioMixer 配置

**AudioMixer 创建步骤**（需在 Unity Editor 中手动操作）：
1. 右键 `Assets/` → `Create` → `Audio Mixer`，命名为 `MusicMixer`
2. 双击打开 Audio Mixer 窗口
3. 在 Groups 面板点击 `+` 创建两个子 Group：**Music** 和 **SFX**
4. 选中 Music Group → Inspector 中右键 Volume 标签 → `Expose 'Volume (of Music)' to script` → 参数名改为 `MusicVolume`
5. 选中 SFX Group → 同上 → 参数名改为 `SFXVolume`
6. 将 `MusicMixer.mixer` 拖入 MusicManager 预制体的 `audioMixer` 字段

#### 4.8.3 MusicVolumePanel

**核心脚本**：`MusicVolumePanel`（`Assets/Scripts/Common/MusicVolumePanel.cs`）

音量设置面板 UI 逻辑，使用**按钮 + 面板**模式：点击 `volumeButton` 打开/关闭 `volumePanel`。

**功能**：
- `musicVolumeSlider`（0~1）→ `MusicManager.SetMusicVolume()`
- `sfxVolumeSlider`（0~1）→ `MusicManager.SetSFXVolume()`
- `specialMusicToggle` → `MusicManager.SetSpecialGameOverMusicEnabled()`
- 所有拖动/切换实时生效并持久化，启动时调用 `LoadSettingsToUI()` 从 MusicManager 同步当前值到 UI 控件（使用 `SetValueWithoutNotify` 避免触发回调循环）。
- 所有按钮交互自动播放按钮音效。

**外部控制**：提供 `SetVolumeButtonVisible(bool)`（暂停时隐藏按钮）、`OpenPanel()`/`ClosePanel()`/`TogglePanel()` 公开方法。PauseMenuManager 中有 `volumeButton` 字段与暂停按钮同步显隐。

#### 4.8.4 ButtonClickSound

**核心脚本**：`ButtonClickSound`（`Assets/Scripts/Common/ButtonClickSound.cs`）

通用按钮点击音效组件。挂载到任意含 Button 组件的 GameObject 上即可自动在 `Start()` 时将 `PlayClickSound()` 注册到 Button.onClick。

使用方式：选中任意 Button GameObject → Add Component → 搜索 `ButtonClickSound` 添加，保持 `autoRegister = true`（默认）即可，无需额外配置。

---

### 4.9 传送门特效系统

**核心脚本**：`PortalVFX`（`Assets/Scripts/Game/PortalVFX.cs`）+ `PortalVortex.shader`（`Assets/Shaders/PortalVortex.shader`）

**作用**：纯装饰性效果，嵌入道路块作为环境点缀，无实际玩法功能。视觉上表现为蓝白渐变（#509AE7 → #8D8DF0 → #FFFFFF）的圆形半透明能量漩涡，直径约 0.8 米，边缘有少量光点飘散，始终面朝相机。

**PortalVortex.shader（URP HLSL 自定义 Shader，程序化生成漩涡纹理，不依赖贴图）**：
- **Billboard**：顶点着色器以物体中心为基准在观察空间重建顶点，确保传送门始终面朝相机。
- **FBM 噪声**：片元着色器用极坐标 + 分形布朗运动生成多层漩涡扭曲效果，`_Time` 驱动持续旋转。
- **三色渐变**：半径方向内圈蓝 → 中圈淡紫 → 外圈白。
- **光环遮罩**：`smoothstep` 实现模糊环带（`_RingRadius`=0.38，`_RingThickness`=0.12），中心微光防止完全透明，外边缘柔和淡出。
- **圆形裁剪**：片元着色器中超出圆范围的片段通过 `discard` 剔除。
- **双 Pass**（Forward + DepthOnly），`ZWrite Off`、`Cull Off`、`Blend SrcAlpha OneMinusSrcAlpha`。

**关键 Shader 属性**：`_InnerColor`（#509AE7）、`_MidColor`（#8D8DF0）、`_OuterColor`（#FFFFFF）、`_VortexSpeed`（1.5）、`_DistortStrength`（0.25）、`_RingRadius`（0.38）、`_RingThickness`（0.12）、`_EdgeSoftness`（0.15）、`_CenterGlow`（0.08）。

**PortalVFX.cs 组件功能**：
- **上下浮动**：正弦函数驱动 `transform.localPosition.y`，幅度 ±0.2m，频率 0.8Hz，支持随机相位偏移使多个传送门不同步。
- **材质参数驱动**：每帧将 `_VortexSpeed`、`_DistortStrength`、`_CenterGlow` 写入材质实例。
- **对象池兼容**：使用 `baseLocalPosition`（本地坐标）而非世界坐标作为浮动基准——`RoadGenerator.SpawnRoad()` 中先 `SetActive(true)` 后设置世界位置，若使用世界坐标会在对象池复用时记录到旧位置。本地坐标相对父物体（道路块）不变，完全规避此问题。

**边缘粒子系统**：可选的 ParticleSystem 子物体，Circle Shape（Radius=0.38），每秒 10~20 个淡紫（#8D8DF0）到白色小光点，Radial 向外速度 0.15~0.35 m/s。

**Prefab 结构建议**：`Portal` 根节点挂 `PortalVFX`，子节点 `VortexQuad`（Quad Mesh + PortalVortex 材质，Scale 0.8×0.8×1），可选子节点 `EdgeParticles`（ParticleSystem）。传送门作为道路块 Prefab 的子物体嵌入道路生成系统。

---

## 5. 对象池设计模式

项目中实现了两种对象池，均遵循 **Get → Use → Release** 模式，避免频繁 Instantiate/Destroy 导致 GC 压力：

| 对象池 | 实现方式 | 说明 |
|--------|----------|------|
| 车辆对象池（VehiclePool） | Unity 内置 `ObjectPool<GameObject>` | 按预制体引用为键，每种预制体独立维护池（默认容量 10，最大 30） |
| 道路块对象池（RoadGenerator 内部） | 手动管理 `Queue<GameObject>` | `inactivePool` 存储已回收道路块，生成时优先从池中取用 |

---

## 6. 输入系统

**配置文件**：`Assets/PlayerControls.inputactions`

由 Unity 新版 Input System 1.14.2 驱动，所有动作集中在单一 Action Map `Player`：

| 动作名 | 类型 | 绑定 | 用途 |
|--------|------|------|------|
| Move | Vector2 | WASD | 角色移动 |
| Look | Vector2 | Mouse Delta | 第三人称视角旋转 |
| ToggleRun | Button | 鼠标右键 | 切换行走/奔跑 |
| CursorUnlock | Button | 左 Alt | 临时解锁鼠标光标 |
| Zoom | Axis | 鼠标滚轮 Y | 相机缩放 |
| Pause | Button | Esc | 切换暂停/恢复 |

**各场景输入用法差异**：
- Game 场景的 `PlayerMove` 直接按名称查找 Action。
- MainMenu 使用 `PlayerInput` 组件通过事件绑定分发输入给 `PlayerMovementInput`。
- `PauseMenuManager` 通过 `InputActionReference` 引用 `Player/Pause` Action 接收 Esc 回调。

> **重要约束**：
> - `PlayerControls` C# 类由 Unity 自动生成于 `Assets/PlayerControls.cs`，**禁止手动移入子目录**。修改 `.inputactions` 后会重新生成到此位置。如果存在旧副本（如 `Assets/Scripts/MainMenu/PlayerControls.cs`），必须删除，否则出现全类重复定义错误。
> - 场景中的 EventSystem GameObject 必须使用 `InputSystemUIInputModule`（替换默认的 `StandaloneInputModule`），否则 UGUI 事件系统会尝试调用旧版 `Input.GetButtonDown()` 导致 `InvalidOperationException`。
> - 触发区域按键检测必须使用新版 Input System 的 `Keyboard.current[key].wasPressedThisFrame`，**禁止**使用旧版 `Input.GetKeyDown()`——在纯 Input System Package 模式下旧版 API 静默失效。

---

## 7. 模型与资源清单

### 7.1 主要模型

| 模型文件 | 用途 |
|----------|------|
| `新人物.fbx` | 玩家角色模型（3D 卡通风格） |
| `粉红色电动车.fbx` | 粉色电动车（车辆对象池预制体 A） |
| `黄色电动车.fbx` | 黄色电动车（车辆对象池预制体 B） |
| `紫色电动车.fbx` | 紫色电动车（车辆对象池预制体 C） |
| `车道.fbx` | 道路块模型 |
| `人行道.fbx` | 人行道装饰模型 |
| `场景.fbx` | MainMenu 场景布景 |
| `1377 Car.obj` | 备用汽车模型（1377 面顶点） |
| `红绿灯.fbx` | 红绿灯装饰模型 |
| `base.fbx` | 基础模型 |

### 7.2 预制体

| 预制体 | 位置 | 用途 |
|--------|------|------|
| Road 预制体 | `Assets/Prefabs/` | 道路块（用于 RoadGenerator 的对象池） |
| Portal 预制体 | `Assets/Prefabs/` | 传送门装饰（VortexQuad + EdgeParticles） |
| TriggerZone 预制体 | `Assets/Prefabs/` | MainMenu 触发区域（GameStartZone + FloatingPromptText + BorderQuad） |
| GameStartPanel 预制体 | `Assets/Prefabs/` | MainMenu 选项面板（GameStartZoneUI） |
| TutorialPanel 预制体 | `Assets/Prefabs/` | MainMenu 教程面板（TutorialPanelManager） |
| SceneTransitionManager 预制体 | `Assets/Resources/` | 场景过渡管理器（存放于 Resources 目录用于动态加载） |

---

## 8. 渲染与画面

### 8.1 渲染管线

使用 **Universal Render Pipeline（URP）14.0.12**，三个渲染质量配置文件位于 `Assets/Settings/`：

| 配置文件 | 用途 |
|----------|------|
| URP-Balanced | 平衡画质 |
| URP-Performant | 性能优先 |
| URP-HighFidelity | 高画质 |

可在 `Project Settings > Graphics` 中切换。

### 8.2 关键视觉效果

- **俯视角 60° 固定相机**：清晰俯瞰大范围道路，便于观察车流。
- **车辆颜色区分**：粉红/黄/紫三种电动车，便于快速识别不同车辆。
- **玩家脚下投影**：3D 阴影辅助定位。
- **传送门装饰**：道路沿线分布蓝紫能量漩涡，增加视觉丰富度。
- **GlowingBorder 脉冲边框**：MainMenu 触发区域蓝色发光边框，sin 波脉冲动画。
- **Polyverse Skies 天空系统**：MainMenu 场景的程序化动态天空（`Assets/BOXOPHOBIC/` 第三方插件，含 Runtime 着色器、Scripts 脚本和 Editor 工具三个独立 .asmdef 程序集）。

### 8.3 美术风格

- **低多边形（Low Poly）+ 卡通渲染**：色彩鲜艳，对比度高。
- 主色调：深灰色路面 + 白色车道分隔线 + 多彩车辆。
- 光照：暖色平行光（#FFF6D6），方向光产生清晰投影辅助空间判断。
- 车辆碰撞箱缩小至视觉模型的 80%，给玩家一定容错空间。

---

## 9. 技术选型与依赖

### 9.1 技术选型

| 技术项 | 选型 | 版本 | 说明 |
|--------|------|------|------|
| 游戏引擎 | Unity LTS | 2022.3.62f3c1 | 长期支持版本 |
| 渲染管线 | Universal Render Pipeline | 14.0.12 | 平衡性能与画面 |
| 输入系统 | Unity Input System | 1.14.2 | 新版输入系统 |
| 相机系统 | Cinemachine | 2.10.7 | FreeLook 虚拟相机（MainMenu） |
| 文本渲染 | TextMeshPro | 3.0.7 | 高质量 UI 文本渲染 |
| 时间线 | Unity Timeline | 1.7.7 | 时间线/过场动画（当前未直接使用） |
| 测试框架 | Unity Test Framework | 1.1.33 | PlayMode 和 EditMode 测试 |
| 第三方插件 | Polyverse Skies | — | 动态天空系统（MainMenu） |

### 9.2 物理引擎

使用 Unity 内置物理：
- MainMenu：`CharacterController` 负责玩家碰撞和移动（不需 Rigidbody 重力）。
- Game：玩家使用 `Transform` 移动 + `OnTriggerEnter` 碰撞检测（Vehicle 层），车辆使用 `Rigidbody` 或 `Transform` 直线移动。

### 9.3 持久化存储

使用 `PlayerPrefs` 键值存储，涉及的键名：

| 键名 | 类型 | 用途 |
|------|------|------|
| `BestDistance` | float | 历史最佳里程 |
| `MusicVolume` | float | 音乐音量（0~1） |
| `SFXVolume` | float | 音效音量（0~1） |
| `EnableSpecialGameOverMusic` | int（0/1） | 特殊失败音乐开关 |

---

## 10. UI / UX 设计

### 10.1 Splash 开屏界面

- 全屏纯色蓝色背景（#509AE7），`sortingOrder=0`。
- "Huster Dash" 白色文字逐字飞入弹跳动画。
- 仅文字淡出，背景保持不透明，确保后续场景异步加载期间画面不闪烁。

### 10.2 MainMenu 主菜单界面

- 3D 自由探索场景，无传统 2D 主菜单按钮。
- 触发区域交互：走近蓝色光效区域 → 弹出选项面板 → 滚轮选择 → F 键确认。
- 新手教学：选择"新手教学"后弹出全屏遮罩分页教程，含 6 页操作说明，支持翻页或 Esc 跳过。
- 音乐音量面板：按钮点击打开/关闭面板，滑条调整音乐/音效音量，Toggle 开关特殊失败音乐。
- 退出游戏按钮。
- 所有按钮交互有 `dianji.ogg` 音效反馈。

### 10.3 Game HUD

- **正上方**：实时里程显示（如 "123.5 m"）。
- **左上角**：暂停按钮图标（Esc 键或点击触发）。
- **右上角**：音量按钮图标（点击打开音量面板）。
- HUD 元素在过场动画期间隐藏。

### 10.4 暂停菜单

- 半透明遮罩 + 居中面板。
- 三个按钮：继续游戏 / 回主菜单 / 清空历史成绩。
- 清空成绩有二次确认弹窗。
- 暂停与失败面板互斥——失败面板显示时不允许暂停。

### 10.5 结算界面（Game Over）

- 居中显示"游戏结束"。
- 显示本次成绩和历史最佳记录。
- 若刷新记录，高亮提示"新纪录！"。
- 两个按钮：重新开始 / 返回主菜单。

---

## 11. 操作说明

### 11.1 启动游戏

解压游戏压缩包后，双击 `HusterDash.exe` 文件即可启动。

### 11.2 按键操作

#### MainMenu 场景

| 操作 | 按键 | 说明 |
|------|------|------|
| 前后左右移动 | WASD | 相对于相机方向 |
| 旋转视角 | 鼠标移动 | Cinemachine FreeLook |
| 切换奔跑 | 鼠标右键（按住） | 行走/奔跑切换 |
| 相机缩放 | 鼠标滚轮 | 缩放距离有限制 |
| 临时解锁光标 | 左 Alt（按住） | 松开后锁定 |
| 确认交互 | F | 站在触发区域内时生效 |
| 关闭教程 | Esc | 教程面板打开时 |

#### Game 场景

| 操作 | 按键 | 说明 |
|------|------|------|
| 前后左右移动 | WASD | 在 XZ 平面移动 |
| 切换奔跑 | 鼠标右键（点击） | 3 m/s ↔ 6 m/s |
| 暂停/呼出菜单 | Esc | 过场和失败期间禁用 |
| 临时解锁光标 | 左 Alt（按住） | 暂停/失败期间禁用 |
| 退出游戏 | Alt + F4 | 系统快捷键 |

### 11.3 完整游戏流程

1. **启动**：双击 exe，播放 "Huster Dash" 开屏动画（逐字飞入弹跳）。
2. **主菜单**：动画结束后进入 3D 自由探索场景。可 WASD 行走、鼠标旋转视角、滚轮缩放、右键奔跑。
   - 走进蓝色触发区域 → 滚轮选择"开始游戏"或"新手教学" → F 键确认。
   - 右上角可打开音量设置面板。
3. **对战**：
   - 开场过场：玩家从后方走入画面 + 相机从侧面旋转到俯视角度。
   - 核心玩法：WASD 控制角色在无限公路上奔跑，躲避左右交替驶来的电动车。
   - 鼠标右键切换奔跑/行走速度。
   - 正上方实时显示前进里程。
4. **游戏结束**：被车辆撞击后立即冻结画面，弹出结算面板。
   - 显示本次里程和历史最佳，若刷新纪录则高亮"新纪录！"。
   - 可重新开始或返回主菜单，均带淡入淡出过渡效果。
5. **暂停**：游戏过程中按 Esc 暂停，可选继续游戏、回主菜单或清空历史成绩（含二次确认）。

---

## 12. 构建与部署

### 12.1 构建配置

- **构建顺序**（`EditorBuildSettings.asset`）：Splash（索引 0）→ MainMenu（索引 1）→ Game（索引 2）。
- Splash 作为首发场景确保 URP 管线在正确的渲染设置下初始化。
- 三个场景的 `RenderSettings` 中 `m_DefaultReflectionMode` 和 `m_AmbientMode` 必须保持一致（均为 0：Skybox）。

### 12.2 关键注意事项汇总

| 类别 | 注意事项 |
|------|----------|
| 场景切换 | 必须通过 `SceneTransitionManager.LaunchTransition()` 触发，禁止直接 `SceneManager.LoadScene` |
| 输入系统 | `PlayerControls.cs` 禁止手动移入子目录；EventSystem 必须用 `InputSystemUIInputModule`；触发区域按键检测必须用新版 Input System API |
| 车辆回收 | 双向回收路径（自回收 + 道路块回收），均有 `isReturned` 防重复标记 |
| 里程起点 | Z=1，`RoadGenerator` 的 `startOffset` 和 `SpawnRoad()` 的方向偏移均以此为基准 |
| 车道方向 | 每 2 米交替，硬编码在 `RoadGenerator.SpawnRoad()` 的 `blockIndex` 和 `groupIndex` 计算中 |
| 玩家移动范围 | X 轴 ±15（默认），Z 轴 -2 下限（默认），纯逻辑限制无视觉提示 |
| 传送门浮动 | 使用 `localPosition` 而非 `worldPosition`，兼容对象池先 SetActive 后设位置的流程 |
| 清空成绩 | 必须同时执行 `PlayerPrefs.DeleteKey("BestDistance")` 和 `DistanceTracker.ResetBestDistance()` |
| 暂停与失败互斥 | 失败面板显示时不允许暂停，过场未结束时不允许暂停 |
| 教程与触发区域互斥 | 教程面板打开时 GameStartZone 跳过所有交互逻辑 |
| 场景碰撞体 | MainMenu 可行走地面必须有 MeshCollider，玩家不需要 Rigidbody 重力 |
| URP 初始化 | Splash 场景必须有 Directional Light 和 UniversalAdditionalCameraData，确保光照管线正常启动 |
| PlayerPrefs 键名 | `BestDistance`、`MusicVolume`、`SFXVolume`、`EnableSpecialGameOverMusic` |

### 12.3 早期规划与实现对比

| 项目 | 早期规划 | 实际实现 |
|------|----------|----------|
| 移动方式 | 网格跳跃（每次一格） | 平滑连续移动 |
| 相机角度 | 固定 45° 等距斜视 | 60° 俯视固定视角 |
| 道路结构 | 固定 6 条车道（3+3） | 无限道路块，每 2 米交替方向 |
| 车辆种类 | 随机颜色多种车辆 | 3 种固定电动车（粉/黄/紫）+ 对象池 |
| 成绩单位 | 前进列数 | 里程（米） |
| 失败条件 | 碰撞 + 出界 | 仅碰撞（移动范围已钳制） |
| 开局流程 | 直接开始 | 过场动画（玩家进场 + 相机运镜） |
| 主菜单 | 传统 2D 菜单 | 3D 自由探索 + 触发区域交互 |
| 开屏动画 | 无 | Splash 场景逐字飞入弹跳 |
| 音乐系统 | 无 | 双音源淡入淡出 + AudioMixer + 失败特殊音乐 |
| 新手教学 | 无 | 分页教程面板（6 页） |
| 音量设置 | 无 | 滑条 + Toggle + PlayerPrefs 持久化 |
| 传送门装饰 | 无 | 蓝紫漩涡（程序化 Shader）+ 粒子特效 |
| 目标平台 | WebGL / 移动端 | PC（Windows） |

### 12.4 性能优化策略

- 道路块采用对象池复用（`Queue<GameObject>`），避免频繁 Instantiate/Destroy。
- 车辆使用 Unity `ObjectPool<GameObject>`，每种预制体独立管理（默认容量 10，最大 30）。
- 道路后方超出回收距离的道路块自动回收，对应车辆一并归还，确保同屏车辆数可控。
- 传送门 Shader 程序化生成漩涡纹理，不依赖贴图资源，减少内存占用。
