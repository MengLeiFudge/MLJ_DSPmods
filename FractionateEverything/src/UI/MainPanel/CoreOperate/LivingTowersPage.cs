using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.CoreOperate;

/// <summary>按塔体类型汇总已存在的生命塔运行状态。</summary>
public sealed class LivingTowersPage : CardBrowserPage {
    protected override string Title => "生命塔";
    protected override string Summary => $"记忆正在流动。当前成功率相对加成 +{ResearchManager.MemorySuccessBoost:P0}。";
    protected override IReadOnlyList<PageCard> ReadCards() {
        var summary = AdvancedFractionatorRuntime.GetTowerSummary();
        var result = new List<PageCard>();
        foreach (int itemId in new[] { IFE高级交互塔, IFE高级矿物塔, IFE高级转化塔 }) {
            summary.TryGetValue(itemId, out var count);
            result.Add(new PageCard { Key = itemId.ToString(), Title = LDB.items.Select(itemId)?.name, ItemId = itemId,
                Status = $"已放置 {count.Total}", Detail = $"饥饿：{count.Hungry}\n缺料：{count.Missing}\n堵塞：{count.Blocked}\n\n" +
                    "营养充足时塔体逐渐恢复，饥饿时耐久下降。优先检查矩阵供养，再检查配方所需的全部输入和输出传送带。\n\n缓存容量按20堆计算。主消耗物不足一轮时，产物尾料会继续流出。" });
        }
        return result;
    }
}
