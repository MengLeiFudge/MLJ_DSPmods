# Logic/Manager — 状态管理层

核心管理层按领域组织；`BuildingManager`、`ProcessManager` 与 `StationManager` 已拆成 partial 文件，避免继续把新逻辑堆回单个大文件。

## 文件职责（当前）

| File | Responsibility |
|---|---|
| `StationManager.cs` | 物流交互站翻译入口与顶层说明 |
| `StationManager.Runtime.cs` | 交互站与数据中心的上传/下载同步、槽位目标数量、电力消耗 |
| `StationManager.UIShared.cs` | 传输/容量模式、UI 状态缓存、共享弹窗与集装 helper |
| `StationManager.StationWindow.cs` | 独立物流站窗口与 `UIStationStorage` patch |
| `StationManager.ControlPanel.cs` | 总控面板窗口、槽位、检查器 patch |
| `StationManager.OutputStackPatch.cs` | 物流站输出集装上限 transpiler |
| `StationManager.Save.cs` | 交互站传输/容量模式存档读写 |
| `BuildingManager.cs` | 建筑注册、材质/能耗刷新、分馏塔基础缓存、存档聚合入口 |
| `BuildingManager.OutputState.cs` | 分馏塔多产物输出拓展状态与运行缓存 |
| `BuildingManager.SingleLock.cs` | 转化塔单锁、复制粘贴/蓝图参数、实体删除清理 |
| `BuildingManager.Growth.cs` | 建筑等级、经验、突破消耗、等级派生属性 |
| `BuildingManager.Resonance.cs` | 交互塔维度共鸣加成状态 |
| `BuildingManager.FissionPool.cs` | 矿物复制塔质能裂变点数池 |
| `ProcessManager.cs` | 分馏器调度入口、各塔 `InternalUpdate` 热路径、成功统计存档 |
| `ProcessManager.Runtime.cs` | 传送带速度、缓存上限、运行配置和强化数组初始化 |
| `ProcessManager.Belts.cs` | 分馏产物选择、流动输入/输出、传送带 IO helper |
| `ProcessManager.Perf.cs` | 分馏热路径性能探针与日志格式化 |
| `ProcessManager.PowerPatch.cs` | 分馏塔能耗 Harmony transpiler 与 `SetPCState` 适配 |
| `ProcessManager.Sacrifice.cs` | 交互塔献祭特质与成功率加成刷新 |
| `ItemManager.cs` | 物品价值与数据中心物品操作 |
| `TutorialManager.cs` | 任务/教程推进 |
| `TechManager.cs` | 科技多语言注册入口 |
| `TechManager.Techs.cs` | 科技注册与科技树坐标 |
| `TechManager.MatrixProgress.cs` | 矩阵层研究进度、原版配方增强开放判断 |
| `TechManager.RecipeBaselines.cs` | 读档/科技解锁后的配方基线补齐 |
| `TechManager.RuntimePatches.cs` | 特殊科技运行解锁标记、科技提示文本 patch、解锁回调 |
| `GachaService.cs` | 卡池构建、抽卡结算、奖励发放 |
| `RecipeManager.cs` | 配方索引/查找 |
| `GachaManager.cs` | 保底计数、池积分、UP 轮换、抽卡状态持久化 |
| `GachaPool.cs` | 卡池与稀有度模型定义 |
| `GachaGalleryBonusManager.cs` | 图鉴完成度加成缓存刷新 |

## Partial 文件归属

- 新增建筑注册、原型刷新、存档聚合入口：放 `BuildingManager.cs`。
- 新增分馏塔实例级状态：优先放 `BuildingManager.OutputState.cs`，不要混进等级成长。
- 新增转化塔单锁、复制粘贴、蓝图参数：放 `BuildingManager.SingleLock.cs`。
- 新增建筑经验/等级/突破公式：放 `BuildingManager.Growth.cs`。
- 新增分馏热路径核心流程：放 `ProcessManager.cs`，保持 `InternalUpdate<T>` 可集中阅读。
- 新增传送带输入输出辅助：放 `ProcessManager.Belts.cs`。
- 新增性能计数或日志桶：放 `ProcessManager.Perf.cs`，不要散落在热路径里。
- 新增能耗 IL patch：放 `ProcessManager.PowerPatch.cs`。
- 新增交互塔献祭相关逻辑：放 `ProcessManager.Sacrifice.cs`。
- 新增交互站运行同步逻辑：放 `StationManager.Runtime.cs`。
- 新增交互站 UI 状态、弹窗状态或共享 helper：放 `StationManager.UIShared.cs`。
- 新增独立物流站窗口 patch：放 `StationManager.StationWindow.cs`。
- 新增总控面板 patch：放 `StationManager.ControlPanel.cs`。
- 新增物流站输出堆叠 IL patch：放 `StationManager.OutputStackPatch.cs`。
- 新增交互站传输/容量模式存档字段：放 `StationManager.Save.cs`。
- 新增科技注册、科技树位置：放 `TechManager.Techs.cs`。
- 新增矩阵层研究进度或原版配方增强开放判断：放 `TechManager.MatrixProgress.cs`。
- 新增科技解锁/读档后的配方基线补齐：放 `TechManager.RecipeBaselines.cs`。
- 新增特殊科技运行解锁、科技提示文本或解锁回调 patch：放 `TechManager.RuntimePatches.cs`。
- 新增科技多语言文本：保留在 `TechManager.cs`。

## 科技与配方解锁 (TechManager)

- **科技奖励**：在 `NotifyTechUnlock` 后自动发放建筑培养配方。
- **矿物复制奖励**：解锁“矿物复制”科技时，自动发放非珍奇的原矿复制配方。
- **原版增强开放**：采用“落后一层”规则（例如：完成能量层所有科技后，才开放电磁层的原版配方增强）。

## 抽奖域边界（重构后）

- `GachaPool`：池 ID 合同 + 概率池 + `PickRandom`
- `GachaManager`：状态机（保底、积分、UP 组轮换）+ Import/Export
- `GachaService`：纯业务流程（构池、Draw、奖励）
- UI 层（`TicketRaffle`/`LimitedTimeStore`）只负责展示与交互，不承载核心概率/保底规则

## 序列化约束

- 统一 `WriteBlocks/ReadBlocks` 标签化存档
- `GachaManager` 对导入值有显式归一化（非负、范围、组索引合法化）
- 兼容老档的迁移规则集中在 `GachaManager`，不要分散到 UI

## 反模式

- 在 `TicketRaffle` 里重写概率/保底逻辑（应进 `GachaService/GachaManager`）
- 跳过 `GachaPool.IsValidPoolId` 直接访问数组
- 在热路径里重复做高开销查询（遵循缓存/预构建思路）
