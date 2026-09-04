using System.Collections.Generic;
using System.IO;
using System.Threading;
using System;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

#region 文明货币打包器

/// <summary>
/// 将 Frag/Memory 基础数量贪心打包为 1000/100/10/1 实体。
/// </summary>
public static class CivilizationCurrencyPacker {
    private static readonly int[] FragDenominations = [1000, 100, 10, 1];
    private static readonly int[] FragItemIds = [IFE残片1000, IFE残片100, IFE残片10, IFE残片];
    private static readonly int[] MemoryDenominations = [1000, 100, 10, 1];
    private static readonly int[] MemoryItemIds = [IFE记忆源点1000, IFE记忆源点100, IFE记忆源点10, IFE记忆源点];

    /// <summary>
    /// 将基础 Frag 数量打包写入输出缓冲。
    /// </summary>
    public static void PackFrag(long amount, ProductOutputBuffer outputs, bool isMainOutput = true) {
        Pack(amount, FragDenominations, FragItemIds, outputs, isMainOutput);
    }

    /// <summary>
    /// 将基础 Memory 数量打包写入输出缓冲。
    /// </summary>
    public static void PackMemory(long amount, ProductOutputBuffer outputs, bool isMainOutput = false) {
        Pack(amount, MemoryDenominations, MemoryItemIds, outputs, isMainOutput);
    }

    /// <summary>
    /// 将基础 Frag 数量打包为产物列表。
    /// </summary>
    public static void PackFrag(long amount, List<ProductOutputInfo> outputs, bool isMainOutput = true) {
        Pack(amount, FragDenominations, FragItemIds, outputs, isMainOutput);
    }

    /// <summary>
    /// 将基础 Memory 数量打包为产物列表。
    /// </summary>
    public static void PackMemory(long amount, List<ProductOutputInfo> outputs, bool isMainOutput = false) {
        Pack(amount, MemoryDenominations, MemoryItemIds, outputs, isMainOutput);
    }

    /// <summary>
    /// 按面额从大到小分解数量；单槽数量超过 int 上限时拆成多条。
    /// </summary>
    private static void Pack(long amount, int[] denominations, int[] itemIds, ProductOutputBuffer outputs,
        bool isMainOutput) {
        if (amount <= 0 || outputs == null) {
            return;
        }

        for (int i = 0; i < denominations.Length; i++) {
            long pieceCount = amount / denominations[i];
            amount %= denominations[i];
            AppendCount(outputs, isMainOutput, itemIds[i], pieceCount);
        }
    }

    private static void Pack(long amount, int[] denominations, int[] itemIds, List<ProductOutputInfo> outputs,
        bool isMainOutput) {
        if (amount <= 0 || outputs == null) {
            return;
        }

        for (int i = 0; i < denominations.Length; i++) {
            long pieceCount = amount / denominations[i];
            amount %= denominations[i];
            AppendCount(outputs, isMainOutput, itemIds[i], pieceCount);
        }
    }

    private static void AppendCount(ProductOutputBuffer outputs, bool isMainOutput, int itemId, long count) {
        while (count > 0) {
            int chunk = count > int.MaxValue ? int.MaxValue : (int)count;
            outputs.Add(isMainOutput, itemId, chunk);
            count -= chunk;
        }
    }

    private static void AppendCount(List<ProductOutputInfo> outputs, bool isMainOutput, int itemId, long count) {
        while (count > 0) {
            int chunk = count > int.MaxValue ? int.MaxValue : (int)count;
            outputs.Add(new ProductOutputInfo(isMainOutput, itemId, chunk));
            count -= chunk;
        }
    }
}

#endregion

#region 记忆余数存储

/// <summary>
/// 保存解析产线共享的 Memory 余数，并保证批量与逐次累计等价。
/// </summary>
public static class MemoryResidueStore {
    public const int FragPerMemory = 2000;

    private static readonly object Sync = new();
    private static long residue;

    /// <summary>
    /// 获取当前余数，范围始终归一到 0..1999。
    /// </summary>
    public static int Residue {
        get {
            lock (Sync) {
                return (int)residue;
            }
        }
    }

    /// <summary>
    /// 累计本次产出的 Frag，并按 2000:1 确定性兑换 Memory。
    /// 使用商余分解，可精确处理 <see cref="long.MaxValue"/> 且不丢 Frag。
    /// </summary>
    /// <param name="fragAmount">本次成功结算产生的 Frag 总量。</param>
    /// <returns>本次应输出的 Memory 基础数量。</returns>
    public static long AddFragAndTakeMemory(long fragAmount) {
        if (fragAmount <= 0) {
            return 0;
        }

        lock (Sync) {
            // residue ∈ [0, 1999]，fragAmount 可至 long.MaxValue。
            // (residue + fragAmount) 可能溢出，故拆成：
            //   memory = fragAmount/2000 + (residue + fragAmount%2000)/2000
            //   residue' = (residue + fragAmount%2000)%2000
            long fragQuotient = fragAmount / FragPerMemory;
            long fragRemainder = fragAmount % FragPerMemory;
            long combinedRemainder = residue + fragRemainder;
            long extraMemory = combinedRemainder / FragPerMemory;
            residue = combinedRemainder % FragPerMemory;
            // fragQuotient + extraMemory：extraMemory 至多为 1（因 residue+rem < 4000）
            return fragQuotient + extraMemory;
        }
    }

    /// <summary>
    /// 将余数写入当前配方存档块。
    /// </summary>
    public static void Export(BinaryWriter w) {
        lock (Sync) {
            w.Write((int)residue);
        }
    }

    /// <summary>
    /// 从配方存档块读取余数，并归一到 0..1999。
    /// </summary>
    public static void Import(BinaryReader r) {
        int raw = r.ReadInt32();
        lock (Sync) {
            residue = Normalize(raw);
        }
    }

    /// <summary>
    /// 切档时清零共享余数。
    /// </summary>
    public static void IntoOtherSave() {
        lock (Sync) {
            residue = 0;
        }
    }

    /// <summary>
    /// 将任意整型余数归一到合法半开区间 [0, 2000)。
    /// </summary>
    public static int Normalize(int value) {
        int mod = value % FragPerMemory;
        return mod < 0 ? mod + FragPerMemory : mod;
    }
}

#endregion

#region 黑雾认知缓存

/// <summary>
/// 保存黑雾域投影到分馏热路径的地表/星域认知等级，供解析塔读取。
/// </summary>
public static class DarkFogCognitionCache {
    private static int groundRank;
    private static int spaceRank;

    /// <summary>
    /// 获取地表认知等级，范围 0..4。
    /// </summary>
    public static int GroundRank => Volatile.Read(ref groundRank);

    /// <summary>
    /// 获取星域认知等级，范围 0..4。
    /// </summary>
    public static int SpaceRank => Volatile.Read(ref spaceRank);

    /// <summary>
    /// 重置双轴认知投影。
    /// </summary>
    public static void Reset() {
        Volatile.Write(ref groundRank, 0);
        Volatile.Write(ref spaceRank, 0);
    }

    /// <summary>
    /// 由黑雾域写入最新双轴认知等级；非法值会被夹紧到 0..4。
    /// </summary>
    public static void SetRanks(int ground, int space) {
        Volatile.Write(ref groundRank, ClampRank(ground));
        Volatile.Write(ref spaceRank, ClampRank(space));
    }

    private static int ClampRank(int rank) => Math.Max(0, Math.Min(4, rank));
}

#endregion

#region 配方可用性存储

/// <summary>
/// 保存文明系统投影到分馏域的协议管理范围和当前可用配方。
/// </summary>
public static class RecipeAvailabilityStore {
    private static readonly HashSet<RecipeKey> managedRecipes = [];
    private static readonly HashSet<RecipeKey> availableRecipes = [];

    public static void Reset() {
        managedRecipes.Clear();
        availableRecipes.Clear();
    }

    public static void RegisterManaged(RecipeKey recipeKey, bool available) {
        managedRecipes.Add(recipeKey);
        if (available) {
            availableRecipes.Add(recipeKey);
        }
    }

    public static bool IsManaged(BaseRecipe recipe) => recipe != null && IsManaged(RecipeKey.FromRecipe(recipe));

    public static bool IsManaged(RecipeKey recipeKey) => managedRecipes.Contains(recipeKey);

    public static bool IsAvailable(BaseRecipe recipe) => recipe != null && IsAvailable(RecipeKey.FromRecipe(recipe));

    public static bool IsAvailable(RecipeKey recipeKey) =>
        !managedRecipes.Contains(recipeKey) || availableRecipes.Contains(recipeKey);
}

#endregion

#region 配方修正缓存

/// <summary>
/// 保存成就域投影到分馏热路径的固定配方加成。
/// </summary>
public static class RecipeModifierCache {
    private static readonly Dictionary<ERecipe, float> successRateBonusByType = [];
    private static float allRecipeSuccessRateBonus;

    public static void Reset() {
        successRateBonusByType.Clear();
        allRecipeSuccessRateBonus = 0f;
    }

    public static void AddSuccessRateBonus(ERecipe recipeType, float bonus) {
        if (bonus <= 0f) {
            return;
        }
        successRateBonusByType.TryGetValue(recipeType, out float current);
        successRateBonusByType[recipeType] = current + bonus;
    }

    public static void AddAllRecipeSuccessRateBonus(float bonus) {
        if (bonus > 0f) {
            allRecipeSuccessRateBonus += bonus;
        }
    }

    public static float GetSuccessRateBonus(BaseRecipe recipe) {
        if (recipe == null) {
            return allRecipeSuccessRateBonus;
        }
        successRateBonusByType.TryGetValue(recipe.RecipeType, out float typeBonus);
        return allRecipeSuccessRateBonus + typeBonus;
    }
}

#endregion
