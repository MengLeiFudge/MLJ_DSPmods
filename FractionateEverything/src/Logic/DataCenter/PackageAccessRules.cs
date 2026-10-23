using FE.Compatibility.Mods;
using FE.Logic.Civilization;

namespace FE.Logic.DataCenter;

/// <summary>
/// 统一判断建筑师模式、无限资源和亚空间下载资格。
/// </summary>
public static class PackageAccessRules {
    /// <summary>
    /// 建筑师模式，所有建筑数目显示为999且建造时不消耗。
    /// </summary>
    public static bool ArchitectMode => Multfunction_mod.ArchitectMode
                                        || CheatEnabler.ArchitectMode
                                        || DeliverySlotsTweaks.ArchitectMode;

    /// <summary>
    /// 指示首遗物是否已经建立亚空间物品交互能力。
    /// </summary>
    public static bool TechItemInteractionUnlocked =>
        CivilizationRecoveryManager.HasDataCenterCommunication;

    /// <summary>
    /// 判断数据中心是否允许把已注册物品作为可下载余额展示或提取。
    /// </summary>
    public static bool CanDownloadFromDataCenter(int itemId) =>
        itemId > 0 && itemId < 12000 && LDB.items.Select(itemId) != null;
}
