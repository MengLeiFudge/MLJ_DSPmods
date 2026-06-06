using System;
using System.Collections.Generic;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.FracRecipes;
using UnityEngine;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取卡池构建、缓存和奖励索引逻辑。
/// </summary>
public static partial class GachaService {
    public static void InitPools() {
        EnsureRecipeRewardIndex(force: true);
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
        int currentStageIndex = GetCurrentProgressStageIndex();
        var allUnits = GetOpeningDrawUnits(currentStageIndex);

        var previousStageUnits = new List<int>();
        var currentStageUnits = new List<int>();
        var lockedCurrentStageUnits = new List<int>();

        foreach (GachaDrawUnit unit in allUnits) {
            int stageIndex = GetDrawUnitStageIndex(unit);
            int itemId = unit.DisplayItemId;
            if (stageIndex < currentStageIndex) {
                AddWeighted(previousStageUnits, itemId, GetDrawUnitWeight(unit, currentStageIndex));
                continue;
            }

            if (stageIndex == currentStageIndex) {
                int weight = GetDrawUnitWeight(unit, currentStageIndex);
                AddWeighted(currentStageUnits, itemId, weight);
                if (HasLockedRecipeAtStage(unit, currentStageIndex)) {
                    AddWeighted(lockedCurrentStageUnits, itemId, weight + 1);
                }
            }
        }

        if (IsSpeedrunMode) {
            List<int> targetUnits = currentStageUnits.Count > 0 ? currentStageUnits :
                previousStageUnits.Count > 0 ? previousStageUnits : lockedCurrentStageUnits;
            if (targetUnits.Count == 0) {
                targetUnits = [IFE残片];
            }
            pool.PoolC.AddRange(targetUnits);
            pool.PoolB.AddRange(targetUnits);
            pool.PoolA.AddRange(targetUnits);
            pool.PoolS.AddRange(lockedCurrentStageUnits.Count > 0 ? lockedCurrentStageUnits : targetUnits);
            return;
        }

        pool.PoolC.Add(IFE残片);

        if (previousStageUnits.Count > 0) {
            pool.PoolB.AddRange(previousStageUnits);
        } else {
            pool.PoolB.AddRange(currentStageUnits);
        }
        if (pool.PoolB.Count == 0) {
            pool.PoolB.Add(IFE残片);
        }

        if (currentStageUnits.Count > 0) {
            pool.PoolA.AddRange(currentStageUnits);
        }
        if (pool.PoolA.Count == 0) {
            pool.PoolA.AddRange(pool.PoolB);
        }

        if (lockedCurrentStageUnits.Count > 0) {
            pool.PoolS.AddRange(lockedCurrentStageUnits);
        } else if (currentStageUnits.Count > 0) {
            pool.PoolS.AddRange(currentStageUnits);
        } else {
            pool.PoolS.AddRange(pool.PoolA);
        }
    }

    private static void FillProtoLoopPool(GachaPool pool) {
        List<int> weightedEmbryos = [];
        foreach (int itemId in FractionatorTowerCatalog.ActiveFractionatorProtoIds) {
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
    /// 主抽取路线偏好当前只消费“生产型”配方。
    /// 工具/解锁型与特殊成长型配方继续走科技、主抽取原胚偏好或成长规划，不混入随机路线入口。
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
            foreach (GachaDrawUnit unit in GetOpeningDrawUnits(GetCurrentProgressStageIndex())) {
                hash = hash * 31 + (int)unit.Key.Kind;
                hash = hash * 31 + (int)unit.Key.RecipeType;
                hash = hash * 31 + unit.Key.InputId;
                hash = hash * 31 + GachaManager.GetDrawUnitResonance(unit.Key);
                foreach (RecipeKey key in unit.RecipeKeys) {
                    BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
                    if (recipe == null) {
                        continue;
                    }

                    hash = hash * 31 + recipe.InputID;
                    hash = hash * 31 + (int)recipe.RecipeType;
                    hash = hash * 31 + RecipeGrowthQueries.GetLevel(recipe);
                    hash = hash * 31 + recipe.MatrixID;
                }
            }
        }
        return hash;
    }

    private static List<GachaDrawUnit> GetOpeningDrawUnits(int maxStageIndex) {
        var mineralGroups = new Dictionary<GachaDrawUnitKey, List<BaseRecipe>>();
        var mineralDisplayItems = new Dictionary<GachaDrawUnitKey, int>();
        var rectificationGroups = new Dictionary<GachaDrawUnitKey, List<BaseRecipe>>();
        var rectificationDisplayItems = new Dictionary<GachaDrawUnitKey, int>();
        var conversionRecipes = new List<BaseRecipe>();

        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            if (recipe == null || GetMatrixStageIndex(recipe.MatrixID) > maxStageIndex) {
                continue;
            }

            if (IsOpeningLineRecipe(recipe) && recipe.RecipeType == ERecipe.MineralCopy) {
                GachaDrawUnitKey key = GetMineralCopyDrawUnitKey(recipe, out int displayItemId);
                AddRecipeToDrawUnitGroup(mineralGroups, mineralDisplayItems, key, displayItemId, recipe);
            } else if (IsOpeningLineRecipe(recipe) && recipe.RecipeType == ERecipe.Conversion) {
                conversionRecipes.Add(recipe);
            } else if (IsRectificationOpeningUnitRecipe(recipe)) {
                GachaDrawUnitKey key = GetRectificationDrawUnitKey((RectificationRecipe)recipe, out int displayItemId);
                AddRecipeToDrawUnitGroup(rectificationGroups, rectificationDisplayItems, key, displayItemId, recipe);
            }
        }

        var units = new List<GachaDrawUnit>();
        AddGroupedDrawUnits(units, mineralGroups, mineralDisplayItems);
        units.AddRange(BuildConversionChainDrawUnits(conversionRecipes));
        AddGroupedDrawUnits(units, rectificationGroups, rectificationDisplayItems);
        return units;
    }

    private static int GetRecipeWeight(BaseRecipe recipe, int currentStageIndex) {
        RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
        float weight = family switch {
            RecipeFamily.MineralCopyNormal => IsSpeedrunMode ? 120f : 100f,
            RecipeFamily.ConversionItemChain => IsSpeedrunMode ? 120f : 100f,
            RecipeFamily.ConversionBuilding => IsSpeedrunMode ? 32f : 40f,
            RecipeFamily.Rectification => IsSpeedrunMode ? 58f : 46f,
            _ => 1f,
        };

        int recipeStageIndex = GetMatrixStageIndex(recipe.MatrixID);
        if (RecipeGrowthQueries.GetLevel(recipe) <= 0) {
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

    private static int GetDrawUnitWeight(GachaDrawUnit unit, int currentStageIndex) {
        float totalWeight = 0f;
        int recipeCount = 0;
        foreach (RecipeKey key in unit.RecipeKeys) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
            if (recipe == null) {
                continue;
            }

            totalWeight += GetRecipeWeight(recipe, currentStageIndex);
            recipeCount++;
        }

        if (recipeCount <= 0) {
            return 1;
        }

        float weight = totalWeight / recipeCount + Math.Min(recipeCount, 6) * (IsSpeedrunMode ? 10f : 8f);
        if (IsDrawUnitFullyUnlocked(unit)
            && GachaManager.GetDrawUnitResonance(unit.Key) < GachaManager.MaxDrawUnitResonance) {
            weight *= IsSpeedrunMode ? 1.18f : 1.12f;
        }
        return Mathf.Max(1, Mathf.RoundToInt(weight));
    }

    private static int GetDrawUnitStageIndex(GachaDrawUnit unit) {
        int stageIndex = 0;
        foreach (RecipeKey key in unit.RecipeKeys) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
            if (recipe != null) {
                stageIndex = Math.Max(stageIndex, GetMatrixStageIndex(recipe.MatrixID));
            }
        }
        return stageIndex;
    }

    private static bool HasLockedRecipeAtStage(GachaDrawUnit unit, int stageIndex) {
        foreach (RecipeKey key in unit.RecipeKeys) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
            if (recipe != null
                && GetMatrixStageIndex(recipe.MatrixID) == stageIndex
                && RecipeGrowthQueries.GetLevel(recipe) <= 0) {
                return true;
            }
        }
        return false;
    }

    private static bool IsDrawUnitFullyUnlocked(GachaDrawUnit unit) {
        foreach (RecipeKey key in unit.RecipeKeys) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
            if (recipe != null && RecipeGrowthQueries.GetLevel(recipe) <= 0) {
                return false;
            }
        }
        return unit.RecipeKeys.Length > 0;
    }

    private static void AddRecipeToDrawUnitGroup(Dictionary<GachaDrawUnitKey, List<BaseRecipe>> groups,
        Dictionary<GachaDrawUnitKey, int> displayItems, GachaDrawUnitKey key, int displayItemId, BaseRecipe recipe) {
        if (!groups.TryGetValue(key, out List<BaseRecipe> recipes)) {
            recipes = [];
            groups[key] = recipes;
            displayItems[key] = displayItemId;
        }

        recipes.Add(recipe);
    }

    private static void AddGroupedDrawUnits(List<GachaDrawUnit> units,
        Dictionary<GachaDrawUnitKey, List<BaseRecipe>> groups,
        Dictionary<GachaDrawUnitKey, int> displayItems) {
        foreach (KeyValuePair<GachaDrawUnitKey, List<BaseRecipe>> pair in groups) {
            if (!displayItems.TryGetValue(pair.Key, out int displayItemId)) {
                displayItemId = pair.Value.Count > 0 ? pair.Value[0].InputID : 0;
            }

            units.Add(CreateDrawUnit(pair.Key, displayItemId, pair.Value));
        }
    }

    private static GachaDrawUnit CreateDrawUnit(GachaDrawUnitKey key, int displayItemId, List<BaseRecipe> recipes) {
        var recipeKeys = new RecipeKey[recipes.Count];
        for (int i = 0; i < recipes.Count; i++) {
            recipeKeys[i] = RecipeKey.FromRecipe(recipes[i]);
        }

        return new GachaDrawUnit(key, displayItemId, recipeKeys);
    }

    private static GachaDrawUnitKey GetMineralCopyDrawUnitKey(BaseRecipe recipe, out int displayItemId) {
        int inputId = recipe.InputID;
        if (IsBasicResourceCopy(inputId)) {
            displayItemId = I铁矿;
            return new GachaDrawUnitKey(GachaDrawUnitKind.ResourceGroup, ERecipe.MineralCopy, I铁矿);
        }

        if (IsFluidResourceCopy(inputId)) {
            displayItemId = I水;
            return new GachaDrawUnitKey(GachaDrawUnitKind.ResourceGroup, ERecipe.MineralCopy, I水);
        }

        if (IsRareResourceCopy(inputId)) {
            displayItemId = I单极磁石;
            return new GachaDrawUnitKey(GachaDrawUnitKind.ResourceGroup, ERecipe.MineralCopy, I单极磁石);
        }

        displayItemId = inputId;
        return GachaDrawUnitKey.FromRecipe(recipe);
    }

    private static bool IsBasicResourceCopy(int inputId) {
        return inputId switch {
            I木材 or I植物燃料 or I铁矿 or I铜矿 or I硅石 or I钛石 or I石矿 or I煤矿 => true,
            _ => false,
        };
    }

    private static bool IsFluidResourceCopy(int inputId) {
        return inputId switch {
            I水 or I原油 or I硫酸 or I氢 or I重氢 => true,
            _ => false,
        };
    }

    private static bool IsRareResourceCopy(int inputId) {
        return inputId switch {
            I可燃冰 or I金伯利矿石 or I分形硅石 or I光栅石 or I刺笋结晶
                or I单极磁石 or I有机晶体 or I临界光子 or I反物质 => true,
            _ => false,
        };
    }

    private static List<GachaDrawUnit> BuildConversionChainDrawUnits(List<BaseRecipe> conversionRecipes) {
        var recipeByInputId = new Dictionary<int, BaseRecipe>();
        var parents = new Dictionary<int, int>();
        foreach (BaseRecipe recipe in conversionRecipes) {
            recipeByInputId[recipe.InputID] = recipe;
            EnsureUnionParent(parents, recipe.InputID);
        }

        foreach (BaseRecipe recipe in conversionRecipes) {
            foreach (OutputInfo output in recipe.OutputMain) {
                if (!recipeByInputId.ContainsKey(output.OutputID)) {
                    continue;
                }
                Union(parents, recipe.InputID, output.OutputID);
            }
        }

        var groups = new Dictionary<int, List<BaseRecipe>>();
        foreach (BaseRecipe recipe in conversionRecipes) {
            int root = FindParent(parents, recipe.InputID);
            if (!groups.TryGetValue(root, out List<BaseRecipe> recipes)) {
                recipes = [];
                groups[root] = recipes;
            }
            recipes.Add(recipe);
        }

        var units = new List<GachaDrawUnit>();
        foreach (List<BaseRecipe> recipes in groups.Values) {
            int displayItemId = GetSmallestInputId(recipes);
            var key = recipes.Count > 1
                ? new GachaDrawUnitKey(GachaDrawUnitKind.ConversionChain, ERecipe.Conversion, displayItemId)
                : GachaDrawUnitKey.FromRecipe(recipes[0]);
            units.Add(CreateDrawUnit(key, displayItemId, recipes));
        }

        return units;
    }

    private static bool IsRectificationOpeningUnitRecipe(BaseRecipe recipe) {
        if (recipe is not RectificationRecipe rectificationRecipe) {
            return false;
        }

        if (rectificationRecipe.Kind == RectificationRecipe.RectificationRecipeKind.EssenceTuning) {
            return true;
        }

        return rectificationRecipe.Kind == RectificationRecipe.RectificationRecipeKind.MatrixExtraction
               && rectificationRecipe.InputID == I黑雾矩阵
               && IsDarkFogMatrixVisible();
    }

    private static bool IsDarkFogMatrixVisible() {
        return GameMain.history != null && GameMain.history.ItemUnlocked(I黑雾矩阵)
               || GetModDataItemCount(I黑雾矩阵) > 0;
    }

    private static GachaDrawUnitKey GetRectificationDrawUnitKey(RectificationRecipe recipe, out int displayItemId) {
        if (recipe.Kind == RectificationRecipe.RectificationRecipeKind.MatrixExtraction
            && recipe.InputID == I黑雾矩阵) {
            displayItemId = I黑雾矩阵;
            return new GachaDrawUnitKey(GachaDrawUnitKind.RectificationFamily, ERecipe.Rectification, I黑雾矩阵);
        }

        displayItemId = IFE电磁精华;
        return new GachaDrawUnitKey(GachaDrawUnitKind.RectificationFamily, ERecipe.Rectification, IFE电磁精华);
    }

    private static int GetSmallestInputId(List<BaseRecipe> recipes) {
        int itemId = int.MaxValue;
        foreach (BaseRecipe recipe in recipes) {
            itemId = Math.Min(itemId, recipe.InputID);
        }
        return itemId == int.MaxValue ? 0 : itemId;
    }

    private static void EnsureUnionParent(Dictionary<int, int> parents, int itemId) {
        if (!parents.ContainsKey(itemId)) {
            parents[itemId] = itemId;
        }
    }

    private static int FindParent(Dictionary<int, int> parents, int itemId) {
        EnsureUnionParent(parents, itemId);
        int parent = parents[itemId];
        if (parent == itemId) {
            return itemId;
        }

        int root = FindParent(parents, parent);
        parents[itemId] = root;
        return root;
    }

    private static void Union(Dictionary<int, int> parents, int a, int b) {
        int rootA = FindParent(parents, a);
        int rootB = FindParent(parents, b);
        if (rootA != rootB) {
            parents[rootB] = rootA;
        }
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
        if (TryGetProtoLoopDrawUnit(itemId, out GachaDrawUnit unit)
            && IsDrawUnitFullyUnlocked(unit)
            && GachaManager.GetDrawUnitResonance(unit.Key) < GachaManager.MaxDrawUnitResonance) {
            weight *= IsSpeedrunMode ? 1.16f : 1.10f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(weight));
    }

    private static IEnumerable<GachaDrawUnit> GetRewardDrawUnits() {
        foreach (GachaDrawUnit unit in GetOpeningDrawUnits(int.MaxValue)) {
            yield return unit;
        }

        foreach (GachaDrawUnit unit in GetProtoLoopDrawUnits()) {
            yield return unit;
        }
    }

    private static List<GachaDrawUnit> GetProtoLoopDrawUnits() {
        List<GachaDrawUnit> units = [];
        foreach (int protoId in FractionatorTowerCatalog.ActiveFractionatorProtoIds) {
            if (TryCreateTowerDrawUnit(protoId, out GachaDrawUnit unit)) {
                units.Add(unit);
            }
        }
        return units;
    }

    private static bool TryGetProtoLoopDrawUnit(int itemId, out GachaDrawUnit unit) {
        return TryCreateTowerDrawUnit(itemId, out unit);
    }

    private static bool TryCreateTowerDrawUnit(int protoId, out GachaDrawUnit unit) {
        unit = default;
        if (!FractionatorTowerCatalog.TryGetBuildingIdForProto(protoId, out int buildingId)) {
            return false;
        }

        List<BaseRecipe> recipes = [];
        BaseRecipe forwardRecipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.BuildingTrain, protoId);
        BaseRecipe reverseRecipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.BuildingTrain, buildingId);
        if (forwardRecipe != null) {
            recipes.Add(forwardRecipe);
        }
        if (reverseRecipe != null) {
            recipes.Add(reverseRecipe);
        }
        if (recipes.Count == 0) {
            return false;
        }

        unit = CreateDrawUnit(
            new GachaDrawUnitKey(GachaDrawUnitKind.TowerFamily, ERecipe.BuildingTrain, buildingId),
            protoId,
            recipes);
        return true;
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
