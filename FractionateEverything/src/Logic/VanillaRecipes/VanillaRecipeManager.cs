using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Progression;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.VanillaRecipes;

/// <summary>
/// 原版配方调节系统的注册、查找和存档入口。
/// </summary>
public static class VanillaRecipeManager {
    public const int MaxTimeLimitLevel = 16;
    private static readonly List<VanillaRecipe> VanillaRecipeList = [];
    private static readonly Dictionary<int, VanillaRecipe> VanillaRecipeDic = [];

    /// <summary>
    /// 添加原版配方调节项。
    /// </summary>
    public static void AddVanillaRecipes() {
        LogInfo("Add vanilla recipes...");
        VanillaRecipeList.Clear();
        VanillaRecipeDic.Clear();
        foreach (RecipeProto recipe in LDB.recipes.dataArray) {
            if (recipe == null || recipe.Type == ERecipeType.Fractionate) {
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

    public static int GlobalTimeLimitLevel => GetMaxTimeLimitLevelByCurrentStack();

    public static double GlobalTimeLimitRatio => StackingManager.CurrentVanillaRecipeTimeRatio;

    public static bool CanUpgradeGlobalTimeLimit() => false;

    public static bool UpgradeGlobalTimeLimit() => false;

    public static double GetTimeRatioForLevel(int level) {
        return level <= 0 ? 1.0 : Math.Max(0.2, 1.0 - Math.Min(MaxTimeLimitLevel, level) * 0.05);
    }

    public static int GetRequiredStackForTimeLevel(int level) {
        double ratio = GetTimeRatioForLevel(level);
        return Math.Max(StackingManager.BaseUnlockedMaxStack, (int)Math.Ceiling(4.0 / ratio));
    }

    public static int GetMaxTimeLimitLevelByCurrentStack() {
        double ratio = StackingManager.CurrentVanillaRecipeTimeRatio;
        return Math.Max(0, Math.Min(MaxTimeLimitLevel, (int)Math.Round((1.0 - ratio) / 0.05)));
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

    public static void Import(BinaryReader r) {
        int count = r.ReadInt32();
        int maxImportedTimeLevel = 0;
        for (int i = 0; i < count; i++) {
            int recipeID = r.ReadInt32();
            VanillaRecipe vanillaRecipe = GetVanillaRecipe(recipeID);
            r.ReadBlocks(
                ("VanillaData", br => {
                    vanillaRecipe?.Import(br);
                    if (vanillaRecipe != null) {
                        maxImportedTimeLevel = Math.Max(maxImportedTimeLevel, vanillaRecipe.GetTimeUpgradeCount());
                    }
                })
            );
        }

        bool globalLimitLoaded = false;
        if (r.BaseStream.Position < r.BaseStream.Length) {
            r.ReadBlocks(
                ("GlobalTimeLimitLevel", br => {
                    br.ReadInt32();
                    globalLimitLoaded = true;
                })
            );
        }

        if (!globalLimitLoaded && maxImportedTimeLevel > 0) {
            LogInfo($"Ignored legacy vanilla recipe time level {maxImportedTimeLevel}; now synced by stack.");
        }

        ClampRecipeTimeLevels();
    }

    public static void Export(BinaryWriter w) {
        w.Write(VanillaRecipeList.Count);
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            w.Write(vanillaRecipe.recipe.ID);
            w.WriteBlocks(
                ("VanillaData", vanillaRecipe.Export)
            );
        }
        w.WriteBlocks(
            ("GlobalTimeLimitLevel", bw => bw.Write(GlobalTimeLimitLevel))
        );
    }

    public static void IntoOtherSave() {
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            vanillaRecipe.IntoOtherSave();
        }
        RefreshRecipeExecuteData();
    }

    public static void SyncRuntimeStateAfterImport() {
        ClampRecipeTimeLevels();
        RefreshRecipeExecuteData();
    }

    internal static void ClampGlobalTimeLimitByStack() { }

    private static void ClampRecipeTimeLevels() {
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            vanillaRecipe.ClampTimeUpgradeToGlobalLimit();
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
/// 单个原版配方的升级状态。
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

    /// <summary>
    /// 返回指定物品的索引、当前配方所需数目、升级后配方所需数目。
    /// 原版配方增强不再修改输入数量，第三项仅为兼容旧 UI 调用保留。
    /// </summary>
    public int[] GetIdxCurrAndNextCount(int itemID) {
        for (int i = 0; i < recipe.Items.Length; i++) {
            if (recipe.Items[i] == itemID) {
                int currCount = recipe.ItemCounts[i];
                return [i, currCount, currCount];
            }
        }
        return [-1, -1, -1];
    }

    /// <summary>
    /// 返回能否升级配方的指定输入
    /// </summary>
    public bool CanUpgradeInput(int itemID) {
        return false;
    }

    /// <summary>
    /// 升级配方的指定输入
    /// </summary>
    public bool UpgradeInput(int itemID) {
        return false;
    }

    /// <summary>
    /// 返回当前配方的花费时间、升级后配方的花费时间
    /// </summary>
    public int[] GetCurrAndNextTimeSpend() {
        int currTimeSpend = recipe.TimeSpend;
        int nextTimeSpend = GetTimeSpendByUpgrade(VanillaRecipeManager.GlobalTimeLimitLevel);
        return [currTimeSpend, nextTimeSpend];
    }

    /// <summary>
    /// 返回能否升级配方的花费时间
    /// </summary>
    public bool CanUpgradeTime() {
        return false;
    }

    /// <summary>
    /// 升级配方的花费时间
    /// </summary>
    public bool UpgradeTime() {
        return false;
    }

    /// <summary>
    /// 获取指定物品的升级次数
    /// </summary>
    public int GetInputUpgradeCount(int itemID) {
        return 0;
    }

    /// <summary>
    /// 获取时间的升级次数
    /// </summary>
    public int GetTimeUpgradeCount() {
        return VanillaRecipeManager.GlobalTimeLimitLevel;
    }

    public int GetTimeSpendByUpgrade(int level) {
        double ratio = level == VanillaRecipeManager.GlobalTimeLimitLevel
            ? VanillaRecipeManager.GlobalTimeLimitRatio
            : VanillaRecipeManager.GetTimeRatioForLevel(level);
        return Math.Max(1, (int)Math.Ceiling(timeSpend * ratio));
    }

    public void ClampTimeUpgradeToGlobalLimit() {
        ApplyTimeSpend();
    }

    private void ApplyTimeSpend() {
        recipe.TimeSpend = GetTimeSpendByUpgrade(VanillaRecipeManager.GlobalTimeLimitLevel);
    }

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        r.ReadBlocks(
            ("InputUpgrades", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    br.ReadInt32();
                    br.ReadInt32();
                }
            }),
            ("TimeUpgrades", br => {
                br.ReadInt32();
                ApplyTimeSpend();
            })
        );
    }

    public virtual void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("InputUpgrades", bw => {
                bw.Write(0);
            }),
            ("TimeUpgrades", bw => { bw.Write(VanillaRecipeManager.GlobalTimeLimitLevel); })
        );
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

    #endregion
}
