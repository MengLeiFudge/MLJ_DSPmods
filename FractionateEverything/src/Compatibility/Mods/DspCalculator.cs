using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using BepInEx.Bootstrap;
using HarmonyLib;

using FE.Logic.Fractionation.Recipes;

namespace FE.Compatibility.Mods;

/// <summary>从计算器的数据入口排除高级塔展示配方，避免空生产设备列表和错误的普通生产推算。</summary>
public static class DspCalculator {
    /// <summary>计算器插件的稳定标识，用于软依赖和启动检测。</summary>
    public const string GUID = "com.GniMaerd.DSPCalculator";

    /// <summary>仅在安装计算器时查找其初始化入口，不引入计算器程序集的编译依赖。</summary>
    public static void Compatible() {
        if (!Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo)) { return; }
        System.Type database = pluginInfo.Instance.GetType().Assembly.GetType("DSPCalculator.Logic.CalcDB");
        MethodInfo init = AccessTools.Method(database, "TryInit", System.Type.EmptyTypes);
        if (init == null || !init.IsStatic || init.ReturnType != typeof(void)) {
            CheckPlugins.LogWarning($"DSPCalculator {pluginInfo.Metadata.Version}：未找到配方初始化入口，高级塔配方过滤未安装。");
            return;
        }
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.DSPCalculator");
        harmony.Patch(init, transpiler: new(typeof(DspCalculator), nameof(CalcDB_TryInit_Transpiler)));
    }

    /// <summary>在计算器创建配方和产物索引之前过滤展示原型，复用其空配方分支；不修改全局配方表。</summary>
    /// <param name="instructions">计算器初始化方法的指令。</param>
    /// <returns>仅替换配方数组取值操作的指令；入口结构不匹配时保留原方法并记录错误。</returns>
    private static IEnumerable<CodeInstruction> CalcDB_TryInit_Transpiler(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        CodeMatch[] pattern = [
            new(OpCodes.Call, AccessTools.PropertyGetter(typeof(LDB), nameof(LDB.recipes))),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(ProtoSet<RecipeProto>), nameof(ProtoSet<RecipeProto>.dataArray))),
            new(instruction => instruction.IsLdloc()),
            new(OpCodes.Ldelem_Ref),
            new(instruction => instruction.IsStloc()),
            new(instruction => instruction.IsLdloc()),
            new(instruction => instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S),
        ];
        matcher.MatchForward(false, pattern);
        if (matcher.IsInvalid || matcher.Clone().Advance(1).MatchForward(false, pattern).IsValid) {
            CheckPlugins.LogError("DSPCalculator：配方读取及空值检查结构不匹配，高级塔配方过滤未安装；请提供计算器版本和日志。");
            return matcher.Start().InstructionEnumeration();
        }
        // 数组与索引仍由原方法提供；替换单条指令以保留原有跳转标签及异常块。
        matcher.Advance(3).Set(OpCodes.Call, AccessTools.Method(typeof(DspCalculator), nameof(ReadRecipe)));
        CheckPlugins.LogInfo("DSPCalculator：已启用高级塔展示配方过滤。");
        return matcher.InstructionEnumeration();
    }

    /// <summary>普通配方原样返回；高级塔展示配方返回空值，使计算器跳过其配方及产物索引。</summary>
    /// <param name="recipes">计算器正在遍历的游戏配方数组，只读。</param>
    /// <param name="index">原遍历索引。</param>
    /// <returns>可供计算器读取的原型，或表示跳过的空值。</returns>
    private static RecipeProto ReadRecipe(RecipeProto[] recipes, int index) {
        RecipeProto recipe = recipes[index];
        return recipe != null && AdvancedRecipeCatalog.IsAdvancedProto(recipe.ID) ? null : recipe;
    }
}
