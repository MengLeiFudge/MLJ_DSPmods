# Building Enhancement - Learnings

## 2026-03-03 Session Start

### Codebase Structure
- Project: FractionateEverything/FractionateEverything.csproj
- Build command: `dotnet build FractionateEverything/FractionateEverything.csproj`
- Key files:
  - `FractionateEverything/src/Logic/Building/InterstellarInteractionStation.cs`
  - `FractionateEverything/src/Logic/Building/PlanetaryInteractionStation.cs`
  - `FractionateEverything/src/Logic/Building/PointAggregateTower.cs`
  - `FractionateEverything/src/Logic/Building/ConversionTower.cs`
  - `FractionateEverything/src/Logic/Manager/ProcessManager.cs`
  - `FractionateEverything/src/Logic/Manager/StationManager.cs`
  - `FractionateEverything/src/Logic/Manager/BuildingManager.cs`

### Key Findings

#### Task 1 (A1): InterstellarInteractionStation.InteractEnergyRatio
- Current values (WRONG): `< 2 => 1.0f, < 5 => 0.95f, < 8 => 0.85f, < 11 => 0.7f, _ => 0.5f`
- Target values (from PlanetaryInteractionStation): `< 2 => 1.0f, < 5 => 0.68f, < 8 => 0.44f, < 11 => 0.28f, _ => 0.2f`
- Location: InterstellarInteractionStation.cs lines 35-41

#### Task 2 (A2): ProcessManager Clamp values
- Two locations with `Mathf.Clamp(Mathf.CeilToInt(fluidInputCountPerCargo), 1, 20)`:
  - Line 356 (belt1 EnableFluidEnhancement branch)
  - Line 446 (belt2 EnableFluidEnhancement branch)
- Change `1` to `4` in both

#### Task 3 (B1): StationManager auto-spray guard
- Location: StationManager.cs line 174 in `SetTargetCount` method's `finally` block
- Current: `AddIncToItem(store.count, ref store.inc);`
- Need to wrap with: `if (PlanetaryInteractionStation.Level >= 3)`

#### Task 4 (B2): Verification
- PlanetaryInteractionStation.MaxProductOutputStack: `< 6 => 1, < 9 => 4, < 12 => 8, _ => 12` ✓ CORRECT
- InterstellarInteractionStation.MaxProductOutputStack: same values ✓ CORRECT
- StationManager uses `maxProductOutputStack` dynamically (need to verify slider)

### Patterns
- All building classes are `public static class` with `Level`, `EnergyRatio`, `InteractEnergyRatio`, etc.
- `PlanetaryInteractionStation.Level` is the global static field; `InterstellarInteractionStation.Level` delegates to it
- `AddIncToItem` is in PackageUtils.cs (via `using static FE.Utils.Utils`)
- BuildingManager.outputDic uses `ConcurrentDictionary<(int, int), List<ProductOutputInfo>>`
- Import/Export pattern: version number first, then data

### Namespace
- `using static FE.Utils.Utils` provides `AddIncToItem`
- `using static FE.Logic.Manager.ProcessManager` provides constants like `IFE点数聚集塔`

## Task 7a: Locked Output Storage Layer (2026-03-03)

### Pattern: Adding new versioned data to BuildingManager
- New data sections go as a new `#region` block after existing region(s)
- Follow the exact same pattern as `outputDic` (lines 117-177)
- `ConcurrentDictionary<(int, int), int>` works well for `(planetId, entityId) => itemId` maps
- Extension methods on `FractionatorComponent` take `PlanetFactory` as second param to get `planetId`
- `itemId == 0` means "no lock" → TryRemove instead of setting 0 value

### Version bump strategy
- `Export` writes new version int, all existing data, then new data at end
- `Import` reads version, reads all existing data unconditionally, then guards new data with `if (version >= N)`
- This ensures backward compatibility: old saves simply skip new fields
- `IntoOtherSave` just calls Clear() on new dicts (no versioning needed)

### Build: 0 errors, 11 pre-existing warnings (unrelated)

## Task 7b: Single-path lock logic (C8)
- `ConversionRecipe.cs` uses `namespace FE.Logic.Recipe` - needed to add `using FE.Logic.Building;` and `using FE.Logic.Manager;` to reference `ConversionTower` and `ProcessManager`
- `GetOutputs` override pattern: call `base.GetOutputs(...)` then post-filter outputs list
- `ProcessManager.emptyOutputs` used as the "failed/passthrough" sentinel value for filtered-out outputs
- `outputs.Where(...).ToList()` works because `ConversionRecipe.cs` already had `using System.Linq;`

## Task 7c: Lock UI in UIFractionatorWindow__OnUpdate_Postfix

- Lock status display appended to `s1` after `recipe.TypeNameWC + "\n" + sb1.ToString()...` assignment
- Placed BEFORE `s2 = ...` so position calc on line count still works correctly
- Pattern: `buildingID == IFE转化塔 && ConversionTower.EnableSingleLock` guards the block
- `fractionator.GetLockedOutput(__instance.factory)` returns 0 when no lock set
- `LDB.items.Select(lockedId)` can return null - always guard with null check
- Build: 0 errors, 11 pre-existing warnings
