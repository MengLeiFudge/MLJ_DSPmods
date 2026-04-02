# TODO

2.3 核心重构已完成。以下仅保留仍未完成的后续项。

## 抽卡主玩法

- [ ] 重新平衡 `流派聚焦`
  - 当前 `复制扩张` / `转化跃迁` 会直接影响开线池权重，`交互物流` / `原胚循环` / `精馏经济` 主要只影响原胚侧，导致主玩法推进速度明显不均衡
  - 需要让每个聚焦方向都至少有一个“开线推进收益”或“成长池独占收益”，避免出现固定最优路线
- [ ] 明确 `UP / 聚焦` 语义
  - 当前实现实际上是“聚焦加权”，不是传统意义的 UP 池
  - `GachaResult.IsUp` / `HitUpMainTarget` 还未真正接入逻辑，后续要么补完整，要么统一改名避免误导
- [x] 补全池积分系统
  - 现在只有成长池积分真正可消费，开线池/原胚池积分主要是展示值
  - 需要决定：要么给不同池积分增加独立用途，要么只保留成长池积分，删掉其余展示
- [x] 处理速通模式 `FocusAffinity` 可被免费快速叠满的问题
  - 当前反复切换/点击同一方向即可堆高权重，容易形成过强的单路线压制
  - 需要增加衰减、冷却，或改成“仅在切换到不同方向时结算”
- [ ] 重新审视成长池报价
  - 当前成长池主循环已经成立，但不同 offer 的性价比与聚焦方向的联动还不够明显
  - 需要让成长池更明确承担“定向补差”而不是“弱化版商店”
- [x] 检查 `GachaGrowthOffer.FocusType`
  - 目前更多只是语义标签，尚未变成真实的成长方向加成入口
  - 需要决定：是否让聚焦页 / 成长页根据 `FocusType` 提供额外折扣、数量提升或刷新偏置

## 成长 / 进度

- [x] 修正“已解锁配方数量”统计口径
  - 当前统计把 `PointAggregate` 这类默认满级/非主线成长配方也算进去了，会导致主线任务、成就、进度判断失真
  - 需要只统计真正参与主线成长的配方类型
- [ ] 重新审视任务与成就的阈值
  - 在修正配方统计口径后，重新校准 `20/50/100` 抽、`50/5000/50000` 分馏、`60/100/150` 配方等关键阈值
  - 常规模式与速通模式应明确区分“压缩时长”与“压缩内容”，不能只是简单缩数值
- [ ] 校验建筑成长节奏
  - 目前普通等级 + 关键节点突破的框架正确，但需要评估 `2/5/8/11` 突破成本与 `3/6/12` 特质开启是否匹配中后期产出
- [ ] 重新平衡高等级特质强度
  - 交互塔 `牺牲特性` 跳变过大
  - 矿物复制塔 `零压循环`
  - 转化塔 `因果追踪 / 单路锁定`
  - 点数聚集塔 `虚空喷涂 / 双倍点数`
  - 需要防止“过阈值后直接变成压倒性最优”
- [x] 明确原版配方增强的开放节奏
  - 当前“落后一层”设计方向正确，但“下一层全部科技研究完成”对普通流程可能过严
  - 需要决定是否改成“核心科技完成”或“阶段完成度达到一定比例”

## 动态经济系统

### 系统边界

- [x] 保留 `基础价值 BaseValue`
  - 继续使用当前 `ItemManager.itemValue`
  - 只服务核心平衡：转化配方等价、物流交互站耗电、内部成长数值
  - **禁止**直接改成动态值
- [x] 新增 `市场价值 MarketValue`
  - 只服务 `市场板` / `交易所`
  - 允许动态波动，但不能反向污染主系统平衡
- [x] 新增 `残片兑换 FragmentExchange`
  - 与交易所无关
  - 作为稳定保底通道存在，允许玩家用残片换取常规物品

### 设计目标

- [x] 让“不同阶段物品的真实紧缺度”进入玩法，而不是永远使用固定价值
- [x] 给后期玩家一个可长期游玩的资源统筹 / 投机系统
- [x] 忽略该系统的玩家也能正常完成 FE 主线成长，不强制参与
- [x] 让 `残片` 成为真正意义上的基础货币，而不仅是成长副资源

### 一、市场价值（底层数据层）

- 为每个物品新增 `MarketMultiplier`
  - 建议区间：`0.35 ~ 3.50`
  - `MarketValue = BaseValue × MarketMultiplier`
- `MarketMultiplier` 的计算来源：
  - 当前阶段权重
  - 数据中心库存
  - 原版分析面板已有的 `当前制作速率`
  - 原版分析面板已有的 `理论最大制作速率`
  - 小幅随机扰动
- 需要加入平滑机制
  - 不允许价格每次刷新直接跳到目标值
  - 使用旧值与目标值按比例混合，避免暴涨暴跌
- 需要给关键保值物品设置最低保值线
  - 如：宇宙矩阵、黑雾矩阵、定向原胚、核心类物品

### 二、市场板（面向所有玩家）

- 定位
  - 限时订单 / 机会单
  - 低理解成本
  - 高收益但非必需
- 每次刷新 4~6 条订单
  - 高价收购
  - 折价抛售
  - 阶段补给
  - 稀有特单
- 市场板价格参考 `MarketValue`，但不能简单等于交易所当前价格
  - 它应该是“带折扣/溢价的系统报价”
- 市场板需要承担的作用
  - 给普通玩家稳定提供“看得懂的好价”
  - 给后期玩家提供刷单空间
  - 给定向成长物、黑雾支线物资留一个可控的额外入口

### 三、交易所（面向后期玩家）

- 定位
  - 股票式随机涨跌系统
  - 玩家不能准确预测涨跌
  - 但玩家能通过大额买卖实体物品影响价格
- 第一版不要全量开放所有物品
  - 先挑 24~36 个代表性物品上市
  - 分阶段覆盖：原矿、熔炼物、中间件、矩阵、少量黑雾材料
- 交易所价格应具备：
  - 当前价
  - 涨跌幅
  - 上次价格
  - 成交量 / 热度
- 玩家交易冲击应当只影响短期价格
  - 允许“短时做局”
  - 但禁止永久控盘
- 需要加入：
  - 单次交易冲击上限
  - 单周期最大涨跌幅上限
  - 自动回归机制

### 四、残片兑换（稳定保底系统）

- 定位
  - 与交易所无关
  - 随时可用
  - 价格稳定
- 规则
  - 玩家可以用残片兑换任意常规已解锁物品
  - 价格始终参考 `BaseValue` + 阶段系数 + 稳定溢价
  - 不随交易所即时涨跌而波动
- 残片兑换必须永远比“好行情”贵
  - 它是保底，不是套利入口
- 需要明确不能进入残片兑换的物品
  - 定向原胚
  - 分馏配方核心
  - 其他会直接打穿抽卡 / 成长池闭环的关键成长物

### 五、阶段性落地方案

- [x] Phase 1：实现 `MarketValueManager`
  - 新增 `MarketMultiplier` / `MarketValue`
  - 读取原版统计面板已有的 `当前制作速率` 与 `理论最大制作速率`
  - 不接 UI，不改已有玩法，只做数据层与调试输出
- [x] Phase 2：实现 `残片兑换`
  - 先做稳定保底兑换
  - 验证 `BaseValue` 与阶段系数是否合理
  - 确保不会替代成长池 / 抽卡
- [x] Phase 3：实现 `市场板`
  - 做限时订单
  - 引入高价收购 / 折价抛售 / 阶段补给 / 稀有特单
  - 作为普通玩家可选玩法入口
- [x] Phase 4：实现 `交易所`
  - 先开放有限物品池
  - 做价格波动、买卖、短期冲击、回归机制
  - 再决定是否加入更复杂的历史曲线 / 分类筛选 / 风险提示
- [ ] Phase 5：整体验证与再平衡
  - 检查市场系统对主线抽卡、成长池、原版增强、黑雾支线是否产生意外替代
  - 只允许它提高上限，不允许它成为唯一正确玩法

### 六、阶段性复核与提交规则

- 每个 Phase 完成后，必须先做一次**高精度复核**
  - 代码层：检查边界、状态流、旧系统耦合点、是否误改 `BaseValue`
  - 数值层：检查价格上下限、是否可套利、是否存在明显固定最优行为
  - UI 层：检查术语是否清楚，避免“交易所 / 市场板 / 残片兑换”概念混淆
  - 玩法层：确认忽略市场的玩家仍能正常推进 FE 主线
- 高精度复核通过后，才能进行该 Phase 的 git 提交
- 提交要求
  - 一个 Phase 至少一个独立 commit
  - 若 Phase 内发现复核问题，先修完再提交，不允许“已实现但未复核”的状态进入历史
- 推荐工作流
  - 设计 / 实现
  - 本地验证
  - 高精度复核
  - 小修
  - 再验证
  - 提交

### 七、可直接开工的技术设计

#### 7.1 模块拆分与文件规划

- `FractionateEverything/src/Logic/Manager/MarketValueManager.cs`
  - 负责维护 `MarketMultiplier` / `MarketValue`
  - 负责定时刷新
  - 负责读取库存、阶段、制作速率并计算目标价格
  - 负责导出调试信息给 UI
- `FractionateEverything/src/Logic/Manager/MarketBoardManager.cs`
  - 负责生成限时订单
  - 负责订单刷新、过期、购买结算
  - 负责与 `MarketValueManager` 解耦，只读取快照
- `FractionateEverything/src/Logic/Manager/ExchangeManager.cs`
  - 负责交易所上市物品列表
  - 负责买入、卖出、价格冲击、回归
  - 负责成交历史与短期价格状态
- `FractionateEverything/src/Logic/Manager/FragmentExchangeManager.cs`
  - 负责残片兑换报价
  - 负责合法物品过滤、稳定价格计算、兑换结算
- `FractionateEverything/src/UI/View/ResourceInteraction/MarketBoard.cs`
  - 市场板 UI
- `FractionateEverything/src/UI/View/ResourceInteraction/Exchange.cs`
  - 交易所 UI
- `FractionateEverything/src/UI/View/ResourceInteraction/FragmentExchange.cs`
  - 残片兑换 UI
- `FractionateEverything/src/UI/View/ResourceInteraction/ResourceOverview.cs`
  - 资源统筹 / 高需求低需求展示
- `FractionateEverything/src/UI/View/MainWindowPageRegistry.cs`
  - 注册上述页面
- `FractionateEverything/src/FractionateEverything.cs`
  - 在初始化、Start、Import/Export、IntoOtherSave 流程中接入新 manager

#### 7.2 核心数据结构

- `MarketValueManager`
  - `public static readonly float[] BaseValue = ItemManager.itemValue;`
  - `public static readonly float[] MarketMultiplier = new float[12000];`
  - `public static readonly float[] MarketValue = new float[12000];`
  - `public static readonly float[] LastTargetMultiplier = new float[12000];`
  - `public static readonly float[] LastCurrentRate = new float[12000];`
  - `public static readonly float[] LastMaxRate = new float[12000];`
  - `public static readonly long[] LastCenterSnapshot = new long[12000];`
  - `private static long lastRefreshTick;`
  - `private static int refreshVersion;`
- `MarketBoardManager`
  - `public readonly struct MarketOffer`
  - 字段建议：
    - `OfferId`
    - `OfferType` (`BuyFromPlayer` / `SellToPlayer` / `StageSupply` / `Special`)
    - `InputItemId`
    - `InputCount`
    - `ExtraInputItemId`
    - `ExtraInputCount`
    - `OutputItemId`
    - `OutputCount`
    - `ExpireTick`
    - `RefreshVersion`
  - `private static readonly List<MarketOffer> activeOffers = [];`
- `ExchangeManager`
  - `public readonly struct ExchangeTicker`
  - 字段建议：
    - `ItemId`
    - `LastPrice`
    - `BidPrice`
    - `AskPrice`
    - `DayOpenPrice`
    - `DayHighPrice`
    - `DayLowPrice`
    - `LastTradeTick`
    - `NetPlayerVolume`
    - `RecentPlayerBuyVolume`
    - `RecentPlayerSellVolume`
  - `private static readonly Dictionary<int, ExchangeTicker> tickers = [];`
  - `private static readonly HashSet<int> listedItems = [];`
- `FragmentExchangeManager`
  - `public readonly struct FragmentQuote`
  - 字段建议：
    - `ItemId`
    - `FragmentCost`
    - `StageWeight`
    - `BaseValue`
    - `CanExchange`
  - `private static readonly Dictionary<int, FragmentQuote> quotes = [];`

#### 7.3 与现有系统的硬边界

- `ItemManager.itemValue`
  - 只能当作 `BaseValue`
  - **禁止**被动态经济系统覆写
- `ConversionRecipe` / `StationManager`
  - 继续读取 `BaseValue`
  - **禁止**读取 `MarketValue`
- `MarketValueManager`
  - 只读 `ItemManager.itemValue`
  - 只读数据中心库存
  - 只读原版统计面板已有速率
  - **禁止**反向修改核心生产逻辑
- `FragmentExchangeManager`
  - 不读取交易所即时成交价
  - 只读 `BaseValue`、阶段、稳定系数

#### 7.4 价格计算公式（第一版）

- 先计算目标倍率 `TargetMultiplier`

```text
TargetMultiplier
= StageFactor
× StockFactor
× ThroughputFactor
× RandomShock
```

- 阶段因子 `StageFactor`
  - 依赖 `ItemManager.itemToMatrix` 与 `GetCurrentProgressMatrixId()`
  - 建议：
    - 当前阶段：`1.00 ~ 1.35`
    - 下一阶段预备：`1.05 ~ 1.60`
    - 低一阶段：`0.70 ~ 0.90`
    - 低两阶段及以下：`0.35 ~ 0.65`
    - 保值物品最低不低于 `0.80`
- 库存因子 `StockFactor`
  - 只看 `ItemManager.centerItemCount[itemId]`
  - 建议第一版使用对数衰减，不做分段表
  - 必须设置下限，避免库存大时价格直接跌到不可玩
- 产能利用因子 `ThroughputFactor`
  - 使用原版统计面板已有：
    - 当前制作速率 `CurrentRate`
    - 理论最大速率 `MaxRate`
  - `Utilization = CurrentRate / max(MaxRate, epsilon)`
  - 对不可制造物 / 无速率物品直接使用 `1.0`
- 随机扰动 `RandomShock`
  - 第一版建议控制在 `±8%`
  - 只制造“市场味”，不能主导价格

- 平滑更新：

```text
NewMultiplier
= OldMultiplier * 0.75
+ TargetMultiplier * 0.25
```

- 最终夹紧：

```text
0.35 <= MarketMultiplier <= 3.50
```

#### 7.5 交易所价格模型

- `MidPrice = BaseValue × MarketMultiplier`
- `BidPrice = MidPrice × 0.96`
- `AskPrice = MidPrice × 1.04`
- 玩家交易冲击
  - 买入：提升 `MidPrice`
  - 卖出：压低 `MidPrice`
  - 建议只影响短周期状态，不永久影响基准倍率
- 冲击模型建议
  - 使用 `sqrt(volume)`，避免大额交易无限放大
  - 单次交易价格冲击上限建议 `±12%`
  - 单刷新周期总涨跌幅上限建议 `±20%`
- 回归机制
  - 每次刷新都向 `BaseValue × MarketMultiplier` 回归
  - 防止玩家永久控盘

#### 7.6 残片兑换价格模型

- `FragmentCost = BaseValue × StageWeight × SafetyPremium`
- `StageWeight`
  - 当前阶段：`1.0`
  - 低阶段：`0.8 ~ 0.9`
  - 高阶段：`1.2 ~ 1.8`
- `SafetyPremium`
  - 第一版固定 `1.35 ~ 1.60`
- 约束
  - 残片兑换价应始终劣于市场板大多数好单
  - 残片兑换价不跟交易所即时价格联动
  - 定向原胚、核心类、关键成长物不进残片兑换常驻池

#### 7.7 物品分类策略

- `ExchangeListed`
  - 可上交易所
  - 第一版控制在 24~36 个物品
- `FragmentExchangeAllowed`
  - 可残片兑换
  - 常规已解锁物品
- `MarketBoardSpecial`
  - 不进入常规兑换，但可出现在市场板稀有特单
  - 如：定向原胚、黑雾关键素材、阶段工具物
- `HardBlocked`
  - 禁止进入任何动态经济系统
  - 如：仅调试物品、纯提示物、会打穿成长闭环的关键道具

#### 7.8 刷新节奏与运行时接入

- `MarketValueManager`
  - 每 10 分钟小刷新一次
  - 推荐使用 `GameMain.gameTick` 驱动
- `MarketBoardManager`
  - 常规模式：60 分钟刷新
  - 速通模式：20 分钟刷新
- `ExchangeManager`
  - 实时读取当前价格
  - 每次买卖即时写入冲击
  - 每次 `MarketValueManager` 刷新时统一回归
- 接入点建议
  - `FractionateEverything.Start()`：初始化
  - `GameMain.FixedUpdate` 或现有定时 Tick：周期刷新
  - `FractionateEverything.Import/Export`：持久化

#### 7.9 存档设计

- 在 `FractionateEverything.Export` / `Import` 中新增一个总块，如：
  - `("Economy", EconomyManager.Export)`
- 若拆分 manager，则 `EconomyManager` 只做聚合导出导入
- 存档内容建议：
  - `MarketMultiplier`
  - `refreshVersion`
  - `lastRefreshTick`
  - `activeOffers`
  - `listed tickers` 的短期状态
  - `player exchange history`（若后续要做）
- 不必持久化的内容
  - `BaseValue`
  - 可即时重算的静态分类结果

#### 7.10 UI 设计

- `ResourceOverview`
  - 展示：
    - 当前高需求 Top5
    - 当前低需求 Top5
    - 当前阶段建议卖出物
    - 当前阶段建议买入物
  - 面向普通玩家，不展示复杂曲线
- `MarketBoard`
  - 展示：
    - 当前订单列表
    - 剩余刷新时间
    - 推荐标签
- `Exchange`
  - 展示：
    - 物品图标、名称
    - 当前价、涨跌幅
    - 买一 / 卖一价格
    - 当前持仓（可选）
    - 买入 / 卖出数量输入
  - 第一版不必做完整 K 线
  - 用近三次价格 + 涨跌箭头即可
- `FragmentExchange`
  - 展示：
    - 分类筛选
    - 搜索
    - 稳定兑换价
    - 当前库存 / 当前残片余额

#### 7.11 每个 Phase 的验收标准

- Phase 1 验收
  - `BaseValue` 未被动态覆写
  - `MarketMultiplier` 可刷新
  - 当前阶段 / 库存 / 速率变化会影响 `MarketValue`
  - 无 UI 依赖也能独立跑通
- Phase 2 验收
  - 常规物品可残片兑换
  - 关键成长物不会错误进入兑换池
  - 无法通过“残片兑换 -> 主循环关键物”直接跳过抽卡 / 成长池
- Phase 3 验收
  - 市场板能正常刷新
  - 订单价格与 `MarketValue` 有相关性
  - 稀有特单不会刷到破坏主线平衡
- Phase 4 验收
  - 交易所价格可涨可跌
  - 玩家交易能短时影响价格
  - 价格会自动回归
  - 不存在简单无限套利
- Phase 5 验收
  - 忽略市场系统的玩家仍可正常毕业
  - 会玩市场系统的玩家能获得效率优势，但不是唯一正确路线
  - 不会反向压制现有抽卡 / 成长 / 建筑 / 配方系统

#### 7.12 高精度复核清单（执行时照抄）

- 代码复核
  - 是否误用了 `ItemManager.itemValue` 作为动态值
  - 是否把交易所逻辑写进了主生产逻辑热路径
  - 是否有未持久化但应持久化的状态
- 数值复核
  - 是否存在买卖同物品无脑套利
  - 是否存在某个阶段物品价格长期锁死在上限/下限
  - 是否存在单个市场板条目收益过高，直接替代主线玩法
- UI 复核
  - “市场板 / 交易所 / 残片兑换”概念是否清楚
  - 是否让普通玩家看不懂就直接放弃页面
- 玩法复核
  - 是否会让玩家只玩市场、不玩 FE 主玩法
  - 是否会让市场系统在前期抢戏
  - 是否会让关键成长物失去原有获取意义

## UI / 体验

- [ ] 分馏统计面板
- [ ] 抽卡 / 成长 / 聚焦页增加更明确的收益说明
  - 当前需要玩家自己脑补不同方向的差异，UI 没把“这个方向到底强在哪”说清楚
- [ ] 抽卡结果页增加更有效的信息
  - 当前仅显示稀有度和结果摘要，后续应显示“是否命中当前聚焦”“是否推进当前阶段开线”“重复配方转化了什么”
- [x] 新增经济系统相关 UI
  - 资源统筹页
  - 市场板
  - 交易所
  - 残片兑换
- [ ] 配方筛选、排序、搜索
- [ ] 新的物品购买式封装 UI
- [ ] MyImageButton 显示优化
- [ ] 确认弹窗开关
- [ ] Mod 介绍图标
- [ ] 宣传视频

## 代码质量

- [ ] 建筑等级阈值提取常量
- [ ] `ProcessManager` 热路径预索引优化
- [ ] 抽卡相关状态与展示字段清理
  - 未使用或半使用字段：`IsUp`、`HitUpMainTarget`、部分池积分展示
- [ ] 将抽卡 / 成长 / 聚焦的设计约束补成代码注释或子目录文档
  - 避免后续继续出现“UI 有语义，逻辑没接上”的情况
