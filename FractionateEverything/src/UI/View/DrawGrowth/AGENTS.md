# UI/View/DrawGrowth — 抽取与成长系统

本目录负责 `抽取成长` 分类下的页面与表现组件：开线抽取、原胚抽取、成长规划、流派聚焦、资源统筹。原 `GetItemRecipe` 路径已废弃，本文件为当前权威子文档。

## 文件职责划分

- **抽取页**
  - `TicketRaffle.cs`：开线抽取 / 原胚抽取 UI，处理单抽、十抽、结果摘要与跳转按钮。
- **成长页**
  - `LimitedTimeStore.cs`：成长规划 / 流派聚焦 UI，消费成长池积分并切换聚焦方向。
  - `GachaExchangeRow.cs`：成长兑换行组件。
- **资源页**
  - `TicketExchange.cs`：矩阵与残片兑换、资源统筹入口。
- **表现组件**
  - `GachaCard.cs`：结果卡片与稀有度显示。
  - `GachaSSREffect.cs`：高稀有度抽取效果。
  - `GachaWindow.cs`：抽取结果窗口占位/承载入口。

## 核心交互契约

1. **跨页跳转**：本域页面切换统一调用 `MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, index)`。
2. **状态同步**：
   - 概率、保底进度、池积分等核心逻辑状态**严禁**在 UI 层计算，必须通过 `Logic/Manager/Gacha*`（如 `GachaService`）获取。
   - 跨面板共享状态（如总抽卡次数）存储在 `MainWindow.SharedPanelState`。
3. **配方获取路径**：
   - 科技奖励：`TechManager` 在关键科技解锁时直接奖励部分配方。
   - 抽卡奖励：`TicketRaffle` 调用 `GachaService.Draw(...)`，由 Service/Recipe 层完成奖励发放。
   - 原版增强：由 `TechManager` 的矩阵层开放规则控制。

## 开发约束

- **禁止**在 UI 类中直接修改 `GachaManager` 的原始状态数组或字段，所有变更应通过 Service/Manager 接口。
- **禁止**硬编码颜色，必须使用 `RichTextUtils` 或 `GachaCard` 定义的稀有度常量。
- **可见性检查**：`UpdateUI` 必须先判断页面是否处于当前活跃面板，避免离屏刷新。
