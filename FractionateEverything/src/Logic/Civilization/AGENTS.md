# Logic/Civilization — 生命工业研究与探索域

本域拥有随机内容发现、永久探索信号、确定性成长科技、首遗物恢复和本地完成标记。

## Structure

```
Civilization/
├── CivilizationManager.cs       # CivilizationModule 聚合、tick、提示队列、文明子块
├── ResearchManager.cs           # 具体内容探索映射、物品共享槽、解析和献祭状态
├── ExplorationManager.cs        # 两节点主线、未知广播、双探测器、额度、抵达和奖励
├── GrowthTechnologyManager.cs   # 第一页物流科技、第二页等级链与运行效果
├── TechnologyTreePatch.cs       # FE 科技显现、沙盒解锁兼容与 2061/2062 第一页分类
├── Recovery.cs                  # 亚空间通信、基础塔配方和首遗物启动补给
├── FragDropPatch.cs             # 地面/太空黑雾 Frag 与通用原胚掉落
├── PermanentProfileStore.cs     # HasPlayedFractionation3 本地跨存档标记
└── Tutorial.cs                  # 五篇当前教程与兼容布局解析
```

## State Contracts

- `ResearchManager` 是具体内容到承载物品的探索映射、已解析稳定内容、物品共享进度、高级塔取得/库存门槛、献祭窗口和当前存档堆叠路线的唯一真源。
- `ExplorationManager` 是点位 ID、位置、内容、星球额度、定时器、两个主线认领状态和星球探测器状态的唯一真源。
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

- 首次生产电磁矩阵只启用遗物探测器并在出生星生成关键信号；FE 不得启动导航、创建移动命令或改变机甲运动状态。兼容清理只能终止仍精确匹配旧首遗物的遗留导航。
- 首遗物回收且曲速推进可用后，在出生星系另一颗星球生成第二个关键信号；回收后恢复星球探测器，普通信号定时器和 Frag 生成路径才启动。
- 关键信号使用 `关键信号{ID}` 并公开目标星球；普通信号使用 `信号{ID}`，不得向 UI 暴露恒星系、星球、内容、承载物品、坐标、真实距离或方向。
- 星球探测器使用远场对数强度，进入目标星球后固定 100%；遗物探测器只在目标星球内使用近场对数强度。两者按同一成长等级量化。
- 普通信号永久存在；定时器仅在成功生成点位后重置，有额度候选为空时保留到期状态。
- 研究发现只提前当前下一次信号；探索成长科技改变新周期、Frag 掉率、探测精度，并在最终级开放玩家主动位置标注。
- 探索资源使用 `DataCenterInventory.AddExplorationReward`，这是唯一正式超容量写入路径。
- 点位按宇宙坐标球形范围认领；气态巨星点位放在外层空间，资源按星球类型从当地实际矿脉、流体或气体中加权选择，并固定保留 10% 原胚权重。
- Nebula 认领由主机使用连接对应玩家的 `UPosition` 验证；普通 HUD 固定槽是本地 UI 偏好，不进入文明快照。
- 沙盒批量解锁沿用原版 `Published` 资格，可直接解锁并显现 FE 有限科技；原版排除的长期成长节点不批量升满，但在前置解锁后显示。普通模式仍要求 `IsTechnologyDiscovered`。

## Prohibited Legacy Paths

不要新增或恢复 `Protocol`、`Achievement`、`AncientTechnology`、旧 `DarkFog` 调查、黑雾样本、远古科技点、正式领取奖励或协议购买流程。
