using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.View.ResourceInteraction;

public static class MarketBoard {
    private const int RowCount = 6;

    private sealed class OfferRow {
        public MyImageButton InputIcon;
        public Text TxtInput;
        public MyImageButton ExtraIcon;
        public Text TxtExtra;
        public MyImageButton OutputIcon;
        public Text TxtOutput;
        public UIButton BtnTrade;
        public int OfferId;
    }

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtExpire;
    private static Text txtSummary;
    private static Text txtBoardTitle;
    private static readonly OfferRow[] rows = new OfferRow[RowCount];

    public static void AddTranslations() {
        Register("市场板", "Market Board");
        Register("交易", "Trade");
        Register("订单刷新", "Order Refresh");
        Register("特单概览", "Special Orders", "特单概览");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(72f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("市场板", objectName: "market-board-header", pos: (0, 0),
                        onBuilt: refs => {
                            header = refs;
                            txtExpire = refs.Summary;
                        }),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "market-board-summary-card",
                        strong: true,
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("特单概览", onBuilt: text => txtBoardTitle = text,
                                pos: (0, 0), objectName: "market-board-summary-title"),
                            TextNode("", 13, onBuilt: text => txtSummary = text,
                                pos: (1, 0), objectName: "market-board-summary"),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "market-board-list-card",
                        rows: BuildEqualRows(RowCount),
                        rowGap: 6f,
                        children: BuildOfferRowNodes()),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildEqualRows(int count) {
        var tracks = new List<LayoutTrack>();
        for (int i = 0; i < count; i++) {
            tracks.Add(1);
        }

        return tracks;
    }

    private static IReadOnlyList<LayoutNode> BuildOfferRowNodes() {
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < RowCount; i++) {
            int rowIndex = i;
            rows[i] = new OfferRow();
            nodes.Add(Grid(
                pos: (rowIndex, 0),
                cols: [Px(50f), 2, Px(50f), 2, Px(50f), 3, 1],
                columnGap: 10f,
                children: [
                    ImageButtonNode(size: 40f, onBuilt: btn => rows[rowIndex].InputIcon = btn,
                        pos: (0, 0), objectName: $"market-board-input-icon-{rowIndex}"),
                    TextNode("", 13, onBuilt: text => rows[rowIndex].TxtInput = text,
                        pos: (0, 1), objectName: $"market-board-input-text-{rowIndex}"),
                    ImageButtonNode(size: 40f, onBuilt: btn => rows[rowIndex].ExtraIcon = btn,
                        pos: (0, 2), objectName: $"market-board-extra-icon-{rowIndex}"),
                    TextNode("", 13, onBuilt: text => rows[rowIndex].TxtExtra = text,
                        pos: (0, 3), objectName: $"market-board-extra-text-{rowIndex}"),
                    ImageButtonNode(size: 40f, onBuilt: btn => rows[rowIndex].OutputIcon = btn,
                        pos: (0, 4), objectName: $"market-board-output-icon-{rowIndex}"),
                    TextNode("", 13, onBuilt: text => rows[rowIndex].TxtOutput = text,
                        pos: (0, 5), objectName: $"market-board-output-text-{rowIndex}"),
                    ButtonNode("交易", fontSize: 13, onBuilt: btn => rows[rowIndex].BtnTrade = btn,
                        onClick: () => {
                            if (rows[rowIndex].OfferId > 0
                                && MarketBoardManager.TryExecuteOffer(rows[rowIndex].OfferId)) {
                                UpdateUI();
                            }
                        },
                        pos: (0, 6), objectName: $"market-board-trade-{rowIndex}"),
                ]));
        }

        return nodes;
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        long ticks = MarketBoardManager.CurrentExpireTick - GameMain.gameTick;
        if (ticks < 0) {
            ticks = 0;
        }
        header.Title.text = "市场板".Translate().WithColor(Orange);
        txtExpire.text = $"{"订单刷新".Translate()}：{FormatTicks(ticks)}";
        txtBoardTitle.text = "特单概览".Translate().WithColor(Orange);
        int specialCount =
            MarketBoardManager.ActiveOffers.Count(offer =>
                offer.OfferType == MarketBoardManager.MarketOfferType.Special);
        int darkFogSpecialCount = MarketBoardManager.ActiveOffers.Count(offer =>
            offer.OfferType == MarketBoardManager.MarketOfferType.Special
            && DarkFogCombatManager.IsDarkFogOffer(offer));
        string stageName = DarkFogCombatManager.GetCurrentStage() switch {
            EDarkFogCombatStage.Dormant => "休眠观察".WithColor(Orange),
            EDarkFogCombatStage.Signal => "信号接触".WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => "地面压制".WithColor(Green),
            EDarkFogCombatStage.StellarHunt => "星域围猎".WithColor(Blue),
            _ => "奇点收束".WithColor(Gold),
        };
        txtSummary.text = $"{"特单概览".Translate()}：特单 {specialCount} 条    黑雾相关 {darkFogSpecialCount} 条    阶段 {stageName}";

        var offers = MarketBoardManager.ActiveOffers;
        for (int i = 0; i < rows.Length; i++) {
            if (i >= offers.Count) {
                SetRowHidden(rows[i]);
                continue;
            }

            var offer = offers[i];
            rows[i].OfferId = offer.OfferId;
            SetItem(rows[i].InputIcon, rows[i].TxtInput, offer.InputItemId, offer.InputCount);
            if (offer.ExtraInputItemId > 0) {
                SetItem(rows[i].ExtraIcon, rows[i].TxtExtra, offer.ExtraInputItemId, offer.ExtraInputCount);
            } else {
                rows[i].ExtraIcon.gameObject.SetActive(false);
                rows[i].TxtExtra.text = "";
            }
            SetItem(rows[i].OutputIcon, rows[i].TxtOutput, offer.OutputItemId, offer.OutputCount);
            if (DarkFogCombatManager.IsDarkFogOffer(offer)
                && !DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputItemId)) {
                rows[i].TxtOutput.text = $"{LDB.items.Select(offer.OutputItemId).name} 配方成长 +{offer.OutputCount}";
            }
            string offerTag = GetOfferTag(offer);
            if (!string.IsNullOrEmpty(offerTag) && !string.IsNullOrEmpty(rows[i].TxtOutput.text)) {
                rows[i].TxtOutput.text = $"{offerTag} {rows[i].TxtOutput.text}";
            }
            rows[i].BtnTrade.gameObject.SetActive(true);
            rows[i].BtnTrade.button.interactable = true;
        }
    }

    private static string GetOfferTag(MarketBoardManager.MarketOffer offer) {
        if (DarkFogCombatManager.IsEnhancedDarkFogOffer(offer)) {
            return "[黑雾增强]".WithColor(Gold);
        }
        if (DarkFogCombatManager.IsDarkFogOffer(offer)) {
            return "[黑雾特单]".WithColor(Blue);
        }
        return offer.OfferType == MarketBoardManager.MarketOfferType.Special
            ? "[特单]".WithColor(Orange)
            : string.Empty;
    }

    private static void SetItem(MyImageButton icon, Text text, int itemId, int count) {
        icon.gameObject.SetActive(itemId > 0);
        icon.Proto = itemId > 0 ? LDB.items.Select(itemId) : null;
        if (itemId > 0) {
            icon.SetCount(count);
            text.text = $"{LDB.items.Select(itemId).name} x{count}";
        } else {
            icon.ClearCountText();
            text.text = "";
        }
    }

    private static void SetRowHidden(OfferRow row) {
        row.OfferId = 0;
        row.InputIcon.gameObject.SetActive(false);
        row.ExtraIcon.gameObject.SetActive(false);
        row.OutputIcon.gameObject.SetActive(false);
        row.TxtInput.text = "";
        row.TxtExtra.text = "";
        row.TxtOutput.text = "";
        row.BtnTrade.gameObject.SetActive(false);
    }

    private static string FormatTicks(long ticks) {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(ticks / 60f));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
