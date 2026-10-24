using System;
using System.Collections.Generic;

namespace VanillaCurveSim;

/// <summary>独立单塔工况；集装和特质均表示已取得的科技效果，不自动授予科技。</summary>
internal sealed class FeScenarioConfig {
    public string Name { get; set; } = "蓝矩阵单层";
    public int Stack { get; set; } = 1;
    public int FoodItemId { get; set; } = 6001;
    public int FoodInc { get; set; }
    public int TraitLevel { get; set; }
    /// <summary>已有献祭资格且阶段全部恢复时，每秒经过解析塔的源点件数。</summary>
    public double MemoryPassesPerSecond { get; set; }
}

/// <summary>离线计算输入；同一随机种子与输入文件产生相同的数值结果。</summary>
internal sealed class FeSimulationOptions {
    public int DurationSeconds { get; set; } = 600;
    public uint Seed { get; set; } = 97;
    public List<FeScenarioConfig> Scenarios { get; set; } = [
        new(),
        new() { Name = "蓝矩阵四层", Stack = 4 },
        new() { Name = "紫矩阵八层", Stack = 8, FoodItemId = 6004, FoodInc = 4 },
        new() { Name = "白矩阵二十层", Stack = 20, FoodItemId = 6006, FoodInc = 10 },
        new() { Name = "白矩阵二十层·六级特质与献祭", Stack = 20, FoodItemId = 6006,
            FoodInc = 10, TraitLevel = 6, MemoryPassesPerSecond = 46.875 },
    ];
}

/// <summary>游戏导出的当前配方；输入数量为参与量，消耗概率与成功概率分别保存。</summary>
internal sealed class FeRecipe {
    public string Id { get; init; }
    public string Name { get; init; }
    public int TowerId { get; init; }
    public bool RequiresFood { get; init; }
    public float SuccessProbability { get; init; }
    public int[] Inputs { get; init; }
    public int[] InputCounts { get; init; }
    public float[] ConsumeProbabilities { get; init; }
    public int[] Outputs { get; init; }
    public float[] OutputCounts { get; init; }
}

/// <summary>已验证的导出数据及其来源指纹。</summary>
internal sealed class FeDataSet {
    public List<FeRecipe> Recipes { get; } = [];
    public Dictionary<int, string> Names { get; } = [];
    public Dictionary<int, double> Values { get; } = [];
    public HashSet<int> Foods { get; } = [];
    public Dictionary<string, string> SourceSha256 { get; } = [];
    public string Name(int id) => Names.TryGetValue(id, out string name) ? name : $"物品{id}";
}

/// <summary>单个工况、单张配方的批量随机结算与长期期望。</summary>
internal sealed class FeProductionResult {
    public string Scenario { get; init; }
    public string RecipeId { get; init; }
    public string RecipeName { get; init; }
    public int TowerId { get; init; }
    public double RoundsPerSecond { get; init; }
    public long Attempts { get; set; }
    public long Successes { get; set; }
    public double ExpectedSuccesses { get; set; }
    public Dictionary<int, long> Consumed { get; } = [];
    public Dictionary<int, long> Returned { get; } = [];
    public Dictionary<int, long> Produced { get; } = [];
    public Dictionary<int, int> MinimumHeld { get; } = [];
    public long WorkNutrition { get; set; }
    public long FoodIgnited { get; set; }
    public double FoodEnergyRemaining { get; set; }
    public double IdleFoodPerMinute { get; init; }
    /// <summary>仅统计生产反应的净值，食物单列；相同产物与投入自动抵消。</summary>
    public double MaterialNetValue { get; set; }
}

/// <summary>单塔对已知样本线索的解析结果，未完成时保留共享槽进度。</summary>
internal sealed class FeResearchResult {
    public string Content { get; init; }
    public int Stack { get; init; }
    public double SampleValue { get; init; }
    public double SuccessProbability { get; init; }
    public double DestroyProbability { get; init; }
    public double ExpectedSeconds { get; init; }
    public long Attempts { get; set; }
    public long ReturnedSamples { get; set; }
    public long DestroyedSamples { get; set; }
    public int CompletedContents { get; set; }
    public int ContentCount { get; init; }
    public int Progress { get; set; }
    public double ElapsedSeconds { get; set; }
}

/// <summary>矩阵阶段恢复的独立工况，不自动开放下一阶段。</summary>
internal sealed class FeRecoveryResult {
    public int Stage { get; init; }
    public int Stack { get; init; }
    public int RequiredMemory { get; init; }
    public int ConsumedMemory { get; set; }
    public long Passes { get; set; }
    public double ExpectedSeconds { get; init; }
    public double ElapsedSeconds { get; set; }
}

/// <summary>完整分钟窗口的源点献祭，增益仅在下一分钟生效。</summary>
internal sealed class FeOfferingMinute {
    public int Minute { get; init; }
    public long ConsumedMemory { get; set; }
    public float AppliedBoost { get; init; }
    public float NextBoost { get; set; }
    public bool LegendaryRecoveryEligible { get; set; }
}

/// <summary>六档矩阵同时供料时各自的独立扩容账本。</summary>
internal sealed class FeExpansionResult {
    public int MatrixId { get; init; }
    public long Processed { get; set; }
    public long ExpansionConsumed { get; set; }
    public int Buffered { get; set; }
    public long MemoryProduced { get; set; }
    public double CapacityIncrement { get; set; }
}

/// <summary>现行框架报告；原版时间线只作背景，不输出未经建模的FE通关时间。</summary>
internal sealed class SimulationComparisonReport {
    public int SchemaVersion => 2;
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public FeSimulationOptions Options { get; init; }
    public Dictionary<string, string> SourceSha256 { get; init; }
    public List<StrategySimulationResult> BaselineResults { get; } = [];
    public List<FeProductionResult> Production { get; } = [];
    public List<FeResearchResult> Research { get; } = [];
    public List<FeRecoveryResult> Recovery { get; } = [];
    public Dictionary<string, List<FeOfferingMinute>> Offerings { get; } = [];
    public Dictionary<int, List<FeExpansionResult>> ExpansionByStack { get; } = [];
}
