using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>同页按固定顺序展示解析三态：未发现为灰色匿名占位，解析中显示样本与进度，已恢复换色显示结果。</summary>
public sealed class AnalysisSamplesPage : CardBrowserPage {
    /// <summary>未发现占位色：不透露物品，只表示此处还有内容。</summary>
    private static readonly Color UndiscoveredTint = new(0.55f, 0.58f, 0.62f, 0.55f);

    protected override string Title => "解析样本".Translate();
    protected override string Summary => "FE.AnalysisSamplesPage.Summary".Translate();
    protected override string EmptyText => "FE.AnalysisSamplesPage.Empty".Translate();

    /// <summary>直接沿用接口顺序；状态变化不重排，条目位置保持不变。</summary>
    protected override IReadOnlyList<PageCard> ReadCards() => ResearchManager.GetAnalysisRecordSnapshots()
        .Select(CreateCard).ToArray();

    private static PageCard CreateCard(AnalysisRecordSnapshot record) {
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
                long stock = DataCenterInventory.GetModDataItemCount(record.SampleItemId)
                             + (GameMain.mainPlayer?.package?.GetItemCount(record.SampleItemId) ?? 0);
                string stockText = stock > 0
                    ? string.Format("手头库存：{0}（亚空间与背包）".Translate(), stock)
                    : "FE.AnalysisSamplesPage.NoStock".Translate();
                return new PageCard {
                    Key = record.Key, ItemId = record.SampleItemId, Tint = Blue, Progress = record.Progress / 10000f,
                    Title = name, Status = string.Format("解析进度 {0:0.#}%".Translate(), record.Progress / 100f),
                    Detail = string.Format("FE.AnalysisSamplesPage.AnalyzingDetail".Translate(), name, stockText,
                        record.Progress / 100f),
                };
            default:
                // 匿名占位：不显示图标与名称，进度条保持空轨道，保证三态版式一致。
                return new PageCard {
                    Key = record.Key, ShowIcon = false, Tint = UndiscoveredTint, Progress = 0f,
                    Title = "？？？", Status = "尚未发现".Translate(),
                    Detail = "FE.AnalysisSamplesPage.UndiscoveredDetail".Translate(),
                };
        }
    }
}
