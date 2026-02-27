using System;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.UI.View.Setting;
using HarmonyLib;
using NGPT;
using static FE.Utils.Utils;

namespace FE.Compatibility;

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
         PatchMethods(harmony);
         CheckPlugins.LogInfo("PackageLogistic Compat finish.");
     }

    private static void PatchMethods(Harmony harmony) {
        Type type = assembly.GetType("PackageLogistic.PackageLogistic");
        harmony.Patch(AccessTools.Method(type, "HasItem"),
            prefix: new(typeof(PackageLogistic), nameof(HasItem)));
        harmony.Patch(AccessTools.Method(type, "AddItem"),
            prefix: new(typeof(PackageLogistic), nameof(AddItem)));
        harmony.Patch(AccessTools.Method(type, "TakeItem"),
            prefix: new(typeof(PackageLogistic), nameof(TakeItem)));
    }

    private static bool HasItem(int itemId, ref bool __result) {
        if (!Miscellaneous.EnablePackageLogistic || !TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId < 0) {
            __result = false;
            return false;
        }
        long itemTotalCount = GetModDataItemCount(itemId);
        __result = itemTotalCount > 0;
        return false;
    }

    private static bool AddItem(int itemId, int count, int inc, ref int[] __result, bool assembler = true) {
        if (!Miscellaneous.EnablePackageLogistic || !TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId < 0 || count < 1) {
            __result = [0, 0];
            return false;
        }
        long modDataItemCount = GetModDataItemCount(itemId);
        if (modDataItemCount < 10000) {
            int min = (int)Math.Min(count, 10000 - modDataItemCount);
            if (min != count) {
                int splitInc = split_inc(ref count, ref inc, min);
                AddItemToModData(itemId, min, splitInc);
                __result = [min, splitInc];
            } else {
                AddItemToModData(itemId, count, inc);
                __result = [count, inc];
            }
        } else {
            __result = [0, 0];
        }
        return false;
    }

    private static bool TakeItem(int itemId, int count, ref int[] __result) {
        if (!Miscellaneous.EnablePackageLogistic || !TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId < 0 || count < 1) {
            __result = [0, 0];
            return false;
        }
        int expectCount = TakeItemFromModData(itemId, count, out int expectInc);
        AddIncToItem(expectCount, ref expectInc);
        __result = [expectCount, expectInc];
        return false;
    }
}
