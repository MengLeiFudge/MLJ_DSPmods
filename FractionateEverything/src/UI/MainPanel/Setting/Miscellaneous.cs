using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Compatibility.Mods;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Setting;

/// <summary>
/// 主面板风格、快捷操作和物流阈值设置页面。
/// </summary>
public static class Miscellaneous {
    private static RectTransform storageTab;
    private static RectTransform interfaceTab;

    private static int[] ClickTakeCounts = [1, 3, 5, 10, 30, 50, 100];
    private static string[] ClickTakeCountsStr = ClickTakeCounts.Select(count => count.ToString()).ToArray();
    private static ConfigEntry<int> LeftClickTakeCountEntry;
    private static ConfigEntry<int> RightClickTakeCountEntry;
    private static readonly string[] ExtractTargetStrs = ["提取到手上", "提取到背包"];
    private static ConfigEntry<int> ExtractTargetEntry;

    private static readonly string[] TakeItemPriorityStrs = [
        "背包 -> 物流背包 -> 分馏亚空间",
        "背包 -> 分馏亚空间 -> 物流背包",
        "物流背包 -> 背包 -> 分馏亚空间",
        "物流背包 -> 分馏亚空间 -> 背包",
        "分馏亚空间 -> 背包 -> 物流背包",
        "分馏亚空间 -> 物流背包 -> 背包",
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

    private static ConfigEntry<bool> EnableConfirmationDialogEntry;

    private static MyCheckBox PackageSortTwiceCheckBox;
    private static MyCheckBox PackageAutoSortTwiceCheckBox;
    private static UIButton SwitchMainPanelButton;
    private static ConfigEntry<bool> EnablePackageSortTwiceEntry;
    private static ConfigEntry<bool> EnablePackageAutoSortTwiceEntry;
    private static ConfigEntry<bool> EnablePackageLogisticEntry;
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

        EnableConfirmationDialogEntry =
            configFile.Bind("Miscellaneous", "EnableConfirmationDialog", true, "启用确认弹窗");

        EnablePackageSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageSortTwice", true, "双击背包排序按钮将多余物品收入分馏亚空间");
        EnablePackageAutoSortTwiceEntry =
            configFile.Bind("Miscellaneous", "EnablePackageAutoSortTwice", false, "AutoSorter模组将背包中多余物品收入分馏亚空间");
        EnablePackageLogisticEntry =
            configFile.Bind("Miscellaneous", "PackageLogistic", false, "PackageLogistic兼容亚空间");
    }

    /// <summary>创建亚空间提取、消耗顺序及站点阈值设置。</summary>
    /// <param name="wnd">面板外壳。</param>
    /// <param name="trans">页面父节点。</param>
    public static void CreateStorageUI(MyWindow wnd, RectTransform trans) {
        storageTab = trans;
        LayoutNode[] children = [
            LabeledComboBoxNode("左键单击时提取几组物品", ClickTakeCountsStr, LeftClickTakeCountEntry, pos: (0, 0)),
            LabeledComboBoxNode("右键单击时提取几组物品", ClickTakeCountsStr, RightClickTakeCountEntry, pos: (1, 0)),
            LabeledComboBoxNode("物品提取目标", ExtractTargetStrs, ExtractTargetEntry, pos: (2, 0)),
            LabeledComboBoxNode("物品消耗顺序", TakeItemPriorityStrs, TakeItemPriorityEntry,
                tipTitle: "物品消耗顺序", tipContent: "物品消耗顺序说明", pos: (3, 0)),
            LabeledSliderNode("物流交互站下载阈值", DownloadThresholdEntry, new DownloadThresholdMapper(), "P0",
                tipTitle: "物流交互站下载阈值", tipContent: "物流交互站阈值修改说明",
                onSliderBuilt: s => DownloadThresholdSlider = s, onTipBuilt: t => DownloadThresholdTipsButton2 = t, pos: (4, 0)),
            LabeledSliderNode("物流交互站上传阈值", UploadThresholdEntry, new UploadThresholdMapper(), "P0",
                tipTitle: "物流交互站上传阈值", tipContent: "物流交互站阈值修改说明",
                onSliderBuilt: s => UploadThresholdSlider = s, onTipBuilt: t => UploadThresholdTipsButton2 = t, pos: (5, 0)),
        ];
        CreateSettingsLayout(wnd, trans, "亚空间存取", "FE.Settings.StorageSummary".Translate(), children);
    }

    /// <summary>创建界面偏好、收纳和已安装模组的兼容设置。</summary>
    /// <param name="wnd">面板外壳。</param>
    /// <param name="trans">页面父节点。</param>
    public static void CreateInterfaceUI(MyWindow wnd, RectTransform trans) {
        interfaceTab = trans;
        List<LayoutNode> children = [
            CheckBoxNode(EnableConfirmationDialogEntry, "启用确认弹窗", pos: (0, 0)),
            CheckBoxNode(EnablePackageSortTwiceEntry, "双击背包排序按钮将多余物品收入分馏亚空间",
                onBuilt: cb => PackageSortTwiceCheckBox = cb, pos: (1, 0)),
        ];
        if (AutoSorter.Enable) {
            children.Add(CheckBoxNode(EnablePackageAutoSortTwiceEntry, "AutoSorter模组将背包中多余物品收入分馏亚空间",
                onBuilt: cb => PackageAutoSortTwiceCheckBox = cb, pos: (children.Count, 0)));
        }
        children.Add(CheckBoxNode(EnablePackageLogisticEntry, "PackageLogistic兼容亚空间", pos: (children.Count, 0)));
        children.Add(ButtonNode(MainWindow.GetSwitchMainPanelButtonLabel(),
            onClick: () => MainWindow.SwitchMainPanelFrom(MainWindow.GetCurrentMainPanelType()),
            onBuilt: btn => SwitchMainPanelButton = btn, pos: (children.Count, 0)));
        CreateSettingsLayout(wnd, trans, "界面与兼容", "FE.Settings.InterfaceSummary".Translate(), children);
    }

    /// <summary>复用设置控件布局，保持两个页面独立的生命周期。</summary>
    /// <param name="wnd">外壳。</param>
    /// <param name="parent">页面父节点。</param>
    /// <param name="title">页面标题。</param>
    /// <param name="summary">页头提示。</param>
    /// <param name="children">按行排列的设置项。</param>
    private static void CreateSettingsLayout(MyWindow wnd, RectTransform parent, string title, string summary,
        IReadOnlyList<LayoutNode> children) {
        BuildLayout(wnd, parent, Grid(rows: [Px(PageLayout.HeaderHeight), 1], rowGap: PageLayout.Gap,
            children: [
                Header(title, pos: (0, 0), onBuilt: refs => refs.Summary.text = summary),
                ContentCard(pos: (1, 0), rows: children.Select(_ => Px(48)).ToArray(), children: children),
            ]));
    }

    /// <summary>仅刷新正在显示的设置页。</summary>
    public static void UpdateUI() {
        if (storageTab != null && storageTab.gameObject.activeInHierarchy) {
            bool isMultiplayer = NebulaMultiplayerModAPI.IsMultiplayerActive;
            DownloadThresholdSlider.slider.interactable = !isMultiplayer;
            DownloadThresholdTipsButton2.gameObject.SetActive(isMultiplayer);
            UploadThresholdSlider.slider.interactable = !isMultiplayer;
            UploadThresholdTipsButton2.gameObject.SetActive(isMultiplayer);
        }
        if (interfaceTab != null && interfaceTab.gameObject.activeInHierarchy) {
            PackageSortTwiceCheckBox.SetEnable(PackageAccessRules.TechItemInteractionUnlocked);
            PackageAutoSortTwiceCheckBox?.SetEnable(PackageAccessRules.TechItemInteractionUnlocked);
            RefreshSwitchMainPanelButtonLabel();
        }
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

    /// <summary>
    /// 物流下载阈值滑条映射。
    /// </summary>
    private class DownloadThresholdMapper() : MyWindow.RangeValueMapper<float>(0, 20) {
        public override float IndexToValue(int index) => index * 0.02f;
        public override int ValueToIndex(float value) => (int)Math.Round(value / 0.02f);
    }

    /// <summary>
    /// 物流上传阈值滑条映射。
    /// </summary>
    private class UploadThresholdMapper() : MyWindow.RangeValueMapper<float>(0, 20) {
        public override float IndexToValue(int index) => 0.6f + index * 0.02f;
        public override int ValueToIndex(float value) => (int)Math.Round((value - 0.6f) / 0.02f);
    }

    #region 模组存档接口实现

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
