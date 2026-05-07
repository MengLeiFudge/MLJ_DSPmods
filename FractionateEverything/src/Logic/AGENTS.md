# Logic — 功能域逻辑层

`Logic` 按玩家可感知/系统职责组织功能域。新增逻辑优先放入对应功能域，不要再把功能堆进 `Manager`。

## Structure

```
Logic/
├── Buildings/       # 建筑注册聚合
├── Fractionation/   # 分馏配方、配方成长、分馏运行热路径、分馏塔表现层 patch
├── Station/         # 物流交互站运行、窗口/总控面板 patch、增产点池
├── DataCenter/      # 数据中心库存、玩家背包访问、物品访问重定向 patch
├── Items/           # FE 物品原型、价值、矩阵阶段、图标/信号选择表现层
├── Gacha/           # 抽取状态、卡池、抽取服务、图鉴加成
├── Economy/         # 市场价值、兑换、残片经济、市场面板数据
├── Progression/     # 科技、教程、成就/引导元数据
├── DarkFog/         # 黑雾分支和战斗进度
├── EnginePatches/   # 独立游戏引擎/数据加载 transpiler
└── Manager/         # 暂未归域的共享 manager
```

## 入口顺序

- `Lifecycle/FeatureBootstrap.cs` 负责翻译、原型注册和 FinalAction 编排。
- `Lifecycle/FeatureSaveRegistry.cs` 负责 Import/Export/IntoOtherSave 顺序。
- 功能域内部再由各自 manager 聚合，例如 `Buildings/BuildingManager.cs`、`Fractionation/FracRecipes/RecipeManager.cs`、`Station/StationManager.cs`。

## 存档约束

所有功能域状态统一使用 `WriteBlocks/ReadBlocks` 标签化格式：

```csharp
public static void Export(BinaryWriter w) {
    w.WriteBlocks(("FieldName", bw => bw.Write(Field)));
}

public static void Import(BinaryReader r) {
    r.ReadBlocks(("FieldName", br => Field = br.ReadInt32()));
}

public static void IntoOtherSave() {
    // reset state
}
```

新增持久状态时必须同时接入对应功能域 manager 和 `FeatureSaveRegistry`。

## 放置规则

- 分馏配方/运行/窗口显示：放 `Fractionation`。
- 物流交互站与数据中心交互：放 `Station`；纯数据中心库存或背包访问规则放 `DataCenter`。
- 物品原型、价值、矩阵阶段和图标/信号表现层：放 `Items`。
- 抽取、市场、科技、教程、黑雾分别放 `Gacha`、`Economy`、`Progression`、`DarkFog`。
- 独立游戏引擎级 transpiler 放 `EnginePatches`；能归属到功能域的 patch 不放这里。

## 反模式

- 新增 `XxxManager` 后直接放进 `Logic/Manager`，或保留只负责转发旧调用面的门面类。
- 把 UI 主面板页面逻辑放进 `Logic`。
- 在 `Utils` 中新增业务状态、Harmony patch 或存档块。
