using System;

namespace FE.Logic.RecipeGrowth;

[Flags]
public enum RecipeUnlockSourceFlags {
    None = 0,
    TechBaseline = 1 << 0,
    Draw = 1 << 1,
    Processing = 1 << 2,
    DarkFogDrop = 1 << 3,
    Sandbox = 1 << 4,
    LegacyImport = 1 << 5,
}
