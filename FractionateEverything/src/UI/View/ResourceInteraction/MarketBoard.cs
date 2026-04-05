using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
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
    private static Text txtExpire;
    private static Text txtSummary;
    private static readonly OfferRow[] rows = new OfferRow[RowCount];

    public static void AddTranslations() {
        Register("市场板", "Market Board");
        Register("交易", "Trade");
        Register("订单刷新", "Order Refresh");
        Register("特单概览", "Special Orders", "特单概览");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "市场板");
        float y = 18f;
        txtExpire = wnd.AddText2(0f, y, tab, "", 13);
        y += 28f;
        txtSummary = wnd.AddText2(0f, y, tab, "", 13);
        txtSummary.supportRichText = true;
        txtSummary.rectTransform.sizeDelta = new Vector2(960f, 24f);
        y += 36f;

        for (int i = 0; i < RowCount; i++) {
            int rowIndex = i;
            rows[i] = new OfferRow {
                InputIcon = wnd.AddImageButton(0f, y, tab, null).WithSize(40f, 40f),
                TxtInput = wnd.AddText2(50f, y, tab, "", 13),
                ExtraIcon = wnd.AddImageButton(250f, y, tab, null).WithSize(40f, 40f),
                TxtExtra = wnd.AddText2(300f, y, tab, "", 13),
                OutputIcon = wnd.AddImageButton(520f, y, tab, null).WithSize(40f, 40f),
                TxtOutput = wnd.AddText2(570f, y, tab, "", 13),
            };
            rows[i].TxtInput.rectTransform.sizeDelta = new Vector2(180f, 24f);
            rows[i].TxtExtra.rectTransform.sizeDelta = new Vector2(180f, 24f);
            rows[i].TxtOutput.rectTransform.sizeDelta = new Vector2(180f, 24f);
            rows[i].BtnTrade = wnd.AddButton(820f, y - 4f, 120f, tab, "交易", 13,
                onClick: () => {
                    if (rows[rowIndex].OfferId > 0 && MarketBoardManager.TryExecuteOffer(rows[rowIndex].OfferId)) {
                        UpdateUI();
                    }
                });
            y += 40f;
        }
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        long ticks = MarketBoardManager.CurrentExpireTick - GameMain.gameTick;
        if (ticks < 0) {
            ticks = 0;
        }
        txtExpire.text = $"{ "订单刷新".Translate() }：{FormatTicks(ticks)}";
        int specialCount = MarketBoardManager.ActiveOffers.Count(offer => offer.OfferType == MarketBoardManager.MarketOfferType.Special);
        int darkFogSpecialCount = MarketBoardManager.ActiveOffers.Count(offer =>
            offer.OfferType == MarketBoardManager.MarketOfferType.Special && DarkFogCombatManager.IsDarkFogOffer(offer));
        string stageName = DarkFogCombatManager.GetCurrentStage() switch {
            EDarkFogCombatStage.Dormant => "休眠观察".WithColor(Orange),
            EDarkFogCombatStage.Signal => "信号接触".WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => "地面压制".WithColor(Green),
            EDarkFogCombatStage.StellarHunt => "星域围猎".WithColor(Blue),
            _ => "奇点收束".WithColor(Gold),
        };
        txtSummary.text = $"{ "特单概览".Translate() }：特单 {specialCount} 条    黑雾相关 {darkFogSpecialCount} 条    阶段 {stageName}";

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
