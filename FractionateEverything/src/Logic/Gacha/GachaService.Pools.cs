using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Gacha;

public static partial class GachaService {
    public static void InitPools() {
        EnsureRecipeRewardIndex();
        cachedMatrixId = GetCurrentProgressMatrixId();
        cachedFocus = GachaManager.CurrentFocus;
        cachedMode = GachaManager.CurrentMode;
        cachedOpeningRecipeStateHash = GetOpeningRecipeStateHash();

        pools.Clear();
        Array.Clear(poolsById, 0, poolsById.Length);

        var openingPool = new GachaPool(GachaPool.PoolIdOpeningLine, GetPoolNameKey(GachaPool.PoolIdOpeningLine));
        FillOpeningLinePool(openingPool);
        RegisterPool(openingPool);

        var protoPool = new GachaPool(GachaPool.PoolIdProtoLoop, GetPoolNameKey(GachaPool.PoolIdProtoLoop));
        FillProtoLoopPool(protoPool);
        RegisterPool(protoPool);

        var growthPool = new GachaPool(GachaPool.PoolIdGrowth, GetPoolNameKey(GachaPool.PoolIdGrowth));
        FillGrowthPool(growthPool);
        RegisterPool(growthPool);

        var focusPool = new GachaPool(GachaPool.PoolIdFocus, GetPoolNameKey(GachaPool.PoolIdFocus));
        FillFocusPool(focusPool);
        RegisterPool(focusPool);
    }

    private static void EnsurePoolsFresh() {
        int currentMatrixId = GetCurrentProgressMatrixId();
        GachaFocusType currentFocus = GachaManager.CurrentFocus;
        GachaMode currentMode = GachaManager.CurrentMode;
        int currentRecipeStateHash = GetOpeningRecipeStateHash();
        if (pools.Count == GachaPool.PoolCount
            && cachedMatrixId == currentMatrixId
            && cachedFocus == currentFocus
            && cachedMode == currentMode
            && cachedOpeningRecipeStateHash == currentRecipeStateHash) {
            return;
        }

        InitPools();
    }

    private static void RegisterPool(GachaPool pool) {
        pools.Add(pool);
        if (GachaPool.IsValidPoolId(pool.PoolId)) {
            poolsById[pool.PoolId] = pool;
        }
    }

    public static int GetCurrentDrawMatrixId() {
        return GetCurrentProgressMatrixId();
    }

    public static int GetDrawMatrixCost(int poolId, int drawCount) {
        if (!GachaPool.IsDrawPool(poolId) || drawCount <= 0) {
            return 0;
        }

        int singleCost = poolId switch {
            GachaPool.PoolIdOpeningLine => 1,
            GachaPool.PoolIdProtoLoop => IsSpeedrunMode ? 1 : 1,
            _ => 0,
        };
        return singleCost * drawCount;
    }

    private static void FillOpeningLinePool(GachaPool pool) {
        var allRecipes = GetOpeningRecipes();
        int currentStageIndex = GetCurrentProgressStageIndex();

        var previousStageRecipes = new List<int>();
        var currentStageRecipes = new List<int>();
        var lockedCurrentStageRecipes = new List<int>();

        foreach (BaseRecipe recipe in allRecipes) {
            int stageIndex = GetMatrixStageIndex(recipe.MatrixID);
            int itemId = recipe.InputID;
            if (stageIndex < currentStageIndex) {
                AddWeighted(previousStageRecipes, itemId, GetRecipeWeight(recipe, currentStageIndex));
                continue;
            }

            if (stageIndex == currentStageIndex) {
                int weight = GetRecipeWeight(recipe, currentStageIndex);
                AddWeighted(currentStageRecipes, itemId, weight);
                if (!RecipeGrowthQueries.IsUnlocked(recipe)) {
                    AddWeighted(lockedCurrentStageRecipes, itemId, weight + 1);
                }
            }
        }

        if (IsSpeedrunMode) {
            List<int> targetRecipes = currentStageRecipes.Count > 0 ? currentStageRecipes :
                previousStageRecipes.Count > 0 ? previousStageRecipes : lockedCurrentStageRecipes;
            if (targetRecipes.Count == 0) {
                targetRecipes = [IFE残片];
            }
            pool.PoolC.AddRange(targetRecipes);
            pool.PoolB.AddRange(targetRecipes);
            pool.PoolA.AddRange(targetRecipes);
            pool.PoolS.AddRange(lockedCurrentStageRecipes.Count > 0 ? lockedCurrentStageRecipes : targetRecipes);
            return;
        }

        pool.PoolC.Add(IFE残片);

        if (previousStageRecipes.Count > 0) {
            pool.PoolB.AddRange(previousStageRecipes);
        } else {
            pool.PoolB.AddRange(currentStageRecipes);
        }
        if (pool.PoolB.Count == 0) {
            pool.PoolB.Add(IFE残片);
        }

        if (currentStageRecipes.Count > 0) {
            pool.PoolA.AddRange(currentStageRecipes);
        }
        if (pool.PoolA.Count == 0) {
            pool.PoolA.AddRange(pool.PoolB);
        }

        if (lockedCurrentStageRecipes.Count > 0) {
            pool.PoolS.AddRange(lockedCurrentStageRecipes);
        } else if (currentStageRecipes.Count > 0) {
            pool.PoolS.AddRange(currentStageRecipes);
        } else {
            pool.PoolS.AddRange(pool.PoolA);
        }
    }

    private static void FillProtoLoopPool(GachaPool pool) {
        List<int> weightedEmbryos = [];
        for (int itemId = IFE交互塔原胚; itemId <= IFE精馏塔原胚; itemId++) {
            AddWeighted(weightedEmbryos, itemId, GetEmbryoWeight(itemId));
        }
        AddWeighted(weightedEmbryos, IFE分馏塔定向原胚, GetEmbryoWeight(IFE分馏塔定向原胚));

        pool.PoolC.AddRange(weightedEmbryos);
        pool.PoolB.AddRange(weightedEmbryos);
        pool.PoolA.AddRange(weightedEmbryos);
        pool.PoolS.AddRange(weightedEmbryos);
    }

    private static void FillGrowthPool(GachaPool pool) {
        pool.PoolC.Add(IFE残片);
        pool.PoolB.Add(GetCurrentDrawMatrixId());
        pool.PoolA.Add(GetFocusedEmbryoReward());
        pool.PoolS.Add(IFE分馏塔定向原胚);
    }

    private static void FillFocusPool(GachaPool pool) {
        foreach (var focus in focusDefinitions) {
            pool.PoolC.Add((int)focus.FocusType);
        }
    }

    /// <summary>
    /// 开线池当前只消费“生产型”配方。
    /// 工具/解锁型与特殊成长型配方继续走科技、原胚闭环或成长页，不混入随机开线入口。
    /// </summary>
    private static bool IsOpeningLineRecipe(BaseRecipe recipe) {
        return recipe != null
               && recipe.RecipeType is ERecipe.MineralCopy or ERecipe.Conversion
               && recipe.GrowthRole == ERecipeGrowthRole.Production
               && recipe.InputID > 0
               && recipe.MatrixID != I黑雾矩阵;
    }

    private static int GetOpeningRecipeStateHash() {
        int hash = 17;
        unchecked {
            foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
                if (!IsOpeningLineRecipe(recipe)) {
                    continue;
                }

                hash = hash * 31 + recipe.InputID;
                hash = hash * 31 + (int)recipe.RecipeType;
                hash = hash * 31 + RecipeGrowthQueries.GetLevel(recipe);
                hash = hash * 31 + recipe.MatrixID;
            }
        }
        return hash;
    }

    private static List<BaseRecipe> GetOpeningRecipes() {
        int currentStageIndex = GetCurrentProgressStageIndex();
        var recipes = new List<BaseRecipe>();
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            if (!IsOpeningLineRecipe(recipe) || GetMatrixStageIndex(recipe.MatrixID) > currentStageIndex) {
                continue;
            }

            recipes.Add(recipe);
        }
        return recipes;
    }

    private static int GetRecipeWeight(BaseRecipe recipe, int currentStageIndex) {
        RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
        float weight = family switch {
            RecipeFamily.MineralCopyNormal => IsSpeedrunMode ? 120f : 100f,
            RecipeFamily.ConversionMaterialNormal => IsSpeedrunMode ? 120f : 100f,
            RecipeFamily.ConversionBuilding => IsSpeedrunMode ? 32f : 40f,
            _ => 1f,
        };

        int recipeStageIndex = GetMatrixStageIndex(recipe.MatrixID);
        if (!RecipeGrowthQueries.IsUnlocked(recipe)) {
            weight *= IsSpeedrunMode ? 1.8f : 1.5f;
        }
        if (recipeStageIndex == currentStageIndex) {
            weight *= IsSpeedrunMode ? 1.5f : 1.3f;
        } else if (IsSpeedrunMode
                   && recipeStageIndex == currentStageIndex - 1
                   && !RecipeGrowthQueries.IsMaxed(recipe)) {
            weight *= 1.25f;
        }

        weight *= GetOpeningRecipeFocusMultiplier(recipe, currentStageIndex);

        if (RecipeGrowthQueries.IsMaxed(recipe)) {
            weight *= IsSpeedrunMode ? 0.20f : 0.35f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(weight));
    }

    private static int GetEmbryoWeight(int itemId) {
        float weight;
        if (itemId == IFE分馏塔定向原胚) {
            weight = IsSpeedrunMode ? 80f : 65f;
        } else if (itemId == GetFocusedEmbryoReward()) {
            weight = IsSpeedrunMode ? 115f : 100f;
        } else {
            weight = IsSpeedrunMode ? 85f : 80f;
        }

        if (!IsSpeedrunMode
            && GachaManager.CurrentFocus == GachaFocusType.RectificationEconomy
            && itemId == IFE精馏塔原胚) {
            weight *= 1.3f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(weight));
    }

    private static void AddWeighted(List<int> target, int itemId, int weight) {
        if (itemId <= 0) {
            return;
        }

        int count = Math.Max(1, weight);
        for (int i = 0; i < count; i++) {
            target.Add(itemId);
        }
    }
}
