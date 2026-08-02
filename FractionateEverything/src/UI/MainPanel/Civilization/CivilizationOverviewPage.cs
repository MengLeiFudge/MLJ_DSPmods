using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Civilization.DarkFog;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 汇总残片/记忆源点余额、协议发现/购买资格/正式解锁、检索与深层解析进度、远古科技点，
/// 并在同一页呈现黑雾地表/星域认知、关键证据、首次清剿与 A/B/C 样本解译状态。
/// </summary>
public static class CivilizationOverviewPage {
    private static readonly int[] DarkFogSamples = [IFE黑雾样本A, IFE黑雾样本B, IFE黑雾样本C];
    private static readonly string[] DarkFogSampleLabels = ["A", "B", "C"];

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text economyText;
    private static Text protocolText;
    private static Text retrievalText;
    private static Text deepText;
    private static Text techPointText;
    private static Text bannerText;
    private static Text groundText;
    private static Text spaceText;
    private static Text sampleText;
    private static Text footerText;

    public static void AddTranslations() {
        Register("文明总览", "Civilization Overview");
        Register("文明总览摘要", "Inspect Frag/Memory balances, protocols, retrieval, dark-fog cognition and sample decoding.",
            "查看残片与记忆源点余额、协议发现/购买资格/正式解锁、检索与深层解析、远古科技点，以及黑雾认知与样本解译。");
        Register("文明总览页脚", "When no incomplete eligible protocols remain, broad retrieval becomes deep analysis.",
            "全局没有可行动未完成协议时，广域检索转为深层解析并产出远古文明科技点。");
        Register("总览-经济与协议", "Economy & Protocols");
        Register("总览-黑雾调查", "Dark Fog Investigation");
        Register("总览-残片", "Frag");
        Register("总览-记忆源点", "Memory");
        Register("总览-协议目录", "Catalog");
        Register("总览-已发现", "Discovered");
        Register("总览-购买资格", "Purchase ready");
        Register("总览-正式解锁", "Unlocked");
        Register("总览-检索次数", "Retrievals");
        Register("总览-基础成本", "Base cost");
        Register("总览-失败保底", "Failure streak");
        Register("总览-深层解析", "Deep analysis");
        Register("总览-下次深层点", "Next point");
        Register("总览-科技点", "Tech points");
        Register("总览-可用", "available");
        Register("总览-累计获得", "earned");
        Register("总览-地表认知", "Ground");
        Register("总览-星域认知", "Space");
        Register("总览-地表单位", "Units");
        Register("总览-地表基地", "Bases");
        Register("总览-星域单位", "S.Units");
        Register("总览-蜂巢", "Hives");
        Register("总览-中继", "Relays");
        Register("总览-引火", "Tinders");
        Register("总览-首次清剿行星", "First planet");
        Register("总览-首次清除星系", "First system");
        Register("总览-样本解译", "Samples");
        Register("总览-已解译", "Decoded");
        Register("总览-未解译", "Undecoded");
        Register("总览-已完成", "Done");
        Register("总览-未完成", "Pending");
        Register("总览-和平模式", "Peaceful mode — no dark fog contact yet.",
            "和平模式：尚未接触黑雾，样本解译将在接触后随认知等级提升。");
        Register("总览-接触中", "Contact established.", "调查状态：接触中。");
        Register("总览-等待首次接触", "Awaiting first contact — no dark fog evidence yet.",
            "调查状态：等待首次接触，尚未获取任何黑雾证据。");
        Register("总览-客户端说明", "Client state is synced by the host.",
            "客户端：文明状态由主机同步。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明总览", "文明总览摘要", pos: (0, 0), objectName: "civilization-overview-header",
                        onBuilt: refs => header = refs),
                    Grid(pos: (1, 0), cols: [1, 1], columnGap: PageLayout.Gap,
                        objectName: "civilization-overview-body",
                        children: [
                            ContentCard(pos: (0, 0), objectName: "civilization-overview-economy-card", strong: true,
                                rows: [Px(28f), Px(30f), Px(30f), Px(30f), Px(30f), 1], rowGap: 6f,
                                children: [
                                    CardTitleNode("总览-经济与协议", pos: (0, 0),
                                        objectName: "civilization-overview-economy-title"),
                                    TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true, pos: (1, 0),
                                        onBuilt: text => {
                                            economyText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-economy"),
                                    TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true, pos: (2, 0),
                                        onBuilt: text => {
                                            protocolText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-protocol"),
                                    TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true, pos: (3, 0),
                                        onBuilt: text => {
                                            retrievalText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-retrieval"),
                                    TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true, pos: (4, 0),
                                        onBuilt: text => {
                                            deepText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-deep"),
                                    TextNode("", 13, Gray, anchor: TextAnchor.UpperLeft, wrap: true, pos: (5, 0),
                                        onBuilt: text => {
                                            techPointText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-techpoints"),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "civilization-overview-darkfog-card",
                                rows: [Px(28f), Px(24f), 1, 1, 1], rowGap: 6f,
                                children: [
                                    CardTitleNode("总览-黑雾调查", pos: (0, 0),
                                        objectName: "civilization-overview-darkfog-title"),
                                    TextNode("", 13, Gray, anchor: TextAnchor.MiddleLeft, wrap: false, pos: (1, 0),
                                        onBuilt: text => {
                                            bannerText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-banner"),
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (2, 0),
                                        onBuilt: text => {
                                            groundText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-ground"),
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (3, 0),
                                        onBuilt: text => {
                                            spaceText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-space"),
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (4, 0),
                                        onBuilt: text => {
                                            sampleText = text;
                                            text.supportRichText = true;
                                        }, objectName: "civilization-overview-sample"),
                                ]),
                        ]),
                    FooterCard(pos: (2, 0), objectName: "civilization-overview-footer",
                        children: [
                            TextNode("", 12, Gray, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    footerText = text;
                                    text.supportRichText = true;
                                }, objectName: "civilization-overview-footer-text"),
                        ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "文明总览".Translate().WithColor(Orange);
        header.Summary.text = "文明总览摘要".Translate().WithColor(White);

        ProtocolRetrievalGlobalSnapshot snapshot = ProtocolRetrievalService.GetGlobalSnapshot();
        int total = ProtocolCatalog.All.Count;
        int discovered = ProtocolCatalog.CountDiscovered();
        int eligible = ProtocolCatalog.CountPurchaseEligible();
        int unlocked = ProtocolCatalog.CountFormallyUnlocked();

        economyText.text =
            $"{"总览-残片".Translate()} {snapshot.Fragments.ToString().WithColor(Orange)}    "
            + $"{"总览-记忆源点".Translate()} {snapshot.Memory.ToString().WithColor(Blue)}";
        protocolText.text =
            $"{"总览-协议目录".Translate()}：{"总览-已发现".Translate()} {discovered}/{total}  ·  "
            + $"{"总览-购买资格".Translate()} {eligible}/{total}  ·  "
            + $"{"总览-正式解锁".Translate()} {unlocked}/{total}";
        retrievalText.text =
            $"{"总览-检索次数".Translate()} {snapshot.TotalRetrievalCount}    "
            + $"{"总览-基础成本".Translate()} {snapshot.NextBaseCost}    "
            + $"{"总览-失败保底".Translate()} {snapshot.FailureStreak}/4";
        deepText.text =
            $"{"总览-深层解析".Translate()} {snapshot.DeepAnalysisProgress}/{snapshot.NextDeepPointCost}    "
            + $"{"总览-下次深层点".Translate()} {snapshot.NextDeepPointCost}";

        string techLine =
            $"{"总览-科技点".Translate()}：{AncientTechTreeState.AvailablePoints.ToString().WithColor(Orange)} "
            + $"{"总览-可用".Translate()}  ·  "
            + $"{AncientTechTreeState.TotalPointsEarned.ToString().WithColor(Green)} {"总览-累计获得".Translate()}";
        if (NebulaMultiplayerModAPI.IsClient) {
            techLine += "\n" + "总览-客户端说明".Translate().WithColor(Gray);
        }
        techPointText.text = techLine;

        bannerText.text = BuildBannerLine();
        groundText.text = BuildGroundLine();
        spaceText.text = BuildSpaceLine();
        sampleText.text = BuildSampleLine();

        footerText.text = "文明总览页脚".Translate().WithColor(Gray);
    }

    private static string BuildBannerLine() {
        // 真正的和平模式由游戏战斗开关决定；战斗模式下尚无任何证据/清剿则视为等待首次接触。
        if (!IsCombatModeActive()) {
            return "总览-和平模式".Translate().WithColor(Gray);
        }
        return HasAnyContact()
            ? "总览-接触中".Translate().WithColor(Orange)
            : "总览-等待首次接触".Translate().WithColor(Gray);
    }

    private static bool IsCombatModeActive() {
        // GameLogic.isCombatMode 是实例字段，不可静态访问；通过真实游戏实例入口 null-safe 读取，存档未就绪时按非战斗处理。
        return GameMain.logic?.isCombatMode ?? GameMain.data?.gameDesc?.isCombatMode ?? false;
    }

    private static bool HasAnyContact() {
        return DarkFogInvestigationState.GroundUnits > 0L
               || DarkFogInvestigationState.GroundBases > 0L
               || DarkFogInvestigationState.SpaceUnits > 0L
               || DarkFogInvestigationState.HiveCores > 0L
               || DarkFogInvestigationState.Relays > 0L
               || DarkFogInvestigationState.Tinders > 0L
               || DarkFogInvestigationState.FirstPlanetCleared
               || DarkFogInvestigationState.FirstSystemCleared;
    }

    private static string BuildGroundLine() {
        int rank = DarkFogInvestigationState.GroundRank;
        return new StringBuilder()
            .Append($"{"总览-地表认知".Translate()} {RankLabel("G", rank)}    ")
            .Append($"{"总览-地表单位".Translate()} {DarkFogInvestigationState.GroundUnits}    ")
            .Append($"{"总览-地表基地".Translate()} {DarkFogInvestigationState.GroundBases}    ")
            .Append($"{"总览-首次清剿行星".Translate()} {ClearedLabel(DarkFogInvestigationState.FirstPlanetCleared)}")
            .ToString();
    }

    private static string BuildSpaceLine() {
        int rank = DarkFogInvestigationState.SpaceRank;
        return new StringBuilder()
            .Append($"{"总览-星域认知".Translate()} {RankLabel("S", rank)}    ")
            .Append($"{"总览-星域单位".Translate()} {DarkFogInvestigationState.SpaceUnits}    ")
            .Append($"{"总览-蜂巢".Translate()} {DarkFogInvestigationState.HiveCores}    ")
            .Append($"{"总览-中继".Translate()} {DarkFogInvestigationState.Relays}    ")
            .Append($"{"总览-引火".Translate()} {DarkFogInvestigationState.Tinders}    ")
            .Append($"{"总览-首次清除星系".Translate()} {ClearedLabel(DarkFogInvestigationState.FirstSystemCleared)}")
            .ToString();
    }

    private static string BuildSampleLine() {
        var builder = new StringBuilder();
        builder.Append($"{"总览-样本解译".Translate()}    ");
        for (int i = 0; i < DarkFogSamples.Length; i++) {
            if (i > 0) {
                builder.Append("    ");
            }
            bool decoded = IsSampleDecoded(DarkFogSamples[i]);
            builder.Append($"{DarkFogSampleLabels[i]} {SampleLabel(decoded)}");
        }
        return builder.ToString();
    }

    private static bool IsSampleDecoded(int sampleItemId) {
        AnalysisRecipe recipe = RecipeManager.GetRecipe<AnalysisRecipe>(ERecipe.Analysis, sampleItemId);
        return recipe != null && recipe.TotalSuccessCount > 0;
    }

    private static string RankLabel(string prefix, int rank) {
        Color color = rank switch {
            <= 0 => Gray,
            < 4 => White,
            _ => Orange,
        };
        return $"{prefix}{rank}".WithColor(color);
    }

    private static string ClearedLabel(bool cleared) =>
        (cleared ? "总览-已完成" : "总览-未完成").Translate().WithColor(cleared ? Green : Gray);

    private static string SampleLabel(bool decoded) =>
        (decoded ? "总览-已解译" : "总览-未解译").Translate().WithColor(decoded ? Green : Gray);
}
