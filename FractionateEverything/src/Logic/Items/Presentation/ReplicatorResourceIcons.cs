using System.Runtime.CompilerServices;

using FE.Logic.Fractionation.Recipes;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

using static FE.FractionateEverything;

namespace FE.Logic.Items.Presentation;

/// <summary>为制造器分馏页补充非手搓资源图标，只提供物品提示，不向配方目录添加虚假制造来源。</summary>
internal static class ReplicatorResourceIcons {
    private static readonly ConditionalWeakTable<UIReplicatorWindow, GameObject[]> Views = new();

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(UIReplicatorWindow), nameof(UIReplicatorWindow.RefreshRecipeIcons))]
    private static void UIReplicatorWindow_RefreshRecipeIcons_Postfix(UIReplicatorWindow __instance) {
        UIReplicatorWindow window = __instance;
        bool visible = window.currentType == tab分馏;
        if (!visible && !Views.TryGetValue(window, out _)) { return; }
        GameObject[] icons = Views.GetValue(window, Create);
        for (int i = 0; i < icons.Length; i++) {
            ItemProto item = LDB.items.Select(FractionationTabLayout.ResourceItems[i]);
            bool show = visible && item != null && (window.isInstantItem || GameMain.history.ItemUnlocked(item.ID));
            icons[i].SetActive(show);
            if (!show) { continue; }
            int row = item.GridIndex % 1000 / 100 - 1;
            int column = item.GridIndex % 100 - 1;
            ((RectTransform)icons[i].transform).anchoredPosition = new Vector2(column * 46 + 3, -row * 46 - 3);
            icons[i].GetComponent<Image>().sprite = item.iconSprite;
        }
    }

    private static GameObject[] Create(UIReplicatorWindow window) {
        int[] ids = FractionationTabLayout.ResourceItems;
        GameObject[] icons = new GameObject[ids.Length];
        for (int i = 0; i < ids.Length; i++) {
            var icon = new GameObject($"fe-resource-{ids[i]}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIButton));
            var rect = (RectTransform)icon.transform;
            rect.SetParent(window.recipeBg.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(40, 40);
            Image image = icon.GetComponent<Image>();
            image.color = Color.white;
            Button button = icon.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            UIButton uiButton = icon.GetComponent<UIButton>();
            // 原版 LateUpdate 直接遍历该数组；无过渡效果也必须提供空数组。
            uiButton.transitions = [];
            uiButton.Init();
            uiButton.tips.type = UIButton.ItemTipType.Item;
            uiButton.tips.itemId = ids[i];
            uiButton.tips.delay = window.showTipsDelay;
            uiButton.tips.corner = window.tipAnchor;
            icons[i] = icon;
        }
        return icons;
    }
}
