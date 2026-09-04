# Logic — 功能域逻辑层

`Logic` 按玩家可感知/系统职责组织功能域。新增逻辑优先放入对应功能域，不要再把功能堆进 `Manager`。

## Structure

```
Logic/
├── Civilization/    # 文明解析、协议检索、远古科技、恢复、成就、黑雾调查与教程
├── DataCenter/      # 数据中心库存、玩家背包访问、物品访问重定向 patch
├── Enhancements/    # 堆叠兼容与原版配方调节（Stacking、VanillaRecipes）
├── Fractionation/   # 分馏配方族、运行投影、分馏热路径、塔型定义与表现层 → Fractionation/AGENTS.md
├── Items/           # FE 物品原型、价值、图标/信号选择表现层
├── Station/         # 物流交互站定义、运行和窗口/总控面板 patch
└── EnginePatches/   # 独立游戏引擎/数据加载 transpiler
```

`Logic/Fractionation` 内部布局（完整对象文件见 `Logic/Fractionation/AGENTS.md`）：

```
Fractionation/
├── Recipes/          # RecipeManager.cs、RecipeRuntime.cs、Definitions(5)、Models(2)
└── Fractionators/    # FractionatorManager.cs、FractionatorRuntime.cs、Definitions(四塔)、Presentation
```

依赖方向：`Civilization`（含成就奖励投影）→ `Fractionation` 运行缓存；`DataCenterUploadRouter` → `DataCenterInventory` 入账；`Station` 交互走 `DataCenter` 上传路由；`Fractionation` 不反向引用 `Civilization`。

## 入口顺序

- `Lifecycle/FeatureBootstrap.cs` 负责翻译、原型注册和 FinalAction 编排。
- `Lifecycle/FeatureSaveRegistry.cs` 负责 Import/Export/IntoOtherSave 顺序。
- 功能域内部再由各自 manager 聚合，例如 `Fractionation/Recipes/RecipeManager.cs`、`Fractionation/Fractionators/FractionatorManager.cs`、`Station/StationManager.cs`、`Civilization/CivilizationManager.cs`。

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
    // 重置状态
}
```

新增持久状态时必须同时接入对应功能域入口和 `FeatureSaveRegistry`。`AncientCivilization` 与 `Achievements` 是两个独立顶层存档块；普通成就存于顶层 `Achievements` 块，秘密成就只记录在本机 profile。

## 放置规则

- 分馏配方族与运行投影：放 `Fractionation/Recipes`；塔型定义、热路径、实例状态与表现层：放 `Fractionation/Fractionators`。
- 物流交互站与数据中心交互：放 `Station`；纯数据中心库存或背包访问规则放 `DataCenter`。
- 物品原型、价值、图标和信号表现层：放 `Items`。
- 文明解析、协议检索、远古科技、恢复、成就、黑雾调查和教程放 `Civilization`；成就源码在 `Civilization/Achievement.cs`，但普通成就存档块仍独立（顶层 `Achievements`），秘密成就走本机 profile。
- 堆叠兼容与原版配方调节：放 `Enhancements`（`Stacking`、`VanillaRecipes`）。
- 独立游戏引擎级 transpiler 放 `EnginePatches`；能归属到功能域的 patch 不放这里。

## 反模式

- 新增无明确所有权的 `XxxManager` 目录，或保留只负责转发旧调用面的门面类。
- 把 UI 主面板页面逻辑放进 `Logic`。
- 在 `Utils` 中新增业务状态、Harmony patch 或存档块。
