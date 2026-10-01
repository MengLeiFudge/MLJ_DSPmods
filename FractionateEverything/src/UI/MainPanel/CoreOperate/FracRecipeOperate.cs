using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Civilization;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// 查看当前已知的稳定配方、完整输入输出和运行边界。
/// </summary>
public static class FracRecipeOperate {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static MyComboBox recipeTypeCombo;
    private static MyComboBox recipeCombo;
    private static Text recipeText;
    private static Text runtimeText;
    private static ConfigEntry<int> recipeTypeEntry;
    private static List<WorldviewRecipe> visibleRecipes = [];
    private static string visibleRecipeSignature = string.Empty;
    private static int selectedRecipeId;
    private static bool suppressSelection;

    private static ERecipe SelectedRecipeType => RecipeTypes[recipeTypeEntry.Value];

    /// <summary>
    /// 注册稳定配方目录页面文本。
    /// </summary>
    public static void AddTranslations() {
        Register("分馏配方", "Fractionation Recipes");
        Register("分馏配方摘要",
            "Inspect discovered stable recipes, all inputs and outputs, and their tower runtime boundaries.",
            "查看已发现的稳定配方、全部输入输出与对应塔型运行边界。");
        Register("配方类型", "Recipe Type");
        Register("配方结构", "Recipe Structure");
        Register("运行边界", "Runtime Boundaries");
        Register("尚无可见配方", "No recipes in this category have been discovered yet.");
        Register("稳定配方ID", "Stable Recipe ID");
        Register("运行塔型", "Tower");
        Register("成功率", "Success Rate");
        Register("配方输入", "Inputs");
        Register("配方输出", "Outputs");
        Register("研究解析", "Research Analysis");
        Register("每次成功消耗概率", "consumption chance per success");
        Register("按回收率结算", "scaled by recovery ratio");
        Register("输入缓存", "Input Buffer");
        Register("产物缓存", "Product Buffer");
        Register("流动输出缓存", "Flow Buffer");
        Register("全局堆叠", "Global Stack");
        Register("电网供能", "Power Grid");
        Register("矩阵营养", "Matrix Nutrition");
        Register("需要", "Required");
        Register("不需要", "Not Required");
        Register("配方目录基础塔说明",
            "Basic towers use the power grid, global stacking, continuous processing, and fixed 20-item output buffers.",
            "基础塔使用电网与全局堆叠，持续处理物品，并采用固定 20 件输出缓存。");
        Register("配方目录高级塔说明",
            "Advanced towers use matrix nutrition and durability. Every per-item input, product, and flow buffer is limited to 20.",
            "高级塔使用矩阵营养与耐久；每种输入、产物和流动输出缓存均限制为 20 件。");
    }

    /// <summary>
    /// 读取配方类别选择。
    /// </summary>
    public static void LoadConfig(ConfigFile configFile) {
        recipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0, "配方目录当前分类。");
        recipeTypeEntry.Value = Mathf.Clamp(recipeTypeEntry.Value, 0, RecipeTypes.Length - 1);
    }

    /// <summary>
    /// 创建类别、稳定配方选择与详情区域。
    /// </summary>
    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        RefreshVisibleRecipes(force: true);
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(64f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("分馏配方", "分馏配方摘要", pos: (0, 0), objectName: "frac-recipe-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "frac-recipe-selector", strong: true,
                        cols: [Px(90f), Px(210f), Px(90f), 1], columnGap: 8f, children: [
                            TextNode("配方类型", 15, pos: (0, 0), objectName: "frac-recipe-type-label"),
                            ComboBoxNode(onBuilt: combo => {
                                    recipeTypeCombo = combo.WithItems(RecipeTypeShortNames)
                                        .WithSize(200f, 0f)
                                        .WithConfigEntry(recipeTypeEntry)
                                        .WithOnSelChanged(_ => OnRecipeTypeChanged());
                                }, pos: (0, 1), objectName: "frac-recipe-type-combo"),
                            TextNode("当前配方", 15, pos: (0, 2), objectName: "frac-recipe-label"),
                            ComboBoxNode(onBuilt: combo => {
                                    recipeCombo = combo.WithSize(520f, 0f)
                                        .WithOnSelChanged(OnRecipeSelected);
                                    RefreshRecipeCombo();
                                }, pos: (0, 3), objectName: "frac-recipe-combo"),
                        ]),
                    Grid(pos: (2, 0), cols: [1, 1], columnGap: PageLayout.Gap, children: [
                        ContentCard(pos: (0, 0), objectName: "frac-recipe-structure-card", strong: true,
                            rows: [Px(28f), 1], rowGap: PageLayout.InnerGap, children: [
                                CardTitleNode("配方结构", pos: (0, 0), objectName: "frac-recipe-structure-title"),
                                TextNode("", 13, White, wrap: true, anchor: TextAnchor.UpperLeft,
                                    onBuilt: text => {
                                        recipeText = text;
                                        text.supportRichText = true;
                                    }, pos: (1, 0), objectName: "frac-recipe-structure-text"),
                            ]),
                        ContentCard(pos: (0, 1), objectName: "frac-recipe-runtime-card",
                            rows: [Px(28f), 1], rowGap: PageLayout.InnerGap, children: [
                                CardTitleNode("运行边界", pos: (0, 0), objectName: "frac-recipe-runtime-title"),
                                TextNode("", 13, White, wrap: true, anchor: TextAnchor.UpperLeft,
                                    onBuilt: text => {
                                        runtimeText = text;
                                        text.supportRichText = true;
                                    }, pos: (1, 0), objectName: "frac-recipe-runtime-text"),
                            ]),
                    ]),
                ]));
        UpdateUI();
    }

    /// <summary>
    /// 刷新研究发现进度与配方详情。
    /// </summary>
    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }
        RefreshVisibleRecipes();
        WorldviewRecipe recipe = visibleRecipes.FirstOrDefault(candidate => candidate.PersistentId == selectedRecipeId);
        header.Title.text = "分馏配方".Translate().WithColor(Orange);
        header.Summary.text = "分馏配方摘要".Translate().WithColor(White);
        recipeText.text = BuildRecipeText(recipe);
        runtimeText.text = BuildRuntimeText(recipe);
    }

    private static void OnRecipeTypeChanged() {
        if (suppressSelection) {
            return;
        }
        selectedRecipeId = 0;
        RefreshVisibleRecipes(force: true);
        UpdateUI();
    }

    private static void OnRecipeSelected(int index) {
        if (suppressSelection || index < 0 || index >= visibleRecipes.Count) {
            return;
        }
        selectedRecipeId = visibleRecipes[index].PersistentId;
        UpdateUI();
    }

    private static void RefreshVisibleRecipes(bool force = false) {
        visibleRecipes = GetVisibleRecipes(SelectedRecipeType);
        string signature = $"{(int)SelectedRecipeType}:{string.Join(",", visibleRecipes.Select(recipe => recipe.PersistentId))}";
        if (!force && signature == visibleRecipeSignature) {
            return;
        }
        visibleRecipeSignature = signature;
        if (visibleRecipes.All(recipe => recipe.PersistentId != selectedRecipeId)) {
            selectedRecipeId = visibleRecipes.FirstOrDefault()?.PersistentId ?? 0;
        }
        RefreshRecipeCombo();
    }

    private static void RefreshRecipeCombo() {
        if (recipeCombo == null) {
            return;
        }
        string[] names = visibleRecipes.Count == 0
            ? ["尚无可见配方"]
            : visibleRecipes.Select(recipe => recipe.DisplayName).ToArray();
        int index = Math.Max(0, visibleRecipes.FindIndex(recipe => recipe.PersistentId == selectedRecipeId));
        suppressSelection = true;
        recipeCombo.SetItems(names);
        recipeCombo.SetIndex(index);
        suppressSelection = false;
    }

    private static List<WorldviewRecipe> GetVisibleRecipes(ERecipe type) {
        HashSet<int> eligibleDirections = new(ResearchManager.GetEligibleDirections());
        return RecipeManager.WorldviewRecipes
            .Where(recipe => recipe.RecipeType == type)
            .Where(recipe => recipe.Kind == WorldviewRecipeKind.BasicAnalysis
                ? recipe.InputID == IFE残片 || eligibleDirections.Contains(recipe.InputID)
                : ResearchManager.IsContentUnlocked(recipe.StableId))
            .OrderBy(recipe => recipe.PersistentId)
            .ToList();
    }

    private static string BuildRecipeText(WorldviewRecipe recipe) {
        if (recipe == null) {
            return "尚无可见配方".Translate().WithColor(Gray);
        }

        var text = new StringBuilder();
        text.AppendLine(recipe.DisplayName.WithColor(Orange));
        text.AppendLine($"{"稳定配方ID".Translate()}：{recipe.StableId}");
        text.AppendLine($"{"运行塔型".Translate()}：{GetTowerName(recipe.RecipeType)}");
        text.AppendLine($"{"成功率".Translate()}：{recipe.CycleSuccessProbability:P0}");
        text.AppendLine();
        text.AppendLine("配方输入".Translate().WithColor(Orange));
        foreach (WorldviewRecipeInput input in recipe.Inputs) {
            string probability = input.ConsumeProbability < 0.9999f
                ? $"  ({input.ConsumeProbability:P0} {"每次成功消耗概率".Translate()})"
                : string.Empty;
            text.AppendLine($"- {input.Count} × {GetItemName(input.ItemId)}{probability}");
        }
        text.AppendLine();
        text.AppendLine("配方输出".Translate().WithColor(Orange));
        if (recipe.Kind == WorldviewRecipeKind.BasicAnalysis) {
            text.AppendLine($"- {"研究解析".Translate()}");
        } else {
            foreach (WorldviewRecipeOutput output in recipe.Products) {
                string recovery = output.ScalesWithRecovery ? $"  ({"按回收率结算".Translate()})" : string.Empty;
                text.AppendLine($"- {output.Count:0.##} × {GetItemName(output.ItemId)}{recovery}");
            }
        }
        return text.ToString();
    }

    private static string BuildRuntimeText(WorldviewRecipe recipe) {
        if (recipe == null) {
            return "尚无可见配方".Translate().WithColor(Gray);
        }

        bool advanced = recipe.IsAdvanced;
        var text = new StringBuilder();
        text.AppendLine((advanced ? "配方目录高级塔说明" : "配方目录基础塔说明").Translate());
        text.AppendLine();
        text.AppendLine($"{"电网供能".Translate()}：{(advanced ? "不需要" : "需要").Translate()}");
        text.AppendLine($"{"矩阵营养".Translate()}：{(advanced ? "需要" : "不需要").Translate()}");
        text.AppendLine($"{"全局堆叠".Translate()}：{StackingManager.CurrentMaxStack}");
        if (advanced) {
            text.AppendLine($"{"输入缓存".Translate()}：20 / item");
        }
        text.AppendLine($"{"产物缓存".Translate()}：20 / item");
        text.AppendLine($"{"流动输出缓存".Translate()}：20 / item");
        return text.ToString();
    }

    private static string GetTowerName(ERecipe type) {
        int itemId = type.GetSpriteItemId();
        return GetItemName(itemId);
    }

    private static string GetItemName(int itemId) => LDB.items.Select(itemId)?.name ?? itemId.ToString();

    /// <summary>
    /// 读取旧页面空块并保持块协议兼容。
    /// </summary>
    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    /// <summary>
    /// 导出页面空块。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    /// <summary>
    /// 切档时重置仅用于显示的配方选择。
    /// </summary>
    public static void IntoOtherSave() {
        selectedRecipeId = 0;
        visibleRecipeSignature = string.Empty;
    }
}
