using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Progression;
using FE.Logic.VanillaRecipes;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>
/// 原版配方强化和倍率调整页面。
/// </summary>
public static class VanillaRecipeOperate {
    // UI元素：输入物品
    private const int MaxInputCount = 6;
    private static RectTransform window;
    private static RectTransform tab;
    private static Text txtCurrRecipe;
    private static MyImageButton btnSelectedRecipe;

    private static MyImageButton btnFragmentIcon;
    private static Text txtFragmentCount;
    private static MyImageButton[] inputImages = new MyImageButton[MaxInputCount];
    private static Text[] txtInputNames = new Text[MaxInputCount];
    private static Text[] txtInputCounts = new Text[MaxInputCount];
    private static Text[] txtInputReadonly = new Text[MaxInputCount];

    // UI元素：时间
    private static Text txtTimeLabel;
    private static Text txtTimeValue;
    private static Text txtTimeUpgrade;
    private static Text txtTimeLimit;
    private static UIButton btnTimeLimitUpgrade;
    private static UIButton btnTimeUpgrade;
    private static UIButton btnTimeUpgradeToLimit;

    private static RecipeProto SelectedRecipe { get; set; } = LDB.recipes.Select(R铁块);

    private static bool IsSupportedRecipe(RecipeProto recipe) {
        return recipe != null && VanillaRecipeManager.GetVanillaRecipe(recipe.ID) != null;
    }

    private static void OnButtonChangeRecipeClick(bool showLocked, float y) {
        //配方选取窗口左上角的X值（anchoredPosition是中心点）
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        //配方选取窗口左上角的Y值（anchoredPosition是中心点）
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIRecipePickerExtension.Popup(new(popupX, popupY), recipe => {
            if (recipe == null) {
                return;
            }
            SelectedRecipe = recipe;
        }, true, recipe => IsSupportedRecipe(recipe) && (showLocked || GameMain.history.RecipeUnlocked(recipe.ID)));
    }

    public static void AddTranslations() {
        Register("原版配方", "Vanilla Recipe");

        Register("当前配方", "Current recipe");
        Register("原版配方提示按钮说明1",
            "Left-click to switch between unlocked recipes, right-click to switch between all available recipes.",
            "左键在已解锁配方之间切换，右键在全部可用配方中切换。");
        Register("输入物品", "Input Items");
        Register("当前数量", "Current Count");
        Register("升级次数", "Sync tier");
        Register("升级", "Auto");
        Register("升满", "Auto");
        Register("提升上限", "Auto sync", "自动同步");
        Register("仅缩短制作时间", "Only crafting time is enhanced");
        Register("全局时间上限", "Recipe time sync");
        Register("需要集装物流系统", "Logistics stacking system required", "需要集装物流系统");
        Register("需要更高堆叠上限", "Higher stack limit required", "随堆叠提升");
        Register("全局时间上限不足", "Synced by stack", "随堆叠同步");
        Register("科技层次不足", "Next-tier tech completion required", "需完成下一层科技");
        Register("已达上限", "Synced", "已同步");
        Register("自动同步", "Auto sync", "自动同步");
        Register("随堆叠同步配方时间", "Recipe time is synced by stack", "配方时间由当前堆叠上限自动同步。");
        Register("制作时间", "Crafting Time");
        Register("当前时间", "Current Time");
        Register("原版增强资源", "Enhance Resource", "增强资源");
        Register("此配方的原料{0}已经无法升级！", "This recipe's input {0} can no longer be upgraded!");
        Register("此配方的时间已经无法升级！", "This recipe's crafting time can no longer be upgraded!");
        Register("来修改此项", "to modify this entry");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("原版配方", objectName: "vanilla-recipe-header", pos: (0, 0),
                        onBuilt: refs => refs.Summary.text = "查看原版配方的原料、耗时与堆叠同步状态".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "vanilla-recipe-content-card",
                        strong: true,
                        rows: BuildContentRows(),
                        cols: [Px(50f), Fr(2), Fr(1), Fr(2), Fr(2), Fr(3)],
                        rowGap: 6f,
                        columnGap: 8f,
                        onBuilt: root => tab = root,
                        children: [
                            Grid(pos: (0, 0), span: (1, 6),
                                cols: [Fr(1), Px(44f), Px(28f), Fr(2), Px(44f), Fr(2)],
                                columnGap: 8f, children: [
                                    TextNode("当前配方", 15, onBuilt: text => txtCurrRecipe = text,
                                        pos: (0, 0), objectName: "textCurrItem"),
                                    ImageButtonNode(SelectedRecipe, 40f,
                                        onBuilt: btn => btnSelectedRecipe = btn.WithClickEvent(
                                            () => { OnButtonChangeRecipeClick(false, 46f); },
                                            () => { OnButtonChangeRecipeClick(true, 46f); }),
                                        pos: (0, 1), objectName: "button-change-item"),
                                    TipsButtonNode("提示", "原版配方提示按钮说明1",
                                        pos: (0, 2), objectName: "vanilla-recipe-tip"),
                                    ImageButtonNode(LDB.items.Select(IFE残片), 40f, onBuilt: btn => btnFragmentIcon = btn,
                                        pos: (0, 4), objectName: "vanilla-recipe-fragment"),
                                    TextNode("", 13, onBuilt: text => txtFragmentCount = text,
                                        pos: (0, 5), objectName: "vanilla-recipe-fragment-count"),
                                ]),
                            TextNode("输入物品", 15, pos: (1, 0), span: (1, 6), objectName: "labelInputItems"),
                            ..BuildInputNodes(),
                            TextNode("制作时间", 15, onBuilt: text => txtTimeLabel = text,
                                pos: (MaxInputCount + 2, 0), span: (1, 6), objectName: "labelTime"),
                            TextNode("", 13, onBuilt: text => txtTimeValue = text,
                                pos: (MaxInputCount + 3, 1), objectName: "txtTimeValue"),
                            TextNode("", 13, onBuilt: text => txtTimeUpgrade = text,
                                pos: (MaxInputCount + 3, 3), objectName: "txtTimeUpgrade"),
                            TextNode("", 13, onBuilt: text => txtTimeLimit = text,
                                pos: (MaxInputCount + 3, 4), objectName: "txtTimeLimit"),
                            ButtonNode("自动同步", fontSize: 13, onClick: ShowAutoSyncTip,
                                onBuilt: btn => btnTimeLimitUpgrade = btn,
                                pos: (MaxInputCount + 3, 5), objectName: "btnTimeLimitUpgrade"),
                            ButtonNode("自动同步", fontSize: 13, onClick: ShowAutoSyncTip,
                                onBuilt: btn => btnTimeUpgrade = btn,
                                pos: (MaxInputCount + 4, 4), objectName: "btnTimeUpgrade"),
                            ButtonNode("自动同步", fontSize: 13, onClick: ShowAutoSyncTip,
                                onBuilt: btn => btnTimeUpgradeToLimit = btn,
                                pos: (MaxInputCount + 4, 5), objectName: "btnTimeUpgradeToLimit"),
                        ]),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildContentRows() {
        var rows = new List<LayoutTrack> { Px(44f), Px(28f) };
        for (int i = 0; i < MaxInputCount; i++) {
            rows.Add(1);
        }
        rows.Add(Px(28f));
        rows.Add(1);
        rows.Add(1);
        rows.Add(1);
        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildInputNodes() {
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < MaxInputCount; i++) {
            int index = i;
            int row = i + 2;
            nodes.Add(ImageButtonNode(size: 40f, onBuilt: btn => inputImages[index] = btn,
                pos: (row, 0), objectName: $"inputImage{index}"));
            nodes.Add(TextNode("", 13, onBuilt: text => txtInputNames[index] = text,
                pos: (row, 1), objectName: $"txtInputName{index}"));
            nodes.Add(TextNode("", 13, onBuilt: text => txtInputCounts[index] = text,
                pos: (row, 2), objectName: $"txtInputCount{index}"));
            nodes.Add(TextNode("仅缩短制作时间", 13, onBuilt: text => txtInputReadonly[index] = text,
                pos: (row, 3), span: (1, 3), objectName: $"txtInputReadonly{index}"));
        }

        return nodes;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedRecipe.Proto = SelectedRecipe;
        btnFragmentIcon.SetCount(GetItemTotalCount(IFE残片));
        txtFragmentCount.text = "";

        if (SelectedRecipe == null) {
            return;
        }

        VanillaRecipe vanillaRecipe = VanillaRecipeManager.GetVanillaRecipe(SelectedRecipe.ID);
        if (vanillaRecipe == null) {
            return;
        }

        // 更新输入物品信息
        int[] items = vanillaRecipe.recipe.Items;
        int[] itemCounts = vanillaRecipe.recipe.ItemCounts;

        for (int i = 0; i < MaxInputCount; i++) {
            if (i < items.Length) {
                // 显示该输入物品
                int itemID = items[i];
                ItemProto item = LDB.items.Select(itemID);
                int currCount = itemCounts[i];

                inputImages[i].Proto = item;
                inputImages[i].gameObject.SetActive(true);
                txtInputNames[i].text = item.name;
                txtInputNames[i].gameObject.SetActive(true);
                txtInputCounts[i].text = $"{"当前数量".Translate()}: {currCount}";
                txtInputCounts[i].gameObject.SetActive(true);
                txtInputReadonly[i].gameObject.SetActive(true);
            } else {
                // 隐藏未使用的UI元素
                inputImages[i].gameObject.SetActive(false);
                txtInputNames[i].gameObject.SetActive(false);
                txtInputCounts[i].gameObject.SetActive(false);
                txtInputReadonly[i].gameObject.SetActive(false);
            }
        }

        // 更新时间信息
        int[] timeInfo = vanillaRecipe.GetCurrAndNextTimeSpend();
        int currTime = timeInfo[0];
        int nextTime = timeInfo[1];
        txtTimeValue.text = currTime == nextTime
            ? $"{"当前时间".Translate()}: {currTime / 60.0f:F2}s"
            : $"{"当前时间".Translate()}: {currTime / 60.0f:F2}s → {nextTime / 60.0f:F2}s";
        txtTimeUpgrade.text = $"{"升级次数".Translate()}: stack {StackingManager.CurrentMaxStack}";
        txtTimeLimit.text = GetTimeLimitText();
        SetAutoSyncButton(btnTimeLimitUpgrade);
        SetAutoSyncButton(btnTimeUpgrade);
        SetAutoSyncButton(btnTimeUpgradeToLimit);
    }

    private static void SetAutoSyncButton(UIButton button) {
        button.button.interactable = false;
        button.button.GetComponentInChildren<Text>().text = "自动同步".Translate();
    }

    public static void ShowAutoSyncTip() {
        UIMessageBox.Show("提示".Translate(),
            "随堆叠同步配方时间".Translate(),
            "确定".Translate(), UIMessageBox.WARNING,
            null);
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

    private static string GetTimeLimitText() {
        if (GameMain.history == null) {
            return "";
        }

        if (!StackingManager.IsUnlocked && !GameMain.history.TechUnlocked(T集装物流系统)) {
            return "需要集装物流系统".Translate();
        }

        int level = VanillaRecipeManager.GlobalTimeLimitLevel;
        double ratio = VanillaRecipeManager.GlobalTimeLimitRatio;
        return $"{ "全局时间上限".Translate()}: {level}/{VanillaRecipeManager.MaxTimeLimitLevel}"
               + $" ({ratio:P0}, stack {StackingManager.CurrentMaxStack})";
    }
}
