# Learnings - Fractionator Traits Implementation

## Mass-Energy Fission (C6) Implementation

### Date: 2026-03-03

### Pattern: ConcurrentDictionary Storage for Per-Tower Data

The codebase uses `ConcurrentDictionary<(int planetId, int entityId), T>` for storing per-tower data:
- `outputDic` - stores product outputs
- `lockedOutputDic` - stores locked output item IDs
- `fissionPointPoolDic` - stores fission point pools (new)

### Extension Method Pattern

Extension methods on `FractionatorComponent` use factory to get planetId and entityId:
```csharp
public static int GetFissionPointPool(this FractionatorComponent fractionator, PlanetFactory factory) {
    int planetId = factory.planetId;
    int entityId = fractionator.entityId;
    return fissionPointPoolDic.TryGetValue((planetId, entityId), out int points) ? points : 0;
}
```

### Import/Export Versioning

Version numbers are incremented when adding new data structures:
- Version 1: Base output extend
- Version 2: LockedOutput
- Version 3: ZeroPressureLoop
- Version 4: FissionPointPool

Always use `if (version >= N)` pattern for backward compatibility.

### Trait Comment Convention

Traits are labeled with C# + number + description:
- C5: Void Spray
- C6: Mass-Energy Fission
- C8: Single Lock
- C12: Zero Pressure Loop

### Processing Loop Logic Placement

- **Before loop**: Supplementation/automatic effects (e.g., void spray adds points, fission supplements from pool)
- **Inside loop**: Per-processing effects (e.g., fission point generation on item consumption)
- **After loop**: Post-processing effects (e.g., zero pressure loop)

### Random Chance in Processing

Use `Random.value < chance` for probability checks (UnityEngine.Random).

## Zero Pressure Cycle (C12) Implementation

### Date: 2026-03-03

### Storage Pattern

Same ConcurrentDictionary pattern as other traits:
```csharp
private static readonly ConcurrentDictionary<(int, int), bool> zeroPressureLoopDic = [];
```

### Belt Detection Pattern

Side output belt check uses:
```csharp
bool hasSideOutputBelt = (__instance.isOutput1 && __instance.belt1 > 0) || (__instance.isOutput2 && __instance.belt2 > 0);
```

- `__instance.belt1` and `__instance.belt2` are side belts
- `__instance.isOutput1` and `__instance.isOutput2` indicate if they are outputs
- Both conditions must be true for a belt to be an output

### Fluid Return Logic

When no side output belt exists:
1. Calculate move count: `Math.Min(fluidOutputCount, fluidInputMax - fluidInputCount)`
2. Transfer count between buffers
3. Transfer inc points proportionally based on average

### Placement in InternalUpdate

Zero pressure cycle logic is placed AFTER the processing loop (around line 376) and BEFORE belt handling:
- Processing loop: lines 281-372
- Zero pressure cycle: lines 376-398
- Belt handling: lines 399+

### State Tracking

The trait tracks its active state via `SetZeroPressureLoopState`:
- Set to `true` when fluid is returned (no side output belt)
- Set to `false` when side output belt exists


## Dimensional Resonance Trait (C??) Implementation

### Date: 2026-03-03

### Pattern: ConcurrentDictionary Storage for Boost Values

Added `resonanceBoostDic` using `ConcurrentDictionary<(int, int), float>` to store per-tower resonance boost (success rate additive multiplier). Follows same pattern as other traits.

### Extension Methods

- `GetResonanceBoost` returns stored boost or 0f.
- `SetResonanceBoost` stores boost, removes entry when boost is 0f.

### Import/Export Versioning

- Version 5: Added ResonanceImport/ResonanceExport.
- Added `if (version >= 5)` check in Import.
- Updated Export to write version 5 and include ResonanceExport.
- Added ResonanceIntoOtherSave call.

### Trait Logic Placement

Resonance calculation placed inside processing loop (else block) where `buffBonus1` and `buffBonus2` are set. This is because the boost affects success rate and output count multipliers.

### Boost Calculation Formula

- Count active traits: `EnableFluidEnhancement`, `EnableSacrificeTrait`, `EnableDimensionalResonance`.
- Success boost additive = sqrt(x / 120.0)
- Speed boost additive = sqrt(x / 60.0)
- If all three traits active (x == 3), double both additive parts (multiply by 2).
- Store success additive in dictionary for persistence.

### Integration with Recipe System

- `buffBonus1` receives success additive, directly multiplies success ratio (1 + buffBonus1).
- `buffBonus2` receives speed additive, multiplies output count (1 + buffBonus2).
- `buffBonus3` remains 0 (reserved for future).

### Notes

- The resonance trait only activates when `InteractionTower.EnableDimensionalResonance` is true (Level >= 12).
- Boost values are per-tower but currently global (same for all towers). Stored per tower for future flexibility.

## Sacrifice Trait (C??) Implementation

### Date: 2026-03-03

### Pattern: Global Static Variables for Global Boost

Unlike per-tower traits, the sacrifice trait provides global boosts based on total Interaction Tower count across all factories. Used static variables in BuildingManager:
- `globalFractionatorCount`: current count (updated each tick)
- `globalSacrificeBoost`: success rate boost (sqrt(decomposeCount/240))
- `globalSacrificeSpeedBoost`: speed boost (sqrt(decomposeCount/120))
- `sacrificeUpdateTimer`: timer to update boosts every 60 ticks (1 second)
- `cachedFractionatorCount`: cached count used for boost calculations during the 60-tick interval

### Import/Export Versioning
- Version 6: Added SacrificeTraitImport/SacrificeTraitExport.
- Added `if (version >= 6)` check in Import.
- Updated Export to write version 6 and include SacrificeTraitExport.
- Added SacrificeTraitIntoOtherSave call.

### Boost Calculation Formula
- Decompose count = (cachedFractionatorCount - 1000) * 0.1f when count > 1000
- Success boost = sqrt(decomposeCount / 240.0)
- Speed boost = sqrt(decomposeCount / 120.0)

### Update Logic
- Called from `FactorySystem_GameTick_Postfix` once per game tick (using `GameMain.gameTick` to avoid per-factory duplicates).
- Increment `sacrificeTimer` each game tick, reset after 60 ticks.
- Count Interaction Towers via `CountInteractionTowers()` that iterates over all factories and fractionatorPool.
- Call `UpdateSacrificeTrait(count)` which updates global variables and recomputes boosts when timer reaches 60.

### Integration with Recipe System
- Boost values are accessible via `GetSacrificeBoost()` and `GetSacrificeSpeedBoost()`.
- These can be used in recipe calculations via `buffBonus1` and `buffBonus2` fields (todo).
- The trait only activates when `InteractionTower.EnableSacrificeTrait` is true (Level >= 6).


## Critical Fixes for Wave 2 Implementation

### Date: 2026-03-03

### Dimensional Resonance Formula Correction
- Original formula used only sqrt(x/120) and sqrt(x/60) without the base 1+.
- Corrected to `1.0 + Math.Sqrt(x / 120.0)` and `1.0 + Math.Sqrt(x / 60.0)`.
- Doubling logic now applies to the total boost (including the base 1) when all three traits active (x == 3).
- Fixed in both `InternalUpdate` method and `UIFractionatorWindow__OnUpdate_Postfix` method.

### Sacrifice Boost Integration
- Previously, sacrifice boost values were computed but not applied to recipe calculations.
- Added `buffBonus1 += BuildingManager.GetSacrificeBoost()` and `buffBonus2 += BuildingManager.GetSacrificeSpeedBoost()` after dimensional resonance block.
- Added same integration in UI update method for consistent display.
- Global sacrifice boost now affects all fractionators (global增幅).

### Verification of UpdateSacrificeTrait Call
- Confirmed that `UpdateSacrificeTrait` is called from `FactorySystem_GameTick_Postfix` with correct count of Interaction Towers.
- Timer logic works as intended (once per second updates).
