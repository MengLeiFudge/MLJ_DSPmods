# Logic/Civilization — 生命工业研究与探索域

本域拥有 FE 3.0 的随机内容发现、永久探索信号、确定性成长科技、首遗物恢复和本地完成标记。

## Structure

```
Civilization/
├── CivilizationManager.cs       # CivilizationModule 聚合、tick、提示队列、文明子块
├── ResearchManager.cs           # 解析资格、多对多方向池、进度、解锁和代际状态
├── ExplorationManager.cs        # 首遗物、定时/Frag 信号、额度、抵达和奖励
├── GrowthTechnologyManager.cs   # 第二页成长科技与运行投影
├── Recovery.cs                  # 通信根、基础塔配方和首遗物启动补给
├── FragDropPatch.cs             # 地面/太空黑雾 Frag 与通用原胚掉落
├── PermanentProfileStore.cs     # HasPlayedFractionation3 本地跨存档标记
└── Tutorial.cs                  # 五篇 3.0 教程与兼容布局解析
```

## State Contracts

- `ResearchManager` 是资格、已发现稳定内容、方向进度、高级塔取得状态和当前存档堆叠路线的唯一真源。
- `ExplorationManager` 是点位 ID、位置、内容、星球额度、定时器和首遗物认领状态的唯一真源。
- `GrowthTechnologyManager` 从原版 `GameHistoryData` 读取确定性科技完成状态，不另存科技等级。
- `PermanentProfileStore` 只保存“真正完成过 3.0”的本机标记；路线选择仍写入每个存档并由主机同步。
- `Civilization3` 块只含 `Research` 与 `Exploration`；不要复用旧文明块名或读取旧协议状态。

## Research Rules

- 目录由 `RecipeManager.WorldviewRecipes` 建立，内容身份使用 `StableId`。
- 一个方向可含多个内容，一个内容可属于多个方向；解锁后从所有方向去重。
- 只从方向当前最低未完成层选择，避免高级内容穿层。
- 基础解析塔调用 `TryAnalyzeDirection`；无资格/池空返回 false 并由分馏域原样流出。
- Frag 分支在取得研究锁前转入探索，普通发现只记录待加速数和提示；锁外主线程再推进探索。
- 取得高级塔时授予对应塔方向资格；塔特质未发现前，相关成长科技不得入队。

## Exploration Rules

- 只有首遗物可自动导航；导航清理必须验证仍是 FE 设置的同一目标。
- 普通信号永久存在，只向 UI 暴露目标星球和现场强度；内容和坐标不提前显示。
- 定时器仅在成功生成点位后重置；可达星球无额度时保留到期状态。
- 研究发现只提前当前下一次信号；探索成长科技通过固定倍率改变新周期。
- 探索资源使用 `DataCenterInventory.AddExplorationReward`，这是唯一正式超容量写入路径。
- Nebula 认领由主机使用连接对应玩家的权威位置验证。

## Prohibited Legacy Paths

不要新增或恢复 `Protocol`、`Achievement`、`AncientTechnology`、旧 `DarkFog` 调查、黑雾样本、远古科技点、正式领取奖励或协议购买流程。
