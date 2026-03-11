# Logic/Manager — Game-State Managers

8 static manager classes, 4300+ lines total. Most embed Harmony patches inline.

## Files & Responsibilities

| File | Lines | Domain |
|---|---|---|
| `ProcessManager.cs` | 1070 | Fractionator tick logic, power, belt I/O, inline Harmony patches |
| `ItemManager.cs` | 800 | Item value table (`itemValue[]`), inventory ops, DataCentre bag |
| `BuildingManager.cs` | 632 | Building registration, upgrades, level persistence, output stacking |
| `TechManager.cs` | 600 | Tech registration, unlock checks, level-up triggers |
| `StationManager.cs` | 559 | Interaction station tick logic, item transfer to DataCentre |
| `TutorialManager.cs` | 552 | Quest / tutorial system |
| `RuneManager.cs` | 327 | Rune (精华) system: slots, upgrade, decompose |
| `RecipeManager.cs` | 316 | Recipe registry (`RecipeTypeArr`), lookup, level persistence |

## Key Data Structures

```csharp
// RecipeManager
static BaseRecipe[][] RecipeTypeArr;   // [ERecipe][inputItemId] → recipe

// ItemManager
static float[] itemValue;             // [itemId] → value in electromagnetic matrix units
static ConcurrentDictionary<int, int> modDataBag;  // DataCentre inventory

// BuildingManager
static int[] outputDic;               // fractionator output slot state
```

## ProcessManager — Fractionator Core

`InternalUpdate<T>(ref T fractionator, ...)` is the hot path called 60×/sec per fractionator.
Steps: lookup recipe → `recipe.GetOutputs()` → write belt output → update power.

Inline Harmony patches inside ProcessManager:
- `GameMain.FixedUpdate` postfix — Interaction Tower trait logic (every 60 ticks)
- `FractionatorComponent.*` patches — belt I/O overrides

## Serialization

All Managers use `WriteBlocks`/`ReadBlocks` directly — no manual version or count writing.
Block tags are stable strings (e.g. `"Level"`, `"Recipe"`, `"Building"`);
unknown tags are skipped automatically (forward-compatible).

```csharp
// Export
w.WriteBlocks(("Tag", bw => { bw.Write(field); }));

// Import
r.ReadBlocks(("Tag", br => { field = br.ReadInt32(); }));
```

No legacy fallback code in any Manager.
Top-level version guard (`version < 10` → skip) lives in `FractionateEverything.Import` only.

Special case in `BuildingManager`: `OutputExtendExport`/`OutputExtendImport` no longer
write/read their own version number — they are wrapped inside a parent `WriteBlocks`/`ReadBlocks` block.
## Anti-Patterns

- `ProcessManager` already >1000 lines — do not add new unrelated features; extract to a new manager
- Never access `BuildingManager.outputDic` from outside `ProcessManager`
- Never call `LDB.items.Select()` on the hot path; cache in `ItemManager` at startup
