using System.Collections.Generic;

namespace VanillaCurveSim;

internal sealed class VanillaItem {
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public int BuildMode { get; init; }
    public int BuildIndex { get; init; }
    public int StackSize { get; set; }
    public double Space { get; set; }
    public double Speed { get; set; }
    public long WorkEnergyPerTick { get; set; }
    public string MainCraftCode { get; init; } = string.Empty;
    public string PreTechCode { get; init; } = string.Empty;
    public bool CanBuild => BuildMode > 0;
    public bool IsResearchLab => BuildIndex is 801 or 806;
}

internal sealed class VanillaRecipe {
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RecipeType { get; init; } = string.Empty;
    public string PreTechCode { get; init; } = string.Empty;
    public int TimeSpend { get; init; }
    public bool Productive { get; init; }
    public List<RecipeAmount> Inputs { get; } = [];
    public List<RecipeAmount> Outputs { get; } = [];
}

internal sealed class VanillaTech {
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<string> PreTechCodes { get; } = [];
    public List<string> ImplicitPreTechCodes { get; } = [];
    public bool IsHiddenTech { get; init; }
    public string PreItemCode { get; init; } = string.Empty;
    public int HashNeeded { get; init; }
    public List<RecipeAmount> CostItems { get; } = [];
    public List<UnlockTarget> UnlockTargets { get; } = [];
}

internal sealed class CalcRecipe {
    public string Name { get; init; } = string.Empty;
    public List<int> Factories { get; } = [];
    public List<int> Items { get; } = [];
    public List<double> ItemCounts { get; } = [];
    public List<int> Results { get; } = [];
    public List<double> ResultCounts { get; } = [];
    public int TimeSpend { get; init; }
    public int Proliferator { get; init; }
}

internal sealed class RecipeAmount {
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double Count { get; init; }
}

internal sealed class UnlockTarget {
    public char Kind { get; init; }
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

internal sealed class GameDataSet {
    public Dictionary<int, VanillaItem> ItemsById { get; } = [];
    public Dictionary<int, VanillaRecipe> RecipesById { get; } = [];
    public Dictionary<int, List<VanillaRecipe>> RecipesByOutputId { get; } = [];
    public Dictionary<int, VanillaTech> TechsById { get; } = [];
    public Dictionary<int, List<CalcRecipe>> CalcRecipesByOutputId { get; } = [];
}

internal enum PlayerStrategyKind {
    Conventional,
    Speedrun,
}

internal enum ProgressPhase {
    Bootstrap = 0,
    Electromagnetic = 1,
    Energy = 2,
    Structure = 3,
    Information = 4,
    Gravity = 5,
    Universe = 6,
}

internal enum RecipeSprayMode {
    Accelerate,
    Proliferate,
}

internal sealed class TechTiming {
    public int TechId { get; init; }
    public string TechCode { get; init; } = string.Empty;
    public string TechName { get; init; } = string.Empty;
    public int PhaseIndex { get; init; }
    public int HashNeeded { get; init; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public Dictionary<string, double> MatrixRequiredTotals { get; } = [];
    public Dictionary<string, double> MatrixInventoryAtStart { get; } = [];
    public Dictionary<string, double> MatrixInventoryAtEnd { get; } = [];
    public Dictionary<string, double> MatrixProductionRatesPerSecond { get; } = [];
    public Dictionary<string, double> MatrixConsumptionRatesPerSecond { get; } = [];
}

internal sealed class TimelineMilestone {
    public string Name { get; init; } = string.Empty;
    public double Seconds { get; init; }
    public string Notes { get; init; } = string.Empty;
}

internal sealed class TimelineEvent {
    public ProgressPhase Phase { get; init; }
    public double Seconds { get; set; }
    public int Sequence { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

internal sealed class PhaseSummary {
    public ProgressPhase Phase { get; init; }
    public int TechCount { get; set; }
    public int HashCost { get; set; }
    public List<string> Techs { get; } = [];
    public List<int> TechIds { get; } = [];
    public List<TechTiming> TechTimings { get; } = [];
    public int LabStackLevel { get; set; }
    public int ResearchLabCount { get; set; }
    public int ResearchLabBaseCount { get; set; }
    public double MatrixTargetRatePerSecond { get; set; }
    public int MatrixLabCount { get; set; }
    public int MatrixLabBaseCount { get; set; }
    public long TotalPowerDemandWatts { get; set; }
    public string DysonModeName { get; set; } = string.Empty;
    public long DysonAvailablePowerWatts { get; set; }
    public long DysonCapturedPowerWatts { get; set; }
    public int RayReceiverCount { get; set; }
    public long RayReceiverPowerPerBuildingWatts { get; set; }
    public bool UseGravitonLens { get; set; }
    public double GravitonLensConsumptionPerMinute { get; set; }
    public double SolarSailLaunchPerMinute { get; set; }
    public double RocketLaunchPerMinute { get; set; }
    public double SwarmSailCountEstimate { get; set; }
    public double ConstructedSpEstimate { get; set; }
    public double ConstructedCpEstimate { get; set; }
    public string PrimaryPowerSourceName { get; set; } = string.Empty;
    public int PrimaryPowerBuildingCount { get; set; }
    public string FuelName { get; set; } = string.Empty;
    public double FuelConsumptionPerSecond { get; set; }
    public double StartSeconds { get; set; }
    public double ResearchEndSeconds { get; set; }
    public double PhaseEndSeconds { get; set; }
    public double SupermarketFillSeconds { get; set; }
    public double TotalBlockingSeconds { get; set; }
    public string PrimaryBlockingItemName { get; set; } = string.Empty;
    public string PrimaryBlockingReason { get; set; } = string.Empty;
    public double PrimaryBlockingSeconds { get; set; }
    public double EventDelaySeconds { get; set; }
    public string EventDelayReason { get; set; } = string.Empty;
    public double EstimatedResearchSeconds { get; set; }
    public double CumulativeSeconds { get; set; }
    public Dictionary<string, int> BuildingCounts { get; } = [];
    public Dictionary<string, double> ResourceRatesPerSecond { get; } = [];
    public Dictionary<string, double> InventorySnapshot { get; } = [];
    public Dictionary<string, int> SupermarketSlots { get; } = [];
    public Dictionary<string, int> SupermarketTargets { get; } = [];
    public Dictionary<string, int> MatrixLabCounts { get; } = [];
    public Dictionary<string, int> MatrixLabBaseCounts { get; } = [];
    public Dictionary<string, double> MatrixBuildStartSeconds { get; } = [];
    public Dictionary<string, double> MatrixProductionStartSeconds { get; } = [];
    public Dictionary<string, string> MatrixBuildNotes { get; } = [];
    public Dictionary<string, string> MatrixProductionNotes { get; } = [];
    public Dictionary<string, double> MatrixRatesPerSecond { get; } = [];
    public List<TimelineEvent> TimelineEvents { get; } = [];
}

internal sealed class StrategySimulationResult {
    public PlayerStrategyKind Strategy { get; init; }
    public int TotalHashCost { get; set; }
    public List<string> TechOrder { get; } = [];
    public List<PhaseSummary> PhaseSummaries { get; } = [];
    public List<TimelineMilestone> Milestones { get; } = [];
    public string MilestoneSource { get; set; } = string.Empty;
    public string GoalTech { get; set; } = string.Empty;

    public void NormalizeForOutput() {
        PhaseSummaries.Sort((left, right) => left.Phase.CompareTo(right.Phase));
        foreach (PhaseSummary phase in PhaseSummaries) {
            phase.TechTimings.Sort((left, right) => {
                int byStart = left.StartSeconds.CompareTo(right.StartSeconds);
                return byStart != 0
                    ? byStart
                    : left.PhaseIndex.CompareTo(right.PhaseIndex);
            });
            phase.TimelineEvents.Sort((left, right) => {
                int byStart = left.Seconds.CompareTo(right.Seconds);
                return byStart != 0
                    ? byStart
                    : left.Sequence.CompareTo(right.Sequence);
            });
        }
        Milestones.Sort((left, right) => left.Seconds.CompareTo(right.Seconds));
    }
}
