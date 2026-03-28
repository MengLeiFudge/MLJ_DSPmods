# Logic/Manager — 状态管理层

11 个管理文件，约 7150 行。重点变化：抽奖系统已从 UI 内联逻辑抽离为 `GachaManager + GachaService + GachaPool (+ GalleryBonus)`。

## 文件职责（当前）

| File | Lines | Responsibility |
|---|---:|---|
| `StationManager.cs` | 2399 | 物流交互站主循环与物品流转 |
| `ProcessManager.cs` | 949 | 分馏器热路径更新/补丁 |
| `BuildingManager.cs` | 723 | 建筑注册与等级数据 |
| `ItemManager.cs` | 715 | 物品价值与数据中心物品操作 |
| `TutorialManager.cs` | 601 | 任务/教程推进 |
| `TechManager.cs` | 568 | 科技注册、解锁联动、配方奖励发放、原版增强开放控制 |
| `GachaService.cs` | 534 | 卡池构建、抽卡结算、奖励发放 |
| `RecipeManager.cs` | 294 | 配方索引/查找 |
| `GachaManager.cs` | 213 | 保底计数、池积分、UP 轮换、抽卡状态持久化 |
| `GachaPool.cs` | 117 | 卡池与稀有度模型定义 |
| `GachaGalleryBonusManager.cs` | 36 | 图鉴完成度加成缓存刷新 |

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
