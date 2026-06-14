# 万物分馏实现设计蓝图

## 文档定位

本文是 Fractionate Everything 分馏域的人工可读实现蓝图。

目标不是写短介绍，而是把当前分馏系统的稳定结构、公式、类职责、系统关系和玩法边界写清楚。读者只看本文，再结合 `ProtoID.cs` 中的物品 ID 名称，应能复写当前分馏域的主要源码结构。

本文记录：

- 系统之间的层级和调用关系。
- 分馏塔、配方、成长、抽取、任务、数据中心和物流交互站之间的闭环。
- 稳定公式、等级表、配方生成规则、抽取规则和任务口径。
- 哪些内容属于系统设计，哪些内容不能被 UI、市场或兼容层越权实现。

本文不记录：

- 构建、发布、验证命令和提交历史。
- AI 工作流、临时计划、当前完成度或 TODO 流水账。
- 仅用于排障的一次性探针。

## 一、源码层级

当前分馏域由以下源码域组成：

```text
FractionateEverything/src/
├── Logic/Fractionation/
│   ├── FracRecipes/
│   │   ├── ERecipe.cs                 # 分馏配方类型枚举和显示映射
│   │   ├── RecipeManager.cs           # 全部配方注册、查找和存档入口
│   │   ├── BaseRecipe.cs              # 通用分馏结算公式
│   │   ├── BuildingTrainRecipe.cs     # 原胚/实体塔培养循环
│   │   ├── MineralCopyRecipe.cs       # 资源复制
│   │   ├── ConversionRecipe.cs        # 相关链条转化与单路锁定
│   │   └── RectificationRecipe.cs     # 矩阵萃取与矩阵精华调相
│   ├── Process/
│   │   ├── ProcessManager.cs          # 分馏塔热路径、输送带、缓存和成长回写
│   │   ├── Sacrifice.cs               # 交互塔献祭加成
│   │   └── FractionatorOutputState.cs # 分馏塔扩展状态
│   ├── Fractionators/
│   │   ├── FractionatorTowerCatalog.cs # 当前有效四塔目录
│   │   ├── BuildingGrowthService.cs    # 建筑等级、经验、突破和倍率
│   │   ├── InteractionTower.cs
│   │   ├── MineralReplicationTower.cs
│   │   ├── ConversionTower.cs
│   │   └── RectificationTower.cs
│   └── Growth/
│       ├── RecipeGrowthManager.cs
│       ├── Rules/RecipeGrowthRules.cs
│       ├── Execution/RecipeGrowthExecutor.cs
│       └── Queries/RecipeGrowthQueries.cs
├── Logic/Gacha/
│   ├── GachaManager.cs                # 保底、成长积分、聚焦、回响状态
│   ├── GachaPool.cs                   # 卡池 ID、稀有度和基础概率
│   ├── GachaDrawUnit.cs               # 抽取单位聚合键
│   └── GachaService/                  # 池构建、抽取、聚焦、成长报价
├── Logic/DataCenter/
│   └── DataCenterInventory.cs         # 全局实体库存、增产点、上传下载事实
├── Logic/Station/
│   ├── Runtime.cs                     # 物流交互站上传/下载/同步
│   ├── ProliferatorPool.cs            # 全局增产点池
│   └── ModeState.cs                   # 槽位传输模式和容量模式
└── UI/MainPanel/
    ├── CoreOperate/                   # 配方和建筑操作面板
    ├── DrawGrowth/                    # 主抽取、成长报价、残片兑换
    ├── ProgressTask/MainTask.cs       # 主线任务路线图
    └── ProgressTask/Achievements.cs   # 全局成就和被动加成
```

实现层级：

```text
玩家产线
└── 分馏塔热路径 ProcessManager
    ├── 按塔型选择配方类型 ERecipe
    ├── 用 BaseRecipe/子类结算产出
    ├── 写回塔内输入、输出、流动缓存和统计
    ├── 写回配方成长、建筑成长、成就统计和残片掉落
    └── 通过数据中心/物流交互站连接外部库存

规划与补差
├── GachaService：抽取配方、原胚、成长报价和回响
├── RecipeGrowthExecutor：把抽取、运行、催化、黑雾补差转成配方等级
├── MainTask/Achievements：读取事实并发一次性奖励或被动加成
└── UI：展示、选择、确认消费，不绕过核心系统生产资源
```

## 二、核心对象模型

### 1. 配方类型

`ERecipe` 是分馏塔运行的配方类型：

```text
BuildingTrain  # 交互塔使用，原胚与实体塔互相培养
MineralCopy    # 矿物复制塔使用，资源同类增殖
Conversion     # 转化塔使用，相关链条互转
Rectification  # 精馏塔使用，矩阵萃取与精华调相
```

配方在成长系统里还有语义角色：

```text
Production     # 生产型，主要来自产线运行和主抽取开线
ToolUnlock     # 工具/解锁型，典型是 BuildingTrain
SpecialGrowth  # 特殊成长型，典型是 Rectification
```

`RecipeKey` 由 `RecipeType + InputID` 组成，是成长、抽取、UI 展示和存档查找的共同键。

### 2. 有效塔型

当前有效分馏塔只有四类：

```text
交互塔        IFE交互塔        IFE交互塔原胚
矿物复制塔    IFE矿物复制塔    IFE矿物复制塔原胚
转化塔        IFE转化塔        IFE转化塔原胚
精馏塔        IFE精馏塔        IFE精馏塔原胚
```

`IFE分馏塔定向原胚` 不是第五类塔，它是可定向到四类塔生态的特殊原胚。

旧 ID 可以保留在 `ProtoID.cs`，但只有 `FractionatorTowerCatalog.ActiveFractionatorBuildingIds` 和 `ActiveFractionatorProtoIds` 中列出的 ID 视为当前有效塔型。

### 3. 三层货币

```text
残片
  底层成长货币。用于补差、成长报价、建筑突破、低中层强化。

矩阵精华
  阶段绑定票据和催化材料。来自精馏，可以拆成残片，也可以压缩/回流/催化成长。

源点
  最高层一次性或活动/补偿货币。不能由稳定产线、市场、精馏链或普通配方产出。
```

矩阵精华面值：

```text
电磁精华 = 2
能量精华 = 4
结构精华 = 8
信息精华 = 16
引力精华 = 32
宇宙精华 = 64
```

源点价值只用于价值体系和展示：

```text
记忆源点 = 256
纯净源点 = 512
理论源点 = 1024
```

### 4. 矩阵阶段

矩阵阶段索引：

```text
0 电磁矩阵
1 能量矩阵
2 结构矩阵
3 信息矩阵
4 引力矩阵
5 宇宙矩阵
```

黑雾矩阵按引力阶段处理：

```text
黑雾矩阵 -> stage 4
```

当前进度阶段取玩家已解锁的最高主线矩阵；未解锁时默认为电磁矩阵。

阶段衰减用于旧阶段矩阵在当前阶段的精馏收益：

```text
当前阶段 - 来源阶段 <= 0 -> 1.00
差 1 -> 0.70
差 2 -> 0.45
差 3+ -> 0.25
```

## 三、分馏塔热路径

### 1. 塔型到配方类型

`ProcessManager` 按实体建筑 ID 分派：

```text
交互塔     -> BuildingTrainRecipe -> ERecipe.BuildingTrain
矿物复制塔 -> MineralCopyRecipe   -> ERecipe.MineralCopy
转化塔     -> ConversionRecipe    -> ERecipe.Conversion
精馏塔     -> RectificationRecipe -> ERecipe.Rectification
```

分派后统一进入 `InternalUpdate<T>()`。

### 2. 运行前置状态

每座塔有原版 `FractionatorComponent` 状态和 FE 扩展状态：

```text
fluidId              当前输入物品
fluidInputCount      输入缓存数量
fluidInputInc        输入缓存总增产点数
fluidOutputCount     直通输出缓存数量
fluidOutputInc       直通输出总增产点数
productId            当前主输出显示物品
productOutputCount   原版主输出缓存数量
extraState.Products  FE 多产物缓存
extraState.Recipe    当前输入对应配方
```

如果当前输入找不到已注册配方，塔进入直通逻辑：输入作为流动输出排出，不产生配方成长。

如果找到配方但成长系统未解锁该配方，也按直通处理。

### 3. 进度公式

塔必须有电力：

```text
power < 0.1 -> 本 tick 不运行
```

每 tick 累加进度：

```text
progress += power * (500 / 3)
          * min(fluidInputCargoCount, MaxBeltSpeed)
          * fluidInputCountPerCargo
          + 0.75
progress 上限 = 300000
```

每 10000 进度处理 1 个输入：

```text
batchCount = min(progress / 10000, fluidInputCount)
progress -= batchCount * 10000
```

### 4. 增产与成功率

输入平均增产点：

```text
fluidInputIncAvg = fluidInputInc / fluidInputCount
```

增产剂提供的分馏成功率加成：

```text
pointsBonus = MaxTableMilli(fluidInputIncAvg) * tower.PlrRatio
```

总成功率加成：

```text
successBoost = tower.SuccessBoost + Achievements.GetSuccessRateBonus()
```

配方实际成功率：

```text
actualSuccess = recipe.SuccessRatio * (1 + pointsBonus) * (1 + successBoost)
```

`MaxTableMilli` 同时考虑原版加速表和固定增产表，取较大值。

### 5. 批量结算结果

每批处理返回：

```text
InputRemoveCount       实际从输入缓存扣除的数量
ConsumedRegisterCount  计入消耗统计和成长的数量
SuccessCount           成功次数
DestroyedCount         损毁次数
PassThroughCount       直通次数
PassThroughInc         直通携带的增产点数
outputs                主产物/副产物缓存增量
```

成功后会：

- 把产物写入塔内 `Products` 缓存。
- 登记 `productRegister`。
- 记录总成功数和峰值成功速率。
- 给对应建筑增加经验，经验量等于 `SuccessCount`。
- 若配方可通过加工成长，调用 `RecipeGrowthExecutor.ApplyProcessingProgress()`。
- 每次成功有 2% 概率掉落 1 个残片到数据中心。

### 6. 输送带规则

侧边 belt1/belt2：

- 输出模式：输出 `fluidOutputCount`，即直通输入。
- 输入模式：吸入当前 `fluidId`；如果当前塔空，则先吸入任意可接受物品并确定 `fluidId`。

正面 belt0：

- 输出模式：输出塔内产物缓存。
- 输入模式仅对交互塔有效：当没有侧边连接且产物缓存为空时，把正面输入物品上传到数据中心。

产物输出优先级：

- 转化塔启用单路锁定时，非锁定产物优先清空，避免历史随机产物堵住锁定产线。
- 普通情况下优先输出达到堆叠上限的产物；输入停下后允许吐出尾料。

## 四、通用配方结算公式

`BaseRecipe` 定义三段判定：

```text
1. 损毁判定
2. 成功判定
3. 未成功则直通
```

单次结算：

```text
if rand < DestroyRatio:
    inputChange = -1
    output = null
    扣除输入平均增产点

else if rand < SuccessRatio * (1 + pointsBonus) * (1 + successBoost):
    从 OutputMain 按累计概率选 1 项主产物
    产物数量 = floor(OutputCount) + Bernoulli(frac(OutputCount))
    主产物命中后再按 DoubleOutputRatio 判定翻倍
    OutputAppend 中每项独立按 SuccessRatio 判定
    如果至少有产物：
        inputChange = Bernoulli(RemainInputRatio) ? 0 : -1
    如果没有任何产物：
        视为损毁，inputChange = -1

else:
    inputChange = -1
    output = emptyOutputs
    进入直通输出
```

批量结算：

```text
destroyedCount = Binomial(batchCount, DestroyRatio)
aliveCount = batchCount - destroyedCount
successCount = Binomial(aliveCount, actualSuccess)
passThroughCount = aliveCount - successCount
remainInputCount = Binomial(successCount, RemainInputRatio)
successConsumedCount = successCount - remainInputCount
InputRemoveCount = destroyedCount + passThroughCount + successConsumedCount
ConsumedRegisterCount = destroyedCount + successCount
```

二项分布近似：

- `trials <= 8` 时逐次 Bernoulli。
- `trials > 8` 时用 3 次均匀随机近似正态分布。

基础损毁率：

```text
BaseRecipe.DestroyRatio = max(0, 0.04 - 成就损毁减免)
```

精馏配方有自己的损毁规则，见精馏章节。

## 五、四类配方

### 1. BuildingTrainRecipe：塔生态培养

设计目标：

- 解释分馏塔从哪里来。
- 让原胚、实体塔、定向原胚构成塔生态循环。
- 给交互塔献祭和原胚抽取提供实体基础。

对每个有效塔型生成两条配方：

```text
塔原胚 -> 96% 实体塔 x1 + 4% 定向原胚 x1
实体塔 -> 96% 对应塔原胚 x1 + 4% 定向原胚 x1
```

共同参数：

```text
RecipeType = BuildingTrain
GrowthRole = ToolUnlock
SuccessRatio = 0.05
DestroyRatio = BaseRecipe 默认
OutputAppend = 空
```

`BuildingTrain` 的输入包括四类普通原胚、四类实体塔和定向原胚能映射到的培养入口。成长系统把原胚输入识别为 `BuildingTrainForward`，把实体塔输入识别为 `BuildingTrainReverse`。

### 2. MineralCopyRecipe：资源复制

设计目标：

- 输入某资源本身，成功时复制同资源。
- 让部分资源关系通过低概率副产物表达。
- 自动兼容可见矿脉资源和资源型物品。

共同参数：

```text
RecipeType = MineralCopy
GrowthRole = Production
SuccessRatio = 0.05
主产物 = 输入物品 x2，概率 100%
副产物 = 按资源关系配置
```

基础资源表：

```text
木材
植物燃料
铁矿
铜矿
硅石 -> 副产物 1% 分形硅石
钛石
石矿 -> 副产物 1% 硅石，1% 钛石
煤矿 -> 副产物 1% 金刚石
水
原油
硫酸
氢 -> 副产物 1% 重氢
重氢 -> 副产物 1% 氢
临界光子
可燃冰
金伯利矿石
分形硅石
光栅石
刺笋结晶
单极磁石
有机晶体
黑雾矩阵
硅基神经元
物质重组器
负熵奇点
核心素
能量碎片
反物质
```

GenesisBook 额外资源：

```text
钨矿
铝矿
硫矿 -> 副产物 1% 硫酸，1% 二氧化硫
放射性矿物 -> 副产物 1% 铀矿，1% 钚矿
海水 -> 副产物 1% 氯化钠
盐酸
硝酸
氨 -> 副产物 1% 氮，1% 氢
氮 -> 副产物 1% 氨
氧 -> 副产物 1% 二氧化碳
氦 -> 副产物 1% 氦三
氦三 -> 副产物 1% 氦
二氧化碳 -> 副产物 1% 氧，1% 高能石墨
二氧化硫 -> 副产物 1% 氧，1% 硫粉
```

OrbitalRing 额外资源：

```text
黄铁矿
铀矿
石墨矿
```

自动补充规则：

- 遍历 `LDB.veins.dataArray`，如果矿脉产物还没有复制配方，则自动创建。
- 遍历 `LDB.items.dataArray`，若物品类型是 `EItemType.Resource` 且被任一原版配方使用或产出，并且还没有复制配方，则自动创建。
- 若输入物价值为 `maxValue`，不创建配方。
- 副产物价值为 `maxValue` 时从副产物列表移除。

质能裂变启用时，矿物复制产物携带 10 点增产点；否则为 0。

### 3. ConversionRecipe：相关链条转化

设计目标：

- 只允许有明确关系的物品链互转。
- 以价值守恒为基础，把随机输出逐步收束成可规划产线。
- 禁止把普通中间件做成万能等价交换池。

共同参数：

```text
RecipeType = Conversion
GrowthRole = Production
SuccessRatio = 0.05
OutputAppend = 空
```

链条生成函数 `CreateChain(levels)`：

```text
levels = 多层物品列表，每层可以有多个同层物品
移除不存在或价值为 maxValue 的物品
移除空层
若只剩一个物品，跳过

对每个输入 input in levels[i]:
    候选输出 = levels[i-1] + levels[i] + levels[i+1] 中除自身以外的所有物品
    每个候选基础权重 = itemValue[target] * target.StackSize
    下层候选权重 *= 0.8
    同层候选权重 *= 1.0
    上层候选权重 *= 1.25
    每个候选路径概率 successRatio = 1 / 候选数量
    totalWeight = 所有候选权重之和
    allocatedValue = itemValue[input] * candidateWeight / totalWeight
    outputCount = allocatedValue / (successRatio * itemValue[target])
```

这保证一次成功后的期望价值围绕输入价值分配，同时通过层级权重偏向更高层输出。

当前链条目录：

```text
物流运输：
  配送运输机 -> 物流运输机 -> 星际物流运输船

黑雾材料：
  能量碎片 -> 黑雾矩阵 -> 物质重组器 -> 硅基神经元 -> 负熵奇点 -> 核心素

电力输送：
  电力感应塔 -> 无线输电塔 -> 卫星配电站

能量建筑：
  风力涡轮机/太阳能板/GB同位素温差发电机
  -> 蓄电器/能量核心
  -> 能量枢纽/星际能量枢纽
  -> 星际能量枢纽MK2

发电厂：
  GenesisBook: 燃料电池发电厂 -> 地热发电站 -> 裂变能源发电站 -> 朱曦K型人造恒星 -> 湛曦O型人造恒星
  Vanilla: 火力发电厂 -> 地热发电站 -> 微型聚变发电站 -> 人造恒星

传送带：
  传送带 -> 高速传送带 -> 极速传送带

物流辅助：
  四向分流器/流速监测器/GB大气采集站/喷涂机/自动集装机

仓储：
  小型储物仓 -> 大型储物仓 -> GB量子储物仓
  储液罐 -> GB量子储液罐

物流站：
  物流配送器 -> 行星内物流运输站 -> 星际物流运输站/MS物资交换物流站 -> 轨道采集器

分拣器：
  分拣器 -> 高速分拣器 -> 极速分拣器 -> 集装分拣器

采矿和油井：
  SmelterMiner 且非 CustomCreateBirthStar:
    采矿机 -> 熔炉采矿机A/B/化工采矿机C -> 大型采矿机 -> 大型熔炉采矿机A/B/大型化工采矿机C
    原油萃取站/原油精炼厂 -> 等离子精炼油井
  其他:
    采矿机 -> 大型采矿机
    原油萃取站/原油精炼厂

抽水：
  抽水站 -> GB聚束液体汲取设施

化工厂：
  GenesisBook: 化工厂 -> GB先进化学反应釜
  Vanilla: 化工厂 -> 量子化工厂

熔炉：
  电弧熔炉 -> GB等离子熔炉 -> 位面熔炉 -> 负熵熔炉

制造台：
  GenesisBook: GB基础制造台 -> GB标准制造单元 -> GB高精度装配线 -> GB物质重组工厂
  Vanilla: 制造台MkI -> 制造台MkII -> 制造台MkIII -> 重组式制造台

研究站：
  矩阵研究站 -> 自演化研究站

射线/发射/对撞：
  MoreMegaStructure: 电磁轨道弹射器/MS射线重构站/垂直发射井/微型粒子对撞机
  Vanilla: 电磁轨道弹射器/射线接收站/垂直发射井/微型粒子对撞机

GenesisBook 特殊建筑：
  物质裂解塔/天穹装配厂/埃克森美孚化工厂/物质分解设施/工业先锋精密加工中心/苍穹粒子加速器

燃料棒：
  GenesisBook:
    空燃料棒 -> 液氢燃料棒 -> 焦油燃料棒 -> 四氢双环戊二烯燃料棒/铀燃料棒
    -> 钚燃料棒 -> 氘核燃料棒/MOX燃料棒 -> 氦三燃料棒
    -> 反物质燃料棒/氘氦混合燃料棒 -> 奇异湮灭燃料棒
  OrbitalRing:
    OR化学燃料棒 -> OR铀燃料棒 -> 氘核燃料棒 -> 反物质燃料棒 -> 奇异湮灭燃料棒
  Vanilla:
    液氢燃料棒 -> 氘核燃料棒 -> 反物质燃料棒 -> 奇异湮灭燃料棒

增产剂：
  增产剂MkI -> 增产剂MkII -> 增产剂MkIII

战斗单位：
  原型机 -> 精准无人机/攻击无人机 -> 护卫舰 -> 驱逐舰 -> MS水滴

能量武器：
  高频激光塔/GB紫外激光塔/近程电浆塔/磁化电浆炮

战场辅助：
  战场分析基站/信号塔/干扰塔/行星护盾发生器

防御塔：
  高斯机枪塔/聚爆加农炮/GB电磁加农炮/导弹防御塔

弹药和胶囊：
  GenesisBook:
    机枪弹箱 -> 钢芯弹箱 -> 超合金弹箱 -> 钨芯弹箱 -> 三元弹箱 -> 湮灭弹箱
    燃烧单元 -> 爆破单元 -> 核子爆破单元 -> 反物质湮灭单元
    炮弹组 -> 高爆炮弹组 -> 微型核弹组 -> 反物质炮弹组
    导弹组 -> 超音速导弹组 -> 引力导弹组 -> 反物质导弹组
    干扰胶囊 -> 压制胶囊
    等离子胶囊 -> 反物质胶囊
  OrbitalRing:
    机枪弹箱 -> OR钢芯弹箱 -> OR贫铀弹箱 -> OR零素矢
    OR炸药单元 -> OR金属氢单元
    OR杀爆榴弹组 -> OR金属氢炮弹组
    导弹组 -> 超音速导弹组 -> OR战术核导弹 -> OR启示录聚变弹 -> OR重力鱼雷
    干扰胶囊 -> 压制胶囊
    OR氘核轨道弹 -> OR反物质轨道弹
  Vanilla:
    机枪弹箱 -> 钛化弹箱 -> 超合金弹箱
    燃烧单元 -> 爆破单元 -> 晶石爆破单元
    炮弹组 -> 高爆炮弹组 -> 晶石炮弹组
    导弹组 -> 超音速导弹组 -> 引力导弹组
    干扰胶囊 -> 压制胶囊
    等离子胶囊 -> 反物质胶囊
```

单路锁定：

- 转化塔 12 级启用。
- 只有同一配方中存在多个可锁定产物时才建立锁定方案。
- 锁定目标的输出数量不使用原随机路径权重，而是：

```text
lockedOutputCount = itemValue[input] / itemValue[lockedOutput]
```

- 损毁、成功率、保留输入、翻倍产出仍走原通用公式。
- 锁定只把“成功后随机选哪条输出”替换为指定输出。

### 4. RectificationRecipe：矩阵萃取与精华调相

设计目标：

- 把原版矩阵转成 FE 内部实体精华。
- 允许精华压缩、回流、拆成残片。
- 让精华作为阶段催化材料参与配方成长。

精馏配方分两类：

```text
MatrixExtraction  # 矩阵萃取
EssenceTuning     # 矩阵精华调相
```

共同参数：

```text
RecipeType = Rectification
GrowthRole = SpecialGrowth
SuccessRatio = 1.0
Rectification 不直通：非损毁即成功，输入总是消耗
```

矩阵萃取输入：

```text
电磁矩阵
能量矩阵
结构矩阵
信息矩阵
引力矩阵
宇宙矩阵
黑雾矩阵
```

矩阵萃取输出：

```text
电磁矩阵 -> 100% 电磁精华 x1
能量矩阵 -> 100% 能量精华 x1
结构矩阵 -> 100% 结构精华 x1
信息矩阵 -> 100% 信息精华 x1
引力矩阵 -> 100% 引力精华 x1
宇宙矩阵 -> 100% 宇宙精华 x1
黑雾矩阵 -> 75% 信息精华 x1 + 25% 引力精华 x1
```

矩阵萃取实际数量：

```text
runtimeCount = output.OutputCount
             * GetStageDecayFactor(InputID)
             * RectificationTower.PlrRatio
下限 = 0.0001
```

矩阵萃取损毁率按配方等级：

```text
Lv0 4.0%
Lv1 3.0%
Lv2 2.0%
Lv3 1.0%
Lv4 0.5%
Lv5 0.0%
```

再减去：

```text
抽取单位回响 * 0.2%
成就损毁减免
```

精华调相输入是六种矩阵精华。

基础参数：

```text
BaseCompressionRatio = 0.45
TicketSplitRatio = 0.10
remainingRatio = 0.90
```

精华调相输出概率：

```text
若不是最高阶：
    向上压缩输出下一阶精华
    inputLevel == 0 时概率 = 0.90
    其他中间阶概率 = 0.90 * 0.45 = 0.405
    基础数量 = 0.5

若不是最低阶：
    向下回流输出上一阶精华
    inputLevel == 最高阶时概率 = 0.90
    其他中间阶概率 = 0.90 * 0.55 = 0.495
    基础数量 = 2.0

残片拆票：
    概率 = 0.10
    数量 = 输入精华面值
```

若概率总和因边界层级不是 1，则归一化。

精华调相运行数量按配方等级覆盖：

```text
向上压缩数量：
Lv0 0.45
Lv1 0.46
Lv2 0.48
Lv3 0.50
Lv4 0.51
Lv5 0.52

向下回流数量：
Lv0 1.80
Lv1 1.88
Lv2 1.96
Lv3 2.00
Lv4 2.06
Lv5 2.12
```

非残片输出还会乘以回响加成：

```text
runtimeCount *= 1 + 抽取单位回响 * 0.005
```

精华调相损毁率为 0。

定向调相：

- 精馏塔可设置 `CurrentTuningTargetId`。
- 只对 `EssenceTuning` 有效。
- 若目标在当前配方输出列表中，则成功输出固定为该目标。

## 六、建筑等级与塔特质

### 1. 等级和突破

所有四塔最高 12 级。

运行成功给对应建筑组增加经验：

```text
每次成功 +1 建筑经验
```

等级经验表：

```text
0 -> 1   200
1 -> 2   500
2 -> 3   需要突破
3 -> 4   1000
4 -> 5   2200
5 -> 6   需要突破
6 -> 7   5000
7 -> 8   9000
8 -> 9   需要突破
9 -> 10  16000
10 -> 11 28000
11 -> 12 需要突破
```

突破点：

```text
Lv2  -> 消耗当前阶段矩阵精华 x1，残片 x36
Lv5  -> 消耗当前阶段矩阵精华 x2，残片 x120
Lv8  -> 消耗当前阶段矩阵精华 x4，残片 x360
Lv11 -> 消耗当前阶段矩阵精华 x8，残片 x960
```

### 2. 通用倍率

默认最大堆叠：

```text
Lv0-5   1
Lv6-8   4
Lv9-11  8
Lv12    12
```

实际四塔堆叠读取 `StackingManager.GetFractionatorMaxStack()`，但目标分档沿用上表。

分馏塔能耗倍率：

```text
Lv0      1.00
Lv1-3    0.95
Lv4-6    0.85
Lv7-9    0.70
Lv10-12  0.50
```

增产点倍率：

```text
Lv0-1    1.0
Lv2-4    1.1
Lv5-7    1.3
Lv8-10   1.6
Lv11-12  1.8
```

特质阈值：

```text
Lv3  流动输入增强
Lv6  第一特质
Lv12 第二特质
```

### 3. 交互塔

职责：

- 跑 `BuildingTrainRecipe`。
- 作为数据中心上传入口。
- 通过献祭当前数据中心里的实体塔，给四塔提供成功率加成。

特质：

```text
Lv3  EnableFluidEnhancement
Lv6  EnableSacrificeTrait
Lv12 EnableDimensionalResonance
```

献祭触发：

```text
GameMain.FixedUpdate
gameTick % 60 == 3
玩家已加载
交互塔等级 >= 6
```

每次献祭对四类实体塔分别执行：

```text
若数据中心该塔数量 < FractionatorSacrificeThreshold -> 不献祭
否则取当前数量的 10%
```

有效献祭数量：

```text
effectiveCount = sacrificedCount * (1 + 塔家族回响 * 0.05)
```

Lv12 维度共振：

```text
buffCount = 本次有献祭的塔种数量
effectiveCount *= 1 + 0.1 * buffCount
```

成功率加成：

```text
rawBoost = sqrt(effectiveCount) / 10
cap = Lv12 ? 1.00 : 0.75
clamped = min(rawBoost, cap)
stepIndex = floor(clamped / 0.05)
SuccessBoost = stepIndex * 0.05
```

四类塔各自使用自己塔种献祭得到的 `SuccessBoost`。

献祭加成不是存档长期数值；进入其他存档或未满足特质时会重置。

### 4. 矿物复制塔

职责：

- 跑 `MineralCopyRecipe`。
- 在后期形成资源自循环。

特质：

```text
Lv3  EnableFluidEnhancement
Lv6  EnableMassEnergyFission
Lv12 EnableZeroPressureCycle
```

质能裂变：

```text
poolTarget = fluidInputCount * 15
pointsPerItem = Lv12 零压循环启用 ? 40 : 25

若裂变点池 <= 0:
    消耗输入物品，按 pointsPerItem 补池到 poolTarget

若输入平均增产点 < 10:
    从池中扣点，把全部输入平均增产点尽量补到 10
```

零压循环：

```text
zeroPressureStack = min(MaxStack, 8)
fluidInputTarget = MaxBeltSpeed * zeroPressureStack
fluidOutputTarget = 2 * zeroPressureStack
```

循环顺序：

```text
1. 已有 fluidOutputCount 先回补输入，侧边输出带不能抢在自循环前出货。
2. 主产物中与输入同 ID 的资源先补 fluidInput 到目标。
3. 输入目标满足后，剩余主产物补 fluidOutput 到内部目标。
4. 仍有剩余才保留为普通产物输出。
```

矿物复制产物增产点：

```text
EnableMassEnergyFission ? 10 : 0
```

### 5. 转化塔

职责：

- 跑 `ConversionRecipe`。
- 把相关链条随机转化逐步收束为可规划产线。

特质：

```text
Lv3  EnableFluidEnhancement
Lv6  EnableCausalTracing
Lv12 EnableSingleLock
```

因果溯源：

```text
对每批 DestroyedCount:
    savedDestroyed = Binomial(DestroyedCount, 0.5)
    InputRemoveCount -= savedDestroyed
    ConsumedRegisterCount -= savedDestroyed
    返还对应输入增产点
```

单路锁定：

- 见 `ConversionRecipe` 章节。
- 锁定目标保存在实体级扩展状态里。
- 塔清空后仍保留锁定预设；当特质未启用时清除。

### 6. 精馏塔

职责：

- 跑 `RectificationRecipe`。
- 把矩阵、矩阵精华、残片和成长催化连接起来。

特质：

```text
Lv3  EnableFluidEnhancement
Lv6  EnableAfterglowExtraction
Lv12 EnableHyperphaseCompression
```

当前稳定运行效果：

- Lv3 走通用流动输入增强。
- 精馏配方本身通过等级表降低矩阵萃取损毁率，提高调相输出数量。
- 精馏配方通过抽取单位回响进一步降低矩阵萃取损毁率、提升非残片调相输出。
- 定向调相由塔扩展状态和 `CurrentTuningTargetId` 实现。

`EnableAfterglowExtraction` 与 `EnableHyperphaseCompression` 是塔特质开关名，设计上对应精馏后期萃取/压缩能力；具体数值效果目前主要落在精馏配方等级、增产点倍率和定向调相路径上。

## 七、配方成长

### 1. 配方状态

每个配方保存：

```text
Level              当前等级，0 表示未解锁
GrowthExp          普通成长经验
PityProgress       保底/怜悯进度，精馏使用
UnlockSourceFlags  科技、抽取、加工、黑雾等来源标记
LastTouchedTick    最近改变 tick
```

最高等级通常是 5。

旧版有效等级映射：

```text
存储 Lv1 -> 有效旧等级 2
存储 Lv2 -> 有效旧等级 4
存储 Lv3 -> 有效旧等级 6
存储 Lv4 -> 有效旧等级 8
存储 Lv5 -> 有效旧等级 10
```

### 2. 家族与规则

成长家族：

```text
BuildingTrainForward      原胚/定向原胚输入
BuildingTrainReverse      实体塔输入
MineralCopyNormal         普通资源复制
MineralCopyDarkFog        黑雾物品复制
ConversionItemChain       普通转化链
ConversionDarkFogChain    黑雾转化链
ConversionBuilding        建筑物转化链
Rectification             精馏配方
```

规则表：

```text
BuildingTrainForward:
  mode = Hybrid
  max = 5
  techBaseline = 2
  drawUnlock = 2
  usesGrowthExp = true

BuildingTrainReverse:
  mode = ProcessExp
  max = 5
  techBaseline = 1
  drawUnlock = 1
  usesGrowthExp = true

MineralCopyNormal:
  mode = ProcessExp
  max = 5
  techBaseline = 电磁3 / 能量2 / 结构1 / 其他0
  drawUnlock = baseline > 0 ? baseline : 1
  usesGrowthExp = true

MineralCopyDarkFog:
  mode = ProcessExp
  max = 5
  techBaseline = 0
  drawUnlock = 1
  usesGrowthExp = true

ConversionItemChain:
  mode = ProcessExp
  max = 5
  techBaseline = 0
  drawUnlock = baseline > 0 ? baseline : 1
  usesGrowthExp = true

ConversionDarkFogChain:
  mode = ProcessExp
  max = 5
  techBaseline = 0
  drawUnlock = 1
  usesGrowthExp = true

ConversionBuilding:
  mode = FixedMax
  max = 5
  drawUnlock = 5
  fixedMaxReward = true
  不靠加工升级

Rectification:
  mode = ProcessExpWithPity
  max = 5
  techBaseline = 1
  drawUnlock = 1
  usesPity = true
```

升级阈值：

```text
BuildingTrain: 12, 20, 34, 56, 90
Production:    16, 28, 48, 80, 132
DarkFog:       12, 20, 34, 56, 90
Rectification: 14, 24, 42, 72, 120
```

### 3. 等级带来的加工概率

对已解锁配方：

```text
remainInputRatio = min(0.95, effectiveLegacyLevel * 0.08 + 回响保留加成)
doubleOutputRatio = min(0.75, effectiveLegacyLevel * 0.05 + 成就翻倍加成 + 回响翻倍加成)
```

即不考虑额外加成时：

```text
Lv1 -> 16% 保留输入，10% 主产物翻倍
Lv2 -> 32% 保留输入，20% 主产物翻倍
Lv3 -> 48% 保留输入，30% 主产物翻倍
Lv4 -> 64% 保留输入，40% 主产物翻倍
Lv5 -> 80% 保留输入，50% 主产物翻倍
```

### 4. 加工经验

加工时基础经验：

```text
gain = inputCount
```

按家族增加成功奖励：

```text
BuildingTrainForward: gain += successCount * 6
BuildingTrainReverse: gain += successCount * 4
MineralCopyNormal:    gain += successCount * 2
ConversionItemChain:  gain += successCount * 2
MineralCopyDarkFog:   gain += successCount * 2，再走黑雾追赶倍率
ConversionDarkFog:    gain += successCount * 2，再走黑雾追赶倍率
Rectification:        PityProgress += gain
```

当累计经验或 `PityProgress` 达到当前等级阈值，就升级并扣除阈值。

### 5. 抽取奖励

抽到配方或抽取单位时：

```text
若配方已满级:
    返还残片
    常规 15
    常规 + 精馏经济聚焦 20
    速通 25
    速通 + 精馏经济聚焦 35

若配方未解锁:
    FixedMaxReward -> 直接设为 MaxLevel
    否则设为 DrawUnlockLevel

若已解锁且规则使用经验或 pity:
    manualCatchup = 当前等级阈值 * (速通 ? 2 : 1) / 2
    加入 GrowthExp 或 PityProgress

其他:
    等级 +1 或直接 MaxLevel
```

抽取会设置 `UnlockSourceFlags.Draw`。

### 6. 科技和黑雾基线

科技基线保证：

- 分馏塔原胚科技：保证 `BuildingTrain` 的基础解锁。
- 矿物复制科技：保证基础矿物复制配方达到阶段基线；稀有矿和黑雾材料不走这个普通基线。
- 精馏科技：保证精馏配方基础解锁。
- 特定转化链由对应科技解锁时补足基线。

黑雾掉落：

- 若配方家族是 `MineralCopyDarkFog` 或 `ConversionDarkFogChain`。
- 且物品已解锁或玩家持有该输入。
- 则未解锁配方提升到 Lv1，并标记 `DarkFogDrop`。

### 7. 精华催化

成长报价可以消耗矩阵精华催化精馏配方：

```text
catalystStage = GetMatrixEssenceLevel(essenceItemId)
对所有 Rectification 配方:
    配方矩阵阶段 <= catalystStage
    已解锁
    未满级
    -> 加入 growthExp
```

催化经验：

```text
essenceFaceValue * (速通 ? 4 : 3)
```

## 八、抽取、聚焦、成长报价和回响

### 1. 抽取池

卡池：

```text
Pool 0 OpeningLine  主抽取路线偏好
Pool 1 ProtoLoop    主抽取原胚偏好
Pool 2 Growth       成长积分和成长报价，不直接随机抽
Pool 3 Focus        聚焦枚举展示，不直接随机抽
```

只有 Pool 0 和 Pool 1 是抽取池。

抽取消耗：

```text
每抽消耗当前进度矩阵 x1
```

每抽都会：

```text
记录保底
Pool 2 成长积分 +1
按稀有度解析奖励
```

稀有度基础概率：

```text
C = 80.9%
B = 15.0%
A = 3.5%
S = 0.6%
```

S 保底：

```text
第 1-73 抽：0.6%
第 74-89 抽：每抽额外 +6%
第 90 抽：必出 S
```

### 2. 聚焦

聚焦类型：

```text
Balanced              平衡发展
MineralExpansion      复制扩张
ConversionLeap        转化跃迁
LogisticsInteraction  交互物流
EmbryoCycle           原胚循环
ProcessOptimization   工艺优化
RectificationEconomy  精馏经济
```

切换聚焦：

```text
常规模式：切换到不同聚焦消耗残片 120
速通模式：0
```

路线抽取权重倍率：

```text
主命中倍率：常规 1.4，速通 1.6
侧命中倍率：常规 1.2，速通 1.3
```

命中规则：

- 矿物复制聚焦提高 `MineralCopy`。
- 转化跃迁提高 `Conversion`。
- 精馏经济提高 `Rectification`。
- 交互物流提高物流类配方。
- 原胚循环提高未解锁配方。
- 工艺优化提高当前矩阵阶段配方。

聚焦成长报价：

```text
折扣 = 常规 0.80，速通 0.85
核心奖励额外 +1
若核心奖励是残片，再额外 +10
```

### 3. OpeningLine 池构建

路线偏好池只消费生产型配方：

```text
MineralCopy
Conversion
部分 Rectification opening unit
```

不直接放入：

- `BuildingTrain` 工具型配方。
- 黑雾矩阵阶段的生产配方。
- 纯特殊成长入口。

分组规则：

```text
MineralCopy:
  基础资源组、流体组、稀有资源组等按 draw unit 聚合。

Conversion:
  按输出链连通关系 union-find 聚合为 ConversionChain。

Rectification:
  按矩阵精华家族或黑雾矩阵家族聚合。
```

配方基础权重：

```text
MineralCopyNormal:     常规 100，速通 120
ConversionItemChain:   常规 100，速通 120
ConversionBuilding:    常规 40，速通 32
Rectification:         常规 46，速通 58
```

权重调整：

```text
未解锁配方：常规 *1.5，速通 *1.8
当前阶段：常规 *1.3，速通 *1.5
速通下前一阶段且未满级：*1.25
聚焦倍率：见聚焦章节
满级配方：常规 *0.35，速通 *0.20
```

抽取单位权重：

```text
unitWeight = 平均配方权重 + min(recipeCount, 6) * (常规 8 / 速通 10)
若单位已全解锁且回响 < 3:
    常规 *1.12
    速通 *1.18
```

Pool 0 稀有度内容：

```text
常规：
  C: 残片
  B: 前阶段单位；没有前阶段则当前阶段
  A: 当前阶段单位；没有则 B
  S: 当前阶段未解锁单位；没有则当前阶段或 A

速通：
  C/B/A: 当前阶段单位；没有则前阶段或未解锁当前阶段
  S: 当前阶段未解锁单位；没有则目标单位
```

抽到抽取单位后选择目标配方：

```text
1. 优先单位内第一条未解锁配方
2. 否则选未满级且等级最低的配方
3. 等级相同选 InputID 更小的配方
4. 都满级则走重复返残片或回响
```

### 4. ProtoLoop 池构建

Pool 1 包含：

```text
四类有效塔原胚
定向原胚
```

同一加权列表放入 C/B/A/S 全稀有度池。

抽到原胚后：

- 物品进入数据中心。
- 如果该原胚映射到 `TowerFamily` 抽取单位，还会对对应 `BuildingTrain` 配方应用抽取奖励。
- 若单位已全解锁且回响未满，则重复抽取提升回响。

### 5. 成长报价

普通模式基础报价：

```text
5 成长积分 -> 残片 50
10 成长积分 + 残片 10 -> 当前阶段矩阵 x4
20 成长积分 + 残片 15 -> 当前聚焦原胚 x1
36 成长积分 + 残片 30 -> 定向原胚 x1
22 成长积分 + 残片 14 + 当前阶段精华 x1 -> 精馏催化经验
```

速通模式基础报价：

```text
4 成长积分 -> 当前阶段矩阵 x6
8 成长积分 + 残片 6 -> 当前聚焦原胚 x1
15 成长积分 + 残片 10 -> 定向原胚 x1
14 成长积分 + 残片 8 + 当前阶段精华 x1 -> 精馏催化经验
```

黑雾成长报价在黑雾阶段解锁后追加：

```text
信号接触:
  能量碎片 catchup，消耗黑雾矩阵 x1

地面压制:
  物质重组器 catchup，消耗黑雾矩阵 x2
  硅基神经元 catchup，消耗黑雾矩阵 x2
  重组式制造台 Conversion 成长，消耗黑雾矩阵 x2
  自演化研究站 Conversion 成长，消耗黑雾矩阵 x2

星域围猎:
  负熵奇点 catchup，消耗黑雾矩阵 x3
  负熵熔炉 Conversion 成长，消耗黑雾矩阵 x3

奇点收束:
  核心素 catchup，消耗黑雾矩阵 x4
  奇异湮灭燃料棒 Conversion 成长，消耗黑雾矩阵 x4

增强层奇点且增强节点 >= 2:
  定向原胚 x1，消耗黑雾矩阵 x4
```

黑雾 catchup 基础经验：

```text
信号接触 12
地面压制 16
星域围猎 22
奇点收束 30
```

### 6. 回响

抽取单位全解锁后，重复抽到该单位时可提升回响，最高 3。

回响效果：

```text
BuildingTrainForward/Reverse:
  主产物翻倍加成 = 回响 * 0.006

MineralCopyNormal:
  主产物翻倍加成 = 回响 * 0.010

ConversionItemChain:
  保留输入加成 = 回响 * 0.010

Rectification:
  矩阵萃取损毁率 -= 回响 * 0.002
  非残片调相输出数量 *= 1 + 回响 * 0.005

TowerFamily:
  献祭有效数量 *= 1 + 回响 * 0.05
```

## 九、数据中心与物流交互站

### 1. 数据中心库存

数据中心保存实体数量和增产点：

```text
centerItemCount[itemId]
centerItemInc[itemId]
leftInc  # 全局增产点池
```

上传：

```text
AddItemToModData(itemId, count, inc)
centerItemCount += count
centerItemInc += inc
若 itemId 是有效分馏塔，则触发科技解锁条件检查
```

提取：

```text
TakeItemFromModData(itemId, count, out inc)
```

提取时增产点分配：

```text
若当前平均增产点 >= 4:
    按 split_inc 等比例拆分
否则:
    优先给提取物补到每个 4 点
    若不足则取走剩余全部增产点
```

沙土特殊处理：数据中心读写沙土直接映射玩家 `sandCount`。

### 2. 献祭取塔

交互塔献祭调用：

```text
Take10PercentTower(itemId)
```

规则：

```text
只接受有效四塔实体 itemId
数据中心数量低于 FractionatorSacrificeThreshold -> 返回 0
否则提取 count / 10
```

这保证献祭只消耗当前实体库存，不读取历史累计上传量。

### 3. 物流交互站

物流交互站不是分馏塔，不跑 `BaseRecipe`。

它负责：

- 把物流站槽位物品上传到数据中心。
- 从数据中心下载物品到物流站槽位。
- 双向同步。
- 用容量模式限制或放开上传。
- 用等级降低交互能耗、提高堆叠/补点能力。

槽位传输模式：

```text
Sync      双向同步
Upload    仅上传
Download  仅下载
```

容量模式：

```text
Limited   有限上传，受数据中心目标数量限制
Infinite  无限上传
```

阈值来自配置：

```text
downloadThreshold = Miscellaneous.DownloadThreshold
uploadThreshold = Miscellaneous.UploadThreshold
```

运行逻辑：

```text
Upload:
  若槽位数量 > max * uploadThreshold:
      上传超出部分

Download:
  若总供应量 < max * downloadThreshold:
      从数据中心下载到 round(max * downloadThreshold)

Sync:
  先按上传阈值裁剪
  否则按下载阈值补足
```

交互耗电：

```text
costPerItem = sqrt(itemValue[itemId]) * 10000 * station.InteractEnergyRatio
```

若单槽剩余电力不足，则按电力裁剪搬运数量。

交互站每搬运 1 个物品，给物流交互站建筑组增加 1 经验。

交互站能耗倍率：

```text
Lv0      1.00
Lv1      0.95
Lv2-3    0.85
Lv4      0.70
Lv5-6    0.55
Lv7      0.40
Lv8-9    0.30
Lv10     0.25
Lv11-12  0.20
```

### 4. 增产点池

增产点池属于物流交互站支撑能力，不是第五条分馏主线。

点池来源是数据中心里的增产剂：

```text
增产剂MkI   基础使用次数 12，基础点数 1
增产剂MkII  基础使用次数 24，基础点数 2
增产剂MkIII 基础使用次数 60，基础点数 4
```

增产剂自身携带点数时，总可用点数按原版增产表放大：

```text
totalPoints[mk, carriedInc] =
    floor(baseUseCount[mk] * (1 + Cargo.incTableMilli[carriedInc])) * basePoints[mk]
```

交互站下载补点：

```text
Lv3+:
  先消耗点池，把下载物品按 1:1 补到平均 4 点

Lv12+:
  在已达到 4 点后，继续以 3 点池成本换 1 点物品增产
  最高补到平均 10 点
```

上传不会凭空增加点池；点池只来自数据中心中的实体增产剂。

## 十、主线任务与成就

### 1. 主线阶段

主线任务使用 8 个阶段列：

```text
起步
电磁矩阵
能量矩阵
结构矩阵
信息矩阵
引力矩阵
宇宙矩阵
黑雾支线
```

常规模式闭环：

```text
解锁配方 >= 100
总分馏成功 >= 5000
```

速通模式闭环：

```text
解锁配方 >= 60
总分馏成功 >= 3000
```

主线分支：

```text
矩阵阶段:
  分馏数据中心科技
  电磁/能量/结构/信息/引力/宇宙矩阵
  超值礼包 1-6

低档分馏:
  总成功 1 / 10 / 50 / 100 / 200 / 300
  最终闭环

低档抽取:
  路线偏好 1 / 5 / 10 / 20 / 50
  原胚偏好 1 / 5 / 10

成长规划:
  解锁第 1 个配方
  任意配方达到 Lv2
  切换到非均衡聚焦
  解锁至少 1 项黑雾成长报价
  完成至少 1 次市场板订单

原胚建筑:
  持有 1 类原胚
  持有 3 类原胚
  持有 4 类原胚或定向原胚
  任意建筑 Lv1
  四塔均 Lv1
  上传任意 1 类实体塔
  上传四类实体塔

低档建筑等级:
  任意建筑 Lv1 / Lv2 / Lv3 / Lv4 / Lv6

低档配方:
  解锁配方 1 / 3 / 5 / 10 / 20 / 30 / 40

资源交互:
  解锁物品交互科技
  数据中心手动提取 1 次
  数据中心手动上传 1 次
  完成市场订单 1 次
  完成残片兑换 1 次

黑雾早期:
  持有或解锁黑雾矩阵
  黑雾资源层级 1
  黑雾阶段达到信号接触
  黑雾资源层级 2
  黑雾阶段达到地面压制
```

任务只读取事实并发放一次性奖励，不能作为稳定产线。

### 2. 成就

成就是全存档共享状态，不按单个存档隔离。

成就分类：

```text
生产
开线
配方
成长
黑雾
挑战
```

生产成就：

```text
总分馏成功:
  100,000,000
  1,000,000,000
  10,000,000,000

历史峰值分馏速率:
  100,000 / min
  1,000,000 / min
  10,000,000 / min
```

开线成就：

```text
路线偏好抽取:
  100
  1000
  10000
```

配方成就：

```text
解锁配方:
  1 / 3 / 5 / 10 / 20 / 30 / 60 / 100 / 120 / 150
```

成长成就：

```text
任意建筑等级:
  1 / 2 / 3 / 4 / 6 / 8 / 10 / 12
```

黑雾成就：

```text
信号接触
地面压制
星域围猎
奇点收束
```

挑战成就：

```text
基础闭环:
  总成功 >= 5000
  解锁配方 >= 60
  任意建筑 >= Lv6

全域工艺:
  总成功 >= 20000
  解锁配方 >= 100
  任意建筑 >= Lv8

万物归一:
  总成功 >= 50000
  解锁配方 >= 100
  任意建筑 >= Lv10

常规毕业:
  非速通
  总成功 >= 30000
  解锁配方 >= 150
  解锁星际物流交互科技

速通毕业:
  速通
  总成功 >= 10000
  路线偏好抽取 >= 500
  解锁星际物流交互科技
```

成就可提供：

- 成功率加成。
- 损毁减免。
- 主产物翻倍加成。
- 能耗减免。
- 物流加成。
- 发电阶段加成。
- 一次性物品奖励。

成就被动加成通过 `GachaGalleryBonusManager` 和 `Achievements` 被分馏热路径、精馏、物流等系统读取。

## 十一、UI 职责

UI 是规划和确认层，不是生产层。

主要入口：

```text
CoreOperate/FracRecipeOperate:
  展示配方、等级、产物、概率、锁定/调相目标和成长信息。

CoreOperate/GlobalGrowthOperate:
  展示建筑成长、突破、全局进度。

DrawGrowth/TicketRaffle:
  主抽取路线偏好和原胚偏好。

DrawGrowth/LimitedTimeStore:
  成长报价、黑雾补差、精华催化。

DrawGrowth/TicketExchange:
  残片兑换和有限补差。

ResourceInteraction:
  数据中心库存、市场板、资源交互。

ProgressTask/MainTask:
  主线阶段和任务路线图。

ProgressTask/Achievements:
  成就、被动加成和一次性奖励。

Archive:
  统计、图鉴、开发日志和教程解释。
```

UI 可以：

- 发起抽取。
- 切换聚焦。
- 购买成长报价。
- 选择锁定输出或调相目标。
- 手动上传/提取数据中心物品。
- 展示概率、成长和任务状态。

UI 不能：

- 绕过 `ProcessManager` 直接生产分馏产物。
- 绕过 `RecipeGrowthExecutor` 直接改配方等级。
- 绕过数据中心凭空生成实体库存。
- 把矩阵直接当 FE 成长货币扣掉。
- 让市场或任务替代稳定产线。

## 十二、设计红线

- 分馏塔负责自动化生产，UI 只负责规划、展示和确认。
- 四条配方线必须放在同一个系统闭环里理解，不能拆成互不相干的“自动化主线”介绍。
- `BuildingTrain`、`MineralCopy`、`Conversion`、`Rectification` 是配方类型，不是四个彼此隔离的游戏模式。
- 数据中心保存实体库存，不保存历史累计幻觉。
- 交互塔献祭消耗当前实体塔库存，不读取历史上传量。
- `Conversion` 只允许相关链条转化，不能扩展为万能等价交换。
- 普通中间件和制造材料不能因为“价值相近”就进入任意转化池。
- 精馏只产出矩阵精华和残片，不产出源点。
- 源点不能由稳定产线、市场、精馏链或普通配方产出。
- 市场、成长报价、任务和成就是补差/引导/奖励层，不能替代产线。
- 增产点池是物流交互站支撑能力，不是第五条分馏路线。
- 黑雾系统只提供阶段、掉落、资源层级和战斗事实；抽取、成长、市场、任务各自解释这些事实。
- 后期允许净正收益，但必须来自明确输入、阶段门槛、成长投入和产线时间。

## 十三、复写实现的最小顺序

如果从本文复写当前分馏域，按以下顺序实现：

```text
1. 定义 ID、矩阵阶段、物品价值和矩阵精华面值。
2. 定义 ERecipe、RecipeKey、OutputInfo、ProductOutputInfo。
3. 实现 BaseRecipe 三段判定和批量二项近似。
4. 实现四个配方子类及其配方生成表。
5. 实现四塔目录、建筑等级、突破、倍率和特质开关。
6. 实现 ProcessManager 热路径、产物缓存、输送带和四塔特质。
7. 实现配方成长状态、规则、运行经验、抽取奖励和精华催化。
8. 实现 GachaManager/GachaService 的保底、聚焦、池构建、成长报价和回响。
9. 实现 DataCenterInventory、Take10PercentTower、交互站运行和增产点池。
10. 实现主线任务、成就被动加成和 UI 操作入口。
11. 最后接入兼容层、存档导入导出、Nebula 同步和展示细节。
```

这个顺序也是系统依赖顺序：后面的 UI、任务和市场只能调用前面的事实源，不能倒过来成为生产或成长事实源。
