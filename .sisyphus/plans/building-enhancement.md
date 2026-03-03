# 建筑强化系统修改

## TL;DR

> **Quick Summary**: 根据 TODO.md 的"建筑分析"设计，修复2处数值不一致，添加自动喷涂开关，实现3个分馏塔特殊特质（虚空喷涂、双重点数、单路锁定）。
> 
> **Deliverables**:
> - A1: 星际物流交互站 InteractEnergyRatio 与行星站统一
> - A2: 分馏塔流动输出最小堆叠改为4
> - B1: 物流交互站自动喷涂添加 Level >= 3 开关
> - B2: 验证集装可调层数已正确实现
> - C5: 点数聚集塔 +6 虚空喷涂特质
> - C6: 点数聚集塔 +12 双重点数特质
> - C8: 转化塔 +12 单路锁定特质
> 
> **Estimated Effort**: Medium
> **Parallel Execution**: YES - 3 waves
> **Critical Path**: Task 1-4 (parallel) → Task 5-6 (parallel) → Task 7a-7d (sequential) → Final

---

## Context

### Original Request
根据 FractionateEverything/TODO.md 中"# 建筑分析"部分的设计，对建筑强化系统进行代码修改。

### Interview Summary
**Key Discussions**:
- 对比了 TODO 设计规格与当前代码，确认 EnergyRatio/PlrRatio/MaxProductOutputStack 已匹配
- InterstellarInteractionStation.InteractEnergyRatio 与设计不一致
- 流动输出集装+3 的含义：最少输出4个（`Clamp` 下限从1改为4），超过4则跟随传送带
- 自动喷涂逻辑（`AddIncToItem`）已在 StationManager 实现，仅需开关控制
- 集装调节 slider UI 已在 StationManager 完整实现，无需改动
- C7（无损转移）从范围中移除，因为用户不希望修改 GetOutputs
- C1-C4（交互塔/矿物复制塔特质）留后续

**Research Findings**:
- 所有塔建筑类遵循相同结构（static class, Level, EnergyRatio, PlrRatio 等）
- `AddIncToItem` 在 PackageUtils.cs:1159 完整实现了自动喷涂逻辑
- `PlanetaryInteractionStation.Level` 是全局 static 字段，`InterstellarInteractionStation.Level` 直接引用它
- 已有 `ConcurrentDictionary<(int,int), T>` 模式可复用（`BuildingManager.outputDic`）
- `buffBonus1/2/3` 在 ProcessManager 中已预留但标记 `// todo`，不在本次范围内

### Metis Review
**Identified Gaps** (addressed):
- C5 注入点应在 `ProcessManager.InternalUpdate` 循环前，而非 `GetOutputs` 中
- C6 语义需要用户确认（消耗减半 vs 产出翻倍 vs 阈值减半）
- C8 需要版本号管理（BuildingManager save version 1→2）
- C8 转化塔切换物品时需清除锁定
- `buffBonus1/2/3` 不可触碰

---

## Work Objectives

### Core Objective
使建筑强化系统的 7 项功能（A1/A2/B1/B2/C5/C6/C8）与 TODO.md 的设计规格一致。

### Concrete Deliverables
- `InterstellarInteractionStation.cs` InteractEnergyRatio 值修正
- `ProcessManager.cs` 两处 Clamp 下限修改
- `StationManager.cs` 自动喷涂开关
- `PointAggregateTower.cs` 新属性 + `PointAggregateRecipe.cs` / `ProcessManager.cs` 逻辑
- `ConversionTower.cs` + `BuildingManager.cs` 单路锁定存储 + `ConversionRecipe.cs` 过滤 + UI

### Definition of Done
- [ ] `dotnet build FractionateEverything/FractionateEverything.csproj` 编译成功，0 error
- [ ] 所有修改符合 TODO.md 设计值
- [ ] C8 存档兼容：旧版存档（version 1）可正常加载

### Must Have
- InterstellarInteractionStation.InteractEnergyRatio = {0.68, 0.44, 0.28, 0.20}
- 流动输出最小堆叠 = 4（当 EnableFluidEnhancement 启用时）
- 自动喷涂仅在 Level >= 3 时激活
- 虚空喷涂在 Level >= 6 时对点数聚集塔生效
- 双重点数在 Level >= 12 时对点数聚集塔生效
- 单路锁定在 Level >= 12 时对转化塔生效，可持久化存储

### Must NOT Have (Guardrails)
- 不得修改 `BaseRecipe.GetOutputs`（它被所有配方类型共享）
- 不得触碰 C1-C4（交互塔特质、矿物复制塔特质）或 C7（无损转移）
- 不得修改 `buffBonus1/2/3` 的预留逻辑（ProcessManager 中标记 `// todo`）
- 不得修改现有 `outputDic` 结构或其 Import/Export 格式
- A1/A2/B1/C5/C6 不得新增 Harmony patch —— 全部在现有代码路径中修改
- 不得修改 `PlanetaryInteractionStation.InteractEnergyRatio`（已正确）
- 不得修改 `EnergyRatio`（所有塔已正确）

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.

### Test Decision
- **Infrastructure exists**: NO（这是 Unity/DSP mod，无单元测试框架）
- **Automated tests**: None
- **Framework**: N/A

### QA Policy
每个 task 的 QA 通过编译验证 + grep/AST 搜索确认修改正确性。
Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.

- **编译验证**: `dotnet build FractionateEverything/FractionateEverything.csproj`
- **代码验证**: grep/ast-grep 搜索确认值/结构正确
- **引用验证**: `lsp_find_references` 确认改动无遗漏

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — trivial fixes, ALL parallel):
├── Task 1: A1 - InterstellarInteractionStation InteractEnergyRatio [quick]
├── Task 2: A2 - ProcessManager 流动输出最小堆叠 [quick]
├── Task 3: B1 - StationManager 自动喷涂开关 [quick]
└── Task 4: B2 - 验证集装可调层数 [quick]

Wave 2 (After Wave 1 — point aggregate traits, parallel):
├── Task 5: C5 - 虚空喷涂 (depends: 1-4 build pass) [unspecified-low]
└── Task 6: C6 - 双重点数 (depends: 1-4 build pass) [unspecified-low]

Wave 3 (After Wave 2 — complex, sequential sub-tasks):
├── Task 7a: C8 存储层 - 单路锁定数据存储 (depends: 5-6) [unspecified-high]
├── Task 7b: C8 逻辑层 - ConversionRecipe 过滤 (depends: 7a) [unspecified-high]
├── Task 7c: C8 UI层 - 分馏塔详情窗口选择 (depends: 7b) [unspecified-high]
└── Task 7d: C8 清理 - 配方切换时清除锁定 (depends: 7b) [quick]

Wave FINAL (After ALL tasks — verification):
├── Task F1: Plan compliance audit (oracle)
├── Task F2: Code quality review (unspecified-high)
├── Task F3: Build + grep verification (unspecified-high)
└── Task F4: Scope fidelity check (deep)

Critical Path: Task 1-4 → Task 5 → Task 7a → 7b → 7c → F1-F4
Max Concurrent: 4 (Wave 1)
```

### Dependency Matrix

| Task | Depends On | Blocks |
|------|-----------|--------|
| 1 (A1) | — | 5, 6 |
| 2 (A2) | — | 5, 6 |
| 3 (B1) | — | 5, 6 |
| 4 (B2) | — | 5, 6 |
| 5 (C5) | 1-4 build | 7a |
| 6 (C6) | 1-4 build | 7a |
| 7a (C8 storage) | 5, 6 | 7b |
| 7b (C8 logic) | 7a | 7c, 7d |
| 7c (C8 UI) | 7b | F1-F4 |
| 7d (C8 cleanup) | 7b | F1-F4 |
| F1-F4 | all | — |

### Agent Dispatch Summary

- **Wave 1**: 4 tasks — T1-T4 → `quick`
- **Wave 2**: 2 tasks — T5-T6 → `unspecified-low`
- **Wave 3**: 4 tasks — T7a-T7c → `unspecified-high`, T7d → `quick`
- **FINAL**: 4 tasks — F1 → `oracle`, F2-F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

- [ ] 1. A1 - 星际物流交互站 InteractEnergyRatio 统一

  **What to do**:
  - 修改 `InterstellarInteractionStation.cs:35-41` 的 `InteractEnergyRatio` switch 表达式
  - 将 `{0.95f, 0.85f, 0.7f, 0.5f}` 改为 `{0.68f, 0.44f, 0.28f, 0.2f}`
  - 与 `PlanetaryInteractionStation.InteractEnergyRatio` 完全一致

  **Must NOT do**:
  - 不修改 `EnergyRatio`（已正确）
  - 不修改 `PlanetaryInteractionStation` 的任何内容

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 2, 3, 4)
  - **Blocks**: Tasks 5, 6
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `InterstellarInteractionStation.cs:35-41` — 需修改的 InteractEnergyRatio switch 表达式
  - `PlanetaryInteractionStation.cs:35-41` — 目标值参考（0.68f, 0.44f, 0.28f, 0.2f）

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: InteractEnergyRatio values corrected
    Tool: Bash (grep)
    Steps:
      1. grep -n "InteractEnergyRatio" InterstellarInteractionStation.cs
      2. Assert: 包含 0.68f, 0.44f, 0.28f, 0.2f
      3. Assert: 不包含 0.95f（在 InteractEnergyRatio 属性中）
    Expected Result: 4个新值全部存在，旧值不存在
    Evidence: .sisyphus/evidence/task-1-interact-energy-ratio.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-1-build.txt
  ```

  **Commit**: YES (groups with 2, 3)
  - Message: `fix(building): unify InteractEnergyRatio and fix fluid output minimum stack`
  - Files: `InterstellarInteractionStation.cs`

- [ ] 2. A2 - ProcessManager 流动输出最小堆叠改为4

  **What to do**:
  - 修改 `ProcessManager.cs` 中两处 `EnableFluidEnhancement` 分支内的 `Clamp` 调用
  - 位置1: 约 line 356 — `Mathf.Clamp(Mathf.CeilToInt(fluidInputCountPerCargo), 1, 20)` → 改 `1` 为 `4`
  - 位置2: 约 line 446 — 同样的修改
  - 含义：当启用流动输出增强时，最少按4个堆叠输出

  **Must NOT do**:
  - 不修改上限20
  - 不修改 `else` 分支（未启用流动输出增强的路径，lines 366-377 和 456-467）
  - 不修改其他任何 ProcessManager 逻辑

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 3, 4)
  - **Blocks**: Tasks 5, 6
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `ProcessManager.cs:353-365` — belt1 的 EnableFluidEnhancement 分支，含第一处 Clamp
  - `ProcessManager.cs:443-455` — belt2 的 EnableFluidEnhancement 分支，含第二处 Clamp

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Both Clamp calls updated
    Tool: Bash (grep)
    Steps:
      1. grep -n "Clamp(Mathf.CeilToInt(fluidInputCountPerCargo)" ProcessManager.cs
      2. Assert: 所有匹配行包含 ", 4, 20)"
      3. Assert: 0 行包含 ", 1, 20)"
    Expected Result: 恰好2处匹配，值均为 4, 20
    Evidence: .sisyphus/evidence/task-2-clamp-values.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-2-build.txt
  ```

  **Commit**: YES (groups with 1, 3)
  - Files: `ProcessManager.cs`

- [ ] 3. B1 - StationManager 自动喷涂开关

  **What to do**:
  - 在 `StationManager.cs:174` 的 `AddIncToItem(store.count, ref store.inc)` 调用外加条件判断
  - 条件：`PlanetaryInteractionStation.Level >= 3`
  - `AddIncToItem` 位于 `SetTargetCount` 方法的 `finally` 块中
  - 当 Level < 3 时，不执行自动喷涂

  **Must NOT do**:
  - 不修改 `SetTargetCount` 方法签名
  - 不添加 factory 参数
  - 不修改 `AddIncToItem` 本身的实现

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2, 4)
  - **Blocks**: Tasks 5, 6
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `StationManager.cs:132-176` — `SetTargetCount` 方法，`finally` 块在 line 173-175
  - `PlanetaryInteractionStation.cs:21` — `public static int Level = 0;` 全局静态字段
  - `PackageUtils.cs:1159-1235` — `AddIncToItem` 实现（参考，不修改）

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Auto-spray gated by Level >= 3
    Tool: Bash (grep)
    Steps:
      1. grep -n -A2 "AddIncToItem" StationManager.cs
      2. Assert: AddIncToItem 调用行的前面或包裹中有 Level >= 3 条件
    Expected Result: finally 块中 AddIncToItem 受条件保护
    Evidence: .sisyphus/evidence/task-3-spray-guard.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-3-build.txt
  ```

  **Commit**: YES (groups with 1, 2)
  - Files: `StationManager.cs`

- [ ] 4. B2 - 验证集装可调层数已正确实现

  **What to do**:
  - 验证性任务，确认以下已正确实现：
    - `PlanetaryInteractionStation.MaxProductOutputStack`: `{< 6 => 1, < 9 => 4, < 12 => 8, _ => 12}` 与 TODO 一致
    - `StationManager.cs` 的 slider UI 已使用 `maxProductOutputStack` 动态上限
    - `GetOutputStack` 方法正确返回 `MaxProductOutputStack()`
  - 如果一切正确，仅需在 evidence 中记录验证结果
  - 如果发现问题，修复

  **Must NOT do**:
  - 除非发现问题，否则不修改任何代码

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Tasks 1, 2, 3)
  - **Blocks**: Tasks 5, 6
  - **Blocked By**: None

  **References**:

  **Pattern References**:
  - `PlanetaryInteractionStation.cs:22-27` — MaxProductOutputStack switch
  - `StationManager.cs:253-256` — slider maxValue 设置
  - `StationManager.cs:550-555` — GetOutputStack 方法

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: MaxProductOutputStack values match TODO
    Tool: Bash (grep)
    Steps:
      1. grep -A6 "MaxProductOutputStack =>" PlanetaryInteractionStation.cs
      2. Assert: 包含 < 6 => 1, < 9 => 4, < 12 => 8, _ => 12
    Expected Result: 4个阈值与 TODO 一致
    Evidence: .sisyphus/evidence/task-4-stack-values.txt

  Scenario: Slider uses dynamic max
    Tool: Bash (grep)
    Steps:
      1. grep "minPilerSlider.maxValue" StationManager.cs
      2. Assert: 赋值为 maxProductOutputStack（非硬编码数字）
    Expected Result: slider 上限跟随 MaxProductOutputStack 动态变化
    Evidence: .sisyphus/evidence/task-4-slider-dynamic.txt
  ```

  **Commit**: NO (验证性任务，无代码改动)

- [ ] 5. C5 - 点数聚集塔 +6 虚空喷涂

  **What to do**:
  - 在 `PointAggregateTower.cs` 新增属性：`public static bool EnableVoidSpray => Level >= 6;`
  - 在 `ProcessManager.cs` 的 `InternalUpdate` 方法中，在 progress 循环（line 267）**之前**，添加虚空喷涂逻辑：
    ```
    if (buildingID == IFE点数聚集塔
        && PointAggregateTower.EnableVoidSpray
        && __instance.fluidInputCount > 0) {
        int avgInc = __instance.fluidInputInc / __instance.fluidInputCount;
        if (avgInc < 4) {
            AddIncToItem(__instance.fluidInputCount, ref __instance.fluidInputInc);
        }
    }
    ```
  - `AddIncToItem` 来自 `PackageUtils`（已有 `using static FE.Utils.Utils`），会自动从数据中心扣增产剂补到4点
  - 注入位置：在 `if (__instance.fluidInputCount > 0 && ...)` 条件块之前（约 line 252 前）

  **Must NOT do**:
  - 不在 `PointAggregateRecipe.GetOutputs` 中添加喷涂逻辑（GetOutputs 没有 fluidInputCount 参数）
  - 不在 progress 循环内部添加（每 tick 喷涂一次即可，而非每次判定）
  - 不修改 `AddIncToItem` 本身

  **Recommended Agent Profile**:
  - **Category**: `unspecified-low`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Task 6)
  - **Blocks**: Task 7a
  - **Blocked By**: Tasks 1-4

  **References**:

  **Pattern References**:
  - `PointAggregateTower.cs:23-24` — 现有 `EnableFluidEnhancement => Level >= 3` 属性，照此模式添加
  - `ProcessManager.cs:245-267` — InternalUpdate 中获取 building 信息后、progress 循环前的区域
  - `PackageUtils.cs:1159-1235` — `AddIncToItem` 实现细节（参考，不修改）

  **WHY Each Reference Matters**:
  - PointAggregateTower.cs: 遵循现有属性命名模式
  - ProcessManager.cs:245-267: 精确定位注入点——buildingID 已获取、fluidInputCount 已可用
  - PackageUtils.cs: 理解 `AddIncToItem` 的参数需求和副作用（lock, 消耗增产剂）

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: EnableVoidSpray property exists
    Tool: Bash (grep)
    Steps:
      1. grep "EnableVoidSpray" PointAggregateTower.cs
      2. Assert: 包含 "Level >= 6"
    Expected Result: 属性存在且阈值为6
    Evidence: .sisyphus/evidence/task-5-void-spray-prop.txt

  Scenario: Void spray logic injected in ProcessManager
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableVoidSpray\|虚空喷涂" ProcessManager.cs
      2. Assert: 存在包含 AddIncToItem 的代码块
      3. Assert: 代码块在 progress 循环（"for.*progress >= 10000"）之前
    Expected Result: 喷涂逻辑存在于正确位置
    Evidence: .sisyphus/evidence/task-5-void-spray-inject.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-5-build.txt
  ```

  **Commit**: YES (groups with 6)
  - Message: `feat(point-aggregate): add void spray and double points traits`
  - Files: `PointAggregateTower.cs`, `ProcessManager.cs`

- [ ] 6. C6 - 点数聚集塔 +12 双重点数

  **What to do**:
  - 在 `PointAggregateTower.cs` 新增属性：`public static bool EnableDoublePoints => Level >= 12;`
  - 修改 `PointAggregateRecipe.cs:GetOutputs`，在成功聚集分支中（line 25）：
    - 将 `fluidInputInc -= PointAggregateTower.MaxInc;` 改为：
    - `fluidInputInc -= PointAggregateTower.EnableDoublePoints ? PointAggregateTower.MaxInc / 2 : PointAggregateTower.MaxInc;`
    - 语义：自动喷涂仍然喷涂到 MaxInc（10点），但双重点数激活时，成功聚集只消耗一半的增产点数（MaxInc/2 = 5），剩余点数保留在原料中

  **Must NOT do**:
  - 不修改失败分支（直通逻辑）
  - 不修改 `GetOutputInc`（输出物品的 inc 仍为 MaxInc）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-low`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Task 5)
  - **Blocks**: Task 7a
  - **Blocked By**: Tasks 1-4

  **References**:

  **Pattern References**:
  - `PointAggregateTower.cs:23-24` — 现有属性模式
  - `PointAggregateRecipe.cs:14-33` — 完整的 GetOutputs 逻辑，成功分支 lines 21-26

  **WHY Each Reference Matters**:
  - PointAggregateRecipe.cs:21-26: line 25 `fluidInputInc -= PointAggregateTower.MaxInc` 是需修改的精确位置

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: EnableDoublePoints property exists
    Tool: Bash (grep)
    Steps:
      1. grep "EnableDoublePoints" PointAggregateTower.cs
      2. Assert: 包含 "Level >= 12"
    Expected Result: 属性存在且阈值为12
    Evidence: .sisyphus/evidence/task-6-double-points-prop.txt

  Scenario: Double points logic in GetOutputs
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableDoublePoints\|双重" PointAggregateRecipe.cs
      2. Assert: 在成功分支中存在条件判断
    Expected Result: 双重点数逻辑影响了点数消耗或阈值
    Evidence: .sisyphus/evidence/task-6-double-points-logic.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-6-build.txt
  ```

  **Commit**: YES (groups with 5)
  - Files: `PointAggregateTower.cs`, `PointAggregateRecipe.cs`

- [ ] 7a. C8 存储层 - 单路锁定数据持久化

  **What to do**:
  - 在 `BuildingManager.cs` 中新增：
    - `ConcurrentDictionary<(int, int), int> lockedOutputDic`，key 为 `(planetId, entityId)`，value 为锁定的 outputItemId（0 = 未锁定）
    - 扩展方法 `GetLockedOutput(this FractionatorComponent, PlanetFactory)` 和 `SetLockedOutput(this FractionatorComponent, PlanetFactory, int itemId)`
    - 在 `OutputExtendImport` 区域之后添加 `LockedOutputImport/Export/IntoOtherSave`
  - 在 `BuildingManager.Import/Export` 中接入新的 save/load 方法
  - **版本管理**：
    - `Export` 中 version 改为 2
    - `Import` 中：version < 2 时跳过 locked output 读取（兼容旧存档）

  **Must NOT do**:
  - 不修改现有 `outputDic` 结构
  - 不修改现有 `Import/Export` 的已有数据格式（只在末尾追加新数据）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (sequential)
  - **Blocks**: Task 7b
  - **Blocked By**: Tasks 5, 6

  **References**:

  **Pattern References**:
  - `BuildingManager.cs:117-175` — 现有 `outputDic` 的 `ConcurrentDictionary` + Import/Export 模式，**严格遵循此模式**
  - `BuildingManager.cs:320-350` — 现有 `Import/Export` 方法中各塔的读写顺序
  - `BuildingManager.cs:167-175` — `products` 扩展方法模式，照此写 `GetLockedOutput`

  **WHY Each Reference Matters**:
  - outputDic 模式: 提供完整的 `ConcurrentDictionary` + `(planetId, entityId)` 键 + Import/Export/IntoOtherSave 三件套参考
  - Import/Export: 确保新数据追加在正确位置且版本号处理正确

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Locked output storage exists
    Tool: Bash (grep)
    Steps:
      1. grep -n "lockedOutputDic\|LockedOutput" BuildingManager.cs
      2. Assert: ConcurrentDictionary 声明存在
      3. Assert: Get/Set 方法存在
      4. Assert: Import/Export/IntoOtherSave 方法存在
    Expected Result: 完整的存储三件套
    Evidence: .sisyphus/evidence/task-7a-storage.txt

  Scenario: Save version upgraded to 2
    Tool: Bash (grep)
    Steps:
      1. grep -n "w.Write(2)" BuildingManager.cs
      2. grep -n "version < 2" BuildingManager.cs
    Expected Result: Export 写入 version 2，Import 处理 version < 2 兼容
    Evidence: .sisyphus/evidence/task-7a-version.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-7a-build.txt
  ```

  **Commit**: NO (等 7b-7d 完成后一起提交)

- [ ] 7b. C8 逻辑层 - ConversionRecipe 单路过滤

  **What to do**:
  - 在 `ConversionTower.cs` 新增属性：`public static bool EnableSingleLock => Level >= 12;`
  - 在 `ConversionRecipe.cs` 中 override `GetOutputs` 方法：
    - 当 `ConversionTower.EnableSingleLock` 且有锁定产物时：
      - 调用 `base.GetOutputs(...)` 获取原始结果
      - 如果输出列表非空，过滤为只保留锁定产物
      - 如果锁定产物不在输出中，直通（视为失败，保持原料不消耗）
    - 当未启用或未锁定时：直接调用 `base.GetOutputs(...)` 不做修改
  - **注意**：需从 `BuildingManager` 获取当前分馏塔的锁定 ID。由于 `GetOutputs` 不接收 `FractionatorComponent`/`PlanetFactory` 参数，需要在 `ProcessManager.InternalUpdate` 中调用 `GetOutputs` 前设置一个临时上下文变量
  - 具体方案：在 `ConversionRecipe` 中添加 `public static int CurrentLockedOutputId`，由 `ProcessManager.InternalUpdate` 在调用 `GetOutputs` 前赋值

  **Must NOT do**:
  - 不修改 `BaseRecipe.GetOutputs`
  - 不修改 `BaseRecipe` 的任何内容
  - 不修改其他配方类型（MineralCopy, PointAggregate, BuildingTrain）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (sequential after 7a)
  - **Blocks**: Tasks 7c, 7d
  - **Blocked By**: Task 7a

  **References**:

  **Pattern References**:
  - `PointAggregateRecipe.cs:14-33` — 完整的 `GetOutputs` override 示例
  - `BaseRecipe.cs:90-167` — base GetOutputs 逻辑，理解输出列表结构
  - `ProcessManager.cs:162-165` — 转化塔的 InternalUpdate 调用入口
  - `ProcessManager.cs:282-287` — `recipe.GetOutputs(...)` 的调用位置

  **WHY Each Reference Matters**:
  - PointAggregateRecipe: 提供了如何 override GetOutputs 的完整模板
  - ProcessManager:282-287: 需要在此处设置 `CurrentLockedOutputId` 上下文

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: EnableSingleLock property exists
    Tool: Bash (grep)
    Steps:
      1. grep "EnableSingleLock" ConversionTower.cs
      2. Assert: 包含 "Level >= 12"
    Expected Result: 属性存在且阈值为12
    Evidence: .sisyphus/evidence/task-7b-single-lock-prop.txt

  Scenario: GetOutputs override in ConversionRecipe
    Tool: Bash (grep)
    Steps:
      1. grep -n "override.*GetOutputs\|CurrentLockedOutputId" ConversionRecipe.cs
      2. Assert: GetOutputs override 存在
      3. Assert: CurrentLockedOutputId 被使用进行过滤
    Expected Result: 过滤逻辑存在
    Evidence: .sisyphus/evidence/task-7b-filter-logic.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-7b-build.txt
  ```

  **Commit**: NO (等 7c-7d 完成后一起提交)

- [ ] 7c. C8 UI层 - 分馏塔详情窗口产物选择

  **What to do**:
  - 在 `ProcessManager.cs` 的 `UIFractionatorWindow__OnUpdate_Postfix` 中：
    - 当 `buildingID == IFE转化塔 && ConversionTower.EnableSingleLock` 时，显示锁定选择 UI
    - UI 方案：在现有详情窗口中添加一个可点击的产物列表/按钮
    - 点击后切换锁定状态：未锁定 → 锁定到该产物 → 再次点击取消锁定
  - 需要获取当前配方的所有 OutputMain 产物列表展示给用户
  - 锁定状态通过 `BuildingManager.SetLockedOutput` 持久化

  **Must NOT do**:
  - 不创建新的 UI 文件
  - 不新增 Harmony patch（在现有 patch 中扩展）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Wave 3 (sequential after 7b)
  - **Blocks**: F1-F4
  - **Blocked By**: Task 7b

  **References**:

  **Pattern References**:
  - `ProcessManager.cs:700-891` — 现有 `UIFractionatorWindow__OnUpdate_Postfix`，在此扩展
  - `ProcessManager.cs:813-828` — 按 buildingID 获取配方的 switch，可在此获取 ConversionRecipe 的 OutputMain
  - `BuildingManager.cs` — `GetLockedOutput`/`SetLockedOutput` 方法（Task 7a 中创建）

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Lock UI exists for conversion tower
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableSingleLock\|lockedOutput\|锁定" ProcessManager.cs
      2. Assert: 在 UIFractionatorWindow__OnUpdate_Postfix 区域存在相关逻辑
    Expected Result: UI 逻辑存在于正确的 patch 方法中
    Evidence: .sisyphus/evidence/task-7c-ui-logic.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-7c-build.txt
  ```

  **Commit**: YES (groups with 7a, 7b, 7d)
  - Message: `feat(conversion): add single-path lock trait with persistent storage`
  - Files: `ConversionTower.cs`, `BuildingManager.cs`, `ConversionRecipe.cs`, `ProcessManager.cs`

- [ ] 7d. C8 清理 - 配方切换时清除锁定

  **What to do**:
  - 在 `ProcessManager.cs` 的 `InternalUpdate` 方法中，配方验证区域（`needResetProducts` 逻辑，lines 196-226）：
    - 当 `needResetProducts == true`（配方发生变化），同时清除该分馏塔的锁定状态
    - 调用 `BuildingManager.SetLockedOutput(fractionator, factory, 0)` 清除锁定
  - 在缓存区全部清空时的重置区域（lines 588-596）：
    - 同样清除锁定状态

  **Must NOT do**:
  - 不修改配方验证逻辑本身
  - 不修改缓存区清空逻辑本身

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []

  **Parallelization**:
  - **Can Run In Parallel**: YES (with 7c)
  - **Parallel Group**: Wave 3 (after 7b)
  - **Blocks**: F1-F4
  - **Blocked By**: Task 7b

  **References**:

  **Pattern References**:
  - `ProcessManager.cs:196-226` — `needResetProducts` 配方验证区域
  - `ProcessManager.cs:588-596` — 缓存区全部清空重置区域
  - `BuildingManager.cs` — `SetLockedOutput` 方法

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY):**

  ```
  Scenario: Lock cleared on recipe change
    Tool: Bash (grep)
    Steps:
      1. grep -n "SetLockedOutput.*0\|清除锁定" ProcessManager.cs
      2. Assert: 至少2处调用（needResetProducts 区域 + 缓存清空区域）
    Expected Result: 两个重置点都清除了锁定
    Evidence: .sisyphus/evidence/task-7d-lock-clear.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-7d-build.txt
  ```

  **Commit**: YES (groups with 7a, 7b, 7c)

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Rejection → fix → re-run.

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, grep). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  Run `dotnet build`. Review all changed files for: commented-out code, unused imports, inconsistent naming. Check AI slop: excessive comments, over-abstraction.
  Output: `Build [PASS/FAIL] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **Build + Grep Verification** — `unspecified-high`
  Execute full build. Verify with grep: A1 values correct, A2 Clamp correct, B1 guard present, C5/C6 conditions present, C8 storage/filter/UI present. Save grep outputs as evidence.
  Output: `Build [PASS/FAIL] | Grep [N/N pass] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual diff (git diff). Verify 1:1 — everything in spec was built (no missing), nothing beyond spec was built (no creep). Check "Must NOT do" compliance. Flag unaccounted changes.
  Output: `Tasks [N/N compliant] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **Wave 1 commit**: `fix(building): unify InteractEnergyRatio and fix fluid output minimum stack` — InterstellarInteractionStation.cs, ProcessManager.cs, StationManager.cs
- **Wave 2 commit**: `feat(point-aggregate): add void spray and double points traits` — PointAggregateTower.cs, PointAggregateRecipe.cs, ProcessManager.cs
- **Wave 3 commit**: `feat(conversion): add single-path lock trait with persistent storage` — ConversionTower.cs, BuildingManager.cs, ConversionRecipe.cs, ProcessManager.cs

---

## Success Criteria

### Verification Commands
```bash
dotnet build FractionateEverything/FractionateEverything.csproj  # Expected: Build succeeded, 0 errors
```

### Final Checklist
- [ ] InterstellarInteractionStation.InteractEnergyRatio = {0.68, 0.44, 0.28, 0.20}
- [ ] ProcessManager.cs 两处 Clamp(…, 4, 20) 且无 Clamp(…, 1, 20)
- [ ] StationManager.cs AddIncToItem 受 Level >= 3 控制
- [ ] 点数聚集塔 Level >= 6 时自动喷涂生效
- [ ] 点数聚集塔 Level >= 12 时双重点数生效
- [ ] 转化塔 Level >= 12 时单路锁定可用
- [ ] BuildingManager 存档版本升为 2，兼容旧版（version 1）
- [ ] 编译成功，0 error
- [ ] 未触碰 C1-C4, C7, BaseRecipe.GetOutputs, buffBonus
