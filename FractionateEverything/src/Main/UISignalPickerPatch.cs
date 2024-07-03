using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using static FractionateEverything.Utils.RecipeHelper;

namespace FractionateEverything.Main {
    /// <summary>
    /// 调整图标选取页面的显示。
    /// </summary>
    public static class UISignalPickerPatch {
        /// <summary>
        /// 公式分页移除所有新增配方的图标
        /// </summary>
        [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<CodeInstruction>
            UISignalPicker_RefreshIcons_Transpiler(IEnumerable<CodeInstruction> instructions) {
            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(RecipeProto), nameof(RecipeProto.hasIcon)))
            ).Advance(-3);

            var dataArray = matcher.InstructionAt(0).operand;
            var index = matcher.InstructionAt(1).operand;
            var label = matcher.InstructionAt(4).operand;

            //添加：if(dataArray[index11].ID > 1000) 跳转到结尾
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_S, dataArray),
                new CodeInstruction(OpCodes.Ldloc_S, index),
                new CodeInstruction(OpCodes.Ldelem_Ref),
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(UISignalPickerPatch),
                        nameof(UISignalPicker_RefreshIcons_Transpiler_InsertMethod))),
                new CodeInstruction(OpCodes.Brtrue_S, label));

            return matcher.InstructionEnumeration();
        }

        public static bool UISignalPicker_RefreshIcons_Transpiler_InsertMethod(RecipeProto recipeProto) {
            return recipeProto.ID > 1000;
        }

        /// <summary>
        /// 所有分馏配方显示在各个页面
        /// </summary>
        [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))]
        [HarmonyPostfix]
        public static void UISignalPicker_RefreshIcons_Postfix(ref UISignalPicker __instance) {
            if (__instance.currentType > 7) {
                IconSet iconSet = GameMain.iconSet;
                RecipeProto[] dataArray = LDB.recipes.dataArray;
                foreach (var recipe in dataArray) {
                    if (UISignalPicker_RefreshIcons_Transpiler_InsertMethod(recipe) && recipe.hasIcon) {
                        int tab = recipe.GridIndex / 1000;
                        if (tab == __instance.currentType - 5) {
                            int row = (recipe.GridIndex - tab * 1000) / 100 - 1;
                            int column = recipe.GridIndex % 100 - 1;
                            if (row >= 0 && column >= 0 && row < maxRowCount && column < maxColumnCount) {
                                int index = row * maxColumnCount + column;
                                if (index >= 0
                                    && index < __instance.indexArray.Length
                                    && __instance.indexArray[index] == 0) {
                                    //这个条件可以避免配方图标占用原有物品图标
                                    int index6 = SignalProtoSet.SignalId(ESignalType.Recipe, recipe.ID);
                                    __instance.indexArray[index] = iconSet.signalIconIndex[index6];
                                    __instance.signalArray[index] = index6;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 如果移动到配方上面，显示配方的弹窗
        /// </summary>
        [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker._OnUpdate))]
        [HarmonyPostfix]
        public static void UISignalPicker__OnUpdate_Postfix(ref UISignalPicker __instance) {
            if (__instance.screenItemTip == null) {
                return;
            }
            if (__instance.hoveredIndex < 0) {
                __instance.screenItemTip.showingItemId = 0;
                __instance.screenItemTip.gameObject.SetActive(false);
                return;
            }
            int index = __instance.signalArray[__instance.hoveredIndex];
            if (index > 20000 && index < 32000) {
                var recipe = LDB.recipes.Select(index - 20000);
                if (recipe == null) {
                    __instance.screenItemTip.showingItemId = 0;
                    __instance.screenItemTip.gameObject.SetActive(false);
                    return;
                }
                int num1 = __instance.hoveredIndex % maxColumnCount;
                int num2 = __instance.hoveredIndex / maxColumnCount;
                if (!__instance.screenItemTip.gameObject.activeSelf) {
                    __instance.screenItemTip.gameObject.SetActive(true);
                }
                //itemId为负数时，表示显示id为-itemId的recipe
                __instance.screenItemTip.SetTip(-recipe.ID,
                    __instance.itemTipAnchor, new(num1 * 46 + 15, -num2 * 46 - 50), __instance.iconImage.transform,
                    0, 0, UIButton.ItemTipType.Other, isSign: true);
            }
        }
    }
}
