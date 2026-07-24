# Logic/Achievements — 成就域

本目录负责单存档成就定义、条件评估、完成状态、固定奖励和分馏运行投影，不属于文明探索域或科技树。

## Structure

```text
Achievements/
├── AchievementModule.cs      # 翻译、初始化、低频 Tick、存档和运行投影入口
├── AchievementCatalog.cs     # 固定定义与排序
├── AchievementDefinition.cs  # 类型化条件和奖励
├── AchievementService.cs     # 条件评估与进度查询
└── AchievementState.cs       # 单存档完成状态
```

## Dependency Rules

- 成就可以只读协议、阶段、科技投入和真实分馏统计，但这些依赖不形成领域所有权。
- `AchievementModule` 将奖励写入 `Logic/Fractionation/FracRecipes/Runtime/RecipeModifierCache`；分馏热路径不得反向引用成就业务对象。
- 客户端不评估或完成成就；主机完成后刷新运行投影并广播权威快照。
- UI 只能读取成就服务和状态，不得直接完成、撤销或改写成就。

## Save Rules

- `FeatureSaveRegistry` 使用顶层 `Achievements` 块调用 `AchievementModule.Import/Export`。
- `AchievementModule.IntoOtherSave` 必须同时清空完成状态、重置低频检查时钟并刷新派生缓存。
- 定义和运行缓存不进存档；导入后由 `AchievementModule.AfterImport` 重建奖励投影。
- 存档总版本保持 10，不为 FE 3.0 开发中间状态中的 `AncientCivilization/Achievements` 子块增加迁移。

## Design Rules

- 成就是单存档固定里程碑，不消耗科技点，不替代协议或科技树。
- 条件、阈值和奖励统一维护在定义与服务中，不散落到 UI。
- 普通成就不提供跨存档决定性强度。
- 不新增领取任务、每日奖励、事件总线或跨存档抽取强度。
