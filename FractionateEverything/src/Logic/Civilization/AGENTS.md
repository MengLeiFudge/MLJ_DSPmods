# Logic/Civilization — 远古文明域

本目录负责文明解析与协议检索（Frag 支付）、远古科技树、文明恢复、普通/秘密成就和黑雾调查。成就不再作为 `Logic` 一级目录：成就源码位于本目录 `Achievement.cs`，但普通成就存档块仍独立（顶层 `Achievements`），秘密成就记录在本机 profile。

## Structure

```text
Civilization/
├── CivilizationManager.cs   # 定义目录、状态、权限校验与运行投影调度
├── CivilizationRuntime.cs   # 文明状态 -> 分馏运行缓存的投影
├── Analysis.cs              # 解析与深层解析：Frag 检索结算、科技点产出
├── Protocol.cs              # 协议目录、发现、完整度、正式解锁（Memory + 实物）
├── AncientTechnology.cs     # 单一科技点和四条塔型主干
├── Recovery.cs              # 文明恢复状态
├── Achievement.cs           # 普通成就（单存档）与秘密成就（本机 profile）
├── Tutorial.cs              # 启动教程与首轮操作引导
└── DarkFog.cs               # 黑雾固定证据/样本调查链
```

## Dependency Rules

- 文明域可以读取分馏配方目录，但不得读取旧抽取、旧成长或首版试验文明状态。
- 文明状态只能通过 `CivilizationRuntimeSync` 投影到分馏域运行缓存；成就奖励由 `AchievementModule` 独立投影（源码在本目录，但存档块独立）。
- `Logic/Fractionation` 不得反向引用 `Logic/Civilization`。
- UI 只能调用文明服务，不得直接修改状态字典。
- 上传边界统一由 `Logic/DataCenter/DataCenterUploadRouter.cs` 进入。

## Save Rules

- 定义目录不进存档。
- `Analysis`、`Protocols`、`Technology` 和 `Recovery` 使用标签化子块，统一由 `CivilizationModule` 写入顶层 `AncientCivilization` 块。
- 普通成就状态写入独立顶层 `Achievements` 块，不写入 `AncientCivilization`；秘密成就记录于本机 profile，不进入存档。
- 不为首版试验 `Civilization` 块保留读取、折算或迁移入口。
- 导入后必须重建运行缓存；禁止保存派生缓存。

## Design Rules

- 只使用一种远古文明科技点。
- Frag 支付广域/方向/锚定检索；方向约为基础成本的 125%、锚定 200%，连续无效响应有保底。
- 完整度达到 100% 只取得购买资格；正式解锁必须同时满足 Memory 分档与实物支付，由主机原子结算。
- 全局无合格未满候选时，广域检索转入深层解析并累计残片成本产出科技点；方向/锚定在无候选时不可执行且不消耗残片。
- 随机性只影响发现顺序和推进速度，核心协议最终必须可得。
- 协议键保持 `RecipeType + InputID`；目录全局唯一，不再按矩阵阶段分段。
- 黑雾固定证据/样本调查链：样本掉落提供接触资格，认知等级参与残片产出公式；黑雾路线是可选支线，只加速恢复，不阻塞任何完成条件。
- 关键概率、保底和成本统一在对应服务中维护，不散落到 UI。
- 不新增事件总线、依赖注入容器或页面持有的业务状态。
