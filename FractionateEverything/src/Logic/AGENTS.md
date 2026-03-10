# Logic — Game Logic Layer

4 subdirs, 27 files, ~7200 lines. All static classes; no DI, no interfaces.

## Structure

```
Logic/
├── Building/   # Static building definitions (7 files) — one class per building
├── Manager/    # Static game-state managers (8 files) — cross-cutting orchestration
├── Patches/    # Standalone Harmony transpiler patches (2 files) — IL-level only
└── Recipe/     # Recipe hierarchy (10 files) — BaseRecipe + concrete subtypes
```

## Initialization Order (called from FractionateEverything.Awake)

```
BuildingManager.AddTranslations()   → calls all Building.AddTranslations()
RecipeManager.AddFracRecipes()      → calls all XxxRecipe.CreateAll()
BuildingManager.AddFractionators()  → calls all Building.Create()
TechManager.AddTechs()
ItemManager.Init()
StationManager / RuneManager / TutorialManager ...
```

## Cross-Domain Dependencies

- `ProcessManager.InternalUpdate<T>()` → `recipe.GetOutputs()`
- `StationManager` → `ItemManager.itemValue[]`
- `TechManager` → `RuneManager.UpdateSlotCount()`
- `BuildingManager` → all Building static properties (`Level`, `MaxStack`, etc.)
- `InterstellarInteractionStation.Level` → `PlanetaryInteractionStation.Level` (direct ref)

## IModCanSave Pattern (all classes implement)

```csharp
public static void Import(BinaryReader r) { int version = r.ReadInt32(); ... }
public static void Export(BinaryWriter w) { w.Write(3); ... }
public static void IntoOtherSave() { /* reset to defaults */ }
```

## Patches Folder Note

Only 2 files (`LDBToolPatch.cs`, `ModelLoadingPatch.cs`). Both use `CodeMatcher` transpilers.
All other Harmony patches live **inside** the relevant Manager class, not here.
