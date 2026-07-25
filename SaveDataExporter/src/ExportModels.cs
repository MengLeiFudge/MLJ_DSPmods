using System;
using System.Collections.Generic;

namespace SaveDataExporter;

/// <summary>
/// 单个导出目标物品的标识与显示名。
/// </summary>
internal sealed class ExportItem {
    /// <summary>
    /// 使用物品 ID 与显示名创建导出目标。
    /// </summary>
    /// <param name="itemId">游戏物品原型 ID。</param>
    /// <param name="name">写入工作表的物品显示名。</param>
    public ExportItem(int itemId, string name) {
        ItemId = itemId;
        Name = name;
    }

    /// <summary>
    /// 游戏物品原型 ID。
    /// </summary>
    public int ItemId { get; }

    /// <summary>
    /// 写入工作表的物品显示名。
    /// </summary>
    public string Name { get; }
}

/// <summary>
/// 一次导出的完整数据集，供工作表构建使用。
/// </summary>
internal sealed class ExportDataset {
    /// <summary>
    /// 创建一次导出的聚合数据集。
    /// </summary>
    /// <param name="exportTime">导出时刻。</param>
    /// <param name="timeLevelName">统计周期显示名。</param>
    /// <param name="items">已解析的目标物品列表。</param>
    /// <param name="planets">按星系遍历顺序收集的星球统计行。</param>
    public ExportDataset(
        DateTime exportTime,
        string timeLevelName,
        IReadOnlyList<ExportItem> items,
        IReadOnlyList<PlanetExportRow> planets) {
        ExportTime = exportTime;
        TimeLevelName = timeLevelName;
        Items = items;
        Planets = planets;
    }

    /// <summary>
    /// 导出时刻。
    /// </summary>
    public DateTime ExportTime { get; }

    /// <summary>
    /// 统计周期显示名，如“10分钟”。
    /// </summary>
    public string TimeLevelName { get; }

    /// <summary>
    /// 已解析的目标物品列表。
    /// </summary>
    public IReadOnlyList<ExportItem> Items { get; }

    /// <summary>
    /// 按星系遍历顺序收集的星球统计行。
    /// </summary>
    public IReadOnlyList<PlanetExportRow> Planets { get; }
}

/// <summary>
/// 单颗星球上全部目标物品的统计行。
/// </summary>
internal sealed class PlanetExportRow {
    /// <summary>
    /// 创建星球维度的导出行。
    /// </summary>
    /// <param name="starName">所属恒星显示名。</param>
    /// <param name="planetName">星球显示名。</param>
    /// <param name="planetIndex">恒星内星球下标，用于宽表排序与列位。</param>
    /// <param name="statistics">目标物品 ID 到四项指标的映射。</param>
    public PlanetExportRow(
        string starName,
        string planetName,
        int planetIndex,
        IReadOnlyDictionary<int, ItemStatistics> statistics) {
        StarName = starName;
        PlanetName = planetName;
        PlanetIndex = planetIndex;
        Statistics = statistics;
    }

    /// <summary>
    /// 所属恒星显示名。
    /// </summary>
    public string StarName { get; }

    /// <summary>
    /// 星球显示名。
    /// </summary>
    public string PlanetName { get; }

    /// <summary>
    /// 恒星内星球下标，用于宽表排序与列位。
    /// </summary>
    public int PlanetIndex { get; }

    /// <summary>
    /// 目标物品 ID 到四项指标的映射。
    /// </summary>
    public IReadOnlyDictionary<int, ItemStatistics> Statistics { get; }
}

/// <summary>
/// 单个物品在指定统计周期下的产量与消耗指标。
/// </summary>
internal readonly struct ItemStatistics {
    /// <summary>
    /// 全零指标，用于未建厂或无统计数据的星球。
    /// </summary>
    public static readonly ItemStatistics Zero = new(0, 0, 0, 0);

    /// <summary>
    /// 创建四项生产/消耗指标。
    /// </summary>
    /// <param name="actualProduction">实际产量（每分钟速率）。</param>
    /// <param name="referenceProduction">理论产量。</param>
    /// <param name="actualConsumption">实际消耗（每分钟速率）。</param>
    /// <param name="referenceConsumption">理论消耗。</param>
    public ItemStatistics(
        double actualProduction,
        double referenceProduction,
        double actualConsumption,
        double referenceConsumption) {
        ActualProduction = actualProduction;
        ReferenceProduction = referenceProduction;
        ActualConsumption = actualConsumption;
        ReferenceConsumption = referenceConsumption;
    }

    /// <summary>
    /// 实际产量（每分钟速率）。
    /// </summary>
    public double ActualProduction { get; }

    /// <summary>
    /// 理论产量。
    /// </summary>
    public double ReferenceProduction { get; }

    /// <summary>
    /// 实际消耗（每分钟速率）。
    /// </summary>
    public double ActualConsumption { get; }

    /// <summary>
    /// 理论消耗。
    /// </summary>
    public double ReferenceConsumption { get; }
}

/// <summary>
/// 导出文件命名模式。
/// </summary>
internal enum OutputFileNameMode {
    /// <summary>
    /// 文件名包含导出时间，每次生成新文件。
    /// </summary>
    TimestampedNewFile = 0,

    /// <summary>
    /// 固定为 SaveDataExporter_&lt;存档名&gt;.xlsx，同名覆盖。
    /// </summary>
    SaveNameOverwrite = 1,
}
