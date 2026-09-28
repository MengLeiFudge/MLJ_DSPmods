using FE.Compatibility.Mods;
using FE.Logic.Civilization;
using static FE.Logic.Items.ItemManager;

namespace FE.Logic.DataCenter;

/// <summary>
/// 统一判断背包访问是否可走建筑师模式或无限资源，以及旧货币包裹访问边界。
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
    /// 判断物品是否为仅可上传、不可下载的 Frag/Memory 包裹。
    /// </summary>
    public static bool IsUploadOnlyCurrencyPackage(int itemId) => IsCurrencyPackageItem(itemId);

    /// <summary>
    /// 判断数据中心是否允许把该物品作为可下载余额展示/提取；包裹永远不行。
    /// </summary>
    public static bool CanDownloadFromDataCenter(int itemId) =>
        itemId > 0 && itemId < 12000 && !IsCurrencyPackageItem(itemId);
}
