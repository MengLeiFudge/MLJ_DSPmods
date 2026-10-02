using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>展示已经收到线索的终极任务，并在交付节点提交源点。</summary>
public sealed class UltimateTasksPage : CardBrowserPage {
    protected override string Title => "终极任务";
    protected override string Summary => "有些门，需要走过漫长的道路才能推开。";
    protected override IReadOnlyList<PageCard> ReadCards() {
        var result = new List<PageCard>();
        for (int branch = 0; branch < 2; branch++) {
            int step = ExplorationManager.GetUltimateStep(branch);
            bool ready = branch == 0 ? GrowthTechnologyManager.ExplorationLevel >= 10 : GrowthTechnologyManager.SubspaceCapacityLevel >= 10;
            if (!ready && step == 0) { continue; }
            string content = branch == 0 ? "exploration" : "capacity";
            ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Kind == ExplorationMarkKind.Ultimate && m.ContentId == content);
            result.Add(new PageCard { Key = content, ItemId = IFE记忆源点,
                Title = branch == 0 ? "长夜中的回声" : "没有尽头的库房",
                Status = step >= 6 ? "已经完成" : $"进程 {step + 1}/6",
                Progress = step / 6f,
                Detail = step >= 6
                    ? branch == 0 ? "中继已经重新连成网络。每当一处信号得到回应，远方就会有新的广播传来。"
                        : "库房重新接入了完整的亚空间。货架向视野之外延伸，再也看不见尽头。"
                    : (mark == null ? "新的应答正在传来，稍后可以在信号追踪中查看。" : ExplorationManager.GetSignalDescription(mark))
                        + (step == 4 ? $"\n\n源点交付：{ExplorationManager.GetUltimateInput(branch)}/2400\n抵达遗迹后，可以分批交付亚空间中的源点。" : ""),
                ActionLabel = "交付源点", Action = mark != null && step == 4 ? () => {
                    if (NebulaMultiplayerModAPI.IsClient) { NebulaMultiplayerModAPI.RequestTaskMemory(mark.Id); }
                    else if (GameMain.mainPlayer != null) {
                        ExplorationManager.SubmitTaskMemory(mark.Id, GameMain.mainPlayer.uPosition,
                            GameMain.mainPlayer.planetId, ExplorationManager.LocalPlayerGrounded);
                    }
                } : null });
        }
        return result;
    }
}
