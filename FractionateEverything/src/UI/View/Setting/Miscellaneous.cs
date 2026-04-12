using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Compatibility;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class Miscellaneous {
    private static RectTransform window;
    private static RectTransform tab;

    private static int[] ClickTakeCounts = [1, 3, 5, 10, 30, 50, 100];
    private static string[] ClickTakeCountsStr = ClickTakeCounts.Select(count => count.ToString()).ToArray();
    private static ConfigEntry<int> LeftClickTakeCountEntry;
    private static ConfigEntry<int> RightClickTakeCountEntry;
    private static readonly string[] ExtractTargetStrs = ["提取到手上", "提取到背包"];
    private static ConfigEntry<int> ExtractTargetEntry;

    private static readonly string[] TakeItemPriorityStrs = [
        $"{"背包".Translate()} -> {"物流背包".Translate()} -> {"分馏数据中心".Translate()}",
        $"{"背包".Translate()} -> {"分馏数据中心".Translate()} -> {"物流背包".Translate()}",
        $"{"物流背包".Translate()} -> {"背包".Translate()} -> {"分馏数据中心".Translate()}",
        $"{"物流背包".Translate()} -> {"分馏数据中心".Translate()} -> {"背包".Translate()}",
        $"{"分馏数据中心".Translate()} -> {"背包".Translate()} -> {"物流背包".Translate()}",
        $"{"分馏数据中心".Translate()} -> {"物流背包".Translate()} -> {"背包".Translate()}",
    ];
    private static readonly int[][] TakeItemPriorityArr = [
        [0, 1, 2],
        [0, 2, 1],
        [1, 0, 2],
        [1, 2, 0],
        [2, 0, 1],
        [2, 1, 0],
    ];
    private static ConfigEntry<int> TakeItemPriorityEntry;

    private static MySlider DownloadThresholdSlider;
    private static UIButton DownloadThresholdTipsButton2;
    private static ConfigEntry<float> DownloadThresholdEntry;

    private static MySlider UploadThresholdSlider;
    private static UIButton UploadThresholdTipsButton2;
    private static ConfigEntry<float> UploadThresholdEntry;

    private static ConfigEntry<bool> ShowFractionateRecipeDetailsEntry;
    private static ConfigEntry<bool> EnableConfirmationDialogEntry;

    private static MyCheckBox PackageSortTwiceCheckBox;
    private static MyCheckBox PackageAutoSortTwiceCheckBox;
    private static UIButton SwitchMainPanelButton;
    private static MyComboBox GachaModeComboBox;
    private static ConfigEntry<bool> EnablePackageSortTwiceEntry;
    private static ConfigEntry<bool> EnablePackageAutoSortTwiceEntry;
    private static ConfigEntry<bool> EnablePackageLogisticEntry;
    public static int LeftClickTakeCount => ClickTakeCounts[LeftClickTakeCountEntry.Value];
    public static int RightClickTakeCount => ClickTakeCounts[RightClickTakeCountEntry.Value];
    public static bool ExtractToHand => ExtractTargetEntry.Value == 0;
    public static int[] TakeItemPriority => TakeItemPriorityArr[TakeItemPriorityEntry.Value];
    public static float DownloadThreshold => DownloadThresholdEntry.Value;
    public static float UploadThreshold => UploadThresholdEntry.Value;
    public static bool ShowFractionateRecipeDetails => ShowFractionateRecipeDetailsEntry.Value;
    public static bool EnableConfirmationDialog => EnableConfirmationDialogEntry.Value;
    public static bool EnablePackageSortTwice => EnablePackageSortTwiceEntry.Value;
    public static bool EnablePackageAutoSortTwice => EnablePackageAutoSortTwiceEntry.Value;
    public static bool EnablePackageLogistic => EnablePackageLogisticEntry.Value;

    public static void AddTranslations() {
        Register("杂项设置", "Miscellaneous");

        Register("左键单击时提取几组物品", "Extract how many sets of items when left-click");
        Register("右键单击时提取几组物品", "Extract how many sets of items when right-click");
        Register("物品提取目标", "Extraction target", "物品提取目标");
        Register("提取到手上", "To cursor", "提取到手上");
        Register("提取到背包", "To package", "提取到背包");

        Register("物品消耗顺序", "Order of consumption of items");
        Register("背包", "Package");
        Register("物流背包", "Delivery Package");
        //Register("分馏数据中心", "Fractionation Data Centre");

        Register("物流交互站下载阈值", "Interaction Station download threshold");
        Register("物流交互站上传阈值", "Interaction Station upload threshold");
        Register("物流交互站阈值修改说明",
            "To ensure consistent processing logic, this value cannot be modified in multiplayer games. You can change it in single player mode and save it before playing online.",
            "为保证处理逻辑一致，多人游戏中无法修改此值。你可以在单人模式修改并保存后再联机游玩。");

        Register("显示分馏配方详细信息", "Show fractionate recipe details");
        Register("抽卡模式", "Gacha Mode", "抽卡模式");
        Register("常规模式", "Normal Mode", "常规模式");
        Register("速通模式", "Speedrun Mode", "速通模式");
        Register("显示分馏配方详细信息说明",
            "Fractionation recipe details include the name, number, and probability of all products of the recipe.\nWhen disabled, the relevant information is gradually unlocked with the number of successful fractionate counts. When enabled, the relevant information is displayed directly.",
            "分馏配方详细信息包括配方所有产物的名称、数目、概率。\n禁用时，相关信息会随着分馏成功的次数逐渐解锁。启用时，相关信息会直接显示。");
        Register("启用确认弹窗", "Enable confirmation dialogs", "启用确认弹窗");
        Register("启用确认弹窗说明",
            "When disabled, FE's own question dialogs execute the confirm branch directly. Warning/info dialogs are not affected.",
            "关闭后，FE 自己的确认弹窗会直接执行“确定”分支；警告框和提示框不受影响。");

        Register("双击背包排序按钮将多余物品收入分馏数据中心",
            "Double-click the backpack sort button to store excess items in the distillation data center");
        Register("AutoSorter模组将背包中多余物品收入分馏数据中心",
            "The AutoSorter module collects surplus items into the distillation data center.");
    }

    public static void LoadConfig(ConfigFile configFile) {
        LeftClickTakeCountEntry = configFile.Bind("Miscellaneous", "LeftClickTakeCount", 0, "左键单击时提取几组物品");
        if (LeftClickTakeCountEntry.Value < 0 || LeftClickTakeCountEntry.Value >= ClickTakeCounts.Length) {
            LeftClickTakeCountEntry.Value = 0;
        }
        RightClickTakeCountEntry = configFile.Bind("Miscellaneous", "RightClickTakeCount", 3, "右键单击时提取几组物品");
        if (RightClickTakeCountEntry.Value < 0 || RightClickTakeCountEntry.Value >= ClickTakeCounts.Length) {
            RightClickTakeCountEntry.Value = 3;
        }
        ExtractTargetEntry = configFile.Bind("Miscellaneous", "ExtractTarget", 0, "物品提取目标");
        if (ExtractTargetEntry.Value < 0 || ExtractTargetEntry.Value >= ExtractTargetStrs.Length) {
            ExtractTargetEntry.Value = 0;
        }

        TakeItemPriorityEntry = configFile.Bind("Miscellaneous", "TakeItemPriority", 1, "物品消耗顺序");
        if (TakeItemPriorityEntry.Value < 0 || TakeItemPriorityEntry.Value >= TakeItemPriorityArr.Length) {
            TakeItemPriorityEntry.Value = 1;
        }

        DownloadThresholdEntry = configFile.Bind("Miscellaneous", "DownloadThreshold", 0.2f, "物流交互站下载阈值");
        if (DownloadThresholdEntry.Value < 0 || DownloadThresholdEntry.Value > 0.4f) {
            DownloadThresholdEntry.Value = 0.2f;
        }
        UploadThresholdEntry = configFile.Bind("Miscellaneous", "UploadThreshold", 0.8f, "物流交互站上传阈值");
        if (UploadThresholdEntry.Value < 0.6f || UploadThresholdEntry.Value > 1) {
            UploadThresholdEntry.Value = 0.8f;
        }

        ShowFractionateRecipeDetailsEntry =
            configFile.Bind("Miscellaneous", "ShowFractionateRecipeDetails", false, "显示分馏配方详细信息");
        EnableConfirmationDialogEntry =
            configFile.Bind("Miscellaneous", "EnableConfirmationDialog", true, "启用确认弹窗");

        EnablePackageSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageSortTwice", true, "双击背包排序按钮将多余物品收入分馏数据中心");
        EnablePackageAutoSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageAutoSortTwice", false, "AutoSorter模组将多余物品收入分馏数据中心");
        EnablePackageLogisticEntry =
            configFile.Bind("Miscellaneous", "PackageLogistic", false, "PackageLogistic兼容数据中心");
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        CreateUIInternal(wnd, trans);
    }

    private static void CreateUIInternal(MyWindow wnd, RectTransform parent) {
        PageLayout.HeaderRefs header = PageLayout.CreatePageHeader(wnd, parent, "杂项设置", "", "misc-setting-header");
        header.Summary.text = "把交互、阈值、抽卡模式与面板切换整理成统一设置页".WithColor(White);
        RectTransform configCard = PageLayout.CreateContentCard(parent, "misc-setting-config-card", 0f,
            PageLayout.HeaderHeight + PageLayout.Gap, PageLayout.DesignWidth, 635f, true);
        RectTransform footerCard = PageLayout.CreateFooterCard(parent, "misc-setting-footer-card",
            PageLayout.HeaderHeight + PageLayout.Gap * 2f + 635f);
        PageLayout.AddCardTitle(wnd, configCard, 18f, 14f, "参数配置", 15, "misc-setting-config-title");

        float x = 0f;
        float y = 52f;
        var txt = wnd.AddText2(x + 18f, y, configCard, "左键单击时提取几组物品");
        wnd.AddComboBox(x + 23f + txt.preferredWidth, y, configCard)
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(LeftClickTakeCountEntry);
        y += 36f;
        txt = wnd.AddText2(x + 18f, y, configCard, "右键单击时提取几组物品");
        wnd.AddComboBox(x + 23f + txt.preferredWidth, y, configCard)
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(RightClickTakeCountEntry);
        y += 36f;
        txt = wnd.AddText2(x + 18f, y, configCard, "物品提取目标");
        wnd.AddComboBox(x + 23f + txt.preferredWidth, y, configCard)
            .WithItems(ExtractTargetStrs).WithSize(200, 0).WithConfigEntry(ExtractTargetEntry);
        y += 36f;
        txt = wnd.AddText2(x + 18f, y, configCard, "物品消耗顺序");
        wnd.AddComboBox(x + 23f + txt.preferredWidth, y, configCard)
            .WithItems(TakeItemPriorityStrs).WithSize(400, 0).WithConfigEntry(TakeItemPriorityEntry);
        y += 36f;
        txt = wnd.AddText2(x + 18f, y, configCard, "抽卡模式");
        GachaModeComboBox = wnd.AddComboBox(x + 23f + txt.preferredWidth, y, configCard)
            .WithItems("常规模式", "速通模式")
            .WithSize(200, 0)
            .WithIndex((int)GachaManager.CurrentMode)
            .WithOnSelChanged(index => GachaManager.SetMode((GachaMode)index));
        y += 36f;
        txt = wnd.AddText2(x + 18f, y, configCard, "物流交互站下载阈值");
        DownloadThresholdSlider = wnd.AddSlider(x + 23f + txt.preferredWidth, y, configCard,
            DownloadThresholdEntry, new DownloadThresholdMapper(), "P0", 200f);
        DownloadThresholdTipsButton2 = wnd.AddTipsButton2(x + 28f + txt.preferredWidth + 200 + 5, y, configCard,
            "物流交互站下载阈值", "物流交互站阈值修改说明");
        y += 36f;
        txt = wnd.AddText2(x + 18f, y, configCard, "物流交互站上传阈值");
        UploadThresholdSlider = wnd.AddSlider(x + 23f + txt.preferredWidth, y, configCard,
            UploadThresholdEntry, new UploadThresholdMapper(), "P0", 200f);
        UploadThresholdTipsButton2 = wnd.AddTipsButton2(x + 28f + txt.preferredWidth + 200 + 5, y, configCard,
            "物流交互站上传阈值", "物流交互站阈值修改说明");
        y += 36f;
        var cb = wnd.AddCheckBox(x + 18f, y, configCard, ShowFractionateRecipeDetailsEntry, "显示分馏配方详细信息");
        wnd.AddTipsButton2(x + 23f + cb.Width + 5, y, configCard,
            "显示分馏配方详细信息", "显示分馏配方详细信息说明");
        y += 36f;
        cb = wnd.AddCheckBox(x + 18f, y, configCard, EnableConfirmationDialogEntry, "启用确认弹窗");
        wnd.AddTipsButton2(x + 23f + cb.Width + 5, y, configCard,
            "启用确认弹窗", "启用确认弹窗说明");
        if (AutoSorter.Enable) {
            y += 36f;
            PackageAutoSortTwiceCheckBox =
                wnd.AddCheckBox(x + 18f, y, configCard, EnablePackageAutoSortTwiceEntry, "AutoSorter模组将多余物品收入分馏数据中心");
        }
        y += 36f;
        PackageSortTwiceCheckBox = wnd.AddCheckBox(x + 18f, y, configCard, EnablePackageSortTwiceEntry, "双击背包排序按钮将多余物品收入分馏数据中心");
        SwitchMainPanelButton = wnd.AddButton(18f, 10f, 220f, footerCard,
            MainWindow.GetSwitchMainPanelButtonLabel(), 14,
            onClick: () => MainWindow.SwitchMainPanelFrom(MainWindow.GetCurrentMainPanelType()));
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        bool isMultiplayer = NebulaMultiplayerModAPI.IsMultiplayerActive;
        DownloadThresholdSlider.slider.interactable = !isMultiplayer;
        DownloadThresholdTipsButton2.gameObject.SetActive(isMultiplayer);
        UploadThresholdSlider.slider.interactable = !isMultiplayer;
        UploadThresholdTipsButton2.gameObject.SetActive(isMultiplayer);
        PackageSortTwiceCheckBox.enabled = TechItemInteractionUnlocked;
        if (AutoSorter.Enable) {
            PackageAutoSortTwiceCheckBox.enabled = TechItemInteractionUnlocked;
        }
        if (GachaModeComboBox != null) {
            GachaModeComboBox.SetIndex((int)GachaManager.CurrentMode);
        }

        RefreshSwitchMainPanelButtonLabel();
    }

    public static void ShowQuestion(string title, string content, Action onConfirm, Action onCancel = null) {
        if (!EnableConfirmationDialog) {
            onConfirm?.Invoke();
            return;
        }
        UIMessageBox.Show(title, content, "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => onConfirm?.Invoke(), () => onCancel?.Invoke());
    }

    private static void RefreshSwitchMainPanelButtonLabel() {
        if (SwitchMainPanelButton == null) {
            return;
        }

        string label = MainWindow.GetSwitchMainPanelButtonLabel();
        Transform buttonText = SwitchMainPanelButton.transform.Find("button-text");
        var localizer = buttonText?.GetComponent<Localizer>();
        if (localizer != null) {
            localizer.stringKey = label;
            localizer.translation = label.Translate();
        }

        var text = buttonText?.GetComponent<Text>();
        if (text != null) {
            text.text = label.Translate();
        }
    }

    private class DownloadThresholdMapper() : MyWindow.RangeValueMapper<float>(0, 20) {
        public override float IndexToValue(int index) => index * 0.02f;
        public override int ValueToIndex(float value) => (int)Math.Round(value / 0.02f);
    }

    private class UploadThresholdMapper() : MyWindow.RangeValueMapper<float>(0, 20) {
        public override float IndexToValue(int index) => 0.6f + index * 0.02f;
        public override int ValueToIndex(float value) => (int)Math.Round((value - 0.6f) / 0.02f);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Thresholds", br => {
                float downloadThreshold = br.ReadSingle();
                float uploadThreshold = br.ReadSingle();
                if (NebulaMultiplayerModAPI.IsClient) {
                    DownloadThresholdEntry.Value = downloadThreshold;
                    UploadThresholdEntry.Value = uploadThreshold;
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Thresholds", bw => {
                bw.Write(DownloadThreshold);
                bw.Write(UploadThreshold);
            })
        );
    }

    public static void IntoOtherSave() { }

    #endregion
}
