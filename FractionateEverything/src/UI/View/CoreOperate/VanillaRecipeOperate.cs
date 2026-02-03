using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.CoreOperate;

public static class VanillaRecipeOperate {
    // UI元素：输入物品
    private const int MaxInputCount = 6;
    private static RectTransform window;
    private static RectTransform tab;
    private static Text txtCurrRecipe;
    private static MyImageButton btnSelectedRecipe;

    private static Text txtCoreCount;
    private static MyImageButton[] inputImages = new MyImageButton[MaxInputCount];
    private static Text[] txtInputNames = new Text[MaxInputCount];
    private static Text[] txtInputCounts = new Text[MaxInputCount];
    private static Text[] txtInputUpgrades = new Text[MaxInputCount];
    private static UIButton[] btnInputUpgrades = new UIButton[MaxInputCount];

    // UI元素：时间
    private static Text txtTimeLabel;
    private static Text txtTimeValue;
    private static Text txtTimeUpgrade;
    private static UIButton btnTimeUpgrade;

    private static RecipeProto SelectedRecipe { get; set; } = LDB.recipes.Select(R铁块);

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
        }, true, recipe => recipe != null && GameMain.history.RecipeUnlocked(recipe.ID));
    }

    public static void AddTranslations() {
        Register("原版配方", "Vanilla Recipe");

        Register("当前配方", "Current recipe");
        Register("原版配方提示按钮说明1",
            "Left-click to switch between unlocked recipes, right-click to switch between all available recipes.",
            "左键在已解锁配方之间切换，右键在全部可用配方中切换。");
        Register("输入物品", "Input Items");
        Register("当前数量", "Current Count");
        Register("升级次数", "Upgrade Times");
        Register("升级", "Upgrade");
        Register("科技层次不足", "Higher matrix tier required", "科技层次不足");
        Register("已达上限", "Max upgrade reached", "已达上限");
        Register("制作时间", "Crafting Time");
        Register("当前时间", "Current Time");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "原版配方");
        float x = 0f;
        float y = 18f + 7f;

        // 第一行：当前配方选择
        txtCurrRecipe = wnd.AddText2(x, y, tab, "当前配方", 15, "textCurrItem");
        float popupY = y + (36f + 7f) / 2;
        btnSelectedRecipe = wnd.AddImageButton(x + txtCurrRecipe.preferredWidth + 5, y, tab,
            SelectedRecipe, "button-change-item").WithClickEvent(
            () => { OnButtonChangeRecipeClick(false, popupY); },
            () => { OnButtonChangeRecipeClick(true, popupY); });
        wnd.AddTipsButton2(x + txtCurrRecipe.preferredWidth + 5 + btnSelectedRecipe.Width + 5, y, tab,
            "提示", "原版配方提示按钮说明1");
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE原版配方核心));
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");

        y += 36f + 7f;

        // 输入物品标题
        wnd.AddText2(x, y, tab, "输入物品", 16, "labelInputItems");
        y += 36f;

        // 为每个可能的输入物品创建UI元素
        for (int i = 0; i < MaxInputCount; i++) {
            int idx = i;
            // 物品图标
            inputImages[i] = wnd.AddImageButton(x, y, tab, null, $"inputImage{i}");
            // 物品名称和当前数量
            txtInputNames[i] = wnd.AddText2(x + 40 + 5, y, tab, "", 14, $"txtInputName{i}");
            txtInputCounts[i] = wnd.AddText2(x + 180, y, tab, "", 14, $"txtInputCount{i}");
            // 升级次数
            txtInputUpgrades[i] = wnd.AddText2(x + 280, y, tab, "", 14, $"txtInputUpgrade{i}");
            // 升级按钮
            btnInputUpgrades[i] = wnd.AddButton(x + 380, y, 80, tab, "升级", 14, $"btnInputUpgrade{i}",
                () => { UpgradeInput(idx); });
            y += 36f + 5f;
        }

        y += 7f;

        // 制作时间部分
        txtTimeLabel = wnd.AddText2(x, y, tab, "制作时间", 16, "labelTime");
        y += 36f;

        // 时间值
        txtTimeValue = wnd.AddText2(x + 40, y, tab, "", 14, "txtTimeValue");
        // 升级次数
        txtTimeUpgrade = wnd.AddText2(x + 280, y, tab, "", 14, "txtTimeUpgrade");
        // 升级按钮
        btnTimeUpgrade = wnd.AddButton(x + 380, y, 80, tab, "升级", 14, "btnTimeUpgrade",
            () => { UpgradeTimeSpend(); });
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedRecipe.Proto = SelectedRecipe;
        txtCoreCount.text = $"x {GetItemTotalCount(IFE原版配方核心)}";

        if (SelectedRecipe == null) {
            return;
        }

        VanillaRecipe vanillaRecipe = GetVanillaRecipe(SelectedRecipe.ID);
        if (vanillaRecipe == null) {
            return;
        }

        // 更新输入物品信息
        int[] items = vanillaRecipe.recipe.Items;
        int[] itemCounts = vanillaRecipe.recipe.ItemCounts;

        bool limitedByMatrix = vanillaRecipe.LimitedByMatrix;

        for (int i = 0; i < MaxInputCount; i++) {
            if (i < items.Length) {
                // 显示该输入物品
                int itemID = items[i];
                ItemProto item = LDB.items.Select(itemID);
                int[] info = vanillaRecipe.GetIdxCurrAndNextCount(itemID);
                int currCount = info[1];
                int nextCount = info[2];
                bool canUpgrade = vanillaRecipe.CanUpgradeInput(itemID);

                // 获取升级次数
                int upgradeCount = vanillaRecipe.GetInputUpgradeCount(itemID);

                inputImages[i].Proto = item;
                inputImages[i].gameObject.SetActive(true);
                txtInputNames[i].text = item.name;
                txtInputNames[i].gameObject.SetActive(true);
                txtInputCounts[i].text = $"{"当前数量".Translate()}: {currCount} → {nextCount}";
                txtInputCounts[i].gameObject.SetActive(true);
                txtInputUpgrades[i].text = $"{"升级次数".Translate()}: {upgradeCount}";
                txtInputUpgrades[i].gameObject.SetActive(true);
                btnInputUpgrades[i].gameObject.SetActive(true);
                btnInputUpgrades[i].button.interactable = canUpgrade;

                // 如果已达上限，修改按钮文本
                if (!canUpgrade) {
                    if (limitedByMatrix) {
                        btnInputUpgrades[i].button.GetComponentInChildren<Text>().text = "科技层次不足".Translate();
                    } else {
                        btnInputUpgrades[i].button.GetComponentInChildren<Text>().text = "已达上限".Translate();
                    }
                } else {
                    btnInputUpgrades[i].button.GetComponentInChildren<Text>().text = "升级".Translate();
                }
            } else {
                // 隐藏未使用的UI元素
                inputImages[i].gameObject.SetActive(false);
                txtInputNames[i].gameObject.SetActive(false);
                txtInputCounts[i].gameObject.SetActive(false);
                txtInputUpgrades[i].gameObject.SetActive(false);
                btnInputUpgrades[i].gameObject.SetActive(false);
            }
        }

        // 更新时间信息
        int[] timeInfo = vanillaRecipe.GetCurrAndNextTimeSpend();
        int currTime = timeInfo[0];
        int nextTime = timeInfo[1];
        bool canUpgradeTime = vanillaRecipe.CanUpgradeTime();

        txtTimeValue.text = $"{"当前时间".Translate()}: {currTime / 60.0f:F2}s → {nextTime / 60.0f:F2}s";
        txtTimeUpgrade.text = $"{"升级次数".Translate()}: {vanillaRecipe.GetTimeUpgradeCount()}";
        btnTimeUpgrade.button.interactable = canUpgradeTime;

        if (!canUpgradeTime) {
            if (limitedByMatrix) {
                btnTimeUpgrade.button.GetComponentInChildren<Text>().text = "科技层次不足".Translate();
            } else {
                btnTimeUpgrade.button.GetComponentInChildren<Text>().text = "已达上限".Translate();
            }
        } else {
            btnTimeUpgrade.button.GetComponentInChildren<Text>().text = "升级".Translate();
        }
    }

    public static void UpgradeInput(int itemIdx) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        VanillaRecipe vanillaRecipe = GetVanillaRecipe(SelectedRecipe.ID);
        int[] items = vanillaRecipe.recipe.Items;
        if (itemIdx >= items.Length) {
            return;
        }
        int itemID = items[itemIdx];
        ItemProto item = LDB.items.Select(itemID);
        if (!vanillaRecipe.CanUpgradeInput(itemID)) {
            UIMessageBox.Show("提示".Translate(),
                $"此配方的原料{item.name}已经无法升级！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (GameMain.sandboxToolsEnabled) {
            vanillaRecipe.UpgradeInput(items[itemIdx]);
        } else {
            int takeId = IFE原版配方核心;
            int takeCount = 1;
            ItemProto takeProto = LDB.items.Select(takeId);
            UIMessageBox.Show("提示".Translate(),
                $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
                + $"{"来修改此项".Translate()}{"吗？".Translate()}",
                "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                () => {
                    if (!TakeItemWithTip(takeId, takeCount, out _)) {
                        return;
                    }
                    vanillaRecipe.UpgradeInput(items[itemIdx]);
                },
                null);
        }
    }

    public static void UpgradeTimeSpend() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        VanillaRecipe vanillaRecipe = GetVanillaRecipe(SelectedRecipe.ID);
        if (!vanillaRecipe.CanUpgradeTime()) {
            UIMessageBox.Show("提示".Translate(),
                "此配方的时间已经无法升级！".Translate(),
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (GameMain.sandboxToolsEnabled) {
            vanillaRecipe.UpgradeTime();
        } else {
            int takeId = IFE原版配方核心;
            int takeCount = 1;
            ItemProto takeProto = LDB.items.Select(takeId);
            UIMessageBox.Show("提示".Translate(),
                $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
                + $"{"来修改此项".Translate()}{"吗？".Translate()}",
                "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                () => {
                    if (!TakeItemWithTip(takeId, takeCount, out _)) {
                        return;
                    }
                    vanillaRecipe.UpgradeTime();
                },
                null);
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() { }

    #endregion
}
