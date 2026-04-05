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
        public Text TxtResource;
        public MyImageButton BtnMatrixIcon;
        public MyImageButton BtnFragmentIcon;
        public Text TxtFocus;
        public readonly List<GrowthRowUi> Rows = [];
    }

    private sealed class FocusPageUi {
        public RectTransform Tab;
        public Text TxtCurrentFocus;
        public Text TxtOverview;
        public readonly List<UIButton> Buttons = [];
        public readonly List<Text> DescTexts = [];
    }

    private static GrowthPageUi growthPage;
    private static FocusPageUi focusPage;

    public static void AddTranslations() {
        Register("成长规划", "Growth Planning");
        Register("流派聚焦", "Focus Control");
        Register("资源统筹", "Resource Overview");
        Register("前往抽取", "Go Draw");
        Register("兑换", "Exchange");
        Register("当前资源", "Resource");
        Register("当前模式", "Mode");
        Register("当前聚焦", "Current Focus");
        Register("切换聚焦", "Switch Focus");
        Register("已生效", "Active");
        Register("成长定向", "Growth Bias");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyConfigWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans, "成长规划");
    public static void CreateProtoUI(MyConfigWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans, "流派聚焦");
    public static void CreateUpUI(MyConfigWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans, "成长规划");
    public static void CreateLimitedUI(MyConfigWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans, "流派聚焦");

    private static void CreateGrowthUI(MyConfigWindow wnd, RectTransform trans, string pageName) {
        growthPage = new GrowthPageUi {
            Tab = wnd.AddTab(trans, pageName)
        };

        float y = 8f;
        growthPage.TxtResource = MyWindow.AddText(0f, y, growthPage.Tab, "当前资源".Translate(), 13);
        y += 24f;
        growthPage.BtnMatrixIcon = MyImageButton.CreateImageButton(0f, y, growthPage.Tab, null).WithSize(40f, 40f);
        growthPage.BtnFragmentIcon = MyImageButton.CreateImageButton(180f, y, growthPage.Tab, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
        y += 40f;
        growthPage.TxtFocus = MyWindow.AddText(0f, y, growthPage.Tab, "", 13);
        growthPage.TxtFocus.rectTransform.sizeDelta = new Vector2(960f, 40f);
        y += 44f;

        wnd.AddButton(780f, 8f, 140f, growthPage.Tab, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0));

        for (int i = 0; i < GrowthRowCount; i++) {
            var row = new GrowthRowUi {
                BtnFragmentCostIcon = MyImageButton.CreateImageButton(110f, y, growthPage.Tab, LDB.items.Select(IFE残片)).WithSize(40f, 40f),
                TxtCost = MyWindow.AddText(0f, y, growthPage.Tab, "", 13),
                BtnExtraCostIcon = MyImageButton.CreateImageButton(220f, y, growthPage.Tab, null).WithSize(40f, 40f),
                TxtExtraCost = MyWindow.AddText(262f, y, growthPage.Tab, "", 13),
                BtnRewardIcon = MyImageButton.CreateImageButton(420f, y, growthPage.Tab, null).WithSize(40f, 40f),
                TxtReward = MyWindow.AddText(462f, y, growthPage.Tab, "", 13),
                TxtDetail = MyWindow.AddText(462f, y + 18f, growthPage.Tab, "", 12),
            };
            row.BtnExchange = wnd.AddButton(760f, y - 4f, 120f, growthPage.Tab, "兑换".Translate(), 13,
                onClick: () => ExchangeOffer(row));
            row.TxtCost.rectTransform.sizeDelta = new Vector2(100f, 24f);
            row.TxtExtraCost.rectTransform.sizeDelta = new Vector2(120f, 24f);
            row.TxtReward.rectTransform.sizeDelta = new Vector2(280f, 24f);
            row.TxtDetail.rectTransform.sizeDelta = new Vector2(280f, 20f);
            growthPage.Rows.Add(row);
            y += 42f;
        }
    }

    private static void CreateFocusUI(MyConfigWindow wnd, RectTransform trans, string pageName) {
        focusPage = new FocusPageUi {
            Tab = wnd.AddTab(trans, pageName)
        };

        float y = 8f;
        focusPage.TxtCurrentFocus = MyWindow.AddText(0f, y, focusPage.Tab, "", 14);
        y += 28f;
        focusPage.TxtOverview = MyWindow.AddText(0f, y, focusPage.Tab, "", 13);
        focusPage.TxtOverview.rectTransform.sizeDelta = new Vector2(960f, 40f);
        y += 52f;

        wnd.AddButton(780f, 8f, 140f, focusPage.Tab, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0));

        foreach (var focus in GachaService.FocusDefinitions) {
            float currentY = y;
            var button = wnd.AddButton(0f, currentY, 280f, focusPage.Tab, "", 13,
                onClick: () => ChangeFocus(focus.FocusType));
            Text desc = MyWindow.AddText(300f, currentY + 4f, focusPage.Tab,
                $"{focus.NameKey.Translate()}：{focus.DescKey.Translate()}", 13)
                ;
            desc.rectTransform.sizeDelta = new Vector2(620f, 40f);
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
        if (offer.PointCost > 0 && !GachaManager.TryConsumePoolPoints(GachaPool.PoolIdGrowth, offer.PointCost)) {
            return;
        }
        if (offer.FragmentCost > 0 && GetItemTotalCount(IFE残片) < offer.FragmentCost) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }
        if (offer.ExtraCostItemId > 0 && GetItemTotalCount(offer.ExtraCostItemId) < offer.ExtraCostCount) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }
        if (offer.FragmentCost > 0 && !TakeItemWithTip(IFE残片, offer.FragmentCost, out _)) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }
        if (offer.ExtraCostItemId > 0 && !TakeItemWithTip(offer.ExtraCostItemId, offer.ExtraCostCount, out _)) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            AddItemToModData(IFE残片, offer.FragmentCost, 0, true);
            return;
        }

        AddItemToModData(offer.OutputId, offer.OutputCount, 0, true);
        string itemName = LDB.items.Select(offer.OutputId)?.name ?? offer.OutputId.ToString();
        UIRealtimeTip.Popup($"获得 {itemName} x{offer.OutputCount}");
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
        return $"{itemName} x{offer.OutputCount}";
    }

    private static string GetOfferDetailText(GachaGrowthOffer offer) {
        if (offer.FocusType == GachaFocusType.Balanced) {
            if (offer.ExtraCostItemId == I黑雾矩阵) {
                return "黑雾支线报价：消耗黑雾矩阵换取阶段性支线资源。".WithColor(Blue);
            }
            return "常规补差：不受聚焦折扣影响。".WithColor(White);
        }

        string focusName = GachaService.GetFocusName(offer.FocusType);
        if (!GachaService.IsFocusedGrowthOffer(offer)) {
            string prefix = offer.ExtraCostItemId == I黑雾矩阵 ? "黑雾支线报价。" : "成长定向：";
            return $"{prefix}{focusName}。切到该方向后才会降价/加量。".WithColor(White);
        }

        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        string detail = offer.ExtraCostItemId == I黑雾矩阵
            ? $"黑雾支线命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算"
            : $"已命中 {focusName}：积分/残片按 {discountPercent:0}% 成本结算";
        if (GachaService.IsCoreGrowthReward(offer)) {
            detail += offer.OutputId == IFE残片 ? "，并额外补 10 残片" : "，并额外 +1";
        }
        return detail.WithColor(Green);
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

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        CreateGrowthUI(wnd, trans, "成长规划");
        CreateFocusUI(wnd, trans, "流派聚焦");
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
