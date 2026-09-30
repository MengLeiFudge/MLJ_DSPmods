using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 展示探索信号、双尺度强度读数，以及玩家主动执行的固定和位置标注操作。
/// </summary>
public static class ExplorationPage {
    private const float MarkLineHeight = 26f;
    private static readonly MethodInfo SetLocationTrackingMethod = AccessTools.Method(
        typeof(UIGlobemap), "SetLocationTracking", [typeof(int), typeof(Vector3), typeof(int)]);

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text signalText;
    private static Text markListText;
    private static RectTransform markListRoot;
    private static Text markDetailText;
    private static Text footerText;
    private static UIButton cycleButton;
    private static UIButton pinButton;
    private static UIButton trackButton;
    private static int selectedMarkId;

    /// <summary>
    /// 注册探索页面文本。
    /// </summary>
    public static void AddTranslations() {
        Register("遗物探索", "Relic Exploration");
        Register("遗物探索摘要",
            "Compare directionless detector strengths to locate permanent old-civilization signals.",
            "比较无方向的探测强度，定位永久存在的探索信号。");
        Register("信号网络", "Signal Network");
        Register("未探索信号", "Unexplored Signals");
        Register("信号详情", "Signal Detail");
        Register("切换信号", "Cycle Signal");
        Register("信号增幅等级", "Signal Amplification Level");
        Register("显示精度", "Display Precision");
        Register("遗物探测器", "Relic Detector");
        Register("星球探测器", "Planet Detector");
        Register("遗物", "Relic");
        Register("星球", "Planet");
        Register("下次定时信号", "Next Timed Signal");
        Register("尚未建立", "Not Established");
        Register("未恢复", "Not Restored");
        Register("已恢复", "Restored");
        Register("目标星球", "Target Planet");
        Register("未知广播", "Unknown Broadcast");
        Register("当前没有未探索信号", "No unexplored signals are currently active.");
        Register("首个遗物状态", "First Relic");
        Register("已回收", "Recovered");
        Register("待回收", "Pending");
        Register("主线固定", "Storyline Pin");
        Register("固定", "Pin");
        Register("取消固定", "Unpin");
        Register("标注位置", "Track Location");
        Register("探索页脚",
            "Signals never expire. Their target, contents, and exact position remain hidden unless the storyline explicitly reveals them; approach within 120 metres to recover one automatically.",
            "信号不会过期；除主线明确公开的信息外，目标、内容与具体位置保持隐藏，接近至 120 米内时自动回收。");
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
                                    Grid(pos: (2, 0), cols: [1, 1, 1], columnGap: 8f,
                                        objectName: "exploration-actions", children: [
                                            ButtonNode("切换信号", CycleMark, pos: (0, 0),
                                                onBuilt: button => cycleButton = button,
                                                objectName: "exploration-cycle-mark"),
                                            ButtonNode("固定", ToggleSelectedMarkPin, pos: (0, 1),
                                                onBuilt: button => pinButton = button,
                                                objectName: "exploration-pin-mark"),
                                            ButtonNode("标注位置", TrackSelectedMark, pos: (0, 2),
                                                onBuilt: button => trackButton = button,
                                                objectName: "exploration-track-mark"),
                                        ]),
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
    /// 刷新主机同步后的点位快照和双探测器强度。
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
        UpdateActionButtons(marks);

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
        string planetDetectorState = (ExplorationManager.PlanetDetectorUnlocked ? "已恢复" : "未恢复").Translate();
        string precision = FormatPercent(GrowthTechnologyManager.ExplorationSignalPrecisionPercent);
        return $"{"信号增幅等级".Translate()}：Lv.{GrowthTechnologyManager.ExplorationLevel}    "
               + $"{"显示精度".Translate()}：{precision.WithColor(Blue)}    "
               + $"{"下次定时信号".Translate()}：{countdown.WithColor(Blue)}\n"
               + $"{"未探索信号".Translate()}：{markCount.ToString().WithColor(Orange)}    "
               + $"{"首个遗物状态".Translate()}：{relicState}    "
               + $"{"星球探测器".Translate()}：{planetDetectorState}";
    }

    private static string BuildMarkList(ExplorationMark[] marks) {
        if (marks.Length == 0) {
            return "当前没有未探索信号".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        foreach (ExplorationMark mark in marks.OrderBy(mark => mark.Id)) {
            string marker = mark.Id == selectedMarkId ? ">".WithColor(Orange) : " ";
            builder.Append(marker).Append(' ')
                .Append(ExplorationManager.GetSignalName(mark));
            if (ExplorationManager.IsKeySignal(mark)) {
                builder.Append("  ·  ").Append(GetPlanetName(mark.PlanetId));
            }
            if (ExplorationSignalHud.IsPinned(mark.Id)) {
                builder.Append("  ·  ").Append("固定".Translate());
            }
            builder.Append('\n');
        }
        return builder.ToString();
    }

    private static string BuildMarkDetail(ExplorationMark[] marks) {
        ExplorationMark mark = marks.FirstOrDefault(candidate => candidate.Id == selectedMarkId);
        if (mark == null) {
            return "当前没有未探索信号".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        builder.Append(ExplorationManager.GetSignalName(mark).WithColor(Orange)).Append("\n\n");
        if (ExplorationManager.IsKeySignal(mark)) {
            builder.Append("目标星球".Translate()).Append("：")
                .Append(GetPlanetName(mark.PlanetId)).Append('\n');
        } else {
            builder.Append("未知广播".Translate().WithColor(Gray)).Append('\n');
        }

        Player player = GameMain.mainPlayer;
        if (player != null && ExplorationManager.TryGetDetectorStrengths(mark, player.uPosition,
                out float planetStrength, out float relicStrength)) {
            if (ExplorationManager.PlanetDetectorUnlocked) {
                builder.Append("星球探测器".Translate()).Append("：")
                    .Append(FormatPercent(planetStrength * 100f).WithColor(Blue)).Append('\n');
            }
            builder.Append("遗物探测器".Translate()).Append("：")
                .Append(FormatPercent(relicStrength * 100f).WithColor(Blue));
        }
        return builder.ToString();
    }

    private static void CycleMark() {
        ExplorationMark[] marks = ExplorationManager.GetActiveMarks()
            .OrderBy(mark => mark.Id)
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
            selectedMarkId = marks.OrderBy(mark => mark.Id).First().Id;
        }
    }

    private static void UpdateActionButtons(ExplorationMark[] marks) {
        ExplorationMark selected = marks.FirstOrDefault(mark => mark.Id == selectedMarkId);
        cycleButton.button.interactable = marks.Length > 1;

        bool keySignal = ExplorationManager.IsKeySignal(selected);
        bool pinned = selected != null && ExplorationSignalHud.IsPinned(selected.Id);
        string pinLabel = keySignal ? "主线固定" : pinned ? "取消固定" : "固定";
        SetButtonText(pinButton, pinLabel);
        pinButton.button.interactable = selected != null && !keySignal
            && (pinned || ExplorationSignalHud.HasAvailableOrdinarySlot);

        trackButton.gameObject.SetActive(GrowthTechnologyManager.ExplorationLocationTrackingUnlocked);
        trackButton.button.interactable = selected != null;
    }

    private static void ToggleSelectedMarkPin() {
        if (selectedMarkId > 0 && ExplorationSignalHud.TogglePinned(selectedMarkId)) {
            UpdateUI();
        }
    }

    private static void TrackSelectedMark() {
        if (!GrowthTechnologyManager.ExplorationLocationTrackingUnlocked) {
            return;
        }
        ExplorationMark mark = ExplorationManager.GetActiveMarks()
            .FirstOrDefault(candidate => candidate.Id == selectedMarkId);
        if (mark == null) {
            return;
        }
        UIGlobemap globemap = UIRoot.instance?.uiGame?.globemap;
        if (globemap == null || SetLocationTrackingMethod == null) {
            LogError("无法调用原版位置标注入口 UIGlobemap.SetLocationTracking。");
            return;
        }
        SetLocationTrackingMethod.Invoke(globemap, [mark.PlanetId, mark.LocalPosition, 519]);
    }

    private static void SetButtonText(UIButton button, string text) {
        Transform textNode = button?.transform.Find("button-text");
        if (textNode == null) {
            return;
        }
        Localizer localizer = textNode.GetComponent<Localizer>();
        if (localizer != null) {
            localizer.stringKey = text;
            localizer.translation = text.Translate();
        }
        Text label = textNode.GetComponent<Text>();
        if (label != null) {
            label.text = text.Translate();
        }
    }

    private static string FormatPercent(float percent) {
        string format = GrowthTechnologyManager.ExplorationSignalPrecisionPercent < 1f ? "0.0" : "0";
        return $"{percent.ToString(format, CultureInfo.InvariantCulture)}%";
    }

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
