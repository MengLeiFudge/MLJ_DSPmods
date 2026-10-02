using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>展示已发现的塔体线索及解析后的特质效果。</summary>
public sealed class TowerTraitsPage : CardBrowserPage {
    protected override string Title => "塔体特质";
    protected override string Summary => "当足够多的生命聚在一起，它们开始显露共同的记忆。";
    protected override IReadOnlyList<PageCard> ReadCards() => ResearchManager.GetKnownContentDefinitions()
        .Where(c => c.Id.StartsWith("trait:"))
        .Select(c => {
            bool unlocked = ResearchManager.IsContentUnlocked(c.Id);
            int itemId = ResearchManager.GetContentDirectionItemIds(c.Id).FirstOrDefault();
            return new PageCard { Key = c.Id, ItemId = itemId,
                Title = unlocked ? c.DisplayName : "未知塔体特质", Status = unlocked ? "可在科技树继续研究" : "等待塔体解析",
                Detail = unlocked
                    ? $"{c.DisplayName}\n\n{Describe(itemId)}\n\n特质采用六级有限成长，使用矩阵研究。"
                    : $"解析样本：{LDB.items.Select(itemId)?.name}\n\n同种高级塔亚空间库存曾达到1000后，探索才可能带回对应特质线索。将样本送入解析塔，完成后查看效果。" };
        }).ToArray();

    /// <summary>返回当前已研究的塔体效果。</summary>
    /// <param name="itemId">塔体物品。</param>
    /// <returns>玩家可读的效果。</returns>
    private static string Describe(int itemId) => itemId switch {
        IFE高级交互塔 => $"繁育均值：{1.5 + 0.05 * GrowthTechnologyManager.AdvancedInteractionTraitLevel:0.00}",
        IFE高级矿物塔 => $"珍奇催化损耗倍率：{1 - 0.1 * GrowthTechnologyManager.AdvancedMineralTraitLevel:P0}",
        IFE高级转化塔 => $"置换回收倍率：{0.5 + 0.05 * GrowthTechnologyManager.AdvancedConversionTraitLevel:P0}",
        _ => "等待新的记录。",
    };
}
