using System;

namespace VanillaCurveSim;

internal sealed class FeGachaEvaluator {
    private readonly FeGachaKernel kernel = new();

    public static void RunSelfCheck() {
        var evaluator = new FeGachaEvaluator();
        var config = FeReference.CreateScenarioConfig(SimulationMode.FeConventional, 2);
        var warehouse = FeWarehouse.CreateInitial(isSpeedrun: false);
        warehouse.AddMatrix(2, 60);
        evaluator.RunPhaseSimulation(config, warehouse, 2, openingDrawCount: 20, protoDrawCount: 10, randomSeed: 7,
            out GachaPhaseEstimate gacha, out GrowthExchangeEstimate growth);
        SimulatorSelfCheck.Require(gacha.MatrixSpent > 0.0, "抽卡应消耗矩阵。");
        SimulatorSelfCheck.Require(warehouse.GrowthPoolPoints >= 0.0, "成长池积分不应为负。");
        SimulatorSelfCheck.Require(growth.GrowthUtility >= 0.0, "成长净值不应为负。");
    }

    public GachaPhaseEstimate EvaluatePhase(FractionationConfigSnapshot config, PhaseSummary baselinePhase,
        double baselinePhaseSeconds) {
        int currentStageIndex = FeReference.ClampStageIndex(config.StageIndex);
        double currentMatrixRatePerSecond = ResolveCurrentMatrixRate(baselinePhase, currentStageIndex);
        double drawShare = FeReference.GetDrawShare(config);
        double openingShare = FeReference.GetOpeningDrawShare(config);

        double matrixBudget = currentMatrixRatePerSecond * baselinePhaseSeconds * drawShare;
        int openingDrawCount = (int)Math.Floor(matrixBudget * openingShare
            / Math.Max(1, FeReference.GetDrawMatrixCost(isOpeningLinePool: true, 1)));
        int protoDrawCount = (int)Math.Floor(matrixBudget * (1.0 - openingShare)
            / Math.Max(1, FeReference.GetDrawMatrixCost(isOpeningLinePool: false, 1)));
        var warehouse = FeWarehouse.CreateInitial(config.IsSpeedrun);
        warehouse.AddRecipeSlotsForStage(currentStageIndex, config.IsSpeedrun);
        warehouse.AddMatrix(currentStageIndex, matrixBudget);

        RunPhaseSimulation(config, warehouse, currentStageIndex, openingDrawCount, protoDrawCount,
            randomSeed: 97 + currentStageIndex * 17 + (config.IsSpeedrun ? 1000 : 0),
            out GachaPhaseEstimate gacha, out _);
        return gacha;
    }

    public GrowthExchangeEstimate EvaluateGrowthExchange(FractionationConfigSnapshot config, double availablePoolPoints,
        double availableFragments, int stageIndex) {
        var warehouse = FeWarehouse.CreateInitial(config.IsSpeedrun);
        warehouse.GrowthPoolPoints = availablePoolPoints;
        warehouse.Fragments = availableFragments;
        return kernel.ExecuteGrowthPlan(config, warehouse, stageIndex);
    }

    public void RunPhaseSimulation(FractionationConfigSnapshot config, FeWarehouse warehouse, int stageIndex,
        int openingDrawCount, int protoDrawCount, int randomSeed, out GachaPhaseEstimate gacha,
        out GrowthExchangeEstimate growth) {
        double utilityBefore = warehouse.GetNormalizedUtility();
        double fragmentsBefore = warehouse.Fragments;
        double poolPointsBefore = warehouse.GrowthPoolPoints;
        var rng = new Random(randomSeed);
        kernel.RunDraws(config, warehouse, stageIndex, openingDrawCount, protoDrawCount, rng,
            out double expectedSOpening, out double expectedSProto);
        growth = kernel.ExecuteGrowthPlan(config, warehouse, stageIndex);

        double matrixSpent = openingDrawCount + protoDrawCount;
        double utilityAfter = warehouse.GetNormalizedUtility();
        double utilityDelta = utilityAfter - utilityBefore;
        double drawOnlyUtility = utilityDelta - growth.GrowthUtility;

        gacha = new GachaPhaseEstimate {
            MatrixSpent = matrixSpent,
            OpeningDrawCount = openingDrawCount,
            ProtoDrawCount = protoDrawCount,
            PoolPointsGenerated = warehouse.GrowthPoolPoints - poolPointsBefore + growth.ConsumedPoolPoints,
            DrawNetValuePerMatrix = matrixSpent <= 0.0001 ? 0.0 : drawOnlyUtility / matrixSpent,
            OpeningExpectedSCount = expectedSOpening,
            ProtoExpectedSCount = expectedSProto,
        };
    }

    private static double ResolveCurrentMatrixRate(PhaseSummary baselinePhase, int stageIndex) {
        string matrixName = stageIndex switch {
            0 => "电磁矩阵",
            1 => "能量矩阵",
            2 => "结构矩阵",
            3 => "信息矩阵",
            4 => "引力矩阵",
            5 => "宇宙矩阵",
            _ => "电磁矩阵",
        };

        if (baselinePhase.MatrixRatesPerSecond.TryGetValue(matrixName, out double rate)) {
            return rate;
        }
        return Math.Max(0.0, baselinePhase.MatrixTargetRatePerSecond);
    }
}
