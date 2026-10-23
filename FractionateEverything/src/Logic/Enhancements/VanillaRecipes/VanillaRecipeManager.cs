using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Civilization;
using FE.Logic.Civilization.Monument;
using FE.Logic.Fractionation.Recipes;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Enhancements.VanillaRecipes;

/// <summary>
/// 原版配方调节系统的注册、查找和存档入口。
/// </summary>
public static class VanillaRecipeManager {
    private static double appliedRatio = double.NaN;
    private static readonly List<VanillaRecipe> VanillaRecipeList = [];
    private static readonly Dictionary<int, VanillaRecipe> VanillaRecipeDic = [];

    /// <summary>
    /// 添加原版配方调节项。
    /// </summary>
    public static void AddVanillaRecipes() {
        LogInfo("Add vanilla recipes...");
        VanillaRecipeList.Clear();
        VanillaRecipeDic.Clear();
        appliedRatio = double.NaN;
        foreach (RecipeProto recipe in LDB.recipes.dataArray) {
            if (recipe == null || recipe.TimeSpend <= 0 || recipe.Items == null || recipe.ItemCounts == null
                || recipe.Items.Length != recipe.ItemCounts.Length || recipe.Results == null || recipe.Results.Length == 0
                || recipe.ResultCounts == null || recipe.Results.Length != recipe.ResultCounts.Length
                || recipe.ItemCounts.Any(count => count <= 0) || recipe.ResultCounts.Any(count => count <= 0)
                || recipe.Items.Any(id => !LDB.items.Exist(id)) || recipe.Results.Any(id => !LDB.items.Exist(id))
                || recipe.Type == ERecipeType.None || recipe.Type == ERecipeType.Fractionate || AdvancedRecipeCatalog.IsAdvancedProto(recipe.ID)) {
                continue;
            }

            var vanillaRecipe = new VanillaRecipe(recipe);
            VanillaRecipeList.Add(vanillaRecipe);
            VanillaRecipeDic[recipe.ID] = vanillaRecipe;
        }
        LogInfo($"Added {VanillaRecipeList.Count} vanilla recipes.");
    }

    /// <summary>
    /// 获取指定原版配方的调节状态。
    /// </summary>
    public static VanillaRecipe GetVanillaRecipe(int recipeId) {
        return VanillaRecipeDic.TryGetValue(recipeId, out VanillaRecipe recipe) ? recipe : null;
    }

    public static double GlobalTimeLimitRatio => StackingManager.CurrentVanillaRecipeTimeRatio / MonumentManager.SpeedFactor;

    /// <summary>空配方集不能触发通关，未解锁的有效配方也参与终局判定。</summary>
    internal static bool AllAtOneFrame => VanillaRecipeList.Count > 0 && VanillaRecipeList.TrueForAll(value => value.recipe.TimeSpend == 1);

    /// <summary>只从加载基线重新计算；整帧结果未改变时不刷新执行缓存。</summary>
    internal static void RefreshComputedTimes() {
        double ratio = GlobalTimeLimitRatio;
        if (appliedRatio == ratio) { return; }
        appliedRatio = ratio;
        bool changed = false;
        foreach (VanillaRecipe value in VanillaRecipeList) {
            int previous = value.recipe.TimeSpend;
            value.ApplyTimeSpend();
            changed |= previous != value.recipe.TimeSpend;
        }
        if (changed) { RefreshRecipeExecuteData(); }
    }

    public static void RefreshRecipeExecuteData(int recipeId = 0) {
        RecipeProto.InitRecipeItems();
        GameData data = GameMain.data;
        if (data?.factories == null) {
            return;
        }

        for (int factoryIndex = 0; factoryIndex < data.factoryCount; factoryIndex++) {
            FactorySystem factorySystem = data.factories[factoryIndex]?.factorySystem;
            if (factorySystem == null) {
                continue;
            }

            RefreshAssemblerExecuteData(factorySystem, recipeId);
            RefreshLabExecuteData(factorySystem, recipeId);
        }
    }

    public static void IntoOtherSave() {
        appliedRatio = double.NaN;
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            vanillaRecipe.IntoOtherSave();
        }
        RefreshRecipeExecuteData();
    }

    public static void SyncRuntimeStateAfterImport() {
        ApplyRecipeTimes();
        RefreshRecipeExecuteData();
    }

    private static void ApplyRecipeTimes() {
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            vanillaRecipe.ApplyTimeSpend();
        }
    }

    private static void RefreshAssemblerExecuteData(FactorySystem factorySystem, int recipeId) {
        if (factorySystem.assemblerPool == null) {
            return;
        }

        for (int i = 1; i < factorySystem.assemblerCursor; i++) {
            ref AssemblerComponent assembler = ref factorySystem.assemblerPool[i];
            if (assembler.id != i || assembler.recipeId <= 0) {
                continue;
            }
            if (recipeId > 0 && assembler.recipeId != recipeId) {
                continue;
            }

            assembler.recipeExecuteData = RecipeProto.recipeExecuteData[assembler.recipeId];
        }
    }

    private static void RefreshLabExecuteData(FactorySystem factorySystem, int recipeId) {
        if (factorySystem.labPool == null) {
            return;
        }

        for (int i = 1; i < factorySystem.labCursor; i++) {
            ref LabComponent lab = ref factorySystem.labPool[i];
            if (lab.id != i || lab.researchMode || lab.recipeId <= 0) {
                continue;
            }
            if (recipeId > 0 && lab.recipeId != recipeId) {
                continue;
            }

            lab.recipeExecuteData = RecipeProto.recipeExecuteData[lab.recipeId];
        }
    }
}

/// <summary>
/// 单个原版配方的加载基线与实时加速。
/// </summary>
public class VanillaRecipe {
    private readonly Dictionary<int, int> inputCounts = [];
    public readonly RecipeProto recipe;
    private readonly int timeSpend;
    public int MatrixId { get; }

    public VanillaRecipe(RecipeProto recipe) {
        this.recipe = recipe;
        MatrixId = ResolveMatrixId(recipe);
        for (int i = 0; i < recipe.Items.Length; i++) {
            inputCounts.Add(recipe.Items[i], recipe.ItemCounts[i]);
        }
        timeSpend = recipe.TimeSpend;
    }

    public bool LimitedByMatrix => !TechManager.IsVanillaEnhancementUnlockedForMatrix(MatrixId);

    private static int ResolveMatrixId(RecipeProto recipeProto) {
        if (recipeProto?.Results != null) {
            foreach (int resultId in recipeProto.Results) {
                if (resultId > 0 && resultId < itemToMatrix.Length && itemToMatrix[resultId] > 0) {
                    return itemToMatrix[resultId];
                }
            }
        }

        if (recipeProto?.Items != null) {
            foreach (int itemId in recipeProto.Items) {
                if (itemId > 0 && itemId < itemToMatrix.Length && itemToMatrix[itemId] > 0) {
                    return itemToMatrix[itemId];
                }
            }
        }

        return I电磁矩阵;
    }

    /// <summary>按当前堆叠与丰碑加速重新计算配方时间。</summary>
    public void ApplyTimeSpend() {
        recipe.TimeSpend = Math.Max(1, (int)Math.Ceiling(timeSpend * VanillaRecipeManager.GlobalTimeLimitRatio));
    }

    public virtual void IntoOtherSave() {
        // 还原配方
        for (int i = 0; i < recipe.Items.Length; i++) {
            inputCounts.TryGetValue(recipe.Items[i], out int count);
            if (count > 0) {
                recipe.ItemCounts[i] = count;
            }
        }
        recipe.TimeSpend = timeSpend;
        ApplyTimeSpend();
    }

}
