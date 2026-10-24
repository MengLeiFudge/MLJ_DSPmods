using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Compatibility.Mods;
using FE.Configuration;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Configuration.ModSettings;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Setting;

/// <summary>
/// 主面板风格、快捷操作和物流阈值设置页面。
/// </summary>
public static class Miscellaneous {
    private static RectTransform storageTab;
    private static RectTransform interfaceTab;

    private static string[] ClickTakeCountsStr = ClickTakeCounts.Select(count => count.ToString()).ToArray();
    private static readonly string[] ExtractTargetStrs = ["提取到手上", "提取到背包"];

    private static readonly string[] TakeItemPriorityStrs = [
        "背包 -> 物流背包 -> 分馏亚空间",
        "背包 -> 分馏亚空间 -> 物流背包",
        "物流背包 -> 背包 -> 分馏亚空间",
        "物流背包 -> 分馏亚空间 -> 背包",
        "分馏亚空间 -> 背包 -> 物流背包",
        "分馏亚空间 -> 物流背包 -> 背包",
    ];

    private static MySlider DownloadThresholdSlider;
    private static UIButton DownloadThresholdTipsButton2;

    private static MySlider UploadThresholdSlider;
    private static UIButton UploadThresholdTipsButton2;


    private static MyCheckBox PackageSortTwiceCheckBox;
    private static MyCheckBox PackageAutoSortTwiceCheckBox;
    private static UIButton SwitchMainPanelButton;
    public static int LeftClickTakeCount => ModSettings.LeftClickTakeCount;
    public static int RightClickTakeCount => ModSettings.RightClickTakeCount;
    public static bool ExtractToHand => ModSettings.ExtractToHand;
    public static int[] TakeItemPriority => ModSettings.TakeItemPriority;
    public static float DownloadThreshold => ModSettings.DownloadThreshold;
    public static float UploadThreshold => ModSettings.UploadThreshold;
    public static bool EnableConfirmationDialog => ModSettings.EnableConfirmationDialog;
    public static bool EnablePackageSortTwice => ModSettings.EnablePackageSortTwice;
    public static bool EnablePackageAutoSortTwice => ModSettings.EnablePackageAutoSortTwice;
    public static bool EnablePackageLogistic => ModSettings.EnablePackageLogistic;

    /// <summary>保留既有公开配置入口；实际配置由 ModSettings 持有。</summary>
    public static void LoadConfig(ConfigFile configFile) => ModSettings.LoadConfig(configFile);

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

    /// <summary>保留既有公开存档入口和块格式。</summary>
    public static void Import(BinaryReader r) => ModSettings.Import(r);

    /// <summary>保留既有公开存档入口和块格式。</summary>
    public static void Export(BinaryWriter w) => ModSettings.Export(w);

    public static void IntoOtherSave() { }

    #endregion
}
