using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>显示一次性能力的任务条件、实际进度与奖励，完成由主机自动结算。</summary>
public sealed class UltimateTasksPage : CardBrowserPage {
    protected override string Title => "终极任务".Translate();
    protected override string Summary => "FE.UltimateTasksPage.Summary".Translate();
    protected override string EmptyText => "FE.UltimateTasksPage.Empty".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() {
        return AbilityTasks.TechnologyIds.Where(id => GrowthTechnologyManager.IsTechnologyDiscovered(id)).Select(id => {
            var requirements = AbilityTasks.GetRequirements(id);
            bool complete = GameMain.history.TechUnlocked(id);
            return new PageCard {
                Key = $"ability:{id}", ItemId = IFE记忆源点, Title = LDB.techs.Select(id).name,
                Status = (complete ? "已经完成" : "FE.Task.InProgress").Translate(),
                Progress = complete ? 1f : (float)requirements.Average(r => System.Math.Min(1d, r.Current / r.Required)),
                Detail = AbilityTasks.GetDescription(id),
            };
        }).ToArray();
    }
}
