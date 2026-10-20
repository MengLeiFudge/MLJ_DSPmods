using System.Collections.Generic;
using System.Reflection.Emit;
using FE.Logic.Enhancements.Stacking;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Station;

/// <summary>
/// 物流站输出堆叠上限的运行补丁。
/// </summary>
public static partial class StationManager {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic._station_output_parallel))]
    private static IEnumerable<CodeInstruction> GameLogic__station_output_parallel_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        // 查找 UpdateOutputSlots 调用的模式
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_S),// cargoTraffic
            new CodeMatch(OpCodes.Ldloc_S),// entitySignPool
            new CodeMatch(OpCodes.Ldloc_S),// stationPilerLevel
            new CodeMatch(OpCodes.Ldloc_S),// active
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(StationComponent), nameof(StationComponent.UpdateOutputSlots)))
        );
        if (matcher.IsInvalid) {
            LogError("Failed to find UpdateOutputSlots call pattern in GameLogic._station_output_parallel");
            return instructions;
        }

        // 保留原参数，六层研究后才投影到FE上限；不依赖其他局部变量编号。
        matcher.Advance(3).Insert(new CodeInstruction(OpCodes.Call,
            AccessTools.Method(typeof(StationManager), nameof(GetOutputStack))));
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 单线程物流输出与并行路径使用相同的接管条件。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick_OutputToBelt))]
    private static IEnumerable<CodeInstruction> PlanetTransport_GameTick_OutputToBelt_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        // 查找 UpdateOutputSlots 调用的模式
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_0),// cargoTraffic
            new CodeMatch(OpCodes.Ldloc_1),// entitySignPool
            new CodeMatch(OpCodes.Ldarg_1),// maxPilerCount
            new CodeMatch(OpCodes.Ldloc_2),// active
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(StationComponent), nameof(StationComponent.UpdateOutputSlots)))
        );
        if (matcher.IsInvalid) {
            LogError("Failed to find UpdateOutputSlots call pattern in PlanetTransport.GameTick_OutputToBelt");
            return instructions;
        }

        matcher.Advance(3).Insert(new CodeInstruction(OpCodes.Call,
            AccessTools.Method(typeof(StationManager), nameof(GetOutputStack))));
        return matcher.InstructionEnumeration();
    }

    /// <summary>六层研究前保留调用方的物流集装上限，之后使用FE成长值。</summary>
    /// <param name="vanillaMaxStack">原版传入的运输站集装上限。</param>
    /// <returns>当前设备接管规则下的上限。</returns>
    private static int GetOutputStack(int vanillaMaxStack) => StackingManager.IsUnlocked
        ? StackingManager.CurrentMaxStack : vanillaMaxStack;
}
