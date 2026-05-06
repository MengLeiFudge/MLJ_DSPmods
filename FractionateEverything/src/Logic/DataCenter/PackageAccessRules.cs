using FE.Compatibility.Mods;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 统一判断背包访问是否可走建筑师模式或无限资源。
/// </summary>
public static class PackageAccessRules {
    /// <summary>
    /// 建筑师模式，所有建筑数目显示为999且建造时不消耗。
    /// </summary>
    public static bool ArchitectMode => Multfunction_mod.ArchitectMode
                                        || CheatEnabler.ArchitectMode
                                        || DeliverySlotsTweaks.ArchitectMode;

    /// <summary>
    /// 指示物品交互科技是否已解锁。
    /// </summary>
    public static bool TechItemInteractionUnlocked => GameMain.history.TechUnlocked(TFE物品交互);

    public const int MinTurretAmmoNeedCount = 6;
}
