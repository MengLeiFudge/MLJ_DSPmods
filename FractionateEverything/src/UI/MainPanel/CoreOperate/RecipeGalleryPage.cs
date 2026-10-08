using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>仅以已解析配方构建完整图鉴卡片，不展示未知结果。</summary>
public sealed class RecipeGalleryPage : CardBrowserPage {
    protected override string Title => "配方图鉴".Translate();
    protected override string Summary => "FE.RecipeGalleryPage.Summary".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() => RecipeManager.WorldviewRecipes
        .Where(r => r.Kind != WorldviewRecipeKind.BasicAnalysis && ResearchManager.IsContentUnlocked(r.StableId))
        .Select(r => new PageCard { Key = r.StableId, Title = r.DisplayName, ItemId = r.PrimaryProductId,
            Status = string.Format("成功率 {0:P0}".Translate(), r.CycleSuccessProbability),
            DetailItems = r.Inputs.Select(i => i.ItemId).Concat(r.Products.Select(p => p.ItemId)).Distinct().ToArray(),
            Detail = (r.IsAdvanced ? "在对应高级塔选择本配方，使用矩阵供养。".Translate() : "基础矿物塔自动匹配输入矿物。".Translate()) +
                "\n\n输入（成功轮结算消耗）：\n".Translate() + string.Join("\n", r.Inputs.Select(i => $"{LDB.items.Select(i.ItemId)?.name} ×{i.Count}" + (i.ConsumeProbability < 1 ? string.Format(" · 消耗概率{0:P0}".Translate(), i.ConsumeProbability) : ""))) +
                "\n\n产物（每成功轮均值）：\n".Translate() + string.Join("\n", r.Products.Select(p => $"{LDB.items.Select(p.ItemId)?.name} ×{p.Count:0.##}" + (p.ScalesWithRecovery ? " · 随置换回收倍率变化".Translate() : ""))) +
                "FE.RecipeGalleryPage.Effects".Translate() }).ToArray();
}
