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

    public static void Import(BinaryReader r) {
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int recipeID = r.ReadInt32();
            VanillaRecipe vanillaRecipe = GetVanillaRecipe(recipeID);
            r.ReadBlocks(
                ("VanillaData", br => vanillaRecipe?.Import(br))
            );
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(VanillaRecipeList.Count);
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            w.Write(vanillaRecipe.recipe.ID);
            w.WriteBlocks(
                ("VanillaData", vanillaRecipe.Export)
            );
        }
    }

    public static void IntoOtherSave() {
        foreach (VanillaRecipe vanillaRecipe in VanillaRecipeList) {
            vanillaRecipe.IntoOtherSave();
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
    private readonly Dictionary<int, int> inputUpgrades = [];
    private int timeSpendUpgrade = 0;
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

    private bool CanUpgradeMore() {
        return !LimitedByMatrix;
    }

    /// <summary>
    /// 返回指定物品的索引、当前配方所需数目、升级后配方所需数目
    /// </summary>
    public int[] GetIdxCurrAndNextCount(int itemID) {
        for (int i = 0; i < recipe.Items.Length; i++) {
            if (recipe.Items[i] == itemID) {
                //根据原始数据、升级次数计算新值
                inputCounts.TryGetValue(itemID, out int count);
                inputUpgrades.TryGetValue(itemID, out int inputUpgrade);
                int nextCount = (int)Math.Ceiling(count * Math.Pow(0.87, inputUpgrade + 1));
                //新值至少减少到上一次-1
                int currCount = recipe.ItemCounts[i];
                nextCount = Math.Min(currCount - 1, nextCount);
                //新值至多减少到最小值
                int minCount = (int)Math.Ceiling(count * 0.5);
                return [i, currCount, Math.Max(minCount, nextCount)];
            }
        }
        return [-1, -1, -1];
    }

    /// <summary>
    /// 返回能否升级配方的指定输入
    /// </summary>
    public bool CanUpgradeInput(int itemID) {
        if (!CanUpgradeMore()) {
            return false;
        }
        int[] info = GetIdxCurrAndNextCount(itemID);
        return info[0] != -1 && info[1] > info[2];
    }

    /// <summary>
    /// 升级配方的指定输入
    /// </summary>
    public bool UpgradeInput(int itemID) {
        int[] info = GetIdxCurrAndNextCount(itemID);
        if (info[0] == -1 || info[1] <= info[2]) {
            return false;
        }
        inputUpgrades.TryGetValue(itemID, out int currUpgradeCount);
        inputUpgrades[itemID] = currUpgradeCount + 1;
        recipe.ItemCounts[info[0]] = info[2];
        return true;
    }

    /// <summary>
    /// 返回当前配方的花费时间、升级后配方的花费时间
    /// </summary>
    public int[] GetCurrAndNextTimeSpend() {
        //根据原始数据、升级次数计算新值
        int nextTimeSpend = (int)Math.Ceiling(timeSpend * Math.Pow(0.72, timeSpendUpgrade + 1));
        //新值至少减少到上一次-1
        int currTimeSpend = recipe.TimeSpend;
        nextTimeSpend = Math.Min(currTimeSpend - 1, nextTimeSpend);
        //新值至多减少到最小值
        int minTimeSpend = (int)Math.Ceiling(timeSpend * 0.2);
        return [currTimeSpend, Math.Max(minTimeSpend, nextTimeSpend)];
    }

    /// <summary>
    /// 返回能否升级配方的花费时间
    /// </summary>
    public bool CanUpgradeTime() {
        if (!CanUpgradeMore()) {
            return false;
        }
        int[] info = GetCurrAndNextTimeSpend();
        return info[0] > info[1];
    }

    /// <summary>
    /// 升级配方的花费时间
    /// </summary>
    public bool UpgradeTime() {
        int[] info = GetCurrAndNextTimeSpend();
        if (info[0] <= info[1]) {
            return false;
        }
        timeSpendUpgrade++;
        recipe.TimeSpend = info[1];
        return true;
    }

    /// <summary>
    /// 获取指定物品的升级次数
    /// </summary>
    public int GetInputUpgradeCount(int itemID) {
        if (inputUpgrades.TryGetValue(itemID, out int count)) {
            return count;
        }
        return 0;
    }

    /// <summary>
    /// 获取时间的升级次数
    /// </summary>
    public int GetTimeUpgradeCount() {
        return timeSpendUpgrade;
    }

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        r.ReadBlocks(
            ("InputUpgrades", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int itemID = br.ReadInt32();
                    int upgradeCount = br.ReadInt32();
                    for (int j = 0; j < upgradeCount; j++) {
                        UpgradeInput(itemID);
                    }
                }
            }),
            ("TimeUpgrades", br => {
                int upgradeCount = br.ReadInt32();
                for (int i = 0; i < upgradeCount; i++) {
                    UpgradeTime();
                }
            })
        );
        // 读取完成后初始化
        RecipeProto.InitRecipeItems();
    }

    public virtual void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("InputUpgrades", bw => {
                bw.Write(inputUpgrades.Count);
                foreach (var p in inputUpgrades) {
                    bw.Write(p.Key);
                    bw.Write(p.Value);
                }
            }),
            ("TimeUpgrades", bw => { bw.Write(timeSpendUpgrade); })
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
        // 清空缓存
        inputUpgrades.Clear();
        timeSpendUpgrade = 0;
        // 重新初始化
        RecipeProto.InitRecipeItems();
    }

    #endregion
}
