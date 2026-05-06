using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View.ProgressTask;
using FE.UI.View.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static partial class FracRecipeOperate {
    // ==================== UI 创建 ====================

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("分馏配方", objectName: "frac-recipe-header", pos: (0, 0),
                        onBuilt: refs => refs.Summary.text = "查看配方成功率、损毁率、产物结构与强化等级信息".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "frac-recipe-content-card",
                        strong: true,
                        onBuilt: root => tab = root,
                        rows: BuildContentRows(),
                        cols: [Px(600f), Fr(1)],
                        rowGap: 0f,
                        columnGap: 20f,
                        children: [
                            Grid(pos: (0, 0), span: (1, 2),
                                cols: [Px(72f), Px(44f), Px(28f), Px(90f), Px(220f), Fr(1)],
                                columnGap: 8f,
                                children: [
                                    TextNode("当前物品", 15, onBuilt: text => txtCurrItem = text,
                                        pos: (0, 0), objectName: "textCurrItem"),
                                    ImageButtonNode(SelectedItem, 40f,
                                        onBuilt: btn => btnSelectedItem = btn.WithClickEvent(
                                            () => { OnButtonChangeItemClick(false, 22f); },
                                            () => { OnButtonChangeItemClick(true, 22f); }),
                                        pos: (0, 1), objectName: "button-change-item"),
                                    TipsButtonNode("提示", "分馏配方提示按钮说明1",
                                        pos: (0, 2), objectName: "frac-recipe-tip"),
                                    TextNode("配方类型", 15, pos: (0, 3), objectName: "frac-recipe-type-label"),
                                    ComboBoxNode(onBuilt: combo => combo.WithItems(RecipeTypeShortNames)
                                            .WithSize(200, 0).WithConfigEntry(RecipeTypeEntry),
                                        pos: (0, 4), objectName: "frac-recipe-type-combo"),
                                ]),
                            Grid(pos: (1, 0), span: (1, 2), cols: [1, 1, 1, 1], columnGap: 12f,
                                children: BuildSandboxButtonNodes()),
                            ..BuildInfoLineNodes(),
                        ]),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildContentRows() {
        var rows = new List<LayoutTrack> { Px(44f), Px(36f) };
        for (int i = 0; i < InfoLineCount; i++) {
            rows.Add(Px(LineHeight));
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildSandboxButtonNodes() {
        return [
            ButtonNode("重置等级", onClick: () => {
                if (SelectedRecipe != null) {
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, 0,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[0] = btn, pos: (0, 0), objectName: "frac-recipe-reset-level"),
            ButtonNode("等级-1", onClick: () => {
                if (SelectedRecipe != null) {
                    int level = RecipeGrowthQueries.GetLevel(SelectedRecipe);
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, level - 1,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[1] = btn, pos: (0, 1), objectName: "frac-recipe-level-down"),
            ButtonNode("等级+1", onClick: () => {
                if (SelectedRecipe != null) {
                    int level = RecipeGrowthQueries.GetLevel(SelectedRecipe);
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, level + 1,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[2] = btn, pos: (0, 2), objectName: "frac-recipe-level-up"),
            ButtonNode("等级升满", onClick: () => {
                if (SelectedRecipe != null) {
                    RecipeGrowthExecutor.SetLevelForSandbox(SelectedRecipe, 5,
                        RecipeGrowthManager.BuildContext(manual: true));
                }
            }, onBuilt: btn => recipeSandboxBtn[3] = btn, pos: (0, 3), objectName: "frac-recipe-level-max"),
        ];
    }

    private static IReadOnlyList<LayoutNode> BuildInfoLineNodes() {
        int[] rang = !GenesisBook.Enable ? [0, 1, 2, 4, 10] : [0, 4, 10];
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < InfoLineCount; i++) {
            int index = i;
            int row = i + 2;
            nodes.Add(Grid(pos: (row, 0), cols: [Px(72f), Px(24f), Px(24f), Fr(1)], children: [
                TextNode("", 15, onBuilt: text => {
                        txtRecipeInfo[index] = text;
                        text.rectTransform.sizeDelta = new Vector2(560f, LineHeight);
                    },
                    pos: (0, 0), span: (1, 4), objectName: $"frac-recipe-info-{index}"),
                TextNode("", 15, onBuilt: text => {
                        txtProductLeft[index] = text;
                        text.gameObject.SetActive(false);
                    },
                    pos: (0, 0), objectName: $"frac-recipe-product-ratio-{index}"),
                ImageButtonNode(size: IconSize, onBuilt: btn => {
                        btn.gameObject.SetActive(false);
                        btnRecipeInfoIcons[index] = btn;
                    },
                    pos: (0, 1), objectName: $"frac-recipe-product-icon-{index}"),
                SliderNode(selectedInc, rang, null, 200f, onBuilt: slider => {
                        slider.gameObject.SetActive(false);
                        incSliders[index] = slider;
                    },
                    pos: (0, 3), objectName: $"frac-recipe-inc-slider-{index}"),
            ]));
            if (i < LevelLineCount) {
                nodes.Add(TextNode("", 13, onBuilt: text => txtLevelInfo[index] = text,
                    pos: (row, 1), objectName: $"frac-recipe-level-info-{index}"));
            }
        }

        return nodes;
    }

    // ==================== UI 更新 ====================
}
