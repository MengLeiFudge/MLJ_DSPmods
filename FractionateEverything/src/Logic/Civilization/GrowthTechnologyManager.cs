using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private static int mineralTraitLevel;
    private static int interactionTraitLevel;
    private static int conversionTraitLevel;

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

    /// <summary>亚空间存取能效科技方向的稳定内容 ID。</summary>
    public const string SubspaceEfficiencyTechnologyContentId = "technology:subspace-efficiency";

    /// <summary>探索信号增幅科技方向的稳定内容 ID。</summary>
    public const string ExplorationTechnologyContentId = "technology:exploration-signal";

    private const int FiniteGrowthLevelCount = 6;

    private static readonly int[] MatrixItems = [
        I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵,
    ];
    private static readonly int[] MatrixTechs = [
        T电磁矩阵, T能量矩阵, T结构矩阵, T信息矩阵, T引力矩阵, T宇宙矩阵,
    ];

    // 采用原版“无人机引擎”升级链的六阶段矩阵数量。
    private static readonly int[] FiniteGrowthCosts = [200, 400, 800, 1200, 1600, 50000];
    /// <summary>每种矩阵每件对应的科研哈希，按蓝至白采用原版无人机引擎各阶段工作量。</summary>
    private static readonly int[] MatrixHashPerItem = [90, 180, 180, 300, 900, 900];
    private static readonly int[] ExperiencedStackCosts = [1200, 2400, 3600, 4800, 6000];
    private static readonly int[] StandardStackCosts = [0, 6000, 8000, 10000, 40000];
    private static readonly int[] ExperiencedStackMatrixStages = [0, 1, 2, 3, 4];
    private static readonly int[] StandardStackMatrixStages = [0, 3, 4, 5, 5];
    private static readonly TechProto[] StackTechs = new TechProto[6];
    private static bool stackProgressionModeApplied;

    /// <summary>当前存档是否采用已玩过分馏的堆叠路线。</summary>
    public static bool UsesExperiencedStackProgression { get; private set; }

    /// <summary>长链任务是否已授予无限仓储。</summary>
    public static bool UltimateCapacity => GameMain.history?.TechUnlocked(TFE终极仓储) == true;

    /// <summary>长链任务是否已授予持续探索。</summary>
    public static bool UltimateExploration => GameMain.history?.TechUnlocked(TFE终极探索) == true;

    /// <summary>获取亚空间存取能效科技等级，范围 0..6。</summary>
    public static int SubspaceEfficiencyLevel => GetUnlockedLevel(TFE亚空间能效, FiniteGrowthLevelCount);

    /// <summary>获取探索常规成长等级，白糖节点承载第 6～10 级。</summary>
    public static int ExplorationLevel => Math.Max(GetUnlockedLevel(TFE探索信号增幅, 5),
        GetCompletedLevel(TFE探索信号增幅 + 6));

    /// <summary>获取探测器显示强度的量化步长，单位为百分点。</summary>
    public static float ExplorationSignalPrecisionPercent => GetSignalPrecision(ExplorationLevel);

    /// <summary>返回指定探索等级的读数量化间隔，单位为百分点。</summary>
    /// <param name="level">已完成探索等级。</param>
    /// <returns>信号读数的最小间隔。</returns>
    private static float GetSignalPrecision(int level) => level switch {
        <= 0 => 20f, 1 => 10f, 2 => 5f, 3 => 2f, 4 => 1f, _ => 0.5f,
    };

    /// <summary>判断玩家是否已经解锁主动位置标注。</summary>
    public static bool ExplorationLocationTrackingUnlocked => ExplorationLevel >= 5;

    /// <summary>获取高级矿物塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedMineralTraitLevel => Volatile.Read(ref mineralTraitLevel);

    /// <summary>获取高级交互塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedInteractionTraitLevel => Volatile.Read(ref interactionTraitLevel);

    /// <summary>获取高级转化塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedConversionTraitLevel => Volatile.Read(ref conversionTraitLevel);

    /// <summary>
    /// 应用当前存档的堆叠成长路线。普通路线由实际集装设备解锁提供4堆叠，FE从8开始。
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

            bool active = experienced || level is >= 2 and <= 4;
            int stage = experienced
                ? ExperiencedStackMatrixStages[level - 1]
                : StandardStackMatrixStages[level - 1];
            int cost = experienced
                ? ExperiencedStackCosts[level - 1]
                : StandardStackCosts[level - 1];
            int previousTech = experienced ? level > 1 ? TFE分馏堆叠成长 + level - 1 : 0 :
                level > 2 ? TFE分馏堆叠成长 + level - 1 : 0;

            int techLevel = experienced ? level : Math.Max(1, level - 1);
            ConfigureGrowthTech(tech, techLevel, !experienced && level == 4 ? 4 : techLevel, previousTech,
                [MatrixItems[stage]], cost);
            tech.Position = GetTechPosition(10, stage - 1);
            // 深空来敌元驱动链占据y=-107；上移两行，在两条链之间保留一行空白。
            if (TheyComeFromVoid.Enable) { tech.Position += new Vector2(0f, 8f); }
            if (!experienced && level == 4) {
                tech.LevelCoef1 = (StandardStackCosts[4] - cost) * MatrixHashPerItem[stage];
                tech.HashNeeded -= (long)tech.Level * tech.LevelCoef1;
            }
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
    public static bool IsTechnologyNodeActive(int techId) => LDB.techs.Select(techId)?.IsObsolete == false;

    /// <summary>
    /// 获取科技节点对应的解析内容 ID。
    /// </summary>
    public static bool TryGetTechnologyContentId(int techId, out string contentId) {
        contentId = techId switch {
            TFE终极探索 => "ultimate:exploration",
            TFE终极仓储 => "ultimate:capacity",
            TFE行星内物流交互 => PlanetaryInteractionTechnologyContentId,
            TFE星际物流交互 => InterstellarInteractionTechnologyContentId,
            > TFE分馏堆叠成长 and <= TFE分馏堆叠成长 + 5 => StackTechnologyContentId,
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
    public static bool IsTechnologyDiscovered(int techId) {
        if (techId is TFE终极探索 or TFE终极仓储) {
            return GameMain.history?.TechUnlocked(techId) == true;
        }
        if (!IsTechnologyNodeActive(techId) || !TryGetTechnologyContentId(techId, out string contentId)) {
            return false;
        }
        if (techId is TFE行星内物流交互 or TFE星际物流交互) {
            return ResearchManager.IsContentUnlocked(contentId);
        }
        if (contentId == StackTechnologyContentId && !UsesExperiencedStackProgression
            && !StackingManager.HasPilingTechnology) { return false; }
        TechProto tech = LDB.techs.Select(techId);
        int stage = tech?.Items?.Length > 0
            ? Array.IndexOf(MatrixItems, tech.Items[tech.Items.Length - 1]) + 1 : 0;
        bool recovered = stage > 0 && ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(stage));
        bool trait = contentId is ResearchManager.AdvancedMineralTraitId
            or ResearchManager.AdvancedInteractionTraitId or ResearchManager.AdvancedConversionTraitId;
        return recovered && (!trait || ResearchManager.IsContentUnlocked(contentId));
    }

    /// <summary>
    /// 获取成长科技在原版“解锁”区域显示的实际效果。
    /// </summary>
    public static bool TryGetTechnologyUnlockText(TechProto tech, out string text) {
        int techId = tech?.ID ?? 0;
        int level = Math.Max(1, tech?.Level ?? 1);
        if (tech != null && tech.MaxLevel > tech.Level
            && GameMain.history?.techStates.TryGetValue(techId, out TechState state) == true) {
            level = Math.Max(tech.Level, Math.Min(tech.MaxLevel, state.curLevel));
        }
        text = techId switch {
            TFE终极探索 => "远古广播持续回应".Translate(),
            TFE终极仓储 => "亚空间容量：无限".Translate(),
            > TFE分馏堆叠成长 and <= TFE分馏堆叠成长 + 5 =>
                string.Format("货物堆叠上限 +{0}".Translate(),
                    UsesExperiencedStackProgression && techId == TFE分馏堆叠成长 + 1 ? 3 : 4),
            > TFE亚空间能效 and <= TFE亚空间能效 + FiniteGrowthLevelCount => "亚空间存取耗能 -10%".Translate(),
            > TFE探索信号增幅 and <= TFE探索信号增幅 + FiniteGrowthLevelCount =>
                BuildExplorationUnlockText(level),
            > TFE高级矿物特质 and <= TFE高级矿物特质 + FiniteGrowthLevelCount => "珍奇催化物损耗 -10%".Translate(),
            > TFE高级交互特质 and <= TFE高级交互特质 + FiniteGrowthLevelCount => "额外原胚概率 +5%".Translate(),
            > TFE高级转化特质 and <= TFE高级转化特质 + FiniteGrowthLevelCount => "部件回收倍率 +5%".Translate(),
            _ => null,
        };
        return text != null;
    }

    /// <summary>生成探索科技本级的能力增量，多级白糖节点各级共用同一说明。</summary>
    /// <param name="level">当前待研究等级。</param>
    /// <returns>原版解锁区域中的逐项增量。</returns>
    private static string BuildExplorationUnlockText(int level) {
        string text = string.Format("FE.Growth.ExplorationIncrement".Translate(),
            FragDropPatch.FragDropProbabilityPerLevel);
        if (level <= 5) {
            text += "\n" + string.Format("信号读数间隔 -{0:0.#}%".Translate(),
                GetSignalPrecision(level - 1) - GetSignalPrecision(level));
        }
        if (level == 5) { text += "\n" + "信号位置标注".Translate(); }
        return text;
    }

    /// <summary>
    /// 注册第一页物流交互科技和第二页成长科技链。
    /// </summary>
    public static void AddTechs() {
        AddInfrastructureTechs();
        AddStackTechs();
        AddFiniteSeries(TFE亚空间能效, "FE亚空间能效", "FE亚空间能效描述",
            "Assets/fe/tech分馏数据中心", 6);
        AddFiniteSeries(TFE探索信号增幅, "FE探索信号增幅", "FE探索信号增幅描述",
            "Assets/fe/tech物品分解", 2);
        AddUltimateTech(TFE终极探索, "终极探索", TFE探索信号增幅 + 6, 2);
        AddUltimateTech(TFE终极仓储, "终极仓储", 0, 1);
        AddFiniteSeries(TFE高级矿物特质, "FE高级矿物特质", "FE高级矿物特质描述",
            "Assets/fe/tech资源复制", 3);
        AddFiniteSeries(TFE高级交互特质, "FE高级交互特质", "FE高级交互特质描述",
            "Assets/fe/tech物品交互", 4);
        AddFiniteSeries(TFE高级转化特质, "FE高级转化特质", "FE高级转化特质描述",
            "Assets/fe/tech物品转化", 5);
    }

    /// <summary>
    /// 将已完成科技同步到全局堆叠和依赖系统。
    /// </summary>
    public static void SyncRuntimeEffects() {
        GameHistoryData history = GameMain.history;
        Volatile.Write(ref mineralTraitLevel, GetUnlockedLevel(TFE高级矿物特质, FiniteGrowthLevelCount));
        Volatile.Write(ref interactionTraitLevel, GetUnlockedLevel(TFE高级交互特质, FiniteGrowthLevelCount));
        Volatile.Write(ref conversionTraitLevel, GetUnlockedLevel(TFE高级转化特质, FiniteGrowthLevelCount));
        if (history == null) {
            return;
        }

        bool experienced = UsesExperiencedStackProgression;
        int stack = !experienced && StackingManager.HasPilingTechnology ? 4 : 1;
        int firstGrowthLevel = experienced ? 1 : 2;
        for (int level = firstGrowthLevel; level <= (experienced ? 5 : 3); level++) {
            if (history.TechUnlocked(TFE分馏堆叠成长 + level)) { stack = level * 4; }
        }
        if (!experienced) {
            int completed = GetCompletedLevel(TFE分馏堆叠成长 + 4);
            if (completed >= 3) { stack = Math.Max(stack, (completed + 1) * 4); }
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
            [RFE行星内物流交互站], GetTechPosition(-23, 4));
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
            [RFE星际物流交互站], GetTechPosition(-23, 6));
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

    private static void AddFiniteSeries(int baseTechId, string nameKey, string descriptionKey,
        string iconPath, int row) {
        var series = new GrowthTechSeries(nameKey, descriptionKey, iconPath, row);
        int previousTech = 0;
        for (int level = 1; level <= FiniteGrowthLevelCount; level++) {
            TechProto tech = RegisterGrowthTech(
                baseTechId + level, series, previousTech,
                GetProgressiveMatrixItems(level - 1), FiniteGrowthCosts[level - 1],
                new GrowthTechLevel(level,
                    baseTechId == TFE探索信号增幅 && level == 6 ? 10 : level,
                    baseTechId == TFE探索信号增幅 && level == 6 ? 10000 : 0));
            tech.IconTag = $"fe-growth-{baseTechId}";
            previousTech = tech.ID;
        }
    }

    private static TechProto RegisterGrowthTech(int techId, GrowthTechSeries series,
        int previousTech, int[] matrixItemIds, int matrixCost, GrowthTechLevel levelDefinition) {
        int hashPerItem = MatrixHashPerItem[Array.IndexOf(MatrixItems, matrixItemIds[matrixItemIds.Length - 1])];
        int linearHashIncrement = checked(levelDefinition.LinearCostIncrement * hashPerItem);
        long baseHash = (long)matrixCost * hashPerItem
                        - (long)levelDefinition.Level * linearHashIncrement;
        // 原版每件矩阵有3600材料点；缩短科研工作量时同步提高每哈希消耗，保持材料总成本。
        int[] itemPoints = Enumerable.Repeat(3600 / hashPerItem, matrixItemIds.Length).ToArray();
        string nodeNameKey = $"{series.NameKey} {levelDefinition.Level}";
        TechProto tech = ProtoRegistry.RegisterTech(
            techId, nodeNameKey, series.DescriptionKey, "FE成长科技结果", series.IconPath,
            previousTech > 0 ? [previousTech] : [],
            matrixItemIds, itemPoints, baseHash,
            [], GetTechPosition(series.Row + 10, levelDefinition.Level - 2));
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
        int[] matrixItemIds, int matrixCost) {
        tech.Level = level;
        tech.MaxLevel = maxLevel;
        tech.LevelCoef1 = 0;
        tech.LevelCoef2 = 0;
        tech.PreTechs = previousTech > 0 ? [previousTech] : [];
        tech.PreTechsImplicit = [GetMatrixTechId(matrixItemIds[matrixItemIds.Length - 1])];
        int hashPerItem = MatrixHashPerItem[Array.IndexOf(MatrixItems, matrixItemIds[matrixItemIds.Length - 1])];
        tech.Items = [.. matrixItemIds];
        tech.ItemPoints = Enumerable.Repeat(3600 / hashPerItem, matrixItemIds.Length).ToArray();
        tech.PropertyOverrideItems = [.. matrixItemIds];
        tech.PropertyItemCounts = Enumerable.Repeat(matrixCost, matrixItemIds.Length).ToArray();
        tech.HashNeeded = (long)matrixCost * hashPerItem;
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
                .Where(candidate => candidate?.IsObsolete == false && candidate.PreTechs?.Contains(techId) == true)
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
                || state.hashUploaded > 0L
                || state.maxLevel == tech.MaxLevel && state.curLevel > tech.Level) {
                continue;
            }
            state.curLevel = tech.Level;
            state.maxLevel = tech.MaxLevel;
            state.hashNeeded = tech.GetHashNeeded(state.curLevel);
            history.techStates[techId] = state;
        }
    }

    /// <summary>按当前原型校准FE科研工作量与白糖计数，保持未完成节点可继续结算，不重复授予效果。</summary>
    public static void SyncResearchWorkload() {
        Dictionary<int, TechState> states = GameMain.history?.techStates;
        if (states == null) { return; }
        foreach (TechProto tech in LDB.techs.dataArray) {
            if (tech == null || tech.IsObsolete || !TryGetTechnologyContentId(tech.ID, out _)
                || !states.TryGetValue(tech.ID, out TechState state)) { continue; }
            long needed = tech.GetHashNeeded(state.curLevel);
            if (needed <= 0) { continue; }
            // 原版导入已用新原型覆盖hashNeeded，但保留原hashUploaded；超过新总量会使AddTechHash直接返回。
            long uploaded = state.unlocked ? needed : Math.Min(needed - 1, Math.Max(0L, state.hashUploaded));
            int universePoints = tech.Items.Length > 0 && tech.Items[0] == I宇宙矩阵 ? tech.ItemPoints[0] : 0;
            if (state.hashNeeded == needed && state.hashUploaded == uploaded
                && state.uPointPerHash == universePoints) { continue; }
            state.hashUploaded = uploaded;
            state.hashNeeded = needed;
            state.uPointPerHash = universePoints;
            states[tech.ID] = state;
        }
    }

    /// <summary>将同格式3.0存档的白糖研究状态归并到当前多级节点，保留已完成等级与当前研究量。</summary>
    public static void ConsolidateWhiteMatrixResearch() {
        if (!ResearchManager.BelongsToGeneration3) { return; }
        if (!UsesExperiencedStackProgression) {
            ConsolidateResearchNode(TFE分馏堆叠成长 + 4, [TFE分馏堆叠成长 + 5]);
        }
    }

    /// <summary>在节点等级上限变化时合并同一条白糖研究链，已归并的存档不再重复处理。</summary>
    /// <param name="techId">当前承载白糖等级的节点。</param>
    /// <param name="retiredIds">同一研究链中已隐藏的节点。</param>
    private static void ConsolidateResearchNode(int techId, int[] retiredIds) {
        GameHistoryData history = GameMain.history;
        TechProto tech = LDB.techs.Select(techId);
        if (tech == null || history == null || !history.techStates.TryGetValue(techId, out TechState state)) { return; }
        for (int i = 0; i < history.techQueue.Length; i++) {
            if (retiredIds.Contains(history.techQueue[i])) { history.techQueue[i] = techId; }
        }
        if (retiredIds.Contains(history.currentTech)) { history.currentTech = techId; }
        if (state.maxLevel == tech.MaxLevel) { return; }
        int completed = GetCompletedLevel(techId);
        TechState pending = state;
        foreach (int id in retiredIds) {
            completed = Math.Max(completed, GetCompletedLevel(id));
            if (history.techStates.TryGetValue(id, out TechState other) && !other.unlocked
                && other.hashUploaded > 0 && other.curLevel > pending.curLevel) { pending = other; }
        }
        state.maxLevel = tech.MaxLevel;
        state.unlocked = completed >= tech.MaxLevel;
        state.curLevel = Math.Max(tech.Level, Math.Min(tech.MaxLevel, completed + 1));
        state.hashNeeded = tech.GetHashNeeded(state.curLevel);
        state.hashUploaded = state.unlocked ? state.hashNeeded
            : pending.curLevel == state.curLevel ? Math.Min(pending.hashUploaded, state.hashNeeded) : 0;
        history.techStates[techId] = state;
    }

    private static int GetMatrixTechId(int matrixItemId) {
        int index = Array.IndexOf(MatrixItems, matrixItemId);
        return index >= 0 ? MatrixTechs[index] : 0;
    }

    /// <summary>读取多级节点已经完成的等级，未研究节点的初始 curLevel 不算完成。</summary>
    /// <param name="techId">科技原型身份。</param>
    /// <returns>已完成等级，尚未研究时为零。</returns>
    private static int GetCompletedLevel(int techId) {
        TechProto tech = LDB.techs.Select(techId);
        if (tech == null || GameMain.history?.techStates.TryGetValue(techId, out TechState state) != true) { return 0; }
        return state.unlocked ? state.maxLevel : state.curLevel > tech.Level ? state.curLevel - 1 : 0;
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

    /// <summary>注册由剧情直接授予的第十一级标记，普通研究入口保持关闭。</summary>
    /// <param name="id">稳定科技身份。</param>
    /// <param name="name">名称键。</param>
    /// <param name="previous">常规分支末节点；仓储由容量里程碑开启，取0。</param>
    /// <param name="row">科技树布局行。</param>
    private static void AddUltimateTech(int id, string name, int previous, int row) {
        TechProto tech = ProtoRegistry.RegisterTech(id, name, "终极任务科技描述", "FE成长科技结果",
            "Assets/fe/tech分馏数据中心", previous > 0 ? [previous] : [], [], [], 0, [], GetTechPosition(row + 10, 5));
        tech.Level = 11;
        tech.MaxLevel = 11;
        tech.IsHiddenTech = true;
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
