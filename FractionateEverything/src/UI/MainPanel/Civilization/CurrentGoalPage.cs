using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>根据已收到的信号与未完成的记录给出可执行目标。</summary>
public sealed class CurrentGoalPage : CardBrowserPage {
    protected override string Title => "当前目标".Translate();
    protected override string Summary => "FE.CurrentGoalPage.Summary".Translate();
    protected override string EmptyText => "FE.CurrentGoalPage.Empty".Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        var result = new List<PageCard>();
        ExplorationMark[] keySignals = ExplorationManager.GetActiveMarks().Where(ExplorationManager.IsKeySignal).ToArray();
        foreach (ExplorationMark mark in keySignals) {
            result.Add(new PageCard {
                Key = $"signal-{mark.Id}", Title = ExplorationManager.GetSignalName(mark), ItemId = IFE残片,
                Status = GameMain.galaxy?.PlanetById(mark.PlanetId)?.displayName,
                Detail = ExplorationManager.GetSignalDescription(mark),
                ActionLabel = (ExplorationDetector.IsTracking(mark.Id) ? "取消标注" : "标注位置").Translate(),
                Action = mark.Kind == ExplorationMarkKind.FirstRelic ? null : () => ExplorationDetector.ToggleTracking(mark.Id),
            });
        }
        if (keySignals.Length > 0) {
            // 关键信号优先，其余目标仍然列出。
        } else if (ExplorationManager.StoryWaitingForTechnology) {
            result.Add(new PageCard {
                Key = "technology", Title = "沉默的频段".Translate(), ItemId = IFE残片, Status = "继续推进科技".Translate(),
                Detail = "FE.CurrentGoalPage.Technology".Translate(),
            });
        } else if (!ExplorationManager.HasProducedFirstMatrix()) {
            result.Add(new PageCard {
                Key = "matrix", Title = "第一枚矩阵".Translate(), ItemId = I电磁矩阵, Status = "继续开拓".Translate(),
                Detail = "FE.CurrentGoalPage.FirstMatrix".Translate(),
            });
        }
        int samples = ResearchManager.GetDirectionSnapshots().Count(s => s.ItemId != IFE记忆源点 && s.HasPendingContent);
        if (samples > 0) {
            result.Add(new PageCard {
                Key = "samples", Title = "物质里的秘密".Translate(), ItemId = IFE基础解析塔, Status = string.Format("{0} 种样本待解析".Translate(), samples),
                Detail = "FE.CurrentGoalPage.Analysis".Translate(),
                ActionLabel = "查看解析样本".Translate(), Action = () => MainWindow.NavigateToPage("研究", 0),
            });
        }
        // 取得源点之前，不提示玩家去拼合阶段记录。
        bool hasMemorySource = ExplorationManager.StoryCompleted >= 4
                               || DataCenterInventory.GetModDataItemCount(IFE记忆源点) > 0;
        int stage = hasMemorySource ? Enumerable.Range(1, ResearchManager.CurrentMatrixStage)
            .FirstOrDefault(s => !ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(s))) : 0;
        if (stage > 0) {
            result.Add(new PageCard {
                Key = "stage", Title = "拼合遗失的记忆".Translate(), ItemId = IFE记忆源点,
                Status = LDB.items.Select(ResearchManager.GetStageMatrix(stage))?.name,
                Detail = "FE.CurrentGoalPage.Stage".Translate(),
                ActionLabel = "查看阶段恢复".Translate(), Action = () => MainWindow.NavigateToPage("研究", 1),
            });
        }
        return result;
    }
}
