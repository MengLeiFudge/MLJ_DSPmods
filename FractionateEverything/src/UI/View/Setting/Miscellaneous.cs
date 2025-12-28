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
    public static int LeftClickTakeCount => ClickTakeCounts[LeftClickTakeCountEntry.Value];
    private static ConfigEntry<int> RightClickTakeCountEntry;
    public static int RightClickTakeCount => ClickTakeCounts[RightClickTakeCountEntry.Value];

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
    public static int[] TakeItemPriority => TakeItemPriorityArr[TakeItemPriorityEntry.Value];

    private static MySlider DownloadThresholdSlider;
    private static UIButton DownloadThresholdTipsButton2;
    private static ConfigEntry<float> DownloadThresholdEntry;
    public static float DownloadThreshold => DownloadThresholdEntry.Value;

    private class DownloadThresholdMapper() : MyWindow.RangeValueMapper<float>(0, 20) {
        public override float IndexToValue(int index) => index * 0.02f;
        public override int ValueToIndex(float value) => (int)Math.Round(value / 0.02f);
    }

    private static MySlider UploadThresholdSlider;
    private static UIButton UploadThresholdTipsButton2;
    private static ConfigEntry<float> UploadThresholdEntry;
    public static float UploadThreshold => UploadThresholdEntry.Value;

    private class UploadThresholdMapper() : MyWindow.RangeValueMapper<float>(0, 20) {
        public override float IndexToValue(int index) => 0.6f + index * 0.02f;
        public override int ValueToIndex(float value) => (int)Math.Round((value - 0.6f) / 0.02f);
    }

    private static ConfigEntry<bool> ShowFractionateRecipeDetailsEntry;
    public static bool ShowFractionateRecipeDetails => ShowFractionateRecipeDetailsEntry.Value;

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
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "杂项设置");
        float x = 0f;
        float y = 18f;
        wnd.AddComboBox(x, y, tab, "左键单击时提取几组物品")
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(LeftClickTakeCountEntry);
        y += 36f;
        wnd.AddComboBox(x, y, tab, "右键单击时提取几组物品")
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(RightClickTakeCountEntry);
        y += 36f;
        wnd.AddComboBox(x, y, tab, "物品消耗顺序")
            .WithItems(TakeItemPriorityStrs).WithSize(400, 0).WithConfigEntry(TakeItemPriorityEntry);
        y += 36f;
        var txt = wnd.AddText2(x, y, tab, "物流交互站下载阈值");
        DownloadThresholdSlider = wnd.AddSlider(x + txt.preferredWidth + 5, y, tab,
            DownloadThresholdEntry, new DownloadThresholdMapper(), "P0", 200f);
        DownloadThresholdTipsButton2 = wnd.AddTipsButton2(x + txt.preferredWidth + 5 + 200 + 5, y, tab,
            "物流交互站下载阈值", "物流交互站阈值修改说明");
        y += 36f;
        txt = wnd.AddText2(x, y, tab, "物流交互站上传阈值");
        UploadThresholdSlider = wnd.AddSlider(x + txt.preferredWidth + 5, y, tab,
            UploadThresholdEntry, new UploadThresholdMapper(), "P0", 200f);
        UploadThresholdTipsButton2 = wnd.AddTipsButton2(x + txt.preferredWidth + 5 + 200 + 5, y, tab,
            "物流交互站上传阈值", "物流交互站阈值修改说明");
        y += 36f;
        var cb = wnd.AddCheckBox(x, y, tab, ShowFractionateRecipeDetailsEntry, "显示分馏配方详细信息");
        wnd.AddTipsButton2(x + cb.Width + 5, y, tab,
            "显示分馏配方详细信息", "显示分馏配方详细信息说明");
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
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        if (version >= 2) {
            float downloadThreshold = r.ReadSingle();
            float uploadThreshold = r.ReadSingle();
            if (NebulaMultiplayerModAPI.IsClient) {
                DownloadThresholdEntry.Value = downloadThreshold;
                UploadThresholdEntry.Value = uploadThreshold;
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(DownloadThreshold);
        w.Write(UploadThreshold);
    }

    public static void IntoOtherSave() { }

    #endregion
}
