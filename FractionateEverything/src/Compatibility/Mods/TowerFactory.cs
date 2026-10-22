using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.Fractionation.Fractionators;
using HarmonyLib;

namespace FE.Compatibility.Mods;

/// <summary>保留交互站的塔厂能力，避免将 FE 分馏塔误用为普通分馏生产机器。</summary>
public static class TowerFactory {
    /// <summary>塔厂插件的稳定标识，用于主插件硬依赖和启动检测。</summary>
    public const string GUID = "com.yummi.dsp.towerfactory";
    /// <summary>本次启动是否安装塔厂插件。</summary>
    public static bool Enable;

    /// <summary>通过反射安装兼容补丁，依赖缺失时由启动检测处理。</summary>
    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) { return; }
        System.Type production = pluginInfo.Instance.GetType().Assembly.GetType("TowerFactory.StationProductionPatch");
        MethodInfo match = AccessTools.Method(production, "MatchFractionatorPlan",
            [typeof(StationStore[]), typeof(string).MakeByRefType()]);
        MethodInfo suggest = AccessTools.Method(production, "SuggestIngredients",
            [typeof(StationComponent), typeof(string).MakeByRefType()]);
        MethodInfo canMake = AccessTools.Method(production, "ProductionBuildingCanMakeProduct",
            [typeof(int), typeof(int)]);
        if (match == null || suggest == null || canMake == null) {
            CheckPlugins.LogWarning($"TowerFactory {pluginInfo.Metadata.Version}：兼容入口缺失，FE 分馏塔隔离补丁未安装。");
            return;
        }
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.TowerFactory");
        harmony.Patch(match, prefix: new(typeof(TowerFactory), nameof(MatchFractionatorPlan_Prefix)));
        harmony.Patch(suggest, prefix: new(typeof(TowerFactory), nameof(SuggestIngredients_Prefix)));
        harmony.Patch(canMake, prefix: new(typeof(TowerFactory), nameof(ProductionBuildingCanMakeProduct_Prefix)));
        CheckPlugins.LogInfo("TowerFactory 兼容已启用。");
    }

    /// <summary>识别塔厂第一格中的机器；交互站自身类型不影响塔厂使用。</summary>
    /// <param name="storage">塔厂当前真实槽位数组。</param>
    /// <returns>第一格机器属于 FE 五塔时为真。</returns>
    private static bool IsFractionatorMachine(StationStore[] storage) => storage?.Length > 0
        && FractionatorTowerCatalog.IsActiveFractionator(storage[0].itemId);

    /// <summary>FE 分馏塔运行依赖实体缓存和研究状态，不能套用塔厂的原版氢分馏计划。</summary>
    /// <param name="storage">塔厂槽位。</param>
    /// <param name="reason">返回给塔厂的本端说明。</param>
    /// <param name="__result">塔厂私有计划类型的结果；排除 FE 塔时置空。</param>
    /// <returns>其他机器继续匹配，FE 塔跳过匹配。</returns>
    private static bool MatchFractionatorPlan_Prefix(StationStore[] storage, ref string reason, ref object __result) {
        if (!IsFractionatorMachine(storage)) { return true; }
        reason = "FE.TowerFactory.FractionatorUnsupported".Translate();
        __result = null;
        return false;
    }

    /// <summary>填原料时同样排除 FE 塔机器，普通制造台仍可制作 FE 建筑。</summary>
    /// <param name="station">当前塔厂所在站点，包括 FE 交互站。</param>
    /// <param name="reason">返回给填料按钮的本端说明。</param>
    /// <param name="__result">建议原料；排除 FE 塔时置空。</param>
    /// <returns>其他机器沿用塔厂原有填料规则。</returns>
    private static bool SuggestIngredients_Prefix(StationComponent station, ref string reason, ref int[] __result) {
        if (!IsFractionatorMachine(station?.storage)) { return true; }
        reason = "FE.TowerFactory.FractionatorUnsupported".Translate();
        __result = null;
        return false;
    }

    /// <summary>自动识别生产建筑时排除 FE 分馏塔，保留原版分馏塔和普通制造台候选。</summary>
    /// <param name="buildingItemId">候选机器物品 ID，而非第二格产物 ID。</param>
    /// <param name="__result">候选能否用于塔厂生产。</param>
    /// <returns>其他建筑继续执行原始识别。</returns>
    private static bool ProductionBuildingCanMakeProduct_Prefix(int buildingItemId, ref bool __result) {
        if (!FractionatorTowerCatalog.IsActiveFractionator(buildingItemId)) { return true; }
        __result = false;
        return false;
    }
}
