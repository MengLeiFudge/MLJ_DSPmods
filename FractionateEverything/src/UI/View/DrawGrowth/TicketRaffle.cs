using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.View.DrawGrowth;

/// <summary>
/// 开线 / 原胚抽取页。
/// 本页只展示当前卡池状态与最近结果，所有概率、保底、聚焦命中和奖励结算都来自 GachaService。
/// </summary>
public static class TicketRaffle {
    private sealed class RaffleTabUi {
        public int PoolId;
        public RectTransform Tab;
        public PageLayout.HeaderRefs Header;
        public Text TxtPoolName;
        public Text TxtPoolDesc;
        public Text TxtResource;
        public Text TxtResourceTitle;
        public MyImageButton BtnMatrixIcon;
        public MyImageButton BtnFragmentIcon;
        public Text TxtMode;
        public Text TxtPity;
        public Text TxtPoints;
        public Text TxtFocus;
        public Text TxtResultSummary;
        public Text TxtResultTitle;
        public readonly Text[] TxtResultLines = new Text[8];
        public readonly MyImageButton[] BtnResultIcons = new MyImageButton[8];
        public UIButton BtnDraw1;
        public UIButton BtnDraw10;
        public UIButton BtnGoGrowth;
        public UIButton BtnGoFocus;
    }

    public static long totalDraws;
    public static long openingLineDraws;
    private static readonly List<RaffleTabUi> activeUis = [];

    private static void CleanupInvalidActiveUis() {
        activeUis.RemoveAll(ui => ui?.Tab == null);
    }

    private static void ResetActiveUisBeforeRecreate() {
        CleanupInvalidActiveUis();
        foreach (var ui in activeUis) {
            if (ui?.Tab != null && ui.Tab.gameObject.activeSelf) {
                return;
            }
        }

        activeUis.Clear();
    }

    private static void SyncTotalDrawsFromSharedState() {
        totalDraws = MainWindow.SharedPanelState?.TicketRaffleTotalDraws ?? 0;
        openingLineDraws = MainWindow.SharedPanelState?.TicketRaffleOpeningLineDraws ?? 0;
    }

    private static void SyncTotalDrawsToSharedState() {
        if (MainWindow.SharedPanelState != null) {
            MainWindow.SharedPanelState.TicketRaffleTotalDraws = totalDraws;
            MainWindow.SharedPanelState.TicketRaffleOpeningLineDraws = openingLineDraws;
        }
    }

    private static void ResetDrawCounters() {
        totalDraws = 0;
        openingLineDraws = 0;
        SyncTotalDrawsToSharedState();
    }

    public static void AddTranslations() {
        Register("开线抽取", "Opening Draw");
        Register("原胚抽取", "Proto Draw");
        Register("成长说明", "Growth Info");
        Register("聚焦说明", "Focus Info");
        Register("常规模式", "Normal Mode");
        Register("速通模式", "Speedrun Mode");

        Register("开线池", "Opening Pool");
        Register("原胚闭环池", "Proto Loop Pool");
        Register("成长池", "Growth Pool");
        Register("流派聚焦", "Focus Control");
        Register("阶段箱池", "Stage Box Pool");
        Register("简化原胚池", "Simplified Proto Pool");
        Register("简化成长池", "Simplified Growth Pool");
        Register("速通聚焦层", "Speedrun Focus Layer");

        Register("开线池说明",
            "Spend the current stage Matrix to draw Mineral Replication / Conversion recipes. Focus raises matching directions.",
            "消耗当前阶段矩阵，抽取矿物复制/转化的新配方与阶段推进条目。当前聚焦会提高对应方向的命中权重。");
        Register("原胚闭环池说明",
            "Spend the current stage Matrix to draw protos and directional protos. Focus raises matching tower embryos.",
            "消耗当前阶段矩阵，抽取各类原胚与定向原胚。当前聚焦会提高对应塔种原胚的出现权重。");
        Register("成长池说明",
            "Growth is deterministic. Use pool points and fragments on the Growth page.",
            "成长池为非随机成长入口。请前往成长页使用池积分与残片进行补差和定向成长。");
        Register("流派聚焦说明",
            "Focus is not a standalone draw pool. Select a focus on the Focus page to bias opening/proto rewards.",
            "流派聚焦不是独立抽卡池。请前往聚焦页选择方向，以偏置开线池和原胚闭环池的奖励分布。");
        Register("阶段箱池说明",
            "Speedrun mode only. Each draw directly yields a current-stage opening target, greatly accelerating line unlock speed.",
            "仅速通模式使用。每次抽取都直接给当前阶段的开线目标，显著提升开线速度。");
        Register("简化原胚池说明",
            "Speedrun mode only. Proto growth is more aggressive and directional protos appear faster.",
            "仅速通模式使用。原胚成长节奏更激进，定向原胚成型更快。");
        Register("简化成长池说明",
            "Speedrun mode only. Only key补差 and rapid breakthroughs remain, with lower resource pressure.",
            "仅速通模式使用。只保留关键补差与快速突破，资源压力更低。");
        Register("速通聚焦层说明",
            "Speedrun mode only. Focus switching is free, and selected themes become much stronger while other themes weaken.",
            "仅速通模式使用。聚焦切换免费，选中的主题会显著变强，其他主题则被明显弱化。");

        Register("当前模式", "Mode");
        Register("当前阶段矩阵", "Current Stage Matrix");
        Register("残片余额", "Fragments");
        Register("保底进度", "Pity");
        Register("当前池积分", "Pool Points");
        Register("成长池积分", "Growth Points");
        Register("抽1次", "Draw x1");
        Register("抽10次", "Draw x10");
        Register("前往成长池", "Go Growth");
        Register("前往聚焦页", "Go Focus");
        Register("结果摘要", "Summary");
        Register("暂无抽取结果", "No draws yet.", "暂无抽取结果");
        Register("更多结果已折叠", "More results folded.", "其余结果已折叠");
        Register("配方解锁", "Recipe Unlock");
        Register("配方提升", "Recipe Upgrade");
        Register("满级转残片", "Duplicate -> Fragments", "满级转残片");
        Register("物品入库", "Stored");
        Register("聚焦主目标", "Focus Main");
        Register("聚焦联动", "Focus Synergy");
        Register("聚焦命中", "Focus Hit");
        Register("保底命中", "Pity Hit");

        Register("聚焦-平衡发展", "Balanced Growth");
        Register("聚焦描述-平衡发展", "No extra bias; both pools stay average.", "不过度偏置任何方向，适合长期稳步推进。");
        Register("聚焦-复制扩张", "Replication Expansion");
        Register("聚焦描述-复制扩张", "Bias Mineral Replication recipes and Mineral Replication Tower protos.",
            "提高矿物复制配方与矿物复制塔原胚的出现权重。");
        Register("聚焦-转化跃迁", "Conversion Leap");
        Register("聚焦描述-转化跃迁", "Bias Conversion recipes and Conversion Tower protos.", "提高转化配方与转化塔原胚的出现权重。");
        Register("聚焦-交互物流", "Interaction Logistics");
        Register("聚焦描述-交互物流", "Bias Interaction Tower protos for logistics and upload loops.", "提高交互塔原胚权重，强化上传与物流中枢。");
        Register("聚焦-原胚循环", "Embryo Cycle");
        Register("聚焦描述-原胚循环", "Bias directional protos and deterministic proto补差.", "提高定向原胚与原胚补差收益，强化原胚闭环。");
        Register("聚焦-工艺优化", "Process Optimization");
        Register("聚焦描述-工艺优化", "Bias current-stage recipes and Point Aggregate Tower protos.", "提高当前阶段配方与点数聚集塔原胚权重。");
        Register("聚焦-精馏经济", "Rectification Economy");
        Register("聚焦描述-精馏经济", "Bias Rectification Tower protos and growth support for fragment economy.",
            "提高精馏塔原胚与残片经济相关补差收益。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyWindow wnd, RectTransform trans) =>
        CreatePoolUI(wnd, trans, GachaPool.PoolIdOpeningLine);

    public static void CreateProtoUI(MyWindow wnd, RectTransform trans) =>
        CreatePoolUI(wnd, trans, GachaPool.PoolIdProtoLoop);

    public static void CreateUpUI(MyWindow wnd, RectTransform trans) =>
        CreatePoolUI(wnd, trans, GachaPool.PoolIdGrowth);

    public static void CreateLimitedUI(MyWindow wnd, RectTransform trans) =>
        CreatePoolUI(wnd, trans, GachaPool.PoolIdFocus);

    private static void CreatePoolUI(MyWindow wnd, RectTransform trans, int poolId) {
        ResetActiveUisBeforeRecreate();
        SyncTotalDrawsFromSharedState();
        var ui = new RaffleTabUi {
            PoolId = poolId,
            Tab = trans
        };
        activeUis.Add(ui);
        BuildLayout(wnd, trans,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(250f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header(GetPoolName(poolId), GetPoolDesc(poolId), $"ticket-raffle-header-{poolId}", pos: (0, 0),
                        onBuilt: refs => {
                            ui.Header = refs;
                            ui.TxtPoolName = refs.Title;
                            ui.TxtPoolDesc = refs.Summary;
                        }),
                    ContentCard(pos: (1, 0), objectName: $"ticket-raffle-resource-card-{poolId}", strong: true,
                        children: [
                            Node(pos: (0, 0), objectName: $"ticket-raffle-resource-body-{poolId}",
                                build: (w, resourceCard) => {
                                    float cardW = resourceCard.sizeDelta.x;
                                    ui.TxtResourceTitle = PageLayout.AddCardTitle(w, resourceCard, 0f, 14f, "当前资源", 15,
                                        $"ticket-raffle-resource-title-{poolId}");
                                    float y = 50f;
                                    ui.TxtResource = MyWindow.AddText(0f, y, resourceCard, "当前资源".Translate(), 13);
                                    ui.TxtResource.rectTransform.sizeDelta = new Vector2(cardW, 22f);
                                    y += 28f;
                                    ui.BtnMatrixIcon = MyImageButton.CreateImageButton(0f, y, resourceCard, null)
                                        .WithSize(40f, 40f);
                                    ui.BtnFragmentIcon = MyImageButton
                                        .CreateImageButton(180f, y, resourceCard, LDB.items.Select(IFE残片))
                                        .WithSize(40f, 40f);
                                    y += 48f;
                                    ui.TxtMode = MyWindow.AddText(0f, y, resourceCard, "", 13);
                                    ui.TxtMode.rectTransform.sizeDelta = new Vector2(cardW, 22f);
                                    y += 26f;
                                    ui.TxtPity = MyWindow.AddText(0f, y, resourceCard, "", 13);
                                    ui.TxtPity.rectTransform.sizeDelta = new Vector2(cardW, 22f);
                                    y += 26f;
                                    ui.TxtPoints = MyWindow.AddText(0f, y, resourceCard, "", 13);
                                    ui.TxtPoints.rectTransform.sizeDelta = new Vector2(cardW, 22f);
                                    y += 26f;
                                    ui.TxtFocus = MyWindow.AddText(0f, y, resourceCard, "", 13);
                                    ui.TxtFocus.rectTransform.sizeDelta = new Vector2(cardW, 40f);
                                }),
                        ]),
                    ContentCard(pos: (2, 0), objectName: $"ticket-raffle-result-card-{poolId}",
                        children: [
                            Node(pos: (0, 0), objectName: $"ticket-raffle-result-body-{poolId}",
                                build: (w, resultCard) => {
                                    float cardW = resultCard.sizeDelta.x;
                                    ui.TxtResultTitle = PageLayout.AddCardTitle(w, resultCard, 0f, 0f, "结果摘要", 15,
                                        $"ticket-raffle-result-title-{poolId}");
                                    float y = 32f;
                                    ui.TxtResultSummary = MyWindow.AddText(0f, y, resultCard, "暂无抽取结果".Translate(), 13);
                                    ui.TxtResultSummary.rectTransform.sizeDelta = new Vector2(cardW, 40f);
                                    y += 40f;
                                    for (int i = 0; i < ui.TxtResultLines.Length; i++) {
                                        ui.BtnResultIcons[i] = MyImageButton.CreateImageButton(0f, y, resultCard, null)
                                            .WithSize(40f, 40f);
                                        ui.BtnResultIcons[i].gameObject.SetActive(false);
                                        ui.TxtResultLines[i] = MyWindow.AddText(48f, y, resultCard, "", 13);
                                        ui.TxtResultLines[i].rectTransform.sizeDelta = new Vector2(cardW - 48f, 20f);
                                        y += 22f;
                                    }
                                }),
                        ]),
                    FooterCard(pos: (3, 0), objectName: $"ticket-raffle-footer-card-{poolId}",
                        children: [
                            Node(pos: (0, 0), objectName: $"ticket-raffle-footer-body-{poolId}",
                                build: (w, footerCard) => {
                                    float cardW = footerCard.sizeDelta.x;
                                    ui.BtnDraw1 = w.AddButton(0f, 0f, 150f, footerCard, "抽1次".Translate(), 14,
                                        onClick: () => StartDraw(ui, 1));
                                    ui.BtnDraw10 = w.AddButton(166f, 0f, 150f, footerCard, "抽10次".Translate(), 14,
                                        onClick: () => StartDraw(ui, 10));
                                    ui.BtnGoGrowth = w.AddButton(cardW - 316f, 0f, 150f, footerCard,
                                        "前往成长池".Translate(), 14,
                                        onClick: () =>
                                            MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName,
                                                2));
                                    ui.BtnGoFocus = w.AddButton(cardW - 150f, 0f, 150f, footerCard, "前往聚焦页".Translate(),
                                        14,
                                        onClick: () =>
                                            MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName,
                                                3));
                                }),
                        ]),
                ]));

        RefreshTabState(ui);
    }

    private static string GetPoolName(int poolId) {
        return GachaService.GetPoolNameKey(poolId).Translate();
    }

    private static string GetPoolDesc(int poolId) {
        return GachaService.GetPoolDescKey(poolId).Translate();
    }

    private static void StartDraw(RaffleTabUi ui, int count) {
        if (!GachaPool.IsDrawPool(ui.PoolId)) {
            return;
        }

        int matrixId = GachaService.GetCurrentDrawMatrixId();
        var results = GachaService.Draw(ui.PoolId, matrixId, count);
        if (results == null || results.Count == 0) {
            return;
        }

        totalDraws += results.Count;
        if (ui.PoolId == GachaPool.PoolIdOpeningLine) {
            openingLineDraws += results.Count;
        }
        SyncTotalDrawsToSharedState();
        RenderResults(ui, matrixId, count, results);
        RefreshTabState(ui);
    }

    private static void RenderResults(RaffleTabUi ui, int matrixId, int count, List<GachaResult> results) {
        int sCount = 0;
        int aCount = 0;
        int bCount = 0;
        int cCount = 0;
        int focusHitCount = 0;
        int focusMainHitCount = 0;
        int hardPityCount = 0;
        foreach (var result in results) {
            switch (result.Rarity) {
                case GachaRarity.S:
                    sCount++;
                    break;
                case GachaRarity.A:
                    aCount++;
                    break;
                case GachaRarity.B:
                    bCount++;
                    break;
                default:
                    cCount++;
                    break;
            }
            if (result.IsFocusHit) {
                focusHitCount++;
            }
            if (result.HitFocusMainTarget) {
                focusMainHitCount++;
            }
            if (result.WasHardPity) {
                hardPityCount++;
            }
        }

        int cost = GachaService.GetDrawMatrixCost(ui.PoolId, count);
        string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
        ui.TxtResultSummary.text =
            $"{"结果摘要".Translate()}：{matrixName} x{cost}    "
            + $"S×{sCount}".WithColor(Gold)
            + $" / A×{aCount}".WithColor(Purple)
            + $" / B×{bCount}".WithColor(Blue)
            + $" / C×{cCount}".WithColor(White)
            + $"    {"成长池积分".Translate()} +{results.Count}".WithColor(Green)
            + $"    {"聚焦命中".Translate()} ×{focusHitCount}".WithColor(Green)
            + $" / {"聚焦主目标".Translate()} ×{focusMainHitCount}".WithColor(Blue)
            + $" / {"保底命中".Translate()} ×{hardPityCount}".WithColor(Gold);

        int lineCount = Mathf.Min(ui.TxtResultLines.Length, results.Count);
        for (int i = 0; i < lineCount; i++) {
            GachaResult result = results[i];
            ui.BtnResultIcons[i].gameObject.SetActive(true);
            ui.BtnResultIcons[i].Proto = LDB.items.Select(result.ItemId);
            ui.BtnResultIcons[i].SetCount(1);
            ui.TxtResultLines[i].text = BuildResultLine(result);
        }

        for (int i = lineCount; i < ui.TxtResultLines.Length; i++) {
            ui.BtnResultIcons[i].gameObject.SetActive(false);
            ui.BtnResultIcons[i].ClearCountText();
            ui.TxtResultLines[i].text = i == lineCount && results.Count > ui.TxtResultLines.Length
                ? "更多结果已折叠".Translate().WithColor(Orange)
                : "";
        }
    }

    private static void RefreshTabState(RaffleTabUi ui) {
        if (ui.TxtPoolName != null) {
            ui.TxtPoolName.text = GetPoolName(ui.PoolId).WithColor(Orange);
        }
        if (ui.TxtPoolDesc != null) {
            ui.TxtPoolDesc.text = GetPoolDesc(ui.PoolId);
        }
        if (ui.TxtResourceTitle != null) {
            ui.TxtResourceTitle.text = "当前资源".Translate().WithColor(Orange);
        }
        if (ui.TxtResultTitle != null) {
            ui.TxtResultTitle.text = "结果摘要".Translate().WithColor(Orange);
        }

        int matrixId = GachaService.GetCurrentDrawMatrixId();
        long matrixCount = GetItemTotalCount(matrixId);
        int draw1Cost = GachaService.GetDrawMatrixCost(ui.PoolId, 1);
        int draw10Cost = GachaService.GetDrawMatrixCost(ui.PoolId, 10);

        ui.BtnMatrixIcon.Proto = LDB.items.Select(matrixId);
        ui.BtnMatrixIcon.SetCount(matrixCount);
        ui.BtnFragmentIcon.SetCount(GetItemTotalCount(IFE残片));
        if (ui.TxtMode != null) {
            ui.TxtMode.text = $"{"当前模式".Translate()}：{GachaService.GetModeNameKey().Translate()}";
        }
        ui.TxtPity.text = GachaPool.IsDrawPool(ui.PoolId)
            ? $"{"保底进度".Translate()}：{GachaManager.PityCount[ui.PoolId] + 1}/90"
            : $"{"保底进度".Translate()}：-";
        ui.TxtPoints.text = $"{"成长池积分".Translate()}：{GachaService.GetDisplayPoolPoints()}";
        ui.TxtFocus.text =
            $"{"当前聚焦".Translate()}：{GachaService.GetFocusName(GachaManager.CurrentFocus)}    {GetFocusEffectSummary()}";

        bool canDraw1 = GachaPool.IsDrawPool(ui.PoolId) && matrixCount >= draw1Cost;
        bool canDraw10 = GachaPool.IsDrawPool(ui.PoolId) && matrixCount >= draw10Cost;

        if (ui.BtnDraw1?.button != null) {
            ui.BtnDraw1.button.interactable = canDraw1;
            ui.BtnDraw1.SetText($"{"抽1次".Translate()} ({draw1Cost})");
        }
        if (ui.BtnDraw10?.button != null) {
            ui.BtnDraw10.button.interactable = canDraw10;
            ui.BtnDraw10.SetText($"{"抽10次".Translate()} ({draw10Cost})");
        }
    }

    private static string BuildResultLine(GachaResult result) {
        string itemName = LDB.items.Select(result.ItemId)?.name ?? result.ItemId.ToString();
        string line = $"{GetRarityTag(result.Rarity)}  {itemName}  {GetRewardText(result)}";
        if (result.WasHardPity) {
            line += $"  {"保底命中".Translate()}".WithColor(Gold);
        }
        if (result.FocusMatchType == GachaFocusMatchType.Main) {
            line += $"  {"聚焦主目标".Translate()}".WithColor(Blue);
        } else if (result.FocusMatchType == GachaFocusMatchType.Side) {
            line += $"  {"聚焦联动".Translate()}".WithColor(Green);
        }
        return line;
    }

    private static string GetRewardText(GachaResult result) {
        return result.RewardType switch {
            GachaRewardType.RecipeUnlock => $"{"配方解锁".Translate()} Lv{result.RewardCount}".WithColor(Orange),
            GachaRewardType.RecipeUpgrade => $"{"配方提升".Translate()} -> Lv{result.RewardCount}".WithColor(Orange),
            GachaRewardType.DuplicateRecipeFragments =>
                $"{"满级转残片".Translate()} {GetRewardItemName(result)} x{result.RewardCount}".WithColor(Green),
            GachaRewardType.ItemGranted => $"{"物品入库".Translate()} x{result.RewardCount}".WithColor(Blue),
            _ => string.Empty,
        };
    }

    private static string GetRewardItemName(GachaResult result) {
        return LDB.items.Select(result.RewardItemId)?.name ?? result.RewardItemId.ToString();
    }

    private static string GetRarityTag(GachaRarity rarity) {
        return rarity switch {
            GachaRarity.S => "[S]".WithColor(Gold),
            GachaRarity.A => "[A]".WithColor(Purple),
            GachaRarity.B => "[B]".WithColor(Blue),
            _ => "[C]".WithColor(White),
        };
    }

    private static string GetFocusEffectSummary() {
        float discountPercent = GachaService.GetFocusedOfferDiscountFactor() * 100f;
        return GachaManager.CurrentFocus switch {
            GachaFocusType.Balanced => "不额外偏置开线或原胚，成长页保持原价。".WithColor(White),
            GachaFocusType.MineralExpansion => $"开线池偏向矿物复制；成长页命中方向条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.ConversionLeap => $"开线池偏向转化配方；成长页命中方向条目按 {discountPercent:0}% 成本结算。".WithColor(Green),
            GachaFocusType.LogisticsInteraction => $"开线池偏向物流链配方，原胚池偏向交互塔原胚；成长页命中方向条目按 {discountPercent:0}% 成本结算。"
                .WithColor(Green),
            GachaFocusType.EmbryoCycle =>
                $"开线池偏向未解锁配方，原胚池偏向定向原胚；成长页命中方向条目按 {discountPercent:0}% 成本并额外 +1。".WithColor(Green),
            GachaFocusType.ProcessOptimization => $"开线池偏向当前阶段配方，原胚池偏向点数聚集塔；成长页命中方向条目按 {discountPercent:0}% 成本结算。"
                .WithColor(Green),
            GachaFocusType.RectificationEconomy => $"原胚池偏向精馏塔，满级重复配方会补偿更多残片；成长页命中方向条目按 {discountPercent:0}% 成本结算。"
                .WithColor(Green),
            _ => string.Empty,
        };
    }

    public static void UpdateUI() {
        CleanupInvalidActiveUis();
        SyncTotalDrawsFromSharedState();
        foreach (var ui in activeUis) {
            if (ui?.Tab == null || !ui.Tab.gameObject.activeSelf) {
                continue;
            }
            RefreshTabState(ui);
        }
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("TotalDraws", br => {
                long value = br.ReadInt64();
                totalDraws = value < 0 ? 0 : value;
            }),
            ("OpeningLineDraws", br => {
                long value = br.ReadInt64();
                openingLineDraws = value < 0 ? 0 : value;
            })
        );
        SyncTotalDrawsToSharedState();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalDraws", bw => bw.Write(totalDraws)),
            ("OpeningLineDraws", bw => bw.Write(openingLineDraws))
        );
    }

    public static void IntoOtherSave() {
        activeUis.Clear();
        ResetDrawCounters();
    }
}
