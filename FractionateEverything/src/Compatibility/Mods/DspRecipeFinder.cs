using System.Reflection;

using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

using FE.Logic.Fractionation.Recipes;

namespace FE.Compatibility.Mods;

/// <summary>为高级塔配方保留 FE 提示路径，普通物品和配方继续使用 RecipeFinder。</summary>
public static class DspRecipeFinder {
    /// <summary>RecipeFinder 插件标识，用于软依赖和启动检测。</summary>
    public const string GUID = "Appun.DSP.plugin.RecipeFinder";

    /// <summary>在 RecipeFinder 的替代提示入口安装兼容补丁，不引入其程序集的编译依赖。</summary>
    public static void Compatible() {
        if (!Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo)) { return; }
        MethodInfo prefix = AccessTools.Method(pluginInfo.Instance.GetType(), "UIItemTip_SetTip_Prefix",
            [typeof(UIItemTip), typeof(int), typeof(int), typeof(Vector2), typeof(Transform),
                typeof(int), typeof(int), typeof(UIButton.ItemTipType), typeof(bool), typeof(bool), typeof(bool), typeof(bool)]);
        if (prefix == null || !prefix.IsStatic || prefix.ReturnType != typeof(bool)) {
            CheckPlugins.LogWarning($"DSPRecipeFinder {pluginInfo.Metadata.Version}：未找到兼容的物品提示入口，高级配方提示兼容未安装；请提供该版本和日志。");
            return;
        }
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.DSPRecipeFinder");
        harmony.Patch(prefix, prefix: new(typeof(DspRecipeFinder), nameof(RecipeFinderTooltip_Prefix)));
        CheckPlugins.LogInfo("DSPRecipeFinder：已启用高级配方提示兼容。");
    }

    /// <summary>仅绕过高级配方的替代提示，让外层 UIItemTip.SetTip 继续执行原版布局及 FE 补丁。</summary>
    /// <param name="itemId">提示目标，负数表示配方原型 ID。</param>
    /// <param name="__result">RecipeFinder 前缀的返回值；true 表示允许外层原方法执行。</param>
    /// <returns>普通提示执行 RecipeFinder 正文；高级配方跳过该正文。</returns>
    private static bool RecipeFinderTooltip_Prefix(int itemId, ref bool __result) {
        if (itemId >= 0 || AdvancedRecipeCatalog.GetRecipe(-itemId) == null) { return true; }
        // RecipeFinder 的前缀完整重写提示并返回 false；这里返回的 false 只跳过它自身。
        __result = true;
        return false;
    }
}
