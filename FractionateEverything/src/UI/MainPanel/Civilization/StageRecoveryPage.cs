using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>展示源点能够恢复的阶段记录，以及当前献祭增益。</summary>
public sealed class StageRecoveryPage : CardBrowserPage {
    protected override string Title => "阶段恢复";
    protected override string Summary => "矩阵点亮时代，记忆把遗失的知识带回眼前。";
    protected override IReadOnlyList<PageCard> ReadCards() {
        int[] matrices = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵];
        var result = new List<PageCard>();
        for (int stage = 1; stage <= ResearchManager.CurrentMatrixStage; stage++) {
            bool restored = ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(stage));
            result.Add(new PageCard { Key = stage.ToString(), Title = LDB.items.Select(matrices[stage - 1])?.name,
                ItemId = matrices[stage - 1], Status = restored ? "资料已恢复" : "尚有残缺",
                Detail = restored
                    ? "散落的片段已经拼合成完整的结构图。这份知识可以交给研究站继续研究，对应的升级已经出现在科技树中。"
                    : $"记录能被新矩阵辨认，内容却仍有缺口。将源点送入基础解析塔，或许就能找回缺失的部分。\n\n已经投入：{ResearchManager.GetStageMemoryInputs(stage)} 份源点\n\n恢复需要一些耐心，每份源点都可能带来新的回应。" });
        }
        if (ResearchManager.SacrificeUnlocked) {
            result.Add(new PageCard { Key = "offering", Title = "记忆献祭", ItemId = IFE记忆源点,
                Status = $"当前相对加成 +{ResearchManager.MemorySuccessBoost:P0}",
                Detail = $"这一分钟已经投入：{ResearchManager.CurrentMemoryOffering} 份源点\n\n源点留下的记忆正在帮助生命塔运转。每分钟的供给会决定下一分钟的成功率加成，最高相对提升75%。\n\n解析塔会先拼合尚未恢复的阶段记录。记录完整之后，继续供给源点就可以维持加成；停止供给后，加成会在后续分钟消退。" });
        }
        return result;
    }
}
