using System;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 展示 FE 3.0 永久探索信号，并提供不含方向提示的现场信号强度读数。
/// </summary>
public static class ExplorationPage {
    private const float MarkLineHeight = 26f;
    private const float ClaimDistance = 120f;

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text signalText;
    private static Text markListText;
    private static RectTransform markListRoot;
    private static Text markDetailText;
    private static Text footerText;
    private static UIButton cycleButton;
    private static int selectedMarkId;

    /// <summary>
    /// 注册探索页面文本。
    /// </summary>
    public static void AddTranslations() {
        Register("遗物探索", "Relic Exploration");
        Register("遗物探索摘要",
            "Track permanent old-civilization signals, travel to their target planets, and search by signal strength.",
            "追踪永久存在的古文明信号，抵达目标星球后依据信号强度搜索。");
        Register("信号网络", "Signal Network");
        Register("未探索信号", "Unexplored Signals");
        Register("信号详情", "Signal Detail");
        Register("切换信号", "Cycle Signal");
        Register("信号增幅等级", "Signal Amplification Level");
        Register("现场信号强度", "Local Signal Strength");
        Register("下次定时信号", "Next Timed Signal");
        Register("尚未建立", "Not Established");
        Register("首个遗物", "First Relic");
        Register("古文明信号", "Old-Civilization Signal");
        Register("解析资格信号", "Research Eligibility");
        Register("资源遗存", "Resource Cache");
        Register("目标星球", "Target Planet");
        Register("尚未抵达目标星球", "Land on the target planet to read signal strength.",
            "抵达目标星球后才能读取现场信号强度。");
        Register("当前没有未探索信号", "No unexplored signals are currently active.");
        Register("首个遗物状态", "First Relic");
        Register("已回收", "Recovered");
        Register("待回收", "Pending");
        Register("探索页脚",
            "Signals never expire. Approach within 120 metres to recover the relic, eligibility, or resource cache automatically.",
            "信号不会过期；接近至 120 米内时，会自动回收遗物、解析资格或资源遗存。");
        Register("探索客户端说明", "Arrival is verified and settled by the host.",
            "抵达状态由主机验证并结算。");
    }

    /// <summary>
    /// 探索页面没有独立配置。
    /// </summary>
    public static void LoadConfig(ConfigFile configFile) { }

    /// <summary>
    /// 创建信号网络、点位列表和现场强度详情区域。
    /// </summary>
    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        int markCount = Math.Max(1, ExplorationManager.GetActiveMarks().Length);
        float markContentHeight = Math.Max(360f, markCount * MarkLineHeight + 20f);

        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(112f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("遗物探索", "遗物探索摘要", pos: (0, 0), objectName: "exploration-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "exploration-signal-card", strong: true,
                        rows: [Px(28f), 1], rowGap: 6f, children: [
                            CardTitleNode("信号网络", pos: (0, 0), objectName: "exploration-signal-title"),
                            TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true,
                                pos: (1, 0), onBuilt: text => {
                                    signalText = text;
                                    text.supportRichText = true;
                                }, objectName: "exploration-signal-text"),
                        ]),
                    Grid(pos: (2, 0), cols: [1, 1], columnGap: PageLayout.Gap,
                        objectName: "exploration-body", children: [
                            ContentCard(pos: (0, 0), objectName: "exploration-mark-list-card", strong: true,
                                rows: [Px(28f), 1], rowGap: 8f, children: [
                                    CardTitleNode("未探索信号", pos: (0, 0),
                                        objectName: "exploration-mark-list-title"),
                                    ScrollableContentCard(markContentHeight, pos: (1, 0),
                                        objectName: "exploration-mark-list-scroll",
                                        onBuilt: root => markListRoot = root, children: [
                                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                                pos: (0, 0), onBuilt: text => {
                                                    markListText = text;
                                                    text.supportRichText = true;
                                                }, objectName: "exploration-mark-list"),
                                        ]),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "exploration-mark-detail-card",
                                rows: [Px(28f), 1, Px(34f)], rowGap: 8f, children: [
                                    CardTitleNode("信号详情", pos: (0, 0),
                                        objectName: "exploration-mark-detail-title"),
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                        pos: (1, 0), onBuilt: text => {
                                            markDetailText = text;
                                            text.supportRichText = true;
                                        }, objectName: "exploration-mark-detail"),
                                    ButtonNode("切换信号", CycleMark, pos: (2, 0),
                                        onBuilt: button => cycleButton = button,
                                        objectName: "exploration-cycle-mark"),
                                ]),
                        ]),
                    FooterCard(pos: (3, 0), objectName: "exploration-footer", children: [
                        TextNode("", 12, Gray, wrap: true, pos: (0, 0), onBuilt: text => {
                            footerText = text;
                            text.supportRichText = true;
                        }, objectName: "exploration-footer-text"),
                    ]),
                ]));
        UpdateUI();
    }

    /// <summary>
    /// 刷新主机同步后的点位快照和现场信号强度。
    /// </summary>
    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        ExplorationMark[] marks = ExplorationManager.GetActiveMarks();
        SynchronizeSelectedMark(marks);
        header.Title.text = "遗物探索".Translate().WithColor(Orange);
        header.Summary.text = "遗物探索摘要".Translate().WithColor(White);
        signalText.text = BuildSignalText(marks.Length);
        markListText.text = BuildMarkList(marks);
        ResizeMarkList();
        markDetailText.text = BuildMarkDetail(marks);
        cycleButton.button.interactable = marks.Length > 1;

        string footer = "探索页脚".Translate();
        if (NebulaMultiplayerModAPI.IsClient) {
            footer += "  ·  " + "探索客户端说明".Translate();
        }
        footerText.text = footer.WithColor(Gray);
    }

    private static string BuildSignalText(int markCount) {
        long remainingTicks = ExplorationManager.GetTicksUntilNextSignal(GameMain.gameTick);
        string countdown = remainingTicks < 0
            ? "尚未建立".Translate()
            : FormatCountdown(remainingTicks);
        string relicState = (ExplorationManager.FirstRelicClaimed ? "已回收" : "待回收").Translate();
        return $"{"信号增幅等级".Translate()}：Lv.{GrowthTechnologyManager.ExplorationLevel}    "
               + $"{"下次定时信号".Translate()}：{countdown.WithColor(Blue)}\n"
               + $"{"未探索信号".Translate()}：{markCount.ToString().WithColor(Orange)}    "
               + $"{"首个遗物状态".Translate()}：{relicState}";
    }

    private static string BuildMarkList(ExplorationMark[] marks) {
        if (marks.Length == 0) {
            return "当前没有未探索信号".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        foreach (ExplorationMark mark in marks.OrderBy(mark => mark.PlanetId).ThenBy(mark => mark.Id)) {
            string marker = mark.Id == selectedMarkId ? ">".WithColor(Orange) : " ";
            builder.Append(marker).Append(' ')
                .Append(GetKindName(mark.Kind))
                .Append("  ·  ").Append(GetPlanetName(mark.PlanetId))
                .Append("  #").Append(mark.Id).Append('\n');
        }
        return builder.ToString();
    }

    private static string BuildMarkDetail(ExplorationMark[] marks) {
        ExplorationMark mark = marks.FirstOrDefault(candidate => candidate.Id == selectedMarkId);
        if (mark == null) {
            return "当前没有未探索信号".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        builder.Append(GetKindName(mark.Kind).WithColor(Orange)).Append("  #").Append(mark.Id).Append("\n\n")
            .Append("目标星球".Translate()).Append("：").Append(GetPlanetName(mark.PlanetId)).Append('\n');

        Player player = GameMain.mainPlayer;
        if (player != null && player.planetId == mark.PlanetId) {
            PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
            float maximumDistance = Math.Max(ClaimDistance + 1f, (planet?.realRadius ?? 200f) * 2f);
            float distance = Vector3.Distance(player.position, mark.LocalPosition);
            float strength = 1f - Mathf.Clamp01((distance - ClaimDistance) / (maximumDistance - ClaimDistance));
            builder.Append("现场信号强度".Translate()).Append("：")
                .Append($"{Mathf.RoundToInt(strength * 100f)}%".WithColor(Blue));
        } else {
            builder.Append("尚未抵达目标星球".Translate().WithColor(Gray));
        }
        return builder.ToString();
    }

    private static void CycleMark() {
        ExplorationMark[] marks = ExplorationManager.GetActiveMarks()
            .OrderBy(mark => mark.PlanetId)
            .ThenBy(mark => mark.Id)
            .ToArray();
        if (marks.Length == 0) {
            return;
        }
        int index = Array.FindIndex(marks, mark => mark.Id == selectedMarkId);
        selectedMarkId = marks[(index + 1) % marks.Length].Id;
        UpdateUI();
    }

    private static void SynchronizeSelectedMark(ExplorationMark[] marks) {
        if (marks.Length == 0) {
            selectedMarkId = 0;
            return;
        }
        if (marks.All(mark => mark.Id != selectedMarkId)) {
            selectedMarkId = marks.OrderBy(mark => mark.PlanetId).ThenBy(mark => mark.Id).First().Id;
        }
    }

    private static string GetKindName(ExplorationMarkKind kind) => kind switch {
        ExplorationMarkKind.FirstRelic => "首个遗物".Translate(),
        ExplorationMarkKind.DirectionEligibility or ExplorationMarkKind.ResourceCache => "古文明信号".Translate(),
        _ => kind.ToString(),
    };

    private static void ResizeMarkList() {
        if (markListRoot == null || markListText == null) {
            return;
        }
        RectTransform viewport = markListRoot.parent as RectTransform;
        float height = Math.Max(viewport?.rect.height ?? 0f, markListText.preferredHeight + 20f);
        markListRoot.sizeDelta = new Vector2(markListRoot.sizeDelta.x, height);
        if (markListText.rectTransform.parent is RectTransform textContainer) {
            float textHeight = Math.Max(0f, height - 20f);
            textContainer.sizeDelta = new Vector2(textContainer.sizeDelta.x, textHeight);
            markListText.rectTransform.sizeDelta = new Vector2(markListText.rectTransform.sizeDelta.x, textHeight);
        }
    }

    private static string GetPlanetName(int planetId) =>
        GameMain.galaxy?.PlanetById(planetId)?.displayName ?? planetId.ToString();

    private static string FormatCountdown(long ticks) {
        long seconds = Math.Max(0L, ticks / 60L);
        long hours = seconds / 3600L;
        long minutes = seconds % 3600L / 60L;
        long remainder = seconds % 60L;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{remainder:00}"
            : $"{minutes:00}:{remainder:00}";
    }
}
