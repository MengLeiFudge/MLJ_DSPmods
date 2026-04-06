using System;
using System.Collections.Generic;

namespace VanillaCurveSim;

internal enum SimulationMode {
    BaselineVanilla = 0,
    FeConventional = 1,
    FeSpeedrun = 2,
}

internal enum FeGachaFocus {
    Balanced = 0,
    MineralExpansion = 1,
    ConversionLeap = 2,
    LogisticsInteraction = 3,
    EmbryoCycle = 4,
    ProcessOptimization = 5,
    RectificationEconomy = 6,
}

internal enum GachaRarity {
    C = 0,
    B = 1,
    A = 2,
    S = 3,
}

internal sealed class FractionationConfigSnapshot {
    public string ScenarioName { get; init; } = string.Empty;
    public SimulationMode Mode { get; init; }
    public bool IsSpeedrun { get; init; }
    public FeGachaFocus Focus { get; init; }
    public int StageIndex { get; init; }
    public int RecipeLevel { get; init; }
    public int SelectedIncLevel { get; init; }
    public int InteractionTowerLevel { get; init; }
    public int MineralReplicationTowerLevel { get; init; }
    public int PointAggregateTowerLevel { get; init; }
    public int ConversionTowerLevel { get; init; }
    public int RectificationTowerLevel { get; init; }
    public float AchievementSuccessBonus { get; init; }
    public float AchievementDestroyReductionBonus { get; init; }
    public float AchievementDoubleOutputBonus { get; init; }
    public float AchievementEnergyReductionBonus { get; init; }
}

internal sealed class FractionationEffectMetrics {
    public double FractionationImpact { get; set; }
    public double ResourceGainMultiplier { get; set; }
    public double EnergyEfficiencyMultiplier { get; set; }
    public double GachaNetValuePerMatrix { get; set; }
    public double GrowthExchangeNetValue { get; set; }
    public double CompositeImpactIndex { get; set; }
}

internal sealed class PhaseImpactBreakdown {
    public string PhaseName { get; init; } = string.Empty;
    public double BaselineSeconds { get; set; }
    public double TreatmentSeconds { get; set; }
    public double TimeCompressionRatio { get; set; }
    public double ResourceGainMultiplier { get; set; }
    public double EnergyEfficiencyMultiplier { get; set; }
    public double GachaNetValuePerMatrix { get; set; }
    public double GrowthExchangeNetValue { get; set; }
    public double FragmentYieldMultiplier { get; set; }
    public List<string> Notes { get; } = [];
}

internal sealed class FractionationScenarioResult {
    public string ScenarioName { get; init; } = string.Empty;
    public string BaselineStrategyName { get; init; } = string.Empty;
    public SimulationMode Mode { get; init; }
    public FractionationConfigSnapshot FinalConfig { get; set; } = new();
    public FractionationEffectMetrics Metrics { get; init; } = new();
    public double BaselineTotalSeconds { get; set; }
    public double TreatmentTotalSeconds { get; set; }
    public List<PhaseImpactBreakdown> Phases { get; } = [];
    public List<string> Findings { get; } = [];
}

internal sealed class SimulationComparisonReport {
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public List<StrategySimulationResult> BaselineResults { get; } = [];
    public List<FractionationScenarioResult> TreatmentResults { get; } = [];
}

internal sealed class GachaExpectedOutcome {
    public int DrawCount { get; init; }
    public double ExpectedCCount { get; init; }
    public double ExpectedBCount { get; init; }
    public double ExpectedACount { get; init; }
    public double ExpectedSCount { get; init; }
    public double ExpectedUtility { get; init; }
    public double CostMatrix { get; init; }
    public double NetValuePerMatrix => CostMatrix <= 0.0001 ? 0.0 : ExpectedUtility / CostMatrix;
}

internal sealed class GachaPhaseEstimate {
    public double MatrixSpent { get; init; }
    public int OpeningDrawCount { get; init; }
    public int ProtoDrawCount { get; init; }
    public double PoolPointsGenerated { get; init; }
    public double DrawNetValuePerMatrix { get; init; }
    public double OpeningExpectedSCount { get; init; }
    public double ProtoExpectedSCount { get; init; }
}

internal sealed class GrowthExchangeEstimate {
    public double ConsumedPoolPoints { get; init; }
    public double ConsumedFragments { get; init; }
    public double GrowthUtility { get; init; }
    public double NetValuePerPoint => ConsumedPoolPoints <= 0.0001 ? 0.0 : GrowthUtility / ConsumedPoolPoints;
}

internal sealed class TowerEffectEstimate {
    public string TowerName { get; init; } = string.Empty;
    public int TowerLevel { get; init; }
    public int RecipeLevel { get; init; }
    public double ResourceGainMultiplier { get; init; }
    public double EnergyEfficiencyMultiplier { get; init; }
    public double TimeAccelerationContribution { get; init; }
    public double FragmentYieldMultiplier { get; init; }
}

internal sealed class FractionationPhaseEstimate {
    public double ResourceGainMultiplier { get; init; }
    public double EnergyEfficiencyMultiplier { get; init; }
    public double TimeAccelerationBonus { get; init; }
    public double FragmentYieldMultiplier { get; init; }
    public double EstimatedFragments { get; init; }
    public List<TowerEffectEstimate> Towers { get; } = [];
}
