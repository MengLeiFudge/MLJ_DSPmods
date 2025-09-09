using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using DSP_Battle;
using HarmonyLib;

namespace FE.Compatibility;

public static class TheyComeFromVoid {
    public const string GUID = "com.ckcz123.DSP_Battle";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.TheyComeFromVoid");
        harmony.PatchAll(typeof(TheyComeFromVoid));
        CheckPlugins.LogInfo("TheyComeFromVoid Compat finish.");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DSP_Battle.Utils), nameof(DSP_Battle.Utils.UIItemUp))]
    private static bool Utils_UIItemUp_Prefix(ref int itemId) {
        if (itemId == 8035) {
            itemId = 9514;
        }
        return true;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BattleProtos), nameof(BattleProtos.AddTranslate))]
    private static IEnumerable<CodeInstruction> BattleProtos_AddTranslate_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var matcher = new CodeMatcher(instructions, generator);
        // 查找 ldc.i4 8035 指令并替换为 ldc.i4 9514
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldc_I4, 8035)
        );
        if (matcher.IsValid) {
            matcher.SetOperandAndAdvance(9514);
            CheckPlugins.LogInfo("TheyComeFromVoid: Replaced ldc.i4 8035 with ldc.i4 9514 in UIButton.LateUpdate");
        }
        return matcher.InstructionEnumeration();
    }
}
