using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SaveDataExporter;

/// <summary>
/// 在原版设置“杂项”页注入导出文件命名模式下拉框，并同步配置读写。
/// </summary>
internal static class OutputFileNameOptionWindow {
    /// <summary>
    /// 原版设置-杂项页面的完整路径，用于回退查找。
    /// </summary>
    private const string OptionWindowMiscContentPath =
        "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5";

    /// <summary>
    /// 原版提示等级标签路径，用作下拉标签的克隆预制体。
    /// </summary>
    private const string OptionWindowTipLevelLabelPath =
        "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5/labels/tiplevel";

    /// <summary>
    /// 原版提示等级下拉框路径，用作下拉框的克隆预制体。
    /// </summary>
    private const string OptionWindowTipLevelComboPath =
        "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5/comps/ComboBox";

    /// <summary>
    /// 注入的导出文件命名模式标签的 GameObject 名。
    /// </summary>
    private const string OptionWindowLabelName = "sde-output-file-name-mode-label";

    /// <summary>
    /// 注入的导出文件命名模式下拉框的 GameObject 名。
    /// </summary>
    private const string OptionWindowComboName = "sde-output-file-name-mode-combo";

    /// <summary>
    /// 杂项页面首行选项的 Y 坐标起点。
    /// </summary>
    private const float OptionRowStartY = -220f;

    /// <summary>
    /// 杂项页面行间距。
    /// </summary>
    private const float OptionRowStepY = 40f;

    /// <summary>
    /// 插件日志源，供控件创建或刷新失败时输出警告。
    /// </summary>
    private static ManualLogSource logger;

    /// <summary>
    /// 注入控件所在父 Transform（杂项页面内容容器）。
    /// </summary>
    private static Transform outputFileNameModeParent;

    /// <summary>
    /// 注入的导出文件命名模式下拉框组件实例。
    /// </summary>
    private static UIComboBox outputFileNameModeComboBox;

    /// <summary>
    /// 注入日志源，供控件创建失败时输出警告。
    /// </summary>
    /// <param name="logSource">插件日志。</param>
    public static void Initialize(ManualLogSource logSource) {
        logger = logSource;
    }

    /// <summary>
    /// 在杂项页面打开后注入/刷新导出文件命名控件。
    /// 排在 GenesisBook 和 OrbitalRing 之后，避免它们的杂项注入被覆盖。
    /// </summary>
    /// <param name="__instance">UIOptionWindow 实例，用于查找杂项页容器。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), "_OnOpen")]
    [HarmonyAfter(new string[] { "org.LoShin.GenesisBook", "org.ProfessorCat305.OrbitalRing" })]
    private static void UIOptionWindow_OnOpen_Postfix(UIOptionWindow __instance) {
        EnsureOptionWindowControl(__instance);
        ResetOptionWindowControl();
    }

    /// <summary>
    /// 杂项页用户点击「取消」时（idx==4 对应杂项），将控件重置为当前配置值。
    /// </summary>
    /// <param name="idx">取消按钮按下的设置页索引；4 对应杂项页（内容页-5）。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnRevertButtonClick))]
    private static void UIOptionWindow_OnRevertButtonClick_Postfix(int idx) {
        if (idx == 4) {
            ResetOptionWindowControl();
        }
    }

    /// <summary>
    /// 杂项页用户点击「应用」时，将下拉框当前选中值持久化到配置。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnApplyClick))]
    private static void UIOptionWindow_OnApplyClick_Postfix() {
        if (outputFileNameModeComboBox == null) {
            return;
        }

        ExportSettings.SetOutputFileNameMode(IndexToOutputFileNameMode(outputFileNameModeComboBox.itemIndex));
    }

    /// <summary>
    /// 确保杂项页面中的导出文件命名控件存在且状态最新；
    /// 若控件尚未创建则克隆原版提示等级下拉样式注入。
    /// </summary>
    /// <param name="optionWindow">当前设置窗口实例，用于获取杂项页容器。</param>
    private static void EnsureOptionWindowControl(UIOptionWindow optionWindow) {
        Transform parent = GetOptionWindowMiscContent(optionWindow);
        if (parent == null) {
            return;
        }

        Transform existing = FindDirectChild(parent, OptionWindowComboName);
        if (existing != null) {
            outputFileNameModeParent = parent;
            outputFileNameModeComboBox = existing.GetComponentInChildren<UIComboBox>();
            if (outputFileNameModeComboBox == null) {
                logger.LogWarning("原版设置-杂项页面下拉框已存在但组件缺失，无法刷新导出文件命名模式。");
                return;
            }

            RefreshOptionWindowControlText();
            return;
        }

        GameObject labelPrefab = GameObject.Find(OptionWindowTipLevelLabelPath);
        GameObject comboPrefab = GameObject.Find(OptionWindowTipLevelComboPath);
        if (labelPrefab == null || comboPrefab == null) {
            logger.LogWarning("未找到原版设置-杂项页面的提示等级控件，无法添加导出文件命名模式下拉框。");
            return;
        }

        float rowY = GetNextOptionRowY(parent);
        outputFileNameModeParent = parent;
        GameObject labelObject = UnityEngine.Object.Instantiate(labelPrefab, parent);
        labelObject.name = OptionWindowLabelName;
        DestroyLocalizer(labelObject);
        ((RectTransform)labelObject.transform).anchoredPosition = new Vector2(30f, rowY);

        GameObject comboObject = UnityEngine.Object.Instantiate(comboPrefab, parent);
        comboObject.name = OptionWindowComboName;
        DestroyLocalizer(comboObject);
        ((RectTransform)comboObject.transform).anchoredPosition = new Vector2(340f, rowY);

        outputFileNameModeComboBox = comboObject.GetComponentInChildren<UIComboBox>();
        if (outputFileNameModeComboBox == null) {
            logger.LogWarning("原版设置-杂项页面下拉框克隆失败，无法添加导出文件命名模式。");
            return;
        }

        ((UnityEventBase)outputFileNameModeComboBox.onItemIndexChange).RemoveAllListeners();
        ((RectTransform)((Component)outputFileNameModeComboBox).transform).sizeDelta = new Vector2(430f, 30f);
        RefreshOptionWindowControlText();
    }

    /// <summary>
    /// 获取杂项页面内容容器 Transform，优先通过 <paramref name="optionWindow"/> 的 tipLevelComp
    /// 导航到父容器；兜底用固定路径 GameObject.Find。
    /// </summary>
    /// <param name="optionWindow">当前设置窗口实例。</param>
    /// <returns>内容容器 Transform，未找到时返回 null。</returns>
    private static Transform GetOptionWindowMiscContent(UIOptionWindow optionWindow) {
        Transform tipLevelTransform = optionWindow?.tipLevelComp == null
            ? null
            : ((Component)optionWindow.tipLevelComp).transform;
        Transform parent = tipLevelTransform?.parent?.parent;
        if (parent != null) {
            return parent;
        }

        return GameObject.Find(OptionWindowMiscContentPath)?.transform;
    }

    /// <summary>
    /// 在父 Transform 的直接子级中按名称查找。不递归。
    /// </summary>
    /// <param name="parent">父 Transform。</param>
    /// <param name="objectName">子 GameObject 名称。</param>
    /// <returns>匹配子级，不存在时返回 null。</returns>
    private static Transform FindDirectChild(Transform parent, string objectName) {
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name == objectName) {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 计算下一个可用选项行的 Y 坐标位置，避开已存在的子控件。
    /// </summary>
    /// <param name="parent">杂项页内容容器 Transform。</param>
    /// <returns>新行的 Y 坐标（负数表示向下）。</returns>
    private static float GetNextOptionRowY(Transform parent) {
        float rowY = OptionRowStartY;
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name is OptionWindowLabelName or OptionWindowComboName || child is not RectTransform rect) {
                continue;
            }

            float childY = rect.anchoredPosition.y;
            if (childY <= OptionRowStartY + 0.1f) {
                rowY = Math.Min(rowY, childY - OptionRowStepY);
            }
        }

        return rowY;
    }

    /// <summary>
    /// 刷新标签文本和下拉选项内容，然后重置为当前配置值。
    /// 在控件已存在且 UI 语言可能变化时（如打开设置页）调用。
    /// </summary>
    private static void RefreshOptionWindowControlText() {
        if (outputFileNameModeComboBox == null) {
            return;
        }

        Transform labelTransform = outputFileNameModeParent == null
            ? null
            : FindDirectChild(outputFileNameModeParent, OptionWindowLabelName);
        if (labelTransform != null && labelTransform.TryGetComponent(out Text labelText)) {
            labelText.text = GetOutputFileNameModeLabel();
        }

        outputFileNameModeComboBox.Items.Clear();
        outputFileNameModeComboBox.Items.AddRange(GetOutputFileNameModeOptions());
        outputFileNameModeComboBox.ItemsData.Clear();
        outputFileNameModeComboBox.translated = true;
        outputFileNameModeComboBox.UpdateItems();
        ResetOptionWindowControl();
    }

    /// <summary>
    /// 将下拉框选中项重置为当前配置的导出文件命名模式。
    /// </summary>
    private static void ResetOptionWindowControl() {
        if (outputFileNameModeComboBox != null) {
            outputFileNameModeComboBox.itemIndex =
                OutputFileNameModeToIndex(ExportSettings.GetOutputFileNameMode());
        }
    }

    /// <summary>
    /// 从克隆的预制体上移除 Localizer 组件，避免覆盖原版提示等级标签的本地化映射。
    /// </summary>
    /// <param name="obj">刚从原版控件克隆的 GameObject。</param>
    private static void DestroyLocalizer(GameObject obj) {
        Localizer localizer = obj.GetComponent<Localizer>();
        if (localizer != null) {
            UnityEngine.Object.DestroyImmediate(localizer);
        }
    }

    /// <summary>
    /// 根据当前游戏语言返回导出文件命名模式标签的本地化文本。
    /// </summary>
    /// <returns>中/英文标签。</returns>
    private static string GetOutputFileNameModeLabel() {
        return IsChineseLanguage()
            ? "导出文件命名模式"
            : "Export file naming";
    }

    /// <summary>
    /// 根据当前游戏语言返回下拉选项列表（2 项：时间戳新文件或固定存档名覆盖）。
    /// </summary>
    /// <returns>本地化后的选项文本列表。</returns>
    private static System.Collections.Generic.List<string> GetOutputFileNameModeOptions() {
        return IsChineseLanguage()
            ? ["含导出时间（每次新文件）", "固定存档名（覆盖同名文件）"]
            : ["Timestamped new file", "Save-name overwrite"];
    }

    /// <summary>
    /// 判断当前游戏 UI 语言是否为中文（简体/繁体/香港繁体）。
    /// </summary>
    /// <returns>中文字体环境时返回 true。</returns>
    private static bool IsChineseLanguage() {
        return Localization.CurrentLanguageLCID is 2052 or 1028 or 3076;
    }

    /// <summary>
    /// 将命名模式映射为杂项页下拉索引。
    /// </summary>
    private static int OutputFileNameModeToIndex(OutputFileNameMode mode) {
        return mode == OutputFileNameMode.SaveNameOverwrite ? 1 : 0;
    }

    /// <summary>
    /// 将杂项页下拉索引映射为命名模式。
    /// </summary>
    private static OutputFileNameMode IndexToOutputFileNameMode(int index) {
        return index == 1 ? OutputFileNameMode.SaveNameOverwrite : OutputFileNameMode.TimestampedNewFile;
    }
}
