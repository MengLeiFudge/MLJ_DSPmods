# Logic/Civilization — 生命工业研究与探索域

本域拥有随机内容发现、永久探索信号、确定性成长科技、首遗物恢复和本地完成标记。

## Structure

```
Civilization/
├── CivilizationManager.cs       # CivilizationModule 聚合、tick、提示队列、文明子块
├── ResearchManager.cs           # 具体内容探索映射、物品共享槽、解析和献祭状态
├── ExplorationManager.cs        # 首遗物、具体内容/资源信号、额度、抵达和奖励
├── GrowthTechnologyManager.cs   # 第一页物流科技、第二页等级链与运行效果
├── TechnologyTreePatch.cs       # FE 科技整行显现与 2061/2062 第一页分类
├── Recovery.cs                  # 亚空间通信、基础塔配方和首遗物启动补给
├── FragDropPatch.cs             # 地面/太空黑雾 Frag 与通用原胚掉落
├── PermanentProfileStore.cs     # HasPlayedFractionation3 本地跨存档标记
└── Tutorial.cs                  # 五篇当前教程与兼容布局解析
```

## State Contracts

- `ResearchManager` 是具体内容到承载物品的探索映射、已解析稳定内容、物品共享进度、高级塔取得/库存门槛、献祭窗口和当前存档堆叠路线的唯一真源。
- `ExplorationManager` 是点位 ID、位置、内容、星球额度、定时器和首遗物认领状态的唯一真源。
- `GrowthTechnologyManager` 从原版 `GameHistoryData` 读取确定性科技完成状态，不另存科技等级。
- `PermanentProfileStore` 只保存“已经完整走过生命工业成长线”的本机标记；路线选择仍写入每个存档并由主机同步。
- `Civilization3` 块只含 `Research` 与 `Exploration`；不要复用旧文明块名或读取旧协议状态。

## Research Rules

- 目录由 `RecipeManager.WorldviewRecipes` 建立，内容身份使用 `StableId`。
- 一个内容可关联多个候选物品，但一次探索必须绑定唯一承载物品；不得把具体发现扩散到其他关联槽。
- 每个物品只有一个共享进度槽；候选只含绑定到该物品、已经探索且尚未解析的内容，槽满后才在这些候选中随机。
- 内容层级只限制探索生成节奏，不得在解析时重新替换为“最低未完成层”抽取。
- 基础解析塔调用 `TryAnalyzeDirection`；普通物品没有已探索候选时返回 false 并原样流出，高级塔有自身待解析特质时解析，其余情况转为献祭。
- Frag 分支在取得研究锁前转入探索，普通发现只记录待加速数和提示；锁外主线程再推进探索。
- 取得高级塔只记录取得状态；同类塔在亚空间首次达到 1000 座后只永久满足对应特质的探索门槛。三类塔分别按最近 60 秒实际献祭数量计算生产成功率加成。
- Memory 只承载普通科技方向；科技方向解析后整行显现，但科技等级继续由 `GameHistoryData` 结算。

## Exploration Rules

- 首遗物只在首次生产电磁矩阵的原版里程碑完成后生成；只有首遗物可自动导航，导航清理必须验证仍是 FE 设置的同一目标。
- 普通信号永久存在，只向 UI 暴露目标星球和现场强度；具体内容、承载物品和坐标都不提前显示。
- 定时器仅在成功生成点位后重置；可达星球无额度时保留到期状态。
- 研究发现只提前当前下一次信号；探索成长科技通过固定倍率改变新周期。
- 探索资源使用 `DataCenterInventory.AddExplorationReward`，这是唯一正式超容量写入路径。
- 点位按宇宙坐标球形范围认领；气态巨星点位放在外层空间，资源按星球类型从当地实际矿脉、流体或气体中加权选择，并固定保留 10% 原胚权重。
- Nebula 认领由主机使用连接对应玩家的 `UPosition` 验证。

## Prohibited Legacy Paths

不要新增或恢复 `Protocol`、`Achievement`、`AncientTechnology`、旧 `DarkFog` 调查、黑雾样本、远古科技点、正式领取奖励或协议购买流程。
