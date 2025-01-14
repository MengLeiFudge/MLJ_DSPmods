using HarmonyLib;

namespace FractionateEverything.UI {
    public static class UIItemTipPatch {
        /// <summary>
        /// 物品提示增加分馏配方
        /// </summary>
        [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
        [HarmonyPostfix]
        public static void UIItemTip_SetTip_Postfix(ref int itemId) { }

        /// <summary>
        /// 如果物品、配方的详情窗口最下面的制作方式有分馏配方，修改对应显示内容
        /// </summary>
        [HarmonyPatch(typeof(UIRecipeEntry), nameof(UIRecipeEntry.SetRecipe))]
        [HarmonyPrefix]
        public static bool UIRecipeEntry_SetRecipe_Prefix(ref UIRecipeEntry __instance, RecipeProto recipe) {
            // if (recipe.Type != ERecipeType.Fractionate) {
            //     return true;
            // }
            // Dictionary<int, float> dic = GetNumRatioNaturalResource(recipe.Items[0]);
            // if (dic.ContainsKey(1) && dic[1] == 0) {
            //     //降级的就不管了
            //     dic = GetNumRatioUpgrade(recipe.Items[0]);
            // }
            // var p = dic.FirstOrDefault(p => p.Key > 0);
            // int index1 = 0;
            // int x1 = 0;
            //
            // ItemProto itemProto = LDB.items.Select(recipe.Results[0]);
            // __instance.icons[index1].sprite = itemProto?.iconSprite;
            // //产物数目使用dic首个不为损毁的概率的key
            // __instance.countTexts[index1].text = p.Key.ToString();
            // __instance.icons[index1].rectTransform.anchoredPosition = new(x1, 0.0f);
            // __instance.icons[index1].gameObject.SetActive(true);
            //
            // ++index1;
            // x1 += 40;
            // __instance.arrow.anchoredPosition = new(x1, -27f);
            // //概率显示包括dic首个不为损毁的概率的Value，以及损毁概率（如果有的话）
            // string str = p.Value.FormatP();
            // if (enableDestroy && dic.TryGetValue(-1, out float destroyRatio)) {
            //     str += "(" + destroyRatio.FormatP() + ")";
            // }
            // __instance.timeText.text = str;
            // //横向拓展，避免显示不下
            // __instance.timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            //
            // int x2 = x1 + 40;
            // itemProto = LDB.items.Select(recipe.Items[0]);
            // __instance.icons[index1].sprite = itemProto?.iconSprite;
            // //原料数目使用1
            // __instance.countTexts[index1].text = "1";
            // __instance.icons[index1].rectTransform.anchoredPosition = new(x2, 0.0f);
            // __instance.icons[index1].gameObject.SetActive(true);
            //
            // ++index1;
            // x2 += 40;
            // for (int index4 = index1; index4 < 7; ++index4)
            //     __instance.icons[index4].gameObject.SetActive(false);
            // return false;
            return true;
        }
    }
}
