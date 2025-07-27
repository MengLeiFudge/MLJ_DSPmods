using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility;
using HarmonyLib;

namespace FE.Logic.Patches;

public static class ModelLoadingPatch {
    [HarmonyPatch(typeof(SpaceSector), nameof(SpaceSector.InitPrefabDescArray))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.InitPrefabDescArray))]
    [HarmonyPatch(typeof(ModelProtoSet), nameof(ModelProtoSet.OnAfterDeserialize))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ModelProtoSet_OnAfterDeserialize_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        if (GenesisBook.Enable || TheyComeFromVoid.Enable) {
            return instructions;
        }
        var matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Newarr));
        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Ldc_I4, 1024));
        return matcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(SkillSystem), MethodType.Constructor, typeof(SpaceSector))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> SkillSystem_Constructor_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        if (GenesisBook.Enable || TheyComeFromVoid.Enable) {
            return instructions;
        }
        var matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Newarr));
        do {
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Ldc_I4, 1024));
            matcher.Advance(1).MatchForward(false, new CodeMatch(OpCodes.Newarr));
        } while (matcher.IsValid);
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 不是用dataArray最后一项的ID，而是整个dataArray中最大的ID作为maxModelIndex
    /// </summary>
    [HarmonyPatch(typeof(ModelProto), nameof(ModelProto.InitMaxModelIndex))]
    [HarmonyPostfix]
    public static void InitMaxModelIndex() {
        ModelProto.maxModelIndex = LDB.models.dataArray.Max(model => model?.ID).GetValueOrDefault();
    }
}
