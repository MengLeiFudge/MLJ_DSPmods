using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>按文明主线或普通遗迹组织任务；状态切换只读取实际进度，不结算奖励。</summary>
/// <param name="civilization">是否展示主线、阶段恢复与能力任务。</param>
public sealed class TaskJournalPage(bool civilization) : CardBrowserPage {
    protected override string Title => (civilization ? "文明寻踪" : "遗迹探索").Translate();
    protected override string Summary {
        get {
            if (civilization) { return "FE.Tasks.CivilizationSummary".Translate(); }
            PlanetData planet = GameMain.localPlanet;
            if (planet == null) { return "FE.Tasks.RelicsSummary".Translate(); }
            ExplorationManager.GetSilentProgress(planet.id, out int found, out int total);
            int starFound = 0, starTotal = 0;
            foreach (PlanetData other in planet.star.planets) {
                ExplorationManager.GetSilentProgress(other.id, out int collected, out int known);
                starFound += collected; starTotal += known;
            }
            return string.Format("FE.SignalTrackingPage.CollectionSummary".Translate(), found, total, starFound, starTotal);
        }
    }
    protected override IReadOnlyList<string> Filters => ["FE.Task.InProgress", "已经完成"];
    protected override string EmptyText => (SelectedFilter == 0 ? "FE.Tasks.NoActive" : "FE.Tasks.NoCompleted").Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        bool completed = SelectedFilter == 1;
        var cards = new List<PageCard>();
        if (completed) {
            foreach (ExplorationRecord record in ExplorationManager.GetRecords()
                         .Where(r => ExplorationManager.IsKeySignal(r.Mark) == civilization)) {
                cards.Add(new PageCard {
                    Key = $"signal:{record.Mark.Id}", Title = ExplorationManager.GetSignalName(record.Mark),
                    ItemId = record.Mark.ItemId > 0 && !civilization ? record.Mark.ItemId : IFE残片,
                    Status = GameMain.galaxy?.PlanetById(record.Mark.PlanetId)?.displayName,
                    Detail = record.Text + (record.Mark.Kind == ExplorationMarkKind.Sacrifice && ResearchManager.SacrificeUnlocked
                        ? "\n\n" + string.Format("当前相对加成 +{0:P0}".Translate(), ResearchManager.MemorySuccessBoost)
                            + "\n" + string.Format("FE.StageRecoveryPage.Offering".Translate(), ResearchManager.CurrentMemoryOffering) : ""),
                    Progress = 1f,
                });
            }
        } else {
            foreach (ExplorationMark mark in ExplorationManager.GetActiveMarks()
                         .Where(m => ExplorationManager.IsKeySignal(m) == civilization)
                         .OrderBy(m => m.Kind is ExplorationMarkKind.Story or ExplorationMarkKind.FirstRelic
                             or ExplorationMarkKind.PlanetDetector ? 0 : 1)
                         .ThenBy(m => m.PlanetId == GameMain.localPlanet?.id ? 0 : 1)) {
                SignalChallengeView challenge = ExplorationManager.GetChallenge(mark.Id);
                cards.Add(new PageCard {
                    Key = $"signal:{mark.Id}", Title = ExplorationManager.GetSignalName(mark), ItemId = IFE残片,
                    Status = GameMain.galaxy?.PlanetById(mark.PlanetId)?.displayName,
                    Detail = ExplorationManager.GetSignalDescription(mark),
                    Progress = challenge == null ? -1f : (float)System.Math.Min(1d, challenge.Progress / (double)System.Math.Max(1, challenge.Required)),
                    ActionLabel = (ExplorationDetector.IsTracking(mark.Id) ? "取消标注" : "标注位置").Translate(),
                    Action = mark.Kind == ExplorationMarkKind.FirstRelic ? null : () => ExplorationDetector.ToggleTracking(mark.Id),
                    SecondaryLabel = "FE.Challenge.SwitchRoute".Translate(),
                    SecondaryAction = ExplorationManager.CanSwitchChallenge(mark.Id) ? () => ChallengeNetwork.RequestRoute(mark.Id) : null,
                });
            }
        }
        if (civilization) { AddCivilizationTasks(cards, completed); }
        return cards;
    }

    /// <summary>将已有研究与科技条件投影到同一任务目录，完成项保留详情。</summary>
    private static void AddCivilizationTasks(List<PageCard> cards, bool completed) {
        if (!completed && !cards.Any(c => c.Key.StartsWith("signal:"))) {
            if (ExplorationManager.StoryWaitingForTechnology) {
                cards.Add(new PageCard {
                    Key = "technology", Title = "沉默的频段".Translate(), ItemId = IFE残片,
                    Status = "继续推进科技".Translate(), Detail = "FE.CurrentGoalPage.Technology".Translate(),
                });
            } else if (!ExplorationManager.HasProducedFirstMatrix()) {
                cards.Add(new PageCard {
                    Key = "matrix", Title = "第一枚矩阵".Translate(), ItemId = I电磁矩阵,
                    Status = "继续开拓".Translate(), Detail = "FE.CurrentGoalPage.FirstMatrix".Translate(),
                });
            }
        }
        bool hasMemory = ExplorationManager.StoryCompleted >= 4
            || DataCenterInventory.GetModDataItemCount(IFE记忆源点) > 0;
        for (int stage = 1; stage <= ResearchManager.CurrentMatrixStage; stage++) {
            bool restored = ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(stage));
            if (restored != completed || !restored && !hasMemory) { continue; }
            int matrix = ResearchManager.GetStageMatrix(stage);
            float progress = ResearchManager.GetStageRecoveryProgress(stage);
            cards.Add(new PageCard {
                Key = $"stage:{stage}", Title = string.Format("FE.Tasks.StageTitle".Translate(), LDB.items.Select(matrix)?.name),
                ItemId = matrix, Status = (restored ? "资料已恢复" : "阶段恢复").Translate(), Progress = progress,
                Detail = restored ? "FE.StageRecoveryPage.Restored".Translate()
                    : string.Format("FE.StageRecoveryPage.Recovering".Translate(), progress),
            });
        }
        foreach (AnalysisRecordSnapshot record in ResearchManager.GetAnalysisRecordSnapshots()
                     .Where(r => r.Key.StartsWith("trait:") && r.State != AnalysisRecordState.Undiscovered
                         && (r.State == AnalysisRecordState.Completed) == completed)) {
            cards.Add(AnalysisRecordCards.Create(record));
        }
        foreach (int id in AbilityTasks.TechnologyIds.Where(GrowthTechnologyManager.IsTechnologyDiscovered)) {
            bool unlocked = GameMain.history.TechUnlocked(id);
            if (unlocked != completed) { continue; }
            var requirements = AbilityTasks.GetRequirements(id);
            cards.Add(new PageCard {
                Key = $"ability:{id}", Title = LDB.techs.Select(id).name, ItemId = IFE记忆源点,
                Status = (unlocked ? "已经完成" : "FE.Task.InProgress").Translate(),
                Progress = unlocked ? 1f : (float)requirements.Average(r => System.Math.Min(1d, r.Current / r.Required)),
                Detail = AbilityTasks.GetDescription(id),
            });
        }
    }
}
