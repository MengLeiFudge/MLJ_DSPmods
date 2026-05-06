using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.MainPanel;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.DrawGrowth;

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
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(180f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("成长规划", objectName: "growth-store-header", pos: (0, 0),
                        onBuilt: refs => growthPage.Header = refs),
                    ContentCard(pos: (1, 0), objectName: "growth-store-resource-card", strong: true,
                        rows: [Px(28f), 1, Px(50f), 2],
                        rowGap: 4f,
                        children: BuildGrowthResourceNodes()),
                    ContentCard(pos: (2, 0), objectName: "growth-store-offer-card",
                        rows: BuildGrowthOfferRows(),
                        cols: [Fr(1), Px(44f), Px(44f), Fr(1), Px(44f), Fr(2), Fr(1)],
                        rowGap: 4f,
                        columnGap: 8f,
                        children: BuildGrowthOfferNodes()),
                    FooterCard(pos: (3, 0), objectName: "growth-store-footer-card",
                        cols: [4, 1],
                        columnGap: PageLayout.InnerGap,
                        children: [
                            ButtonNode("前往抽取",
                                onClick: () =>
                                    MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0),
                                pos: (0, 1), objectName: "growth-store-go-draw"),
                        ]),
                ]));
    }

    private static void CreateFocusUI(MyWindow wnd, RectTransform trans) {
        focusPage = new FocusPageUi {
            Tab = trans
        };
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(140f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("流派聚焦", objectName: "focus-store-header", pos: (0, 0),
                        onBuilt: refs => focusPage.Header = refs),
                    ContentCard(pos: (1, 0), objectName: "focus-store-current-card", strong: true,
                        rows: [Px(28f), 1, 2],
                        rowGap: 4f,
                        children: [
                            CardTitleNode("当前聚焦", onBuilt: text => focusPage.TxtCurrentFocusTitle = text,
                                pos: (0, 0), objectName: "focus-store-current-title"),
                            TextNode("", 13, wrap: true, onBuilt: text => focusPage.TxtCurrentFocus = text,
                                pos: (1, 0), objectName: "focus-store-current-focus"),
                            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => focusPage.TxtOverview = text,
                                pos: (2, 0), objectName: "focus-store-overview"),
                        ]),
                    ContentCard(pos: (2, 0), objectName: "focus-store-list-card",
                        rows: BuildFocusRows(),
                        cols: [Fr(1), Fr(3)],
                        rowGap: 6f,
                        columnGap: PageLayout.InnerGap,
                        children: BuildFocusNodes()),
                    FooterCard(pos: (3, 0), objectName: "focus-store-footer-card",
                        cols: [4, 1],
                        columnGap: PageLayout.InnerGap,
                        children: [
                            ButtonNode("前往抽取",
                                onClick: () =>
                                    MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0),
                                pos: (0, 1), objectName: "focus-store-go-draw"),
                        ]),
                ]));
    }

    private static IReadOnlyList<LayoutNode> BuildGrowthResourceNodes() {
        return [
            CardTitleNode("当前资源", onBuilt: text => growthPage.TxtResourceTitle = text,
                pos: (0, 0), objectName: "growth-store-resource-title"),
            TextNode("当前资源", 13, wrap: true, onBuilt: text => growthPage.TxtResource = text,
                pos: (1, 0), objectName: "growth-store-resource-text"),
            Grid(pos: (2, 0), cols: [1, 1, 4], columnGap: PageLayout.InnerGap, children: [
                ImageButtonNode(size: 40f, onBuilt: btn => growthPage.BtnMatrixIcon = btn,
                    pos: (0, 0), objectName: "growth-store-matrix-icon"),
                ImageButtonNode(LDB.items.Select(IFE残片), 40f, onBuilt: btn => growthPage.BtnFragmentIcon = btn,
                    pos: (0, 1), objectName: "growth-store-fragment-icon"),
            ]),
            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true, onBuilt: text => growthPage.TxtFocus = text,
                pos: (3, 0), objectName: "growth-store-focus"),
        ];
    }

    private static IReadOnlyList<LayoutTrack> BuildGrowthOfferRows() {
        var rows = new List<LayoutTrack> { Px(28f) };
        for (int i = 0; i < GrowthRowCount; i++) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildGrowthOfferNodes() {
        var nodes = new List<LayoutNode> {
            CardTitleNode("成长定向", onBuilt: text => growthPage.TxtOfferTitle = text,
                pos: (0, 0), span: (1, 7), objectName: "growth-store-offer-title"),
        };
        for (int i = 0; i < GrowthRowCount; i++) {
            int rowIndex = i;
            var row = new GrowthRowUi();
            growthPage.Rows.Add(row);
            int rowPos = rowIndex + 1;
            nodes.Add(TextNode("", 13, onBuilt: text => row.TxtCost = text,
                pos: (rowPos, 0), objectName: $"growth-store-cost-{rowIndex}"));
            nodes.Add(ImageButtonNode(LDB.items.Select(IFE残片), 40f,
                onBuilt: btn => row.BtnFragmentCostIcon = btn,
                pos: (rowPos, 1), objectName: $"growth-store-fragment-cost-{rowIndex}"));
            nodes.Add(ImageButtonNode(size: 40f, onBuilt: btn => row.BtnExtraCostIcon = btn,
                pos: (rowPos, 2), objectName: $"growth-store-extra-cost-icon-{rowIndex}"));
            nodes.Add(TextNode("", 13, onBuilt: text => row.TxtExtraCost = text,
                pos: (rowPos, 3), objectName: $"growth-store-extra-cost-{rowIndex}"));
            nodes.Add(ImageButtonNode(size: 40f, onBuilt: btn => row.BtnRewardIcon = btn,
                pos: (rowPos, 4), objectName: $"growth-store-reward-icon-{rowIndex}"));
            nodes.Add(TextNode("", 13, wrap: true, onBuilt: text => row.TxtReward = text,
                pos: (rowPos, 5), objectName: $"growth-store-reward-{rowIndex}"));
            nodes.Add(TextNode("", 12, wrap: true, onBuilt: text => row.TxtDetail = text,
                pos: (rowPos, 5), margin: Inset(0f, 18f, 0f, 0f),
                objectName: $"growth-store-detail-{rowIndex}"));
            nodes.Add(ButtonNode("兑换", fontSize: 13, onBuilt: btn => row.BtnExchange = btn,
                onClick: () => ExchangeOffer(row),
                pos: (rowPos, 6), objectName: $"growth-store-exchange-{rowIndex}"));
        }

        return nodes;
    }

    private static IReadOnlyList<LayoutTrack> BuildFocusRows() {
        var rows = new List<LayoutTrack> { Px(28f) };
        foreach (var _ in GachaService.FocusDefinitions) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildFocusNodes() {
        var nodes = new List<LayoutNode> {
            CardTitleNode("切换聚焦", onBuilt: text => focusPage.TxtFocusListTitle = text,
                pos: (0, 0), span: (1, 2), objectName: "focus-store-list-title"),
        };
        int index = 0;
        foreach (var focus in GachaService.FocusDefinitions) {
            var focusDefinition = focus;
            int row = index + 1;
            nodes.Add(ButtonNode("", fontSize: 13,
                onClick: () => ChangeFocus(focusDefinition.FocusType),
                onBuilt: button => focusPage.Buttons.Add(button),
                pos: (row, 0), objectName: $"focus-store-button-{index}"));
            nodes.Add(TextNode($"{focus.NameKey.Translate()}：{focus.DescKey.Translate()}", 13,
                anchor: TextAnchor.UpperLeft, wrap: true,
                onBuilt: text => focusPage.DescTexts.Add(text),
                pos: (row, 1), objectName: $"focus-store-desc-{index}"));
            index++;
        }

        return nodes;
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
        string resourceText =
            $"成长积分 x{GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth)}    黑雾矩阵 x{GetItemTotalCount(I黑雾矩阵)}";
        string focusText =
            $"{"当前模式".Translate()}：{GachaService.GetModeNameKey().Translate()}    {"当前聚焦".Translate()}：{GetCurrentFocusName()}";

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
                              && (offer.ExtraCostItemId <= 0
                                  || GetItemTotalCount(offer.ExtraCostItemId) >= offer.ExtraCostCount);
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
                    ? $"{focus.NameKey.Translate()} ({"已生效".Translate()})"
                    : $"{focus.NameKey.Translate()} ({"切换聚焦".Translate()}: {fragmentCost})";
                focusPage.Buttons[i].SetText(buttonText);
                focusPage.Buttons[i].button.interactable = active || GetItemTotalCount(IFE残片) >= fragmentCost;
                focusPage.DescTexts[i].text =
                    $"{focus.DescKey.Translate()}  {GetFocusDetailText(focus.FocusType, active)}";
            }
        }
    }

    private static string GetCurrentFocusName() {
        return GachaService.GetFocusName(GachaManager.CurrentFocus);
    }

    private static string GetOfferRewardText(GachaGrowthOffer offer) {
        if (GachaService.IsDarkFogCatchupOffer(offer)) {
            return "配方成长";
        }
        if (GachaService.IsDarkFogRecipeGrowthOffer(offer)) {
            return "转化配方成长";
        }
        return string.Empty;
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
                ? DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputId) ? "黑雾增强层报价。" :
                GachaService.IsDarkFogRecipeGrowthOffer(offer) ? "黑雾支线配方报价。" : "黑雾支线报价。"
                : "成长定向：";
            return $"{prefix}{focusName}。切到该方向后才会降价/加量。".WithColor(White);
        }

        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        string detail = offer.ExtraCostItemId == I黑雾矩阵
            ? DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputId) ?
                $"黑雾增强层命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算" :
                GachaService.IsDarkFogRecipeGrowthOffer(offer) ?
                    $"黑雾支线配方命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算" :
                    $"黑雾支线命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算"
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
            GachaFocusType.LogisticsInteraction =>
                $"交互物流：开线池偏物流链，原胚池偏交互塔，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.EmbryoCycle =>
                $"原胚循环：开线池偏未解锁配方，原胚池偏定向原胚，成长页命中条目按 {discountPercent:0}% 成本并额外 +1。".WithColor(Green),
            GachaFocusType.ProcessOptimization => $"工艺优化：开线池偏当前阶段配方，原胚池偏点数聚集塔，成长页命中条目按 {discountPercent:0}% 成本结算。"
                .WithColor(Green),
            GachaFocusType.RectificationEconomy => $"精馏经济：原胚池偏精馏塔，重复满级配方补偿更多残片，成长页命中条目按 {discountPercent:0}% 成本结算。"
                .WithColor(Green),
            _ => string.Empty,
        };
    }

    private static string GetFocusDetailText(GachaFocusType focusType, bool active) {
        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        string activePrefix = active ? "当前生效。" : "切换后生效。";
        return focusType switch {
            GachaFocusType.Balanced => $"{activePrefix} 不额外偏置两类抽池，成长页不触发方向折扣。".WithColor(active ? Green : White),
            GachaFocusType.MineralExpansion =>
                $"{activePrefix} 开线池更偏矿物复制，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(active ? Green : White),
            GachaFocusType.ConversionLeap => $"{activePrefix} 开线池更偏转化配方，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(
                active ? Green : White),
            GachaFocusType.LogisticsInteraction =>
                $"{activePrefix} 开线池偏物流链，原胚池偏交互塔原胚，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(
                    active ? Green : White),
            GachaFocusType.EmbryoCycle => $"{activePrefix} 开线池偏未解锁配方，原胚池偏定向原胚，成长页命中条目按 {discountPercent:0}% 成本并额外 +1。"
                .WithColor(active ? Green : White),
            GachaFocusType.ProcessOptimization =>
                $"{activePrefix} 开线池偏当前阶段配方，原胚池偏点数聚集塔，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(
                    active ? Green : White),
            GachaFocusType.RectificationEconomy =>
                $"{activePrefix} 原胚池偏精馏塔，满级重复配方补偿更多残片，成长页命中条目按 {discountPercent:0}% 成本结算。".WithColor(
                    active ? Green : White),
            _ => string.Empty,
        };
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        CreateGrowthUI(wnd, trans);
        CreateFocusUI(wnd, trans);
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }
}
