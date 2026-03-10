# Logic/Building — Static Building Definitions

7 files, one static class per building. All follow an identical template — deviation is a bug.

## Mandatory Template

```csharp
public static class XxxTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(r, g, b);

    public static int Level = 0;
    public static bool EnableXxx   => Level >= N;    // feature gate
    public static int  MaxStack    => Level switch { < 6 => 1, < 9 => 4, _ => 8 };
    public static float EnergyRatio => Level switch { ... };

    public static void AddTranslations() { ... }
    public static void Create()           { ... }   // register item/recipe/model in LDB
    public static void SetMaterial()      { ... }   // apply colors/textures
    public static void UpdateHpAndEnergy(){ ... }   // scale HP/energy by Level

    #region IModCanSave
    public static void Import(BinaryReader r) { int version = r.ReadInt32(); Level = r.ReadInt32(); }
    public static void Export(BinaryWriter w) { w.Write(1); w.Write(Level); }
    public static void IntoOtherSave()        { Level = 0; }
    #endregion
}
```

## Files

| File | Building | Key trait |
|---|---|---|
| `InteractionTower.cs` | 交互塔 | Sacrifice trait, Dimensional Resonance; references all tower IDs |
| `MineralReplicationTower.cs` | 矿物复制塔 | Controls fluid enhancement |
| `PointAggregateTower.cs` | 点数聚集塔 | Point aggregation efficiency |
| `ConversionTower.cs` | 转化塔 | Conversion recipes |
| `RecycleTower.cs` | 回收塔 | 25% recycle rate |
| `PlanetaryInteractionStation.cs` | 行星内物流交互站 | Station-Data Centre link |
| `InterstellarInteractionStation.cs` | 星际物流交互站 | `Level` delegates to `PlanetaryInteractionStation.Level` |

## Registration Flow

`BuildingManager.AddFractionators()` calls `Building.Create()` for each building.
`BuildingManager.SetFractionatorMaterial()` calls `Building.SetMaterial()`.
`BuildingManager.UpdateHpAndEnergy()` calls `Building.UpdateHpAndEnergy()`.

## Anti-Patterns to Avoid

- Adding state that isn't persisted via `Import/Export`
- Skipping `IntoOtherSave()` reset
- Using instance fields (all fields are `private static`)
