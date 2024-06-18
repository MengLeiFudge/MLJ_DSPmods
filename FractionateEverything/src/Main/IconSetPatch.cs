using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace FractionateEverything.Main {
    public static class IconSetPatch {
        /// <summary>
        /// 移除TechProto[] dataArray3的所有处理，从而有足够空间容纳所有分馏图标
        /// </summary>
        [HarmonyPatch(typeof(IconSet), nameof(IconSet.Create))]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<CodeInstruction>
            IconSet_Create_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(LDB), nameof(LDB.techs)))
            );

            var matcher2 = matcher.Clone();
            matcher2.MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(IconSet), nameof(IconSet.techIconIndexBuffer))),
                new CodeMatch(OpCodes.Ldarg_0)
            );

            while (matcher.Pos < matcher2.Pos) {
                matcher.SetAndAdvance(OpCodes.Nop, null);
            }

            return matcher.InstructionEnumeration();
        }
    }
}
