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

        Register("当前配方", "Current item");
        Register("配方操作提示按钮说明1",
            "Left-click to switch between unlocked recipes in the current recipe category, right-click to switch between all available recipes in the current recipe category.",
            "左键在当前配方类别已解锁配方之间切换，右键在当前配方类别全部可用配方中切换。");
        Register("配方类型", "Recipe type");

        Register("解锁配方", "Unlock recipe");
        Register("兑换回响", "Exchange echo");
        Register("无法解锁", "Can not unlock");
        Register("升至下一级", "Upgrade to next level");
        Register("升至最高级", "Upgrade to max level");
        Register("突破品质", "Breakthrough quality");

        Register("回响", "Echo");

        Register("配方不存在！", "Recipe does not exist!");
        Register("分馏配方未解锁", "Recipe locked", "配方未解锁");
        Register("费用", "Cost");
        Register("每种精华", "Each essence");
        Register("成功率", "Success Rate");
        Register("损毁率", "Destroy Rate");
        Register("产出", "Output");
        //Register("增产点数", "Proliferator Points");//原版已翻译
        //Register("其他", "Others");//原版已翻译

        Register("完全处理后的输出如下：", "The fully processed output is as follows:");
        Register("配方已完全升级！", "Recipe has been completely upgraded!");
        Register("配方已到最高品质！", "Recipe has reached the highest quality!");
        Register("配方品质可突破，突破条件：",
            "Recipe quality can be broken through. Conditions for breaking through:");
        Register("达到当前品质最高等级（", "Reaching the highest current quality level (");
        Register("）", ")");
        Register("达到当前等级经验上限（", "Reach the current level experience cap (");
        Register("拥有足够的同名回响（", "Have sufficient echoes of the same name (");

        Register("当前配方尚未解锁，或科技层次不足！",
            "The current item has not been unlocked, or the technology level is insufficient!");
        Register("配方回响数目已达到上限！", "The number of recipe echoes has reached the limit!");
        Register("配方回响数目已达到突破要求，暂时无法兑换！",
            "The number of recipe echoes has reached the breakthrough requirement and cannot be exchanged for the time being!");
        Register("配方经验已达上限！", "Recipe experience has reached the limit!");
        Register("配方已升至当前品质最高等级！", "Recipe has been upgraded to the highest quality level currently available!");
        Register("配方回响数目不足！", "Insufficient number of recipe echoes!");
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
