using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Fractionation.FracRecipes;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Setting;

/// <summary>
/// 沙盒模式倍率和调试开关设置页面。
/// </summary>
public static class SandboxMode {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<float> ExpMultiRatioEntry;
    public static float ExpMultiRatio => GameMain.sandboxToolsEnabled ? ExpMultiRatioEntry.Value : 1;

    /// <summary>
    /// 沙盒倍率滑条的指数映射。
    /// </summary>
    private class MultiRatioMapper() : MyWindow.RangeValueMapper<float>(0, 40) {
        public override float IndexToValue(int index) => (float)Math.Pow(10, (index - 10) / 10.0);
        public override int ValueToIndex(float value) => (int)(Math.Log10(value) * 10.0 + 10);
    }

    public static void AddTranslations() {
        Register("沙盒模式", "Sandbox Mode");

        Register("解锁所有分馏配方", "Unlock all fractionation recipes");
        Register("锁定所有分馏配方", "Lock all fractionation recipes");
        Register("所有分馏配方已锁定。", "All fractionation recipes have been locked.");
        Register("所有分馏配方已等级+1。", "All fractionation recipes gained +1 level.");
        Register("所有分馏配方已满级。", "All fractionation recipes have been maxed.");
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
        BuildLayout(wnd, parent,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1, 3],
                rowGap: PageLayout.Gap,
                children: [
                    Header("沙盒模式", objectName: "sandbox-mode-header", pos: (0, 0),
                        onBuilt: refs => refs.Summary.text = "批量锁定或解锁配方，调整经验倍率，并执行测试用沙盒操作".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "sandbox-mode-action-card",
                        strong: true,
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("批量操作", pos: (0, 0), objectName: "sandbox-mode-action-title"),
                            Grid(
                                pos: (1, 0),
                                cols: [1, 1, 1],
                                columnGap: PageLayout.InnerGap,
                                children: [
                                    ButtonNode("锁定所有分馏配方", RecipeManager.LockAllFracRecipes,
                                        pos: (0, 0), objectName: "button-lock-all-recipes"),
                                    ButtonNode("获得所有分馏配方", RecipeManager.RewardAllFracRecipes,
                                        pos: (0, 1), objectName: "button-reward-all-recipes"),
                                    ButtonNode("满级所有分馏配方", RecipeManager.MaxAllFracRecipes,
                                        pos: (0, 2), objectName: "button-max-all-recipes"),
                                ]),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "sandbox-mode-config-card",
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("倍率与说明", pos: (0, 0), objectName: "sandbox-mode-config-title"),
                            LabeledSliderNode("经验获取倍率", ExpMultiRatioEntry, new MultiRatioMapper(), "0.#",
                                tipTitle: "经验获取倍率", tipContent: "经验获取倍率说明",
                                pos: (1, 0), objectName: "sandbox-mode-exp-ratio"),
                        ]),
                ]));
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
