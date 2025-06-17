using System.Collections.Generic;
using HarmonyLib;
using xiaoye97;

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
}
