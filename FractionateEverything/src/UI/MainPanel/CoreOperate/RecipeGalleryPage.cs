using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>仅以已解析配方构建图鉴卡片，不受完整信息偏好影响而泄露未知结果。</summary>
public sealed class RecipeGalleryPage : CardBrowserPage {
    protected override string Title => "配方图鉴";
    protected override string Summary => "未知的反应，已经成为生产线上可靠的一环。";
    protected override IReadOnlyList<PageCard> ReadCards() => RecipeManager.WorldviewRecipes
        .Where(r => r.Kind != WorldviewRecipeKind.BasicAnalysis && ResearchManager.IsContentUnlocked(r.StableId))
        .Select(r => new PageCard { Key = r.StableId, Title = r.DisplayName, ItemId = r.PrimaryProductId,
            Status = $"成功率 {r.CycleSuccessProbability:P0}",
            DetailItems = r.Inputs.Select(i => i.ItemId).Concat(r.Products.Select(p => p.ItemId)).Distinct().ToArray(),
            Detail = $"{(r.IsAdvanced ? "在对应高级塔选择本配方，使用矩阵供养。" : "基础矿物塔自动匹配输入矿物。")}" +
                "\n\n输入（成功轮结算消耗）：\n" + string.Join("\n", r.Inputs.Select(i => $"{LDB.items.Select(i.ItemId)?.name} ×{i.Count}" + (i.ConsumeProbability < 1 ? $" · 消耗概率{i.ConsumeProbability:P0}" : ""))) +
                "\n\n产物（每成功轮均值）：\n" + string.Join("\n", r.Products.Select(p => $"{LDB.items.Select(p.ItemId)?.name} ×{p.Count:0.##}" + (p.ScalesWithRecovery ? " · 随置换回收倍率变化" : ""))) +
                "\n\n失败原料回流。催化消耗、繁育与置换收益还会受到已研究特质影响。" }).ToArray();
}
