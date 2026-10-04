using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>按塔体类型汇总已存在的生命塔运行状态。</summary>
public sealed class LivingTowersPage : CardBrowserPage {
    protected override string Title => "生命塔".Translate();
    protected override string Summary => string.Format("FE.LivingTowersPage.Summary".Translate(), ResearchManager.MemorySuccessBoost);
    protected override string EmptyText => "FE.LivingTowersPage.Empty".Translate();
    /// <summary>按星球与塔体类型列出运行状态，存在饥饿或停滞的星球排在前面。</summary>
    protected override IReadOnlyList<PageCard> ReadCards() => AdvancedFractionatorRuntime.GetTowerSummary()
        .OrderByDescending(pair => pair.Value.Hungry + pair.Value.Missing + pair.Value.Blocked)
        .ThenBy(pair => pair.Key.PlanetId).ThenBy(pair => pair.Key.ItemId)
        .Select(pair => {
            var count = pair.Value;
            string planet = GameMain.galaxy?.PlanetById(pair.Key.PlanetId)?.displayName ?? pair.Key.PlanetId.ToString();
            int stalled = count.Missing + count.Blocked;
            string status = count.Hungry > 0 ? string.Format("饥饿 {0}".Translate(), count.Hungry) : stalled > 0 ? string.Format("停滞 {0}".Translate(), stalled) : "运转正常".Translate();
            return new PageCard { Key = $"{pair.Key.PlanetId}-{pair.Key.ItemId}", ItemId = pair.Key.ItemId,
                Title = $"{planet} · {LDB.items.Select(pair.Key.ItemId)?.name}",
                Status = string.Format("{0} 座 · {1}".Translate(), count.Total, status),
                Detail = string.Format("FE.LivingTowersPage.Counts".Translate(), planet, count.Total, count.Hungry, count.Missing, count.Blocked) +
                    "FE.LivingTowersPage.Advice".Translate() };
        }).ToArray();
}
