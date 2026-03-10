# Compatibility — Mod Integration

15 files, ~1000 lines. Each file handles exactly one external mod.

## Files

| File | External Mod | Lines | Notes |
|---|---|---|---|
| `CheckPlugins.cs` | — | 229 | **Entry point**: detects all mods, sets `Enable` flags |
| `NebulaMultiplayerModAPI.cs` | Nebula | 191 | Multiplayer sync hooks |
| `GenesisBook.cs` | GenesisBook | 81 | Recipe/tech compatibility |
| `TheyComeFromVoid.cs` | TheyComeFromVoid | 124 | Dark Fog item handling |
| `PackageLogistic.cs` | PackageLogistic | 88 | Logistics integration |
| `DeliverySlotsTweaks.cs` | DeliverySlotsTweaks | 50 | Slot count tweaks |
| `CheatEnabler.cs` | CheatEnabler | 46 | Cheat mode detection |
| `Multfunction_mod.cs` | Multfunction | 46 | Multi-function compat |
| `OrbitalRing.cs` | OrbitalRing | 22 | Orbital ring compat |
| `MoreMegaStructure.cs` | MoreMegaStructure | 22 | Mega structure compat |
| `AutoSorter.cs` | AutoSorter | 22 | Auto-sorter compat |
| `BuildToolOpt.cs` | BuildToolOpt | 22 | Build tool compat |
| `CustomCreateBirthStar.cs` | CustomCreateBirthStar | 22 | Birth star compat |
| `SmelterMiner.cs` | SmelterMiner | 22 | Smelter compat |
| `UxAssist.cs` | UxAssist | 22 | UX assist compat |

## Pattern: Detection + Conditional Patch

```csharp
public static class GenesisBook {
    public static bool Enable { get; private set; }

    // Called from CheckPlugins.Check()
    internal static void Check() {
        Enable = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("GenesisBook.GenesisBook");
    }
}

// Usage elsewhere:
if (GenesisBook.Enable) { /* apply GenesisBook-specific logic */ }
```

## Adding Support for a New Mod

1. Create `NewMod.cs` with `public static bool Enable { get; private set; }` + `internal static void Check()`
2. Call `NewMod.Check()` inside `CheckPlugins.Check()`
3. Use `NewMod.Enable` guard anywhere the integration is applied
4. If Harmony patches are needed, add them as static methods in the same file

## Key Rule

`CheckPlugins.Check()` runs at Awake — `Enable` flags are set once and never change.
Never read `PluginInfos` outside of `Check()`.
