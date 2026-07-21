# Fractionate Everything 机制细则

本文记录 `DESIGN.md` 对应的可执行规则、状态和代码入口。若细则需要改变模块目的或边界，先修改 `DESIGN.md`。

## 1. 代码域

| 设计域 | 主要代码域 |
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

`Logic/Fractionation` 不引用 `Logic/Civilization`。`CivilizationModule` 及其子服务对应残缺文明档案核心，只管理定义索引、状态、权限校验和运行投影调度；协议内容仍由已注册配方定义提供，服务不得自行生成新科技或协议正文。

资源职责固定为：残片支付方向检索，记忆源点支付锚定检索，远古文明科技点只购买科技树节点。三者不提供兑换入口。

## 2. 阶段配置

`ProgressionProfileRegistry` 在原型和分馏配方注册完成后构建当前配置。默认配置 ID 为 `vanilla-compatible`，版本为 `1`。

| 顺序 | StageKey | 矩阵 | 实体解析数据 |
|---:|---|---|---|
| 0 | `electromagnetic` | 电磁矩阵 | 电磁解析数据 |
| 1 | `energy` | 能量矩阵 | 能量解析数据 |
| 2 | `structure` | 结构矩阵 | 结构解析数据 |
| 3 | `information` | 信息矩阵 | 信息解析数据 |
| 4 | `gravity` | 引力矩阵 | 引力解析数据 |
| 5 | `universe` | 宇宙矩阵 | 宇宙解析数据 |

六种实体解析数据使用独立的 active IFE ID `8162..8167`，玩家可见名称与用途统一为解析数据。旧矩阵精华语义不再作为 active 内容。

## 3. 解析数据与检索机会

代码入口：

- `Logic/Civilization/Analysis/AnalysisProgressStore.cs`
- `Logic/Civilization/Analysis/AnalysisService.cs`
- `Logic/DataCenter/DataCenterUploadRouter.cs`

阶段状态：

```text
PendingData
GeneratedOpportunities
AvailableOpportunities
```

六阶段基础成本：

```text
[8, 12, 18, 28, 42, 64]
```

第 `n` 个已生成机会的成本：

```text
ceil(BaseCost(stage) * 1.32 ^ n)
```

单次成本最高按 `1,000,000,000` 计算。上传解析数据时直接消费实体物品并增加对应阶段进度，不进入数据中心普通库存。

## 4. 协议目录与完整度

代码入口：

- `Logic/Civilization/Protocols/ProtocolCatalog.cs`
- `Logic/Civilization/Protocols/ProtocolProgressStore.cs`
- `Logic/Civilization/Protocols/ProtocolEligibilityService.cs`

协议键：

```text
RecipeKey = (ERecipe RecipeType, int InputId)
```

当前进入协议目录的配方类型：

- `ERecipe.MineralCopy`
- `ERecipe.Conversion`
- `ERecipe.BuildingTrain` 中的实体塔返祖配方

通用原胚随机孵化、专属原胚稳定培养和矩阵解析不进入随机协议目录。实体塔返祖固定归入电磁阶段，只有对应专属培养累计成功达到 `20` 次后才具备检索资格，并且不阻塞阶段主线完成。

资源复制协议沿用资源本身的阶段分类。转化协议按全部主产物和副产物中的最高主线矩阵阶段归类，避免低阶输入提前开放高阶输出。

每项协议保存：

```text
Discovered
Completeness: 0..100
```

每张配方另由 `BaseRecipe` 持久化 `TotalSuccessCount`。该值从配方第一次实际产出开始累计，不因科技节点尚未购买而丢失，用于主路锁定和副产物弃置的配方校准。

完整度达到 `100` 后，`CivilizationRuntimeSync` 将配方写入 `RecipeAvailabilityStore` 的可用集合。协议管理配方不再读取旧抽取共鸣、旧配方成长输入保留或旧双倍产出加成。

黑雾输入协议只有在对应物品已解锁或玩家已实际持有后才具备检索资格。当前黑雾协议挂入电磁检索池并标记为附属协议，不计入六个主阶段的完成条件；一旦取得资格，仍可消耗该阶段机会恢复。

## 5. 协议检索

代码入口：`Logic/Civilization/Protocols/ProtocolRetrievalService.cs`。

只有当前存在可推进协议或可执行深层解析时，检索才消费对应阶段一个 `AvailableOpportunities`。策略分为：

- 广域检索：只消耗一次机会，在阶段全部可行动协议中结算。
- 方向检索：额外消耗 `8` 个残片，只在选定 `ERecipe` 方向中结算。
- 锚定检索：额外消耗 `1` 个记忆源点，跳过无效响应并保证对选中的未完成协议发现或推进。
- 默认批量数为 `10`；批量入口逐次调用同一单次规则，每一步都更新候选池、货币和保底。

策略货币在请求无候选或机会不足时不扣除；扣除后若机会消费失败则原额返还。检索失败仍获得 `2` 个残片，因此单次机会不会完全无收益。

结果规则：

- 基础无效响应概率：`20%`。
- 连续 4 次无效响应后，下一次保证获得有效结果。
- 存在未发现协议时，基础新发现概率为 `35%`。
- 连续 5 次未产生新发现后，下一次有效结果优先发现新协议。
- 首次发现给予随机 `20..40` 完整度。
- 推进已发现协议时增加随机 `12..25` 完整度。

玩家可在已发现且未完成的协议中设置一个优先目标。若当前有 `N` 项可推进协议，优先目标被选中的概率为：

```text
0.4 + 0.6 / N
```

其余概率仍进入普通随机选择。随机性只影响恢复顺序和速度，不改变最终可得性。

## 6. 深层解析与科技点

当一个阶段内所有计入完成度的协议均达到 `100` 时，该阶段后续检索机会不再抽协议，而是增加深层解析进度。

科技点成本：

```text
NextPointCost = 2 + floor(TotalPointsEarned / 3)
```

达到成本后获得 1 点远古文明科技点，并扣除对应深层解析进度。科技树只保存：

```text
AvailablePoints
TotalPointsEarned
TotalPointsSpent
NodeLevels[nodeKey]
```

## 7. 科技树节点

代码入口：

- `Logic/Civilization/Technology/AncientTechTreeCatalog.cs`
- `Logic/Civilization/Technology/AncientTechTreeState.cs`
- `Logic/Civilization/Technology/AncientTechTreeService.cs`

节点定义包含：

```text
NodeKey
DisplayNameKey
TowerType
Cost
EffectType
PrerequisiteNodeKey
```

四条主干按固定顺序使用 `BuildingTrain`、`Rectification`、`MineralCopy`、`Conversion`，分别代表交互塔、解析塔、资源塔、转化塔。

每条主干固定为五级串行节点：

```text
流动输出堆叠 -> 产物输出堆叠 -> 分馏永动 -> 主路锁定 -> 副产物弃置
```

节点价格依次为 `1 / 2 / 3 / 5 / 8`。购买要求至少完成一个协议阶段，并且前置节点已经解锁；同一塔型的所有实体塔共享解锁状态。五项能力均已接入运行层。

分馏热路径将以下能力分开读取：

- `EnableFluidOutputStacking`：允许流动物品按塔当前堆叠上限整组输出。
- `EnableProductOutputStacking`：允许主产物和副产物按塔当前堆叠上限整组输出。
- `EnableFractionationForever`：产物缓存满载时仍继续分馏。
- `MainOutputLock`：只从 `OutputMain` 选择固定目标；交互塔通用孵化、转化塔多主产物和解析塔谱系分化分别保存实例目标。
- `ByproductDiscard`：只清除 `OutputAppend`，不补偿主产物、成功率、输入、增产点或速度。

主路锁定与副产物弃置使用三层许可：塔型节点已购买、当前配方达到累计成功门槛、单塔实例已设置。主路锁定门槛 `S` 对建筑培养配方为 `20`，其余按矩阵阶段从 `200` 逐阶段翻倍；副产物弃置门槛为 `5S`。实例状态接入 `BuildingManager` 存档块、复制粘贴、可共存的蓝图参数块和 Nebula packet type `2/3/4`。

Nebula 实时同步采用主机权威模型：客户端只发送手动解析数据上传、优先协议切换、单次/整批检索和科技购买动作请求，由主机结算后使用 `CivilizationStatePacket` 广播文明状态、配方校准和残片/记忆源点余额；自动上传形成新检索机会和主机成就完成后也广播，玩家加入时主机补发当前快照。主机不接受客户端全量快照，避免落后副本覆盖权威进度。

## 8. 成就

代码入口：

- `Logic/Civilization/Achievements/AchievementCatalog.cs`
- `Logic/Civilization/Achievements/AchievementService.cs`
- `Logic/Fractionation/FracRecipes/Runtime/RecipeModifierCache.cs`

成就每 60 tick 低频检查一次，完成状态保存在当前存档。当前定义：

| 键 | 条件 | 奖励 |
|---|---|---|
| `first-protocol` | 完成 1 项协议 | 资源复制成功率 +1% |
| `first-stage` | 完成 1 个阶段 | 转化成功率 +1% |
| `first-tech` | 累计投入 1 科技点 | 全配方成功率 +0.5% |
| `fractionation-1000` | 累计成功分馏 1000 次 | 全配方成功率 +0.5% |

成就完成后调用 `CivilizationRuntimeSync.Refresh()`，热路径只读取 `RecipeModifierCache`。

## 9. 上传路由

`DataCenterUploadRouter.Upload` 是玩家和建筑实体上传的统一入口。

```text
若 itemId 是当前阶段配置中的解析数据:
    AnalysisService.TrySubmitDataItem
否则:
    DataCenterInventory.AddItemToModData
    若是有效分馏塔实体，再触发内部塔型注册科技兼容逻辑
```

当前接入入口：

- 交互塔正面上传。
- 玩家丢弃物回收。
- 背包二次排序上传。
- 物流交互站上传。
- PackageLogistic 兼容写入。

内部奖励、联机库存同步包和残片奖励继续直接写数据中心库存，避免把系统内部写入误判为玩家上传。

## 10. 运行投影

`CivilizationRuntimeSync.Refresh()` 执行顺序：

1. 重建协议管理范围和配方可用集合。
2. 重建成就提供的配方成功率加成。
3. 重建科技树提供的塔型能力。
4. 刷新 `ProcessManager` 分馏塔运行配置。

`ProcessManager` 只在实际产生至少一个产物时调用 `BaseRecipe.RecordSuccesses`。批量结算把“判定成功但实际产物数为 0”按单次语义计为损毁，不能提前推进配方校准。

投影缓存都属于分馏域，不保存到存档。

## 11. 存档

`FeatureSaveRegistry` 使用顶层 `AncientCivilization` 块。内部子块：

```text
Profile
Analysis
Protocols
Technology
Achievements
Recovery
```

定义目录不保存，只保存当前存档状态。导入后统一重建运行投影。

首版试验使用的顶层 `Civilization` 块不注册读取入口，由通用未知块机制直接跳过。其协议、解析、科技点和成就状态不折算、不迁移；新结构从 `AncientCivilization` 的空状态开始。旧 Gacha、Economy、RecipeGrowth、旧主面板页面和旧顶层文明恢复块同样不再读取或导出。

`LegacyProtoMigration` 以 FE `v2.2.3` 为基准，只映射六类现役建筑 `8021/8022/8026/8027/8028/8029`、对应模型 `601/602/606/607/608/609`、物流配方 `928/929`、旧定向原胚 `8016` 及明确对应的隐藏科技。迁移覆盖实体、预建筑、蓝图、库存与物流组件、传送带/分馏/分拣缓存、配方和科技引用；量子复制、点金、点数聚集和旧 I-V 原胚不映射。映射函数幂等，但没有真实 FE 2.x 存档和蓝图样本时只能声明结构验证。

数据中心旧 `LeftInc` 自动喷涂池块不再导出；导入旧存档时由未知标签机制跳过。下载物品只保留库存中原有增产点。

## 12. UI 页面

文明解析分类包含：

- 文明总览：六阶段数据、机会、发现数和完成数。
- 文明协议恢复：阶段切换、广域/方向/锚定策略、目标、批量结算、优先协议、货币余额和结果反馈。
- 远古科技树：科技点和四条塔型主干节点。
- 文明成就：当前进度、完成状态和固定奖励。

旧抽取、成长规划、市场交易、旧主线任务、旧成就、旧分馏统计、全局成长和建筑操作页面及其存档块已经删除，不再保留隐藏入口或兼容读取。
