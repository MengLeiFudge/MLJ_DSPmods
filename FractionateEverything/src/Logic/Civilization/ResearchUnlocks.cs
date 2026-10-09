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

/// <summary>研究域的分级恢复、残页、实物样本发现与电路板制样入口。</summary>
public static partial class ResearchManager {
    /// <summary>纯消耗入口每件经过时被吃掉的概率，未选中部分保留增产点侧流。</summary>
    private const double AnalysisProcessingRatio = 0.08d;
    /// <summary>残片及塔体被吃掉后产生一份成果的概率，保持平均每份实际消费50件。</summary>
    private const double OfferingYieldRatio = 0.02d;
    /// <summary>蓝至白矩阵平均实际消耗这些件数才能换得一份源点。</summary>
    private static readonly int[] MatrixMemoryBudgets = [100, 60, 40, 25, 15, 10];
    /// <summary>电路板每件经过时成功制取一件样本的概率。</summary>
    private const float PreparationSuccessRatio = 0.01f;
    /// <summary>电路板每件经过时无成果损毁的概率，与制样成功互斥。</summary>
    private const float PreparationDestroyRatio = 0.03f;
    /// <summary>研究锁内登记、主线程在锁外入库的原型奖励。</summary>
    private static readonly Queue<int> PendingPrototypeRewards = [];
    /// <summary>按唯一实物样本建立的稀有配方目录，用于无线索样本的首次研究。</summary>
    private static readonly Dictionary<int, WorldviewRecipe[]> SampleRecipes = [];
    /// <summary>电路板可制样的置换配方目录；阶段和完成状态在使用时检查。</summary>
    private static WorldviewRecipe[] preparationRecipes = [];

    /// <summary>判断内容是否走共享样本槽；普通和传说配方使用各自的恢复入口。</summary>
    /// <param name="contentId">配方、特质或阶段身份。</param>
    /// <returns>是否允许样本解析。</returns>
    private static bool IsSampleContent(string contentId) {
        WorldviewRecipe recipe = RecipeManager.GetRecipe(contentId);
        return recipe == null ? !IsRecoveredPageId(contentId) : recipe.UnlockTier == RecipeUnlockTier.Rare;
    }

    /// <summary>建立百科残页、实物样本和置换制样目录，不改变任何游玩进度。</summary>
    private static void BuildUnlockCatalog() {
        for (int i = 1; i <= 3; i++) {
            AddContentDefinition(new ResearchContentDefinition($"encyclopedia:{i}", "FE.Encyclopedia.Title".Translate(), 0));
        }
        SampleRecipes.Clear();
        foreach (var group in RecipeManager.WorldviewRecipes
                     .Where(recipe => recipe.Kind != WorldviewRecipeKind.BasicAnalysis
                         && recipe.UnlockTier == RecipeUnlockTier.Rare && recipe.AnalysisItemId > 0)
                     .GroupBy(recipe => recipe.AnalysisItemId)) {
            SampleRecipes.Add(group.Key, group.OrderBy(recipe => recipe.PersistentId).ToArray());
        }
        preparationRecipes = RecipeManager.WorldviewRecipes
            .Where(recipe => recipe.Kind == WorldviewRecipeKind.Replacement)
            .OrderBy(recipe => recipe.PersistentId).ToArray();
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

    /// <summary>无分配统计当前阶段未恢复的配方，研究发现、制样及窗口共用。</summary>
    /// <param name="recipes">固定配方目录，调用方不得修改。</param>
    /// <param name="snapshot">本次判断使用的不可变研究快照。</param>
    /// <param name="stage">已经取得资格的矩阵阶段。</param>
    /// <param name="sampleId">0统计全部，非0只统计指定样本。</param>
    /// <returns>符合阶段、完成状态及样本条件的候选数。</returns>
    private static int CountPendingRecipes(WorldviewRecipe[] recipes, AnalysisRuntimeSnapshot snapshot,
        int stage, int sampleId = 0) {
        int count = 0;
        foreach (WorldviewRecipe recipe in recipes) {
            if (recipe.ResearchStage <= stage && !snapshot.UnlockedContents.Contains(recipe.StableId)
                && (sampleId == 0 || recipe.AnalysisItemId == sampleId)) { count++; }
        }
        return count;
    }

    /// <summary>判断样本是否有已知研究，或有当前阶段可由首次成功建立的稀有线索。</summary>
    /// <param name="itemId">实物样本身份。</param>
    /// <param name="snapshot">本次查询使用的研究快照。</param>
    /// <returns>是否允许抽取研究成功和损毁结果。</returns>
    private static bool CanResearchSample(int itemId, AnalysisRuntimeSnapshot snapshot) =>
        snapshot.CandidatesByItemId.ContainsKey(itemId)
        || SampleRecipes.TryGetValue(itemId, out WorldviewRecipe[] recipes)
            && CountPendingRecipes(recipes, snapshot, CurrentMatrixStage) > 0;

    /// <summary>在研究锁内为无线索样本登记一项当前阶段的稀有内容，不自动发现特质。</summary>
    /// <param name="itemId">本次已经抽到研究成功的样本。</param>
    /// <param name="seed">该塔随机种子，候选等概率选择。</param>
    /// <returns>是否登记了一项尚未知晓的线索。</returns>
    private static bool DiscoverSampleUnsafe(int itemId, ref uint seed) {
        if (!SampleRecipes.TryGetValue(itemId, out WorldviewRecipe[] recipes)) { return false; }
        int stage = CurrentMatrixStage;
        AnalysisRuntimeSnapshot snapshot = Volatile.Read(ref analysisRuntimeSnapshot);
        int count = CountPendingRecipes(recipes, snapshot, stage);
        if (count == 0) { return false; }
        int index = Math.Min(count - 1, (int)(GetRandDouble(ref seed) * count));
        foreach (WorldviewRecipe recipe in recipes) {
            if (recipe.ResearchStage > stage || snapshot.UnlockedContents.Contains(recipe.StableId)) { continue; }
            if (index-- == 0) { return DiscoverContent(recipe.StableId, itemId); }
        }
        return false;
    }

    /// <summary>查询红矩阵资格起可用的置换制样候选，不公开未知配方身份。</summary>
    /// <param name="sampleId">0统计全部，非0只统计指定实物样本。</param>
    /// <returns>当前可制取的候选数，全部恢复后为0。</returns>
    public static int GetPreparationCount(int sampleId = 0) {
        int stage = CurrentMatrixStage;
        return stage < 2 ? 0 : CountPendingRecipes(preparationRecipes,
            Volatile.Read(ref analysisRuntimeSnapshot), stage, sampleId);
    }

    /// <summary>电路板按互斥成功与损毁结果制样，同批样本共用一次等概率候选选择。</summary>
    /// <param name="count">本批经过的电路板件数。</param>
    /// <param name="seed">该塔随机种子。</param>
    /// <returns>实际消费和实体样本；未处理部分由运行器保留增产点侧流。</returns>
    private static AnalysisBatchResult ProcessPreparationBatch(int count, ref uint seed) {
        lock (Sync) {
            int candidates = GetPreparationCount();
            if (candidates == 0) { return default; }
            var (successCount, destroyedCount) = RollAnalysisCounts(count,
                PreparationSuccessRatio, PreparationDestroyRatio, ref seed);
            if (successCount == 0) { return new(destroyedCount); }
            int index = Math.Min(candidates - 1, (int)(GetRandDouble(ref seed) * candidates));
            int stage = CurrentMatrixStage;
            foreach (WorldviewRecipe recipe in preparationRecipes) {
                if (recipe.ResearchStage > stage || UnlockedContents.Contains(recipe.StableId)) { continue; }
                if (index-- != 0) { continue; }
                DiscoverContent(recipe.StableId, recipe.AnalysisItemId);
                return new(successCount + destroyedCount, successCount, recipe.AnalysisItemId, successCount);
            }
            return default;
        }
    }

    /// <summary>读取指定矩阵被吃掉后换得一份源点的概率，运行与窗口使用同一档位表。</summary>
    /// <param name="itemId">蓝至白六种主线矩阵之一。</param>
    /// <returns>处理后的源点产率，非主线矩阵为0。</returns>
    public static float GetMatrixMemoryRatio(int itemId) {
        int stage = Array.IndexOf(StageMatrices, itemId);
        return stage < 0 ? 0f : 1f / MatrixMemoryBudgets[stage];
    }

    /// <summary>返回解析入口全部潜在实体产物，目录不随研究或临时模式改变。</summary>
    /// <param name="itemId">输入物品身份。</param>
    /// <returns>去重的样本返还、制样或源点身份，供稳定缓存架构使用。</returns>
    public static int[] GetAnalysisProductIds(int itemId) {
        HashSet<int> ids = [];
        bool tower = FractionatorTowerCatalog.IsAdvancedTower(itemId);
        if (SampleRecipes.ContainsKey(itemId) || tower) { ids.Add(itemId); }
        if (tower || Array.IndexOf(StageMatrices, itemId) >= 0) { ids.Add(IFE记忆源点); }
        if (itemId == I电路板) {
            foreach (WorldviewRecipe recipe in preparationRecipes) { ids.Add(recipe.AnalysisItemId); }
        }
        return ids.OrderBy(id => id).ToArray();
    }
}
