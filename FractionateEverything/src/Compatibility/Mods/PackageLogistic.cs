using System;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.DataCenter;
using FE.Compatibility.Nebula;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using static FE.Logic.DataCenter.DataCenterInventory;

namespace FE.Compatibility.Mods;

/// <summary>
/// 背包物流模组检测与兼容状态入口。
/// </summary>
public static class PackageLogistic {
    public const string GUID = "com.qlvlp.dsp.PackageLogistic";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.PackageLogistic");
        harmony.PatchAll(typeof(PackageLogistic));
        if (!PatchMethods(harmony)) {
            return;
        }
        CheckPlugins.LogInfo("PackageLogistic Compat finish.");
    }

    private static bool PatchMethods(Harmony harmony) {
        Type type = assembly.GetType("PackageLogistic.PackageLogistic");
        MethodInfo hasItem = type == null ? null : AccessTools.Method(type, "HasItem");
        MethodInfo addItem = type == null ? null : AccessTools.Method(type, "AddItem");
        MethodInfo takeItem = type == null ? null : AccessTools.Method(type, "TakeItem");
        if (type == null || hasItem == null || addItem == null || takeItem == null) {
            CheckPlugins.LogWarning(
                $"PackageLogistic compatibility skipped: missing targets in {assembly.GetName().Version}.");
            Enable = false;
            return false;
        }
        harmony.Patch(hasItem, prefix: new(typeof(PackageLogistic), nameof(HasItem)));
        harmony.Patch(addItem, prefix: new(typeof(PackageLogistic), nameof(AddItem)));
        harmony.Patch(takeItem, prefix: new(typeof(PackageLogistic), nameof(TakeItem)));
        return true;
    }

    private static bool HasItem(int itemId, ref bool __result) {
        if (!Miscellaneous.EnablePackageLogistic || !PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId < 0) {
            __result = false;
            return false;
        }
        if (NebulaMultiplayerModAPI.IsClient) {
            MaterialLeases.Ensure(itemId, 1);
            __result = PlayerInventoryAccess.GetItemTotalCount(itemId) > 0; return false;
        }
        long itemTotalCount = GetAvailableItemCount(itemId, out _);
        __result = itemTotalCount > 0;
        return false;
    }

    private static bool AddItem(int itemId, int count, int inc, ref int[] __result, bool assembler = true) {
        if (!Miscellaneous.EnablePackageLogistic || !PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId < 0 || count < 1) {
            __result = [0, 0];
            return false;
        }
        long accepted = DataCenterUploadRouter.Upload(itemId, count, inc, manual: true);
        int acceptedCount = (int)Math.Min(count, accepted);
        int acceptedInc = count <= 0
            ? 0
            : (int)Math.Min(int.MaxValue, (long)Math.Max(0, inc) * acceptedCount / count);
        __result = [acceptedCount, acceptedInc];
        return false;
    }

    private static bool TakeItem(int itemId, int count, ref int[] __result) {
        if (!Miscellaneous.EnablePackageLogistic || !PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId < 0 || count < 1) {
            __result = [0, 0];
            return false;
        }
        if (NebulaMultiplayerModAPI.IsClient) {
            MaterialLeases.Ensure(itemId, count);
            GameMain.mainPlayer.package.TakeItemInternal(itemId, count, out int received, out int points);
            __result = [received, points]; return false;
        }
        int expectCount = TakeItemFromModData(itemId, count, out int expectInc, manual: true);
        __result = [expectCount, expectInc];
        return false;
    }
}
