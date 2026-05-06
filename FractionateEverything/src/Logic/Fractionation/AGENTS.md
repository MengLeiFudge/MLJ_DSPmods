# Logic/Fractionation — 分馏域

分馏域是 FE 核心玩法逻辑，包含配方、配方成长、分馏塔运行热路径、实例状态和分馏塔窗口表现层 patch。

## Structure

```
Fractionation/
├── Recipes/        # BaseRecipe、ERecipe、RecipeManager、具体配方
├── Growth/         # 配方成长状态、规则、执行器、快照
├── Process/        # 分馏器运行热路径、传送带 IO、能耗 patch、性能探针
├── State/          # 分馏塔实例状态：输出扩展、单路锁定、裂变点池、共鸣
└── Presentation/   # 分馏塔窗口、brief info、配方显示相关 UI patch
```

## Recipe Rules

- `BaseRecipe.GetOutputs()` 是共享热路径，禁止直接为单个需求改它。
- 新配方类型放 `Recipes/NewRecipe.cs`，继承 `BaseRecipe`，并在 `RecipeManager.AddFracRecipes()` 注册。
- `OutputMain` 是主产物，`OutputAppend` 是副产物。
- `fluidInputInc` 必须沿输出链路传递，不能吞掉增产点。

## Process Rules

- `Process/ProcessManager/ProcessManager.cs` 保持核心 `InternalUpdate<T>` 集中可读。
- 传送带输入输出 helper 放 `Process/ProcessManager/Belts.cs`。
- 能耗 IL patch 放 `Process/ProcessManager/PowerPatch.cs`。
- 性能探针和日志桶放 `Process/ProcessManager/Perf.cs`。
- 交互塔献祭相关成功率逻辑放 `Process/ProcessManager/Sacrifice.cs`。

## State Rules

- 分馏塔实例级状态放 `State`，并接入 `BuildingManager`/相关 manager 的存档聚合。
- 转化塔单路锁定相关实体、复制粘贴、蓝图同步在 `FractionatorSingleLock.cs`。

## Presentation Rules

- 分馏塔原生窗口 partial 放 `Presentation/FractionatorWindow/`，brief info、配方显示 patch 直接放 `Presentation`。
- 这里只处理表现层和窗口交互；核心配方/运行状态不得写进 Presentation。
