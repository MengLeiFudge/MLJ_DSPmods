using System;
using System.Collections.Generic;
using System.IO;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 原版配方升级信息
/// </summary>
public class VanillaRecipe {
    private static readonly int[] UpgradeLimitTechIds = [T电磁矩阵, T能量矩阵, T结构矩阵, T信息矩阵, T引力矩阵];
    private readonly Dictionary<int, int> inputCounts = [];
    public readonly RecipeProto recipe;
    private readonly int timeSpend;
    private readonly Dictionary<int, int> inputUpgrades = [];
    private int timeSpendUpgrade = 0;

    public VanillaRecipe(RecipeProto recipe) {
        this.recipe = recipe;
        for (int i = 0; i < recipe.Items.Length; i++) {
            inputCounts.Add(recipe.Items[i], recipe.ItemCounts[i]);
        }
        timeSpend = recipe.TimeSpend;
    }

    public bool LimitedByMatrix => !CanUpgradeMore();

    private static int GetUpgradeLimit() {
        if (GameMain.history == null) {
            return 0;
        }
        if (GameMain.history.TechUnlocked(T宇宙矩阵)) {
            return int.MaxValue;
        }
        int count = 0;
        for (int i = 0; i < UpgradeLimitTechIds.Length; i++) {
            if (GameMain.history.TechUnlocked(UpgradeLimitTechIds[i])) {
                count++;
            }
        }
        return count;
    }

    private int GetTotalUpgradeCount() {
        int count = timeSpendUpgrade;
        foreach (int upgrade in inputUpgrades.Values) {
            count += upgrade;
        }
        return count;
    }

    private bool CanUpgradeMore() {
        int limit = GetUpgradeLimit();
        return limit == int.MaxValue || GetTotalUpgradeCount() < limit;
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
        int version = r.ReadInt32();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int itemID = r.ReadInt32();
            int upgrade = r.ReadInt32();
            for (int j = 0; j < upgrade; j++) {
                UpgradeInput(itemID);
            }
        }
        int upgrade2 = r.ReadInt32();
        for (int i = 0; i < upgrade2; i++) {
            UpgradeTime();
        }
        RecipeProto.InitRecipeItems();
    }

    public virtual void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(inputUpgrades.Count);
        foreach (var p in inputUpgrades) {
            w.Write(p.Key);
            w.Write(p.Value);
        }
        w.Write(timeSpendUpgrade);
    }

    public virtual void IntoOtherSave() {
        //还原配方
        for (int i = 0; i < recipe.Items.Length; i++) {
            inputCounts.TryGetValue(recipe.Items[i], out int count);
            if (count > 0) {
                recipe.ItemCounts[i] = count;
            }
        }
        recipe.TimeSpend = timeSpend;
        //清空缓存
        inputUpgrades.Clear();
        timeSpendUpgrade = 0;
        RecipeProto.InitRecipeItems();
    }

    #endregion
}
