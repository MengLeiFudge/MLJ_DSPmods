# Fractionate Everything 机制细则

本文面向作者和 AI 维护者，记录 FE 分馏域的公式、阈值、执行频率、状态字段、数据表、代码入口和 UI 操作细节。

本文必须服从 `DESIGN.md`：

- `DESIGN.md` 定义模块目的、必要性、交互边界和审计结论。
- 本文只能细化执行规则，不能反向改变模块目的。
- 如果本文中的细则需要改变 `DESIGN.md` 的方向，必须先更新 `DESIGN.md` 的审计结论。

## 1. 代码域索引

| 设计域 | 主要代码域 |
|---|---|
| 分馏配方 | `Logic/Fractionation/FracRecipes` |
| 分馏热路径 | `Logic/Fractionation/Process` |
| 配方成长 | `Logic/Fractionation/Growth` |
| 塔定义和建筑成长 | `Logic/Fractionation/Fractionators`, `Logic/Buildings` |
| 时隧检索 | `Logic/Gacha` |
| 资源经济 | `Logic/Items`, `Logic/Economy` |
| 统一堆叠 | `Logic/Progression/StackingManager.cs` |
| 原版配方同步 | `Logic/VanillaRecipes` |
| 任务与成就 | `UI/MainPanel/ProgressTask` |
| 数据中心 | `Logic/DataCenter`, `Logic/Station` |

内部代码名 `Gacha` 可以作为兼容实现名保留；玩家可见设计语言统一叫“时隧检索”。

## 2. 资源层级细则

### 2.1 矩阵精华面值

| 精华 | 面值 |
|---|---:|
| 电磁精华 | 2 |
| 能量精华 | 4 |
| 结构精华 | 8 |
| 信息精华 | 16 |
| 引力精华 | 32 |
| 宇宙精华 | 64 |

### 2.2 记忆源点

记忆源点是目标设计中唯一源点，显示价值为 256。

约束：

- 只能来自一次性奖励、活动、补偿或跨存档残留。
- 不能由稳定产线、市场、精馏链或普通配方产出。
- 旧源点 ID 只能作为存档兼容占位。

## 3. 主线阶段与黑雾边界细则

FE 自定义能力表现：

- FE 能力不再作为主脑科技树普通研究项展示。
- 原 `TFE...` 科技状态可以保留为旧存档迁移、内部兼容或隐藏状态，但不能作为玩家可见的目标表达。
- 玩家可见口径使用“旧文明信号”“启动套件”“数据中心通信”“文明恢复进度”“协议恢复”“塔型注册”。
- 如果详细规则需要重新让玩家在主脑科技树研究 FE 能力，必须先修改 `DESIGN.md`。

主线矩阵阶段：

| 阶段 | 矩阵 | 精华 |
|---:|---|---|
| 0 | 电磁矩阵 | 电磁精华 |
| 1 | 能量矩阵 | 能量精华 |
| 2 | 结构矩阵 | 结构精华 |
| 3 | 信息矩阵 | 信息精华 |
| 4 | 引力矩阵 | 引力精华 |
| 5 | 宇宙矩阵 | 宇宙精华 |

黑雾边界：

- 黑雾矩阵不进入矩阵萃取输入。
- 黑雾矩阵不产生黑雾精华。
- 黑雾矩阵不映射到信息、引力或其它主线科技阶段。
- 黑雾基地等级不能用于推导主线矩阵阶段。

## 4. 通用分馏热路径细则

### 4.1 核心代码入口

- `Logic/Fractionation/FracRecipes/BaseRecipe.cs`
- `Logic/Fractionation/Process/ProcessManager.cs`

### 4.2 单次结算顺序

所有分馏配方继承 `BaseRecipe`。一次输入的结算顺序：

1. 损毁判定。
2. 成功判定。
3. 成功则输出主产物和副产物。
4. 成功且有产物时，再判定输入保留。
5. 成功但产出数量为 0 时视为损毁。
6. 未成功则输入直通。

公式：

```text
DestroyRatio = max(0, 0.04 - 当前存档成就损毁减免)
EffectiveSuccess = SuccessRatio * (1 + 增产点数加成) * (1 + 成功率加成)
```

说明：

- 输入直通不是输入保留。
- 直通会从塔内输入缓存移除，然后作为流动输出吐出。
- 输入保留只发生在成功产出后。
- 成就损毁减免必须来自当前存档内普通成就结果。

### 4.3 输出判定

主产物：

- `OutputMain` 概率之和应为 100%。
- 成功时必定选择且仅选择其中一项。
- 若输出数量为小数，整数部分必定输出，小数部分再次按概率判定。
- 主产物命中后再判定产物翻倍。

副产物：

- `OutputAppend` 中每一项独立判定。
- 小数数量同样按整数部分加小数概率处理。

若成功但所有产物数量为 0，则视为损毁。

### 4.4 批量结算

批量结算用于运行热路径减少分配和重复随机调用。

当前目标语义：

```text
destroyedCount = Binomial(batchCount, DestroyRatio)
aliveCount = batchCount - destroyedCount
successCount = Binomial(aliveCount, EffectiveSuccess)
passThroughCount = aliveCount - successCount
remainInputCount = Binomial(successCount, RemainInputRatio)
successConsumedCount = successCount - remainInputCount
InputRemoveCount = destroyedCount + passThroughCount + successConsumedCount
ConsumedRegisterCount = destroyedCount + successCount
```

批量近似不得改变单次语义。

## 5. 四类配方细则

### 5.1 建筑培养

代码入口：

- `Logic/Fractionation/FracRecipes/BuildingTrainRecipe.cs`

生成规则：

```text
对每个有效塔型生成两条配方：

塔原胚 -> 实体塔 x1
实体塔 -> 对应塔原胚 x1
SuccessRatio = 0.05
主产物概率 = 100%
无副产物
```

当前有效塔型：

- 交互塔原胚 <-> 交互塔
- 矿物复制塔原胚 <-> 矿物复制塔
- 转化塔原胚 <-> 转化塔
- 精馏塔原胚 <-> 精馏塔

成长角色：

- `GrowthRole = ToolUnlock`

### 5.2 矿物复制

目标规则：

```text
输入 A -> A x2
SuccessRatio = 0.05
主产物概率 = 100%
可有稀有副产物
```

副产物只能表达强资源关系，例如硅石到分形硅石。

### 5.3 物品转化

目标规则：

```text
输入 A -> 同链条候选输出
SuccessRatio = 0.05
输出列表由 CreateChain 生成
转化塔可开启单路锁定
```

单路锁定规则：

- 锁定后只产出指定目标。
- 非锁定历史产物优先从塔内产物缓存清空，避免旧随机产物堵住锁定产线。
- 单路锁定必须影响热路径输出，不是 UI 过滤。

### 5.4 精馏

代码入口：

- `Logic/Fractionation/FracRecipes/RectificationRecipe.cs`

矩阵萃取：

```text
电磁矩阵 -> 电磁精华 x1
能量矩阵 -> 能量精华 x1
结构矩阵 -> 结构精华 x1
信息矩阵 -> 信息精华 x1
引力矩阵 -> 引力精华 x1
宇宙矩阵 -> 宇宙精华 x1

SuccessRatio = 0.05
主产物概率 = 100%
失败直通
损毁使用通用损毁规则
```

精华重整当前实现：

- 输入为六种矩阵精华。
- `RectificationRecipeKind.EssenceTuning` 表示重整配方。
- `BaseCompressionRatio = 0.45`。
- `MaxPlannedCompressionRatio = 0.60`。
- `TicketSplitRatio = 0.10`。
- 非边界层级同时存在向高阶压缩、向低阶回流和残片拆票。
- 边界层级只存在单方向重整与残片拆票。

重整输出计数表：

```text
CompressionOutputCounts = [0.45, 0.46, 0.48, 0.50, 0.51, 0.52]
RefluxOutputCounts      = [1.80, 1.88, 1.96, 2.00, 2.06, 2.12]
```

审计状态：

- 精华重整是待验证细则。
- 如果后续审计证明它只是货币换算，应迁到数据中心或删除。

## 6. 分馏塔、建筑成长与献祭细则

### 6.1 通用三口模型

所有模组分馏塔共享三口结构：

| 接口 | 通用行为 |
|---|---|
| 侧边 belt1/belt2 输出 | 输出 `fluidOutputCount`，即直通输入 |
| 侧边 belt1/belt2 输入 | 吸入当前 `fluidId`；塔为空时可吸入任意可接受输入并确定 `fluidId` |
| 正面 belt0 输出 | 输出塔内产物缓存 |

正面输入仅对交互塔的物品交互模式有效：

- 没有侧边连接。
- 产物缓存为空。
- 正面输入物品上传到数据中心。

产物输出优先级：

1. 若启用单路锁定，先清空非锁定历史产物。
2. 普通情况下优先输出达到堆叠上限的产物。
3. 输入停下后允许吐出尾料。

### 6.2 塔型特质

| 塔 | 配方族 | 特质方向 |
|---|---|---|
| 交互塔 | 建筑培养 | 原胚修复、实体塔回退、数据中心上传、献祭 |
| 矿物复制塔 | 矿物复制 | 复制效率、副产物、产物增产点数 |
| 转化塔 | 物品转化 | 同链条转化、单路锁定、输入保留 |
| 精馏塔 | 精馏 | 矩阵萃取、精华重整、精华催化 |

### 6.3 献祭

代码入口：

- `Logic/Fractionation/Process/Sacrifice.cs`
- `Logic/DataCenter/DataCenterInventory.cs#Take10PercentTower`

启用条件：

- `InteractionTower.EnableSacrificeTrait` 为真。
- 数据中心中对应有效分馏塔实体库存达到献祭阈值。

库存阈值：

```text
FractionatorSacrificeThreshold = 1000
```

执行频率：

```text
GameMain.FixedUpdate 后置补丁
GameMain.gameTick % 60 == 3 时执行
约每秒一次
```

消耗规则：

```text
若 centerItemCount[itemId] < 1000:
    本轮不消耗
否则:
    sacrificedCount = TakeItemFromModData(itemId, centerItemCount[itemId] / 10)
```

献祭只消耗当前数据中心实体库存，不读取历史累计上传量。

有效献祭量：

```text
effectiveCount = sacrificedCount
若对应 TowerFamily 回响 > 0:
    effectiveCount = sacrificedCount * (1 + resonance * 0.05)
```

维度共鸣：

```text
若 InteractionTower.EnableDimensionalResonance:
    effectiveCount *= 1 + 0.1 * buffCount
```

其中 `buffCount` 是本轮四类塔中实际发生献祭的类型数量。

增幅计算：

```text
rawBoost = sqrt(effectiveCount) / 10
boostCap = EnableDimensionalResonance ? 1.00 : 0.75
clampedBoost = min(rawBoost, boostCap)
sacrificeStepIndex = floor(clampedBoost / 0.05)
SuccessBoost = sacrificeStepIndex * 0.05
```

增幅映射：

| 献祭库存类型 | 增幅目标 |
|---|---|
| 交互塔 | `InteractionTower.SuccessBoost` |
| 矿物复制塔 | `MineralReplicationTower.SuccessBoost` |
| 转化塔 | `ConversionTower.SuccessBoost` |
| 精馏塔 | `RectificationTower.SuccessBoost` |

献祭特质关闭时：

- 清空最近献祭数量。
- 清空四类塔 `SuccessBoost`。
- 刷新分馏塔运行时配置。

存档：

- 当前献祭增幅不作为长期存档状态保存。
- 导入时重置献祭状态。

## 7. 时隧检索与确定性补差细则

玩家可见术语：

| 旧内部概念 | 玩家语义 |
|---|---|
| Pool | 检索域 |
| Pity | 稳定锚定 |
| Rarity | 信号完整度 |
| Growth Points | 校准进度 |
| Focus | 检索偏向 |
| Resonance | 回响 |

检索域：

- 路线检索域：获得资源组、转化链、精馏家族等检索单位。
- 原胚检索域：获得四类实际塔原胚。
- 成长规划：非随机入口，消耗校准进度、残片、精华或黑雾矩阵完成补差。
- 检索偏向：不独立产出，只影响路线检索和成长规划的权重与折扣。

回响：

- 一个检索单位全解锁后，重复命中可提升回响。
- 回响提供该单位对应配方族的轻量处理增益。
- 回响最高 3，不能替代配方等级和建筑成长。

## 8. 配方成长细则

每个配方由 `RecipeType + InputID` 定位。成长状态包括：

- 是否解锁。
- 当前等级。
- 当前经验。
- 稳定锚定进度。
- 来源标记。

允许的任务口径：

- 当前可发现配方解锁覆盖率达到 X%。
- 当前可发现配方满级覆盖率达到 X%。
- 某类配方首次达到 3 级。
- 某类配方满级覆盖率达到 X%。
- 任意检索单位回响达到 N。
- 精馏配方催化目标数量达到 X。

不允许的任务口径：

- 解锁固定 N 个全部配方。
- 满级固定 N 个全部配方。
- 要求某兼容模组不存在时无法出现的配方。

## 9. 统一堆叠与原版配方时间细则

代码入口：

- `Logic/Progression/StackingManager.cs`
- `Logic/VanillaRecipes/VanillaRecipeManager.cs`

堆叠常量：

```text
LockedMaxStack = 1
BaseUnlockedMaxStack = 4
AbsoluteMaxStack = 20
StackMilestones = [4, 8, 12, 16, 20]
```

当前堆叠：

```text
若集装物流系统未解锁:
    CurrentMaxStack = 1
否则:
    CurrentMaxStack = max(4, ConfiguredMaxStack)
```

原版配方时间同步：

```text
CurrentVanillaRecipeTimeRatio = IsUnlocked ? 4.0 / CurrentMaxStack : 1.0
NewTimeSpend = ceil(BaseTimeSpend * CurrentVanillaRecipeTimeRatio)
```

隐藏原版集装科技：

- 集装分拣器相关隐藏科技由 `StackingManager` 标记完成。
- 运输站集装物流相关隐藏科技由 `StackingManager` 标记完成。
- 玩家外显只看 FE 统一堆叠档位。

刷新依赖：

- 分馏塔运行时配置。
- 分馏塔缓存容量。
- 原版配方时间同步状态。

## 10. 数据中心与物流交互细则

### 10.0 数据中心通信入口

入口链路：

1. 开局短时间后，玩家探测到异常旧文明信号。
2. 信号不完整，只提供出生星附近的指定地点或方向。
3. 玩家到达指定地点后，回收旧文明启动套件。
4. 启动套件建立数据中心通信方式，并开放 `Shift + F` 数据中心入口。
5. 玩家第一次打开面板时，只展示通信日志、启动套件内容、第一轮恢复目标和当前可用功能。

当前触发细节：

- 开局 12 秒后开始提示异常旧文明信号。
- 异常点位于出生星着陆点东北偏东方向约 160 米处。
- 玩家在出生星地表靠近异常点 35 米内时，回收启动套件。
- 未回收前，每 30 秒至多提示一次异常信号方向和距离。

启动套件奖励：

| 物品 | 数量 | 来源职责 |
|---|---:|---|
| 交互塔 | 1 | 原 `TFE分馏塔原胚` 初始奖励 |
| 交互塔原胚 | 110 | 原 `TFE分馏数据中心` 奖励 80 + 原 `TFE分馏塔原胚` 奖励 30 |
| 矿物复制塔原胚 | 30 | 原 `TFE分馏塔原胚` 初始奖励 |
| 转化塔原胚 | 20 | 原 `TFE分馏塔原胚` 初始奖励 |
| 精馏塔原胚 | 20 | 原 `TFE分馏塔原胚` 初始奖励 |

规则：

- 启动套件搬迁当前初始科技奖励，不顺手削弱、增强或重平衡奖励数量。
- 启动套件同时完成两件事：解锁数据中心通信入口，给玩家第一套可操作 FE 物件。
- 启动套件不是超值礼包；不得包含后续礼包、矩阵阶段奖励、物流交互、塔型特质或高阶功能。
- 启动套件只能领取一次；旧存档已解锁对应初始科技或已获得等价奖励时，不得重复发放。
- 若奖励直接进入数据中心库存，必须先同步开放数据中心通信入口。
- 若奖励进入玩家背包，必须处理背包空间、提示和重复领取保护。
- 后续能力仍按文明恢复进度、矩阵阶段、塔型上传和协议恢复展开。

### 10.1 数据中心库存

代码入口：

- `Logic/DataCenter/DataCenterInventory.cs`

核心状态：

```text
centerItemCount[itemId]  # 数据中心实体库存
centerItemInc[itemId]    # 数据中心库存携带的增产点
leftInc                  # 全局增产点池
ManualExtractCount       # 手动提取次数
ManualUploadCount        # 手动上传次数
```

上传：

```text
AddItemToModData(itemId, count, inc)
centerItemCount[itemId] += count
centerItemInc[itemId] += inc
若 itemId 是有效分馏塔:
    检查对应科技解锁条件
```

提取：

```text
TakeItemFromModData(itemId, count, out inc)
```

沙土特殊处理：

- 数据中心读写沙土直接映射玩家 `sandCount`。

### 10.2 物流交互站

代码入口：

- `Logic/Station/Runtime.cs`
- `Logic/Station/ProliferatorPool.cs`

槽位传输模式：

- `Sync`：双向同步。
- `Upload`：仅上传。
- `Download`：仅下载。

容量模式：

- `Limited`：有限上传，受数据中心目标数量限制。
- `Infinite`：无限上传。

阈值来自配置：

```text
downloadThreshold = Miscellaneous.DownloadThreshold
uploadThreshold = Miscellaneous.UploadThreshold
```

上传模式：

```text
若槽位数量 > store.max * uploadThreshold:
    上传超出部分
    若 Limited 模式:
        上传数量不得让数据中心库存超过 itemModSaveCount[itemId]
```

下载模式：

```text
若总供应量 < store.max * downloadThreshold:
    从数据中心下载到 round(store.max * downloadThreshold)
```

交互耗电：

```text
costPerItem = sqrt(itemValue[itemId]) * 10000 * station.InteractEnergyRatio
```

若单槽剩余电力不足，则按电力裁剪搬运数量。

物流交互站每搬运 1 个物品，给物流交互站建筑组增加 1 经验。

### 10.3 增产点池

下载补点：

```text
若行星内物流交互站 Level >= 3:
    消耗点池，把下载物品补到平均 4 点
若 Level >= 12:
    允许过载补点，超过 4 点后每补 1 点消耗 3 点池
```

增产点池属于物流交互站支撑能力，不是第五条分馏路线。

## 11. 任务、成就、日志与引导细则

主线入口任务：

- 第一步：异常旧文明信号。
- 第二步：前往指定地点。
- 第三步：回收旧文明启动套件。
- 第四步：建立数据中心通信并打开面板。
- 第五步：完成第一轮 FE 闭环，引导交互塔、原胚培养、时隧检索和文明恢复进度。

恢复手册：

- 启动套件包含旧文明说明书；说明书解锁数据中心面板中的“恢复手册”页面。
- 原生 `Tutorial` 只承担入口提示和 G 键回看，不承载完整搭建状态机。
- 恢复手册第一章为“初始原胚孵化”，按以下顺序展示：
  1. 回收启动套件。
  2. 打开分馏数据中心。
  3. 放置第一个交互塔。
  4. 左右口连接成环。
  5. 正面输出先接临时箱子。
  6. 向环输入交互塔原胚。
  7. 获得并放置第二个交互塔。
  8. 拆除临时箱子，将第一台塔产物接入第二台塔正面入口。
  9. 上传交互塔并恢复物品交互协议。
- 可稳定读取的事实节点自动检测：数据中心权限、交互塔/交互塔原胚持有、交互塔成长或库存、物品交互协议。
- 左右成环、临时箱子、输入原胚、获得并放置第二塔、接入第二塔这类拓扑步骤由玩家手动确认；不要为引导页面写脆弱的传送带拓扑扫描。
- 手动确认只记录引导进度，不发物品、不解锁生产能力、不替代真实协议恢复条件。
- 恢复手册后续章节继续承载数据中心库存、时隧检索、塔型注册、物流交互和精馏等系统说明。

术语替换：

| 旧口径 | 新口径 |
|---|---|
| 研究分馏数据中心科技 | 回收启动套件 / 建立数据中心通信 |
| 解锁分馏塔原胚科技 | 恢复原胚培养资料 / 取得启动套件原胚 |
| 解锁物品交互科技 | 恢复物品交互协议 |
| 解锁矿物复制科技 | 注册矿物复制塔型 / 恢复矿物复制协议 |
| 解锁物品转化科技 | 注册转化塔型 / 恢复物品转化协议 |
| 解锁物品精馏科技 | 注册精馏塔型 / 恢复精馏协议 |
| 超值礼包 | 文明恢复阶段奖励 / 数据中心补给 |

普通成就规则：

- 不跨存档。
- 可以提供当前存档内的轻量加成。
- 可以影响损毁减免、检索稳定、补差折扣等，但加成应小且可解释。
- 每个成就记录当前存档解锁时间。
- 解锁事件写入日志，便于玩家反馈真实完成顺序。

秘密成就规则：

- 可跨存档。
- 与游戏进度弱相关。
- 适合打开某面板、触发隐藏提示、特殊交互等发现型目标。
- 不提供决定性强度，只提供展示、便利或提前访问。

必须记录的反馈数据：

- 当前存档普通成就解锁时间。
- 秘密成就首次发现时间。
- 关键任务完成时间。
- 配方解锁、首次 3 级、首次满级、覆盖率节点。
- 堆叠阶段解锁时间。
- 玩家首次获得黑雾矩阵和黑雾支线阶段时间。

黑雾成就必须按玩家实际接触时间穿插在普通序列中，不能单独放到最后。

## 12. UI 主面板与页面细则

页面职责：

| 页面域 | 职责 |
|---|---|
| 配方操作 | 查看配方、产出、概率、等级、锁定或重整目标 |
| 建筑操作 | 查看塔等级、特质、突破、库存和成长瓶颈 |
| 时隧检索 | 选择检索域、查看稳定锚定、消耗和结果 |
| 成长规划 | 确定性补差、催化、缺口收束 |
| 数据中心 | 查看 FE 库存、上传下载、资源交互 |
| 任务成就 | 当前阶段引导、长期目标、日志反馈 |
| 图鉴统计 | 回顾已发现内容、统计和说明 |

UI 可以：

- 发起检索。
- 切换偏向。
- 购买成长报价。
- 选择锁定输出或重整目标。
- 手动上传/提取数据中心物品。
- 展示概率、成长和任务状态。

UI 不能：

- 绕过热路径直接生产分馏产物。
- 绕过成长执行器直接改配方等级。
- 绕过数据中心凭空生成实体库存。
- 让市场或任务替代稳定产线。

## 13. 黑雾支线细则

黑雾资源只用于：

- 黑雾资源层级。
- 黑雾链条转化。
- 黑雾成长报价。
- 黑雾任务和黑雾成就。

黑雾边界重复确认：

- 黑雾矩阵不进入主线矩阵阶段。
- 黑雾矩阵不进入矩阵萃取。
- 黑雾矩阵不产出精华。
- 黑雾基地等级不推导主线矩阵阶段。
