# 分馏塔未实现特质实施计划

## TL;DR

> **Quick Summary**: 根据TODO.md设计规格，实现交互塔、矿物复制塔、转化塔的6个未实现特质，修复单路锁定的静态问题。
> 
> **Deliverables**:
> - IT1: 交互塔+6分馏献祭特质 - 数据中心分馏塔>1000时自动分解，提供全局增幅
> - IT2: 交互塔+12维度共鸣特质 - 成功率1+√(x/120)，处理速度1+√(x/60)，全部激活时翻倍
> - MRT1: 矿物复制塔+6质能裂变特质 - 自动分解原料为增产点数（25点/物品），塔内点数池
> - MRT2: 矿物复制塔+12零压循环特质 - 50点/物品，侧面无输出时回填至流动输入
> - CT1: 转化塔+6因果溯源特质 - 原料损毁时50%概率不消耗原料
> - CT2: 转化塔单路锁定修复 - 从静态改为每塔独立设置
> 
> **Estimated Effort**: Medium-Large
> **Parallel Execution**: YES - 3 waves (每组建筑并行)
> **Critical Path**: Wave 1 (基础属性) → Wave 2 (交互塔/矿物复制塔) → Wave 3 (转化塔) → Final

---

## Context

### Original Request
阅读todo.md，制定一个计划，实现分馏塔的目前所有未实现的特质。

### Interview Summary
**Key Discussions**:
- 用户确认："按照TODO.md描述逐项实现"
- 范围：交互塔、矿物复制塔、转化塔的所有未实现特质
- 等级分配确认：Level+6对应分馏献祭/质能裂变/因果溯源，Level+12对应维度共鸣/零压循环
- 物流交互站特质已实现，点数聚集特质（虚空喷涂、双重点数）已实现
- 单路锁定已实现但有问题：需要从静态改为每塔独立设置

**Research Findings**:
- 所有分馏塔建筑遵循相同静态类模式：`Level`、`EnableXxx => Level >= X`、switch表达式
- 现有特质实现模式：PointAggregateTower的虚空喷涂、双重点数；ConversionTower的单路锁定
- 存储模式：`ConcurrentDictionary<(int, int), T>` 存储每塔状态
- 关键管理器：BuildingManager（注册、存储）、ProcessManager（游戏逻辑）、StationManager（物流）
- 已有building-enhancement.md计划作为参考模板

### Metis Review
**Identified Gaps** (addressed in plan):
1. **性能风险**：分馏献祭的自动分解检查需要缓存优化（每N tick检查）
2. **存储兼容性**：新增每塔状态需要BuildingManager版本管理
3. **全局状态复杂性**：分馏献祭全局增幅需要线程安全和正确保存/加载
4. **流体循环实现**：零压循环的回填逻辑需要谨慎处理皮带连接检测
5. **配方集成**：因果溯源的几率效果需要与BaseRecipe.GetOutputs集成而不破坏其他配方类型

**关键约束**：
- 不得修改`BaseRecipe.GetOutputs` - 创建覆盖方法替代
- 不得触碰`buffBonus1/2/3`预留字段
- 必须遵循现有`PointAggregateRecipe.cs:14-33`的配方特质集成模式
- 必须使用`ConcurrentDictionary<(int, int), T>`存储每塔状态
- 必须实现保存/加载版本管理（增加BuildingManager版本）

---

## Work Objectives

### Core Objective
使交互塔、矿物复制塔、转化塔的6个未实现特质与TODO.md的设计规格一致，修复单路锁定的静态问题。

### Concrete Deliverables
1. **交互塔**：
   - `InteractionTower.cs`：`EnableSacrificeTrait`、`EnableDimensionalResonance`属性
   - `ProcessManager.cs`：分馏献祭自动分解逻辑，维度共鸣计算逻辑
   - `BuildingManager.cs`：全局增幅存储，数据中心分馏塔计数跟踪

2. **矿物复制塔**：
   - `MineralReplicationTower.cs`：`EnableMassEnergyFission`、`EnableZeroPressureCycle`属性
   - `ProcessManager.cs`：质能裂变点数池管理，零压循环回填逻辑
   - `BuildingManager.cs`：每塔点数池存储

3. **转化塔**：
   - `ConversionTower.cs`：`EnableCausalTracing`属性（`EnableSingleLock`已存在）
   - `ConversionRecipe.cs`：因果溯源几率逻辑
   - `BuildingManager.cs`：单路锁定存储修复（从静态改为每塔）

### Definition of Done
- [ ] `dotnet build FractionateEverything/FractionateEverything.csproj` 编译成功，0 error
- [ ] 所有特质符合TODO.md设计规格
- [ ] 新增存储系统兼容旧版存档（版本管理）
- [ ] 单路锁定从静态改为每塔独立设置

### Must Have
- 分馏献祭：数据中心分馏塔>1000时自动分解10%/秒，提供全局增幅
- 维度共鸣：成功率1+√(x/120)，处理速度1+√(x/60)，全部激活时翻倍
- 质能裂变：自动分解原料为增产点数（25点/物品），塔内点数池，平均<10时补足
- 零压循环：50点/物品，侧面无输出时回填至流动输入
- 因果溯源：原料损毁时50%概率不消耗原料
- 单路锁定修复：每塔独立设置，持久化存储

### Must NOT Have (Guardrails)
- 不得修改`BaseRecipe.GetOutputs`（被所有配方类型共享）
- 不得触碰`buffBonus1/2/3`的预留逻辑（ProcessManager中标记`// todo`）
- 不得修改现有`outputDic`结构或其Import/Export格式
- 不得修改物流交互站或点数聚集塔的已实现特质
- 不得引入性能问题（分馏献祭检查需要优化）
- 不得破坏现有存档兼容性（必须版本管理）

---

## Verification Strategy (MANDATORY)

> **ZERO HUMAN INTERVENTION** — ALL verification is agent-executed. No exceptions.
> Acceptance criteria requiring "user manually tests/confirms" are FORBIDDEN.

### Test Decision
- **Infrastructure exists**: NO（这是Unity/DSP mod，无单元测试框架）
- **Automated tests**: None
- **Framework**: N/A
- **验证方法**：编译验证 + grep/ast-grep代码检查 + 引用验证

### QA Policy
每个任务必须包含agent-executed QA场景（见TODO模板）。
证据保存到`.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`。

- **编译验证**: `dotnet build FractionateEverything/FractionateEverything.csproj`
- **代码验证**: grep/ast-grep搜索确认值/结构正确
- **引用验证**: `lsp_find_references`确认改动无遗漏
- **存储验证**: 检查BuildingManager版本号增加和Import/Export实现

---

## Execution Strategy

### Parallel Execution Waves

> 最大化吞吐量，按建筑类型分组并行。每组建筑任务可并行执行。
> 目标：每波3-4个任务。

```
Wave 1 (立即开始 — 基础属性添加，ALL parallel):
├── Task 1: 交互塔属性添加 [quick]
├── Task 2: 矿物复制塔属性添加 [quick]
└── Task 3: 转化塔属性添加 [quick]

Wave 2 (Wave 1完成后 — 交互塔/矿物复制塔实现，parallel):
├── Task 4: 交互塔分馏献祭实现 [deep]
├── Task 5: 交互塔维度共鸣实现 [deep]
├── Task 6: 矿物复制塔质能裂变实现 [unspecified-high]
└── Task 7: 矿物复制塔零压循环实现 [unspecified-high]

Wave 3 (Wave 2完成后 — 转化塔实现，sequential sub-tasks):
├── Task 8a: 转化塔因果溯源实现 [unspecified-high]
├── Task 8b: 单路锁定存储修复 [unspecified-high]
└── Task 8c: 单路锁定UI集成 [unspecified-high]

Wave FINAL (ALL任务完成后 — 验证):
├── Task F1: 计划符合性审计 (oracle)
├── Task F2: 代码质量审查 (unspecified-high)
├── Task F3: 编译+搜索验证 (unspecified-high)
└── Task F4: 范围保真检查 (deep)

Critical Path: Task 1-3 → Task 4-7 → Task 8a-8c → F1-F4
Parallel Speedup: ~60% faster than sequential
Max Concurrent: 4 (Waves 1 & 2)
```

### Dependency Matrix (abbreviated)

- **1-3**: — — 4-7, 1
- **4-7**: 1-3 — 8a-8c, 2
- **8a-8c**: 4-7 — F1-F4, 3
- **F1-F4**: 8a-8c — —

### Agent Dispatch Summary

- **Wave 1**: **3** — T1-T3 → `quick`
- **Wave 2**: **4** — T4-T5 → `deep`, T6-T7 → `unspecified-high`
- **Wave 3**: **3** — T8a-T8c → `unspecified-high`
- **FINAL**: **4** — F1 → `oracle`, F2-F3 → `unspecified-high`, F4 → `deep`

---

## TODOs

> 实现+测试=一个任务。不分开。
> 每个任务必须包含：推荐Agent Profile + 并行化信息 + QA场景。
> **没有QA场景的任务是不完整的。没有例外。**

- [x] 1. 交互塔属性添加

  **What to do**:
  - 在`InteractionTower.cs`中添加属性：
    - `public static bool EnableSacrificeTrait => Level >= 6;`
    - `public static bool EnableDimensionalResonance => Level >= 12;`
  - 确保现有属性正确：`EnableFluidEnhancement => Level >= 3`

  **Must NOT do**:
  - 不修改现有`EnergyRatio`、`PlrRatio`、`MaxProductOutputStack`计算
  - 不修改`Create()`、`SetMaterial()`、`UpdateHpAndEnergy()`方法

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []
  - **Reason**: 简单属性添加，遵循现有模式

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (与任务2、3并行)
  - **Blocks**: Tasks 4, 5
  - **Blocked By**: None

  **References**:
  - `PointAggregateTower.cs:22-28` - `EnableVoidSpray`、`EnableDoublePoints`属性模式
  - `InteractionTower.cs:21-26` - 现有Level和EnableXxx属性结构
  - `ConversionTower.cs` - `EnableSingleLock`属性模式

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: EnableSacrificeTrait property exists with correct level condition
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableSacrificeTrait" InteractionTower.cs
      2. Assert: 输出包含 "Level >= 6"
    Expected Result: 属性存在且等级条件正确
    Evidence: .sisyphus/evidence/task-1-sacrifice-property.txt

  Scenario: EnableDimensionalResonance property exists with correct level condition
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableDimensionalResonance" InteractionTower.cs
      2. Assert: 输出包含 "Level >= 12"
    Expected Result: 属性存在且等级条件正确
    Evidence: .sisyphus/evidence/task-1-resonance-property.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEveryting.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-1-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-1-sacrifice-property.txt
  - [ ] task-1-resonance-property.txt
  - [ ] task-1-build.txt

  **Commit**: YES | NO (与任务2、3一起提交)
  - Message: `feat(interaction): add sacrifice and resonance trait properties`
  - Files: `InteractionTower.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [x] 2. 矿物复制塔属性添加

  **What to do**:
  - 在`MineralReplicationTower.cs`中添加属性：
    - `public static bool EnableMassEnergyFission => Level >= 6;`
    - `public static bool EnableZeroPressureCycle => Level >= 12;`
  - 确保现有属性正确：`EnableFluidEnhancement => Level >= 3`

  **Must NOT do**:
  - 不修改现有`EnergyRatio`、`PlrRatio`、`MaxProductOutputStack`计算
  - 不添加点数池存储逻辑（在后续任务中）

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []
  - **Reason**: 简单属性添加，遵循现有模式

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (与任务1、3并行)
  - **Blocks**: Tasks 6, 7
  - **Blocked By**: None

  **References**:
  - `PointAggregateTower.cs:22-28` - `EnableVoidSpray`、`EnableDoublePoints`属性模式
  - `MineralReplicationTower.cs:21-26` - 现有Level和EnableXxx属性结构

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: EnableMassEnergyFission property exists with correct level condition
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableMassEnergyFission" MineralReplicationTower.cs
      2. Assert: 输出包含 "Level >= 6"
    Expected Result: 属性存在且等级条件正确
    Evidence: .sisyphus/evidence/task-2-fission-property.txt

  Scenario: EnableZeroPressureCycle property exists with correct level condition
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableZeroPressureCycle" MineralReplicationTower.cs
      2. Assert: 输出包含 "Level >= 12"
    Expected Result: 属性存在且等级条件正确
    Evidence: .sisyphus/evidence/task-2-cycle-property.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-2-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-2-fission-property.txt
  - [ ] task-2-cycle-property.txt
  - [ ] task-2-build.txt

  **Commit**: YES | NO (与任务1、3一起提交)
  - Message: `feat(mineral-replication): add fission and cycle trait properties`
  - Files: `MineralReplicationTower.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [x] 3. 转化塔属性添加

  **What to do**:
  - 在`ConversionTower.cs`中添加属性：
    - `public static bool EnableCausalTracing => Level >= 6;`
  - 验证现有属性：`EnableSingleLock => Level >= 12`（已存在）
  - 确保现有属性正确：`EnableFluidEnhancement => Level >= 3`

  **Must NOT do**:
  - 不修改现有`EnableSingleLock`属性定义
  - 不修改现有单路锁定逻辑（在后续任务中修复）

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: []
  - **Reason**: 简单属性添加，遵循现有模式

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (与任务1、2并行)
  - **Blocks**: Tasks 8a, 8b
  - **Blocked By**: None

  **References**:
  - `PointAggregateTower.cs:22-28` - `EnableVoidSpray`、`EnableDoublePoints`属性模式
  - `ConversionTower.cs` - 现有`EnableSingleLock`属性
  - `InteractionTower.cs` - 新增属性模式

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: EnableCausalTracing property exists with correct level condition
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableCausalTracing" ConversionTower.cs
      2. Assert: 输出包含 "Level >= 6"
    Expected Result: 属性存在且等级条件正确
    Evidence: .sisyphus/evidence/task-3-tracing-property.txt

  Scenario: EnableSingleLock property still exists
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableSingleLock" ConversionTower.cs
      2. Assert: 输出包含 "Level >= 12"
    Expected Result: 现有属性未受影响
    Evidence: .sisyphus/evidence/task-3-lock-property.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-3-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-3-tracing-property.txt
  - [ ] task-3-lock-property.txt
  - [ ] task-3-build.txt

  **Commit**: YES | NO (与任务1、2一起提交)
  - Message: `feat(conversion): add causal tracing trait property`
  - Files: `ConversionTower.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [x] 4. 交互塔分馏献祭实现

  **What to do**:
  - 在`BuildingManager.cs`中添加存储：
    - `private static int globalFractionatorCount = 0;` 跟踪数据中心分馏塔总数
    - `private static float globalSacrificeBoost = 0f;` 全局增幅值（成功率提升）
    - `private static float globalSacrificeSpeedBoost = 0f;` 全局处理速度提升
    - `private static int sacrificeUpdateTimer = 0;` 更新计时器（每60 tick更新一次）
    - `private static int cachedFractionatorCount = 0;` 缓存的分解数量（更新后1秒内不变）
  - 在`ProcessManager.cs`的`InternalUpdate`中添加逻辑：
    - 每tick更新计时器：`sacrificeUpdateTimer++`
    - 每60 tick执行一次：`if (sacrificeUpdateTimer >= 60) { sacrificeUpdateTimer = 0; ... }`
    - 当`InteractionTower.EnableSacrificeTrait && globalFractionatorCount > 1000`时：
      - 计算分解数量：`decomposeCount = (globalFractionatorCount - 1000) * 0.1f`（10%/秒）
      - 缓存分解数量：`cachedFractionatorCount = decomposeCount`
      - 计算增幅：`globalSacrificeBoost = Math.Sqrt(decomposeCount / 240)`（成功率），`globalSacrificeSpeedBoost = Math.Sqrt(decomposeCount / 120)`（处理速度）
    - 在接下来的1秒（60 tick）内，所有分馏塔使用缓存的`cachedFractionatorCount`计算增幅
  - 在`BuildingManager.cs`中添加Import/Export/IntoOtherSave方法，版本管理（版本2→3）

  **Must NOT do**:
  - 不要每tick都检查全局分馏塔计数（性能优化）
  - 不要修改现有的`outputDic`或`lockedOutputDic`结构
  - 不要触碰`buffBonus1/2/3`字段

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []
  - **Reason**: 复杂游戏逻辑，需要理解现有ProcessManager结构和性能优化

  **Parallelization**:
  - **Can Run In Parallel**: YES (与任务5、6、7并行)
  - **Parallel Group**: Wave 2 (与任务5-7并行)
  - **Blocks**: Tasks 8a-8c
  - **Blocked By**: Task 1

  **References**:
  - `BuildingManager.cs:123-144` - `outputDic`存储模式
  - `BuildingManager.cs:185-210` - `lockedOutputDic`存储模式
  - `ProcessManager.cs:InternalUpdate` - 游戏逻辑注入点
  - `PointAggregateRecipe.cs:14-33` - 配方特质集成模式

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: Sacrifice storage exists in BuildingManager
    Tool: Bash (grep)
    Steps:
      1. grep -n "globalSacrificeBoost\|globalSacrificeSpeedBoost\|sacrificeUpdateTimer\|cachedFractionatorCount" BuildingManager.cs
      2. Assert: 全局增幅变量和计时器存在
    Expected Result: 存储结构已添加
    Evidence: .sisyphus/evidence/task-4-storage.txt

  Scenario: Import/Export methods handle version 3
    Tool: Bash (grep)
    Steps:
      1. grep -n "Export.*3\|Import.*version.*3" BuildingManager.cs
      2. Assert: 版本3的Import/Export逻辑存在
    Expected Result: 版本管理正确实现
    Evidence: .sisyphus/evidence/task-4-version.txt

  Scenario: ProcessManager has sacrifice logic with 60-tick update cycle
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableSacrificeTrait\|sacrificeUpdateTimer.*60\|cachedFractionatorCount" ProcessManager.cs -A2 -B2
      2. Assert: 包含60 tick更新周期和缓存逻辑
    Expected Result: 逻辑已注入且优化
    Evidence: .sisyphus/evidence/task-4-logic.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-4-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-4-storage.txt
  - [ ] task-4-version.txt
  - [ ] task-4-logic.txt
  - [ ] task-4-build.txt

  **Commit**: YES | NO (与任务5-7一起提交)
  - Message: `feat(interaction): implement sacrifice trait with global boost system`
  - Files: `BuildingManager.cs`, `ProcessManager.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [x] 5. 交互塔维度共鸣实现

  **What to do**:
  - 在`BuildingManager.cs`中添加存储：
    - `private static readonly ConcurrentDictionary<(int, int), float> resonanceBoostDic = [];` 存储每塔共鸣增幅
    - 添加`GetResonanceBoost`、`SetResonanceBoost`方法
  - 在`ProcessManager.cs`的`InternalUpdate`中添加逻辑：
    - 当`InteractionTower.EnableDimensionalResonance`启用时
    - 计算：`successBoost = 1 + Math.Sqrt(x / 120f)`
    - 计算：`speedBoost = 1 + Math.Sqrt(x / 60f)`
    - 当所有增幅同时激活时：`successBoost *= 2f; speedBoost *= 2f;`
    - 存储在`resonanceBoostDic`中
  - 在`BuildingManager.cs`的Import/Export中处理`resonanceBoostDic`

  **Must NOT do**:
  - 不要重复计算已经存在的增幅（分馏献祭）
  - 不要修改现有的增幅计算逻辑

  **Recommended Agent Profile**:
  - **Category**: `deep`
  - **Skills**: []
  - **Reason**: 复杂数学计算和状态管理，需要与现有增幅系统集成

  **Parallelization**:
  - **Can Run In Parallel**: YES (与任务4、6、7并行)
  - **Parallel Group**: Wave 2 (与任务4、6、7并行)
  - **Blocks**: Tasks 8a-8c
  - **Blocked By**: Task 1

  **References**:
  - `BuildingManager.cs:123-144` - `outputDic`存储模式
  - `ProcessManager.cs:InternalUpdate` - 游戏逻辑注入点
  - 任务4的实现 - 增幅存储和计算模式

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: Resonance storage exists in BuildingManager
    Tool: Bash (grep)
    Steps:
      1. grep -n "resonanceBoostDic" BuildingManager.cs
      2. Assert: 存储字典存在
    Expected Result: 存储结构已添加
    Evidence: .sisyphus/evidence/task-5-storage.txt

  Scenario: Resonance calculations use correct formulas
    Tool: Bash (grep)
    Steps:
      1. grep -n "Math.Sqrt.*120\|Math.Sqrt.*60" ProcessManager.cs
      2. Assert: 包含√(x/120)和√(x/60)计算
    Expected Result: 数学公式正确
    Evidence: .sisyphus/evidence/task-5-formulas.txt

  Scenario: Double boost when all active
    Tool: Bash (grep)
    Steps:
      1. grep -n "2f\|doubled\|翻倍" ProcessManager.cs -A2 -B2
      2. Assert: 包含"所有增幅同时激活时翻倍"逻辑
    Expected Result: 翻倍逻辑正确实现
    Evidence: .sisyphus/evidence/task-5-double.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-5-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-5-storage.txt
  - [ ] task-5-formulas.txt
  - [ ] task-5-double.txt
  - [ ] task-5-build.txt

  **Commit**: YES | NO (与任务4、6、7一起提交)
  - Message: `feat(interaction): implement dimensional resonance trait with boost calculations`
  - Files: `BuildingManager.cs`, `ProcessManager.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [x] 6. 矿物复制塔质能裂变实现

  **What to do**:
  - 在`BuildingManager.cs`中添加存储：
    - `private static readonly ConcurrentDictionary<(int, int), int> fissionPointPoolDic = [];` 存储每塔点数池
    - 添加`GetFissionPointPool`、`SetFissionPointPool`方法
  - 在`ProcessManager.cs`的`InternalUpdate`中添加逻辑：
    - 当`MineralReplicationTower.EnableMassEnergyFission`启用时
    - 自动分解部分原料：`if (random.NextDouble() < 0.1f)` 分解10%原料
    - 每分解物品增加25点：`pointPool += 25`
    - 当原料或产物平均增产点数<10时：使用点数池补足到10
  - 在`BuildingManager.cs`的Import/Export中处理`fissionPointPoolDic`

  **Must NOT do**:
  - 不要修改现有矿物复制配方逻辑
  - 不要影响非矿物复制塔的建筑

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []
  - **Reason**: 需要集成点数系统和随机分解逻辑

  **Parallelization**:
  - **Can Run In Parallel**: YES (与任务4、5、7并行)
  - **Parallel Group**: Wave 2 (与任务4、5、7并行)
  - **Blocks**: Tasks 8a-8c
  - **Blocked By**: Task 2

  **References**:
  - `BuildingManager.cs:123-144` - `outputDic`存储模式
  - `ProcessManager.cs:InternalUpdate` - 游戏逻辑注入点
  - `PackageUtils.cs:1159` - `AddIncToItem`增产剂喷涂逻辑参考

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: Fission point pool storage exists
    Tool: Bash (grep)
    Steps:
      1. grep -n "fissionPointPoolDic" BuildingManager.cs
      2. Assert: 存储字典存在
    Expected Result: 点数池存储已添加
    Evidence: .sisyphus/evidence/task-6-storage.txt

  Scenario: Auto-decomposition logic with 25 points per item
    Tool: Bash (grep)
    Steps:
      1. grep -n "25\|分解.*原料" ProcessManager.cs -A2 -B2
      2. Assert: 包含25点和分解逻辑
    Expected Result: 分解逻辑正确
    Evidence: .sisyphus/evidence/task-6-decomposition.txt

  Scenario: Point pool usage when average < 10
    Tool: Bash (grep)
    Steps:
      1. grep -n "平均.*10\|average.*10" ProcessManager.cs -A2 -B2
      2. Assert: 包含平均<10时使用点数池逻辑
    Expected Result: 点数池使用逻辑正确
    Evidence: .sisyphus/evidence/task-6-pool-usage.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-6-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-6-storage.txt
  - [ ] task-6-decomposition.txt
  - [ ] task-6-pool-usage.txt
  - [ ] task-6-build.txt

  **Commit**: YES | NO (与任务4、5、7一起提交)
  - Message: `feat(mineral-replication): implement mass-energy fission trait with point pool`
  - Files: `BuildingManager.cs`, `ProcessManager.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [x] 7. 矿物复制塔零压循环实现

  **What to do**:
  - 在`BuildingManager.cs`中添加存储：
    - `private static readonly ConcurrentDictionary<(int, int), bool> zeroPressureLoopDic = [];` 存储每塔循环状态（用于UI显示）
    - 添加`GetZeroPressureLoopState`、`SetZeroPressureLoopState`方法
  - 在`ProcessManager.cs`的`InternalUpdate`中添加逻辑：
    - 当`MineralReplicationTower.EnableZeroPressureCycle`启用时
    - 检查侧面输出皮带连接：`if (!HasSideOutputBelt(fractionator, factory))`
    - 设置循环状态：`SetZeroPressureLoopState(fractionator, factory, true)`
    - 直接移动流体输出槽物品到流体输入槽：`MoveItemDirectly(fractionator.fluidOutput, fractionator.fluidInput)`
    - 产物输出优先回填至流动输入：当产物输出槽有物品且流体输入槽有空位时，移动物品
    - 每分解物品增加50点（覆盖质能裂变的25点）
    - 当侧面有输出皮带时：`SetZeroPressureLoopState(fractionator, factory, false)`
  - 在`BuildingManager.cs`的Import/Export中处理`zeroPressureLoopDic`（存储循环状态，用于存档/加载）

  **Must NOT do**:
  - 不要修改皮带连接检测的现有方法
  - 不要影响其他建筑的流体处理逻辑

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []
  - **Reason**: 需要处理流体系统和皮带连接检测

  **Parallelization**:
  - **Can Run In Parallel**: YES (与任务4、5、6并行)
  - **Parallel Group**: Wave 2 (与任务4-6并行)
  - **Blocks**: Tasks 8a-8c
  - **Blocked By**: Task 2

  **References**:
  - `BuildingManager.cs:123-144` - `outputDic`存储模式
  - `ProcessManager.cs:InternalUpdate` - 游戏逻辑注入点
  - 需要查找现有皮带连接检测方法（可能在其他文件）

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: Zero pressure loop storage exists
    Tool: Bash (grep)
    Steps:
      1. grep -n "zeroPressureLoopDic" BuildingManager.cs
      2. Assert: 存储字典存在
    Expected Result: 循环状态存储已添加
    Evidence: .sisyphus/evidence/task-7-storage.txt

  Scenario: 50 points per item (overrides 25 from fission)
    Tool: Bash (grep)
    Steps:
      1. grep -n "50\|零压循环.*点数" ProcessManager.cs -A2 -B2
      2. Assert: 包含50点和覆盖逻辑
    Expected Result: 50点逻辑正确
    Evidence: .sisyphus/evidence/task-7-points.txt

  Scenario: Fluid return logic when no side output belt
    Tool: Bash (grep)
    Steps:
      1. grep -n "侧面无输出\|no.*side.*output\|回填" ProcessManager.cs -A2 -B2
      2. Assert: 包含皮带连接检查和回填逻辑
    Expected Result: 回填逻辑正确
    Evidence: .sisyphus/evidence/task-7-return.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-7-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-7-storage.txt
  - [ ] task-7-points.txt
  - [ ] task-7-return.txt
  - [ ] task-7-build.txt

  **Commit**: YES | NO (与任务4-6一起提交)
  - Message: `feat(mineral-replication): implement zero-pressure cycle trait with fluid return`
  - Files: `BuildingManager.cs`, `ProcessManager.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [ ] 8a. 转化塔因果溯源实现

  **What to do**:
  - 在`ConversionRecipe.cs`的`GetOutputs`方法中添加逻辑：
    - 当`ConversionTower.EnableCausalTracing`启用时
    - 分馏判定为"原料损毁"时：`if (destroyed && GetRandDouble(ref seed) < 0.5)`
    - 50%概率不消耗原料：不减少原料数量
  - 确保不修改`BaseRecipe.GetOutputs`，只在`ConversionRecipe`中覆盖

  **Must NOT do**:
  - 不要修改`BaseRecipe.GetOutputs`方法（被所有配方共享）
  - 不要影响其他配方类型的损毁逻辑

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []
  - **Reason**: 需要理解配方系统和随机数生成

  **Parallelization**:
  - **Can Run In Parallel**: NO (依赖任务1-7完成)
  - **Parallel Group**: Wave 3 (顺序执行8a→8b→8c)
  - **Blocks**: Tasks 8b, 8c
  - **Blocked By**: Tasks 1-7

  **References**:
  - `ConversionRecipe.cs` - 现有`GetOutputs`方法
  - `BaseRecipe.cs` - 基类方法，不能修改
  - `PointAggregateRecipe.cs:14-33` - 配方特质集成模式

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: Causal tracing logic in ConversionRecipe.GetOutputs
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableCausalTracing\|50%.*概率\|GetRandDouble.*0.5" ConversionRecipe.cs -A2 -B2
      2. Assert: 包含50%概率不消耗原料逻辑
    Expected Result: 因果溯源逻辑正确实现
    Evidence: .sisyphus/evidence/task-8a-logic.txt

  Scenario: BaseRecipe.GetOutputs not modified
    Tool: Bash (grep)
    Steps:
      1. grep -n "GetOutputs" BaseRecipe.cs | wc -l
      2. Assert: 输出行数不变（与修改前相同）
    Expected Result: 基类方法未修改
    Evidence: .sisyphus/evidence/task-8a-base-check.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-8a-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-8a-logic.txt
  - [ ] task-8a-base-check.txt
  - [ ] task-8a-build.txt

  **Commit**: YES | NO (与任务8b、8c一起提交)
  - Message: `feat(conversion): implement causal tracing trait with 50% chance to not consume material`
  - Files: `ConversionRecipe.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [ ] 8b. 单路锁定存储修复

  **What to do**:
  - 检查现有`BuildingManager.lockedOutputDic`：确保使用`ConcurrentDictionary<(int, int), int>`存储每塔锁定状态
  - 修复`ConversionTower.EnableSingleLock`属性：确保是`=> Level >= 12`
  - 验证`BuildingManager.GetLockedOutput`和`SetLockedOutput`方法正确使用字典
  - 确保`ProcessManager.InternalUpdate`中单路锁定逻辑使用每塔存储而不是静态变量

  **Must NOT do**:
  - 不要删除现有`lockedOutputDic`或相关方法
  - 不要修改`BuildingManager`版本号（已在building-enhancement中处理）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []
  - **Reason**: 需要审查现有实现并修复静态问题

  **Parallelization**:
  - **Can Run In Parallel**: NO (依赖任务8a)
  - **Parallel Group**: Wave 3 (顺序执行8a→8b→8c)
  - **Blocks**: Task 8c
  - **Blocked By**: Task 8a

  **References**:
  - `BuildingManager.cs:185-210` - `lockedOutputDic`存储
  - `ConversionTower.cs` - `EnableSingleLock`属性
  - `ProcessManager.cs` - 单路锁定UI和逻辑

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: lockedOutputDic uses ConcurrentDictionary with per-tower keys
    Tool: Bash (grep)
    Steps:
      1. grep -n "ConcurrentDictionary.*lockedOutputDic" BuildingManager.cs -A5
      2. Assert: 使用(planetId, entityId)作为键
    Expected Result: 每塔存储结构正确
    Evidence: .sisyphus/evidence/task-8b-storage.txt

  Scenario: EnableSingleLock property uses Level >= 12
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableSingleLock.*Level.*12" ConversionTower.cs
      2. Assert: 属性条件正确
    Expected Result: 等级条件正确
    Evidence: .sisyphus/evidence/task-8b-property.txt

  Scenario: No static variables for single lock in ProcessManager
    Tool: Bash (grep)
    Steps:
      1. grep -n "static.*lock\|单路锁定.*static" ProcessManager.cs
      2. Assert: 没有用于单路锁定的静态变量
    Expected Result: 无静态锁定变量
    Evidence: .sisyphus/evidence/task-8b-no-static.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-8b-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-8b-storage.txt
  - [ ] task-8b-property.txt
  - [ ] task-8b-no-static.txt
  - [ ] task-8b-build.txt

  **Commit**: YES | NO (与任务8a、8c一起提交)
  - Message: `fix(conversion): fix single-path lock to use per-tower storage instead of static`
  - Files: `BuildingManager.cs`, `ConversionTower.cs`, `ProcessManager.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

- [ ] 8c. 单路锁定UI集成

  **What to do**:
  - 验证`ProcessManager.UIFractionatorWindow__OnUpdate_Postfix`中：
    - 当`ConversionTower.EnableSingleLock`启用时显示锁定UI
    - UI使用`BuildingManager.GetLockedOutput`获取每塔锁定状态
    - 用户选择时调用`BuildingManager.SetLockedOutput`设置每塔状态
  - 确保UI正确处理锁定状态变化和清除

  **Must NOT do**:
  - 不要修改现有的UI显示逻辑结构
  - 不要添加新的UI组件，只修复集成问题

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
  - **Skills**: []
  - **Reason**: 需要理解UI集成和状态管理

  **Parallelization**:
  - **Can Run In Parallel**: NO (依赖任务8b)
  - **Parallel Group**: Wave 3 (顺序执行8a→8b→8c)
  - **Blocks**: F1-F4
  - **Blocked By**: Task 8b

  **References**:
  - `ProcessManager.cs:UIFractionatorWindow__OnUpdate_Postfix` - UI更新逻辑
  - `BuildingManager.cs` - `GetLockedOutput`和`SetLockedOutput`方法
  - 现有building-enhancement计划中的UI实现部分

  **Acceptance Criteria**:

  **QA Scenarios (MANDATORY)**:
  ```
  Scenario: UI uses GetLockedOutput for per-tower state
    Tool: Bash (grep)
    Steps:
      1. grep -n "GetLockedOutput" ProcessManager.cs -A2 -B2
      2. Assert: 在UI更新区域调用
    Expected Result: UI使用每塔状态获取
    Evidence: .sisyphus/evidence/task-8c-ui-get.txt

  Scenario: UI uses SetLockedOutput for state changes
    Tool: Bash (grep)
    Steps:
      1. grep -n "SetLockedOutput" ProcessManager.cs -A2 -B2
      2. Assert: 在UI交互区域调用
    Expected Result: UI使用每塔状态设置
    Evidence: .sisyphus/evidence/task-8c-ui-set.txt

  Scenario: UI shows lock option only when EnableSingleLock true
    Tool: Bash (grep)
    Steps:
      1. grep -n "EnableSingleLock" ProcessManager.cs -A2 -B2
      2. Assert: 控制UI显示条件
    Expected Result: UI条件正确
    Evidence: .sisyphus/evidence/task-8c-ui-condition.txt

  Scenario: Build succeeds
    Tool: Bash
    Steps:
      1. dotnet build FractionateEverything/FractionateEverything.csproj
    Expected Result: Build succeeded, 0 errors
    Evidence: .sisyphus/evidence/task-8c-build.txt
  ```

  **Evidence to Capture**:
  - [ ] task-8c-ui-get.txt
  - [ ] task-8c-ui-set.txt
  - [ ] task-8c-ui-condition.txt
  - [ ] task-8c-build.txt

  **Commit**: YES | NO (与任务8a、8b一起提交)
  - Message: `fix(conversion): integrate single-path lock UI with per-tower storage`
  - Files: `ProcessManager.cs`
  - Pre-commit: `dotnet build FractionateEverything/FractionateEverything.csproj`

---

## Final Verification Wave (MANDATORY — after ALL implementation tasks)

> 4 review agents run in PARALLEL. ALL must APPROVE. Rejection → fix → re-run.

- [ ] F1. **Plan Compliance Audit** — `oracle`
  Read the plan end-to-end. For each "Must Have": verify implementation exists (read file, grep, ast-grep). For each "Must NOT Have": search codebase for forbidden patterns — reject with file:line if found. Check evidence files exist in .sisyphus/evidence/. Compare deliverables against plan.
  Output: `Must Have [8/8] | Must NOT Have [6/6] | Tasks [11/11] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  Run `dotnet build`. Review all changed files for: `as any`/`@ts-ignore` (C# equivalent), empty catches, console.log in prod, commented-out code, unused imports. Check AI slop: excessive comments, over-abstraction, generic names (data/result/item/temp).
  Output: `Build [PASS/FAIL] | Files [N clean/N issues] | VERDICT`

- [ ] F3. **Build + Grep Verification** — `unspecified-high`
  Execute full build. Verify with grep: All EnableXxx properties exist with correct level conditions, storage dictionaries exist, Import/Export version increased, logic injected in ProcessManager. Save grep outputs as evidence.
  Output: `Build [PASS/FAIL] | Grep [12/12 pass] | VERDICT`

- [ ] F4. **Scope Fidelity Check** — `deep`
  For each task: read "What to do", read actual diff (git log/diff). Verify 1:1 — everything in spec was built (no missing), nothing beyond spec was built (no creep). Check "Must NOT do" compliance. Detect cross-task contamination: Task N touching Task M's files. Flag unaccounted changes.
  Output: `Tasks [11/11 compliant] | Contamination [CLEAN/N issues] | Unaccounted [CLEAN/N files] | VERDICT`

---

## Commit Strategy

- **Wave 1 commit**: `feat(buildings): add trait properties for interaction, mineral, conversion towers` — InteractionTower.cs, MineralReplicationTower.cs, ConversionTower.cs
- **Wave 2 commit**: `feat(interaction): implement sacrifice and resonance traits with global boosts` — BuildingManager.cs, ProcessManager.cs
- **Wave 2 commit**: `feat(mineral-replication): implement fission and cycle traits with point pools` — BuildingManager.cs, ProcessManager.cs  
- **Wave 3 commit**: `feat(conversion): implement causal tracing and fix single-path lock` — ConversionRecipe.cs, BuildingManager.cs, ProcessManager.cs

---

## Success Criteria

### Verification Commands
```bash
dotnet build FractionateEverything/FractionateEverything.csproj  # Expected: Build succeeded, 0 errors
```

### Final Checklist
- [ ] 交互塔 EnableSacrificeTrait (Level >= 6) 存在且正确
- [ ] 交互塔 EnableDimensionalResonance (Level >= 12) 存在且正确
- [ ] 矿物复制塔 EnableMassEnergyFission (Level >= 6) 存在且正确
- [ ] 矿物复制塔 EnableZeroPressureCycle (Level >= 12) 存在且正确
- [ ] 转化塔 EnableCausalTracing (Level >= 6) 存在且正确
- [ ] 转化塔 EnableSingleLock (Level >= 12) 存在且正确
- [ ] BuildingManager 新增存储字典：sacrificeBoostDic, resonanceBoostDic, fissionPointPoolDic, zeroPressureLoopDic
- [ ] BuildingManager 版本号增加（2→3），兼容旧版存档
- [ ] ProcessManager 包含分馏献祭、维度共鸣、质能裂变、零压循环逻辑
- [ ] ConversionRecipe 包含因果溯源50%概率逻辑
- [ ] 单路锁定使用每塔存储而不是静态变量
- [ ] 所有改动编译成功，0 error
- [ ] 未触碰 BaseRecipe.GetOutputs, buffBonus1/2/3, 现有outputDic结构

