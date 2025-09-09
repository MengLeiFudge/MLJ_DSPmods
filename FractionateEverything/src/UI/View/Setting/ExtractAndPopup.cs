using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class ExtractAndPopup {
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

    public static void AddTranslations() {
        Register("提取&弹窗", "Extract & Pop-up");

        Register("左键单击时提取几组物品", "Extract how many sets of items when left-click");
        Register("右键单击时提取几组物品", "Extract how many sets of items when right-click");

        Register("背包", "Package");
        Register("物流背包", "Delivery Package");
        //Register("分馏数据中心", "Fractionation Data Centre");
    }

    public static void LoadConfig(ConfigFile configFile) {
        LeftClickTakeCountEntry = configFile.Bind("Extract & Pop-up", "LeftClickTakeCount", 0, "左键单击时提取几组物品");
        if (LeftClickTakeCountEntry.Value < 0 || LeftClickTakeCountEntry.Value >= ClickTakeCounts.Length) {
            LeftClickTakeCountEntry.Value = 0;
        }
        RightClickTakeCountEntry = configFile.Bind("Extract & Pop-up", "RightClickTakeCount", 3, "右键单击时提取几组物品");
        if (RightClickTakeCountEntry.Value < 0 || RightClickTakeCountEntry.Value >= ClickTakeCounts.Length) {
            RightClickTakeCountEntry.Value = 3;
        }
        TakeItemPriorityEntry = configFile.Bind("Extract & Pop-up", "TakeItemPriority", 1, "使用物品顺序");
        if (TakeItemPriorityEntry.Value < 0 || TakeItemPriorityEntry.Value >= TakeItemPriorityArr.Length) {
            TakeItemPriorityEntry.Value = 1;
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "提取&弹窗");
        float x = 0f;
        float y = 18f;
        wnd.AddComboBox(x, y, tab, "左键单击时提取几组物品")
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(LeftClickTakeCountEntry);
        y += 36f;
        wnd.AddComboBox(x, y, tab, "右键单击时提取几组物品")
            .WithItems(ClickTakeCountsStr).WithSize(200, 0).WithConfigEntry(RightClickTakeCountEntry);
        y += 36f;
        wnd.AddComboBox(x, y, tab, "使用物品顺序")
            .WithItems(TakeItemPriorityStrs).WithSize(400, 0).WithConfigEntry(TakeItemPriorityEntry);
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
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
