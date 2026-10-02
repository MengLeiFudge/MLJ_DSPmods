using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>根据已收到的信号与未完成的记录给出可执行目标。</summary>
public sealed class CurrentGoalPage : CardBrowserPage {
    protected override string Title => "当前目标";
    protected override string Summary => "还有几件事，让伊卡洛斯放不下。";

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        var result = new List<PageCard>();
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(ExplorationManager.IsKeySignal);
        if (mark != null) {
            result.Add(new PageCard {
                Key = "story", Title = ExplorationManager.GetSignalName(mark), ItemId = IFE残片,
                Status = GameMain.galaxy?.PlanetById(mark.PlanetId)?.displayName,
                Detail = ExplorationManager.GetSignalDescription(mark),
                ActionLabel = "查看信号", Action = () => MainWindow.NavigateToPage("探索", 0),
            });
        } else if (!ExplorationManager.HasProducedFirstMatrix()) {
            result.Add(new PageCard {
                Key = "matrix", Title = "第一枚矩阵", ItemId = I电磁矩阵, Status = "继续开拓",
                Detail = "手头的记录暂时整理完了，熟悉的开拓任务还在继续。\n\n铺好电网，接通矿石产线，再让研究站制造出第一枚电磁矩阵。那些陌生装置可以先留在身旁，伊卡洛斯总觉得，它们还没有说完自己的故事。",
            });
        }
        int samples = ResearchManager.GetDirectionSnapshots().Count(s => s.ItemId != IFE记忆源点 && s.HasPendingContent);
        if (samples > 0) {
            result.Add(new PageCard {
                Key = "samples", Title = "物质里的秘密", ItemId = IFE基础解析塔, Status = $"{samples} 种样本待解析",
                Detail = "将某些物品送进解析塔，也许就能发现一些奇特的东西……\n\n带回来的记录上，几种样本被反复标记。可以先挑一种，用传送带从解析塔侧面送进去，再把另一侧流出的物品接回来循环。\n\n塔内偶尔闪过的光，似乎正与记录里的纹路一点点重合。",
                ActionLabel = "查看解析样本", Action = () => MainWindow.NavigateToPage("研究", 0),
            });
        }
        int stage = Enumerable.Range(1, ResearchManager.CurrentMatrixStage)
            .FirstOrDefault(s => !ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(s)));
        if (stage > 0) {
            result.Add(new PageCard {
                Key = "stage", Title = "拼合遗失的记忆", ItemId = IFE记忆源点,
                Status = LDB.items.Select(ResearchManager.GetStageMatrix(stage))?.name,
                Detail = "新矩阵让一些模糊的记录变得可以辨认，缺失的部分却仍然连不起来。\n\n源点中保存着那些生命留下的记忆。把它送进基础解析塔，或许能替这些记录补上最后几笔。等完整的结构图出现后，就可以在科技树中继续研究了。",
                ActionLabel = "查看阶段恢复", Action = () => MainWindow.NavigateToPage("研究", 1),
            });
        }
        return result;
    }
}
