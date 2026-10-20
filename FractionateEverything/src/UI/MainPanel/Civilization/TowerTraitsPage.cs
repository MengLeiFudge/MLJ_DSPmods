using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>展示已发现的塔体线索及解析后的特质效果。</summary>
public sealed class TowerTraitsPage : CardBrowserPage {
    protected override string Title => "塔体特质".Translate();
    protected override string Summary => "FE.TowerTraitsPage.Summary".Translate();
    protected override string EmptyText => "FE.TowerTraitsPage.Empty".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() => ResearchManager.GetKnownContentDefinitions()
        .Where(c => c.Id.StartsWith("trait:"))
        .Select(c => {
            bool unlocked = ResearchManager.IsContentUnlocked(c.Id);
            int itemId = ResearchManager.GetContentDirectionItemIds(c.Id).FirstOrDefault();
            return new PageCard { Key = c.Id, ItemId = itemId,
                Title = unlocked ? c.DisplayName : "未知塔体特质".Translate(), Status = unlocked ? "可在科技树继续研究".Translate() : "等待塔体解析".Translate(),
                Detail = unlocked
                    ? string.Format("FE.TowerTraitsPage.Known".Translate(), c.DisplayName, Describe(itemId))
                    : string.Format("FE.TowerTraitsPage.Unknown".Translate(), LDB.items.Select(itemId)?.name) };
        }).ToArray();

    /// <summary>返回当前已研究的塔体效果。</summary>
    /// <param name="itemId">塔体物品。</param>
    /// <returns>玩家可读的效果。</returns>
    private static string Describe(int itemId) => itemId switch {
        IFE高级交互塔 => string.Format("繁育均值：{0:0.00}".Translate(), GrowthTechnologyManager.EmbryoBreedingOutput),
        IFE高级矿物塔 => string.Format("珍奇催化损耗倍率：{0:P0}".Translate(), 1 - 0.1 * GrowthTechnologyManager.AdvancedMineralTraitLevel),
        IFE高级转化塔 => string.Format("置换回收倍率：{0:P0}".Translate(), 0.5 + 0.05 * GrowthTechnologyManager.AdvancedConversionTraitLevel),
        _ => "等待新的记录。".Translate(),
    };
}
