using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using static FE.UI.Components.GridDsl;
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
        BuildLayout(wnd, parent,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1, 3],
                rowGap: PageLayout.Gap,
                children: [
                    Header("沙盒模式", objectName: "sandbox-mode-header", pos: (0, 0),
                        onBuilt: refs => refs.Summary.text = "集中放置高影响力的沙盒操作，避免按钮散落在页面角落".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "sandbox-mode-action-card",
                        strong: true,
                        rows: [Px(24f), 1],
                        children: [
                            Node(pos: (0, 0), objectName: "sandbox-mode-action-title",
                                build: (w, root) => {
                                    PageLayout.AddCardTitle(w, root, 0f, 0f, "批量操作", 15, "sandbox-mode-action-title");
                                }),
                            Node(pos: (1, 0), objectName: "sandbox-mode-action-body", build: (w, root) => {
                                w.AddButton(0, 3, 32f, root, "锁定所有分馏配方", 15, "button-lock-all-recipes",
                                    RecipeManager.LockAllFracRecipes);
                                w.AddButton(1, 3, 32f, root, "获得所有分馏配方", 15, "button-reward-all-recipes",
                                    RecipeManager.RewardAllFracRecipes);
                                w.AddButton(2, 3, 32f, root, "满级所有分馏配方", 15, "button-max-all-recipes",
                                    RecipeManager.MaxAllFracRecipes);
                            }),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "sandbox-mode-config-card",
                        rows: [Px(24f), 1],
                        children: [
                            Node(pos: (0, 0), objectName: "sandbox-mode-config-title",
                                build: (w, root) => {
                                    PageLayout.AddCardTitle(w, root, 0f, 0f, "倍率与说明", 15, "sandbox-mode-config-title");
                                }),
                            Node(pos: (1, 0), objectName: "sandbox-mode-config-body", build: (w, root) => {
                                var txt = w.AddText2(0f, 32f, root, "经验获取倍率", 15, "text-exp-multi-ratio");
                                w.AddSlider(23f + txt.preferredWidth, 32f, root,
                                    ExpMultiRatioEntry, new MultiRatioMapper(), "0.#", 200f);
                                w.AddTipsButton2(28f + txt.preferredWidth + 200 + 5, 32f, root,
                                    "经验获取倍率", "经验获取倍率说明");
                            }),
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
