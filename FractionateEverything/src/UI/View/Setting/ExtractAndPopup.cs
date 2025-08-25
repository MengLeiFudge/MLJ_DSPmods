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

    public static void AddTranslations() {
        Register("提取&弹窗", "Extract & Pop-up");

        Register("1组", "1 set");
        Register("3组", "3 sets");
        Register("10组", "10 sets");
        Register("30组", "30 sets");
        Register("100组", "100 sets");
        Register("全部", "All");
    }

    public static void LoadConfig(ConfigFile configFile) {
        LeftClickTakeCountEntry = configFile.Bind("Extract & Pop-up", "LeftClickTakeCount", 0, "左键单击时提取物品数目");
        if (LeftClickTakeCountEntry.Value < 0 || LeftClickTakeCountEntry.Value >= ClickTakeCounts.Length) {
            LeftClickTakeCountEntry.Value = 0;
        }
        RightClickTakeCountEntry = configFile.Bind("Extract & Pop-up", "RightClickTakeCount", 3, "右键单击时提取物品数目");
        if (RightClickTakeCountEntry.Value < 0 || RightClickTakeCountEntry.Value >= ClickTakeCounts.Length) {
            RightClickTakeCountEntry.Value = 3;
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
