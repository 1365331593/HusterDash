# FogWall 预制体组装指南

---

## 前置条件

- Unity 2022.3 LTS，URP 14.x
- 已在 Unity Editor 中打开 HusterDash 项目
- 已创建 `Assets/Shaders/FogWall.shader` 和 `Assets/Scripts/MainMenu/FogWall.cs`

---

## 步骤 1：创建 FogWall 材质

1. 在 Project 窗口右键 → **Create > Material**
2. 命名为 `FogWall`
3. 选中材质，在 Inspector 中将 **Shader** 改为 `HusterDash/FogWall`
4. 检查材质预览：应该能看到灰白色的半透明效果

---

## 步骤 2：创建 FogWall 预制体

### 2.1 创建基础 GameObject

1. 在 Hierarchy 中右键 → **3D Object > Cube**
2. 命名为 `FogWall`
3. 设置 Transform：
   - **Position**：`(0, 2.5, 0)` — Y 坐标设为期望壁高的一半（5m 高 → Y=2.5），使底部对齐 y=0（地面）
   - **Rotation**：`(0, 0, 0)` — 保持默认
   - **Scale**：`(2, 5, 0.5)` — 宽 2m × 高 5m × 厚 0.5m，手动在 Inspector 中设置

   > **关键**：Transform 的 Scale 直接控制网格可见尺寸，BoxCollider 需手动匹配。

### 2.2 配置 BoxCollider

1. 选中 FogWall，找到 **BoxCollider** 组件
2. **Is Trigger**：取消勾选（实体阻挡）
3. **Center**：`(0, 0, 0)` — 与 Transform 原点一致
4. **Size**：`(2, 5, 0.5)` — 与 Transform Scale 保持一致

   > 碰撞体尺寸和中心由你手动调整，脚本不再自动覆盖。

### 2.3 挂载材质

1. 将步骤 1 创建的 `FogWall` 材质拖放到 Cube 的 Inspector 中
2. （替代方式：在 Cube 的 MeshRenderer 组件中，将 Materials 数组第 0 项设为 `FogWall`）

### 2.4 挂载 FogWall 脚本

1. 选中 `FogWall` GameObject
2. 在 Inspector 底部点击 **Add Component**
3. 搜索 `FogWall` 并添加

### 2.5 配置脚本参数

脚本只负责雾的视觉效果（材质参数），不控制碰撞体或 Transform：

| 参数 | 推荐值 | 说明 |
|------|--------|------|
| Fog Color | (0.85, 0.85, 0.85) | 灰白色雾 |
| Noise Scale | 3.0 | 噪声纹理缩放，越小雾团越大 |
| Density | **1.5** | 雾密度（0.5=淡，3.0=浓，5.0=完全遮蔽） |
| Scroll Speed X | 0.05 | 噪声水平漂移速度 |
| Scroll Speed Y | 0.02 | 噪声垂直漂移速度 |
| Top Fade | 0.6 | 顶部虚化起点（0=底，1=顶） |
| Edge Softness | 0.15 | 左右边缘柔和过渡，方便相邻雾墙拼接 |

---

## 步骤 3：保存为预制体

1. 在 Project 窗口进入 `Assets/Prefabs/` 目录（如不存在则创建）
2. 将 Hierarchy 中的 `FogWall` GameObject 拖入 Project 窗口
3. 预制体创建完成，可以删除 Hierarchy 中的实例

---

## 步骤 4：在 MainMenu 场景中摆放边界

1. 打开 `Assets/Scenes/MainMenu.unity`
2. 在 Hierarchy 中创建空容器：右键 → **Create Empty**，命名 `BoundaryWalls`
3. 将 `FogWall` 预制体拖入 `BoundaryWalls` 下
4. 沿地图矩形边界逐个放置：

   | 边界 | 摆放方式 |
   |------|---------|
   | 北边（+Z） | 一排沿 X 轴排列，Y=2.5 |
   | 南边（-Z） | 一排沿 X 轴排列，Y=2.5 |
   | 东边（+X） | 旋转 90°，沿 Z 轴排列，Y=2.5 |
   | 西边（-X） | 旋转 90°，沿 Z 轴排列，Y=2.5 |

### 摆放技巧

- 使用 Unity 的 **Vertex Snapping**（按住 V 键拖拽）对齐相邻雾墙块
- 每段雾墙默认宽度 2m，可在 Transform Scale 中调整，**同时调整 BoxCollider Size 匹配**
- 矩形地图的四个角落可交错放置或留空
- 选中所有雾墙后在 Inspector 中统一调整颜色/密度等视觉参数
- **放置后检查**：选中雾墙，确认 Gizmo 线框与可见网格完全重叠，确认 BoxCollider 尺寸与 Transform Scale 一致

---

## 步骤 5：运行测试

1. 点击 Play 进入运行模式
2. 向边界行走，确认角色被阻挡
3. 转动相机望向边界外，确认被浓雾完全遮蔽
4. 观察雾墙动画效果（噪声缓缓漂移）
5. Density 默认 1.5，如需"伸手不见五指"可调到 3.0~5.0

---

## 常见问题

**Q: 雾墙在游戏视图中看不见？**
- 检查 URP Renderer 资产：确保 Transparent 渲染队列未被禁用
- 检查材质的 Shader 是否正确设为 `HusterDash/FogWall`

**Q: 角色穿过了雾墙？**
- 确认 BoxCollider 的 **Is Trigger** 未勾选
- 确认 BoxCollider 的 **Size** 与 Transform Scale 匹配
- 确认玩家有 CharacterController 或合适的碰撞体

**Q: 雾墙太透明？**
- 调高 `Density` 参数（最大 5.0，此时整面接近完全不透明）
- 减小 `Noise Scale`（让雾团更大更密，覆盖更多面积）

**Q: 雾墙顶部边缘太锋利？**
- 增大 `Top Fade` 参数（如 0.8），让虚化区域更舒缓

**Q: 场景视图中 Gizmo 线框和网格不重叠？**
- 确认 BoxCollider 的 Size 与 Transform Scale 一致
- 确认 BoxCollider 的 Center 设为 (0, 0, 0)
