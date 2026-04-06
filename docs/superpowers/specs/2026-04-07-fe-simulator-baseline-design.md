# FE 模拟器基线对照设计

## 目标

把当前 `VanillaCurveSim` 从单一“原版主线时间线模拟器”扩展为双层模型：

1. 保留现有原版输出作为 `baseline`
2. 新增 FE 数值验证层与 FE 流程场景输出作为 `treatment`
3. 明确产出“分馏影响度”，用于衡量 FE 对推进、资源与收益的实际影响

## 为什么要这样改

当前模拟器的主目标是原版推进：

- `Simulator.cs:L58` 写死原版终局科技
- `Program.cs:L7-L13` 直接运行 `RunAll()`
- `ReportWriter.cs:L26-L74` 输出阶段时间线、瓶颈、电力、里程碑

但 FE 的真正平衡口径不在现有模拟器里，而在模组代码：

- 建筑等级、突破阈值、堆叠与特性：`BuildingManager.cs:L16-L25`
- 配方成功率、损毁率、重复处理、翻倍产出：`FracRecipeOperate.cs:L248-L269`、`FracRecipeOperate.cs:L460-L469`
- 抽卡成本、保底、成长兑换与奖励：`GachaManager.cs:L35-L38`、`GachaManager.cs:L69-L105`、`GachaService.cs:L138-L149`、`GachaService.cs:L171-L209`、`GachaService.cs:L549-L633`

如果继续往现有 `VanillaCurveSimulator` 里直接堆逻辑，最终会变成流程、平衡、抽卡、报告全部耦合的大文件，不利于校准和维护。

## 设计结论

采用 **C2 双层架构**：

- **底层：FE 数值验证层**
  - 负责计算 FE 建筑/配方/抽卡在给定状态下的期望收益
  - 不负责时间线推进
- **上层：流程场景层**
  - 保留 `baseline`
  - 新增 `FE 常规`、`FE 速通`
  - 只消费底层输出的标准化指标
- **报告层**
  - 统一输出 baseline / treatment 对照
  - 单独输出“分馏影响度”

## 第一阶段范围

第一阶段只纳入最影响资源平衡的 FE 系统：

- 五类分馏塔
  - 交互塔
  - 矿物复制塔
  - 点数聚集塔
  - 转化塔
  - 精馏塔
- 配方强化 / 成就加成 / 增产点数
- 抽卡系统
  - 开线池
  - 原胚闭环池
  - 成长池兑换
  - 聚焦切换成本
  - 保底与 S 概率曲线

第一阶段不引入其余 FE 系统作为完整仿真对象，只允许它们作为常量或边界条件被读取。

## 基线与实验组

### Baseline

沿用现有原版输出，不改其目标：

- `Conventional`
- `Speedrun`

仍然输出当前 `StrategySimulationResult` 结构。

### Treatment

新增两组 FE 场景：

- `FE Conventional`
- `FE Speedrun`

其流程目标不是“证明 FE 能通关”，而是回答：

- FE 是否显著压缩推进时间
- FE 是否制造资源正反馈失控
- 抽卡与成长是否压过正常造线
- 某类塔/配方/特性是否变成唯一最优

## 影响指标

### 主指标：分馏影响度

定义：

`FractionationImpact = baseline_total_time / fe_total_time`

解释：

- `1.00`：无影响
- `1.10`：轻度增强
- `1.30`：明显增强
- `1.60+`：需重点复核

### 副指标 1：资源净增益倍率

用于衡量 FE 是否把资源曲线推爆：

- 单位原料等效产出提升
- 单位矩阵抽卡净值
- 单位残片成长兑换净值
- 单位建筑/单位电力净收益

### 副指标 2：综合影响指数

对以下项做归一化加权：

- 推进压缩率
- 资源净增益倍率
- 能耗收益比
- 抽卡净收益
- 配方强化边际收益

第一版用固定权重即可，不引入外部配置。

## 新的数据结构

在 `Models.cs` 中新增 FE 专用模型，避免污染现有 `StrategySimulationResult`：

- `SimulationMode`
  - `BaselineVanilla`
  - `FeConventional`
  - `FeSpeedrun`
- `FractionationConfigSnapshot`
  - 五塔等级
  - 聚焦类型
  - 抽卡模式
  - 成就/强化摘要
- `FractionationEffectMetrics`
  - `FractionationImpact`
  - `ResourceGainMultiplier`
  - `EnergyEfficiencyMultiplier`
  - `GachaNetValuePerMatrix`
  - `GrowthExchangeNetValue`
  - `CompositeImpactIndex`
- `FractionationScenarioResult`
  - 场景名
  - 模式
  - 配置快照
  - 指标集合
  - 关键结论列表
  - 可选流程摘要
- `SimulationComparisonReport`
  - baseline 结果
  - treatment 结果
  - 对照差异

## 新的模块拆分

第一阶段建议在 `VanillaCurveSim/src/` 新增以下文件：

- `FeModels.cs`
  - FE 专用数据结构
- `FeReference.cs`
  - 把 FE 真实公式固化为模拟器可消费的引用层
  - 来源注明对应代码位置
- `FeGachaEvaluator.cs`
  - 负责矩阵消耗、保底概率、抽卡结算、成长池兑换净值
- `FeWarehouse.cs`
  - 负责模拟仓库与配方奖励状态
- `FeGachaKernel.cs`
  - 尽量按 FE `GachaService.Draw` / `ResolveReward` / `TryExchangeGrowthOffer` 口径执行抽卡与成长兑换
- `FeFractionationEvaluator.cs`
  - 负责五塔、配方强化、成就、增产点数的期望产出/能耗/吞吐评估
- `FeScenarioSimulator.cs`
  - 负责 `FE Conventional` / `FE Speedrun` 两个流程场景
- `ComparisonReporter.cs`
  - 输出 baseline 与 treatment 的对照报告

现有文件调整：

- `Program.cs`
  - 改成先跑 baseline，再跑 FE treatment，再汇总输出
- `ReportWriter.cs`
  - 保留原版报告能力，同时新增 comparison 输出
- `Models.cs`
  - 保留原有结构；只新增最小共享枚举，避免把 FE 模型塞进原结构
- `Simulator.cs`
  - 尽量不再继续承载 FE 公式
  - 只保留 baseline 原版流程模拟职责

## FE 公式处理原则

必须优先复用 FE 代码的真实口径，不允许在模拟器里重新发明一套“差不多”的公式。

第一版直接按以下逻辑对齐：

- 建筑等级阈值、突破、堆叠、特性开启：
  - `BuildingManager.cs:L16-L25`
- 配方成功率与损毁率展示口径：
  - `FracRecipeOperate.cs:L248-L269`
- 等效平均产出：
  - `FracRecipeOperate.cs:L460-L469`
- 开线池 / 原胚池矩阵成本：
  - `GachaService.cs:L138-L149`
- 成长池报价：
  - `GachaService.cs:L171-L209`
- 抽卡实际主循环：
  - `GachaService.cs:L549-L579`
- 抽卡奖励解析：
  - `GachaService.cs:L599-L633`
- 保底概率：
  - `GachaManager.cs:L69-L105`

抽卡层不采用纯净值黑箱估算，而是采用“模拟仓库 + FE 抽卡核心逻辑搬运”的方式：

- 用 `FeWarehouse` 模拟矩阵、残片、原胚、定向原胚、成长池积分、配方奖励状态
- 用 `FeGachaKernel` 执行：
  - 抽卡扣矩阵
  - 保底递进
  - 奖励入仓
  - 成长池积分累加
  - 成长池兑换

这样即使后续模组内容变化，只要同步池构建与奖励规则，模拟结果也不会因为“手工净值估算”而失真。

## 输出报告

第一阶段报告分成 3 个文件：

1. 现有原版报告
   - `vanilla-strategy-report.json`
   - `vanilla-strategy-report.md`
2. FE 对照报告
   - `fe-impact-report.json`
   - `fe-impact-report.md`
3. 汇总说明
   - 明确 baseline / treatment
   - 明确主指标和判定区间

Markdown 报告至少包含：

- baseline 与 treatment 总推进时间
- 分馏影响度
- 每阶段资源净增益倍率
- 每阶段抽卡净值
- 哪些系统贡献了最大压缩率
- 哪些系统有失衡风险

## 验证策略

当前仓库没有现成测试工程，第一阶段采用“最小自校验 + 整解构建验证”：

1. 在 `VanillaCurveSim` 内新增可直接运行的最小自校验逻辑
   - 验证分馏影响度公式
   - 验证抽卡保底概率曲线
   - 验证成长池净值计算
2. 运行 `VanillaCurveSim` 生成报告
3. 按仓库要求运行整解 `MSBuild`
4. 成功后启动 `AfterBuildEvent.exe`

## 非目标

第一阶段不做：

- 游戏内实时读取存档状态
- 所有 FE 子系统的全量建模
- 把 FE 公式反射调用进模拟器运行时
- 动态可配置权重系统
- UI 页面联动展示

## 推荐实施顺序

1. 先保住 baseline，不改原版结果口径
2. 再抽出 FE 公式层与抽卡期望层
3. 再补 FE 常规 / FE 速通两个 treatment 场景
4. 最后生成 comparison 报告与分馏影响度
