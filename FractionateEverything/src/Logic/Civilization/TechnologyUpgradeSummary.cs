using System;
using System.Runtime.CompilerServices;
using FE.Logic.Enhancements.Stacking;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>原版自动化升级数据末尾的FE加成汇总，只读取运行效果，不持有研究状态。</summary>
internal static class TechnologyUpgradeSummary {
    private sealed class Panel {
        public RectTransform Root;
        public Text Title;
        public Text Labels;
        public Text Values;
    }

    private static readonly ConditionalWeakTable<UITechTree, Panel> Panels = new();

    /// <summary>随原版数据和语言刷新；父级负责页签显隐及对象销毁。</summary>
    internal static void Refresh(UITechTree tree) {
        if (GameMain.history == null || tree.rect1_8 == null || tree.dataLable1_8 == null) { return; }
        Panel panel = Panels.GetValue(tree, Create);
        if (panel.Root == null) { return; }
        panel.Title.text = "万物分馏".Translate();
        panel.Labels.text = "FE.Growth.Summary.Labels".Translate();
        panel.Values.text = $"{StackingManager.CurrentMaxStack}\n"
            + $"{GrowthTechnologyManager.AdvancedMineralCatalystConsumptionRatio:0%}\n"
            + $"{GrowthTechnologyManager.EmbryoBreedingOutput:0.##}\n"
            + $"{GrowthTechnologyManager.AdvancedConversionRecoveryRatio:0%}\n"
            + $"{GrowthTechnologyManager.SubspaceEnergyRatio:0%}\n"
            + $"{GrowthTechnologyManager.InteractionStationPowerRatio:0%}";
        RectTransform previous = tree.rect1_8;
        panel.Root.anchoredPosition = previous.anchoredPosition - new Vector2(0f, previous.rect.height + 5f);
        float height = Mathf.Max(panel.Labels.preferredHeight, panel.Values.preferredHeight);
        panel.Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(150f, 34f + height));
        panel.Labels.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        panel.Values.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    /// <summary>复制原版“其他”分组，保留背景、标题、双列文字的字体材质、透明度和间距。</summary>
    private static Panel Create(UITechTree tree) {
        Text[] originals = tree.rect1_8.GetComponentsInChildren<Text>(true);
        int labelIndex = Array.IndexOf(originals, tree.dataLable1_8);
        int valueIndex = Array.IndexOf(originals, tree.dataValue1_8);
        int titleIndex = Array.FindIndex(originals, text => text.GetComponent<Localizer>()?.stringKey == "其他"
            || text.text == "其他".Translate());
        if (labelIndex < 0 || valueIndex < 0 || titleIndex < 0) {
            LogWarning("[UI] 无法定位原版升级数据分组的标题或数值列，万物分馏汇总未创建。");
            return new Panel();
        }
        RectTransform root = UnityEngine.Object.Instantiate(tree.rect1_8, tree.rect1_8.parent, false);
        root.name = "fe-upgrade-summary";
        foreach (Localizer localizer in root.GetComponentsInChildren<Localizer>(true)) { localizer.enabled = false; }
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++) {
            texts[i].enabled = i == labelIndex || i == valueIndex || i == titleIndex;
            texts[i].raycastTarget = false;
        }
        Panel panel = new() {
            Root = root, Title = texts[titleIndex], Labels = texts[labelIndex], Values = texts[valueIndex],
        };
        panel.Title.gameObject.SetActive(true);
        panel.Labels.gameObject.SetActive(true);
        panel.Values.gameObject.SetActive(true);
        panel.Labels.horizontalOverflow = panel.Values.horizontalOverflow = HorizontalWrapMode.Overflow;
        panel.Labels.verticalOverflow = panel.Values.verticalOverflow = VerticalWrapMode.Overflow;
        root.gameObject.SetActive(true);
        return panel;
    }
}
