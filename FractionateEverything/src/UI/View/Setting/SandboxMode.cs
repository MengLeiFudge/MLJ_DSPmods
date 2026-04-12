using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class SandboxMode {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<float> ExpMultiRatioEntry;
    public static float ExpMultiRatio => GameMain.sandboxToolsEnabled ? ExpMultiRatioEntry.Value : 1;

    private class MultiRatioMapper() : MyWindow.RangeValueMapper<float>(0, 40) {
        public override float IndexToValue(int index) => (float)Math.Pow(10, (index - 10) / 10.0);
        public override int ValueToIndex(float value) => (int)(Math.Log10(value) * 10.0 + 10);
    }

    public static void AddTranslations() {
        Register("沙盒模式", "Sandbox Mode");

        Register("解锁所有分馏配方", "Unlock all fractionation recipes");
        Register("锁定所有分馏配方", "Lock all fractionation recipes");
        Register("经验获取倍率", "Experience gain multiplier");
        Register("经验获取倍率说明",
            "Adjust the speed at which recipe experience is gained by processing items.",
            "调整通过处理物品获取配方经验的速度。");
    }

    public static void LoadConfig(ConfigFile configFile) {
        ExpMultiRatioEntry = configFile.Bind("TabSetting", "ExpMultiRatio", 1.0f, "经验获取倍率");
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        CreateUIInternal(wnd, trans);
    }

    private static void CreateUIInternal(MyWindow wnd, RectTransform parent) {
        PageLayout.HeaderRefs header = PageLayout.CreatePageHeader(wnd, parent, "沙盒模式", "", "sandbox-mode-header");
        header.Summary.text = "集中放置高影响力的沙盒操作，避免按钮散落在页面角落".WithColor(White);
        RectTransform actionCard = PageLayout.CreateContentCard(parent, "sandbox-mode-action-card", 0f,
            PageLayout.HeaderHeight + PageLayout.Gap, PageLayout.DesignWidth, 250f, true);
        RectTransform configCard = PageLayout.CreateContentCard(parent, "sandbox-mode-config-card", 0f,
            PageLayout.HeaderHeight + PageLayout.Gap * 2f + 250f, PageLayout.DesignWidth, 413f);
        PageLayout.AddCardTitle(wnd, actionCard, 18f, 14f, "批量操作", 15, "sandbox-mode-action-title");
        PageLayout.AddCardTitle(wnd, configCard, 18f, 14f, "倍率与说明", 15, "sandbox-mode-config-title");

        float x = 0f;
        float y = 18f;
        wnd.AddButton(0, 3, y + 34f, actionCard, "锁定所有分馏配方", 16, "button-lock-all-recipes",
            RecipeManager.LockAllFracRecipes);
        wnd.AddButton(1, 3, y + 34f, actionCard, "获得所有分馏配方", 16, "button-reward-all-recipes",
            RecipeManager.RewardAllFracRecipes);
        wnd.AddButton(2, 3, y + 34f, actionCard, "满级所有分馏配方", 16, "button-max-all-recipes",
            RecipeManager.MaxAllFracRecipes);
        y += 36f;
        var txt = wnd.AddText2(x + 18f, y + 34f, configCard, "经验获取倍率", 15, "text-exp-multi-ratio");
        wnd.AddSlider(x + 23f + txt.preferredWidth, y + 34f, configCard,
            ExpMultiRatioEntry, new MultiRatioMapper(), "0.#", 200f);
        wnd.AddTipsButton2(x + 28f + txt.preferredWidth + 200 + 5, y + 34f, configCard,
            "经验获取倍率", "经验获取倍率说明");
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
