# Fractionate Everything 游戏设计规格

本文细化 `GAME_DESIGN.md` 的稳定设计，记录当前可执行规则、参数、状态、结算顺序和必要代码入口。若规格需要改变系统目的、玩家决策、职责边界或设计红线，必须先修改 `GAME_DESIGN.md`。

两份文档的章节编号和名称必须保持一致。本文件不记录候选方案、未确认平衡、实现差距、审计过程、提交记录、构建发布状态或 AI 工作流。

## 1. 模组定位与体验目标

主要代码域：

| 设计职责 | 代码域 |
|---|---|
| 文明阶段、协议、科技、成就 | `Logic/Civilization` |
| 分馏配方和可用性投影 | `Logic/Fractionation/FracRecipes` |
| 分馏热路径 | `Logic/Fractionation/Process` |
| 塔型定义、能力投影和实例状态 | `Logic/Fractionation/Fractionators` |
| FE 2.x Proto 迁移 | `Logic/Buildings/Migration` |
| 数据中心库存和上传路由 | `Logic/DataCenter` |
| 物流自动上传 | `Logic/Station` |
| 文明解析页面 | `UI/MainPanel/Civilization` |

依赖方向：

```text
DataCenterUploadRouter -> Civilization Analysis / DataCenterInventory
Civilization state -> CivilizationRuntimeSync -> Fractionation runtime caches
Fractionation hot path -> runtime caches
```

`Logic/Fractionation` 不引用 `Logic/Civilization`。`CivilizationModule` 及其子服务只管理定义索引、状态、权限校验和运行投影调度；协议内容由已注册配方定义提供，文明服务不生成新科技或协议正文。

当前稳定配置由 `ProgressionProfileRegistry` 在原型和分馏配方注册完成后构建。默认配置 ID 为 `vanilla-compatible`，版本为 `1`。

## 2. 核心玩法循环

当前循环的主要执行入口：

```text
矩阵或原胚进入分馏塔
    -> 分馏热路径结算实际产物
    -> 交互塔或其他上传入口调用 DataCenterUploadRouter.Upload
    -> 解析数据进入 AnalysisService
    -> 生成阶段检索机会
    -> ProtocolRetrievalService 逐次结算检索
    -> ProtocolProgressStore 保存发现与完整度
    -> CivilizationRuntimeSync.Refresh 重建配方和科技投影
    -> 新协议、科技能力和配方校准进入后续分馏循环
```

`CivilizationRuntimeSync.Refresh()` 的固定顺序：

1. 重建协议管理范围和配方可用集合。
2. 重建成就提供的配方成功率加成。
3. 重建科技树提供的塔型能力。
4. 刷新 `ProcessManager` 分馏塔运行配置。

`ProcessManager` 只在实际产生至少一个产物时调用 `BaseRecipe.RecordSuccesses`。批量结算把“判定成功但实际产物数为零”按单次语义视为损毁，不推进配方校准。

## 3. 资源与状态体系

资源职责：

| 资源或状态 | 用途 | 不允许的用途 |
|---|---|---|
| 残片 | 支付方向检索 | 购买科技、保证锚定结果 |
| 记忆源点 | 支付锚定检索 | 购买科技、兑换残片 |
| 远古文明科技点 | 购买科技树节点 | 协议检索、普通库存交易 |
| 检索机会 | 执行当前阶段检索或深层解析 | 跨阶段转移、物品化交易 |
| 实体解析数据 | 上传并生成对应阶段检索机会 | 进入普通数据中心库存 |
| 配方累计成功 | 校准主路锁定和副产物弃置 | 代替协议完整度或科技解锁 |

阶段状态由 `AnalysisProgressStore` 保存：

```text
PendingData
GeneratedOpportunities
AvailableOpportunities
```

协议状态由 `ProtocolProgressStore` 保存：

```text
Discovered
Completeness: 0..100
```

配方状态由 `BaseRecipe` 保存：

```text
TotalSuccessCount
```

科技树状态只保存：

```text
AvailablePoints
TotalPointsEarned
TotalPointsSpent
NodeLevels[nodeKey]
```

运行投影缓存不进入存档。残片奖励和内部联机库存同步直接写数据中心库存，不经过玩家上传路由，避免被误判为新的上传行为。

## 4. 文明解析与协议检索

代码入口：

- `Logic/Civilization/Analysis/AnalysisProgressStore.cs`
- `Logic/Civilization/Analysis/AnalysisService.cs`
- `Logic/Civilization/Protocols/ProtocolRetrievalService.cs`
- `Logic/DataCenter/DataCenterUploadRouter.cs`

六阶段与实体解析数据：

| 顺序 | StageKey | 矩阵 | 实体解析数据 |
|---:|---|---|---|
| 0 | `electromagnetic` | 电磁矩阵 | 电磁解析数据 |
| 1 | `energy` | 能量矩阵 | 能量解析数据 |
| 2 | `structure` | 结构矩阵 | 结构解析数据 |
| 3 | `information` | 信息矩阵 | 信息解析数据 |
| 4 | `gravity` | 引力矩阵 | 引力解析数据 |
| 5 | `universe` | 宇宙矩阵 | 宇宙解析数据 |

六种实体解析数据使用 active IFE ID `8162..8167`，玩家可见名称与用途统一为“解析数据”。旧矩阵精华不再作为 active 内容。

六阶段基础机会成本：

```text
[8, 12, 18, 28, 42, 64]
```

阶段内第 `n` 个已生成机会的成本：

```text
ceil(BaseCost(stage) * 1.32 ^ n)
```

单次成本上限为 `1,000,000,000`。上传解析数据时直接消费实体物品并增加对应阶段进度，不进入普通库存。

检索策略：

| 策略 | 额外成本 | 候选范围 | 结果保证 |
|---|---:|---|---|
| 广域检索 | 0 | 阶段全部可行动协议 | 允许无效响应 |
| 方向检索 | 残片 8 | 指定 `ERecipe` 方向 | 允许无效响应 |
| 锚定检索 | 记忆源点 1 | 指定未完成协议 | 必定发现或推进目标 |

默认批量数为 `10`。批量入口逐次调用同一单次规则，每一步更新候选池、货币、保底和协议状态。

消费顺序：

1. 验证阶段、候选、机会和策略参数。
2. 验证额外货币余额。
3. 消费策略货币。
4. 消费一个检索机会。
5. 若机会消费失败，原额返还策略货币。
6. 执行无效响应、发现、推进或深层解析结算。

没有候选、机会不足或策略参数无效时，不扣除机会和策略货币。

普通检索结果参数：

- 基础无效响应概率：`20%`。
- 连续 `4` 次无效响应后，下一次保证有效。
- 无效响应奖励残片 `2`。
- 存在未发现协议时，基础新发现概率：`35%`。
- 连续 `5` 次未产生新发现后，下一次有效结果优先发现新协议。
- 首次发现增加完整度：随机 `20..40`。
- 推进已发现协议增加完整度：随机 `12..25`。

优先协议必须已发现且未完成。当前有 `N` 项可推进协议时，优先目标被选中的概率：

```text
0.4 + 0.6 / N
```

剩余概率进入普通随机选择。锚定检索跳过无效响应，并保证对指定目标产生发现或推进结果。

阶段主协议（即 `CountsTowardStageCompletion` 为 `true`，不含附属协议）全部达到 `100` 后，后续机会执行深层解析。下一个科技点所需深层解析进度：

```text
NextPointCost = 2 + floor(TotalPointsEarned / 3)
```

达到成本后获得 `1` 点远古文明科技点，并扣除对应深层解析进度。

## 5. 协议、配方与阶段

代码入口：

- `Logic/Civilization/Protocols/ProtocolCatalog.cs`
- `Logic/Civilization/Protocols/ProtocolProgressStore.cs`
- `Logic/Civilization/Protocols/ProtocolEligibilityService.cs`
- `Logic/Fractionation/FracRecipes/RecipeManager.cs`

协议稳定键：

```text
RecipeKey = (ERecipe RecipeType, int InputId)
```

进入协议目录的配方类型：

- `ERecipe.MineralCopy`
- `ERecipe.Conversion`
- `ERecipe.BuildingTrain` 中的实体塔返祖配方

不进入协议目录：

- 通用原胚随机培养。
- 专属原胚稳定培养。
- 六色矩阵解析。
- 通用原胚谱系分化。

实体塔返祖固定归入电磁阶段。只有对应专属培养累计实际产出成功达到 `20` 次后才具备检索资格，并且标记为附属协议，不阻塞阶段主线完成。

资源复制协议沿用资源本身的阶段分类。不可自动采集的木材和植物燃料归电磁阶段；可燃冰允许进入能量阶段；必须依赖星际航行的其他珍奇矿物不得提前。转化协议按全部主产物和副产物中的最高主线矩阵阶段归类；取得协议后，建筑升级链不再要求目标建筑的原版科技解锁或制造掌握。

黑雾输入协议只有在玩家实际接触对应掉落后才具备检索资格。实现判断为 `GameHistoryData.ItemUnlocked(inputId)` 或玩家实际持有该物品；对 `EItemType.DarkFog` / `UnlockKey == -2` 物品，前者表示该敌方掉落已经进入 `enemyDropItemUnlocked`，不是普通科技解锁。当前黑雾协议挂入电磁检索池并标记为附属协议，不计入六个主阶段完成条件；取得资格后仍消耗该阶段机会恢复。

协议完整度达到 `100` 后，`CivilizationRuntimeSync` 将配方写入 `RecipeAvailabilityStore` 的可用集合。配方不读取旧抽取共鸣、旧配方成长输入保留或旧双倍产出加成。

`MineralCopyRecipe.Create` 明确排除临界光子和反物质。每张资源复制配方的主产物固定为 `100%` 输出 `2` 个输入资源；以下自然伴生副产物各自独立按 `1%` 结算：

| 输入 | 自然伴生副产物 |
|---|---|
| 硅石 | 分形硅石 |
| 石矿 | 硅石、钛石 |
| 煤矿 | 金刚石 |
| 氢 | 重氢 |
| 重氢 | 氢 |
| GenesisBook 硫矿 | 硫酸、二氧化硫 |
| GenesisBook 放射性矿物 | 铀矿、钚矿 |
| GenesisBook 海水 | 氯化钠 |
| GenesisBook 氨 | 氮、氢 |
| GenesisBook 氮 | 氨 |
| GenesisBook 氧 | 二氧化碳 |
| GenesisBook 氦 | 氦三 |
| GenesisBook 氦三 | 氦 |
| GenesisBook 二氧化碳 | 氧、高能石墨 |
| GenesisBook 二氧化硫 | 氧、硫粉 |

外部模组未提供某个副产物时，创建配方前过滤该输出；不附带复制精华或其他旧精华。

`ConversionRecipe.CreateAll` 保持已经确认的建筑升级、物流运输、发电与燃料、生产设施、防御与弹药、消耗品、黑雾材料和兼容建筑链，并排除普通矿物、基础中间产物和通用制造材料链。`CreateChain` 继续按相邻层级和同层候选生成主产物，并根据 `itemValue` 计算输出数量；对于 `A -> B -> C` 建筑链，大量投入 `A` 可以得到 `B`，投入 `B` 可以得到 `C`。转化配方不附带转化精华。

## 6. 四塔职责与原胚体系

主要代码入口：

- `Logic/Fractionation/FracRecipes/BuildingTrainRecipe.cs`
- `Logic/Fractionation/FracRecipes/RectificationRecipe.cs`
- `Logic/Fractionation/FracRecipes/MineralCopyRecipe.cs`
- `Logic/Fractionation/FracRecipes/ConversionRecipe.cs`
- `Logic/Fractionation/Fractionators/FractionatorTowerCatalog.cs`

塔型与稳定内部配方类型：

| 塔型 | `ERecipe` | 基础职责 |
|---|---|---|
| 交互塔 | `BuildingTrain` | 原胚培养、实体塔返祖、上传 |
| 解析塔 | `Rectification` | 矩阵解析、原胚谱系分化 |
| 资源塔 | `MineralCopy` | 同种原矿复制与既定自然伴生副产物 |
| 转化塔 | `Conversion` | 已确认物品链的层级转换 |

`BuildingTrainRecipe` 生成三类规则：

- 通用原胚输入：随机产出四类实体塔。
- 专属原胚输入：稳定产出对应实体塔。
- 实体塔输入：在返祖协议完成后产出对应专属原胚。

`RectificationRecipe` 生成两类规则：

- 六色矩阵输入：产出对应阶段实体解析数据。
- 通用原胚输入：随机分化为四类专属原胚；满足主路锁定许可和校准后可保存实例谱系目标。

返祖资格按对应专属培养配方的 `TotalSuccessCount >= 20` 判断。通用培养、专属培养和谱系分化作为基础设施始终不进入随机协议目录。

开局套件提供交互塔和通用原胚。具体数量必须满足第二座交互塔和首座解析塔不会因首轮随机培养结果而永久阻断；启动套件不再发放旧 I-V 原胚、点数聚集塔或旧提示物品。

## 7. 远古科技与运行控制

代码入口：

- `Logic/Civilization/Technology/AncientTechTreeCatalog.cs`
- `Logic/Civilization/Technology/AncientTechTreeState.cs`
- `Logic/Civilization/Technology/AncientTechTreeService.cs`
- `Logic/Fractionation/Fractionators/TowerRuntimeModifierCache.cs`
- `Logic/Fractionation/Fractionators/ConversionSingleLock.cs`
- `Logic/Fractionation/Fractionators/AnalysisLineageTarget.cs`
- `Logic/Fractionation/Fractionators/FractionatorByproductDiscard.cs`
- `Logic/Fractionation/Process/ProcessManager.cs`

节点定义字段：

```text
NodeKey
DisplayNameKey
TowerType
Cost
EffectType
PrerequisiteNodeKey
```

四条主干固定顺序：

| 顺序 | 塔型 | 稳定内部类型 |
|---:|---|---|
| 1 | 交互塔 | `BuildingTrain` |
| 2 | 解析塔 | `Rectification` |
| 3 | 资源塔 | `MineralCopy` |
| 4 | 转化塔 | `Conversion` |

每条主干固定五级串行节点：

| 顺序 | 能力 | 成本 |
|---:|---|---:|
| 1 | 流动输出堆叠 | 1 |
| 2 | 产物输出堆叠 | 2 |
| 3 | 分馏永动 | 3 |
| 4 | 主路锁定 | 5 |
| 5 | 副产物弃置 | 8 |

购买要求至少完成一个协议阶段，并且前置节点已解锁。同一塔型的所有实体塔共享节点状态。

能力语义：

- `EnableFluidOutputStacking`：流动物品按塔当前堆叠上限整组输出。
- `EnableProductOutputStacking`：主产物和副产物按塔当前堆叠上限整组输出。
- `EnableFractionationForever`：产物缓存满载时仍继续分馏。
- `MainOutputLock`：只从 `OutputMain` 选择固定目标。
- `ByproductDiscard`：只清除 `OutputAppend`，不提供任何补偿。

主路锁定和副产物弃置使用三层许可：

```text
塔型节点已购买
AND 当前配方 TotalSuccessCount 达到门槛
AND 当前实体塔已保存对应设置
```

主路锁定校准门槛 `S`：

- 建筑培养配方：`20`。
- 其他配方：电磁阶段从 `200` 开始，此后每个矩阵阶段翻倍。

副产物弃置校准门槛：

```text
5S
```

交互塔通用孵化、转化塔多主产物和解析塔谱系分化分别保存实例目标。主路锁定只作用于 `OutputMain`；副产物弃置只作用于 `OutputAppend`。

批量结算只按实际产出成功数调用 `RecordSuccesses`。副产物批量补救按缺失成功次数和本批次已观察副产物命中率计算：

```text
RollBinomialApprox(
    missingSuccessCount,
    producedHits / rolledSuccessCount)
```

三个实例状态通过 `FractionatorBlueprintParameters` 的共享 `Upsert/TryRead` 写入同一蓝图参数数组，互不覆盖；同时接入存档、复制粘贴和 Nebula packet type `2/3/4`。

## 8. 成就与长期目标

代码入口：

- `Logic/Civilization/Achievements/AchievementCatalog.cs`
- `Logic/Civilization/Achievements/AchievementService.cs`
- `Logic/Fractionation/FracRecipes/Runtime/RecipeModifierCache.cs`

成就每 `60 tick` 低频检查一次，完成状态保存在当前存档。当前定义：

| 键 | 条件 | 奖励 |
|---|---|---|
| `first-protocol` | 完成 1 项协议 | 资源复制成功率 `+1%` |
| `first-stage` | 完成 1 个阶段 | 转化成功率 `+1%` |
| `first-tech` | 累计投入 1 科技点 | 全配方成功率 `+0.5%` |
| `fractionation-1000` | 累计成功分馏 1000 次 | 全配方成功率 `+0.5%` |

成就完成后调用 `CivilizationRuntimeSync.Refresh()`，热路径只读取 `RecipeModifierCache`。成就状态不直接替代协议完整度、科技节点或配方校准。

## 9. UI、教学与信息反馈

主面板长期页面范围包括生产、文明解析、资源、恢复手册、图鉴和设置。文明解析分类的主要页面：

| 页面 | 展示和操作范围 |
|---|---|
| 文明总览 | 六阶段数据、机会、发现数和完成数 |
| 文明协议恢复 | 阶段、广域/方向/锚定策略、目标、批量结算、优先协议、货币和结果 |
| 远古科技树 | 科技点、四条塔型主干、前置和购买 |
| 文明成就 | 当前进度、完成状态和固定奖励 |

客户端联机 UI 提交需要主机结算的动作后，显示“等待主机”状态，不继续修改本地文明状态。

`DataCenterUploadRouter.Upload` 是玩家和建筑实体上传的统一入口：

```text
若 itemId 是当前阶段配置中的解析数据:
    AnalysisService.TrySubmitDataItem
否则:
    DataCenterInventory.AddItemToModData
    若是有效分馏塔实体，再触发内部塔型注册科技兼容逻辑
```

接入上传路由的入口：

- 交互塔正面上传。
- 玩家丢弃物回收。
- 背包二次排序上传。
- 物流交互站上传。
- `PackageLogistic` 兼容写入。

内部奖励、联机库存同步包和残片奖励直接写数据中心库存，不经过上传路由。

恢复手册保存长期机制说明；原生 Tutorial 只提供入口提示和首轮操作引导。旧抽取、成长规划、市场交易、旧主线任务、旧成就、旧分馏统计、全局成长和旧建筑操作页面及其存档块不再保留隐藏入口或兼容读取。

## 10. 存档、联机与版本迁移

`FeatureSaveRegistry` 使用顶层 `AncientCivilization` 块，内部子块：

```text
Profile
Analysis
Protocols
Technology
Achievements
Recovery
```

定义目录不保存，只保存当前存档状态；导入后统一重建运行投影。首版试验使用的顶层 `Civilization` 块不注册读取，由通用未知块机制跳过；其中的协议、解析、科技点和成就不折算、不迁移，新结构从 `AncientCivilization` 空状态开始。旧 `Gacha`、`Economy`、`RecipeGrowth`、旧主面板页面和旧顶层文明恢复块同样不读取或导出。

`BuildingManager` 保存配方累计成功和实例运行设置。解析谱系目标导入同时识别旧块名 `RectificationTuningTarget` 与当前块名 `AnalysisLineageTarget`，用于同一现役状态的名称兼容。

Nebula 文明同步采用主机权威模型：

- 客户端动作请求包括手动解析数据上传、优先协议切换、单次/批量检索和科技购买。
- 主机验证检索模式；网络批量请求上限为默认批量数 `10`。
- 主机结算后使用 `CivilizationStatePacket` 广播阶段、协议、科技、成就、配方校准和残片/记忆源点余额。
- 自动上传形成新检索机会、主机成就完成和玩家加入时，由主机广播或补发权威快照。
- `CivilizationStatePacketProcessor` 在主机拒绝客户端全量快照。
- 客户端请求分支返回等待主机状态，不提前修改本地文明状态。

`LegacyProtoMigration` 以 FE `v2.2.3` 为基准，映射范围：

- 六类现役建筑：`8021/8022/8026/8027/8028/8029`。
- 对应模型：`601/602/606/607/608/609`。
- 物流配方：`928/929`。
- 旧定向原胚：`8016`。
- 有明确现役对应物的隐藏科技和相关引用。

迁移覆盖实体、预建筑、蓝图、库存与物流组件、传送带/分馏/分拣缓存、配方和科技引用。量子复制、点金、点数聚集和旧 I-V 原胚不映射。映射函数幂等。

`FractionatorOutputState.OutputExtendImport` 只保留 `LDB.items.Exist(outputId)` 的产物。数据中心旧 `LeftInc` 自动喷涂池块不再导出；导入旧存档时由未知标签长度机制完整跳过。下载物品只保留库存原有增产点。

没有真实 FE 2.x 存档和蓝图样本时，迁移验证范围仅包括结构探针、人工映射复核和可编译性，不声明实档迁移通过。

## 11. 全局红线

以下规则是实现审查的直接否决条件：

- 不注册旧建筑等级、经验、突破、献祭、裂变池、共振、`SuccessBoost` 或自动喷涂池运行路径。
- 不注册旧抽取、成长商店、市场交易、旧线性配方成长页面或存档块。
- 不把残片、记忆源点和远古文明科技点加入互换入口。
- 不让 `MineralCopy` 创建临界光子、反物质或配方未明确列出的关键稀有产物；既定自然伴生副产物不属于该禁令。
- 不让普通广域或方向检索绕过随机候选，也不让锚定检索返回无关或无效结果。
- 不让批量检索采用与逐次单次不同的近似状态结算。
- 不从客户端发送全量文明状态覆盖主机。
- 不把 `Logic/Civilization` 引入逐物品分馏热路径。
- 不直接修改共享 `BaseRecipe.GetOutputs` 来实现某一配方特例；配方差异由子类承担。
- 不使用保留字段 `buffBonus1/2/3`。
- 不把构建命令、验证日志、提交历史、临时缺口、AI 工作流或聊天状态写入本文件。
