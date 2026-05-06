# Logic/Economy — 经济域

经济域负责市场价值、兑换、残片经济和市场面板数据。

## Files

- `EconomyManager.cs`：经济域聚合入口。
- `MarketValueManager.cs`：动态市场价值。
- `FragmentExchangeManager.cs`：残片兑换。
- `ExchangeManager.cs`：通用兑换。
- `MarketBoardManager.cs`：市场面板数据。

## Rules

- 价值计算依赖 Items 的基础价值和矩阵阶段；不要反向让 Items 依赖 Economy。
- UI 页面只读取经济域结果，不在页面里重算市场规则。
