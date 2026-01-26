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
    private static RectTransform window;
    private static RectTransform tab;

    private static RecipeProto SelectedRecipe { get; set; } = LDB.recipes.Select(R铁块);
    private static Text txtCurrRecipe;
    private static MyImageButton btnSelectedRecipe;

    private static void OnButtonChangeRecipeClick(bool showLocked, float y) {
        //配方选取窗口左上角的X值（anchoredPosition是中心点）
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        //配方选取窗口左上角的Y值（anchoredPosition是中心点）
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIRecipePickerExtension.Popup(new(popupX, popupY), recipe => {
            if (recipe == null) return;
            SelectedRecipe = recipe;
        }, true, recipe => recipe != null && GameMain.history.RecipeUnlocked(recipe.ID));
    }

    private static Text txtCoreCount;
    private static UIButton btnGetRecipe;
    private static Text[] txtRecipeInfo = new Text[30];
    private static float txtRecipeInfoBaseY = 0;

    public static void AddTranslations() {
        Register("原版配方", "Vanilla Recipe");

        Register("当前配方", "Current recipe");
        Register("配方操作提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "原版配方");
        float x = 0f;
        float y = 18f + 7f;
        txtCurrRecipe = wnd.AddText2(x, y, tab, "当前配方", 15, "textCurrItem");
        float popupY = y + (36f + 7f) / 2;
        btnSelectedRecipe = wnd.AddImageButton(x + txtCurrRecipe.preferredWidth + 5, y, tab,
            SelectedRecipe, "button-change-item").WithClickEvent(
            () => { OnButtonChangeRecipeClick(false, popupY); },
            () => { OnButtonChangeRecipeClick(true, popupY); });
        wnd.AddTipsButton2(x + txtCurrRecipe.preferredWidth + 5 + btnSelectedRecipe.Width + 5, y, tab,
            "提示", "配方操作提示按钮说明1");
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE原版配方核心));
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddButton(0, 4, y, tab, "升级第一项",
            onClick: () => { UpgradeInput(0); });
        wnd.AddButton(1, 4, y, tab, "升级第二项",
            onClick: () => { UpgradeInput(1); });
        wnd.AddButton(2, 4, y, tab, "升级第三项",
            onClick: () => { UpgradeInput(2); });
        wnd.AddButton(3, 4, y, tab, "升级时间",
            onClick: () => { UpgradeTimeSpend(); });

        y += 36f;
        txtRecipeInfoBaseY = y;
        for (int i = 0; i < txtRecipeInfo.Length; i++) {
            txtRecipeInfo[i] = wnd.AddText2(x, y, tab, "动态刷新");
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedRecipe.Proto = SelectedRecipe;
        txtCoreCount.text = $"x {GetItemTotalCount(IFE原版配方核心)}";

        int line = 0;
        //写一些东西
        for (; line < txtRecipeInfo.Length; line++) {
            txtRecipeInfo[line].text = "";
            txtRecipeInfo[line].SetPosition(0, 0);
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
