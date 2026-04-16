using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.DrawGrowth;

/// <summary>
/// 成长规划 / 流派聚焦页。
/// 成长池承担确定性补差，聚焦页只负责方向偏置和成长报价修正，不再伪装成独立抽卡池。
/// </summary>
public static class LimitedTimeStore {
    private const int GrowthRowCount = 8;

    private sealed class GrowthRowUi {
        public bool HasOffer;
        public GachaGrowthOffer Offer;
        public MyImageButton BtnFragmentCostIcon;
        public Text TxtCost;
        public MyImageButton BtnExtraCostIcon;
        public Text TxtExtraCost;
        public MyImageButton BtnRewardIcon;
        public Text TxtReward;
        public Text TxtDetail;
        public UIButton BtnExchange;
    }

    private sealed class GrowthPageUi {
        public RectTransform Tab;
        public PageLayout.HeaderRefs Header;
        public Text TxtResource;
        public Text TxtResourceTitle;
        public Text TxtOfferTitle;
        public MyImageButton BtnMatrixIcon;
        public MyImageButton BtnFragmentIcon;
        public Text TxtFocus;
        public readonly List<GrowthRowUi> Rows = [];
    }

    private sealed class FocusPageUi {
        public RectTransform Tab;
        public PageLayout.HeaderRefs Header;
        public Text TxtCurrentFocus;
        public Text TxtOverview;
        public Text TxtCurrentFocusTitle;
        public Text TxtFocusListTitle;
        public readonly List<UIButton> Buttons = [];
        public readonly List<Text> DescTexts = [];
    }

    private static GrowthPageUi growthPage;
    private static FocusPageUi focusPage;

    public static void AddTranslations() {
        Register("成长规划", "Growth Planning");
        Register("前往抽取", "Go Draw");
        Register("当前聚焦", "Current Focus");
        Register("切换聚焦", "Switch Focus");
        Register("已生效", "Active");
        Register("成长定向", "Growth Bias");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans);
    public static void CreateProtoUI(MyWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans);
    public static void CreateUpUI(MyWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans);
    public static void CreateLimitedUI(MyWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans);

    private static void CreateGrowthUI(MyWindow wnd, RectTransform trans) {
        growthPage = new GrowthPageUi {
            Tab = trans
        };

        growthPage.Header = PageLayout.CreatePageHeader(wnd, growthPage.Tab, "成长规划", "", "growth-store-header");
        float top = PageLayout.HeaderHeight + PageLayout.Gap;
        RectTransform resourceCard = PageLayout.CreateContentCard(growthPage.Tab, "growth-store-resource-card", 0f,
            top, PageLayout.DesignWidth, 180f, true);
        RectTransform offerCard = PageLayout.CreateContentCard(growthPage.Tab, "growth-store-offer-card", 0f,
            top + 180f + PageLayout.Gap, PageLayout.DesignWidth, 397f);
        RectTransform footerCard = PageLayout.CreateFooterCard(growthPage.Tab, "growth-store-footer-card",
            top + 180f + PageLayout.Gap + 397f + PageLayout.Gap);

        growthPage.TxtResourceTitle = PageLayout.AddCardTitle(wnd, resourceCard, 18f, 14f, "当前资源", 15,
            "growth-store-resource-title");
        growthPage.TxtOfferTitle = PageLayout.AddCardTitle(wnd, offerCard, 18f, 14f, "成长定向", 15,
            "growth-store-offer-title");

        float y = 48f;
        growthPage.TxtResource = MyWindow.AddText(18f, y, resourceCard, "当前资源".Translate(), 13);
        growthPage.TxtResource.rectTransform.sizeDelta = new Vector2(1028f, 22f);
        y += 28f;
        growthPage.BtnMatrixIcon = MyImageButton.CreateImageButton(18f, y, resourceCard, null).WithSize(40f, 40f);
        growthPage.BtnFragmentIcon = MyImageButton.CreateImageButton(198f, y, resourceCard, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
        y += 48f;
        growthPage.TxtFocus = MyWindow.AddText(18f, y, resourceCard, "", 13);
        growthPage.TxtFocus.rectTransform.sizeDelta = new Vector2(1028f, 40f);
        y = 52f;

        wnd.AddButton(872f, 10f, 150f, footerCard, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0));

        for (int i = 0; i < GrowthRowCount; i++) {
            var row = new GrowthRowUi {
                BtnFragmentCostIcon = MyImageButton.CreateImageButton(128f, y, offerCard, LDB.items.Select(IFE残片)).WithSize(40f, 40f),
                TxtCost = MyWindow.AddText(18f, y, offerCard, "", 13),
                BtnExtraCostIcon = MyImageButton.CreateImageButton(258f, y, offerCard, null).WithSize(40f, 40f),
                TxtExtraCost = MyWindow.AddText(300f, y, offerCard, "", 13),
                BtnRewardIcon = MyImageButton.CreateImageButton(472f, y, offerCard, null).WithSize(40f, 40f),
                TxtReward = MyWindow.AddText(514f, y, offerCard, "", 13),
                TxtDetail = MyWindow.AddText(514f, y + 18f, offerCard, "", 12),
            };
            row.BtnExchange = wnd.AddButton(890f, y - 4f, 120f, offerCard, "兑换".Translate(), 13,
                onClick: () => ExchangeOffer(row));
            row.TxtCost.rectTransform.sizeDelta = new Vector2(100f, 24f);
            row.TxtExtraCost.rectTransform.sizeDelta = new Vector2(120f, 24f);
            row.TxtReward.rectTransform.sizeDelta = new Vector2(280f, 24f);
            row.TxtDetail.rectTransform.sizeDelta = new Vector2(280f, 20f);
            growthPage.Rows.Add(row);
            y += 42f;
        }
    }

    private static void CreateFocusUI(MyWindow wnd, RectTransform trans) {
        focusPage = new FocusPageUi {
            Tab = trans
        };

        focusPage.Header = PageLayout.CreatePageHeader(wnd, focusPage.Tab, "流派聚焦", "", "focus-store-header");
        float top = PageLayout.HeaderHeight + PageLayout.Gap;
        RectTransform currentCard = PageLayout.CreateContentCard(focusPage.Tab, "focus-store-current-card", 0f, top,
            PageLayout.DesignWidth, 140f, true);
        RectTransform listCard = PageLayout.CreateContentCard(focusPage.Tab, "focus-store-list-card", 0f,
            top + 140f + PageLayout.Gap, PageLayout.DesignWidth, 421f);
        RectTransform footerCard = PageLayout.CreateFooterCard(focusPage.Tab, "focus-store-footer-card",
            top + 140f + PageLayout.Gap + 421f + PageLayout.Gap);

        focusPage.TxtCurrentFocusTitle = PageLayout.AddCardTitle(wnd, currentCard, 18f, 14f, "当前聚焦", 15,
            "focus-store-current-title");
        focusPage.TxtFocusListTitle = PageLayout.AddCardTitle(wnd, listCard, 18f, 14f, "切换聚焦", 15,
            "focus-store-list-title");

        float y = 48f;
        focusPage.TxtCurrentFocus = MyWindow.AddText(18f, y, currentCard, "", 14);
        focusPage.TxtCurrentFocus.rectTransform.sizeDelta = new Vector2(1028f, 24f);
        y += 30f;
        focusPage.TxtOverview = MyWindow.AddText(18f, y, currentCard, "", 13);
        focusPage.TxtOverview.rectTransform.sizeDelta = new Vector2(1028f, 40f);
        y = 52f;

        wnd.AddButton(872f, 10f, 150f, footerCard, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0));

        foreach (var focus in GachaService.FocusDefinitions) {
            float currentY = y;
            var button = wnd.AddButton(18f, currentY, 280f, listCard, "", 13,
                onClick: () => ChangeFocus(focus.FocusType));
            Text desc = MyWindow.AddText(318f, currentY + 4f, listCard,
                $"{focus.NameKey.Translate()}：{focus.DescKey.Translate()}", 13)
                ;
            desc.rectTransform.sizeDelta = new Vector2(704f, 40f);
            focusPage.Buttons.Add(button);
            focusPage.DescTexts.Add(desc);
            y += 48f;
        }
    }

    private static void ExchangeOffer(GrowthRowUi row) {
        if (!row.HasOffer) {
            return;
        }

        GachaGrowthOffer offer = row.Offer;
        if (!GachaService.TryExchangeGrowthOffer(offer, out GachaRewardResolution reward)) {
            return;
        }

        string itemName = LDB.items.Select(offer.OutputId)?.name ?? offer.OutputId.ToString();
        if (GachaService.IsDarkFogCatchupOffer(offer)) {
            UIRealtimeTip.Popup(reward.RewardCount > 0
                ? $"对应 {itemName} 黑雾配方成长 +{reward.RewardCount}"
                : $"对应 {itemName} 黑雾配方暂未推进");
        } else if (GachaService.IsDarkFogRecipeGrowthOffer(offer)) {
            string message = reward.RewardType switch {
                GachaRewardType.RecipeUnlock => $"{itemName} 转化配方已解锁，当前 Lv{reward.RewardCount}",
                GachaRewardType.RecipeUpgrade => $"{itemName} 转化配方提升到 Lv{reward.RewardCount}",
                GachaRewardType.DuplicateRecipeFragments => $"{itemName} 转化配方已满级，转化为残片 x{reward.RewardCount}",
                _ => $"{itemName} 转化配方暂未推进",
            };
            UIRealtimeTip.Popup(message);
        } else {
            UIRealtimeTip.Popup($"获得 {itemName} x{reward.RewardCount}");
        }
        UpdateUI();
    }

    private static void ChangeFocus(GachaFocusType focusType) {
        if (!GachaService.TryChangeFocus(focusType)) {
            return;
        }

        UpdateUI();
    }

    public static void UpdateUI() {
        int matrixId = GachaService.GetCurrentDrawMatrixId();
        string resourceText = $"成长积分 x{GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth)}    黑雾矩阵 x{GetItemTotalCount(I黑雾矩阵)}";
        string focusText = $"{"当前模式".Translate()}：{GachaService.GetModeNameKey().Translate()}    {"当前聚焦".Translate()}：{GetCurrentFocusName()}";

        if (growthPage?.Tab != null && growthPage.Tab.gameObject.activeSelf) {
            growthPage.Header.Title.text = "成长规划".Translate().WithColor(Orange);
            growthPage.TxtResourceTitle.text = "当前资源".Translate().WithColor(Orange);
            growthPage.TxtOfferTitle.text = "成长定向".Translate().WithColor(Orange);
            growthPage.TxtResource.text = resourceText;
            growthPage.BtnMatrixIcon.Proto = LDB.items.Select(matrixId);
            growthPage.BtnMatrixIcon.SetCount(GetItemTotalCount(matrixId));
            growthPage.BtnFragmentIcon.SetCount(GetItemTotalCount(IFE残片));
            growthPage.TxtFocus.text = $"{focusText}    {GetCurrentFocusEffectText()}";
            IReadOnlyList<GachaGrowthOffer> offers = GachaService.GetGrowthOffers();
            for (int i = 0; i < growthPage.Rows.Count; i++) {
                bool visible = i < offers.Count;
                growthPage.Rows[i].HasOffer = visible;
                growthPage.Rows[i].BtnFragmentCostIcon.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtCost.gameObject.SetActive(visible);
                growthPage.Rows[i].BtnExtraCostIcon.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtExtraCost.gameObject.SetActive(visible);
                growthPage.Rows[i].BtnRewardIcon.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtReward.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtDetail.gameObject.SetActive(visible);
                growthPage.Rows[i].BtnExchange.gameObject.SetActive(visible);
                if (!visible) {
                    continue;
                }

                GachaGrowthOffer offer = offers[i];
                growthPage.Rows[i].Offer = offer;
                growthPage.Rows[i].BtnFragmentCostIcon.SetCount(offer.FragmentCost);
                growthPage.Rows[i].TxtCost.text = $"积分 {offer.PointCost}";
                if (offer.ExtraCostItemId > 0) {
                    growthPage.Rows[i].BtnExtraCostIcon.Proto = LDB.items.Select(offer.ExtraCostItemId);
                    growthPage.Rows[i].BtnExtraCostIcon.gameObject.SetActive(true);
                    growthPage.Rows[i].BtnExtraCostIcon.SetCount(offer.ExtraCostCount);
                    growthPage.Rows[i].TxtExtraCost.text = "";
                } else {
                    growthPage.Rows[i].BtnExtraCostIcon.gameObject.SetActive(false);
                    growthPage.Rows[i].BtnExtraCostIcon.ClearCountText();
                    growthPage.Rows[i].TxtExtraCost.text = "";
                }
                growthPage.Rows[i].BtnRewardIcon.Proto = LDB.items.Select(offer.OutputId);
                growthPage.Rows[i].BtnRewardIcon.SetCount(offer.OutputCount);
                growthPage.Rows[i].TxtReward.text = GetOfferRewardText(offer);
                growthPage.Rows[i].TxtDetail.text = GetOfferDetailText(offer);
                bool canBuy = GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth) >= offer.PointCost
                              && GetItemTotalCount(IFE残片) >= offer.FragmentCost
                              && (offer.ExtraCostItemId <= 0 || GetItemTotalCount(offer.ExtraCostItemId) >= offer.ExtraCostCount);
                growthPage.Rows[i].BtnExchange.button.interactable = canBuy;
            }
        }

        if (focusPage?.Tab != null && focusPage.Tab.gameObject.activeSelf) {
            focusPage.Header.Title.text = "流派聚焦".Translate().WithColor(Orange);
            focusPage.TxtCurrentFocusTitle.text = "当前聚焦".Translate().WithColor(Orange);
            focusPage.TxtFocusListTitle.text = "切换聚焦".Translate().WithColor(Orange);
            focusPage.TxtCurrentFocus.text = focusText;
            focusPage.TxtOverview.text = GetCurrentFocusEffectText();
            for (int i = 0; i < focusPage.Buttons.Count && i < GachaService.FocusDefinitions.Count; i++) {
                var focus = GachaService.FocusDefinitions[i];
                int fragmentCost = GachaService.GetFocusSwitchFragmentCost(focus.FocusType);
                bool active = focus.FocusType == GachaManager.CurrentFocus;
                string buttonText = active
                    ? $"{focus.NameKey.Translate()} ({ "已生效".Translate() })"
                    : $"{focus.NameKey.Translate()} ({ "切换聚焦".Translate() }: {fragmentCost})";
                focusPage.Buttons[i].SetText(buttonText);
                focusPage.Buttons[i].button.interactable = active || GetItemTotalCount(IFE残片) >= fragmentCost;
                focusPage.DescTexts[i].text = $"{focus.DescKey.Translate()}  {GetFocusDetailText(focus.FocusType, active)}";
            }
        }
    }

    private static string GetCurrentFocusName() {
        return GachaService.GetFocusName(GachaManager.CurrentFocus);
    }

    private static string GetOfferRewardText(GachaGrowthOffer offer) {
        string itemName = LDB.items.Select(offer.OutputId)?.name ?? offer.OutputId.ToString();
        if (GachaService.IsDarkFogCatchupOffer(offer)) {
            return $"{itemName} 配方成长 +{offer.OutputCount}";
        }
        if (GachaService.IsDarkFogRecipeGrowthOffer(offer)) {
            return $"{itemName} 转化配方成长";
        }
        return $"{itemName} x{offer.OutputCount}";
    }

    private static string GetOfferDetailText(GachaGrowthOffer offer) {
        if (offer.FocusType == GachaFocusType.Balanced) {
            if (offer.ExtraCostItemId == I黑雾矩阵) {
                if (DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputId)) {
                    return "黑雾增强层报价：消耗黑雾矩阵换取战斗支线的后段突破资源。".WithColor(Gold);
                }
                if (GachaService.IsDarkFogRecipeGrowthOffer(offer)) {
                    return $"黑雾支线配方报价：当前阶段 {GetDarkFogStageName()}，消耗黑雾矩阵推进隐藏科技转化配方。".WithColor(Blue);
                }
                return $"黑雾支线报价：当前阶段 {GetDarkFogStageName()}，消耗黑雾矩阵换取阶段性支线资源。".WithColor(Blue);
            }
            return "常规补差：不受聚焦折扣影响。".WithColor(White);
        }

        string focusName = GachaService.GetFocusName(offer.FocusType);
        if (!GachaService.IsFocusedGrowthOffer(offer)) {
            string prefix = offer.ExtraCostItemId == I黑雾矩阵
                ? DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputId)
                    ? "黑雾增强层报价。"
                    : GachaService.IsDarkFogRecipeGrowthOffer(offer)
                        ? "黑雾支线配方报价。"
                        : "黑雾支线报价。"
                : "成长定向：";
            return $"{prefix}{focusName}。切到该方向后才会降价/加量。".WithColor(White);
        }

        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        string detail = offer.ExtraCostItemId == I黑雾矩阵
            ? DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputId)
                ? $"黑雾增强层命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算"
                : GachaService.IsDarkFogRecipeGrowthOffer(offer)
                    ? $"黑雾支线配方命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算"
                : $"黑雾支线命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算"
            : $"已命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算";
        if (GachaService.IsCoreGrowthReward(offer)) {
            detail += offer.OutputId == IFE残片 ? "，并额外补 10 残片" : "，并额外 +1";
        }
        return detail.WithColor(Green);
    }

    private static string GetDarkFogStageName() {
        return DarkFogCombatManager.GetCurrentStage() switch {
            EDarkFogCombatStage.Dormant => "休眠观察",
            EDarkFogCombatStage.Signal => "信号接触",
            EDarkFogCombatStage.GroundSuppression => "地面压制",
            EDarkFogCombatStage.StellarHunt => "星域围猎",
            _ => "奇点收束",
        };
    }

    private static string GetCurrentFocusEffectText() {
        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        return GachaManager.CurrentFocus switch {
            GachaFocusType.Balanced => "平衡发展：开线池与原胚池不额外偏置，成长页只保留常规补差。".WithColor(White),
            GachaFocusType.MineralExpansion => $"复制扩张：开线池更偏矿物复制，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.ConversionLeap => $"转化跃迁：开线池更偏转化配方，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.LogisticsInteraction => $"交互物流：开线池偏物流链，原胚池偏交互塔，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.EmbryoCycle => $"原胚循环：开线池偏未解锁配方，原胚池偏定向原胚，成长页命中条目按 {discountPercent:0}% 成本并额外 +1。".WithColor(Green),
            GachaFocusType.ProcessOptimization => $"工艺优化：开线池偏当前阶段配方，原胚池偏点数聚集塔，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.RectificationEconomy => $"精馏经济：原胚池偏精馏塔，重复满级配方补偿更多残片，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            _ => string.Empty,
        };
    }

    private static string GetFocusDetailText(GachaFocusType focusType, bool active) {
        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        string activePrefix = active ? "当前生效。" : "切换后生效。";
        return focusType switch {
            GachaFocusType.Balanced => $"{activePrefix} 不额外偏置两类抽池，成长页不触发方向折扣。".WithColor(active ? Green : White),
            GachaFocusType.MineralExpansion => $"{activePrefix} 开线池更偏矿物复制，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(active ? Green : White),
            GachaFocusType.ConversionLeap => $"{activePrefix} 开线池更偏转化配方，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(active ? Green : White),
            GachaFocusType.LogisticsInteraction => $"{activePrefix} 开线池偏物流链，原胚池偏交互塔原胚，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(active ? Green : White),
            GachaFocusType.EmbryoCycle => $"{activePrefix} 开线池偏未解锁配方，原胚池偏定向原胚，成长页命中条目按 {discountPercent:0}% 成本并额外 +1。".WithColor(active ? Green : White),
            GachaFocusType.ProcessOptimization => $"{activePrefix} 开线池偏当前阶段配方，原胚池偏点数聚集塔，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(active ? Green : White),
            GachaFocusType.RectificationEconomy => $"{activePrefix} 原胚池偏精馏塔，满级重复配方补偿更多残片，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(active ? Green : White),
            _ => string.Empty,
        };
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        CreateGrowthUI(wnd, trans);
        CreateFocusUI(wnd, trans);
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
