using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Patches;

public static class LDBToolPatch {
    private static bool cleared = false;

    /// <summary>
    /// LDBTool使用配置文件判断ID、GridIndex是否有冲突，但是对于有变动的情况很不友好。
    /// 此方法将在最开始清除配置文件的所有内容，以便能正确判断冲突情况。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LDBTool), "Bind")]
    public static void LDBTool_Bind_Prefix() {
        if (!cleared) {
            var LDBTool = Traverse.Create(typeof(LDBTool));
            string[] fields =
                ["CustomID", "CustomGridIndex", "CustomStringZHCN", "CustomStringENUS", "CustomStringFRFR"];
            foreach (string field in fields) {
                LDBTool.Field(field).Property("Entries").GetValue<Dictionary<ConfigDefinition, ConfigEntryBase>>()
                    .Clear();
                LDBTool.Field(field).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>()
                    .Clear();
            }
            LogInfo("LDBTool config cleared.");
            cleared = true;
        }
    }
}
