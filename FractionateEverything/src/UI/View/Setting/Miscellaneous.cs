using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Compatibility;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class Miscellaneous {
    private static RectTransform window;
    private static RectTransform tab;

    private static int[] ClickTakeCounts = [1, 3, 5, 10, 30, 50, 100];
    private static string[] ClickTakeCountsStr = ClickTakeCounts.Select(count => count.ToString()).ToArray();
    private static ConfigEntry<int> LeftClickTakeCountEntry;
    private static ConfigEntry<int> RightClickTakeCountEntry;

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

    private static MyCheckBox PackageSortTwiceCheckBox;
    private static MyCheckBox PackageAutoSortTwiceCheckBox;
    private static ConfigEntry<bool> EnablePackageSortTwiceEntry;
    private static ConfigEntry<bool> EnablePackageAutoSortTwiceEntry;
    private static ConfigEntry<bool> EnablePackageLogisticEntry;
    public static int LeftClickTakeCount => ClickTakeCounts[LeftClickTakeCountEntry.Value];
    public static int RightClickTakeCount => ClickTakeCounts[RightClickTakeCountEntry.Value];
    public static int[] TakeItemPriority => TakeItemPriorityArr[TakeItemPriorityEntry.Value];
    public static float DownloadThreshold => DownloadThresholdEntry.Value;
    public static float UploadThreshold => UploadThresholdEntry.Value;
    public static bool ShowFractionateRecipeDetails => ShowFractionateRecipeDetailsEntry.Value;
    public static bool EnablePackageSortTwice => EnablePackageSortTwiceEntry.Value;
    public static bool EnablePackageAutoSortTwice => EnablePackageAutoSortTwiceEntry.Value;
    public static bool EnablePackageLogistic => EnablePackageLogisticEntry.Value;

    public static void AddTranslations() {
        Register("杂项设置", "Miscellaneous");

        Register("左键单击时提取几组物品", "Extract how many sets of items when left-click");
        Register("右键单击时提取几组物品", "Extract how many sets of items when right-click");

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
        Register("显示分馏配方详细信息说明",
            "Fractionation recipe details include the name, number, and probability of all products of the recipe.\nWhen disabled, the relevant information is gradually unlocked with the number of successful fractionate counts. When enabled, the relevant information is displayed directly.",
            "分馏配方详细信息包括配方所有产物的名称、数目、概率。\n禁用时，相关信息会随着分馏成功的次数逐渐解锁。启用时，相关信息会直接显示。");

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

        EnablePackageSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageSortTwice", true, "双击背包排序按钮将多余物品收入分馏数据中心");
        EnablePackageAutoSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageAutoSortTwice", false, "AutoSorter模组将多余物品收入分馏数据中心");
        EnablePackageLogisticEntry =
            configFile.Bind("Miscellaneous", "PackageLogistic", false, "PackageLogistic兼容数据中心");
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "杂项设置");
        float x = 0f;
        float y = 18f;
        var txt = wnd.AddText2(x, y, tab, "左键单击时提取几组物品");
        wnd.AddComboBox(x + 5 + txt.preferredWidth, y, tab)
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(LeftClickTakeCountEntry);
        y += 36f;
        txt = wnd.AddText2(x, y, tab, "右键单击时提取几组物品");
        wnd.AddComboBox(x + 5 + txt.preferredWidth, y, tab)
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(RightClickTakeCountEntry);
        y += 36f;
        txt = wnd.AddText2(x, y, tab, "物品消耗顺序");
        wnd.AddComboBox(x + 5 + txt.preferredWidth, y, tab)
            .WithItems(TakeItemPriorityStrs).WithSize(400, 0).WithConfigEntry(TakeItemPriorityEntry);
        y += 36f;
        txt = wnd.AddText2(x, y, tab, "物流交互站下载阈值");
        DownloadThresholdSlider = wnd.AddSlider(x + 5 + txt.preferredWidth, y, tab,
            DownloadThresholdEntry, new DownloadThresholdMapper(), "P0", 200f);
        DownloadThresholdTipsButton2 = wnd.AddTipsButton2(x + 5 + txt.preferredWidth + 200 + 5, y, tab,
            "物流交互站下载阈值", "物流交互站阈值修改说明");
        y += 36f;
        txt = wnd.AddText2(x, y, tab, "物流交互站上传阈值");
        UploadThresholdSlider = wnd.AddSlider(x + 5 + txt.preferredWidth, y, tab,
            UploadThresholdEntry, new UploadThresholdMapper(), "P0", 200f);
        UploadThresholdTipsButton2 = wnd.AddTipsButton2(x + 5 + txt.preferredWidth + 200 + 5, y, tab,
            "物流交互站上传阈值", "物流交互站阈值修改说明");
        y += 36f;
        var cb = wnd.AddCheckBox(x, y, tab, ShowFractionateRecipeDetailsEntry, "显示分馏配方详细信息");
        wnd.AddTipsButton2(x + cb.Width + 5, y, tab,
            "显示分馏配方详细信息", "显示分馏配方详细信息说明");
        if (AutoSorter.Enable) {
            y += 36f;
            PackageAutoSortTwiceCheckBox = wnd.AddCheckBox(x, y, tab, EnablePackageAutoSortTwiceEntry, "AutoSorter模组将多余物品收入分馏数据中心");
        }
        y += 36f;
        PackageSortTwiceCheckBox = wnd.AddCheckBox(x, y, tab, EnablePackageSortTwiceEntry, "双击背包排序按钮将多余物品收入分馏数据中心");
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
        int version = r.ReadInt32();
        float downloadThreshold = r.ReadSingle();
        float uploadThreshold = r.ReadSingle();
        if (NebulaMultiplayerModAPI.IsClient) {
            DownloadThresholdEntry.Value = downloadThreshold;
            UploadThresholdEntry.Value = uploadThreshold;
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(DownloadThreshold);
        w.Write(UploadThreshold);
    }

    public static void IntoOtherSave() { }

    #endregion
}
