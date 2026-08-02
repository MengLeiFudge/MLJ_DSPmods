# ADR 0001：成就的领域归属与存档边界

- 状态：已接受
- 日期：2026-07-24
- 更新：2026-08-02（成就源码归入 `Logic/Civilization/Achievement.cs`，不再作为 `Logic` 一级目录）

## 决定

FE 的成就不作为 `Logic` 一级功能域：成就源码位于 `Logic/Civilization/Achievement.cs`，与文明域代码同目录，但存档所有权保持独立。

- `AchievementModule` 负责翻译、初始化、低频检查、存档、切档和奖励运行投影。
- 普通成就：完成状态保存在独立顶层 `Achievements` 存档块，由 `AchievementModule` 管理，主机权威结算；奖励与完成时机匹配，不跨档提供决定性强度。
- 秘密成就：状态只记录在本机 profile，不进入存档；只承载发现记录、背景或纪念内容，不发放生产强度或经济资源。
- `FeatureSaveRegistry` 使用顶层 `Achievements` 块；`AncientCivilization` 不包含成就状态。
- 成就页面放在 `UI/MainPanel/ProgressTask/AchievementPage.cs`，玩家可见名称为“成就”。
- 协议、无阶段协议数量里程碑、科技投入和真实分馏统计只是成就条件的数据来源，不形成领域所有权。
- Nebula 可以继续复用现有状态快照传输文明与成就，但载荷使用独立块，客户端仍不得结算成就。

## 原因

成就条件跨越文明进度和真实生产，奖励作用于分馏配方。把成就挂在 Civilization 存档块下会把依赖关系误写为所有权，并迫使成就奖励通过 `CivilizationRuntimeSync` 投影。

源码与文明域同目录（`Civilization/Achievement.cs`）保持单一子域组织；存档块与奖励投影保持独立。依赖单向：成就只读进度事实，向分馏运行缓存写入固定奖励；文明和分馏域不反向拥有成就状态。

## 边界

- 存档总版本继续为 `10`。
- 不迁移 FE 3.0 开发中间状态中的 `AncientCivilization/Achievements` 子块。
- 四项成就的键、条件、阈值、奖励、60 tick 检查节奏和主机权威语义不变。
- 秘密成就只在本机 profile，不进入存档，不参与联机同步结算。
- 此决定不引入领取任务、每日奖励、跨存档强度、事件总线或新网络包。
