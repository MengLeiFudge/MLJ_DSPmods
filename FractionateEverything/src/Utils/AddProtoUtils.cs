using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using xiaoye97;
using static FE.Utils.LogUtils;

namespace FE.Utils;

/// <summary>
/// 无需等待PreAddDataAction/PostAddDataAction结束，立即将内容添加至LDB。
/// 注：使用LDBTool.PreAddDataAction时，所有mod的PreAddDataAction事件都结束后才会执行PreAddData操作。
/// </summary>
public static class AddProtoUtils {
    public static void AddItem(ItemProto item) {
        AddItem([item]);
    }

    public static void AddItem(List<Proto> itemList) {
        typeof(LDBTool).GetMethod("AddProtosToSet", AccessTools.all)
            ?.MakeGenericMethod(typeof(ItemProto))
            .Invoke(null, [LDB.items, itemList]);
    }

    public static void AddRecipe(RecipeProto recipe) {
        AddRecipe([recipe]);
    }

    public static void AddRecipe(List<RecipeProto> recipeList) {
        List<Proto> list = [];
        list.AddRange(recipeList);
        typeof(LDBTool).GetMethod("AddProtosToSet", AccessTools.all)
            ?.MakeGenericMethod(typeof(RecipeProto))
            .Invoke(null, [LDB.recipes, list]);
    }

    public static void AddModel(ModelProto model) {
        AddModel([model]);
    }

    public static void AddModel(List<Proto> modelList) {
        typeof(LDBTool).GetMethod("AddProtosToSet", AccessTools.all)
            ?.MakeGenericMethod(typeof(ModelProto))
            .Invoke(null, [LDB.models, modelList]);
    }

    public static void AddTech(TechProto tech) {
        AddTech([tech]);
    }

    public static void AddTech(List<Proto> techList) {
        typeof(LDBTool).GetMethod("AddProtosToSet", AccessTools.all)
            ?.MakeGenericMethod(typeof(TechProto))
            .Invoke(null, [LDB.techs, techList]);
    }

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
                LDBTool.Field(field).Property("Entries").GetValue<Dictionary<ConfigDefinition, ConfigEntryBase>>().Clear();
                LDBTool.Field(field).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>().Clear();
            }
            LogInfo("LDBTool config cleared.");
            cleared = true;
        }
    }
}
