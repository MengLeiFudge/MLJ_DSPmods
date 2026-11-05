using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>任务与塔型配方目录共用的解析三态展示；未发现记录不公开物品身份。</summary>
internal static class AnalysisRecordCards {
    private static readonly Color UndiscoveredTint = new(0.55f, 0.58f, 0.62f, 0.55f);

    /// <summary>从领域的安全快照生成详情及定向解析命令。</summary>
    /// <param name="record">已过滤公开范围的解析记录。</param>
    /// <returns>不包含未授权结果信息的卡片。</returns>
    internal static PageCard Create(AnalysisRecordSnapshot record) {
        switch (record.State) {
            case AnalysisRecordState.Completed:
                return new PageCard {
                    Key = record.Key, ItemId = record.SampleItemId, Tint = Gold, Progress = 1f,
                    Title = record.ResultName, Status = "已恢复".Translate(),
                    Detail = string.Format("FE.AnalysisSamplesPage.CompletedDetail".Translate(), record.ResultName,
                        LDB.items.Select(record.SampleItemId)?.name ?? "未知样本".Translate()),
                };
            case AnalysisRecordState.Analyzing:
                string name = LDB.items.Select(record.SampleItemId)?.name ?? "未知样本".Translate();
                long stock = PlayerInventoryAccess.GetItemTotalCount(record.SampleItemId);
                return new PageCard {
                    Key = record.Key, ItemId = record.SampleItemId, Tint = Blue, Progress = record.Progress / 10000f,
                    Title = string.IsNullOrEmpty(record.ResultName) ? name : record.ResultName,
                    Status = string.Format("解析进度 {0:0.#}%".Translate(), record.Progress / 100f),
                    Detail = string.Format("FE.AnalysisSamplesPage.AnalyzingDetail".Translate(), name,
                        string.Format("FE.Analysis.AvailableStock".Translate(), stock), record.Progress / 100f),
                    ActionLabel = (ResearchManager.IsAnalysisTarget(record.SampleItemId, record.Key)
                        ? "FE.Analysis.CancelTarget" : "FE.Analysis.SetTarget").Translate(),
                    Action = !GrowthTechnologyManager.DirectedAnalysis ? null : () => AnalysisTargetSync.SetTarget(record.SampleItemId,
                        ResearchManager.IsAnalysisTarget(record.SampleItemId, record.Key) ? "" : record.Key),
                };
            default:
                return new PageCard {
                    Key = record.Key, ShowIcon = false, Tint = UndiscoveredTint, Progress = 0f,
                    Title = "？？？", Status = "尚未发现".Translate(),
                    Detail = "FE.AnalysisSamplesPage.UndiscoveredDetail".Translate(),
                };
        }
    }
}
