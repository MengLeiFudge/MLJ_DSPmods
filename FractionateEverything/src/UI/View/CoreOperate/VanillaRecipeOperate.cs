using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View.Setting;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ItemManager;
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

    private static MyImageButton btnFragmentIcon;
    private static Text txtFragmentCount;
    private static MyImageButton btnMatrixIcon;
    private static Text txtMatrixCount;
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
        }, true, recipe => recipe != null && (showLocked || GameMain.history.RecipeUnlocked(recipe.ID)));
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
        Register("科技层次不足", "Next-tier tech completion required", "需完成下一层科技");
        Register("已达上限", "Max upgrade reached", "已达上限");
        Register("制作时间", "Crafting Time");
        Register("当前时间", "Current Time");
        Register("原版增强资源", "Enhance Resource", "增强资源");
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
                        onBuilt: refs => refs.Summary.text = "查看原版配方的原料、耗时与升级进度".WithColor(White)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "vanilla-recipe-content-card",
                        strong: true,
                        children: [
                            Node(pos: (0, 0), objectName: "vanilla-recipe-content-root", build: (w, root) => {
                                tab = root;
                                float x = 0f;
                                float y = 18f + 7f;
                                txtCurrRecipe = w.AddText2(x, y, tab, "当前配方", 15, "textCurrItem");
                                float popupY = y + (36f + 7f) / 2;
                                btnSelectedRecipe = w.AddImageButton(x + txtCurrRecipe.preferredWidth + 5, y, tab,
                                    SelectedRecipe, "button-change-item").WithClickEvent(
                                    () => { OnButtonChangeRecipeClick(false, popupY); },
                                    () => { OnButtonChangeRecipeClick(true, popupY); });
                                w.AddTipsButton2(x + txtCurrRecipe.preferredWidth + 5 + btnSelectedRecipe.Width + 5, y, tab,
                                    "提示", "原版配方提示按钮说明1");
                                btnFragmentIcon = w.AddImageButton(GetPosition(3, 4).Item1, y, tab, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
                                txtFragmentCount = w.AddText2(GetPosition(3, 4).Item1 + 45f, y, tab, "");
                                btnMatrixIcon = w.AddImageButton(GetPosition(3, 4).Item1 + 120f, y, tab, null).WithSize(40f, 40f);
                                txtMatrixCount = w.AddText2(GetPosition(3, 4).Item1 + 165f, y, tab, "");
                                y += 36f + 7f;
                                w.AddText2(x, y, tab, "输入物品", 16, "labelInputItems");
                                y += 36f;
                                for (int i = 0; i < MaxInputCount; i++) {
                                    int idx = i;
                                    inputImages[i] = w.AddImageButton(x, y, tab, null, $"inputImage{i}");
                                    txtInputNames[i] = w.AddText2(x + 40 + 5, y, tab, "", 14, $"txtInputName{i}");
                                    txtInputCounts[i] = w.AddText2(x + 180, y, tab, "", 14, $"txtInputCount{i}");
                                    txtInputUpgrades[i] = w.AddText2(x + 280, y, tab, "", 14, $"txtInputUpgrade{i}");
                                    btnInputUpgrades[i] = w.AddButton(x + 380, y, 80, tab, "升级", 14, $"btnInputUpgrade{i}",
                                        () => { UpgradeInput(idx); });
                                    y += 36f + 5f;
                                }
                                y += 7f;
                                txtTimeLabel = w.AddText2(x, y, tab, "制作时间", 16, "labelTime");
                                y += 36f;
                                txtTimeValue = w.AddText2(x + 40, y, tab, "", 14, "txtTimeValue");
                                txtTimeUpgrade = w.AddText2(x + 280, y, tab, "", 14, "txtTimeUpgrade");
                                btnTimeUpgrade = w.AddButton(x + 380, y, 80, tab, "升级", 14, "btnTimeUpgrade",
                                    () => { UpgradeTimeSpend(); });
                            }),
                        ]),
                ]));
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedRecipe.Proto = SelectedRecipe;
        int currentMatrixId = GetCurrentProgressMatrixId();
        btnMatrixIcon.Proto = LDB.items.Select(currentMatrixId);
        btnFragmentIcon.SetCount(GetItemTotalCount(IFE残片));
        btnMatrixIcon.SetCount(GetItemTotalCount(currentMatrixId));
        txtFragmentCount.text = "";
        txtMatrixCount.text = "";

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
                        btnInputUpgrades[i].button.GetComponentInChildren<Text>().text = GetMatrixRequirementText(vanillaRecipe);
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
                btnTimeUpgrade.button.GetComponentInChildren<Text>().text = GetMatrixRequirementText(vanillaRecipe);
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
            (int matrixId, int matrixCount, int fragmentCount) = GetInputUpgradeCost(vanillaRecipe, itemID);
            string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
            Miscellaneous.ShowQuestion("提示".Translate(),
                $"{"要花费".Translate()} {matrixName} x {matrixCount} + 残片 x {fragmentCount} "
                + $"{"来修改此项".Translate()}{"吗？".Translate()}",
                () => {
                    if (!TakeItemWithTip(matrixId, matrixCount, out _)
                        || !TakeItemWithTip(IFE残片, fragmentCount, out _)) {
                        return;
                    }
                    vanillaRecipe.UpgradeInput(items[itemIdx]);
                });
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
            (int matrixId, int matrixCount, int fragmentCount) = GetTimeUpgradeCost(vanillaRecipe);
            string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
            Miscellaneous.ShowQuestion("提示".Translate(),
                $"{"要花费".Translate()} {matrixName} x {matrixCount} + 残片 x {fragmentCount} "
                + $"{"来修改此项".Translate()}{"吗？".Translate()}",
                () => {
                    if (!TakeItemWithTip(matrixId, matrixCount, out _)
                        || !TakeItemWithTip(IFE残片, fragmentCount, out _)) {
                        return;
                    }
                    vanillaRecipe.UpgradeTime();
                });
        }
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

    private static (int matrixId, int matrixCount, int fragmentCount) GetInputUpgradeCost(VanillaRecipe recipe, int itemId) {
        int currentUpgrade = recipe.GetInputUpgradeCount(itemId);
        return (GetCurrentProgressMatrixId(), 1 + currentUpgrade / 2, 20 + currentUpgrade * 10);
    }

    private static (int matrixId, int matrixCount, int fragmentCount) GetTimeUpgradeCost(VanillaRecipe recipe) {
        int currentUpgrade = recipe.GetTimeUpgradeCount();
        return (GetCurrentProgressMatrixId(), 1 + currentUpgrade / 2, 30 + currentUpgrade * 15);
    }

    private static string GetMatrixRequirementText(VanillaRecipe recipe) {
        int stageIndex = GetMatrixStageIndex(recipe.MatrixId);
        int requiredIndex = Mathf.Min(stageIndex + 1, MainProgressMatrixIds.Length - 1);
        int requiredMatrixId = MainProgressMatrixIds[requiredIndex];
        string nextMatrixName = LDB.items.Select(requiredMatrixId)?.name ?? requiredMatrixId.ToString();
        return $"{nextMatrixName} 全科技";
    }
}
