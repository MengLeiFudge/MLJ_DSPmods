using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>研究域的分级恢复、残页和低效样本分解入口。</summary>
public static partial class ResearchManager {
    /// <summary>矩阵过量献祭的逐件源点产率，不受矩阵档位或增产点影响。</summary>
    public const float MatrixMemoryRatio = 0.002f;
    /// <summary>分解稀有配方主产物时取得实物样本的逐件概率。</summary>
    private const float DisassemblyRatio = 0.01f;
    /// <summary>电路板制样的逐件概率，供早期低效自动化使用。</summary>
    private const float PreparationRatio = 0.001f;
    /// <summary>研究锁内登记、主线程在锁外入库的原型奖励。</summary>
    private static readonly Queue<int> PendingPrototypeRewards = [];
    /// <summary>主产物分解和电路板制样的静态索引；不混入基础解析入口。</summary>
    private static readonly Dictionary<int, WorldviewRecipe[]> DisassemblyRecipes = [];

    /// <summary>判断内容是否走共享样本槽；普通和传说配方使用各自的恢复入口。</summary>
    /// <param name="contentId">配方、特质或阶段身份。</param>
    /// <returns>是否允许样本解析。</returns>
    private static bool IsSampleContent(string contentId) {
        WorldviewRecipe recipe = RecipeManager.GetRecipe(contentId);
        return recipe == null ? !IsRecoveredPageId(contentId) : recipe.UnlockTier == RecipeUnlockTier.Rare;
    }

    /// <summary>建立残页身份和可分解产物目录，不改变任何游玩进度。</summary>
    private static void BuildUnlockCatalog() {
        for (int i = 1; i <= 3; i++) {
            AddContentDefinition(new ResearchContentDefinition($"encyclopedia:{i}", "FE.Encyclopedia.Title".Translate(), 0));
        }
        DisassemblyRecipes.Clear();
        foreach (var group in RecipeManager.WorldviewRecipes
                     .Where(recipe => recipe.Kind != WorldviewRecipeKind.BasicAnalysis
                         && recipe.UnlockTier == RecipeUnlockTier.Rare && recipe.PrimaryProductId > 0)
                     .GroupBy(recipe => recipe.PrimaryProductId)) {
            DisassemblyRecipes.Add(group.Key, group.OrderBy(recipe => recipe.PersistentId).ToArray());
        }
        DisassemblyRecipes.Add(I电路板, RecipeManager.WorldviewRecipes
            .Where(recipe => recipe.Kind == WorldviewRecipeKind.Replacement)
            .OrderBy(recipe => recipe.PersistentId).ToArray());
    }

    /// <summary>按已经完成的矩阵科技恢复普通配方；培育还等待第一枚矩阵实际生产。</summary>
    private static void RefreshMatrixRecipes() {
        if (!ExplorationManager.FirstRelicClaimed) { return; }
        bool produced = ExplorationManager.HasProducedFirstMatrix();
        int stage = CurrentMatrixStage;
        lock (Sync) {
            foreach (WorldviewRecipe recipe in RecipeManager.WorldviewRecipes) {
                if (recipe.UnlockTier != RecipeUnlockTier.Common || UnlockedContents.Contains(recipe.StableId)
                    || stage < Math.Max(1, recipe.ResearchStage)
                    || recipe.Kind == WorldviewRecipeKind.TowerCultivation && !produced) { continue; }
                // 普通恢复不发高级原型；第一批交互塔仍由矩阵主线提供。
                UnlockWithoutReward(recipe.StableId);
                QueueNotification(string.Format("FE.Research.CommonRecovered".Translate(), recipe.DisplayName));
                snapshotDirty = true;
            }
        }
    }

    /// <summary>识别百科残页的固定内容身份，旧记录和联机提示共用。</summary>
    /// <param name="contentId">待检查身份。</param>
    /// <returns>是否为三份百科残页之一。</returns>
    public static bool IsRecoveredPageId(string contentId) => contentId is "encyclopedia:1" or "encyclopedia:2" or "encyclopedia:3";

    /// <summary>从高级地点恢复一份百科残页，补全后恢复当前阶段的一项传说配方。</summary>
    /// <param name="seed">探索随机种子。</param>
    /// <param name="contentId">本次实际恢复的稳定身份，写入回收档案。</param>
    /// <returns>本次是否取得残页或传说内容。</returns>
    public static bool TryRecoverPage(ref uint seed, out string contentId) {
        contentId = null;
        if (NebulaMultiplayerModAPI.IsClient) { return false; }
        int towerId = 0;
        lock (Sync) {
            for (int i = 1; i <= 3; i++) {
                string id = $"encyclopedia:{i}";
                if (UnlockedContents.Contains(id)) { continue; }
                UnlockWithoutReward(id);
                snapshotDirty = true;
                contentId = id;
                return true;
            }
            WorldviewRecipe[] recipes = GetPendingLegendaryRecipesUnsafe();
            if (recipes.Length == 0) { return false; }
            int index = Math.Min(recipes.Length - 1, (int)(GetRandDouble(ref seed) * recipes.Length));
            contentId = recipes[index].StableId;
            towerId = UnlockContent(contentId);
        }
        if (towerId > 0) { DataCenterInventory.AddExplorationReward(towerId, 1); }
        return true;
    }

    /// <summary>读取研究锁内当前阶段尚未恢复的传说配方，顺序固定。</summary>
    /// <returns>可由残页或完整分钟献祭恢复的配方。</returns>
    private static WorldviewRecipe[] GetPendingLegendaryRecipesUnsafe() => RecipeManager.WorldviewRecipes
        .Where(recipe => recipe.UnlockTier == RecipeUnlockTier.Legendary
            && recipe.ResearchStage <= CurrentMatrixStage && !UnlockedContents.Contains(recipe.StableId))
        .OrderBy(recipe => recipe.PersistentId).ToArray();

    /// <summary>完整分钟献祭达到门槛时恢复一项传说，原型待锁外发放。</summary>
    private static void RecoverOfferingLegendaryUnsafe() {
        WorldviewRecipe recipe = GetPendingLegendaryRecipesUnsafe().FirstOrDefault();
        if (recipe == null) { return; }
        int towerId = UnlockContent(recipe.StableId);
        if (towerId > 0) { PendingPrototypeRewards.Enqueue(towerId); }
    }

    /// <summary>读取静态分解目录和不可变研究快照，不创建候选数组。</summary>
    /// <param name="itemId">投入的配方主产物或电路板。</param>
    /// <param name="recipes">静态目录，调用方不得修改。</param>
    /// <param name="snapshot">本次判断使用的研究快照。</param>
    /// <returns>是否允许检查分解候选；已知研究和塔体献祭优先。</returns>
    private static bool TryGetDisassemblyCatalog(int itemId, out WorldviewRecipe[] recipes,
        out AnalysisRuntimeSnapshot snapshot) {
        snapshot = Volatile.Read(ref analysisRuntimeSnapshot);
        recipes = null;
        return !snapshot.CandidatesByItemId.ContainsKey(itemId)
            && !(SacrificeUnlocked && FractionatorTowerCatalog.IsAdvancedTower(itemId))
            && DisassemblyRecipes.TryGetValue(itemId, out recipes);
    }

    /// <summary>无分配统计当前阶段尚未恢复的候选，生产结算与窗口共用。</summary>
    /// <param name="recipes">静态分解目录。</param>
    /// <param name="snapshot">本次判断使用的研究快照。</param>
    /// <param name="stage">已经取得资格的矩阵阶段。</param>
    /// <param name="sampleId">0统计全部，非0只统计指定样本。</param>
    /// <returns>符合条件的候选数。</returns>
    private static int CountDisassemblyRecipes(WorldviewRecipe[] recipes, AnalysisRuntimeSnapshot snapshot,
        int stage, int sampleId = 0) {
        int count = 0;
        foreach (WorldviewRecipe recipe in recipes) {
            if (recipe.ResearchStage <= stage && !snapshot.UnlockedContents.Contains(recipe.StableId)
                && (sampleId == 0 || recipe.AnalysisItemId == sampleId)) { count++; }
        }
        return count;
    }

    /// <summary>无锁、无候选数组分配地查询分解数量；不公开未知配方身份。</summary>
    /// <param name="itemId">当前投入物品。</param>
    /// <param name="sampleId">0统计全部，非0只统计指定实物样本。</param>
    /// <returns>当前可分解的候选数。</returns>
    public static int GetDisassemblyCount(int itemId, int sampleId = 0) =>
        TryGetDisassemblyCatalog(itemId, out WorldviewRecipe[] recipes, out AnalysisRuntimeSnapshot snapshot)
            ? CountDisassemblyRecipes(recipes, snapshot, CurrentMatrixStage, sampleId) : 0;

    /// <summary>取得该输入的实物样本产率，运行态和窗口共用。</summary>
    /// <param name="itemId">当前投入物品。</param>
    /// <returns>每件投入取得样本的概率。</returns>
    public static float GetDisassemblyRatio(int itemId) => itemId == I电路板 ? PreparationRatio : DisassemblyRatio;

    /// <summary>分解产物或以电路板制样，仅发现新线索时更新研究快照。</summary>
    /// <param name="itemId">投入物品。</param>
    /// <param name="count">本批实际分解件数。</param>
    /// <param name="seed">该塔随机种子。</param>
    /// <param name="sample">本批实物样本及数量，未成功时数量为零。</param>
    /// <returns>是否使用了分解路径；true时整批投入均消耗。</returns>
    public static bool TryDisassemble(int itemId, int count, ref uint seed, out (int ItemId, int Count) sample) {
        sample = default;
        if (NebulaMultiplayerModAPI.IsClient || count <= 0 || GetDisassemblyCount(itemId) == 0) { return false; }
        lock (Sync) {
            // 锁外排除无用途输入，锁内重新读取快照，避免并发研究完成后继续消耗。
            if (!TryGetDisassemblyCatalog(itemId, out WorldviewRecipe[] recipes, out AnalysisRuntimeSnapshot snapshot)) { return false; }
            int stage = CurrentMatrixStage;
            int candidates = CountDisassemblyRecipes(recipes, snapshot, stage);
            if (candidates == 0) { return false; }
            int index = Math.Min(candidates - 1, (int)(GetRandDouble(ref seed) * candidates));
            foreach (WorldviewRecipe recipe in recipes) {
                if (recipe.ResearchStage > stage || snapshot.UnlockedContents.Contains(recipe.StableId)) { continue; }
                if (index-- != 0) { continue; }
                int output = WorldviewRecipe.RollExpected(ref seed, count * (double)GetDisassemblyRatio(itemId));
                if (output > 0) { DiscoverContent(recipe.StableId, recipe.AnalysisItemId); }
                sample = (recipe.AnalysisItemId, output);
                return true;
            }
            return false;
        }
    }

    /// <summary>返回基础解析入口必须保留的全部潜在实物产物，以便缓存架构保持稳定。</summary>
    /// <param name="itemId">输入物品身份。</param>
    /// <returns>去重的实物产物身份，不依据当前临时模式删减缓存。</returns>
    public static int[] GetAnalysisProductIds(int itemId) {
        HashSet<int> ids = [];
        if (FractionatorTowerCatalog.IsAdvancedTower(itemId) || Array.IndexOf(StageMatrices, itemId) >= 0) {
            ids.Add(IFE记忆源点);
        }
        if (DisassemblyRecipes.TryGetValue(itemId, out WorldviewRecipe[] recipes)) {
            foreach (WorldviewRecipe recipe in recipes) { ids.Add(recipe.AnalysisItemId); }
        }
        return ids.OrderBy(id => id).ToArray();
    }
}
