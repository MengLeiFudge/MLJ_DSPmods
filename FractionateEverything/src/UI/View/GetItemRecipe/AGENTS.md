# UI/View/GetItemRecipe — 抽奖与商店子域

9 个文件。核心已重构为「UI 展示层 + Logic 抽奖服务层」分层：

- UI：`TicketRaffle.cs`、`LimitedTimeStore.cs`
- Logic：`GachaManager.cs`、`GachaService.cs`、`GachaPool.cs`

## 文件定位

| File | Role |
|---|---|
| `TicketRaffle.cs` | 抽奖页 UI、按钮交互、结果渲染、与商店互跳 |
| `LimitedTimeStore.cs` | 商店页 UI、积分兑换、与抽奖互跳 |
| `TicketExchange.cs` | 奖券兑换页（矩阵/残片 → 奖券） |
| `GachaSSREffect.cs` / `GachaCard.cs` | 抽卡表现层组件 |
| `GachaLimitedUnlocks.cs` | 限定池解锁判定（宇宙矩阵） |

## 关键契约

1. 抽奖与商店互跳统一：
   - 抽奖 -> 商店：`NavigateToPage(StoreCategoryName, poolId)`
   - 商店 -> 抽奖：`NavigateToPage(GachaCategoryName, poolId)`
2. 池 ID 约定必须与 `GachaPool` 保持一致：
   - `0` 常驻配方、`1` 常驻建筑、`2` UP、`3` 限定
3. 抽卡总次数跨面板共享：`MainWindow.SharedPanelState.TicketRaffleTotalDraws`
4. 页面更新前先判可见性与面板类型（Legacy/Analysis）

## 开发规则

- 概率、保底、UP 轮换、积分扣增：只能改 `Logic/Manager/Gacha*`
- `TicketRaffle` 只做展示与交互，不写概率核心
- `LimitedTimeStore` 只做积分兑换 UI 与兑换动作，不定义池规则
- 新奖池/新兑换规则：先改 `GachaPool/GachaService`，再补 UI 文案与入口

## 反模式

- 在 UI 文件里复制一套抽卡概率常量
- 直接操作 `PityCount/PoolPoints` 数组而不走管理器接口
- 跨页跳转写死窗口实现（应走 `MainWindow.NavigateToPage`）
