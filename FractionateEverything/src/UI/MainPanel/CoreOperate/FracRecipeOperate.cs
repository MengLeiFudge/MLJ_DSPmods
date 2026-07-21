using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Buildings;
using FE.Logic.Civilization.Configuration;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.FracRecipes.Runtime;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Fractionation.FracRecipes.ERecipeExtension;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// 查看分馏配方的协议状态、产物结构和当前塔型运行参数。
/// </summary>
public static class FracRecipeOperate {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static ItemProto selectedItem = LDB.items.Select(I铁矿);
    private static MyImageButton selectedItemButton;
    private static Text selectedItemNameText;
    private static MyComboBox recipeTypeCombo;
    private static Text recipeText;
    private static Text protocolText;
    private static ConfigEntry<int> recipeTypeEntry;

    private static ERecipe SelectedRecipeType => RecipeTypes[recipeTypeEntry.Value];

    private static BaseRecipe SelectedRecipe => selectedItem == null
        ? null
        : GetRecipe<BaseRecipe>(SelectedRecipeType, selectedItem.ID);

    public static void AddTranslations() {
        Register("分馏配方", "Fractionation Recipes");
        Register("分馏配方摘要", "View protocol status, recipe outputs, and current runtime parameters.",
            "查看协议状态、配方产物结构与当前运行参数。");
        Register("当前物品", "Current Item");
        Register("配方类型", "Recipe Type");
        Register("分馏配方提示按钮说明1",
            "Select any item that has at least one fractionation recipe. The recipe type automatically switches to a valid tower.",
            "选择至少拥有一种分馏配方的物品；当前塔型不适用时会自动切换到有效塔型。");
        Register("配方产物结构", "Recipe Outputs");
        Register("协议与运行状态", "Protocol and Runtime Status");
        Register("协议未发现", "Protocol not discovered");
        Register("协议解析中", "Protocol analysis in progress");
        Register("协议已恢复", "Protocol recovered");
        Register("不由文明协议控制", "Not controlled by civilization protocols");
        Register("尚未进入检索池", "Not yet eligible for retrieval");
        Register("已经进入检索池", "Eligible for retrieval");
        Register("当前不可运行", "Unavailable");
        Register("当前可运行", "Available");
        Register("协议未完整恢复，产物结构将在完整度达到100%后显示。",
            "The output structure becomes visible after protocol completeness reaches 100%.");
        Register("主产物", "Main Outputs");
        Register("副产物", "Byproducts");
        Register("无", "None");
        Register("输入", "Input");
        Register("基础成功率", "Base Success Rate");
        Register("损毁率", "Destruction Rate");
        Register("状态", "Status");
        Register("文明阶段", "Civilization Stage");
        Register("协议状态", "Protocol Status");
        Register("协议完整度", "Protocol Completeness");
        Register("检索资格", "Retrieval Eligibility");
        Register("运行塔型", "Runtime Tower");
        Register("处理堆叠上限", "Processing Stack Limit");
        Register("增产点倍率", "Proliferator Point Multiplier");
        Register("全局成功率增幅", "Global Success Rate Bonus");
        Register("流动输出堆叠", "Fluid Output Stacking");
        Register("产物输出堆叠", "Product Output Stacking");
        Register("已启用", "Enabled");
        Register("未解锁", "Locked");
    }

    public static void LoadConfig(ConfigFile configFile) {
        recipeTypeEntry = configFile.Bind("Recipe Operate", "Recipe Type", 0, "想要查看的配方类型。");
        if (recipeTypeEntry.Value < 0 || recipeTypeEntry.Value >= RecipeTypes.Length) {
            recipeTypeEntry.Value = 0;
        }
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        selectedItem ??= LDB.items.Select(I铁矿);
        EnsureValidRecipeType();
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(64f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("分馏配方", "分馏配方摘要", pos: (0, 0), objectName: "frac-recipe-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "frac-recipe-selector", strong: true,
                        cols: [Px(90f), Px(44f), Px(28f), Fr(1), Px(90f), Px(210f)],
                        columnGap: 8f,
                        children: [
                            TextNode("当前物品", 15, pos: (0, 0), objectName: "frac-recipe-current-item-label"),
                            ImageButtonNode(selectedItem, 40f,
                                onBuilt: button => selectedItemButton = button.WithClickEvent(OpenItemPicker, null),
                                pos: (0, 1), objectName: "frac-recipe-current-item"),
                            TipsButtonNode("提示", "分馏配方提示按钮说明1",
                                pos: (0, 2), objectName: "frac-recipe-tip"),
                            TextNode("", 13, onBuilt: text => selectedItemNameText = text,
                                pos: (0, 3), objectName: "frac-recipe-current-item-name"),
                            TextNode("配方类型", 15, pos: (0, 4), objectName: "frac-recipe-type-label"),
                            ComboBoxNode(onBuilt: combo => {
                                    recipeTypeCombo = combo.WithItems(RecipeTypeShortNames)
                                        .WithSize(200f, 0f)
                                        .WithConfigEntry(recipeTypeEntry)
                                        .WithOnSelChanged(_ => EnsureValidRecipeType());
                                },
                                pos: (0, 5), objectName: "frac-recipe-type-combo"),
                        ]),
                    Grid(pos: (2, 0), cols: [1, 1], columnGap: PageLayout.Gap, children: [
                        ContentCard(pos: (0, 0), objectName: "frac-recipe-output-card", strong: true,
                            rows: [Px(28f), 1], rowGap: PageLayout.InnerGap, children: [
                                CardTitleNode("配方产物结构", pos: (0, 0), objectName: "frac-recipe-output-title"),
                                TextNode("", 13, White, wrap: true, anchor: TextAnchor.UpperLeft,
                                    onBuilt: text => {
                                        recipeText = text;
                                        text.supportRichText = true;
                                    }, pos: (1, 0), objectName: "frac-recipe-output-text"),
                            ]),
                        ContentCard(pos: (0, 1), objectName: "frac-recipe-protocol-card",
                            rows: [Px(28f), 1], rowGap: PageLayout.InnerGap, children: [
                                CardTitleNode("协议与运行状态", pos: (0, 0), objectName: "frac-recipe-protocol-title"),
                                TextNode("", 13, White, wrap: true, anchor: TextAnchor.UpperLeft,
                                    onBuilt: text => {
                                        protocolText = text;
                                        text.supportRichText = true;
                                    }, pos: (1, 0), objectName: "frac-recipe-protocol-text"),
                            ]),
                    ]),
                ]));
        UpdateUI();
    }

    private static void OpenItemPicker() {
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2f;
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2f - PageLayout.HeaderHeight;
        UIItemPickerExtension.Popup(new Vector2(popupX, popupY), item => {
            if (item == null) {
                return;
            }
            selectedItem = item;
            EnsureValidRecipeType();
            UpdateUI();
        }, true, item => item != null && HasAnyRecipe(item.ID));
    }

    private static bool HasAnyRecipe(int itemId) {
        foreach (ERecipe recipeType in RecipeTypes) {
            if (GetRecipe<BaseRecipe>(recipeType, itemId) != null) {
                return true;
            }
        }
        return false;
    }

    private static void EnsureValidRecipeType() {
        if (recipeTypeEntry == null || selectedItem == null) {
            return;
        }

        int selectedIndex = Mathf.Clamp(recipeTypeEntry.Value, 0, RecipeTypes.Length - 1);
        if (GetRecipe<BaseRecipe>(RecipeTypes[selectedIndex], selectedItem.ID) != null) {
            if (recipeTypeEntry.Value != selectedIndex) {
                recipeTypeEntry.Value = selectedIndex;
            }
            return;
        }

        for (int i = 0; i < RecipeTypes.Length; i++) {
            if (GetRecipe<BaseRecipe>(RecipeTypes[i], selectedItem.ID) == null) {
                continue;
            }
            recipeTypeEntry.Value = i;
            recipeTypeCombo?.SetIndex(i);
            return;
        }
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        EnsureValidRecipeType();
        BaseRecipe recipe = SelectedRecipe;
        selectedItemButton.Proto = selectedItem;
        selectedItemNameText.text = selectedItem?.name ?? string.Empty;
        header.Title.text = "分馏配方".Translate().WithColor(Orange);
        header.Summary.text = "分馏配方摘要".Translate().WithColor(White);
        recipeText.text = BuildRecipeText(recipe);
        protocolText.text = BuildProtocolText(recipe);
    }

    private static string BuildRecipeText(BaseRecipe recipe) {
        if (recipe == null) {
            return string.Empty;
        }

        bool available = RecipeAvailabilityStore.IsAvailable(recipe);
        var text = new StringBuilder();
        text.AppendLine(recipe.TypeNameWC);
        text.AppendLine($"{"输入".Translate()}：1 × {GetItemName(recipe.InputID)}");
        text.AppendLine($"{"基础成功率".Translate()}：{recipe.SuccessRatio:P3}");
        text.AppendLine($"{"损毁率".Translate()}：{recipe.DestroyRatio:P3}");
        text.AppendLine($"{"状态".Translate()}：{(available ? "当前可运行" : "当前不可运行").Translate()}");
        text.AppendLine();
        if (!available) {
            text.Append("协议未完整恢复，产物结构将在完整度达到100%后显示。".Translate());
            return text.ToString();
        }

        AppendOutputs(text, "主产物".Translate(), recipe.OutputMain);
        text.AppendLine();
        AppendOutputs(text, "副产物".Translate(), recipe.OutputAppend);
        return text.ToString();
    }

    private static void AppendOutputs(StringBuilder text, string title, IReadOnlyList<OutputInfo> outputs) {
        text.AppendLine(title.WithColor(Orange));
        if (outputs.Count == 0) {
            text.AppendLine($"- {"无".Translate()}");
            return;
        }
        foreach (OutputInfo output in outputs) {
            text.AppendLine($"- {output}");
        }
    }

    private static string BuildProtocolText(BaseRecipe recipe) {
        if (recipe == null) {
            return string.Empty;
        }

        var text = new StringBuilder();
        RecipeKey recipeKey = RecipeKey.FromRecipe(recipe);
        ProtocolDefinition definition = ProtocolCatalog.Get(recipeKey);
        if (definition == null) {
            text.AppendLine($"{"协议状态".Translate()}：{"不由文明协议控制".Translate()}");
        } else {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(recipeKey);
            MatrixStageDefinition stage = ProgressionProfileRegistry.Current?.GetStage(definition.StageKey);
            string protocolState = progress.Completeness >= 100
                ? "协议已恢复"
                : progress.Discovered ? "协议解析中" : "协议未发现";
            text.AppendLine($"{"文明阶段".Translate()}：{stage?.DisplayNameKey.Translate() ?? definition.StageKey}");
            text.AppendLine($"{"协议状态".Translate()}：{protocolState.Translate()}");
            text.AppendLine($"{"协议完整度".Translate()}：{progress.Completeness}%");
            text.AppendLine($"{"检索资格".Translate()}：{(ProtocolEligibilityService.IsEligible(definition)
                ? "已经进入检索池" : "尚未进入检索池").Translate()}");
        }

        text.AppendLine();
        ItemProto building = LDB.items.Select(recipe.RecipeType.GetSpriteItemId());
        if (building != null) {
            text.AppendLine($"{"运行塔型".Translate()}：{building.name}");
            text.AppendLine($"{"处理堆叠上限".Translate()}：{building.MaxStack()}");
            text.AppendLine($"{"增产点倍率".Translate()}：{building.PlrRatio():P0}");
        }
        text.AppendLine();
        text.AppendLine($"{"流动输出堆叠".Translate()}：{GetSwitchText(
            TowerRuntimeModifierCache.IsFluidOutputStackingEnabled(recipe.RecipeType))}");
        text.AppendLine($"{"产物输出堆叠".Translate()}：{GetSwitchText(
            TowerRuntimeModifierCache.IsProductOutputStackingEnabled(recipe.RecipeType))}");
        text.AppendLine($"{"分馏永动".Translate()}：{GetSwitchText(
            TowerRuntimeModifierCache.IsFractionationForeverEnabled(recipe.RecipeType))}");
        return text.ToString();
    }

    private static string GetItemName(int itemId) => LDB.items.Select(itemId)?.name ?? itemId.ToString();

    private static string GetSwitchText(bool enabled) => (enabled ? "已启用" : "未解锁").Translate();

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
