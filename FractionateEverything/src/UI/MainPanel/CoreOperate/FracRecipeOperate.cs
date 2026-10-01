using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Civilization;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// 完整配方图鉴：按类别列出全部配方槽位，并仅向玩家披露已经发现的配方内容。
/// </summary>
public static class FracRecipeOperate {
    private static readonly ERecipe[] GalleryRecipeTypes = [
        ERecipe.BasicMineral,
        ERecipe.BasicAnalysis,
        ERecipe.AdvancedMineral,
        ERecipe.AdvancedInteraction,
        ERecipe.AdvancedConversion,
    ];

    private static readonly HashSet<int> eligibleDirectionIds = [];
    private static readonly HashSet<string> unlockedContentIds = [];
    private static ConfigEntry<int> recipeTypeEntry;
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text progressHeadlineText;
    private static Text overallProgressText;
    private static Text categoryProgressText;
    private static Image overallProgressFill;
    private static Image categoryProgressFill;
    private static Text catalogTitleText;
    private static UIButton[] catalogButtons = [];
    private static RectTransform catalogRoot;
    private static Text recipeTitleText;
    private static Text recipeText;
    private static Text runtimeText;
    private static RectTransform recipeTextRoot;
    private static RectTransform runtimeTextRoot;
    private static List<WorldviewRecipe> currentRecipes = [];
    private static int selectedRecipeId;
    private static string catalogSignature = string.Empty;
    private static bool resetDetailScroll;

    private static ERecipe SelectedRecipeType =>
        GalleryRecipeTypes[Mathf.Clamp(recipeTypeEntry.Value, 0, GalleryRecipeTypes.Length - 1)];

    /// <summary>
    /// 注册配方图鉴文本。
    /// </summary>
    public static void AddTranslations() {
        Register("配方图鉴", "Recipe Gallery");
        Register("配方图鉴摘要",
            "Browse every fractionation recipe slot, discovery progress, recipe structure, and runtime boundaries.",
            "浏览全部分馏配方槽位、发现进度、配方结构和运行边界；未发现内容不会提前泄露。");
        Register("配方档案", "Recipe Archive");
        Register("已发现配方", "Discovered Recipes");
        Register("当前分类进度", "Current Category");
        Register("配方目录", "Recipe Directory");
        Register("配方类别", "Recipe Category");
        Register("未知配方槽位", "???", "？？？");
        Register("配方结构", "Recipe Structure");
        Register("运行参数", "Runtime Parameters");
        Register("稳定配方ID", "Stable Recipe ID");
        Register("运行塔型", "Tower");
        Register("成功率", "Success Rate");
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
        Register("配方尚未发现", "This recipe has not been discovered yet.", "此配方尚未发现。");
        Register("配方目录为空", "No recipes are registered in this category.", "当前分类尚未注册配方。");
        Register("配方输入", "Inputs");
    }

    /// <summary>
    /// 加载图鉴中记忆的配方分类。
    /// </summary>
    public static void LoadConfig(ConfigFile configFile) {
        recipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0,
            "Recipe category selected in the recipe gallery.");
        recipeTypeEntry.Value = Mathf.Clamp(recipeTypeEntry.Value, 0, GalleryRecipeTypes.Length - 1);
    }

    /// <summary>
    /// 创建配方图鉴的目录与详情双栏。
    /// </summary>
    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        int catalogRowCount = Math.Max(1, GalleryRecipeTypes
            .Select(type => WorldviewRecipes.Count(recipe => recipe.RecipeType == type))
            .DefaultIfEmpty(1)
            .Max());
        float catalogContentHeight = catalogRowCount * 34f + (catalogRowCount - 1) * 4f + 20f;
        catalogButtons = new UIButton[catalogRowCount];

        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(104f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("配方图鉴", "配方图鉴摘要", pos: (0, 0), objectName: "recipe-gallery-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "recipe-gallery-progress", strong: true,
                        rows: [1, 1], cols: [Px(250f), 1], rowGap: 8f, columnGap: 24f,
                        children: [
                            TextNode("", 18, White, anchor: TextAnchor.MiddleLeft, wrap: true,
                                pos: (0, 0), span: (2, 1), onBuilt: text => {
                                    progressHeadlineText = text;
                                    text.supportRichText = true;
                                }, objectName: "recipe-gallery-progress-headline"),
                            Node(pos: (0, 1), objectName: "recipe-gallery-overall-progress",
                                build: (_, root) => BuildProgressMetric(root, out overallProgressText,
                                    out overallProgressFill)),
                            Node(pos: (1, 1), objectName: "recipe-gallery-category-progress",
                                build: (_, root) => BuildProgressMetric(root, out categoryProgressText,
                                    out categoryProgressFill, new Color(0.45f, 0.85f, 0.48f, 0.92f))),
                        ]),
                    Grid(pos: (2, 0), cols: [Px(410f), 1], columnGap: PageLayout.Gap,
                        objectName: "recipe-gallery-body", children: [
                            ContentCard(pos: (0, 0), objectName: "recipe-gallery-directory-card", strong: true,
                                rows: [Px(28f), Px(44f), 1], rowGap: 8f, children: [
                                    CardTitleNode("配方目录", pos: (0, 0), onBuilt: text => catalogTitleText = text,
                                        objectName: "recipe-gallery-directory-title"),
                                    LabeledComboBoxNode("配方类别",
                                        GalleryRecipeTypes.Select(type => type.GetShortName()).ToArray(),
                                        recipeTypeEntry.Value, OnRecipeTypeChanged,
                                        pos: (1, 0),
                                        objectName: "recipe-gallery-category-combo"),
                                    ScrollableArea(catalogContentHeight, pos: (2, 0),
                                        objectName: "recipe-gallery-directory-scroll",
                                        rows: Enumerable.Repeat(Px(34f), catalogRowCount).ToArray(),
                                        rowGap: 4f, onBuilt: root => catalogRoot = root,
                                        children: BuildCatalogButtonNodes(catalogRowCount)),
                                ]),
                            Grid(pos: (0, 1), rows: [1, Px(180f)], rowGap: PageLayout.Gap,
                                objectName: "recipe-gallery-detail-column", children: [
                                    ContentCard(pos: (0, 0), objectName: "recipe-gallery-structure-card",
                                        rows: [Px(28f), 1], rowGap: 8f, children: [
                                            CardTitleNode("配方结构", pos: (0, 0),
                                                onBuilt: text => recipeTitleText = text,
                                                objectName: "recipe-gallery-structure-title"),
                                            ScrollableArea(520f, pos: (1, 0),
                                                objectName: "recipe-gallery-structure-scroll",
                                                onBuilt: root => recipeTextRoot = root, children: [
                                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft,
                                                        wrap: true, pos: (0, 0), onBuilt: text => {
                                                            recipeText = text;
                                                            text.supportRichText = true;
                                                        }, objectName: "recipe-gallery-structure-text"),
                                                ]),
                                        ]),
                                    ContentCard(pos: (1, 0), objectName: "recipe-gallery-runtime-card",
                                        rows: [Px(28f), 1], rowGap: 8f, children: [
                                            CardTitleNode("运行参数", pos: (0, 0),
                                                objectName: "recipe-gallery-runtime-title"),
                                            ScrollableArea(220f, pos: (1, 0),
                                                objectName: "recipe-gallery-runtime-scroll",
                                                onBuilt: root => runtimeTextRoot = root, children: [
                                                    TextNode("", 12, White, anchor: TextAnchor.UpperLeft,
                                                        wrap: true, pos: (0, 0), onBuilt: text => {
                                                            runtimeText = text;
                                                            text.supportRichText = true;
                                                        }, objectName: "recipe-gallery-runtime-text"),
                                                ]),
                                        ]),
                                ]),
                        ]),
                ]));

        RefreshDiscoverySnapshot();
        RefreshCatalog(true);
        UpdateUI();
    }

    /// <summary>
    /// 刷新图鉴进度、目录披露状态和选中配方详情。
    /// </summary>
    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        RefreshDiscoverySnapshot();
        string signature = BuildCatalogSignature();
        if (signature != catalogSignature) {
            RefreshCatalog(false);
        }

        header.Title.text = "配方图鉴".Translate().WithColor(Orange);
        header.Summary.text = "配方图鉴摘要".Translate().WithColor(White);
        UpdateProgress();
        RefreshCatalogButtons();

        WorldviewRecipe selectedRecipe = currentRecipes.FirstOrDefault(recipe =>
            recipe.PersistentId == selectedRecipeId);
        bool discovered = IsRecipeDiscovered(selectedRecipe);
        recipeTitleText.text = (selectedRecipe == null
                ? "配方结构".Translate()
                : discovered ? selectedRecipe.DisplayName : "未知配方槽位".Translate())
            .WithColor(discovered ? Orange : Gray);
        recipeText.text = BuildRecipeText(selectedRecipe, discovered);
        runtimeText.text = BuildRuntimeText(selectedRecipe, discovered);
        PageLayout.ResizeScrollableText(recipeTextRoot, recipeText);
        PageLayout.ResizeScrollableText(runtimeTextRoot, runtimeText);
        if (resetDetailScroll) {
            PageLayout.ScrollToTop(recipeTextRoot);
            PageLayout.ScrollToTop(runtimeTextRoot);
            resetDetailScroll = false;
        }
    }

    private static IReadOnlyList<LayoutNode> BuildCatalogButtonNodes(int rowCount) {
        var nodes = new List<LayoutNode>(rowCount);
        for (int index = 0; index < rowCount; index++) {
            int buttonIndex = index;
            nodes.Add(ButtonNode("", () => SelectRecipe(buttonIndex), pos: (index, 0),
                onBuilt: button => catalogButtons[buttonIndex] = button,
                objectName: $"recipe-gallery-entry-{index + 1}"));
        }
        return nodes;
    }

    private static void BuildProgressMetric(RectTransform root, out Text label, out Image fill,
        Color? fillColor = null) {
        label = PageLayout.AddCenteredText(root, string.Empty, 13, White, TextAnchor.MiddleLeft,
            0f, 0f, root.sizeDelta.x, 20f, $"{root.name}-label");
        label.supportRichText = true;
        fill = PageLayout.CreateProgressBar(root, $"{root.name}-bar",
            0f, Math.Max(22f, root.sizeDelta.y - 10f), root.sizeDelta.x, 8f, fillColor);
    }

    private static void UpdateProgress() {
        int total = WorldviewRecipes.Count();
        int discovered = WorldviewRecipes.Count(IsRecipeDiscovered);
        int categoryTotal = currentRecipes.Count;
        int categoryDiscovered = currentRecipes.Count(IsRecipeDiscovered);

        progressHeadlineText.text =
            $"{"配方档案".Translate().WithColor(Orange)}  {discovered}/{total}\n"
            + $"{"当前配方".Translate()}：{SelectedRecipeType.GetName()}";
        overallProgressText.text = $"{"已发现配方".Translate()}  {discovered}/{total}";
        categoryProgressText.text = $"{"当前分类进度".Translate()}  {categoryDiscovered}/{categoryTotal}";
        PageLayout.SetProgress(overallProgressFill, total > 0 ? discovered / (float)total : 0f);
        PageLayout.SetProgress(categoryProgressFill,
            categoryTotal > 0 ? categoryDiscovered / (float)categoryTotal : 0f);
        catalogTitleText.text =
            $"{"配方目录".Translate()}  {categoryDiscovered}/{categoryTotal}".WithColor(Orange);
    }

    private static void OnRecipeTypeChanged(int index) {
        if (index < 0 || index >= GalleryRecipeTypes.Length) {
            return;
        }
        recipeTypeEntry.Value = index;
        selectedRecipeId = 0;
        RefreshCatalog(true);
        UpdateUI();
    }

    private static void SelectRecipe(int index) {
        if (index < 0 || index >= currentRecipes.Count) {
            return;
        }
        selectedRecipeId = currentRecipes[index].PersistentId;
        resetDetailScroll = true;
        UpdateUI();
    }

    private static void RefreshCatalog(bool resetCatalogScroll) {
        currentRecipes = WorldviewRecipes
            .Where(recipe => recipe.RecipeType == SelectedRecipeType)
            .OrderBy(recipe => recipe.PersistentId)
            .ToList();
        if (currentRecipes.All(recipe => recipe.PersistentId != selectedRecipeId)) {
            selectedRecipeId = currentRecipes.FirstOrDefault(IsRecipeDiscovered)?.PersistentId
                               ?? currentRecipes.FirstOrDefault()?.PersistentId
                               ?? 0;
            resetDetailScroll = true;
        }
        catalogSignature = BuildCatalogSignature();
        RefreshCatalogButtons();
        if (resetCatalogScroll) {
            PageLayout.ScrollToTop(catalogRoot);
            resetDetailScroll = true;
        }
    }

    private static void RefreshCatalogButtons() {
        for (int index = 0; index < catalogButtons.Length; index++) {
            UIButton button = catalogButtons[index];
            if (button == null) {
                continue;
            }
            bool active = index < currentRecipes.Count;
            button.gameObject.SetActive(active);
            if (!active) {
                continue;
            }

            WorldviewRecipe recipe = currentRecipes[index];
            bool discovered = IsRecipeDiscovered(recipe);
            string label = discovered ? recipe.DisplayName : "未知配方槽位".Translate();
            button.SetText($"{index + 1:00}  {label}");
            button.highlighted = recipe.PersistentId == selectedRecipeId;
            button.button.interactable = true;
        }
    }

    private static string BuildCatalogSignature() {
        var builder = new StringBuilder(SelectedRecipeType.ToString());
        foreach (WorldviewRecipe recipe in WorldviewRecipes
                     .Where(candidate => candidate.RecipeType == SelectedRecipeType)
                     .OrderBy(candidate => candidate.PersistentId)) {
            builder.Append('|').Append(recipe.PersistentId).Append(':')
                .Append(IsRecipeDiscovered(recipe) ? '1' : '0');
        }
        return builder.ToString();
    }

    private static bool IsRecipeDiscovered(WorldviewRecipe recipe) {
        if (recipe == null) {
            return false;
        }
        if (recipe.Kind == WorldviewRecipeKind.BasicAnalysis) {
            return recipe.PrimaryInputId == IFE残片 || eligibleDirectionIds.Contains(recipe.PrimaryInputId);
        }
        return unlockedContentIds.Contains(recipe.StableId);
    }

    private static void RefreshDiscoverySnapshot() {
        eligibleDirectionIds.Clear();
        foreach (int itemId in ResearchManager.GetEligibleDirections()) {
            eligibleDirectionIds.Add(itemId);
        }
        unlockedContentIds.Clear();
        foreach (ResearchContentDefinition definition in ResearchManager.GetUnlockedContentDefinitions()) {
            unlockedContentIds.Add(definition.Id);
        }
    }

    private static string BuildRecipeText(WorldviewRecipe recipe, bool discovered) {
        if (recipe == null) {
            return "配方目录为空".Translate().WithColor(Gray);
        }
        if (!discovered) {
            return "配方尚未发现".Translate().WithColor(Gray);
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
                string recovery = output.ScalesWithRecovery
                    ? $"  ({"按回收率结算".Translate()})"
                    : string.Empty;
                text.AppendLine($"- {output.Count:0.##} × {GetItemName(output.ItemId)}{recovery}");
            }
        }
        return text.ToString();
    }

    private static string BuildRuntimeText(WorldviewRecipe recipe, bool discovered) {
        if (recipe == null) {
            return "配方目录为空".Translate().WithColor(Gray);
        }
        if (!discovered) {
            return "配方尚未发现".Translate().WithColor(Gray);
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

    private static string GetTowerName(ERecipe type) => GetItemName(type.GetSpriteItemId());

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
        catalogSignature = string.Empty;
        resetDetailScroll = true;
    }
}
