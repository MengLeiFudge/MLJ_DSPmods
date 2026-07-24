# ADR 0001：成就作为独立功能域

- 状态：已接受
- 日期：2026-07-24

## 决定

FE 的成就是独立的单存档生产里程碑，不属于远古文明探索域或科技树。

- 业务代码放在 `FractionateEverything/src/Logic/Achievements/`。
- `AchievementModule` 负责翻译、初始化、低频检查、存档、切档和奖励运行投影。
- `FeatureSaveRegistry` 使用顶层 `Achievements` 块；`AncientCivilization` 不再包含成就状态。
- 成就页面放在 `UI/MainPanel/ProgressTask/AchievementPage.cs`，玩家可见名称为“成就”。
- 协议、阶段、科技投入和真实分馏统计只是成就条件的数据来源，不形成领域所有权。
- Nebula 可以继续复用现有状态快照传输文明与成就，但载荷使用独立块，客户端仍不得结算成就。

## 原因

成就条件跨越文明进度和真实生产，奖励作用于分馏配方。把目录、生命周期、存档和 UI 都挂在 Civilization 下，会把依赖关系误写为所有权，并迫使成就奖励通过 `CivilizationRuntimeSync` 投影。

独立功能域让依赖保持单向：成就只读进度事实，向分馏运行缓存写入固定奖励；文明和分馏域不反向拥有成就状态。

## 边界

- 存档总版本继续为 `10`。
- 不迁移 FE 3.0 开发中间状态中的 `AncientCivilization/Achievements` 子块。
- 四项成就的键、条件、阈值、奖励、60 tick 检查节奏和主机权威语义不变。
- 此决定不引入领取任务、每日奖励、跨存档强度、事件总线或新网络包。
