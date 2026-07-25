using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaveDataExporter;

/// <summary>
/// 从已载入存档采集星系/星球维度生产统计，并解析导出目标物品。
/// </summary>
internal static class SaveStatisticsCollector {
    /// <summary>
    /// 各统计周期档位的时间跨度除数，用于将统计池累计值转为每分钟速率。
    /// [1分钟/1d, 10分钟/10d, 1小时/60d, 10小时/600d, 100小时/6000d]
    /// </summary>
    private static readonly double[] RateDivisors = [1d, 10d, 60d, 600d, 6000d];

    /// <summary>
    /// 各统计周期的中文显示名，索引 0-4 与配置 TimeLevel 一致。
    /// </summary>
    private static readonly string[] TimeLevelNames = ["1分钟", "10分钟", "1小时", "10小时", "100小时"];

    /// <summary>
    /// 支持的统计周期档位数，与配置 TimeLevel 合法下标上界+1 对齐。
    /// </summary>
    public static int TimeLevelCount => TimeLevelNames.Length;

    /// <summary>
    /// 尝试获取可导出的已载入存档数据。
    /// </summary>
    /// <param name="gameData">成功时返回当前游戏数据。</param>
    /// <returns>存档已运行且生产统计池可用时为 true。</returns>
    public static bool TryGetLoadedGameData(out GameData gameData) {
        gameData = GameMain.data;
        return GameMain.isRunning
               && gameData?.galaxy != null
               && gameData.statistics?.production?.factoryStatPool != null;
    }

    /// <summary>
    /// 刷新各工厂参考速度，使理论产量/消耗与游戏统计面板一致。
    /// </summary>
    /// <param name="gameData">当前游戏数据。</param>
    public static void RefreshReferenceSpeeds(GameData gameData) {
        ProductionExtraInfoCalculator calculator = gameData.statistics.production.extraInfoCalculator;
        for (int i = 0; i < gameData.factoryCount; i++) {
            calculator.AddFactory(i);
        }
        calculator.CalculateImmediately();
    }

    /// <summary>
    /// 将配置字符串解析为去重后的目标物品列表。
    /// </summary>
    /// <param name="rawValue">逗号等分隔的物品 ID 或名称。</param>
    /// <returns>可导出的目标物品；无法解析的 token 被跳过。</returns>
    public static List<ExportItem> ResolveTargetItems(string rawValue) {
        IEnumerable<string> tokens = (rawValue ?? "")
            .Split([',', ';', '，', '；', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0);

        List<ExportItem> items = [];
        HashSet<int> seen = [];
        foreach (string token in tokens) {
            ItemProto proto = ResolveItem(token);
            if (proto == null || !seen.Add(proto.ID)) {
                continue;
            }
            items.Add(new ExportItem(proto.ID, GetItemName(proto)));
        }

        return items;
    }

    /// <summary>
    /// 遍历星系与星球，构建一次导出的完整数据集。
    /// </summary>
    /// <param name="gameData">当前游戏数据。</param>
    /// <param name="items">已解析目标物品。</param>
    /// <param name="timeLevel">统计周期下标（0-4）。</param>
    /// <returns>供工作表构建使用的数据集。</returns>
    public static ExportDataset BuildDataset(GameData gameData, IReadOnlyList<ExportItem> items, int timeLevel) {
        List<PlanetExportRow> planets = [];
        GalaxyData galaxy = gameData.galaxy;
        for (int starIndex = 0; starIndex < galaxy.starCount; starIndex++) {
            StarData star = galaxy.stars[starIndex];
            if (star == null) {
                continue;
            }

            for (int planetIndex = 0; planetIndex < star.planetCount; planetIndex++) {
                PlanetData planet = star.planets[planetIndex];
                if (planet == null) {
                    continue;
                }

                Dictionary<int, ItemStatistics> statistics = [];
                foreach (ExportItem item in items) {
                    statistics[item.ItemId] = GetPlanetItemStatistics(gameData, planet, item.ItemId, timeLevel);
                }

                planets.Add(new PlanetExportRow(
                    star.displayName,
                    planet.displayName,
                    planetIndex,
                    statistics));
            }
        }

        return new ExportDataset(
            DateTime.Now,
            TimeLevelNames[timeLevel],
            items,
            planets);
    }

    /// <summary>
    /// 将单个 token（物品 ID 或名称）解析为 ItemProto。
    /// 先尝试按 ID 整数查找；未命中时对已知名称域做归一化匹配。
    /// </summary>
    /// <param name="token">去掉分隔符后的单个物品标识。</param>
    /// <returns>匹配的原型，未找到时返回 null。</returns>
    private static ItemProto ResolveItem(string token) {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) {
            return LDB.items.Select(itemId);
        }

        string normalizedToken = NormalizeName(token);
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item == null) {
                continue;
            }

            if (NormalizeName(item.name) == normalizedToken
                || NormalizeName(item.Name) == normalizedToken
                || NormalizeName(GetItemName(item)) == normalizedToken) {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// 去除非换行空白（包括不间断空格 \u00a0）、去除外部空格并转小写，
    /// 用于物品名称的容错比较。
    /// </summary>
    /// <param name="value">原始名称。</param>
    /// <returns>清理后全小写无空格的名称。</returns>
    private static string NormalizeName(string value) {
        return (value ?? "")
            .Replace("\u00a0", "")
            .Replace(" ", "")
            .Trim()
            .ToLowerInvariant();
    }

    /// <summary>
    /// 获取物品的当前语言显示名，优先取翻译后名称；翻译不可用时回退到成员名或 ID。
    /// 将不间断空格（\u00a0）替换为普通空格以保持 xlsx 渲染兼容。
    /// </summary>
    /// <param name="item">物品原型。</param>
    /// <returns>可用作工作表单元格的显示名。</returns>
    private static string GetItemName(ItemProto item) {
        string translatedName = item.Name?.Translate();
        if (!string.IsNullOrWhiteSpace(translatedName) && translatedName != item.Name) {
            return translatedName.Replace('\u00a0', ' ');
        }
        return (item.name ?? item.Name ?? item.ID.ToString(CultureInfo.InvariantCulture)).Replace('\u00a0', ' ');
    }

    /// <summary>
    /// 获取单颗星球上指定物品在指定统计周期的四项指标。
    /// 工厂索引越界、物品不在工厂统计池中时返回 <see cref="ItemStatistics.Zero"/>。
    /// </summary>
    /// <param name="gameData">当前游戏数据。</param>
    /// <param name="planet">目标星球。</param>
    /// <param name="itemId">物品原型 ID。</param>
    /// <param name="timeLevel">统计周期下标（0-4），决定除数与 total 数组下标偏移。</param>
    /// <returns>四项生产/消耗指标。</returns>
    private static ItemStatistics GetPlanetItemStatistics(GameData gameData, PlanetData planet, int itemId, int timeLevel) {
        if (planet.factoryIndex < 0 || planet.factoryIndex >= gameData.statistics.production.factoryStatPool.Length) {
            return ItemStatistics.Zero;
        }

        FactoryProductionStat factoryStat = gameData.statistics.production.factoryStatPool[planet.factoryIndex];
        if (factoryStat?.productIndices == null || itemId < 0 || itemId >= factoryStat.productIndices.Length) {
            return ItemStatistics.Zero;
        }

        int productIndex = factoryStat.productIndices[itemId];
        if (productIndex <= 0 || productIndex >= factoryStat.productCursor) {
            return ItemStatistics.Zero;
        }

        ProductStat productStat = factoryStat.productPool[productIndex];
        if (productStat?.total == null) {
            return ItemStatistics.Zero;
        }

        double divisor = RateDivisors[timeLevel];
        int productionLevel = timeLevel + 1;
        int consumptionLevel = productionLevel + 7;
        return new ItemStatistics(
            productStat.total[productionLevel] / divisor,
            productStat.refProductSpeed,
            productStat.total[consumptionLevel] / divisor,
            productStat.refConsumeSpeed);
    }
}
