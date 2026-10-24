using System;
using System.Collections.Generic;
using System.Linq;

namespace FE.Logic.Civilization;

/// <summary>解析目录的三种展示状态，由既有探索和恢复记录派生，不另存进度。</summary>
public enum AnalysisRecordState {
    /// <summary>当前可探索，尚未获得线索；隐藏样本和结果。</summary>
    Undiscovered,
    /// <summary>已有线索，使用实物样本的共享进度。</summary>
    Analyzing,
    /// <summary>已恢复，允许展示真实结果。</summary>
    Completed,
}

/// <summary>单项解析记录的安全展示投影；匿名项不携带样本或结果名称。</summary>
/// <param name="key">只用于稳定排序和选择的内容键，不能显示给玩家。</param>
/// <param name="state">由已有记录派生的状态。</param>
/// <param name="sampleItemId">有线索后公开的样本ID；匿名项为0。</param>
/// <param name="resultName">完成后公开的结果名称，其余为空。</param>
/// <param name="progress">共享进度0～9999，完成项为10000。</param>
public sealed class AnalysisRecordSnapshot(string key, AnalysisRecordState state, int sampleItemId,
    string resultName, int progress) {
    /// <summary>跨三态保持不变的稳定内容键。</summary>
    public string Key { get; } = key;
    /// <summary>当前可公开的状态。</summary>
    public AnalysisRecordState State { get; } = state;
    /// <summary>已知样本ID；0表示尚未发现，不能据此查询物品提示。</summary>
    public int SampleItemId { get; } = sampleItemId;
    /// <summary>完成后才公开的结果名称。</summary>
    public string ResultName { get; } = resultName;
    /// <summary>供同页进度条使用的万分比进度。</summary>
    public int Progress { get; } = progress;
}

public static partial class ResearchManager {
    /// <summary>按阶段和稳定身份列出解析三态；状态变化不改变排序，未知项不暴露实物或结果。</summary>
    /// <returns>当前可探索以及已经获得线索或完成的样本内容，不含普通配方、传说及科技资料。</returns>
    public static AnalysisRecordSnapshot[] GetAnalysisRecordSnapshots() {
        lock (Sync) {
            List<AnalysisRecordSnapshot> records = [];
            foreach (ResearchContentDefinition definition in ContentDefinitions.Values
                         .Where(definition => !definition.Id.StartsWith("technology:", StringComparison.Ordinal)
                             && !definition.Id.StartsWith("stage:", StringComparison.Ordinal) && IsSampleContent(definition.Id))
                         .OrderBy(definition => definition.Tier)
                         .ThenBy(definition => definition.Id, StringComparer.Ordinal)) {
                string id = definition.Id;
                bool completed = UnlockedContents.Contains(id);
                bool known = completed || ExploredContents.Contains(id);
                if (!known && (definition.Tier > CurrentMatrixStage || !IsContentExplorableUnsafe(id))) { continue; }
                int itemId = 0;
                if (known && !ExploredContentDirections.TryGetValue(id, out itemId)
                    && ContentDirections.TryGetValue(id, out IReadOnlyList<int> directions)) {
                    itemId = directions.FirstOrDefault();
                }
                AnalysisRecordState state = completed ? AnalysisRecordState.Completed
                    : known ? AnalysisRecordState.Analyzing : AnalysisRecordState.Undiscovered;
                records.Add(new AnalysisRecordSnapshot(id, state, itemId,
                    completed ? definition.DisplayName : string.Empty,
                    completed ? 10000 : known ? GetDirectionProgressUnsafe(itemId) : 0));
            }
            return records.ToArray();
        }
    }
}
