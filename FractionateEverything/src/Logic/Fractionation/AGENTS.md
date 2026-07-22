# Logic/Fractionation — 分馏域

分馏域是 FE 核心玩法逻辑，包含配方、文明状态的运行投影、分馏塔运行热路径、实例状态和分馏塔窗口表现层 patch。

## Structure

```
Fractionation/
├── FracRecipes/    # BaseRecipe、ERecipe、RecipeManager、具体分馏配方
├── Fractionators/  # 分馏塔定义、科技运行投影与单塔实例状态
├── Process/        # 分馏器运行热路径、能耗 patch、性能探针
└── Presentation/   # 分馏塔窗口、brief info、配方显示相关 UI patch
```

## Recipe Rules

- `BaseRecipe.GetOutputs()` 是共享热路径，禁止直接为单个需求改它。
- 新配方类型放 `FracRecipes/NewRecipe.cs`，继承 `BaseRecipe`，并在 `RecipeManager.AddFracRecipes()` 注册。
- `OutputMain` 是主产物，`OutputAppend` 是副产物。
- `fluidInputInc` 必须沿输出链路传递，不能吞掉增产点。
- `FracRecipes/Runtime/` 只保存文明域投影过来的配方可用性和固定加成；不得引用文明业务对象。
- 旧线性配方等级、经验、突破和重复抽取增幅已经删除，不得在运行缓存中恢复这些概念。

## Process Rules

- `Process/ProcessManager.cs` 保持核心 `InternalUpdate<T>` 集中可读。
- 完整处理流程内部的传送带输入输出 helper 留在 `Process/ProcessManager.cs`。
- 能耗 IL patch 放 `Process/PowerPatch.cs`。
- 性能探针和日志桶放 `Process/Perf.cs`。
- 旧献祭成功率、建筑等级和裂变池逻辑已经删除，不得重新接入热路径。

## Fractionator Rules

- 分馏塔定义、科技运行投影和单塔实例状态放 `Fractionators`。
- `TowerRuntimeModifierCache` 是科技树投影结果，不保存、不自行计算科技条件。
- 五项能力彼此保持独立；对每一项能力，四塔必须接入同一份控制代码、状态字段和 UI。每塔布尔值可以不同，但不得按塔型或配方属性省略整项能力。
- 主路锁定和副产物弃置采用“塔型科技许可 + 配方累计成功校准 + 单塔实例设置”三层约束。
- 四塔主路目标统一使用 `LockedOutput` 实例状态；解析谱系只是解析配方提供的候选与结算语义，不得另建 active 状态或独立 UI 路径。
- 当前配方没有可选主产物或副产物时显示当前无效果，不隐藏对应能力状态。
- 主路锁定只处理 `OutputMain`；副产物弃置只处理 `OutputAppend`，不得提供补偿。
- 单塔实例状态必须接入存档、复制粘贴、蓝图和 Nebula 同步。

## Presentation Rules

- 分馏塔原生窗口 partial 放 `Presentation/FractionatorWindow/`，brief info、配方显示 patch 直接放 `Presentation`。
- 这里只处理表现层和窗口交互；核心配方/运行状态不得写进 Presentation。
