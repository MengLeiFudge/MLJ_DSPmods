using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Enhancements.Stacking;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 定义 FE 科技行，并把已完成等级投影到运行规则。
/// </summary>
public static class GrowthTechnologyManager {
    /// <summary>一条成长科技行共享的显示与布局定义。</summary>
    private sealed class GrowthTechSeries(string nameKey, string descriptionKey, string iconPath, int row) {
        public readonly string NameKey = nameKey;
        public readonly string DescriptionKey = descriptionKey;
        public readonly string IconPath = iconPath;
        public readonly int Row = row;
    }

    /// <summary>单个科技原型承载的等级区间与线性成本增量。</summary>
    private readonly struct GrowthTechLevel(int level, int maxLevel, int linearCostIncrement = 0) {
        public readonly int Level = level;
        public readonly int MaxLevel = maxLevel;
        public readonly int LinearCostIncrement = linearCostIncrement;
    }

    /// <summary>行星亚空间交互科技方向的稳定内容 ID。</summary>
    public const string PlanetaryInteractionTechnologyContentId = "technology:planetary-subspace-interaction";

    /// <summary>星际亚空间交互科技方向的稳定内容 ID。</summary>
    public const string InterstellarInteractionTechnologyContentId = "technology:interstellar-subspace-interaction";

    /// <summary>超级压缩技术方向的稳定内容 ID。</summary>
    public const string StackTechnologyContentId = "technology:super-compression";

    /// <summary>亚空间扩容科技方向的稳定内容 ID。</summary>
    public const string SubspaceCapacityTechnologyContentId = "technology:subspace-capacity";

    /// <summary>亚空间存取能效科技方向的稳定内容 ID。</summary>
    public const string SubspaceEfficiencyTechnologyContentId = "technology:subspace-efficiency";

    /// <summary>探索信号增幅科技方向的稳定内容 ID。</summary>
    public const string ExplorationTechnologyContentId = "technology:exploration-signal";

    private const int FiniteGrowthLevelCount = 6;
    private const int CapacityFiniteLevelCount = 8;
    private const int CapacityInfiniteStartLevel = 9;
    private const int CapacityMaximumLevel = 10000;
    private const int CapacityInfiniteStartCost = 32000;
    private const int CapacityInfiniteCostIncrement = 8000;

    private static readonly int[] MatrixItems = [
        I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵,
    ];
    private static readonly int[] MatrixTechs = [
        T电磁矩阵, T能量矩阵, T结构矩阵, T信息矩阵, T引力矩阵, T宇宙矩阵,
    ];

    // 采用原版“无人机引擎”升级链的六阶段矩阵数量。
    private static readonly int[] FiniteGrowthCosts = [200, 400, 800, 1200, 1600, 50000];
    private static readonly int[] CapacityCosts = [
        600, 1200, 1800, 2400, 3000, 4000, 8000, 16000, CapacityInfiniteStartCost,
    ];
    private static readonly int[] ExperiencedStackCosts = [1200, 2400, 3600, 4800, 6000];
    private static readonly int[] StandardStackCosts = [0, 6000, 8000, 10000, 40000];
    private static readonly int[] ExperiencedStackMatrixStages = [0, 1, 2, 3, 4];
    private static readonly int[] StandardStackMatrixStages = [0, 3, 4, 5, 5];
    private static readonly TechProto[] StackTechs = new TechProto[6];
    private static bool stackProgressionModeApplied;

    /// <summary>当前存档是否采用已玩过分馏的堆叠路线。</summary>
    public static bool UsesExperiencedStackProgression { get; private set; }

    /// <summary>获取亚空间扩容科技的已完成等级，范围 0..10000。</summary>
    public static int SubspaceCapacityLevel => GetCapacityLevel();

    /// <summary>获取亚空间存取能效科技等级，范围 0..6。</summary>
    public static int SubspaceEfficiencyLevel => GetUnlockedLevel(TFE亚空间能效, FiniteGrowthLevelCount);

    /// <summary>获取探索信号成长科技等级，范围 0..6。</summary>
    public static int ExplorationLevel => GetUnlockedLevel(TFE探索信号增幅, FiniteGrowthLevelCount);

    /// <summary>获取探测器显示强度的量化步长，单位为百分点。</summary>
    public static float ExplorationSignalPrecisionPercent => ExplorationLevel switch {
        <= 0 => 20f,
        1 => 10f,
        2 => 5f,
        3 => 2f,
        4 => 1f,
        _ => 0.5f,
    };

    /// <summary>判断玩家是否已经解锁主动位置标注。</summary>
    public static bool ExplorationLocationTrackingUnlocked => ExplorationLevel >= FiniteGrowthLevelCount;

    /// <summary>获取高级矿物塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedMineralTraitLevel => GetUnlockedLevel(TFE高级矿物特质, FiniteGrowthLevelCount);

    /// <summary>获取高级交互塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedInteractionTraitLevel => GetUnlockedLevel(TFE高级交互特质, FiniteGrowthLevelCount);

    /// <summary>获取高级转化塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedConversionTraitLevel => GetUnlockedLevel(TFE高级转化特质, FiniteGrowthLevelCount);

    /// <summary>
    /// 应用当前存档的堆叠成长路线。未玩过分馏时由原版黄糖科技提供 4 堆叠，FE 从 8 开始。
    /// </summary>
    public static void SetStackProgressionMode(bool experienced) {
        if (stackProgressionModeApplied && UsesExperiencedStackProgression == experienced) {
            SyncUnstartedStackTechStates();
            return;
        }

        UsesExperiencedStackProgression = experienced;
        for (int level = 1; level <= 5; level++) {
            TechProto tech = StackTechs[level] ?? LDB.techs.Select(TFE分馏堆叠成长 + level);
            if (tech == null) {
                continue;
            }

            bool active = experienced || level > 1;
            int stage = experienced
                ? ExperiencedStackMatrixStages[level - 1]
                : StandardStackMatrixStages[level - 1];
            int cost = experienced
                ? ExperiencedStackCosts[level - 1]
                : StandardStackCosts[level - 1];
            int previousTech = experienced
                ? level > 1 ? TFE分馏堆叠成长 + level - 1 : 0
                : level > 2 ? TFE分馏堆叠成长 + level - 1 : 0;
            int[] additionalImplicitTechs = !experienced && level == 2 ? [T集装物流系统] : [];

            int techLevel = experienced ? level : Math.Max(1, level - 1);
            ConfigureGrowthTech(tech, techLevel, techLevel, previousTech,
                [MatrixItems[stage]], cost, additionalImplicitTechs);
            tech.IsHiddenTech = true;
            tech.IsObsolete = !active;
        }

        RefreshStackTechLinks();
        stackProgressionModeApplied = true;
        SyncUnstartedStackTechStates();
    }

    /// <summary>
    /// 判断科技节点是否属于当前存档实际使用的路线。
    /// </summary>
    public static bool IsTechnologyNodeActive(int techId) =>
        UsesExperiencedStackProgression || techId != TFE分馏堆叠成长 + 1;

    /// <summary>
    /// 获取科技节点对应的解析内容 ID。
    /// </summary>
    public static bool TryGetTechnologyContentId(int techId, out string contentId) {
        contentId = techId switch {
            TFE行星内物流交互 => PlanetaryInteractionTechnologyContentId,
            TFE星际物流交互 => InterstellarInteractionTechnologyContentId,
            > TFE分馏堆叠成长 and <= TFE分馏堆叠成长 + 5 => StackTechnologyContentId,
            > TFE亚空间扩容 and <= TFE亚空间扩容 + 9 => SubspaceCapacityTechnologyContentId,
            > TFE亚空间能效 and <= TFE亚空间能效 + FiniteGrowthLevelCount =>
                SubspaceEfficiencyTechnologyContentId,
            > TFE探索信号增幅 and <= TFE探索信号增幅 + FiniteGrowthLevelCount =>
                ExplorationTechnologyContentId,
            > TFE高级矿物特质 and <= TFE高级矿物特质 + FiniteGrowthLevelCount =>
                ResearchManager.AdvancedMineralTraitId,
            > TFE高级交互特质 and <= TFE高级交互特质 + FiniteGrowthLevelCount =>
                ResearchManager.AdvancedInteractionTraitId,
            > TFE高级转化特质 and <= TFE高级转化特质 + FiniteGrowthLevelCount =>
                ResearchManager.AdvancedConversionTraitId,
            _ => null,
        };
        return contentId != null;
    }

    /// <summary>
    /// 判断科技是否已经完成探索和解析，且可以显示或加入研究队列。
    /// </summary>
    public static bool IsTechnologyDiscovered(int techId) =>
        IsTechnologyNodeActive(techId)
        && TryGetTechnologyContentId(techId, out string contentId)
        && ResearchManager.IsContentUnlocked(contentId);

    /// <summary>
    /// 注册科技树使用的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("FE分馏堆叠成长", "Super Compression Technology", "超级压缩技术");
        Register("FE分馏堆叠成长描述",
            "Raises global cargo stacking and proportionally shortens every vanilla recipe.",
            "提高全局货物堆叠，并按 4/当前堆叠 等比例缩短全部原版配方耗时。");
        Register("FE亚空间扩容", "Subspace Capacity", "亚空间扩容");
        Register("FE亚空间扩容描述",
            "Adds ten stacks to every item slot in the fractionation subspace.",
            "使分馏亚空间中每种物品的容量增加 10 组。");
        Register("FE亚空间能效", "Subspace Transfer Efficiency", "亚空间存取能效");
        Register("FE亚空间能效描述",
            "Reduces logistics-interaction energy use by 10% per level.",
            "每级使物流交互站存取亚空间的耗能降低 10%。");
        Register("FE探索信号增幅", "Exploration Signal Amplification", "探索信号增幅");
        Register("FE探索信号增幅描述",
            "Improves detector precision, raises Frag discovery chance, and shortens the guaranteed signal interval.",
            "提高探测器显示精度和 Frag 发现概率，并缩短探索信号定时保底间隔；最终级解锁主动位置标注。");
        Register("FE高级矿物特质", "Rare Catalyst Stability", "珍奇催化稳定性");
        Register("FE高级矿物特质描述",
            "Reduces rare-catalyst consumption in every advanced mineral tower.",
            "降低全部高级矿物塔的珍奇催化物损耗。");
        Register("FE高级交互特质", "Embryo Breeding Activity", "原胚繁育活性");
        Register("FE高级交互特质描述",
            "Raises the chance of an extra embryo from advanced interaction towers.",
            "提高全部高级交互塔繁育额外原胚的概率。");
        Register("FE高级转化特质", "Replacement Recovery", "置换回收效率");
        Register("FE高级转化特质描述",
            "Raises recovery output from advanced conversion towers.",
            "提高全部高级转化塔的置换回收量。");
        Register("FE成长科技结果", "Global evolution capability enhanced.", "全局演化能力已增强。");
    }

    /// <summary>
    /// 注册第一页物流交互科技和第二页成长科技链。
    /// </summary>
    public static void AddTechs() {
        AddInfrastructureTechs();
        AddStackTechs();
        AddCapacityTechs();
        AddFiniteSeries(TFE亚空间能效, "FE亚空间能效", "FE亚空间能效描述",
            "Assets/fe/tech分馏数据中心", 6);
        AddFiniteSeries(TFE探索信号增幅, "FE探索信号增幅", "FE探索信号增幅描述",
            "Assets/fe/tech物品分解", 2);
        AddFiniteSeries(TFE高级矿物特质, "FE高级矿物特质", "FE高级矿物特质描述",
            "Assets/fe/tech资源复制", 3);
        AddFiniteSeries(TFE高级交互特质, "FE高级交互特质", "FE高级交互特质描述",
            "Assets/fe/tech物品交互", 4);
        AddFiniteSeries(TFE高级转化特质, "FE高级转化特质", "FE高级转化特质描述",
            "Assets/fe/tech物品转化", 5);
    }

    /// <summary>
    /// 将旧版 Level=0 的独立科技状态迁移为原版升级链状态。
    /// </summary>
    public static void MigrateLegacyTechnologyStates() {
        GameHistoryData history = GameMain.history;
        if (history == null) {
            return;
        }

        foreach (int techId in GetGrowthTechIds()) {
            TechProto tech = LDB.techs.Select(techId);
            if (tech == null || !history.techStates.TryGetValue(techId, out TechState state)) {
                continue;
            }

            int universePointPerHash = tech.Items.Length > 0 && tech.Items[0] == I宇宙矩阵
                ? tech.ItemPoints[0]
                : 0;
            bool hasCurrentShape = state.maxLevel == tech.MaxLevel
                                   && state.curLevel >= tech.Level
                                   && state.curLevel <= tech.MaxLevel;
            if (hasCurrentShape) {
                state.uPointPerHash = universePointPerHash;
                if (state.unlocked) {
                    state.curLevel = tech.MaxLevel;
                }
                state.hashNeeded = tech.GetHashNeeded(state.curLevel);
                state.hashUploaded = state.unlocked
                    ? state.hashNeeded
                    : Math.Max(0L, Math.Min(state.hashUploaded, state.hashNeeded));
                history.techStates[techId] = state;
                continue;
            }

            bool completedLegacyNode = state.unlocked;
            long uploaded = Math.Max(0L, state.hashUploaded);
            state.maxLevel = tech.MaxLevel;
            state.uPointPerHash = universePointPerHash;

            if (completedLegacyNode && tech.MaxLevel > tech.Level) {
                state.unlocked = false;
                state.curLevel = Math.Min(tech.MaxLevel, tech.Level + 1);
                state.hashUploaded = 0L;
                state.hashNeeded = tech.GetHashNeeded(state.curLevel);
            } else if (completedLegacyNode) {
                state.unlocked = true;
                state.curLevel = tech.MaxLevel;
                state.hashNeeded = tech.GetHashNeeded(state.curLevel);
                state.hashUploaded = state.hashNeeded;
            } else {
                state.unlocked = false;
                state.curLevel = tech.Level;
                state.hashNeeded = tech.GetHashNeeded(state.curLevel);
                state.hashUploaded = Math.Min(uploaded, state.hashNeeded);
            }

            history.techStates[techId] = state;
        }
    }

    /// <summary>
    /// 将已完成科技同步到全局堆叠和依赖系统。
    /// </summary>
    public static void SyncRuntimeEffects() {
        GameHistoryData history = GameMain.history;
        if (history == null) {
            return;
        }

        bool experienced = UsesExperiencedStackProgression;
        int stack = !experienced && history.TechUnlocked(T集装物流系统) ? 4 : 1;
        int firstGrowthLevel = experienced ? 1 : 2;
        for (int level = firstGrowthLevel; level <= 5; level++) {
            if (history.TechUnlocked(TFE分馏堆叠成长 + level)) {
                stack = level * 4;
            }
        }
        StackingManager.ConfiguredMaxStack = stack;
        StackingManager.SyncRuntimeState();
    }

    private static void AddInfrastructureTechs() {
        TechProto planetaryVanilla = LDB.techs.Select(T行星物流系统);
        TechProto planetary = ProtoRegistry.RegisterTech(
            TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果",
            planetaryVanilla.IconPath, [],
            [.. planetaryVanilla.Items], [.. planetaryVanilla.ItemPoints], planetaryVanilla.HashNeeded,
            [RFE行星内物流交互站], GetTechPosition(10, 1));
        planetary.PreTechsImplicit = [T行星物流系统];
        planetary.PropertyOverrideItems = [.. planetaryVanilla.PropertyOverrideItems];
        planetary.PropertyItemCounts = [.. planetaryVanilla.PropertyItemCounts];
        planetary.IsHiddenTech = true;
        planetary.IconTag = "txxnjh";

        TechProto interstellarVanilla = LDB.techs.Select(T星际物流系统);
        TechProto interstellar = ProtoRegistry.RegisterTech(
            TFE星际物流交互, "T星际物流交互", "星际物流交互描述", "星际物流交互结果",
            interstellarVanilla.IconPath, [],
            [.. interstellarVanilla.Items], [.. interstellarVanilla.ItemPoints], interstellarVanilla.HashNeeded,
            [RFE星际物流交互站], GetTechPosition(10, 2));
        interstellar.PreTechsImplicit = [T星际物流系统];
        interstellar.PropertyOverrideItems = [.. interstellarVanilla.PropertyOverrideItems];
        interstellar.PropertyItemCounts = [.. interstellarVanilla.PropertyItemCounts];
        interstellar.IsHiddenTech = true;
        interstellar.IconTag = "txjjh";
    }

    private static void AddStackTechs() {
        var series = new GrowthTechSeries("FE分馏堆叠成长", "FE分馏堆叠成长描述",
            "Assets/fe/tech资源复制", 0);
        int previousTech = 0;
        for (int level = 1; level <= 5; level++) {
            int techId = TFE分馏堆叠成长 + level;
            TechProto tech = RegisterGrowthTech(techId, series, previousTech,
                [MatrixItems[ExperiencedStackMatrixStages[level - 1]]],
                ExperiencedStackCosts[level - 1], new GrowthTechLevel(level, level));
            tech.IconTag = "fe-stack-growth";
            StackTechs[level] = tech;
            previousTech = techId;
        }
        SetStackProgressionMode(PermanentProfileStore.HasPlayedFractionation);
    }

    private static void AddCapacityTechs() {
        var series = new GrowthTechSeries("FE亚空间扩容", "FE亚空间扩容描述",
            "Assets/fe/tech分馏数据中心", 1);
        int previousTech = 0;
        for (int level = 1; level <= CapacityInfiniteStartLevel; level++) {
            int matrixStage = Math.Min(MatrixItems.Length - 1,
                (level - 1) * MatrixItems.Length / CapacityInfiniteStartLevel);
            int maxLevel = level == CapacityInfiniteStartLevel ? CapacityMaximumLevel : level;
            int linearCost = level == CapacityInfiniteStartLevel ? CapacityInfiniteCostIncrement : 0;
            TechProto tech = RegisterGrowthTech(
                TFE亚空间扩容 + level, series, previousTech,
                GetProgressiveMatrixItems(matrixStage), CapacityCosts[level - 1],
                new GrowthTechLevel(level, maxLevel, linearCost));
            tech.IconTag = $"fe-growth-{TFE亚空间扩容}";
            previousTech = tech.ID;
        }
    }

    private static void AddFiniteSeries(int baseTechId, string nameKey, string descriptionKey,
        string iconPath, int row) {
        var series = new GrowthTechSeries(nameKey, descriptionKey, iconPath, row);
        int previousTech = 0;
        for (int level = 1; level <= FiniteGrowthLevelCount; level++) {
            TechProto tech = RegisterGrowthTech(
                baseTechId + level, series, previousTech,
                GetProgressiveMatrixItems(level - 1), FiniteGrowthCosts[level - 1],
                new GrowthTechLevel(level, level));
            tech.IconTag = $"fe-growth-{baseTechId}";
            previousTech = tech.ID;
        }
    }

    private static TechProto RegisterGrowthTech(int techId, GrowthTechSeries series,
        int previousTech, int[] matrixItemIds, int matrixCost, GrowthTechLevel levelDefinition) {
        int linearHashIncrement = checked(levelDefinition.LinearCostIncrement * 3600);
        long baseHash = (long)matrixCost * 3600L
                        - (long)levelDefinition.Level * linearHashIncrement;
        int[] itemPoints = Enumerable.Repeat(1, matrixItemIds.Length).ToArray();
        TechProto tech = ProtoRegistry.RegisterTech(
            techId, series.NameKey, series.DescriptionKey, "FE成长科技结果", series.IconPath,
            previousTech > 0 ? [previousTech] : [],
            matrixItemIds, itemPoints, baseHash,
            [], GetTechPosition(series.Row, levelDefinition.Level));
        tech.Level = levelDefinition.Level;
        tech.MaxLevel = levelDefinition.MaxLevel;
        tech.LevelCoef1 = linearHashIncrement;
        tech.LevelCoef2 = 0;
        tech.PreTechsImplicit = [GetMatrixTechId(matrixItemIds[matrixItemIds.Length - 1])];
        tech.AddItems = [];
        tech.AddItemCounts = [];
        tech.PropertyOverrideItems = [.. matrixItemIds];
        tech.PropertyItemCounts = Enumerable.Repeat(matrixCost, matrixItemIds.Length).ToArray();
        tech.IsHiddenTech = true;
        tech.IsObsolete = false;
        return tech;
    }

    private static void ConfigureGrowthTech(TechProto tech, int level, int maxLevel, int previousTech,
        int[] matrixItemIds, int matrixCost, int[] additionalImplicitTechs) {
        tech.Level = level;
        tech.MaxLevel = maxLevel;
        tech.LevelCoef1 = 0;
        tech.LevelCoef2 = 0;
        tech.PreTechs = previousTech > 0 ? [previousTech] : [];
        tech.PreTechsImplicit = additionalImplicitTechs
            .Append(GetMatrixTechId(matrixItemIds[matrixItemIds.Length - 1]))
            .Distinct()
            .ToArray();
        tech.Items = [.. matrixItemIds];
        tech.ItemPoints = Enumerable.Repeat(1, matrixItemIds.Length).ToArray();
        tech.PropertyOverrideItems = [.. matrixItemIds];
        tech.PropertyItemCounts = Enumerable.Repeat(matrixCost, matrixItemIds.Length).ToArray();
        tech.HashNeeded = (long)matrixCost * 3600L;
    }

    private static int[] GetProgressiveMatrixItems(int matrixStage) => matrixStage switch {
        <= 0 => [I电磁矩阵],
        1 => [I电磁矩阵, I能量矩阵],
        2 => [I电磁矩阵, I能量矩阵, I结构矩阵],
        3 => [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵],
        4 => [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵],
        _ => [I宇宙矩阵],
    };

    private static void RefreshStackTechLinks() {
        if (LDB.techs?.dataArray == null || LDB.techs.Select(TFE分馏堆叠成长 + 1) == null) {
            return;
        }

        int[] affectedTechIds = [
            TFE分馏堆叠成长 + 1,
            TFE分馏堆叠成长 + 2,
            TFE分馏堆叠成长 + 3,
            TFE分馏堆叠成长 + 4,
            TFE分馏堆叠成长 + 5,
        ];
        foreach (int techId in affectedTechIds) {
            LDB.techs.Select(techId)?.Preload2();
        }
        foreach (int techId in affectedTechIds) {
            TechProto prerequisite = LDB.techs.Select(techId);
            if (prerequisite == null) {
                continue;
            }
            prerequisite.postTechArray = LDB.techs.dataArray
                .Where(candidate => candidate?.PreTechs?.Contains(techId) == true)
                .ToArray();
        }
    }

    private static void SyncUnstartedStackTechStates() {
        GameHistoryData history = GameMain.history;
        if (history == null) {
            return;
        }

        for (int level = 1; level <= 5; level++) {
            int techId = TFE分馏堆叠成长 + level;
            TechProto tech = LDB.techs.Select(techId);
            if (tech == null
                || !history.techStates.TryGetValue(techId, out TechState state)
                || state.unlocked
                || state.hashUploaded > 0L) {
                continue;
            }
            state.curLevel = tech.Level;
            state.maxLevel = tech.MaxLevel;
            state.hashNeeded = tech.GetHashNeeded(state.curLevel);
            history.techStates[techId] = state;
        }
    }

    private static int GetMatrixTechId(int matrixItemId) {
        int index = Array.IndexOf(MatrixItems, matrixItemId);
        return index >= 0 ? MatrixTechs[index] : 0;
    }

    private static int GetCapacityLevel() {
        GameHistoryData history = GameMain.history;
        if (history == null) {
            return 0;
        }

        int level = GetUnlockedLevel(TFE亚空间扩容, CapacityFiniteLevelCount);
        int infiniteTechId = TFE亚空间扩容 + CapacityInfiniteStartLevel;
        if (!history.techStates.TryGetValue(infiniteTechId, out TechState state)) {
            return level;
        }
        int completedLevel = state.unlocked
            ? state.maxLevel
            : state.curLevel > CapacityInfiniteStartLevel ? state.curLevel - 1 : level;
        return Math.Max(level, Math.Max(0, Math.Min(CapacityMaximumLevel, completedLevel)));
    }

    private static int GetUnlockedLevel(int baseTechId, int maxLevel) {
        GameHistoryData history = GameMain.history;
        if (history == null) {
            return 0;
        }
        int level = 0;
        for (int current = 1; current <= maxLevel; current++) {
            if (history.TechUnlocked(baseTechId + current)) {
                level = current;
            }
        }
        return level;
    }

    private static IEnumerable<int> GetGrowthTechIds() {
        for (int level = 1; level <= 5; level++) {
            yield return TFE分馏堆叠成长 + level;
        }
        for (int level = 1; level <= CapacityInfiniteStartLevel; level++) {
            yield return TFE亚空间扩容 + level;
        }
        int[] finiteBases = [
            TFE亚空间能效,
            TFE探索信号增幅,
            TFE高级矿物特质,
            TFE高级交互特质,
            TFE高级转化特质,
        ];
        foreach (int baseTechId in finiteBases) {
            for (int level = 1; level <= FiniteGrowthLevelCount; level++) {
                yield return baseTechId + level;
            }
        }
    }

    private static Vector2 GetTechPosition(int row, int column) {
        if (GenesisBook.Enable) {
            return new Vector2(9 + column * 4, -47 - row * 4);
        }
        if (OrbitalRing.Enable) {
            return new Vector2(8 + column * 4, -76 - row * 4);
        }
        return new Vector2(13 + column * 4, -67 - row * 4);
    }
}
