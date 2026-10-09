using System;
using System.Collections.Generic;
using FE.Logic.Civilization;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 配方的功能分类。
/// </summary>
public enum WorldviewRecipeKind {
    BasicMineral,
    BasicAnalysis,
    RareCatalysis,
    TowerCultivation,
    EmbryoBreeding,
    Replacement,
}

/// <summary>生产配方的取得方式，与塔型和生产概率独立。</summary>
public enum RecipeUnlockTier {
    /// <summary>对应矩阵科技研究完成后恢复。</summary>
    Common,
    /// <summary>以唯一实物样本概率解析。</summary>
    Rare,
    /// <summary>由高级残页或持续献祭恢复。</summary>
    Legendary,
}

/// <summary>
/// 定义一项高级配方输入。消耗概率只在成功轮次结算。
/// </summary>
public sealed class WorldviewRecipeInput(int itemId, int count, float consumeProbability = 1f) {
    /// <summary>物品 ID。</summary>
    public int ItemId { get; } = itemId;

    /// <summary>每轮参与数量。</summary>
    public int Count { get; } = count;

    /// <summary>每个成功轮次的消耗概率。</summary>
    public float ConsumeProbability { get; } = consumeProbability;
}

/// <summary>
/// 定义一项高级配方产物。
/// </summary>
public sealed class WorldviewRecipeOutput(int itemId, float count, bool scalesWithRecovery = false) {
    /// <summary>物品 ID。</summary>
    public int ItemId { get; } = itemId;

    /// <summary>每个成功轮次的平均数量。</summary>
    public float Count { get; } = count;

    /// <summary>是否按高级转化塔的置换回收比例结算。</summary>
    public bool ScalesWithRecovery { get; } = scalesWithRecovery;
}

/// <summary>
/// 使用稳定身份的多输入、多输出配方。
/// </summary>
public sealed class WorldviewRecipe : BaseRecipe {
    /// <summary>跨存档、蓝图和联机稳定的配方字符串 ID。</summary>
    public string StableId { get; }

    /// <summary>解析本配方所需的唯一实物样本，与生产输入输出独立定义。</summary>
    public int AnalysisItemId { get; set; }

    /// <summary>配方所属矩阵阶段，0 为开局，1～6 为蓝至白。</summary>
    public int ResearchStage { get; set; }

    /// <summary>稳定目录对应的研究层级；基础解析是处理入口，不参与解锁。</summary>
    public RecipeUnlockTier UnlockTier => Kind is WorldviewRecipeKind.BasicMineral or WorldviewRecipeKind.TowerCultivation
        ? RecipeUnlockTier.Common
        : Kind == WorldviewRecipeKind.EmbryoBreeding || StableId == "mineral-memory:1016"
            ? RecipeUnlockTier.Legendary : RecipeUnlockTier.Rare;

    /// <summary>跨存档、蓝图和联机稳定的配方整数 ID。</summary>
    public int PersistentId { get; }

    /// <summary>玩家可见配方名称。</summary>
    public string DisplayName { get; }

    /// <summary>配方类型。</summary>
    public override ERecipe RecipeType { get; }

    /// <summary>配方功能分类。</summary>
    public WorldviewRecipeKind Kind { get; }

    /// <summary>每轮成功率。</summary>
    public float CycleSuccessProbability { get; }

    private readonly Dictionary<int, WorldviewRecipeInput> inputByItemId = [];
    private readonly HashSet<int> productItemIds = [];

    /// <summary>全部反应输入的固定顺序数组。</summary>
    internal WorldviewRecipeInput[] InputArray { get; }

    /// <summary>全部产物的固定顺序数组。</summary>
    internal WorldviewRecipeOutput[] ProductArray { get; }

    /// <summary>全部反应输入。</summary>
    public IReadOnlyList<WorldviewRecipeInput> Inputs => InputArray;

    /// <summary>全部产物。</summary>
    public IReadOnlyList<WorldviewRecipeOutput> Products => ProductArray;

    /// <summary>第一个输入的物品 ID；没有输入时为 0。</summary>
    public int PrimaryInputId => InputArray.Length > 0 ? InputArray[0].ItemId : 0;

    /// <summary>第一个产物的物品 ID；没有产物时为 0。</summary>
    public int PrimaryProductId => ProductArray.Length > 0 ? ProductArray[0].ItemId : 0;

    /// <summary>是否属于需要供养的高级配方。</summary>
    public bool IsAdvanced => Kind is not WorldviewRecipeKind.BasicMineral and not WorldviewRecipeKind.BasicAnalysis;

    /// <summary>解析样本使用研究域的价值损毁率，生产配方失败仍然回流。</summary>
    public override float DestroyRatio => Kind == WorldviewRecipeKind.BasicAnalysis
        && ResearchManager.TryGetAnalysisProbabilities(InputID, out _, out float ratio) ? ratio : 0f;

    /// <summary>
    /// 创建一张稳定配方。
    /// </summary>
    public WorldviewRecipe(string stableId, int persistentId, string displayName, ERecipe recipeType,
        WorldviewRecipeKind kind, float successProbability,
        IReadOnlyList<WorldviewRecipeInput> inputs, IReadOnlyList<WorldviewRecipeOutput> outputs)
        : base(inputs.Count > 0 ? inputs[0].ItemId : 0, successProbability,
            BuildBaseOutputs(outputs), []) {
        StableId = stableId;
        AnalysisItemId = inputs.Count > 0 ? inputs[0].ItemId : 0;
        ResearchStage = kind == WorldviewRecipeKind.BasicMineral ? 0 : 2;
        PersistentId = persistentId;
        DisplayName = displayName;
        RecipeType = recipeType;
        Kind = kind;
        CycleSuccessProbability = successProbability;
        InputArray = CopyToArray(inputs);
        ProductArray = CopyToArray(outputs);
        foreach (WorldviewRecipeInput input in InputArray) {
            inputByItemId[input.ItemId] = input;
        }
        foreach (WorldviewRecipeOutput output in ProductArray) {
            productItemIds.Add(output.ItemId);
        }
    }

    /// <summary>
    /// 查询物品是否为本配方输入，并返回固定输入定义。
    /// </summary>
    internal bool TryGetInput(int itemId, out WorldviewRecipeInput input) =>
        inputByItemId.TryGetValue(itemId, out input);

    /// <summary>
    /// 判断物品是否属于本配方输入。
    /// </summary>
    internal bool ContainsInput(int itemId) => inputByItemId.ContainsKey(itemId);

    /// <summary>
    /// 判断物品是否属于本配方产物。
    /// </summary>
    internal bool ContainsProduct(int itemId) => productItemIds.Contains(itemId);

    /// <summary>按实体产物剩余空间限制基础塔批量，解析最多每件输入产生一件实体。</summary>
    /// <param name="batchCount">由基础塔速率和输入库存确定的经过件数。</param>
    /// <param name="availableOutput">当前实体产物缓存的最小剩余件数。</param>
    /// <param name="pointsBonus">基础矿物的增产加成，解析不使用。</param>
    /// <param name="successBoost">基础矿物额外成功率，解析不使用。</param>
    /// <returns>保证所有实体产物都能缓存的经过件数；纯虚拟成果不占实体空间。</returns>
    public int LimitBasicBatchCountByOutputRoom(int batchCount, int availableOutput,
        float pointsBonus, float successBoost) {
        if (batchCount <= 0 || Products.Count == 0) { return batchCount; }
        if (Kind == WorldviewRecipeKind.BasicAnalysis) {
            AnalysisMode mode = ResearchManager.GetAnalysisOutcome(InputID).Mode;
            bool hasPhysicalOutput = mode == AnalysisMode.Research && InputID != IFE记忆源点
                || mode is AnalysisMode.SamplePreparation or AnalysisMode.TowerSacrifice
                    or AnalysisMode.MatrixSacrifice or AnalysisMode.MatrixExpansion;
            // 预留最坏情况下每件输入一件成果；扩容批次填满供料后也可能产源点。
            return hasPhysicalOutput ? Math.Min(batchCount, Math.Max(0, availableOutput)) : batchCount;
        }
        if (Kind != WorldviewRecipeKind.BasicMineral) { return batchCount; }
        int outputPerSuccess = System.Math.Max(1, UnityEngine.Mathf.CeilToInt(Products[0].Count));
        int maximumSuccesses = System.Math.Max(0, availableOutput) / outputPerSuccess;
        if (maximumSuccesses <= 0) {
            return 0;
        }
        float probability = System.Math.Min(1f,
            CycleSuccessProbability * (1f + pointsBonus) * (1f + successBoost));
        if (probability <= 0f) {
            return batchCount;
        }
        int maximumRounds = UnityEngine.Mathf.FloorToInt(maximumSuccesses / probability);
        return System.Math.Min(batchCount, System.Math.Max(0, maximumRounds));
    }

    /// <inheritdoc />
    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus,
        float successBoost, int batchCount, int fluidInputIncAvg, ref int fluidInputInc,
        ProductOutputBuffer outputs) {
        outputs.Clear();
        if (batchCount <= 0) {
            return default;
        }

        if (Kind == WorldviewRecipeKind.BasicAnalysis) {
            AnalysisBatchResult result = ResearchManager.ProcessAnalysisBatch(InputID, batchCount, ref seed);
            int returnedCount = batchCount - result.ConsumedCount - result.ReturnedSampleCount;
            if (result.ProductCount > 0) { outputs.Add(true, result.ProductItemId, result.ProductCount); }
            fluidInputInc -= fluidInputIncAvg * batchCount;
            return new FractionationBatchResult {
                InputRemoveCount = batchCount,
                ConsumedRegisterCount = result.ConsumedCount,
                SuccessCount = result.SuccessCount,
                ReturnedProductCount = result.ReturnedSampleCount,
                PassThroughCount = returnedCount,
                PassThroughInc = fluidInputIncAvg * returnedCount,
            };
        }

        int successCount = RollExpected(ref seed,
            batchCount * CycleSuccessProbability * (1f + pointsBonus) * (1f + successBoost));
        if (successCount > batchCount) {
            successCount = batchCount;
        }
        int passThroughCount = batchCount - successCount;
        if (successCount > 0) {
            outputs.Add(true, InputID, successCount * 2);
        }

        fluidInputInc -= fluidInputIncAvg * batchCount;
        return new FractionationBatchResult {
            InputRemoveCount = batchCount,
            ConsumedRegisterCount = successCount,
            SuccessCount = successCount,
            PassThroughCount = passThroughCount,
            PassThroughInc = fluidInputIncAvg * passThroughCount,
        };
    }

    /// <summary>
    /// 按长期期望值和单个随机数结算一种结果数量。
    /// </summary>
    public static int RollExpected(ref uint seed, double expectedCount) {
        if (expectedCount <= 0d) {
            return 0;
        }
        if (expectedCount >= int.MaxValue) {
            return int.MaxValue;
        }
        return (int)(expectedCount + GetRandDouble(ref seed));
    }

    private static T[] CopyToArray<T>(IReadOnlyList<T> source) {
        if (source == null || source.Count == 0) {
            return Array.Empty<T>();
        }
        T[] result = new T[source.Count];
        for (int i = 0; i < source.Count; i++) {
            result[i] = source[i];
        }
        return result;
    }

    private static List<OutputInfo> BuildBaseOutputs(IReadOnlyList<WorldviewRecipeOutput> outputs) {
        List<OutputInfo> result = [];
        foreach (WorldviewRecipeOutput output in outputs) {
            result.Add(new OutputInfo(1f, output.ItemId, output.Count));
        }
        return result;
    }
}
