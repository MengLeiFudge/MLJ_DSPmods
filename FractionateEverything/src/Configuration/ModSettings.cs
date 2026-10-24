using System.IO;
using BepInEx.Configuration;
using FE.Compatibility.Nebula;
using static FE.Utils.Utils;

namespace FE.Configuration;

/// <summary>共享玩家偏好与物流阈值；业务和界面读取同一份配置，不依赖页面生命周期。</summary>
public static class ModSettings {
    internal static readonly int[] ClickTakeCounts = [1, 3, 5, 10, 30, 50, 100];
    internal static ConfigEntry<int> LeftClickTakeCountEntry;
    internal static ConfigEntry<int> RightClickTakeCountEntry;
    internal static ConfigEntry<int> ExtractTargetEntry;

    private static readonly int[][] TakeItemPriorityArr = [
        [0, 1, 2],
        [0, 2, 1],
        [1, 0, 2],
        [1, 2, 0],
        [2, 0, 1],
        [2, 1, 0],
    ];
    internal static ConfigEntry<int> TakeItemPriorityEntry;

    internal static ConfigEntry<float> DownloadThresholdEntry;
    internal static ConfigEntry<float> UploadThresholdEntry;
    internal static ConfigEntry<bool> EnableConfirmationDialogEntry;
    internal static ConfigEntry<bool> EnablePackageSortTwiceEntry;
    internal static ConfigEntry<bool> EnablePackageAutoSortTwiceEntry;
    internal static ConfigEntry<bool> EnablePackageLogisticEntry;
    public static int LeftClickTakeCount => ClickTakeCounts[LeftClickTakeCountEntry.Value];
    public static int RightClickTakeCount => ClickTakeCounts[RightClickTakeCountEntry.Value];
    public static bool ExtractToHand => ExtractTargetEntry.Value == 0;
    public static int[] TakeItemPriority => TakeItemPriorityArr[TakeItemPriorityEntry.Value];
    public static float DownloadThreshold => DownloadThresholdEntry.Value;
    public static float UploadThreshold => UploadThresholdEntry.Value;
    public static bool EnableConfirmationDialog => EnableConfirmationDialogEntry.Value;
    public static bool EnablePackageSortTwice => EnablePackageSortTwiceEntry.Value;
    public static bool EnablePackageAutoSortTwice => EnablePackageAutoSortTwiceEntry.Value;
    public static bool EnablePackageLogistic => EnablePackageLogisticEntry.Value;

    /// <summary>绑定既有 Miscellaneous 配置键，并沿用原有范围校正。</summary>
    /// <param name="configFile">插件配置文件。</param>
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
        if (ExtractTargetEntry.Value < 0 || ExtractTargetEntry.Value >= 2) {
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

        EnableConfirmationDialogEntry =
            configFile.Bind("Miscellaneous", "EnableConfirmationDialog", true, "启用确认弹窗");

        EnablePackageSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageSortTwice", true, "双击背包排序按钮将多余物品收入分馏亚空间");
        EnablePackageAutoSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageAutoSortTwice", false, "AutoSorter模组将背包中多余物品收入分馏亚空间");
        EnablePackageLogisticEntry =
            configFile.Bind("Miscellaneous", "PackageLogistic", false, "PackageLogistic兼容亚空间");
    }

    /// <summary>读取物流阈值块；仅联机客户端应用主机设置。</summary>
    /// <param name="r">当前 Miscellaneous 存档块的读取器。</param>
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Thresholds", br => {
                float downloadThreshold = br.ReadSingle();
                float uploadThreshold = br.ReadSingle();
                if (NebulaMultiplayerModAPI.IsClient) {
                    DownloadThresholdEntry.Value = downloadThreshold;
                    UploadThresholdEntry.Value = uploadThreshold;
                }
            }),
            ("DownloadThreshold", br => { float value = br.ReadSingle(); if (NebulaMultiplayerModAPI.IsClient) { DownloadThresholdEntry.Value = value; } }),
            ("UploadThreshold", br => { float value = br.ReadSingle(); if (NebulaMultiplayerModAPI.IsClient) { UploadThresholdEntry.Value = value; } })
        );
    }

    /// <summary>下载和上传阈值分别保存，缺少一个字段不影响另一个。</summary>
    /// <param name="w">当前 Miscellaneous 存档块的写入器。</param>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("DownloadThreshold", bw => bw.Write(DownloadThreshold)),
            ("UploadThreshold", bw => bw.Write(UploadThreshold))
        );
    }
}
