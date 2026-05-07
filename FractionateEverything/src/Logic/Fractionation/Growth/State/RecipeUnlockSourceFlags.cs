using System;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 记录配方由科技、抽取或追赶解锁的来源标记。
/// </summary>
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
