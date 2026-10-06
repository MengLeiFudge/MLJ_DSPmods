using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>展示源点能够恢复的阶段记录，以及当前献祭增益。</summary>
public sealed class StageRecoveryPage : CardBrowserPage {
    protected override string Title => "阶段恢复".Translate();
    protected override string Summary => "FE.StageRecoveryPage.Summary".Translate();
    protected override string EmptyText => "FE.StageRecoveryPage.Empty".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() {
        int[] matrices = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵];
        var result = new List<PageCard>();
        for (int stage = 1; stage <= ResearchManager.CurrentMatrixStage; stage++) {
            bool restored = ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(stage));
            float progress = ResearchManager.GetStageRecoveryProgress(stage);
            result.Add(new PageCard { Key = stage.ToString(), Title = LDB.items.Select(matrices[stage - 1])?.name,
                ItemId = matrices[stage - 1], Status = restored ? "资料已恢复".Translate()
                    : string.Format("FE.StageRecoveryPage.Progress".Translate(), progress),
                Progress = progress,
                Detail = restored
                    ? "FE.StageRecoveryPage.Restored".Translate()
                    : string.Format("FE.StageRecoveryPage.Recovering".Translate(), progress) });
        }
        if (ResearchManager.SacrificeUnlocked) {
            result.Add(new PageCard { Key = "offering", Title = "记忆献祭".Translate(), ItemId = IFE记忆源点,
                Status = string.Format("当前相对加成 +{0:P0}".Translate(), ResearchManager.MemorySuccessBoost),
                Detail = string.Format("FE.StageRecoveryPage.Offering".Translate(), ResearchManager.CurrentMemoryOffering) });
        }
        return result;
    }
}
