# Logic/Civilization — 远古文明探索域

本目录负责矩阵阶段、解析数据、协议发现与完整度、远古科技树和单存档成就。

## Structure

```text
Civilization/
├── Configuration/  # 当前模组组合的稳定阶段配置
├── Analysis/       # 解析数据、检索机会和深层解析
├── Protocols/      # 协议定义、资格、进度和随机检索
├── Technology/     # 单一科技点和四条塔型主干
├── Achievements/   # 类型化条件、单存档状态和固定奖励
├── CivilizationModule.cs
└── CivilizationRuntimeSync.cs
```

## Dependency Rules

- 文明域可以读取分馏配方目录，但不得读取旧抽取、旧成长或首版试验文明状态。
- 文明状态只能通过 `CivilizationRuntimeSync` 投影到分馏域运行缓存。
- `Logic/Fractionation` 不得反向引用 `Logic/Civilization`。
- UI 只能调用文明服务，不得直接修改状态字典。
- 上传边界统一由 `Logic/DataCenter/DataCenterUploadRouter.cs` 进入。

## Save Rules

- 定义目录不进存档。
- `Analysis`、`Protocols`、`Technology`、`Achievements` 和 `Recovery` 使用标签化子块。
- 新状态必须接入 `CivilizationModule`，再由 `FeatureSaveRegistry` 的顶层 `AncientCivilization` 块统一读写。
- 不为首版试验 `Civilization` 块保留读取、折算或迁移入口。
- 导入后必须重建运行缓存；禁止保存派生缓存。

## Design Rules

- 只使用一种远古文明科技点。
- 随机性只影响发现顺序和推进速度，核心协议最终必须可得。
- 协议键保持 `RecipeType + InputID`。
- 关键概率、保底和成本统一在对应服务中维护，不散落到 UI。
- 不新增事件总线、依赖注入容器或页面持有的业务状态。
