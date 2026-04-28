using System;
using System.Collections.Generic;
using System.Linq;

namespace VanillaCurveSim;

internal sealed class VanillaCurveSimulator {
    private enum DysonBuildMode {
        None,
        SailOnly,
        SailAndRocket,
    }

    private sealed class DysonPowerEstimate {
        public DysonBuildMode Mode { get; init; }
        public long AvailablePowerWatts { get; init; }
        public long CapturedPowerWatts { get; init; }
        public int ReceiverCount { get; init; }
        public long ReceiverPowerPerBuildingWatts { get; init; }
        public bool UseGravitonLens { get; init; }
        public double GravitonLensConsumptionPerMinute { get; init; }
        public double SolarSailLaunchPerMinute { get; init; }
        public double RocketLaunchPerMinute { get; init; }
        public double SwarmSailCountEstimate { get; init; }
        public double ConstructedSpEstimate { get; init; }
        public double ConstructedCpEstimate { get; init; }
    }

    private sealed class PowerSourceProfile {
        public PowerSourceProfile(string sourceName, double powerPerBuildingWatts, string fuelName,
            double fuelHeatValue) {
            SourceName = sourceName;
            PowerPerBuildingWatts = powerPerBuildingWatts;
            FuelName = fuelName;
            FuelHeatValue = fuelHeatValue;
        }

        public string SourceName { get; }
        public double PowerPerBuildingWatts { get; }
        public string FuelName { get; }
        public double FuelHeatValue { get; }
        public bool UsesFuel => FuelHeatValue > 0 && FuelName.Length > 0;
    }

    private sealed class BlockingRecord {
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public double MissingCount { get; set; }
        public double BlockingSeconds { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private sealed class PhaseBuildPlan {
        public Dictionary<int, int> TargetActiveCounts { get; } = [];
        public Dictionary<int, double> BuildItemDemand { get; } = [];
        public Dictionary<int, string> BuildReasons { get; } = [];
    }

    private const int GoalTechId = 1508;
    private const int ElectromagneticMatrixTechId = 1002;
    private const int SmeltingTechId = 1401;
    private const int BasicManufacturingTechId = 1201;
    private const int PlanetaryLogisticsTechId = 1604;
    private const int InterstellarLogisticsTechId = 1605;
    private const int ThermalPowerTechId = 1412;
    private const int SolarSailOrbitTechId = 1503;
    private const int RayReceiverTechId = 1504;
    private const int IonosphereUtilizationTechId = 1505;
    private const int WaveFunctionInterferenceTechId = 1141;
    private const int QuantumPrintingTechId = 1203;
    private const int QuantumChipTechId = 1303;
    private const int PlaneSmeltingTechId = 1417;
    private const int QuantumEntanglementTechId = 1305;
    private const int PhotonMiningTechId = 1304;
    private const int VerticalLaunchTechId = 1522;
    private const int GravitonRefractionTechId = 1704;
    private const int BeltMkIItemId = 2001;
    private const int BeltMkIIItemId = 2002;
    private const int BeltMkIIIItemId = 2003;
    private const int InserterMkIItemId = 2011;
    private const int InserterMkIIItemId = 2012;
    private const int InserterMkIIIItemId = 2013;
    private const int StackedInserterItemId = 2014;
    private const int StaticLabStackLevel = 15;
    private const int InitialLabStackLevel = 3;
    private const int MaxLabStackLevel = 15;
    private const double MkIIIAccelerateFactor = 2.0;
    private const double MkIIIProliferateFactor = 1.25;
    private const double DefaultLabHashPerSecond = 60.0;
    private const double SmallMinerVeinCount = 8.0;
    private const double LargeMinerVeinCount = 30.0;
    private const double MiningRatePerVeinPerSecond = 0.5;
    private const double OilExtractorRatePerSecond = 1.0;
    private const double WaterPumpRatePerSecond = 1.0;
    private const int DefaultBuildingStackSize = 50;
    private const int DefaultBuildingInventoryGroupCount = 2;
    private const int LogisticsInventoryGroupCount = 10;
    private const int PlanetaryInventoryLimit = 5000;
    private const int InterstellarInventoryLimit = 20000;
    private const double HandcraftAssemblerSpeed = 0.75;
    private const double HandcraftFurnaceSpeed = 1.0;
    private const double ResourceBufferSeconds = 300.0;
    private const double IntermediateBufferSeconds = 180.0;
    private const double MatrixBufferSeconds = 120.0;
    private const double MatrixRateHeadroomConventional = 1.05;
    private const double MatrixRateHeadroomSpeedrun = 1.1;
    private const double MatrixRateHeadroomDefault = 1.0;
    private const int SupermarketInventoryGroupCount = 2;
    private const double DefaultSolarSailLifeSeconds = 1800.0;
    private const double SolarSailAbsorbDelaySeconds = 240.0;
    private const double RayReceiverWarmupSeconds = 20.0 * 60.0;
    private const double RayReceiverWarmupWithLensSeconds = 8.0 * 60.0;
    private const double EjectorLaunchesPerMinute = 30.0;// 3600 / (80 + 40)
    private const double SiloLaunchesPerMinute = 13.3333333333;// 3600 / (150 + 120)
    private const long ReceiverBasePowerWatts = 480_000_000;// 8,000,000 * 60
    private const long SwarmPowerPerSailWatts = 24_000;// 400 * 60
    private const long FramePowerPerSpWatts = 90_000;// 1500 * 60
    private const long ShellPowerPerCpWatts = 18_000;// 300 * 60
    private const double ShellCpPerSpCap = 60.0;

    private static readonly int[] matrixIds = [6001, 6002, 6003, 6004, 6005, 6006];
    private static readonly int[] milestoneTechIds = [1002, 1111, 1124, 1312, 1705, 1507];
    private static readonly Dictionary<int, double> conventionalCriticalPathBonusByTechId = new() {
        [WaveFunctionInterferenceTechId] = 120.0,
        [QuantumChipTechId] = 120.0,
        [QuantumPrintingTechId] = 120.0,
        [PlaneSmeltingTechId] = 120.0,
        [QuantumEntanglementTechId] = 140.0,
        [1312] = 180.0,
        [PhotonMiningTechId] = 110.0,
        [GravitonRefractionTechId] = 140.0,
        [1705] = 180.0,
        [1506] = 180.0,
        [1507] = 220.0,
    };
    private static readonly int[] researchSpeedTechIds = [3901, 3902, 3903];
    private static readonly int[] cargoStackTechIds = [3301, 3302, 3303, 3304, 3305, 3306];
    private static readonly int[] phaseResearchLabCountsConventional = [24, 48, 96, 288, 576, 864, 1296];
    private static readonly int[] phaseResearchLabCountsSpeedrun = [18, 36, 72, 144, 288, 432, 648];
    private static readonly double[] phaseInventoryCoverageRatios = [0.04, 0.12, 0.28, 0.45, 0.65, 0.85, 1.0];
    // 阶段电力模型只做主体建筑近似，不追求逐秒电网精度。
    private static readonly Dictionary<int, double> factorySpeedByItemId = new() {
        [2302] = 1.0,// 电弧熔炉
        [2315] = 2.0,// 位面熔炉
        [2319] = 3.0,// 负熵熔炉
        [2303] = 0.75,// 制造台 Mk.I
        [2304] = 1.0,// 制造台 Mk.II
        [2305] = 1.5,// 制造台 Mk.III
        [2318] = 3.0,// 重组式制造台
        [2309] = 1.0,// 化工厂
        [2317] = 2.0,// 量子化工厂
        [2308] = 1.0,// 原油精炼厂
        [2310] = 1.0,// 微型粒子对撞机
        [2314] = 1.0,// 分馏塔
        [2208] = 1.0,// 射线接收站（临界光子）
        [2901] = 1.0,// 矩阵研究站
        [2902] = 3.0,// 自演化研究站（初步假设）
    };
    private static readonly Dictionary<int, double> buildingPowerByItemId = new() {
        [2301] = 420_000,// 采矿机
        [2316] = 1_440_000,// 大型采矿机
        [2306] = 300_000,// 抽水站
        [2307] = 840_000,// 原油萃取站
        [2302] = 360_000,// 电弧熔炉
        [2315] = 1_440_000,// 位面熔炉
        [2319] = 2_880_000,// 负熵熔炉
        [2303] = 270_000,// 制造台 Mk.I
        [2304] = 540_000,// 制造台 Mk.II
        [2305] = 1_080_000,// 制造台 Mk.III
        [2318] = 2_160_000,// 重组式制造台
        [2309] = 720_000,// 化工厂
        [2317] = 2_160_000,// 量子化工厂
        [2308] = 960_000,// 原油精炼厂
        [2310] = 12_000_000,// 微型粒子对撞机
        [2314] = 720_000,// 分馏塔
        [2901] = 480_000,// 矩阵研究站
        [2902] = 1_440_000,// 矩阵研究站 Mk.II
    };
    private static readonly PowerSourceProfile windPowerProfile = new("风力涡轮机", 300_000, string.Empty, 0);
    private static readonly PowerSourceProfile thermalPowerProfile = new("火力发电厂", 2_160_000, "煤矿", 5_400_000);
    private static readonly PowerSourceProfile fusionPowerProfile = new("微型聚变发电站", 9_000_000, "氘核燃料棒", 3_000_000_000);
    private static readonly PowerSourceProfile artificialStarPowerProfile =
        new("人造恒星", 75_000_000, "反物质燃料棒", 72_000_000_000);
    private static readonly int[] verticalConstructionTechIds = [3701, 3702, 3703, 3704, 3705, 3706];
    private static readonly int[] beltItemIds = [BeltMkIItemId, BeltMkIIItemId, BeltMkIIIItemId];
    private static readonly int[] inserterItemIds =
        [InserterMkIItemId, InserterMkIIItemId, InserterMkIIIItemId, StackedInserterItemId];
    private static readonly int[] smelterUpgradeItemIds = [2302, 2315, 2319];
    private static readonly int[] assemblerUpgradeItemIds = [2303, 2304, 2305, 2318];
    private static readonly int[] chemicalUpgradeItemIds = [2309, 2317];
    private static readonly int[] labUpgradeItemIds = [2901, 2902];
    private static readonly Dictionary<int, double> beltRateByItemId = new() {
        [BeltMkIItemId] = 6.0,
        [BeltMkIIItemId] = 12.0,
        [BeltMkIIIItemId] = 30.0,
    };
    private static readonly Dictionary<int, int> averageConnectionCountByBuildingId = new() {
        [2301] = 1,
        [2316] = 1,
        [2306] = 1,
        [2307] = 1,
        [2302] = 2,
        [2315] = 2,
        [2319] = 2,
        [2303] = 3,
        [2304] = 3,
        [2305] = 3,
        [2318] = 3,
        [2309] = 3,
        [2317] = 3,
        [2308] = 3,
        [2310] = 3,
        [2314] = 2,
        [2901] = 2,
        [2902] = 2,
    };

    private readonly GameDataSet dataSet;
    private readonly Dictionary<int, int> itemDepthCache = [];
    private readonly Dictionary<int, CalcRecipe> calcRecipeByVanillaRecipeId = [];
    private readonly Dictionary<int, RecipeSprayMode> sprayModeByRecipeId = [];
    private readonly Dictionary<int, double> bestAreaPerItemCache = [];
    private readonly Dictionary<long, double> areaByRecipeModeCache = [];

    public VanillaCurveSimulator(GameDataSet dataSet) {
        this.dataSet = dataSet;
        BuildCalcRecipeMapping();
        BuildStaticSprayModeTable();
    }

    public IReadOnlyList<StrategySimulationResult> RunAll() =>
        Enum.GetValues(typeof(PlayerStrategyKind))
            .Cast<PlayerStrategyKind>()
            .Select(Run)
            .ToArray();

    public StrategySimulationResult Run(PlayerStrategyKind strategyKind) {
        var result = new StrategySimulationResult {
            Strategy = strategyKind,
            GoalTech = dataSet.TechsById.TryGetValue(GoalTechId, out VanillaTech goalTech)
                ? $"{goalTech.Code} {goalTech.Name}"
                : GoalTechId.ToString(),
        };
        var unlockedTechs = new HashSet<int> { 1 };
        var remainingTechs = BuildGoalTechSet(strategyKind);
        remainingTechs.Remove(1);

        while (remainingTechs.Count > 0) {
            List<VanillaTech> candidates = remainingTechs
                .Select(id => dataSet.TechsById[id])
                .Where(tech => IsUnlocked(tech, unlockedTechs))
                .ToList();
            if (candidates.Count == 0) {
                break;
            }

            // 当前模拟器改为“逐层清空”：只要低层还有未完成科技，就不允许高层抢跑。
            ProgressPhase currentPhase = remainingTechs
                .Select(id => GetPhase(dataSet.TechsById[id]))
                .Min();
            List<VanillaTech> phaseCandidates = candidates
                .Where(tech => GetPhase(tech) == currentPhase)
                .ToList();
            VanillaTech selected = (phaseCandidates.Count > 0 ? phaseCandidates : candidates)
                .OrderByDescending(tech => ScoreTech(tech, strategyKind))
                .ThenBy(tech => tech.HashNeeded)
                .ThenBy(tech => tech.Id)
                .First();

            unlockedTechs.Add(selected.Id);
            remainingTechs.Remove(selected.Id);
            result.TechOrder.Add($"{selected.Code} {selected.Name}");
            result.TotalHashCost += selected.HashNeeded;

            ProgressPhase phase = GetPhase(selected);
            PhaseSummary phaseSummary = GetOrCreatePhaseSummary(result, phase);
            phaseSummary.TechCount++;
            phaseSummary.HashCost += selected.HashNeeded;
            phaseSummary.TechIds.Add(selected.Id);
            phaseSummary.TechTimings.Add(new TechTiming {
                TechId = selected.Id,
                TechCode = selected.Code,
                TechName = selected.Name,
                PhaseIndex = phaseSummary.TechTimings.Count,
                HashNeeded = selected.HashNeeded,
            });
            if (phaseSummary.Techs.Count < 8) {
                phaseSummary.Techs.Add($"{selected.Code} {selected.Name}");
            }
        }

        ComputePhaseCapacity(strategyKind, result);
        PopulateMilestones(strategyKind, result);
        return result;
    }

    private static void PopulateMilestones(PlayerStrategyKind strategyKind, StrategySimulationResult result) {
        result.Milestones.Clear();
        result.MilestoneSource = string.Empty;
        if (strategyKind != PlayerStrategyKind.Speedrun) {
            return;
        }

        result.MilestoneSource = "用户提供的速通基准时间线";
        result.Milestones.AddRange([
            new TimelineMilestone { Name = "第一台采矿机", Seconds = 51, Notes = "开局资源采集启动" },
            new TimelineMilestone { Name = "第一座熔炉", Seconds = 82, Notes = "进入基础冶炼" },
            new TimelineMilestone { Name = "第一段传送带、分拣器", Seconds = 143, Notes = "进入基础物流" },
            new TimelineMilestone { Name = "第一座制造台", Seconds = 206, Notes = "进入基础自动化制造" },
            new TimelineMilestone { Name = "第一座矩阵研究站（蓝糖）", Seconds = 243, Notes = "前 20 分钟关键锚点" },
            new TimelineMilestone { Name = "第一座火力发电机", Seconds = 354, Notes = "前中期供电切换锚点" },
            new TimelineMilestone { Name = "第一次异星起飞", Seconds = 2625, Notes = "本星系异星远征开始" },
            new TimelineMilestone { Name = "异星落地铺火电", Seconds = 2648, Notes = "首轮异星补给阶段" },
            new TimelineMilestone { Name = "第一次异星返航", Seconds = 3131, Notes = "钛/硅首轮回运完成" },
            new TimelineMilestone { Name = "第二次异星起飞", Seconds = 4623, Notes = "中后期二次远征起点" },
            new TimelineMilestone { Name = "飞往熔岩星", Seconds = 4859, Notes = "中后期关键材料远征" },
            new TimelineMilestone { Name = "三级增产剂产线蓝图", Seconds = 5684, Notes = "高阶喷涂链路成型" },
            new TimelineMilestone { Name = "紫糖产线蓝图", Seconds = 5805, Notes = "信息矩阵前置锚点" },
            new TimelineMilestone { Name = "白糖产线蓝图", Seconds = 7977, Notes = "宇宙矩阵前置锚点" },
            new TimelineMilestone { Name = "正式白糖", Seconds = 8418, Notes = "稳定宇宙矩阵量产" },
            new TimelineMilestone { Name = "结束", Seconds = 9727, Notes = "用户提供的速通基准终点" },
        ]);
    }

    private void BuildCalcRecipeMapping() {
        foreach (VanillaRecipe recipe in dataSet.RecipesById.Values) {
            CalcRecipe calcRecipe = FindMatchingCalcRecipe(recipe);
            if (calcRecipe != null) {
                calcRecipeByVanillaRecipeId[recipe.Id] = calcRecipe;
            }
        }
    }

    private void BuildStaticSprayModeTable() {
        foreach (VanillaRecipe recipe in dataSet.RecipesById.Values) {
            sprayModeByRecipeId[recipe.Id] = DecideSprayMode(recipe);
        }
    }

    private RecipeSprayMode DecideSprayMode(VanillaRecipe recipe) {
        if (!CanProliferate(recipe)) {
            return RecipeSprayMode.Accelerate;
        }

        double accelerateArea = EvaluateRecipeArea(recipe, RecipeSprayMode.Accelerate, new HashSet<int>());
        double proliferateArea = EvaluateRecipeArea(recipe, RecipeSprayMode.Proliferate, new HashSet<int>());
        return proliferateArea < accelerateArea
            ? RecipeSprayMode.Proliferate
            : RecipeSprayMode.Accelerate;
    }

    private double EvaluateBestAreaPerItem(int itemId, HashSet<int> stack) {
        if (bestAreaPerItemCache.TryGetValue(itemId, out double cached)) {
            return cached;
        }

        if (!dataSet.RecipesByOutputId.TryGetValue(itemId, out List<VanillaRecipe> recipes) || recipes.Count == 0) {
            bestAreaPerItemCache[itemId] = 0;
            return 0;
        }

        double best = double.PositiveInfinity;
        foreach (VanillaRecipe recipe in recipes) {
            double area = EvaluateBestAreaPerRecipe(recipe, stack);
            if (area < best) {
                best = area;
            }
        }

        if (double.IsNaN(best) || double.IsInfinity(best)) {
            best = 0;
        }

        bestAreaPerItemCache[itemId] = best;
        return best;
    }

    private double EvaluateBestAreaPerRecipe(VanillaRecipe recipe, HashSet<int> stack) {
        double accelerateArea = EvaluateRecipeArea(recipe, RecipeSprayMode.Accelerate, stack);
        if (!CanProliferate(recipe)) {
            return accelerateArea;
        }

        double proliferateArea = EvaluateRecipeArea(recipe, RecipeSprayMode.Proliferate, stack);
        return Math.Min(accelerateArea, proliferateArea);
    }

    private double EvaluateRecipeArea(VanillaRecipe recipe, RecipeSprayMode mode, HashSet<int> stack) {
        long cacheKey = ((long)recipe.Id << 1) | (mode == RecipeSprayMode.Proliferate ? 1L : 0L);
        if (areaByRecipeModeCache.TryGetValue(cacheKey, out double cached)) {
            return cached;
        }

        if (!stack.Add(recipe.Id)) {
            return double.PositiveInfinity;
        }

        CalcRecipe calcRecipe = GetCalcRecipe(recipe);
        if (calcRecipe == null) {
            stack.Remove(recipe.Id);
            return double.PositiveInfinity;
        }

        int factoryId = ChooseStaticFactory(calcRecipe, recipe);
        if (!dataSet.ItemsById.TryGetValue(factoryId, out VanillaItem factory)
            || factory.Space <= 0
            || factory.Speed <= 0) {
            stack.Remove(recipe.Id);
            return double.PositiveInfinity;
        }

        double outputCount = recipe.Outputs.Sum(o => o.Count);
        if (outputCount <= 0) {
            stack.Remove(recipe.Id);
            return double.PositiveInfinity;
        }

        double outputMultiplier = GetOutputMultiplier(recipe, mode);
        double speedMultiplier = GetSpeedMultiplier(recipe, mode);
        double effectiveOutputCount = outputCount * outputMultiplier;
        double localRate =
            effectiveOutputCount * factory.Speed * speedMultiplier * 60.0 / Math.Max(1, recipe.TimeSpend);
        if (localRate <= 0) {
            stack.Remove(recipe.Id);
            return double.PositiveInfinity;
        }

        double localArea = GetEffectiveBuildingArea(factory, StaticLabStackLevel) / localRate;
        double totalArea = localArea;
        foreach (RecipeAmount input in recipe.Inputs) {
            double childArea = EvaluateBestAreaPerItem(input.Id, stack);
            totalArea += childArea * input.Count / effectiveOutputCount;
        }

        stack.Remove(recipe.Id);
        areaByRecipeModeCache[cacheKey] = totalArea;
        return totalArea;
    }

    private CalcRecipe FindMatchingCalcRecipe(VanillaRecipe recipe) {
        int primaryOutputId = recipe.Outputs.Count > 0 ? recipe.Outputs[0].Id : 0;
        if (primaryOutputId <= 0
            || !dataSet.CalcRecipesByOutputId.TryGetValue(primaryOutputId, out List<CalcRecipe> recipes)) {
            return null;
        }

        return recipes
            .Where(candidate => RecipeMatchesCalcRecipe(recipe, candidate))
            .OrderByDescending(candidate => candidate.Proliferator)
            .ThenBy(candidate => candidate.TimeSpend)
            .FirstOrDefault();
    }

    private static bool RecipeMatchesCalcRecipe(VanillaRecipe recipe, CalcRecipe calcRecipe) {
        if (Math.Abs(calcRecipe.TimeSpend - recipe.TimeSpend) > 0) {
            return false;
        }

        if (!AmountsMatch(recipe.Inputs, calcRecipe.Items, calcRecipe.ItemCounts)) {
            return false;
        }

        return AmountsMatch(recipe.Outputs, calcRecipe.Results, calcRecipe.ResultCounts);
    }

    private static bool AmountsMatch(List<RecipeAmount> amounts, List<int> ids, List<double> counts) {
        if (amounts.Count != ids.Count || ids.Count != counts.Count) {
            return false;
        }

        Dictionary<int, double> left = amounts
            .GroupBy(amount => amount.Id)
            .ToDictionary(group => group.Key, group => group.Sum(amount => amount.Count));
        Dictionary<int, double> right = ids
            .Select((id, index) => (id, count: counts[index]))
            .GroupBy(pair => pair.id)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.count));

        if (left.Count != right.Count) {
            return false;
        }

        foreach (var pair in left) {
            if (!right.TryGetValue(pair.Key, out double count) || Math.Abs(pair.Value - count) > 1e-6) {
                return false;
            }
        }

        return true;
    }

    private CalcRecipe GetCalcRecipe(VanillaRecipe recipe) {
        if (recipe == null) {
            return null;
        }

        if (calcRecipeByVanillaRecipeId.TryGetValue(recipe.Id, out CalcRecipe calcRecipe)) {
            return calcRecipe;
        }

        return FindMatchingCalcRecipe(recipe);
    }

    private bool CanProliferate(VanillaRecipe recipe) {
        if (recipe == null || !recipe.Productive) {
            return false;
        }

        CalcRecipe calcRecipe = GetCalcRecipe(recipe);
        return calcRecipe != null && (calcRecipe.Proliferator & 0b10) != 0;
    }

    private bool CanAccelerate(VanillaRecipe recipe) {
        CalcRecipe calcRecipe = GetCalcRecipe(recipe);
        return calcRecipe != null && (calcRecipe.Proliferator & 0b01) != 0;
    }

    private RecipeSprayMode GetSprayMode(VanillaRecipe recipe) {
        if (recipe == null) {
            return RecipeSprayMode.Accelerate;
        }

        return sprayModeByRecipeId.TryGetValue(recipe.Id, out RecipeSprayMode mode)
            ? mode
            : RecipeSprayMode.Accelerate;
    }

    private double GetOutputMultiplier(VanillaRecipe recipe, RecipeSprayMode mode) {
        return mode == RecipeSprayMode.Proliferate && CanProliferate(recipe)
            ? MkIIIProliferateFactor
            : 1.0;
    }

    private double GetSpeedMultiplier(VanillaRecipe recipe, RecipeSprayMode mode) {
        return mode == RecipeSprayMode.Accelerate && CanAccelerate(recipe)
            ? MkIIIAccelerateFactor
            : 1.0;
    }

    private int ChooseStaticFactory(CalcRecipe calcRecipe, VanillaRecipe recipe) {
        List<int> factories = calcRecipe?.Factories?.Where(id => id > 1).ToList() ?? [];
        if (factories.Count == 0) {
            return GetFallbackFactory(recipe);
        }

        int selected = factories[0];
        double bestScore = double.MinValue;
        foreach (int factoryId in factories) {
            if (!dataSet.ItemsById.TryGetValue(factoryId, out VanillaItem factory)
                || factory.Space <= 0
                || factory.Speed <= 0) {
                continue;
            }

            double effectiveArea = GetEffectiveBuildingArea(factory, StaticLabStackLevel);
            double score = factory.Speed / effectiveArea;
            if (score > bestScore) {
                bestScore = score;
                selected = factoryId;
            }
        }

        return selected;
    }

    private static double GetEffectiveBuildingArea(VanillaItem factory, int labStackLevel) {
        if (factory == null || factory.Space <= 0) {
            return 0;
        }

        return factory.IsResearchLab
            ? factory.Space / Math.Max(1, labStackLevel)
            : factory.Space;
    }

    private static int GetCurrentLabStackLevel(HashSet<int> unlockedTechs) {
        int unlockedVerticalTechCount = verticalConstructionTechIds.Count(unlockedTechs.Contains);
        return Math.Min(MaxLabStackLevel, InitialLabStackLevel + unlockedVerticalTechCount * 2);
    }

    private static int
        GetCurrentMatrixInventoryLimit(int matrixItemId, HashSet<int> unlockedTechs, GameDataSet dataSet) {
        if (!dataSet.ItemsById.TryGetValue(matrixItemId, out VanillaItem item)) {
            return 0;
        }

        if (unlockedTechs.Contains(InterstellarLogisticsTechId)) {
            return InterstellarInventoryLimit;
        }

        if (unlockedTechs.Contains(PlanetaryLogisticsTechId)) {
            return PlanetaryInventoryLimit;
        }

        int stackSize = item.StackSize > 0 ? item.StackSize : 1;
        return 30 * stackSize;
    }

    private static int GetCurrentCargoStackMultiplier(HashSet<int> unlockedTechs) {
        return 1 + Math.Min(3, cargoStackTechIds.Count(unlockedTechs.Contains));
    }

    private static int GetCurrentBestBeltItemId(HashSet<int> unlockedItems) {
        foreach (int beltItemId in beltItemIds.Reverse()) {
            if (unlockedItems.Contains(beltItemId)) {
                return beltItemId;
            }
        }

        return BeltMkIItemId;
    }

    private static int GetCurrentBestInserterItemId(HashSet<int> unlockedItems) {
        foreach (int inserterItemId in inserterItemIds.Reverse()) {
            if (unlockedItems.Contains(inserterItemId)) {
                return inserterItemId;
            }
        }

        return InserterMkIItemId;
    }

    private static double
        GetCurrentDynamicFullBeltRatePerSecond(HashSet<int> unlockedTechs, HashSet<int> unlockedItems) {
        int beltItemId = GetCurrentBestBeltItemId(unlockedItems);
        double beltRate = beltRateByItemId.TryGetValue(beltItemId, out double mappedRate)
            ? mappedRate
            : beltRateByItemId[BeltMkIItemId];
        return beltRate * GetCurrentCargoStackMultiplier(unlockedTechs);
    }

    private static int GetAverageConnectionCount(int buildingItemId) {
        return averageConnectionCountByBuildingId.TryGetValue(buildingItemId, out int laneCount)
            ? laneCount
            : 2;
    }

    private static bool IsLogisticsItem(int itemId) =>
        beltItemIds.Contains(itemId) || inserterItemIds.Contains(itemId);

    private static bool IsBeltItem(int itemId) =>
        beltItemIds.Contains(itemId);

    private static bool IsInserterItem(int itemId) =>
        inserterItemIds.Contains(itemId);

    private static bool IsMatrixItem(int itemId) =>
        matrixIds.Contains(itemId);

    private static string FormatStringOrEmpty(string value) =>
        string.IsNullOrEmpty(value) ? "无" : value;

    private static string FormatPower(long watts) {
        if (watts <= 0) {
            return "0 W";
        }

        if (watts >= 1_000_000) {
            return $"{watts / 1_000_000d:0.##} MW";
        }

        if (watts >= 1_000) {
            return $"{watts / 1_000d:0.##} kW";
        }

        return $"{watts} W";
    }

    private static int[] GetUpgradeFamily(int itemId) {
        if (beltItemIds.Contains(itemId)) {
            return beltItemIds;
        }

        if (inserterItemIds.Contains(itemId)) {
            return inserterItemIds;
        }

        if (smelterUpgradeItemIds.Contains(itemId)) {
            return smelterUpgradeItemIds;
        }

        if (assemblerUpgradeItemIds.Contains(itemId)) {
            return assemblerUpgradeItemIds;
        }

        if (chemicalUpgradeItemIds.Contains(itemId)) {
            return chemicalUpgradeItemIds;
        }

        if (labUpgradeItemIds.Contains(itemId)) {
            return labUpgradeItemIds;
        }

        return [];
    }

    private static int GetUpgradeRank(int itemId) {
        int[] family = GetUpgradeFamily(itemId);
        return family.Length == 0 ? -1 : Array.IndexOf(family, itemId);
    }

    private static bool IsHigherTierUpgrade(int newItemId, int oldItemId) {
        int[] newFamily = GetUpgradeFamily(newItemId);
        int[] oldFamily = GetUpgradeFamily(oldItemId);
        return newFamily.Length > 0
               && ReferenceEquals(newFamily, oldFamily)
               && GetUpgradeRank(newItemId) > GetUpgradeRank(oldItemId);
    }

    private int GetBuildingInventoryTargetCount(int itemId) {
        if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item)) {
            return 0;
        }

        int stackSize = item.StackSize > 0 ? item.StackSize : DefaultBuildingStackSize;
        if (IsLogisticsItem(itemId)) {
            return LogisticsInventoryGroupCount * stackSize;
        }

        return DefaultBuildingInventoryGroupCount * stackSize;
    }

    private double GetBackgroundBuildRatePerSecond(int itemId, HashSet<int> unlockedItems) {
        VanillaRecipe recipe = ChoosePrimaryRecipe(itemId);
        if (recipe == null) {
            return 0;
        }

        CalcRecipe calcRecipe = GetCalcRecipe(recipe) ?? ChoosePrimaryCalcRecipe(itemId);
        int factoryId = ChooseFactory(calcRecipe, unlockedItems, recipe);
        return GetFactoryOutputRatePerSecond(factoryId, recipe, itemId);
    }

    private static string FormatAmount(double value) =>
        Math.Abs(value - Math.Round(value)) < 1e-6
            ? Math.Round(value).ToString("0")
            : value.ToString("0.##");

    private static int RoundUpCount(double value) =>
        value <= 0
            ? 0
            : (int)Math.Ceiling(value - 1e-6);

    private static string FormatTimelineDuration(double seconds) {
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1) {
            return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
        }

        return $"{span.Minutes}m {span.Seconds}s";
    }

    private static bool CanManualSupplyMatrixCount(double requiredCount) =>
        requiredCount > 0 && requiredCount <= 20.0 + 1e-6;

    private static bool IsElectromagneticLinePrerequisiteTech(int techId) =>
        techId is ElectromagneticMatrixTechId or SmeltingTechId or BasicManufacturingTechId or ThermalPowerTechId;

    private const double TimelineConvergenceToleranceSeconds = 0.5;
    private const int MaxTimelineIterations = 32;

    private static string GetPhaseDisplayName(ProgressPhase phase) {
        return phase switch {
            ProgressPhase.Bootstrap or ProgressPhase.Electromagnetic => "电磁矩阵阶段",
            ProgressPhase.Energy => "能量矩阵阶段",
            ProgressPhase.Structure => "结构矩阵阶段",
            ProgressPhase.Information => "信息矩阵阶段",
            ProgressPhase.Gravity => "引力矩阵阶段",
            ProgressPhase.Universe => "宇宙矩阵阶段",
            _ => phase.ToString(),
        };
    }

    private string BuildTechEventNotes(int techId, TechTiming timing, PhaseSummary phase, double? eventSeconds = null) {
        if (!dataSet.TechsById.TryGetValue(techId, out VanillaTech tech) || tech.CostItems.Count == 0) {
            return GetPhaseDisplayName(phase.Phase);
        }

        double[] matrixDemand = new double[matrixIds.Length];
        double[] matrixSnapshot = new double[matrixIds.Length];
        var materialDemandParts = new List<string>();
        var materialSnapshotParts = new List<string>();

        foreach (RecipeAmount cost in tech.CostItems) {
            int matrixIndex = Array.IndexOf(matrixIds, cost.Id);
            if (matrixIndex >= 0) {
                matrixDemand[matrixIndex] += cost.Count;
                matrixSnapshot[matrixIndex] = timing.MatrixInventoryAtStart.TryGetValue(cost.Name, out double stored)
                    ? stored
                    : 0;
                continue;
            }

            materialDemandParts.Add($"{cost.Name}x{FormatAmount(cost.Count)}");
            double snapshotCount = phase.InventorySnapshot.TryGetValue(cost.Name, out double storedCount)
                ? storedCount
                : 0;
            materialSnapshotParts.Add($"{cost.Name}{FormatAmount(snapshotCount)}");
        }

        var sections = new List<string>();
        if (matrixDemand.Any(value => value > 0)) {
            List<string> activeMatrixParts = matrixIds
                .Select((matrixId, index) => (matrixId, demand: matrixDemand[index], snapshot: matrixSnapshot[index]))
                .Where(entry => entry.demand > 0 || entry.snapshot > 0)
                .Select(entry =>
                    $"{GetItemName(entry.matrixId)}x{FormatAmount(entry.demand)}/{FormatAmount(entry.snapshot)}")
                .ToList();
            if (activeMatrixParts.Count > 0) {
                sections.Add($"矩阵 {string.Join(" / ", activeMatrixParts)}（需求/快照）");
            }
        }
        if (materialDemandParts.Count > 0) {
            sections.Add($"材料 {string.Join(" + ", materialDemandParts)}");
            sections.Add($"库存 {string.Join(" + ", materialSnapshotParts)}");
        }

        return sections.Count == 0 ? GetPhaseDisplayName(phase.Phase) : string.Join("；", sections);
    }

    private string BuildTechUnlockNotes(int techId) {
        if (!dataSet.TechsById.TryGetValue(techId, out VanillaTech tech) || tech.UnlockTargets.Count == 0) {
            return "无新增解锁";
        }

        return $"解锁 {string.Join(" / ", tech.UnlockTargets.Select(target => target.Name).Distinct())}";
    }

    private string BuildItemSourceNote(int itemId, double eventSeconds, PhaseSummary phase, double requiredCount = 0) {
        if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item)) {
            return "来源未知";
        }

        if (GetMatrixStage(itemId) > 0) {
            return BuildMatrixSourceNote(item.Name, eventSeconds, phase, requiredCount);
        }

        if (IsGatheredResource(itemId, item)) {
            int gatherBuildingId = GetGatherBuildingForTiming(phase, itemId);
            string gatherBuildingName = gatherBuildingId > 0 ? GetItemName(gatherBuildingId) : "手采";
            double gatherStartSeconds = FindFactorySupplyStartSeconds(phase, gatherBuildingId);
            int gatherTargetCount = phase.BuildingCounts.TryGetValue(gatherBuildingName, out int gatherCount)
                ? gatherCount
                : 0;
            return eventSeconds >= gatherStartSeconds
                ? $"采集供给，{gatherBuildingName} {FormatTimelineDuration(gatherStartSeconds)}起，阶段目标 {gatherTargetCount} 台"
                : $"手动采矿，{gatherBuildingName} 需到 {FormatTimelineDuration(gatherStartSeconds)}，阶段目标 {gatherTargetCount} 台";
        }

        VanillaRecipe recipe = ChoosePrimaryRecipe(itemId);
        if (recipe == null) {
            return "手动采矿+手搓";
        }

        CalcRecipe calcRecipe = GetCalcRecipe(recipe) ?? ChoosePrimaryCalcRecipe(itemId);
        int factoryId = ChooseFactoryForTiming(phase, calcRecipe, recipe);
        string factoryName = GetItemName(factoryId);
        double factorySupplyStartSeconds = FindFactorySupplyStartSeconds(phase, factoryId);
        int factoryTargetCount = phase.BuildingCounts.TryGetValue(factoryName, out int factoryCount) ? factoryCount : 0;
        return eventSeconds >= factorySupplyStartSeconds
            ? $"产线供给，{factoryName} {FormatTimelineDuration(factorySupplyStartSeconds)}起，阶段目标 {factoryTargetCount} 台"
            : $"手动采矿+手搓，{factoryName} 需到 {FormatTimelineDuration(factorySupplyStartSeconds)}，阶段目标 {factoryTargetCount} 台";
    }

    private string BuildMatrixSourceNote(string matrixName, double eventSeconds, PhaseSummary phase,
        double requiredCount) {
        if (CanManualSupplyMatrixCount(requiredCount)) {
            return "手动采矿+手搓";
        }

        double productionStartSeconds =
            phase.MatrixProductionStartSeconds.TryGetValue(matrixName, out double mappedStartSeconds)
                ? mappedStartSeconds
                : phase.StartSeconds;
        int targetCount = phase.MatrixLabCounts.TryGetValue(matrixName, out int count) ? count : 0;
        return eventSeconds >= productionStartSeconds
            ? $"矩阵产线供给，矩阵研究站 {FormatTimelineDuration(productionStartSeconds)}起，阶段目标 {targetCount} 台"
            : $"库存/待产线供给，矩阵研究站需到 {FormatTimelineDuration(productionStartSeconds)}，阶段目标 {targetCount} 台";
    }

    private double FindFactorySupplyStartSeconds(PhaseSummary phase, int factoryItemId) {
        if (factoryItemId <= 0) {
            return phase.StartSeconds;
        }

        return GetItemAvailabilitySeconds(phase, factoryItemId);
    }

    private int ChooseFactoryForTiming(PhaseSummary phase, CalcRecipe calcRecipe, VanillaRecipe recipe) {
        List<int> factories = calcRecipe?.Factories?.Where(id => id > 1).ToList() ?? [];
        if (factories.Count == 0) {
            return GetFallbackFactory(recipe);
        }

        return factories
            .OrderBy(factoryId => GetItemAvailabilitySeconds(phase: phase, itemId: factoryId))
            .ThenByDescending(factoryId => factorySpeedByItemId.TryGetValue(factoryId, out double speed) ? speed : 1.0)
            .First();
    }

    private int GetGatherBuildingForTiming(PhaseSummary phase, int itemId) {
        int baseBuildingId = itemId switch {
            1000 => 2306,
            1007 => 2307,
            _ => 2301,
        };
        if (itemId is 1000 or 1007) {
            return baseBuildingId;
        }

        double largeMinerAvailability = GetItemAvailabilitySeconds(phase, 2316);
        double baseMinerAvailability = GetItemAvailabilitySeconds(phase, baseBuildingId);
        return largeMinerAvailability < double.MaxValue && largeMinerAvailability <= baseMinerAvailability
            ? 2316
            : baseBuildingId;
    }

    private double GetItemAvailabilitySeconds(PhaseSummary phase, int itemId) {
        if (itemId <= 0 || !dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item)) {
            return phase.StartSeconds;
        }

        if (string.IsNullOrEmpty(item.PreTechCode) || item.PreTechCode[0] != 'T') {
            return phase.StartSeconds;
        }

        if (!int.TryParse(item.PreTechCode.Substring(1), out int unlockTechId)
            || !dataSet.TechsById.TryGetValue(unlockTechId, out VanillaTech unlockTech)) {
            return phase.StartSeconds;
        }

        ProgressPhase unlockPhase = GetPhase(unlockTech);
        if (unlockPhase < phase.Phase) {
            return phase.StartSeconds;
        }

        if (unlockPhase > phase.Phase) {
            return double.MaxValue;
        }

        TechTiming unlockTiming = FindUnlockTimingForItem(phase, itemId);
        return unlockTiming?.EndSeconds ?? double.MaxValue;
    }

    private void PopulateMatrixLineTimelineData(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedItemsBeforePhase,
        HashSet<int> unlockedTechsBeforePhase, Dictionary<int, int> carriedMatrixLabCounts,
        Dictionary<int, int> activeBuildCounts,
        Dictionary<int, double> currentInventory) {
        phase.MatrixBuildStartSeconds.Clear();
        phase.MatrixProductionStartSeconds.Clear();
        phase.MatrixBuildNotes.Clear();
        phase.MatrixProductionNotes.Clear();
        phase.MatrixLabCount = 0;
        phase.MatrixLabBaseCount = 0;

        Dictionary<int, double> itemUnlockSeconds = BuildPhaseItemUnlockSeconds(phase, unlockedItemsBeforePhase);
        HashSet<int> phaseUnlockedItems = BuildPhaseUnlockedItems(phase, unlockedItemsBeforePhase);
        HashSet<int> buildAvailableItems = [.. unlockedItemsBeforePhase];
        TechTiming powerPriorityTiming = FindPhasePowerPriorityTiming(strategyKind, phase, unlockedTechsBeforePhase);
        double powerReadySeconds = powerPriorityTiming?.EndSeconds ?? phase.StartSeconds;

        foreach (var pair in phase.MatrixRatesPerSecond.OrderByDescending(p => p.Value).ThenBy(p => p.Key)) {
            int matrixItemId = TryGetItemIdByName(pair.Key);
            if (matrixItemId <= 0) {
                continue;
            }
            int existingLabCount = carriedMatrixLabCounts.TryGetValue(matrixItemId, out int mappedExistingLabCount)
                ? mappedExistingLabCount
                : 0;

            var buildingDemand = new Dictionary<int, double>();
            var resourceDemand = new Dictionary<int, double>();
            // 开线时先按阶段起点已解锁的机器估算，避免把本阶段后续升级科技反向当成开线前置。
            AccumulateDemand(matrixItemId, pair.Value, buildAvailableItems, buildingDemand, resourceDemand,
                new HashSet<int>());

            Dictionary<int, int> requiredBuildingCounts = buildingDemand
                .Where(demand =>
                    demand.Value > 0
                    && dataSet.ItemsById.TryGetValue(demand.Key, out VanillaItem item)
                    && item.CanBuild)
                .ToDictionary(demand => demand.Key, demand => RoundUpCount(demand.Value));
            Dictionary<int, int> addedBuildingCounts = requiredBuildingCounts
                .Select(demand => (itemId: demand.Key, count: Math.Max(0, demand.Value
                                                                          - GetExistingBuildingCountForMatrixLine(
                                                                              demand.Key, matrixItemId,
                                                                              existingLabCount, activeBuildCounts))))
                .Where(demand => demand.count > 0)
                .ToDictionary(demand => demand.itemId, demand => demand.count);
            int actualLabCount = labUpgradeItemIds.Sum(labItemId =>
                requiredBuildingCounts.TryGetValue(labItemId, out int count) ? count : 0);
            if (actualLabCount < existingLabCount) {
                actualLabCount = existingLabCount;
            }
            if (actualLabCount > 0) {
                int actualBaseCount = GetMatrixLabBaseCount(actualLabCount, phase.LabStackLevel);
                phase.MatrixLabCounts[pair.Key] = actualLabCount;
                phase.MatrixLabBaseCounts[pair.Key] = actualBaseCount;
                phase.MatrixLabCount += actualLabCount;
                phase.MatrixLabBaseCount += actualBaseCount;
            }
            HashSet<int> prerequisiteItemIds = [];
            CollectRequiredProductionItemIds(matrixItemId, phaseUnlockedItems, prerequisiteItemIds, new HashSet<int>());
            foreach (int buildingItemId in requiredBuildingCounts.Keys) {
                prerequisiteItemIds.Add(buildingItemId);
            }

            double buildStartSeconds = Math.Max(
                GetLatestUnlockSeconds(phase.StartSeconds, itemUnlockSeconds, prerequisiteItemIds),
                powerReadySeconds);
            Dictionary<int, int> bootstrapAddedBuildingCounts = BuildBootstrapAddedBuildingCounts(strategyKind,
                actualLabCount, existingLabCount,
                requiredBuildingCounts, matrixItemId, activeBuildCounts);
            double buildLeadSeconds = EstimateBuildLeadSeconds(strategyKind, bootstrapAddedBuildingCounts,
                buildAvailableItems, unlockedTechsBeforePhase, currentInventory);
            double productionStartSeconds = existingLabCount > 0 ? phase.StartSeconds :
                addedBuildingCounts.Count == 0 ? phase.StartSeconds : buildStartSeconds + buildLeadSeconds;
            string prerequisiteTechNotes = BuildUnlockTechSummary(phase, prerequisiteItemIds, powerPriorityTiming);
            string addedBuildingNotes = FormatBuildingCountSummary(addedBuildingCounts);
            string requiredBuildingNotes = FormatBuildingCountSummary(requiredBuildingCounts);
            string directInputNotes = BuildDirectInputRateSummary(matrixItemId, pair.Value, phaseUnlockedItems);

            phase.MatrixBuildStartSeconds[pair.Key] = buildStartSeconds;
            phase.MatrixProductionStartSeconds[pair.Key] = productionStartSeconds;
            phase.MatrixBuildNotes[pair.Key] = addedBuildingCounts.Count == 0
                ? string.Empty
                : $"前置科技 {prerequisiteTechNotes}；需新增建筑 {addedBuildingNotes}；投产建筑 {requiredBuildingNotes}；需物品 {directInputNotes}";
            phase.MatrixProductionNotes[pair.Key] =
                $"{BuildMatrixLabNotes(phase, pair.Key)}；前置科技 {prerequisiteTechNotes}；投产建筑 {requiredBuildingNotes}；需物品 {directInputNotes}";
        }
    }

    private HashSet<int> BuildPhaseUnlockedItems(PhaseSummary phase, HashSet<int> unlockedItemsBeforePhase) {
        var phaseUnlockedItems = new HashSet<int>(unlockedItemsBeforePhase);
        foreach (TechTiming timing in phase.TechTimings) {
            if (!dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)) {
                continue;
            }

            foreach (UnlockTarget target in tech.UnlockTargets) {
                foreach (int itemId in ResolveUnlockedItemIds(target)) {
                    phaseUnlockedItems.Add(itemId);
                }
            }
        }

        return phaseUnlockedItems;
    }

    private Dictionary<int, double> BuildPhaseItemUnlockSeconds(PhaseSummary phase,
        HashSet<int> unlockedItemsBeforePhase) {
        var unlockSeconds = new Dictionary<int, double>();
        foreach (int itemId in unlockedItemsBeforePhase) {
            unlockSeconds[itemId] = phase.StartSeconds;
        }

        foreach (TechTiming timing in phase.TechTimings) {
            if (!dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)) {
                continue;
            }

            foreach (UnlockTarget target in tech.UnlockTargets) {
                foreach (int itemId in ResolveUnlockedItemIds(target)) {
                    unlockSeconds[itemId] = unlockSeconds.TryGetValue(itemId, out double existingSeconds)
                        ? Math.Min(existingSeconds, timing.EndSeconds)
                        : timing.EndSeconds;
                }
            }
        }

        return unlockSeconds;
    }

    private void CollectRequiredProductionItemIds(int itemId, HashSet<int> unlockedItems, HashSet<int> requiredItemIds,
        HashSet<int> visiting) {
        if (!requiredItemIds.Add(itemId) || !visiting.Add(itemId)) {
            return;
        }

        VanillaRecipe recipe = ChoosePrimaryRecipe(itemId);
        if (recipe != null) {
            foreach (RecipeAmount input in recipe.Inputs) {
                CollectRequiredProductionItemIds(input.Id, unlockedItems, requiredItemIds, visiting);
            }
        }

        visiting.Remove(itemId);
    }

    private double GetLatestUnlockSeconds(double defaultSeconds, Dictionary<int, double> itemUnlockSeconds,
        IEnumerable<int> itemIds) {
        double latestSeconds = defaultSeconds;
        foreach (int itemId in itemIds.Distinct()) {
            if (itemUnlockSeconds.TryGetValue(itemId, out double unlockSeconds)) {
                latestSeconds = Math.Max(latestSeconds, unlockSeconds);
            }
        }
        return latestSeconds;
    }

    private static int GetActiveBuildCount(Dictionary<int, int> activeBuildCounts, int itemId) {
        return activeBuildCounts.TryGetValue(itemId, out int count) ? count : 0;
    }

    private int GetExistingBuildingCountForMatrixLine(int buildingItemId, int matrixItemId, int existingLabCount,
        Dictionary<int, int> activeBuildCounts) {
        if (labUpgradeItemIds.Contains(buildingItemId)) {
            return existingLabCount;
        }

        return GetActiveBuildCount(activeBuildCounts, buildingItemId);
    }

    private Dictionary<int, int> BuildBootstrapAddedBuildingCounts(PlayerStrategyKind strategyKind, int actualLabCount,
        int existingLabCount,
        Dictionary<int, int> requiredBuildingCounts, int matrixItemId, Dictionary<int, int> activeBuildCounts) {
        if (existingLabCount > 0 || actualLabCount <= 0) {
            return [];
        }

        int bootstrapLabCount = GetBootstrapMatrixLabCount(strategyKind, actualLabCount);
        double bootstrapRatio = actualLabCount <= 0 ? 1.0 : bootstrapLabCount / (double)actualLabCount;
        var bootstrapCounts = new Dictionary<int, int>();
        foreach (var pair in requiredBuildingCounts) {
            int targetCount = labUpgradeItemIds.Contains(pair.Key)
                ? bootstrapLabCount
                : Math.Max(1, (int)Math.Ceiling(pair.Value * bootstrapRatio));
            int existingCount =
                GetExistingBuildingCountForMatrixLine(pair.Key, matrixItemId, existingLabCount, activeBuildCounts);
            int deltaCount = Math.Max(0, targetCount - existingCount);
            if (deltaCount > 0) {
                bootstrapCounts[pair.Key] = deltaCount;
            }
        }
        return bootstrapCounts;
    }

    private int GetBootstrapMatrixLabCount(PlayerStrategyKind strategyKind, int actualLabCount) {
        double ratio = strategyKind == PlayerStrategyKind.Conventional ? 0.01 : 0.005;
        return Math.Max(1, Math.Min(actualLabCount, (int)Math.Ceiling(actualLabCount * ratio)));
    }

    private double EstimateBuildLeadSeconds(PlayerStrategyKind strategyKind, Dictionary<int, int> addedBuildingCounts,
        HashSet<int> unlockedItems,
        HashSet<int> unlockedTechs, Dictionary<int, double> currentInventory) {
        double maxLeadSeconds = 0;
        int parallelFactor = GetBuildParallelFactor(strategyKind, unlockedTechs, unlockedItems);
        foreach (var pair in addedBuildingCounts) {
            double currentStored = currentInventory.TryGetValue(pair.Key, out double stored) ? stored : 0;
            double shortage = Math.Max(0, pair.Value - currentStored);
            if (shortage <= 0) {
                continue;
            }

            double buildRate = GetBackgroundBuildRatePerSecond(pair.Key, unlockedItems) * parallelFactor;
            if (buildRate <= 0) {
                continue;
            }

            maxLeadSeconds = Math.Max(maxLeadSeconds, shortage / buildRate);
        }

        return maxLeadSeconds;
    }

    private int GetBuildParallelFactor(PlayerStrategyKind strategyKind, HashSet<int> unlockedTechs,
        HashSet<int> unlockedItems) {
        double fullBeltRate = GetCurrentDynamicFullBeltRatePerSecond(unlockedTechs, unlockedItems);
        return strategyKind switch {
            PlayerStrategyKind.Conventional => Math.Max(1, (int)Math.Ceiling(fullBeltRate)),
            PlayerStrategyKind.Speedrun => Math.Max(1, (int)Math.Ceiling(fullBeltRate / 2.0)),
            _ => 1,
        };
    }

    private string BuildUnlockTechSummary(PhaseSummary phase, IEnumerable<int> itemIds, TechTiming extraTiming = null) {
        List<TechTiming> unlockTimings = itemIds
            .Distinct()
            .Select(itemId => FindUnlockTimingForItem(phase, itemId))
            .Where(timing => timing != null)
            .GroupBy(timing => timing.TechId)
            .Select(group => group.First())
            .OrderBy(timing => timing.EndSeconds)
            .ToList();
        if (extraTiming != null && unlockTimings.All(timing => timing.TechId != extraTiming.TechId)) {
            unlockTimings.Add(extraTiming);
            unlockTimings = unlockTimings.OrderBy(timing => timing.EndSeconds).ToList();
        }

        return unlockTimings.Count == 0
            ? "无新增科技前置"
            : string.Join(" / ", unlockTimings.Select(timing => $"{timing.TechCode} {timing.TechName}"));
    }

    private TechTiming FindPhasePowerPriorityTiming(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedTechsBeforePhase) {
        if (phase.Phase == ProgressPhase.Electromagnetic) {
            // 蓝糖首线默认允许直接吃前期风电，避免把火电科技反向当成开线前置。
            return null;
        }

        bool alreadyUnlockedPowerTech = unlockedTechsBeforePhase
            .Where(techId => dataSet.TechsById.ContainsKey(techId))
            .Select(techId => dataSet.TechsById[techId])
            .Any(tech => tech.UnlockTargets.Any(IsPowerUnlock));
        if (alreadyUnlockedPowerTech) {
            return null;
        }

        return phase.TechTimings
            .Where(timing => dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)
                             && tech.UnlockTargets.Any(IsPowerUnlock))
            .OrderBy(timing => timing.EndSeconds)
            .ThenBy(timing => timing.PhaseIndex)
            .FirstOrDefault();
    }

    private TechTiming FindUnlockTimingForItem(PhaseSummary phase, int itemId) {
        foreach (TechTiming timing in phase.TechTimings.OrderBy(timing => timing.EndSeconds)
                     .ThenBy(timing => timing.PhaseIndex)) {
            if (!dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)) {
                continue;
            }

            foreach (UnlockTarget target in tech.UnlockTargets) {
                if (ResolveUnlockedItemIds(target).Contains(itemId)) {
                    return timing;
                }
            }
        }

        return null;
    }

    private string FormatBuildingCountSummary(Dictionary<int, int> buildingCounts) {
        return buildingCounts.Count == 0
            ? "无"
            : string.Join(" / ", buildingCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
                .Select(pair => $"{GetItemName(pair.Key)} x{pair.Value}"));
    }

    private string BuildDirectInputRateSummary(int outputItemId, double outputRatePerSecond,
        HashSet<int> unlockedItems) {
        VanillaRecipe recipe = ChoosePrimaryRecipe(outputItemId);
        if (recipe == null || recipe.Inputs.Count == 0) {
            return "无";
        }

        RecipeSprayMode sprayMode = GetSprayMode(recipe);
        double outputCount = recipe.Outputs.Where(output => output.Id == outputItemId).Sum(output => output.Count);
        if (outputCount <= 0) {
            return "无";
        }

        double outputMultiplier = GetOutputMultiplier(recipe, sprayMode);
        double effectiveOutputCount = outputCount * outputMultiplier;
        if (effectiveOutputCount <= 0) {
            return "无";
        }

        return string.Join(" / ", recipe.Inputs
            .Select(input => $"{input.Name} {outputRatePerSecond * input.Count / effectiveOutputCount * 60:0.##}/min"));
    }

    private string GetItemName(int itemId) {
        return dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item)
            ? item.Name
            : itemId.ToString();
    }

    private void ComputePhaseCapacity(PlayerStrategyKind strategyKind, StrategySimulationResult result) {
        var unlockedItems = new HashSet<int>();
        var builtFactoryCounts = new Dictionary<int, int>();
        var currentInventory = new Dictionary<int, double>();
        var currentMatrixInventory = new Dictionary<int, double>();
        var currentMatrixLabCounts = new Dictionary<int, int>();
        var unlockedTechs = new HashSet<int> { 1 };
        var techUnlockSeconds = new Dictionary<int, double> { [1] = 0 };
        double cumulativeSeconds = 0;
        foreach (PhaseSummary phase in result.PhaseSummaries.OrderBy(p => p.Phase)) {
            phase.StartSeconds = cumulativeSeconds;
            int phaseIndex = (int)phase.Phase;
            int labCount = HasResearchLabUnlocked(unlockedTechs)
                ? GetResearchLabCount(strategyKind, phaseIndex)
                : 0;
            phase.LabStackLevel = GetCurrentLabStackLevel(unlockedTechs);
            phase.ResearchLabCount = labCount;
            phase.ResearchLabBaseCount = (int)Math.Ceiling(labCount / (double)Math.Max(1, phase.LabStackLevel));
            phase.MatrixLabCount = 0;
            phase.MatrixLabBaseCount = 0;

            phase.EstimatedResearchSeconds = EstimatePhaseResearchSeconds(strategyKind, phase, unlockedTechs,
                unlockedItems,
                currentMatrixLabCounts, phaseIndex);
            HashSet<int> unlockedItemsBeforePhase = [.. unlockedItems];
            HashSet<int> unlockedTechsBeforePhase = [.. unlockedTechs];
            Dictionary<int, double> matrixInventoryBeforePhase = currentMatrixInventory
                .Where(pair => GetMatrixStage(pair.Key) > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            EstimateMatrixLimitedSeconds(strategyKind, phase, unlockedItemsBeforePhase, unlockedTechsBeforePhase,
                currentMatrixLabCounts, phaseIndex,
                phase.EstimatedResearchSeconds);
            PopulateMatrixLineTimelineData(strategyKind, phase, unlockedItemsBeforePhase, unlockedTechsBeforePhase,
                currentMatrixLabCounts, builtFactoryCounts, currentInventory);
            for (int iteration = 0; iteration < MaxTimelineIterations; iteration++) {
                List<(double startSeconds, double endSeconds)> previousTimingWindows = phase.TechTimings
                    .OrderBy(timing => timing.PhaseIndex)
                    .Select(timing => (timing.StartSeconds, timing.EndSeconds))
                    .ToList();
                var previousMatrixProductionStartSeconds =
                    new Dictionary<string, double>(phase.MatrixProductionStartSeconds);

                RecalculatePhaseTechTimingsWithMatrixAvailability(strategyKind, phase, unlockedTechsBeforePhase,
                    unlockedItemsBeforePhase,
                    matrixInventoryBeforePhase, phaseIndex);
                EstimateMatrixLimitedSeconds(strategyKind, phase, unlockedItemsBeforePhase, unlockedTechsBeforePhase,
                    currentMatrixLabCounts, phaseIndex,
                    phase.EstimatedResearchSeconds);
                PopulateMatrixLineTimelineData(strategyKind, phase, unlockedItemsBeforePhase, unlockedTechsBeforePhase,
                    currentMatrixLabCounts, builtFactoryCounts, currentInventory);

                bool timelineStable = phase.TechTimings
                    .OrderBy(timing => timing.PhaseIndex)
                    .Select((timing, index) =>
                        Math.Abs(timing.StartSeconds - previousTimingWindows[index].startSeconds)
                        <= TimelineConvergenceToleranceSeconds
                        && Math.Abs(timing.EndSeconds - previousTimingWindows[index].endSeconds)
                        <= TimelineConvergenceToleranceSeconds)
                    .All(stable => stable);
                bool matrixStable =
                    previousMatrixProductionStartSeconds.Count == phase.MatrixProductionStartSeconds.Count
                    && phase.MatrixProductionStartSeconds.All(pair =>
                        previousMatrixProductionStartSeconds.TryGetValue(pair.Key, out double previousStartSeconds)
                        && Math.Abs(pair.Value - previousStartSeconds) <= TimelineConvergenceToleranceSeconds);
                if (timelineStable && matrixStable) {
                    break;
                }
            }
            RecalculatePhaseTechTimingsWithMatrixAvailability(strategyKind, phase, unlockedTechsBeforePhase,
                unlockedItemsBeforePhase,
                matrixInventoryBeforePhase, phaseIndex);
            EstimateMatrixLimitedSeconds(strategyKind, phase, unlockedItemsBeforePhase, unlockedTechsBeforePhase,
                currentMatrixLabCounts, phaseIndex,
                phase.EstimatedResearchSeconds);
            PopulateMatrixLineTimelineData(strategyKind, phase, unlockedItemsBeforePhase, unlockedTechsBeforePhase,
                currentMatrixLabCounts, builtFactoryCounts, currentInventory);

            phase.ResearchEndSeconds = phase.StartSeconds + phase.EstimatedResearchSeconds;
            cumulativeSeconds = phase.ResearchEndSeconds;

            foreach (TechTiming timing in phase.TechTimings) {
                techUnlockSeconds[timing.TechId] = timing.EndSeconds;
            }
            ApplyPhaseUnlocks(phase.TechIds, unlockedTechs, unlockedItems);
            PopulateTechMatrixFlows(strategyKind, phase, unlockedTechs, unlockedItems, currentMatrixInventory);
            foreach (var pair in phase.MatrixLabCounts) {
                int itemId = TryGetItemIdByName(pair.Key);
                if (itemId > 0) {
                    currentMatrixLabCounts[itemId] = pair.Value;
                }
            }

            var itemRateDemand = new Dictionary<int, double>();
            foreach (int techId in phase.TechIds) {
                VanillaTech tech = dataSet.TechsById[techId];
                foreach (RecipeAmount cost in tech.CostItems) {
                    if (cost.Id <= 0 || cost.Count <= 0) {
                        continue;
                    }

                    itemRateDemand[cost.Id] = itemRateDemand.TryGetValue(cost.Id, out double existing)
                        ? existing + cost.Count / phase.EstimatedResearchSeconds
                        : cost.Count / phase.EstimatedResearchSeconds;
                }
            }

            var buildingDemand = new Dictionary<int, double>();
            var resourceDemand = new Dictionary<int, double>();
            foreach (var pair in itemRateDemand) {
                AccumulateDemand(pair.Key, pair.Value, unlockedItems, buildingDemand, resourceDemand,
                    new HashSet<int>());
            }

            PhaseBuildPlan buildPlan = BuildPhaseBuildPlan(phase, buildingDemand, unlockedItems, builtFactoryCounts,
                currentInventory);
            List<BlockingRecord> blockingRecords = ApplyBackgroundBuildModel(strategyKind, phase, unlockedItems,
                unlockedTechs, currentInventory, buildPlan);

            foreach (var pair in buildingDemand.OrderBy(p => p.Key)) {
                string name = dataSet.ItemsById.TryGetValue(pair.Key, out VanillaItem item)
                    ? item.Name
                    : pair.Key.ToString();
                int count = RoundUpCount(pair.Value);
                phase.BuildingCounts[name] = count;
            }

            foreach (var pair in resourceDemand.OrderBy(p => p.Key)) {
                string name = dataSet.ItemsById.TryGetValue(pair.Key, out VanillaItem item)
                    ? item.Name
                    : pair.Key.ToString();
                phase.ResourceRatesPerSecond[name] = Math.Round(pair.Value, 3);
            }

            foreach (var pair in itemRateDemand.OrderByDescending(p => p.Value)) {
                string name = dataSet.ItemsById.TryGetValue(pair.Key, out VanillaItem item)
                    ? item.Name
                    : pair.Key.ToString();
                double bufferSeconds = GetInventoryBufferSeconds(pair.Key);
                phase.InventorySnapshot[name] = Math.Round(pair.Value * bufferSeconds, 2);
            }

            ApplySupermarketModel(phase, unlockedItems, currentInventory);
            phase.EventDelaySeconds = EstimatePhaseEventDelaySeconds(phase);
            phase.EventDelayReason = DescribePhaseEventDelay(phase);
            cumulativeSeconds += phase.SupermarketFillSeconds + phase.EventDelaySeconds;
            phase.PhaseEndSeconds = cumulativeSeconds;
            phase.CumulativeSeconds = cumulativeSeconds;
            phase.TotalPowerDemandWatts = EstimatePhasePowerDemandWatts(phase, buildingDemand, unlockedItems);
            ApplyDysonPowerModel(result, strategyKind, phase, techUnlockSeconds);
            ApplyPhasePowerModel(phase, buildingDemand, unlockedItems);
            PopulatePhaseTimeline(phase, blockingRecords, techUnlockSeconds);
        }
    }

    private static PhaseSummary GetOrCreatePhaseSummary(StrategySimulationResult result, ProgressPhase phase) {
        PhaseSummary phaseSummary = result.PhaseSummaries.FirstOrDefault(p => p.Phase == phase);
        if (phaseSummary != null) {
            return phaseSummary;
        }

        phaseSummary = new PhaseSummary { Phase = phase };
        result.PhaseSummaries.Add(phaseSummary);
        return phaseSummary;
    }

    private double EstimatePhaseResearchSeconds(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedTechs, HashSet<int> unlockedItems, Dictionary<int, int> currentMatrixLabCounts,
        int phaseIndex) {
        double estimatedSeconds = 0;
        foreach (TechTiming timing in phase.TechTimings) {
            if (!dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)) {
                continue;
            }

            double researchSpeed = GetCurrentResearchSpeed(unlockedTechs);
            int labCount = HasResearchLabUnlocked(unlockedTechs)
                ? GetResearchLabCount(strategyKind, phaseIndex)
                : 0;
            estimatedSeconds += !RequiresMatrixResearch(tech) || labCount <= 0
                ? tech.HashNeeded / Math.Max(1.0, DefaultLabHashPerSecond * researchSpeed)
                : tech.HashNeeded / Math.Max(1.0, labCount * DefaultLabHashPerSecond * researchSpeed);
        }
        estimatedSeconds = Math.Max(1.0, estimatedSeconds);

        for (int iteration = 0; iteration < 2; iteration++) {
            Dictionary<int, double> matrixRatesByItemId =
                BuildPhaseMatrixRates(strategyKind, phase, unlockedItems, unlockedTechs, currentMatrixLabCounts,
                    phaseIndex,
                    estimatedSeconds);
            var techUnlockedTechs = new HashSet<int>(unlockedTechs);
            var techUnlockedItems = new HashSet<int>(unlockedItems);
            double currentSeconds = phase.StartSeconds;

            foreach (TechTiming timing in phase.TechTimings) {
                if (!dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)) {
                    timing.StartSeconds = currentSeconds;
                    timing.EndSeconds = currentSeconds;
                    continue;
                }

                double duration = EstimateSingleTechResearchSeconds(strategyKind, tech, techUnlockedTechs,
                    techUnlockedItems, phaseIndex,
                    matrixRatesByItemId);
                timing.StartSeconds = currentSeconds;
                timing.EndSeconds = currentSeconds + duration;
                currentSeconds = timing.EndSeconds;

                techUnlockedTechs.Add(tech.Id);
                foreach (UnlockTarget target in tech.UnlockTargets) {
                    foreach (int itemId in ResolveUnlockedItemIds(target)) {
                        techUnlockedItems.Add(itemId);
                    }
                }
            }

            estimatedSeconds = Math.Max(0, currentSeconds - phase.StartSeconds);
        }

        return estimatedSeconds;
    }

    private void RecalculatePhaseTechTimingsWithMatrixAvailability(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedTechsBeforePhase, HashSet<int> unlockedItemsBeforePhase,
        Dictionary<int, double> matrixInventoryBeforePhase,
        int phaseIndex) {
        var localUnlockedTechs = new HashSet<int>(unlockedTechsBeforePhase);
        var localUnlockedItems = new HashSet<int>(unlockedItemsBeforePhase);
        var localMatrixInventory = new Dictionary<int, double>(matrixInventoryBeforePhase);
        var matrixRatesByItemId = phase.MatrixRatesPerSecond
            .Select(pair => (itemId: TryGetItemIdByName(pair.Key), rate: pair.Value))
            .Where(pair => pair.itemId > 0)
            .ToDictionary(pair => pair.itemId, pair => pair.rate);
        double currentSeconds = phase.StartSeconds;
        var pendingTimings = phase.TechTimings.OrderBy(timing => timing.PhaseIndex).ToList();

        while (pendingTimings.Count > 0) {
            List<(TechTiming timing, VanillaTech tech, double startSeconds)> readyTimings = pendingTimings
                .Select(timing => (timing,
                    tech: dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech) ? tech : null))
                .Where(pair => pair.tech != null && IsUnlocked(pair.tech, localUnlockedTechs))
                .Select(pair => (pair.timing, pair.tech,
                    startSeconds: GetTechEarliestStartSeconds(phase, pair.tech, currentSeconds)))
                .ToList();
            if (phase.Phase == ProgressPhase.Electromagnetic) {
                double electromagneticProductionReadySeconds =
                    phase.MatrixProductionStartSeconds.TryGetValue("电磁矩阵", out double mappedStartSeconds)
                        ? mappedStartSeconds
                        : double.MaxValue;
                if (currentSeconds + TimelineConvergenceToleranceSeconds < electromagneticProductionReadySeconds) {
                    List<(TechTiming timing, VanillaTech tech, double startSeconds)> bootstrapCandidates = readyTimings
                        .Where(pair =>
                            !RequiresMatrixProductionLine(pair.tech)
                            || IsElectromagneticLinePrerequisiteTech(pair.tech.Id))
                        .ToList();
                    if (bootstrapCandidates.Count > 0) {
                        readyTimings = bootstrapCandidates;
                    }
                }
            }

            if (readyTimings.Count == 0) {
                foreach (TechTiming timing in pendingTimings) {
                    timing.StartSeconds = currentSeconds;
                    timing.EndSeconds = currentSeconds;
                }
                break;
            }

            (TechTiming timing, VanillaTech tech, double startSeconds) selected = readyTimings
                .OrderBy(pair => pair.startSeconds)
                .ThenBy(pair => pair.timing.PhaseIndex)
                .First();

            double researchSpeed = GetCurrentResearchSpeed(localUnlockedTechs);
            if (researchSpeed <= 0) {
                researchSpeed = 1;
            }

            int labCount = HasResearchLabUnlocked(localUnlockedTechs)
                ? GetResearchLabCount(strategyKind, phaseIndex)
                : 0;
            double hashLimitedSeconds = !RequiresMatrixResearch(selected.tech) || labCount <= 0
                ? selected.tech.HashNeeded / (DefaultLabHashPerSecond * researchSpeed)
                : selected.tech.HashNeeded / Math.Max(1.0, labCount * DefaultLabHashPerSecond * researchSpeed);
            double matrixLimitedSeconds = GetTechMatrixWaitSeconds(phase, selected.tech, selected.startSeconds,
                localMatrixInventory,
                matrixRatesByItemId);
            double duration = Math.Max(hashLimitedSeconds, matrixLimitedSeconds);

            selected.timing.StartSeconds = selected.startSeconds;
            selected.timing.EndSeconds = selected.startSeconds + duration;
            currentSeconds = selected.timing.EndSeconds;

            foreach (int matrixItemId in matrixRatesByItemId.Keys) {
                double rate = matrixRatesByItemId[matrixItemId];
                string matrixName = GetItemName(matrixItemId);
                double productionStartSeconds =
                    phase.MatrixProductionStartSeconds.TryGetValue(matrixName, out double mappedStartSeconds)
                        ? mappedStartSeconds
                        : phase.StartSeconds;
                double producedSeconds = Math.Max(0,
                    selected.timing.EndSeconds - Math.Max(selected.timing.StartSeconds, productionStartSeconds));
                double producedCount = producedSeconds * rate;
                double consumedCount = selected.tech.CostItems.Where(cost => cost.Id == matrixItemId)
                    .Sum(cost => cost.Count);
                double updatedInventory = (localMatrixInventory.TryGetValue(matrixItemId, out double existingInventory)
                                              ? existingInventory
                                              : 0)
                                          + producedCount
                                          - consumedCount;
                localMatrixInventory[matrixItemId] = Math.Max(0, updatedInventory);
            }

            localUnlockedTechs.Add(selected.tech.Id);
            foreach (UnlockTarget target in selected.tech.UnlockTargets) {
                foreach (int itemId in ResolveUnlockedItemIds(target)) {
                    localUnlockedItems.Add(itemId);
                }
            }

            pendingTimings.Remove(selected.timing);
        }

        phase.EstimatedResearchSeconds = Math.Max(0, currentSeconds - phase.StartSeconds);
    }

    private double GetTechEarliestStartSeconds(PhaseSummary phase, VanillaTech tech, double currentSeconds) {
        double startSeconds = currentSeconds;
        if (!RequiresMatrixResearch(tech)) {
            return startSeconds;
        }

        foreach (RecipeAmount cost in tech.CostItems.Where(cost => GetMatrixStage(cost.Id) > 0)) {
            double unlockReadySeconds = FindUnlockTimingForItem(phase, cost.Id)?.EndSeconds ?? phase.StartSeconds;
            if (CanManualSupplyMatrixCount(cost.Count)) {
                startSeconds = Math.Max(startSeconds, unlockReadySeconds);
                continue;
            }

            double productionReadySeconds =
                phase.MatrixProductionStartSeconds.TryGetValue(cost.Name, out double mappedStartSeconds)
                    ? mappedStartSeconds
                    : phase.StartSeconds;
            startSeconds = Math.Max(startSeconds, Math.Max(unlockReadySeconds, productionReadySeconds));
        }

        return startSeconds;
    }

    private double GetTechMatrixWaitSeconds(PhaseSummary phase, VanillaTech tech, double startSeconds,
        Dictionary<int, double> localMatrixInventory, Dictionary<int, double> matrixRatesByItemId) {
        double matrixLimitedSeconds = 0;
        foreach (RecipeAmount cost in tech.CostItems.Where(cost => GetMatrixStage(cost.Id) > 0)) {
            if (CanManualSupplyMatrixCount(cost.Count)) {
                continue;
            }

            double availableInventory = localMatrixInventory.TryGetValue(cost.Id, out double stored) ? stored : 0;
            double shortage = Math.Max(0, cost.Count - availableInventory);
            if (shortage <= 0) {
                continue;
            }

            double productionStartSeconds =
                phase.MatrixProductionStartSeconds.TryGetValue(cost.Name, out double mappedStartSeconds)
                    ? mappedStartSeconds
                    : phase.StartSeconds;
            double rate = matrixRatesByItemId.TryGetValue(cost.Id, out double mappedRate) ? mappedRate : 0;
            double earliestProductionSeconds = Math.Max(startSeconds, productionStartSeconds);
            if (rate <= 0) {
                continue;
            }

            double waitSeconds = Math.Max(0, earliestProductionSeconds - startSeconds) + shortage / rate;
            matrixLimitedSeconds = Math.Max(matrixLimitedSeconds, waitSeconds);
        }

        return matrixLimitedSeconds;
    }

    private static bool RequiresMatrixProductionLine(VanillaTech tech) =>
        tech.CostItems.Any(cost => GetMatrixStage(cost.Id) > 0 && !CanManualSupplyMatrixCount(cost.Count));

    private double EstimateSingleTechResearchSeconds(PlayerStrategyKind strategyKind, VanillaTech tech,
        HashSet<int> unlockedTechs, HashSet<int> unlockedItems, int phaseIndex,
        Dictionary<int, double> matrixRatesByItemId) {
        double researchSpeed = GetCurrentResearchSpeed(unlockedTechs);
        if (researchSpeed <= 0) {
            researchSpeed = 1;
        }

        if (!RequiresMatrixResearch(tech) || !HasResearchLabUnlocked(unlockedTechs)) {
            return tech.HashNeeded / (DefaultLabHashPerSecond * researchSpeed);
        }

        int labCount = GetResearchLabCount(strategyKind, phaseIndex);
        double hashLimitedSeconds = tech.HashNeeded / Math.Max(1.0, labCount * DefaultLabHashPerSecond * researchSpeed);
        double matrixLimitedSeconds = EstimateTechMatrixLimitedSeconds(tech, matrixRatesByItemId);
        return Math.Max(hashLimitedSeconds, matrixLimitedSeconds);
    }

    private static bool RequiresMatrixResearch(VanillaTech tech) =>
        tech.CostItems.Any(cost => GetMatrixStage(cost.Id) > 0);

    private static bool HasResearchLabUnlocked(HashSet<int> unlockedTechs) =>
        unlockedTechs.Contains(1002);

    private static double GetCurrentResearchSpeed(HashSet<int> unlockedTechs) =>
        Math.Max(1, 1 + researchSpeedTechIds.Count(unlockedTechs.Contains));

    private void ApplyPhaseUnlocks(IEnumerable<int> techIds, HashSet<int> unlockedTechs, HashSet<int> unlockedItems) {
        foreach (int techId in techIds) {
            unlockedTechs.Add(techId);
            if (!dataSet.TechsById.TryGetValue(techId, out VanillaTech tech)) {
                continue;
            }

            foreach (UnlockTarget target in tech.UnlockTargets) {
                foreach (int itemId in ResolveUnlockedItemIds(target)) {
                    unlockedItems.Add(itemId);
                }
            }
        }
    }

    private void PopulateTechMatrixFlows(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedTechs,
        HashSet<int> unlockedItems, Dictionary<int, double> currentMatrixInventory) {
        Dictionary<int, double> productionCapacityByItemId = phase.MatrixRatesPerSecond
            .Select(pair => (itemId: TryGetItemIdByName(pair.Key), rate: pair.Value))
            .Where(pair => pair.itemId > 0)
            .ToDictionary(pair => pair.itemId, pair => pair.rate);
        var actualProductionMaxByItemId = new Dictionary<int, double>();

        foreach (TechTiming timing in phase.TechTimings) {
            timing.MatrixRequiredTotals.Clear();
            timing.MatrixInventoryAtStart.Clear();
            timing.MatrixInventoryAtEnd.Clear();
            timing.MatrixProductionRatesPerSecond.Clear();
            timing.MatrixConsumptionRatesPerSecond.Clear();

            if (!dataSet.TechsById.TryGetValue(timing.TechId, out VanillaTech tech)) {
                continue;
            }

            double duration = Math.Max(0, timing.EndSeconds - timing.StartSeconds);
            HashSet<int> trackedMatrixIds = [.. productionCapacityByItemId.Keys];
            foreach (RecipeAmount cost in tech.CostItems.Where(cost => GetMatrixStage(cost.Id) > 0)) {
                trackedMatrixIds.Add(cost.Id);
            }
            foreach (int matrixItemId in currentMatrixInventory.Keys.Where(id => GetMatrixStage(id) > 0)) {
                trackedMatrixIds.Add(matrixItemId);
            }

            foreach (int matrixItemId in trackedMatrixIds.OrderBy(id => id)) {
                string matrixName = dataSet.ItemsById.TryGetValue(matrixItemId, out VanillaItem matrixItem)
                    ? matrixItem.Name
                    : matrixItemId.ToString();
                double startInventory =
                    currentMatrixInventory.TryGetValue(matrixItemId, out double stored) ? stored : 0;
                double productionCapacity =
                    productionCapacityByItemId.TryGetValue(matrixItemId, out double rate) ? rate : 0;
                double productionStartSeconds =
                    phase.MatrixProductionStartSeconds.TryGetValue(matrixName, out double mappedStartSeconds)
                        ? mappedStartSeconds
                        : phase.StartSeconds;
                double consumptionRate = 0;
                double consumptionCount = tech.CostItems.Where(cost => cost.Id == matrixItemId).Sum(cost => cost.Count);
                if (duration > 0 && consumptionCount > 0) {
                    consumptionRate = consumptionCount / duration;
                }
                double targetEndInventory = EstimateTargetMatrixInventory(strategyKind, phase, timing.PhaseIndex,
                    matrixItemId,
                    unlockedTechs, unlockedItems);
                double productionRate = productionCapacity;
                if (duration > 0 && productionCapacity > 0) {
                    double availableSeconds = Math.Max(0,
                        timing.EndSeconds - Math.Max(timing.StartSeconds, productionStartSeconds));
                    if (availableSeconds <= 0) {
                        productionRate = 0;
                    } else {
                        double maxProducedCount = productionCapacity * availableSeconds;
                        double requiredProducedCount =
                            Math.Max(0, targetEndInventory - startInventory + consumptionCount);
                        double actualProducedCount = Math.Min(maxProducedCount,
                            requiredProducedCount <= 0 ? maxProducedCount : requiredProducedCount);
                        productionRate = actualProducedCount / duration;
                    }
                }

                if (startInventory > 0 || productionRate > 0 || consumptionRate > 0) {
                    timing.MatrixInventoryAtStart[matrixName] = Math.Round(startInventory, 2);
                }
                if (consumptionCount > 0) {
                    timing.MatrixRequiredTotals[matrixName] = Math.Round(consumptionCount, 2);
                }
                if (productionRate > 0) {
                    timing.MatrixProductionRatesPerSecond[matrixName] = Math.Round(productionRate, 3);
                    actualProductionMaxByItemId[matrixItemId] =
                        actualProductionMaxByItemId.TryGetValue(matrixItemId, out double existingMax)
                            ? Math.Max(existingMax, productionRate)
                            : productionRate;
                }
                if (consumptionRate > 0) {
                    timing.MatrixConsumptionRatesPerSecond[matrixName] = Math.Round(consumptionRate, 3);
                }

                double endInventory = Math.Max(0, startInventory + productionRate * duration - consumptionCount);
                currentMatrixInventory[matrixItemId] = endInventory;
                int inventoryLimit = GetCurrentMatrixInventoryLimit(matrixItemId, unlockedTechs, dataSet);
                if (inventoryLimit > 0 && currentMatrixInventory[matrixItemId] > inventoryLimit) {
                    currentMatrixInventory[matrixItemId] = inventoryLimit;
                    endInventory = inventoryLimit;
                }

                if (startInventory > 0 || productionRate > 0 || consumptionRate > 0 || endInventory > 0) {
                    timing.MatrixInventoryAtEnd[matrixName] = Math.Round(endInventory, 2);
                }
            }
        }

        phase.MatrixRatesPerSecond.Clear();
        foreach (var pair in actualProductionMaxByItemId.OrderBy(pair => pair.Key)) {
            string matrixName = dataSet.ItemsById.TryGetValue(pair.Key, out VanillaItem matrixItem)
                ? matrixItem.Name
                : pair.Key.ToString();
            phase.MatrixRatesPerSecond[matrixName] = Math.Round(pair.Value, 3);
        }
    }

    private double EstimateTargetMatrixInventory(PlayerStrategyKind strategyKind, PhaseSummary phase,
        int currentPhaseIndex,
        int matrixItemId, HashSet<int> unlockedTechs, HashSet<int> unlockedItems) {
        int inventoryLimit = GetCurrentMatrixInventoryLimit(matrixItemId, unlockedTechs, dataSet);
        if (inventoryLimit <= 0) {
            return 0;
        }

        if (strategyKind != PlayerStrategyKind.Speedrun) {
            return inventoryLimit;
        }

        double futureDemand = 0;
        for (int index = currentPhaseIndex + 1; index < phase.TechTimings.Count; index++) {
            TechTiming nextTiming = phase.TechTimings[index];
            if (!dataSet.TechsById.TryGetValue(nextTiming.TechId, out VanillaTech nextTech)) {
                continue;
            }

            futureDemand += nextTech.CostItems.Where(cost => cost.Id == matrixItemId).Sum(cost => cost.Count);
        }

        if (futureDemand <= 0) {
            return 0;
        }

        double blueprintRate = GetCurrentDynamicFullBeltRatePerSecond(unlockedTechs, unlockedItems);
        double boundedDemand = Math.Min(inventoryLimit, futureDemand);
        return Math.Min(boundedDemand, Math.Max(0, blueprintRate * 60.0));
    }

    private int TryGetItemIdByName(string itemName) {
        foreach (var pair in dataSet.ItemsById) {
            if (pair.Value.Name == itemName) {
                return pair.Key;
            }
        }
        return 0;
    }

    private static void AddBuildDemand(Dictionary<int, double> demandByItemId, Dictionary<int, string> reasonByItemId,
        int itemId,
        double count, string reason) {
        if (itemId <= 0 || count <= 0) {
            return;
        }

        demandByItemId[itemId] = demandByItemId.TryGetValue(itemId, out double existing) ? existing + count : count;
        if (!reasonByItemId.TryGetValue(itemId, out string existingReason) || string.IsNullOrEmpty(existingReason)) {
            reasonByItemId[itemId] = reason;
            return;
        }

        if (!existingReason.Contains(reason)) {
            reasonByItemId[itemId] = $"{existingReason} / {reason}";
        }
    }

    private int DismantleLowerTierBuildFamily(Dictionary<int, int> activeBuildCounts,
        Dictionary<int, double> currentInventory,
        int preferredItemId) {
        int[] family = GetUpgradeFamily(preferredItemId);
        if (family.Length == 0) {
            return 0;
        }

        int dismantledCount = 0;
        foreach (int itemId in family) {
            if (itemId == preferredItemId || !IsHigherTierUpgrade(preferredItemId, itemId)) {
                continue;
            }

            if (!activeBuildCounts.TryGetValue(itemId, out int activeCount) || activeCount <= 0) {
                continue;
            }

            currentInventory[itemId] = currentInventory.TryGetValue(itemId, out double existingInventory)
                ? existingInventory + activeCount
                : activeCount;
            activeBuildCounts[itemId] = 0;
            dismantledCount += activeCount;
        }

        return dismantledCount;
    }

    private PhaseBuildPlan BuildPhaseBuildPlan(PhaseSummary phase, Dictionary<int, double> buildingDemand,
        HashSet<int> unlockedItems,
        Dictionary<int, int> activeBuildCounts, Dictionary<int, double> currentInventory) {
        var plan = new PhaseBuildPlan();
        foreach (var pair in activeBuildCounts) {
            if (pair.Value > 0) {
                plan.TargetActiveCounts[pair.Key] = pair.Value;
            }
        }

        var targetMachineCounts = buildingDemand
            .Where(pair => pair.Value > 0)
            .ToDictionary(pair => pair.Key, pair => RoundUpCount(pair.Value));

        foreach (var pair in targetMachineCounts.OrderBy(pair => pair.Key)) {
            int itemId = pair.Key;
            if (IsLogisticsItem(itemId)) {
                continue;
            }

            _ = DismantleLowerTierBuildFamily(activeBuildCounts, currentInventory, itemId);
            int currentActiveCount = activeBuildCounts.TryGetValue(itemId, out int activeCount) ? activeCount : 0;
            int deltaBuilt = Math.Max(0, pair.Value - currentActiveCount);
            if (deltaBuilt > 0) {
                AddBuildDemand(plan.BuildItemDemand, plan.BuildReasons, itemId, deltaBuilt, "阶段扩建主体产线");
            }
            plan.TargetActiveCounts[itemId] = Math.Max(currentActiveCount, pair.Value);
        }

        double addedBeltCount = 0;
        double addedInserterCount = 0;
        foreach (var pair in targetMachineCounts.OrderBy(pair => pair.Key)) {
            int itemId = pair.Key;
            if (IsLogisticsItem(itemId)) {
                continue;
            }

            int currentActiveCount = activeBuildCounts.TryGetValue(itemId, out int activeCount) ? activeCount : 0;
            int targetCount = plan.TargetActiveCounts.TryGetValue(itemId, out int plannedCount)
                ? plannedCount
                : currentActiveCount;
            int deltaBuilt = Math.Max(0, targetCount - currentActiveCount);
            if (deltaBuilt <= 0) {
                continue;
            }

            int laneCount = GetAverageConnectionCount(itemId);
            addedBeltCount += laneCount * deltaBuilt * 3.0;
            addedInserterCount += laneCount * deltaBuilt;
        }

        int preferredBeltItemId = GetCurrentBestBeltItemId(unlockedItems);
        int rebuiltBelts = DismantleLowerTierBuildFamily(activeBuildCounts, currentInventory, preferredBeltItemId);
        if (rebuiltBelts > 0) {
            AddBuildDemand(plan.BuildItemDemand, plan.BuildReasons, preferredBeltItemId, rebuiltBelts, "传送带升级重建整片产线");
        }

        if (addedBeltCount > 0) {
            AddBuildDemand(plan.BuildItemDemand, plan.BuildReasons, preferredBeltItemId, addedBeltCount, "配套铺设产线");
        }

        int preferredInserterItemId = GetCurrentBestInserterItemId(unlockedItems);
        int rebuiltInserters =
            DismantleLowerTierBuildFamily(activeBuildCounts, currentInventory, preferredInserterItemId);
        if (rebuiltInserters > 0) {
            AddBuildDemand(plan.BuildItemDemand, plan.BuildReasons, preferredInserterItemId, rebuiltInserters,
                "分拣器升级重建整片产线");
        }

        if (addedInserterCount > 0) {
            AddBuildDemand(plan.BuildItemDemand, plan.BuildReasons, preferredInserterItemId, addedInserterCount,
                "配套铺设产线");
        }

        if (plan.BuildItemDemand.TryGetValue(preferredBeltItemId, out double beltDemand)) {
            int currentBeltCount = activeBuildCounts.TryGetValue(preferredBeltItemId, out int activeCount)
                ? activeCount
                : 0;
            plan.TargetActiveCounts[preferredBeltItemId] = currentBeltCount + (int)Math.Ceiling(beltDemand);
        }

        if (plan.BuildItemDemand.TryGetValue(preferredInserterItemId, out double inserterDemand)) {
            int currentInserterCount = activeBuildCounts.TryGetValue(preferredInserterItemId, out int activeCount)
                ? activeCount
                : 0;
            plan.TargetActiveCounts[preferredInserterItemId] = currentInserterCount + (int)Math.Ceiling(inserterDemand);
        }

        foreach (var pair in plan.TargetActiveCounts) {
            activeBuildCounts[pair.Key] = pair.Value;
        }

        return plan;
    }

    private List<BlockingRecord> ApplyBackgroundBuildModel(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedItems,
        HashSet<int> unlockedTechs, Dictionary<int, double> currentInventory, PhaseBuildPlan buildPlan) {
        var blockingRecords = new List<BlockingRecord>();
        double maxBlockingSeconds = 0;
        int parallelFactor = GetBuildParallelFactor(strategyKind, unlockedTechs, unlockedItems);
        double buildDemandScale = GetBuildDemandScale(strategyKind, phase.Phase);

        HashSet<int> trackedItemIds =
            [.. currentInventory.Keys, .. buildPlan.BuildItemDemand.Keys, .. buildPlan.TargetActiveCounts.Keys];
        foreach (int itemId in trackedItemIds.OrderBy(id => id)) {
            if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item) || !item.CanBuild) {
                continue;
            }

            double buildNeed = (buildPlan.BuildItemDemand.TryGetValue(itemId, out double mappedNeed) ? mappedNeed : 0)
                               * buildDemandScale;
            double currentStored = currentInventory.TryGetValue(itemId, out double stored) ? stored : 0;
            double machineRate = GetBackgroundBuildRatePerSecond(itemId, unlockedItems) * parallelFactor;
            double availableBeforeBlocking = currentStored + machineRate * phase.EstimatedResearchSeconds;
            double shortage = Math.Max(0, buildNeed - availableBeforeBlocking);
            double blockingSeconds = shortage > 0 && machineRate > 0 ? shortage / machineRate : 0;
            if (blockingSeconds > maxBlockingSeconds) {
                maxBlockingSeconds = blockingSeconds;
            }

            if (shortage > 0) {
                blockingRecords.Add(new BlockingRecord {
                    ItemId = itemId,
                    ItemName = item.Name,
                    MissingCount = shortage,
                    BlockingSeconds = blockingSeconds,
                    Reason = buildPlan.BuildReasons.TryGetValue(itemId, out string reason) ? reason : "阶段扩建",
                });
            }
        }

        phase.SupermarketFillSeconds = maxBlockingSeconds;
        phase.TotalBlockingSeconds = maxBlockingSeconds;

        BlockingRecord primaryBlocking = blockingRecords
            .OrderByDescending(record => record.BlockingSeconds)
            .ThenByDescending(record => record.MissingCount)
            .FirstOrDefault();
        phase.PrimaryBlockingItemName = primaryBlocking?.ItemName ?? string.Empty;
        phase.PrimaryBlockingReason = primaryBlocking?.Reason ?? string.Empty;
        phase.PrimaryBlockingSeconds = primaryBlocking?.BlockingSeconds ?? 0;

        double totalBackgroundSeconds = phase.EstimatedResearchSeconds + maxBlockingSeconds;
        foreach (int itemId in trackedItemIds.OrderBy(id => id)) {
            if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item) || !item.CanBuild) {
                continue;
            }

            double currentStored = currentInventory.TryGetValue(itemId, out double stored) ? stored : 0;
            double buildNeed = (buildPlan.BuildItemDemand.TryGetValue(itemId, out double mappedNeed) ? mappedNeed : 0)
                               * buildDemandScale;
            double machineRate = GetBackgroundBuildRatePerSecond(itemId, unlockedItems) * parallelFactor;
            int targetInventory = GetBuildingInventoryTargetCount(itemId);
            double endInventory = currentStored + machineRate * totalBackgroundSeconds - buildNeed;
            endInventory = Math.Max(0, Math.Min(targetInventory, endInventory));
            currentInventory[itemId] = endInventory;
        }

        return blockingRecords;
    }

    private static double GetBuildDemandScale(PlayerStrategyKind strategyKind, ProgressPhase phase) {
        return strategyKind switch {
            PlayerStrategyKind.Conventional => phase switch {
                ProgressPhase.Information => 0.8,
                ProgressPhase.Gravity => 0.45,
                ProgressPhase.Universe => 0.35,
                _ => 1.0,
            },
            PlayerStrategyKind.Speedrun => phase switch {
                ProgressPhase.Electromagnetic => 1.0,
                ProgressPhase.Energy => 0.95,
                ProgressPhase.Structure => 0.9,
                ProgressPhase.Information => 0.7,
                ProgressPhase.Gravity => 0.1,
                ProgressPhase.Universe => 0.05,
                _ => 1.0,
            },
            _ => 1.0,
        };
    }

    private void AddTimelineEvent(PhaseSummary phase, ref int sequence, double seconds, string action, string notes) {
        if (seconds < phase.StartSeconds || seconds > phase.PhaseEndSeconds) {
            return;
        }

        phase.TimelineEvents.Add(new TimelineEvent {
            Phase = phase.Phase,
            Seconds = seconds,
            Sequence = sequence++,
            Action = action,
            Notes = notes,
        });
    }

    private static string BuildMatrixLabNotes(PhaseSummary phase, string matrixName) {
        if (!phase.MatrixLabCounts.TryGetValue(matrixName, out int labCount) || labCount <= 0) {
            return string.Empty;
        }

        int baseCount = phase.MatrixLabBaseCounts.TryGetValue(matrixName, out int mappedBaseCount)
            ? mappedBaseCount
            : (int)Math.Ceiling(labCount / (double)Math.Max(1, phase.LabStackLevel));
        return $"{matrixName}研究站 {labCount} 台（底座 {baseCount} 座，当前堆叠 {phase.LabStackLevel} 层）";
    }

    private static double? FindPhaseTechEndSeconds(PhaseSummary phase, int techId) =>
        phase.TechTimings.FirstOrDefault(timing => timing.TechId == techId)?.EndSeconds;

    private void PopulateDysonTimelineEvents(PhaseSummary phase, IReadOnlyDictionary<int, double> techUnlockSeconds,
        ref int sequence) {
        double? lensStartSeconds = null;
        if (techUnlockSeconds.TryGetValue(GravitonRefractionTechId, out double lensUnlockSeconds)
            && techUnlockSeconds.TryGetValue(IonosphereUtilizationTechId, out double ionosphereUnlockSeconds)) {
            lensStartSeconds = Math.Max(lensUnlockSeconds, ionosphereUnlockSeconds);
        }

        double? sailUnlockSeconds = FindPhaseTechEndSeconds(phase, SolarSailOrbitTechId);
        if (sailUnlockSeconds.HasValue) {
            AddTimelineEvent(phase, ref sequence, sailUnlockSeconds.Value,
                "开始打太阳帆",
                phase.SolarSailLaunchPerMinute > 0
                    ? $"太阳帆 {phase.SolarSailLaunchPerMinute:0.##}/min，模式 {FormatStringOrEmpty(phase.DysonModeName)}"
                    : "解锁太阳帆与电磁轨道弹射器");
        }

        double? receiverUnlockSeconds = FindPhaseTechEndSeconds(phase, RayReceiverTechId);
        if (receiverUnlockSeconds.HasValue) {
            bool lensReadyAtReceiverStart = phase.UseGravitonLens
                                            && lensStartSeconds.HasValue
                                            && lensStartSeconds.Value <= receiverUnlockSeconds.Value;
            AddTimelineEvent(phase, ref sequence, receiverUnlockSeconds.Value,
                "开始临界光子产线",
                phase.RayReceiverCount > 0
                    ? (lensReadyAtReceiverStart
                        ? $"持续照射假设；射线接收站 x{phase.RayReceiverCount}，初始即带透镜，速率会在约 8 分钟内逐步爬升"
                        : $"持续照射假设；射线接收站 x{phase.RayReceiverCount}，速率会在约 20 分钟内逐步爬升")
                    : "解锁射线接收站");
            AddTimelineEvent(
                phase,
                ref sequence,
                receiverUnlockSeconds.Value
                + (lensReadyAtReceiverStart ? RayReceiverWarmupWithLensSeconds : RayReceiverWarmupSeconds),
                lensReadyAtReceiverStart ? "临界光子产线透镜热机完成" : "临界光子产线热机完成",
                lensReadyAtReceiverStart ? "持续照射假设；达到带透镜稳定态" : "持续照射假设；达到无透镜稳定态");
        }

        double? rocketUnlockSeconds = FindPhaseTechEndSeconds(phase, VerticalLaunchTechId);
        if (rocketUnlockSeconds.HasValue) {
            AddTimelineEvent(phase, ref sequence, rocketUnlockSeconds.Value,
                "开始打火箭",
                phase.RocketLaunchPerMinute > 0
                    ? $"小型运载火箭 {phase.RocketLaunchPerMinute:0.##}/min"
                    : "解锁垂直发射井与小型运载火箭");
        }

        if (lensStartSeconds.HasValue
            && (!receiverUnlockSeconds.HasValue || lensStartSeconds.Value > receiverUnlockSeconds.Value)) {
            AddTimelineEvent(phase, ref sequence, lensStartSeconds.Value,
                "开始启用引力透镜",
                phase.UseGravitonLens
                    ? $"透镜 {phase.GravitonLensConsumptionPerMinute:0.##}/min，速率会在约 8 分钟内继续爬升"
                    : "解锁引力透镜并可用于射线接收站");
            AddTimelineEvent(phase, ref sequence, lensStartSeconds.Value + RayReceiverWarmupWithLensSeconds,
                "引力透镜热机完成",
                "持续照射假设；达到透镜稳定态");
        }
    }

    private void PopulatePhaseTimeline(PhaseSummary phase, IReadOnlyList<BlockingRecord> blockingRecords,
        IReadOnlyDictionary<int, double> techUnlockSeconds) {
        phase.TimelineEvents.Clear();
        int sequence = 0;
        if (!string.IsNullOrEmpty(phase.PrimaryPowerSourceName) && phase.PrimaryPowerBuildingCount > 0) {
            string powerAction = phase.PrimaryPowerBuildingCount > 0
                ? $"开始部署 {phase.PrimaryPowerSourceName} x{phase.PrimaryPowerBuildingCount} 供电"
                : $"开始接入 {phase.PrimaryPowerSourceName} 供电";
            string powerNotes = phase.TotalPowerDemandWatts > 0
                ? $"总功率 {phase.TotalPowerDemandWatts / 1_000_000d:0.##} MW"
                : string.Empty;
            AddTimelineEvent(phase, ref sequence, phase.StartSeconds, powerAction, powerNotes);
        }

        foreach (var pair in phase.MatrixRatesPerSecond.OrderByDescending(pair => pair.Value)
                     .ThenBy(pair => pair.Key)) {
            double buildStartSeconds =
                phase.MatrixBuildStartSeconds.TryGetValue(pair.Key, out double mappedBuildStartSeconds)
                    ? mappedBuildStartSeconds
                    : phase.StartSeconds;
            double productionStartSeconds =
                phase.MatrixProductionStartSeconds.TryGetValue(pair.Key, out double mappedProductionStartSeconds)
                    ? mappedProductionStartSeconds
                    : phase.StartSeconds;
            string buildNotes = phase.MatrixBuildNotes.TryGetValue(pair.Key, out string mappedBuildNotes)
                ? mappedBuildNotes
                : BuildMatrixLabNotes(phase, pair.Key);
            string productionNotes = phase.MatrixProductionNotes.TryGetValue(pair.Key, out string mappedProductionNotes)
                ? mappedProductionNotes
                : BuildMatrixLabNotes(phase, pair.Key);

            if (buildStartSeconds > phase.StartSeconds || !string.IsNullOrEmpty(buildNotes)) {
                AddTimelineEvent(phase, ref sequence, buildStartSeconds,
                    $"开始建设 {pair.Value * 60:0.##}个/min 的{pair.Key}产线",
                    buildNotes);
            }
            AddTimelineEvent(phase, ref sequence, productionStartSeconds,
                $"开始制作 {pair.Value * 60:0.##}个/min 的{pair.Key}产线",
                productionNotes);
        }

        PopulateDysonTimelineEvents(phase, techUnlockSeconds, ref sequence);

        if (phase.TechTimings.Count > 0) {
            TechTiming firstTiming = phase.TechTimings[0];
            AddTimelineEvent(phase, ref sequence, phase.StartSeconds,
                $"开始 {firstTiming.TechCode} {firstTiming.TechName}",
                BuildTechEventNotes(firstTiming.TechId, firstTiming, phase));
            AddTimelineEvent(phase, ref sequence, firstTiming.EndSeconds,
                $"完成 {firstTiming.TechCode} {firstTiming.TechName}",
                BuildTechUnlockNotes(firstTiming.TechId));

            for (int index = 1; index < phase.TechTimings.Count; index++) {
                TechTiming currentTiming = phase.TechTimings[index];
                AddTimelineEvent(phase, ref sequence, currentTiming.StartSeconds,
                    $"开始 {currentTiming.TechCode} {currentTiming.TechName}",
                    BuildTechEventNotes(currentTiming.TechId, currentTiming, phase));
                AddTimelineEvent(phase, ref sequence, currentTiming.EndSeconds,
                    $"完成 {currentTiming.TechCode} {currentTiming.TechName}",
                    BuildTechUnlockNotes(currentTiming.TechId));
            }
        }

        foreach (BlockingRecord record in blockingRecords.OrderByDescending(record => record.BlockingSeconds)
                     .ThenBy(record => record.ItemId)) {
            AddTimelineEvent(phase, ref sequence, phase.ResearchEndSeconds, $"开始等待{record.ItemName}补足",
                $"{record.ItemName} x{Math.Ceiling(record.MissingCount)}，{record.Reason}");
        }

        if (phase.EventDelaySeconds > 0) {
            AddTimelineEvent(phase, ref sequence, phase.ResearchEndSeconds + phase.TotalBlockingSeconds,
                $"开始 {phase.EventDelayReason}", phase.EventDelayReason);
        }
    }

    private void ApplySupermarketModel(PhaseSummary phase, HashSet<int> unlockedItems,
        Dictionary<int, double> currentInventory) {
        var supermarketGroups = new Dictionary<int, int>();
        foreach (int itemId in unlockedItems.OrderBy(id => id)) {
            if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item) || !item.CanBuild) {
                continue;
            }

            VanillaRecipe recipe = ChoosePrimaryRecipe(itemId);
            if (recipe == null) {
                continue;
            }

            CalcRecipe calcRecipe = GetCalcRecipe(recipe) ?? ChoosePrimaryCalcRecipe(itemId);
            int factoryId = ChooseFactory(calcRecipe, unlockedItems, recipe);
            int targetCount = GetBuildingInventoryTargetCount(itemId);

            supermarketGroups[factoryId] = supermarketGroups.TryGetValue(factoryId, out int slotCount)
                ? slotCount + 1
                : 1;
            phase.SupermarketTargets[item.Name] = targetCount;
            phase.InventorySnapshot[item.Name] = currentInventory.TryGetValue(itemId, out double stored) ? stored : 0;
        }

        foreach (var pair in supermarketGroups.OrderBy(p => p.Key)) {
            string name = dataSet.ItemsById.TryGetValue(pair.Key, out VanillaItem factory)
                ? factory.Name
                : pair.Key.ToString();
            phase.SupermarketSlots[name] = pair.Value;
        }
    }

    // 速通校准第一轮：把“跨星拿钛/硅”“中后期二次远征”显式记成等待窗口，
    // 避免全程都被当成连续科研或连续生产。
    private static double EstimatePhaseEventDelaySeconds(PhaseSummary phase) {
        return phase.Phase switch {
            ProgressPhase.Energy => 8.5 * 60.0,
            ProgressPhase.Structure => 4.0 * 60.0,
            _ => 0,
        };
    }

    private static string DescribePhaseEventDelay(PhaseSummary phase) {
        return phase.Phase switch {
            ProgressPhase.Energy => "本星系异星远征与首轮钛/硅补给",
            ProgressPhase.Structure => "中后期二次远征与关键材料回运",
            _ => string.Empty,
        };
    }

    private void AccumulateDemand(int itemId, double requiredRate, HashSet<int> unlockedItems,
        Dictionary<int, double> buildingDemand, Dictionary<int, double> resourceDemand, HashSet<int> stack) {
        if (requiredRate <= 0 || stack.Contains(itemId)) {
            return;
        }

        if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item)) {
            return;
        }

        if (IsGatheredResource(itemId, item)) {
            int gatherBuildingId = GetGatherBuildingId(itemId, unlockedItems);
            double gatherRate = GetGatherRatePerBuilding(itemId, gatherBuildingId);
            if (gatherBuildingId > 0 && gatherRate > 0) {
                buildingDemand[gatherBuildingId] =
                    buildingDemand.TryGetValue(gatherBuildingId, out double existingBuildings)
                        ? existingBuildings + requiredRate / gatherRate
                        : requiredRate / gatherRate;
            }
            resourceDemand[itemId] = resourceDemand.TryGetValue(itemId, out double existingRate)
                ? existingRate + requiredRate
                : requiredRate;
            return;
        }

        VanillaRecipe recipe = ChoosePrimaryRecipe(itemId);
        if (recipe == null || recipe.Outputs.Count == 0) {
            resourceDemand[itemId] = resourceDemand.TryGetValue(itemId, out double existingRate)
                ? existingRate + requiredRate
                : requiredRate;
            return;
        }

        CalcRecipe calcRecipe = GetCalcRecipe(recipe) ?? ChoosePrimaryCalcRecipe(itemId);
        int factoryId = ChooseFactory(calcRecipe, unlockedItems, recipe);
        RecipeSprayMode sprayMode = GetSprayMode(recipe);
        double speed = GetFactorySpeed(factoryId, recipe) * GetSpeedMultiplier(recipe, sprayMode);
        double outputCount = recipe.Outputs.Where(o => o.Id == itemId).Sum(o => o.Count);
        if (outputCount <= 0) {
            outputCount = recipe.Outputs[0].Count;
        }
        outputCount *= GetOutputMultiplier(recipe, sprayMode);

        double perBuildingRate = outputCount * speed * 60.0 / Math.Max(1, recipe.TimeSpend);
        if (perBuildingRate <= 0) {
            return;
        }

        buildingDemand[factoryId] = buildingDemand.TryGetValue(factoryId, out double existingDemand)
            ? existingDemand + requiredRate / perBuildingRate
            : requiredRate / perBuildingRate;

        stack.Add(itemId);
        foreach (RecipeAmount input in recipe.Inputs) {
            double inputRate = requiredRate * input.Count / outputCount;
            AccumulateDemand(input.Id, inputRate, unlockedItems, buildingDemand, resourceDemand, stack);
        }
        stack.Remove(itemId);
    }

    private VanillaRecipe ChoosePrimaryRecipe(int outputItemId) {
        if (!dataSet.RecipesByOutputId.TryGetValue(outputItemId, out List<VanillaRecipe> recipes)
            || recipes.Count == 0) {
            return null;
        }

        return recipes
            .OrderBy(r => r.PreTechCode.Length > 0 ? 1 : 0)
            .ThenBy(r => r.Inputs.Sum(i => GetItemDepth(i.Id)))
            .ThenBy(r => r.TimeSpend)
            .First();
    }

    private CalcRecipe ChoosePrimaryCalcRecipe(int outputItemId) {
        if (!dataSet.CalcRecipesByOutputId.TryGetValue(outputItemId, out List<CalcRecipe> recipes)
            || recipes.Count == 0) {
            return null;
        }

        return recipes
            .Where(r => r.Factories.Any(id =>
                dataSet.ItemsById.TryGetValue(id, out VanillaItem item) && item.Space > 0 && item.Speed > 0))
            .OrderBy(r => r.Items.Sum(GetItemDepth))
            .ThenBy(r => r.TimeSpend)
            .FirstOrDefault();
    }

    private static bool IsGatheredResource(int itemId, VanillaItem item) {
        if (item.ItemType != "Resource") {
            return itemId is 1000 or 1007;
        }

        return true;
    }

    private static int GetGatherBuildingId(int itemId, HashSet<int> unlockedItems) {
        if (itemId == 1000) {
            return 2306;// 抽水站
        }

        if (itemId == 1007) {
            return 2307;// 原油萃取站
        }

        return unlockedItems.Contains(2316) ? 2316 : 2301;
    }

    private static double GetGatherRatePerBuilding(int itemId, int buildingId) {
        if (itemId == 1000) {
            return WaterPumpRatePerSecond;
        }

        if (itemId == 1007) {
            return OilExtractorRatePerSecond;
        }

        return buildingId == 2316
            ? LargeMinerVeinCount * MiningRatePerVeinPerSecond
            : SmallMinerVeinCount * MiningRatePerVeinPerSecond;
    }

    private int GetItemDepth(int itemId) {
        return GetItemDepth(itemId, new HashSet<int>());
    }

    private int GetItemDepth(int itemId, HashSet<int> visiting) {
        if (itemDepthCache.TryGetValue(itemId, out int cached)) {
            return cached;
        }

        if (visiting.Contains(itemId)) {
            return 0;
        }

        if (!dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item)) {
            return 0;
        }

        if (item.ItemType is "Resource" or "DarkFog") {
            itemDepthCache[itemId] = 0;
            return 0;
        }

        if (!dataSet.CalcRecipesByOutputId.TryGetValue(itemId, out List<CalcRecipe> recipes) || recipes.Count == 0) {
            itemDepthCache[itemId] = 0;
            return 0;
        }

        visiting.Add(itemId);
        int depth = recipes
            .Select(recipe =>
                1 + recipe.Items.Select(inputId => GetItemDepth(inputId, visiting)).DefaultIfEmpty(0).Max())
            .Min();
        visiting.Remove(itemId);
        itemDepthCache[itemId] = depth;
        return depth;
    }

    private bool IsUnlocked(VanillaTech tech, HashSet<int> unlockedTechs) {
        foreach (string code in tech.PreTechCodes.Concat(tech.ImplicitPreTechCodes)) {
            if (string.IsNullOrEmpty(code) || code[0] != 'T') {
                continue;
            }

            if (!unlockedTechs.Contains(ParseNumericCode(code))) {
                return false;
            }
        }

        return true;
    }

    private double ScoreTech(VanillaTech tech, PlayerStrategyKind strategyKind) {
        int unlockCount = tech.UnlockTargets.Count;
        int infrastructureCount = tech.UnlockTargets.Count(IsInfrastructureUnlock);
        int buildingCount = tech.UnlockTargets.Count(IsBuildingUnlock);
        int powerCount = tech.UnlockTargets.Count(IsPowerUnlock);
        int capacityUpgradeCount = tech.UnlockTargets.Count(IsCapacityUpgradeUnlock);
        int matrixAdvance = milestoneTechIds.Contains(tech.Id) ? 1 : 0;
        int verticalConstructionAdvance = verticalConstructionTechIds.Contains(tech.Id) ? 1 : 0;
        int unlockedDepth = tech.UnlockTargets.Where(t => t.Kind == 'I').Sum(t => GetItemDepth(t.Id));
        int matrixStage = tech.CostItems.Select(a => GetMatrixStage(a.Id)).DefaultIfEmpty(0).Max();
        double hardPriorityScore = powerCount * 100.0 + buildingCount * 30.0 + infrastructureCount * 12.0;
        double criticalPathBonus = GetConventionalCriticalPathBonus(tech.Id);

        return strategyKind switch {
            PlayerStrategyKind.Conventional =>
                hardPriorityScore
                + criticalPathBonus
                + capacityUpgradeCount * 80.0
                + unlockCount * 2.0
                + unlockedDepth * 0.3
                + matrixAdvance * 6.0
                + verticalConstructionAdvance * 4.0
                - matrixStage * 0.4,
            PlayerStrategyKind.Speedrun =>
                hardPriorityScore * 0.7
                + capacityUpgradeCount * 10.0
                + matrixAdvance * 12.0
                + matrixStage * 2.0
                + unlockCount * 1.2
                + verticalConstructionAdvance * 2.0
                + unlockedDepth * 0.15,
            _ => unlockCount,
        };
    }

    private static double GetConventionalCriticalPathBonus(int techId) =>
        conventionalCriticalPathBonusByTechId.TryGetValue(techId, out double bonus) ? bonus : 0;

    private bool IsCapacityUpgradeUnlock(UnlockTarget target) =>
        ResolveUnlockedItemIds(target).Any(itemId => GetUpgradeFamily(itemId).Length > 0 || IsLogisticsItem(itemId));

    private bool IsBuildingUnlock(UnlockTarget target) =>
        ResolveUnlockedItemIds(target).Any(itemId =>
            dataSet.ItemsById.TryGetValue(itemId, out VanillaItem item) && item.CanBuild);

    private static bool IsInfrastructureUnlock(UnlockTarget target) =>
        target.Name.Contains("物流")
        || target.Name.Contains("传送带")
        || target.Name.Contains("分拣器")
        || target.Name.Contains("储物仓")
        || target.Name.Contains("流速监测器")
        || target.Name.Contains("喷涂机")
        || target.Name.Contains("配送")
        || target.Name.Contains("运输站");

    private static bool IsPowerUnlock(UnlockTarget target) =>
        target.Name.Contains("发电")
        || target.Name.Contains("电力")
        || target.Name.Contains("蓄电")
        || target.Name.Contains("太阳能")
        || target.Name.Contains("射线接收站")
        || target.Name.Contains("能量枢纽");

    private static ProgressPhase GetPhase(VanillaTech tech) {
        int stage = tech.CostItems.Select(a => GetMatrixStage(a.Id)).DefaultIfEmpty(0).Max();
        return stage switch {
            <= 1 => ProgressPhase.Electromagnetic,
            2 => ProgressPhase.Energy,
            3 => ProgressPhase.Structure,
            4 => ProgressPhase.Information,
            5 => ProgressPhase.Gravity,
            _ => ProgressPhase.Universe,
        };
    }

    private static int GetMatrixStage(int itemId) {
        int index = Array.IndexOf(matrixIds, itemId);
        return index >= 0 ? index + 1 : 0;
    }

    private double EstimateMatrixLimitedSeconds(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedItems,
        HashSet<int> unlockedTechs, Dictionary<int, int> currentMatrixLabCounts, int phaseIndex, double targetSeconds) {
        phase.MatrixTargetRatePerSecond = 0;
        phase.MatrixLabCount = 0;
        phase.MatrixLabBaseCount = 0;
        phase.MatrixLabCounts.Clear();
        phase.MatrixLabBaseCounts.Clear();
        phase.MatrixRatesPerSecond.Clear();
        Dictionary<int, double> matrixCosts = BuildPhaseMatrixCosts(phase);

        if (matrixCosts.Count == 0) {
            return 0;
        }

        double matrixTargetRatePerSecond = GetPhaseMatrixTargetRatePerSecond(strategyKind, matrixCosts, phaseIndex,
            targetSeconds, unlockedTechs, unlockedItems);
        phase.MatrixTargetRatePerSecond = Math.Round(matrixTargetRatePerSecond, 3);
        Dictionary<int, double> matrixRatesByItemId =
            BuildPhaseMatrixRates(strategyKind, phase, unlockedItems, unlockedTechs, currentMatrixLabCounts, phaseIndex,
                targetSeconds);
        double bottleneckSeconds = 0;
        foreach (var pair in matrixCosts.OrderBy(p => p.Key)) {
            if (!dataSet.ItemsById.TryGetValue(pair.Key, out VanillaItem matrixItem)) {
                continue;
            }

            double rate = matrixRatesByItemId.TryGetValue(pair.Key, out double mappedRate) ? mappedRate : 0;
            int labCount = GetAdaptiveMatrixLabCount(unlockedItems, currentMatrixLabCounts, pair.Key,
                matrixTargetRatePerSecond);
            int baseCount = GetMatrixLabBaseCount(labCount, phase.LabStackLevel);
            phase.MatrixLabCounts[matrixItem.Name] = labCount;
            phase.MatrixLabBaseCounts[matrixItem.Name] = baseCount;
            phase.MatrixRatesPerSecond[matrixItem.Name] = Math.Round(rate, 3);
            phase.MatrixLabCount += labCount;
            phase.MatrixLabBaseCount += baseCount;
            if (rate > 0) {
                bottleneckSeconds = Math.Max(bottleneckSeconds, pair.Value / rate);
            }
        }

        return bottleneckSeconds;
    }

    private double EstimateTechMatrixLimitedSeconds(VanillaTech tech, Dictionary<int, double> matrixRatesByItemId) {
        double bottleneckSeconds = 0;
        foreach (RecipeAmount cost in tech.CostItems.Where(cost => GetMatrixStage(cost.Id) > 0)) {
            double rate = matrixRatesByItemId.TryGetValue(cost.Id, out double mappedRate) ? mappedRate : 0;
            if (rate > 0) {
                bottleneckSeconds = Math.Max(bottleneckSeconds, cost.Count / rate);
            }
        }
        return bottleneckSeconds;
    }

    private Dictionary<int, double> BuildPhaseMatrixCosts(PhaseSummary phase) {
        var matrixCosts = new Dictionary<int, double>();
        foreach (int techId in phase.TechIds) {
            VanillaTech tech = dataSet.TechsById[techId];
            foreach (RecipeAmount cost in tech.CostItems) {
                if (GetMatrixStage(cost.Id) <= 0) {
                    continue;
                }

                matrixCosts[cost.Id] = matrixCosts.TryGetValue(cost.Id, out double existing)
                    ? existing + cost.Count
                    : cost.Count;
            }
        }

        return matrixCosts;
    }

    // 每个阶段先确定一个统一矩阵目标产率，再按各色矩阵单站产能反推所需研究站台数。
    private Dictionary<int, double> BuildPhaseMatrixRates(PlayerStrategyKind strategyKind, PhaseSummary phase,
        HashSet<int> unlockedItems,
        HashSet<int> unlockedTechs, Dictionary<int, int> currentMatrixLabCounts, int phaseIndex, double targetSeconds) {
        var matrixRatesByItemId = new Dictionary<int, double>();
        Dictionary<int, double> matrixCosts = BuildPhaseMatrixCosts(phase);
        double matrixTargetRatePerSecond = GetPhaseMatrixTargetRatePerSecond(strategyKind, matrixCosts, phaseIndex,
            targetSeconds, unlockedTechs, unlockedItems);
        foreach (var pair in matrixCosts) {
            int labCount = GetAdaptiveMatrixLabCount(unlockedItems, currentMatrixLabCounts, pair.Key,
                matrixTargetRatePerSecond);
            matrixRatesByItemId[pair.Key] = GetMatrixRatePerSecond(pair.Key, labCount, unlockedItems);
        }

        return matrixRatesByItemId;
    }

    private double GetPhaseMatrixTargetRatePerSecond(PlayerStrategyKind strategyKind,
        Dictionary<int, double> matrixCosts, int phaseIndex,
        double targetSeconds, HashSet<int> unlockedTechs, HashSet<int> unlockedItems) {
        if (matrixCosts.Count == 0 || targetSeconds <= 0) {
            return 0;
        }

        double requiredRate = matrixCosts.Values.Max() / targetSeconds;
        double targetRate = requiredRate * GetMatrixRateHeadroom(strategyKind, phaseIndex);
        if (strategyKind == PlayerStrategyKind.Conventional) {
            double fullBeltRate = GetCurrentDynamicFullBeltRatePerSecond(unlockedTechs, unlockedItems);
            targetRate = Math.Max(targetRate, fullBeltRate);
            targetRate = Math.Min(targetRate,
                GetConventionalMatrixRateCapPerSecond((ProgressPhase)phaseIndex, fullBeltRate));
        } else if (strategyKind == PlayerStrategyKind.Speedrun) {
            double fullBeltRate = GetCurrentDynamicFullBeltRatePerSecond(unlockedTechs, unlockedItems);
            targetRate = Math.Max(targetRate, fullBeltRate * 0.25);
            targetRate = Math.Min(targetRate,
                GetSpeedrunMatrixRateCapPerSecond((ProgressPhase)phaseIndex, fullBeltRate));
        }
        return targetRate;
    }

    private static double GetConventionalMatrixRateCapPerSecond(ProgressPhase phase, double fullBeltRatePerSecond) {
        if (fullBeltRatePerSecond <= 0) {
            return 0;
        }

        return phase switch {
            ProgressPhase.Information or ProgressPhase.Gravity or ProgressPhase.Universe => fullBeltRatePerSecond,
            _ => double.PositiveInfinity,
        };
    }

    private static double GetSpeedrunMatrixRateCapPerSecond(ProgressPhase phase, double fullBeltRatePerSecond) {
        if (fullBeltRatePerSecond <= 0) {
            return 0;
        }

        // 速通可以秒铺蓝图，但后段矩阵目标产率仍不应无限膨胀到远超物流主干的规模。
        double multiplier = phase switch {
            ProgressPhase.Gravity or ProgressPhase.Universe => 0.6,
            ProgressPhase.Information => 4.0,
            ProgressPhase.Structure => 3.0,
            ProgressPhase.Energy => 2.0,
            _ => 1.5,
        };
        return fullBeltRatePerSecond * multiplier;
    }

    private double GetMatrixRatePerSecond(int matrixItemId, int labCount, HashSet<int> unlockedItems) {
        return GetSingleMatrixLabRatePerSecond(matrixItemId, unlockedItems) * Math.Max(1, labCount);
    }

    private double GetSingleMatrixLabRatePerSecond(int matrixItemId, HashSet<int> unlockedItems) {
        VanillaRecipe recipe = ChoosePrimaryRecipe(matrixItemId);
        if (recipe == null) {
            return 0;
        }

        CalcRecipe calcRecipe = GetCalcRecipe(recipe) ?? ChoosePrimaryCalcRecipe(matrixItemId);
        int factoryId = ChooseFactory(calcRecipe, unlockedItems, recipe);
        RecipeSprayMode sprayMode = GetSprayMode(recipe);
        double speed = GetFactorySpeed(factoryId, recipe) * GetSpeedMultiplier(recipe, sprayMode);
        double outputCount = recipe.Outputs.Where(o => o.Id == matrixItemId).Sum(o => o.Count);
        if (outputCount <= 0) {
            return 0;
        }
        outputCount *= GetOutputMultiplier(recipe, sprayMode);

        return outputCount * speed * 60.0 / Math.Max(1, recipe.TimeSpend);
    }

    private static int GetResearchLabCount(PlayerStrategyKind strategyKind, int phaseIndex) {
        int[] table = strategyKind switch {
            PlayerStrategyKind.Conventional => phaseResearchLabCountsConventional,
            PlayerStrategyKind.Speedrun => phaseResearchLabCountsSpeedrun,
            _ => phaseResearchLabCountsConventional,
        };
        return table[Math.Min(phaseIndex, table.Length - 1)];
    }

    private int GetAdaptiveMatrixLabCount(HashSet<int> unlockedItems, Dictionary<int, int> currentMatrixLabCounts,
        int matrixItemId,
        double targetRatePerSecond) {
        double singleLabRate = GetSingleMatrixLabRatePerSecond(matrixItemId, unlockedItems);
        if (singleLabRate <= 0) {
            return 1;
        }

        int requiredLabCount = targetRatePerSecond <= 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(targetRatePerSecond / singleLabRate));
        int existingLabCount = currentMatrixLabCounts.TryGetValue(matrixItemId, out int mappedLabCount)
            ? mappedLabCount
            : 0;
        return Math.Max(requiredLabCount, Math.Max(1, existingLabCount));
    }

    private static int GetMatrixLabBaseCount(int labCount, int labStackLevel) {
        return (int)Math.Ceiling(Math.Max(1, labCount) / (double)Math.Max(1, labStackLevel));
    }

    private static double GetMatrixRateHeadroom(PlayerStrategyKind strategyKind, int phaseIndex) {
        double phaseBonus = phaseIndex switch {
            >= (int)ProgressPhase.Gravity => 0.08,
            (int)ProgressPhase.Information => 0.05,
            (int)ProgressPhase.Structure => 0.03,
            _ => 0,
        };
        double baseHeadroom = strategyKind switch {
            PlayerStrategyKind.Conventional => MatrixRateHeadroomConventional,
            PlayerStrategyKind.Speedrun => MatrixRateHeadroomSpeedrun,
            _ => MatrixRateHeadroomDefault,
        };

        return baseHeadroom + phaseBonus;
    }

    private void ApplyDysonPowerModel(StrategySimulationResult result, PlayerStrategyKind strategyKind,
        PhaseSummary phase,
        Dictionary<int, double> techUnlockSeconds) {
        DysonPowerEstimate estimate = EstimateDysonPower(result, strategyKind, phase, techUnlockSeconds);
        phase.DysonModeName = estimate.Mode switch {
            DysonBuildMode.SailOnly => "只打帆",
            DysonBuildMode.SailAndRocket => "帆 + 火箭",
            _ => string.Empty,
        };
        phase.DysonAvailablePowerWatts = estimate.AvailablePowerWatts;
        phase.DysonCapturedPowerWatts = estimate.CapturedPowerWatts;
        phase.RayReceiverCount = estimate.ReceiverCount;
        phase.RayReceiverPowerPerBuildingWatts = estimate.ReceiverPowerPerBuildingWatts;
        phase.UseGravitonLens = estimate.UseGravitonLens;
        phase.GravitonLensConsumptionPerMinute = estimate.GravitonLensConsumptionPerMinute;
        phase.SolarSailLaunchPerMinute = estimate.SolarSailLaunchPerMinute;
        phase.RocketLaunchPerMinute = estimate.RocketLaunchPerMinute;
        phase.SwarmSailCountEstimate = estimate.SwarmSailCountEstimate;
        phase.ConstructedSpEstimate = estimate.ConstructedSpEstimate;
        phase.ConstructedCpEstimate = estimate.ConstructedCpEstimate;
    }

    private DysonPowerEstimate EstimateDysonPower(StrategySimulationResult result, PlayerStrategyKind strategyKind,
        PhaseSummary phase,
        Dictionary<int, double> techUnlockSeconds) {
        // 速通校准第一轮：前中期仍以火电/核电为主，不允许过早把射线接收站当主力供电。
        if (phase.Phase < ProgressPhase.Gravity) {
            return new DysonPowerEstimate { Mode = DysonBuildMode.None };
        }

        if (!techUnlockSeconds.TryGetValue(1504, out double receiverUnlockSeconds)) {
            return new DysonPowerEstimate { Mode = DysonBuildMode.None };
        }

        DysonBuildMode mode = strategyKind == PlayerStrategyKind.Speedrun
            ? DysonBuildMode.SailOnly
            : DysonBuildMode.SailAndRocket;
        double phaseEndSeconds = phase.PhaseEndSeconds > 0 ? phase.PhaseEndSeconds : phase.ResearchEndSeconds;
        double receiverRuntimeMinutes = Math.Max(0, phaseEndSeconds - receiverUnlockSeconds) / 60.0;
        if (receiverRuntimeMinutes <= 0) {
            return new DysonPowerEstimate { Mode = DysonBuildMode.None };
        }

        int phaseIndex = (int)phase.Phase;
        double activeEjectorCount = techUnlockSeconds.ContainsKey(1503)
            ? GetActiveDysonBuildingCount(phase.ResearchLabCount, phaseIndex, 0.5, 6)
            : 0;
        double sailLaunchPerMinute = activeEjectorCount * EjectorLaunchesPerMinute;
        double totalLaunchedSails = sailLaunchPerMinute * receiverRuntimeMinutes;

        double rocketRuntimeMinutes = techUnlockSeconds.TryGetValue(1522, out double rocketUnlockSeconds)
            ? Math.Max(0, phaseEndSeconds - rocketUnlockSeconds) / 60.0
            : 0;
        double activeSiloCount = mode == DysonBuildMode.SailAndRocket && rocketRuntimeMinutes > 0
            ? GetActiveDysonBuildingCount(phase.ResearchLabCount, phaseIndex, 0.125, 3)
            : 0;
        double rocketLaunchPerMinute = activeSiloCount * SiloLaunchesPerMinute;
        double constructedSp = rocketLaunchPerMinute * rocketRuntimeMinutes;

        double shellUnlockMinutes = techUnlockSeconds.TryGetValue(1523, out double shellUnlockSeconds)
            ? Math.Max(0, phaseEndSeconds - shellUnlockSeconds) / 60.0
            : 0;
        double maxLivingSwarmSails = sailLaunchPerMinute * (DefaultSolarSailLifeSeconds / 60.0);
        double shellCpCap = constructedSp * ShellCpPerSpCap;
        double shellAbsorbProgress =
            Math.Min(1.0, shellUnlockMinutes * 60.0 / Math.Max(SolarSailAbsorbDelaySeconds, 1.0));
        double absorbedSails = mode == DysonBuildMode.SailAndRocket
            ? Math.Min(totalLaunchedSails * shellAbsorbProgress, shellCpCap)
            : 0;
        double swarmSails = Math.Max(0, Math.Min(totalLaunchedSails, maxLivingSwarmSails) - absorbedSails);
        double constructedCp = absorbedSails;

        long availablePowerWatts = (long)Math.Round(swarmSails * SwarmPowerPerSailWatts
                                                    + constructedSp * FramePowerPerSpWatts
                                                    + constructedCp * ShellPowerPerCpWatts);

        bool useGravitonLens = techUnlockSeconds.ContainsKey(1704) && techUnlockSeconds.ContainsKey(1505);
        double warmupProgress = Math.Min(1.0, receiverRuntimeMinutes / (useGravitonLens ? 8.0 : 20.0));
        long receiverPowerPerBuildingWatts = (long)Math.Round(ReceiverBasePowerWatts
                                                              * (1.0 + 1.5 * warmupProgress)
                                                              * (useGravitonLens ? 2.0 : 1.0));
        long capturedPowerWatts = phase.TotalPowerDemandWatts > 0
            ? Math.Min(availablePowerWatts, phase.TotalPowerDemandWatts)
            : availablePowerWatts;
        int receiverCount = capturedPowerWatts > 0 && receiverPowerPerBuildingWatts > 0
            ? (int)Math.Ceiling(capturedPowerWatts / (double)receiverPowerPerBuildingWatts)
            : 0;

        return new DysonPowerEstimate {
            Mode = mode,
            AvailablePowerWatts = availablePowerWatts,
            CapturedPowerWatts = capturedPowerWatts,
            ReceiverCount = receiverCount,
            ReceiverPowerPerBuildingWatts = receiverPowerPerBuildingWatts,
            UseGravitonLens = useGravitonLens,
            GravitonLensConsumptionPerMinute = useGravitonLens ? receiverCount : 0,
            SolarSailLaunchPerMinute = sailLaunchPerMinute,
            RocketLaunchPerMinute = rocketLaunchPerMinute,
            SwarmSailCountEstimate = swarmSails,
            ConstructedSpEstimate = constructedSp,
            ConstructedCpEstimate = constructedCp,
        };
    }

    private static double GetActiveDysonBuildingCount(int researchLabCount, int phaseIndex, double ratio, int minimum) {
        if (researchLabCount <= 0) {
            return 0;
        }

        return Math.Max(minimum, Math.Ceiling(researchLabCount * ratio * Math.Max(1.0, 1.0 + phaseIndex * 0.15)));
    }

    private void ApplyPhasePowerModel(PhaseSummary phase, Dictionary<int, double> buildingDemand,
        HashSet<int> unlockedItems) {
        PowerSourceProfile profile = GetPowerSourceProfile(phase.Phase);
        long totalPowerDemandWatts = phase.TotalPowerDemandWatts > 0
            ? phase.TotalPowerDemandWatts
            : EstimatePhasePowerDemandWatts(phase, buildingDemand, unlockedItems);
        phase.TotalPowerDemandWatts = totalPowerDemandWatts;
        // 当前模拟固定把射线接收站视为临界光子链路，不把其发电模式并入电网供电。
        // 相关理论计算保留在 DysonCapturedPowerWatts 等字段里，供未来切换模型时复用。
        long residualPowerWatts = totalPowerDemandWatts;
        phase.PrimaryPowerSourceName = profile.SourceName;
        phase.PrimaryPowerBuildingCount = residualPowerWatts > 0
            ? (int)Math.Ceiling(residualPowerWatts / profile.PowerPerBuildingWatts)
            : 0;
        phase.FuelName = profile.UsesFuel ? profile.FuelName : string.Empty;
        phase.FuelConsumptionPerSecond = profile.UsesFuel && residualPowerWatts > 0
            ? residualPowerWatts / profile.FuelHeatValue
            : 0;
    }

    private long EstimatePhasePowerDemandWatts(PhaseSummary phase, Dictionary<int, double> buildingDemand,
        HashSet<int> unlockedItems) {
        long totalPowerDemandWatts = 0;
        foreach (var pair in buildingDemand) {
            if (pair.Key is 2901 or 2902) {
                continue;
            }

            int count = RoundUpCount(pair.Value);
            if (count <= 0 || !buildingPowerByItemId.TryGetValue(pair.Key, out double powerPerBuildingWatts)) {
                continue;
            }

            totalPowerDemandWatts += (long)Math.Round(count * powerPerBuildingWatts);
        }

        // 研究站由阶段科研站 + 矩阵站统一收口，避免和 buildingDemand 重复统计。
        int explicitLabCount = phase.ResearchLabCount + phase.MatrixLabCount;
        int plannedLabCount = GetRoundedBuildingCount(buildingDemand, 2901)
                              + GetRoundedBuildingCount(buildingDemand, 2902);
        int totalLabCount = Math.Max(explicitLabCount, plannedLabCount);
        int labItemId = unlockedItems.Contains(2902) ? 2902 : 2901;
        if (totalLabCount > 0 && buildingPowerByItemId.TryGetValue(labItemId, out double labPowerWatts)) {
            totalPowerDemandWatts += (long)Math.Round(totalLabCount * labPowerWatts);
        }

        return totalPowerDemandWatts;
    }

    private static int GetRoundedBuildingCount(Dictionary<int, double> buildingDemand, int itemId) {
        return buildingDemand.TryGetValue(itemId, out double count)
            ? (int)Math.Ceiling(count)
            : 0;
    }

    private static PowerSourceProfile GetPowerSourceProfile(ProgressPhase phase) {
        return phase switch {
            ProgressPhase.Bootstrap or ProgressPhase.Electromagnetic => windPowerProfile,
            ProgressPhase.Energy => thermalPowerProfile,
            ProgressPhase.Structure or ProgressPhase.Information or ProgressPhase.Gravity => fusionPowerProfile,
            ProgressPhase.Universe => artificialStarPowerProfile,
            _ => thermalPowerProfile,
        };
    }

    private IEnumerable<int> ResolveUnlockedItemIds(UnlockTarget target) {
        if (target.Kind != 'I') {
            yield break;
        }

        if (dataSet.ItemsById.ContainsKey(target.Id)) {
            yield return target.Id;
            yield break;
        }

        if (dataSet.RecipesById.TryGetValue(target.Id, out VanillaRecipe recipe)) {
            foreach (int outputId in recipe.Outputs.Select(o => o.Id).Distinct()) {
                if (dataSet.ItemsById.ContainsKey(outputId)) {
                    yield return outputId;
                }
            }
        }
    }

    private static int ChooseFactory(CalcRecipe calcRecipe, HashSet<int> unlockedItems, VanillaRecipe recipe) {
        List<int> factories = calcRecipe?.Factories?.Where(id => id > 1).ToList() ?? [];
        if (factories.Count == 0) {
            return GetFallbackFactory(recipe);
        }

        int selected = factories[0];
        double bestSpeed = double.MinValue;
        foreach (int factoryId in factories) {
            bool unlocked = unlockedItems.Contains(factoryId);
            double speed = factorySpeedByItemId.TryGetValue(factoryId, out double mapped) ? mapped : 1.0;
            if (unlocked && speed > bestSpeed) {
                bestSpeed = speed;
                selected = factoryId;
            }
        }

        return selected;
    }

    private static int GetFallbackFactory(VanillaRecipe recipe) {
        return recipe.RecipeType switch {
            "Smelt" => 2302,
            "Assemble" => 2303,
            "Chemical" => 2309,
            "Refine" => 2308,
            "Particle" => 2310,
            "Fractionate" => 2314,
            "Research" => 2901,
            _ => 2303,
        };
    }

    private static double GetFactorySpeed(int factoryId, VanillaRecipe recipe) {
        if (recipe.RecipeType == "Research") {
            return 1.0;
        }

        if (factorySpeedByItemId.TryGetValue(factoryId, out double speed)) {
            return speed;
        }

        return recipe.RecipeType switch {
            "Smelt" => HandcraftFurnaceSpeed,
            _ => HandcraftAssemblerSpeed,
        };
    }

    private double GetFactoryOutputRatePerSecond(int factoryId, VanillaRecipe recipe, int itemId) {
        RecipeSprayMode sprayMode = GetSprayMode(recipe);
        double speed = GetFactorySpeed(factoryId, recipe) * GetSpeedMultiplier(recipe, sprayMode);
        double outputCount = recipe.Outputs.Where(o => o.Id == itemId).Sum(o => o.Count);
        if (outputCount <= 0) {
            outputCount = recipe.Outputs[0].Count;
        }
        outputCount *= GetOutputMultiplier(recipe, sprayMode);
        return outputCount * speed * 60.0 / Math.Max(1, recipe.TimeSpend);
    }

    private static double GetInventoryBufferSeconds(int itemId) {
        return GetMatrixStage(itemId) > 0 ? MatrixBufferSeconds :
            itemId < 2000 ? ResourceBufferSeconds : IntermediateBufferSeconds;
    }

    private static int ParseNumericCode(string code) => int.Parse(code.Substring(1));

    private HashSet<int> BuildGoalTechSet(PlayerStrategyKind strategyKind) {
        var result = new HashSet<int>();

        IEnumerable<int> rootTechIds = strategyKind == PlayerStrategyKind.Speedrun
            ? BuildSpeedrunGoalRootTechIds()
            : dataSet.TechsById.Values
                .Where(tech => !tech.IsHiddenTech && tech.HashNeeded > 0)
                // 常规口径仍然保持 0-5 层全清；宇宙矩阵层只保留“任务完成”本身。
                .Where(tech => GetPhase(tech) < ProgressPhase.Universe || tech.Id == GoalTechId)
                .OrderBy(tech => tech.Id)
                .Select(tech => tech.Id);

        foreach (int techId in rootTechIds) {
            VisitTech(techId, result);
        }
        return result;
    }

    private IEnumerable<int> BuildSpeedrunGoalRootTechIds() {
        // 速通口径只保留通关依赖链，以及会显著改变推进节奏的关键科技锚点。
        HashSet<int> rootTechIds = [
            GoalTechId,
            ElectromagneticMatrixTechId,
            ThermalPowerTechId,
            PlanetaryLogisticsTechId,
            InterstellarLogisticsTechId,
            SolarSailOrbitTechId,
            RayReceiverTechId,
            IonosphereUtilizationTechId,
            VerticalLaunchTechId,
            GravitonRefractionTechId,
            .. milestoneTechIds,
            .. researchSpeedTechIds,
            .. cargoStackTechIds,
        ];

        return rootTechIds.OrderBy(id => id);
    }

    private void VisitTech(int techId, HashSet<int> result) {
        if (!result.Add(techId) || !dataSet.TechsById.TryGetValue(techId, out VanillaTech tech)) {
            return;
        }

        foreach (string code in tech.PreTechCodes.Concat(tech.ImplicitPreTechCodes)) {
            if (!string.IsNullOrEmpty(code) && code[0] == 'T') {
                VisitTech(ParseNumericCode(code), result);
            }
        }
    }
}
