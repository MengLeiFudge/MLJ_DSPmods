using System.Collections.Generic;
using System.Linq;

namespace SaveDataExporter;

/// <summary>
/// 将导出数据集构建为固定合同的工作表模型。
/// </summary>
internal static class ExportSheetBuilder {
    /// <summary>
    /// 四项指标的中文列名，顺序对应 ItemStatistics 的 Actual/Reference Production/Consumption。
    /// </summary>
    private static readonly string[] MetricNames = ["实际产量", "理论产量", "实际消耗", "理论消耗"];

    /// <summary>
    /// 构建宽表“星球信息导出模板1”：以恒星为行，每颗星球占一组列，仅使用首个目标物品。
    /// </summary>
    /// <param name="dataset">一次导出的数据集。</param>
    /// <returns>宽表工作表。</returns>
    public static XlsxSheet BuildWideSheet(ExportDataset dataset) {
        ExportItem firstItem = dataset.Items[0];
        List<XlsxRow> rows = [
            Row("导出时间", dataset.ExportTime.ToString("yyyy-MM-dd-HH-mm-ss"), "", "", "", "", "导出数据的时间"),
            Row("导出目标", firstItem.Name, "", "", "", "", "导出统计信息的目标产物，这个模板应该只导出单种比较好"),
            Row("统计周期", dataset.TimeLevelName, "", "", "", "", "导出的统计周期，参考游戏内统计面板的统计周期"),
            Row("导出内容", string.Join("、", MetricNames), "", "", "", "", "导出的统计信息，主要是这四种"),
        ];

        List<object> header = ["星系"];
        for (int i = 1; i <= 6; i++) {
            header.Add($"星球{i}");
            header.AddRange(MetricNames);
        }
        rows.Add(Row());
        rows.Add(new XlsxRow(header));

        foreach (IGrouping<string, PlanetExportRow> starGroup in dataset.Planets.GroupBy(row => row.StarName)) {
            List<object> cells = [starGroup.Key];
            foreach (PlanetExportRow planet in starGroup.OrderBy(row => row.PlanetIndex).Take(6)) {
                ItemStatistics stats = planet.Statistics[firstItem.ItemId];
                cells.Add(planet.PlanetName);
                cells.Add(stats.ActualProduction);
                cells.Add(stats.ReferenceProduction);
                cells.Add(stats.ActualConsumption);
                cells.Add(stats.ReferenceConsumption);
            }

            while (cells.Count < header.Count) {
                int planetBlockStart = cells.Count % 5;
                cells.Add(planetBlockStart == 1 ? "/" : "");
            }
            rows.Add(new XlsxRow(cells));
        }

        return new XlsxSheet("星球信息导出模板1", rows);
    }

    /// <summary>
    /// 构建规范化“星球信息导出模板2”：每行一颗星球，按目标物品展开四项指标。
    /// </summary>
    /// <param name="dataset">一次导出的数据集。</param>
    /// <returns>规范化工作表。</returns>
    public static XlsxSheet BuildNormalizedSheet(ExportDataset dataset) {
        List<XlsxRow> rows = [
            Row("导出时间", dataset.ExportTime.ToString("yyyy-MM-dd-HH-mm-ss"), "", "导出数据的时间"),
            Row("导出目标", string.Join("、", dataset.Items.Select(item => item.Name)), "", "导出统计信息的目标产物，可多选"),
            Row("统计周期", dataset.TimeLevelName, "", "导出的统计周期，参考游戏内统计面板的统计周期"),
            Row("导出内容", string.Join("、", MetricNames), "", "导出的统计信息，如前"),
        ];

        List<object> header = ["星系", "星球"];
        foreach (ExportItem item in dataset.Items) {
            header.Add($"{item.Name}-实际产量");
            header.Add($"{item.Name}-理论产量");
            header.Add($"{item.Name}-实际消耗");
            header.Add($"{item.Name}-理论消耗");
        }

        rows.Add(Row());
        rows.Add(new XlsxRow(header));
        foreach (PlanetExportRow planet in dataset.Planets) {
            List<object> cells = [planet.StarName, planet.PlanetName];
            foreach (ExportItem item in dataset.Items) {
                ItemStatistics stats = planet.Statistics[item.ItemId];
                cells.Add(stats.ActualProduction);
                cells.Add(stats.ReferenceProduction);
                cells.Add(stats.ActualConsumption);
                cells.Add(stats.ReferenceConsumption);
            }
            rows.Add(new XlsxRow(cells));
        }

        return new XlsxSheet("星球信息导出模板2", rows);
    }

    /// <summary>
    /// 将可变参数包装为单行对象，简化工作表元数据行构造。
    /// </summary>
    /// <param name="cells">从左到右的单元格值序列。</param>
    /// <returns>包含指定单元格值的行。</returns>
    private static XlsxRow Row(params object[] cells) {
        return new XlsxRow(cells);
    }
}
