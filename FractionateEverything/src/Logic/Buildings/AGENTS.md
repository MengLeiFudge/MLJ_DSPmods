# Logic/Buildings — 建筑域

建筑域包含 FE 新增建筑定义、建筑等级/经验、建筑原型注册、材质/能耗刷新和建筑相关存档聚合。

## Structure

```
Buildings/
├── BuildingManager.cs          # 建筑注册、材质/能耗刷新、缓存上限、存档聚合
├── BuildingGrowthService.cs    # 等级、经验、突破消耗、等级派生属性
└── Definitions/                # 一类建筑一个静态定义类
```

## Building Definition Template

```csharp
namespace FE.Logic.Buildings.Definitions;

public static class XxxTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(r, g, b);

    public static int Level = 0;
    public static bool EnableXxx => Level >= N;
    public static int MaxStack => Level switch { < 6 => 1, < 9 => 4, _ => 8 };
    public static float EnergyRatio => Level switch { ... };

    public static void AddTranslations() { ... }
    public static void Create() { ... }
    public static void SetMaterial() { ... }
    public static void UpdateHpAndEnergy() { ... }

    public static void Import(BinaryReader r) { ... }
    public static void Export(BinaryWriter w) { ... }
    public static void IntoOtherSave() { ... }
}
```

## Files

- `Definitions/InteractionTower.cs`：交互塔，献祭特质和维度共鸣相关入口。
- `Definitions/MineralReplicationTower.cs`：矿物复制塔，流动增强相关。
- `Definitions/PointAggregateTower.cs`：点数聚集塔，点数聚集效率。
- `Definitions/ConversionTower.cs`：转化塔，转化配方和单路锁定能力。
- `Definitions/RectificationTower.cs`：精馏塔，矩阵/黑雾矩阵转残片。
- `Definitions/PlanetaryInteractionStation.cs`：行星内物流交互站。
- `Definitions/InterstellarInteractionStation.cs`：星际物流交互站，等级委托到行星站。

## Registration Flow

`BuildingManager.AddFractionators()` 调用各建筑 `Create()`。
`BuildingManager.SetFractionatorMaterial()` 调用各建筑 `SetMaterial()`。
`BuildingManager.UpdateHpAndEnergy()` 调用各建筑 `UpdateHpAndEnergy()`。

## Anti-Patterns

- 新增建筑状态但不接入 `Import/Export/IntoOtherSave`。
- 在建筑定义里处理分馏运行热路径；运行逻辑应进 `Logic/Fractionation/Process`。
- 在建筑定义里处理主面板 UI；主面板页面应进 `UI/MainPanel`。
