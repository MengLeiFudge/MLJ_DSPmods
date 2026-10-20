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
    /// <summary>成长首行相对第二页宇宙探索的网格行，护盾链下方留出一行空白。</summary>
    private const int FirstGrowthRow = 36;

    /// <summary>六阶段成长的单种材料预算；实际材料及比例读取最终科技树。</summary>
    private static readonly int[] FiniteGrowthCosts = [200, 400, 800, 1200, 1600, 50000];
    private static readonly int[] StackCosts = [3000, 6000, 4000, 6000, 8000];
    private static readonly int[] StackStages = [3, 3, 4, 4, 4];
    private static readonly TechProto[] StackTechs = new TechProto[6];
    /// <summary>成长节点的阶段身份与预算，不受参考科技材料组合影响。</summary>
    private static readonly Dictionary<int, (int Stage, int Budget, int Increment)> ResearchCosts = [];
    /// <summary>成长节点相对成长首行的行列，最终加载后统一应用。</summary>
    private static readonly Dictionary<int, (int Row, int Column)> GrowthTechPositions = [];

    /// <summary>长链任务是否已授予无限仓储。</summary>
    public static bool UltimateCapacity => GameMain.history?.TechUnlocked(TFE终极仓储) == true;

    /// <summary>长链任务是否已授予持续探索。</summary>
    public static bool UltimateExploration => GameMain.history?.TechUnlocked(TFE终极探索) == true;

    /// <summary>一次性研究完成后的剩余耗电倍率，索引零表示未研究。</summary>
    private static readonly double[] SubspaceEnergyRatios = [1d, 0.7d, 0.45d, 0.3d, 0.2d];
    /// <summary>白糖能效的重复研究次数，最后一次达到基础耗电的百分之一。</summary>
    private const int WhiteEfficiencyLevels = 29;

    /// <summary>物流交互站实际采用的单件存取耗电倍率。</summary>
    public static double SubspaceEnergyRatio {
        get {
            int level = GetUnlockedLevel(TFE亚空间能效 + 1, 4);
            int whiteLevels = Math.Max(0, GetCompletedLevel(TFE亚空间能效 + 6) - 4);
            return whiteLevels > 0
                ? Math.Max(0.01d, 0.2d * Math.Pow(0.9d, whiteLevels))
                : SubspaceEnergyRatios[level];
        }
    }

    /// <summary>获取高级矿物塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedMineralTraitLevel => Volatile.Read(ref mineralTraitLevel);

    /// <summary>繁育成功轮的平均原胚产量，运行结算与界面共用。</summary>
    public static float EmbryoBreedingOutput => 1.5f + 0.25f * AdvancedInteractionTraitLevel;

    /// <summary>获取高级交互塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedInteractionTraitLevel => Volatile.Read(ref interactionTraitLevel);

    /// <summary>获取高级转化塔特质科技等级，范围 0..6。</summary>
    public static int AdvancedConversionTraitLevel => Volatile.Read(ref conversionTraitLevel);

    /// <summary>配置紫糖两级与绿糖三级堆叠，五个稳定身份连续排列。</summary>
    public static void ConfigureStackProgression() {
        for (int level = 1; level <= 5; level++) {
            TechProto tech = StackTechs[level] ?? LDB.techs.Select(TFE分馏堆叠成长 + level);
            if (tech == null) { continue; }
            int stage = StackStages[level - 1];
            int budget = StackCosts[level - 1];
            int increment = 0;
            tech.Level = level;
            tech.MaxLevel = tech.Level;
            tech.PreTechs = level == 1 ? [3315, 3803, T信息矩阵]
                : [TFE分馏堆叠成长 + level - 1];
            ResearchCosts[tech.ID] = (stage, budget, increment);
            MatrixResearchCosts.Apply(tech, stage, budget, increment);
            GrowthTechPositions[tech.ID] = (3, level - 1);
            tech.Position = GetGrowthTechPosition(3, level - 1);
            tech.IsHiddenTech = true;
            tech.IsObsolete = false;
        }
        RefreshStackTechLinks();
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
    /// 判断科技是否已经完成探索和解析，且可以公开真实内容或加入研究队列。
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
        int stage = GetResearchStage(techId);
        bool recovered = stage > 0 && ResearchManager.IsContentUnlocked(ResearchManager.StageContentId(stage));
        bool trait = contentId is ResearchManager.AdvancedMineralTraitId
            or ResearchManager.AdvancedInteractionTraitId or ResearchManager.AdvancedConversionTraitId;
        return recovered && (!trait || ResearchManager.IsContentUnlocked(contentId));
    }

    /// <summary>预告当前资格下的直接后继；只表示仍有未恢复内容，不授予研究资格。</summary>
    /// <param name="techId">活动FE科技的稳定ID。</param>
    /// <returns>满足原版物流资格或当前矩阵阶段及已完成显式前置、但尚未恢复的节点为true。</returns>
    public static bool IsTechnologyPreview(int techId) {
        GameHistoryData history = GameMain.history;
        if (history == null || !IsTechnologyNodeActive(techId) || !TryGetTechnologyContentId(techId, out string contentId)
            || techId is TFE终极探索 or TFE终极仓储 || history.TechUnlocked(techId) || IsTechnologyDiscovered(techId)) {
            return false;
        }
        if (techId is TFE行星内物流交互 or TFE星际物流交互) {
            return history.TechUnlocked(techId == TFE行星内物流交互 ? T行星物流系统 : T星际物流系统);
        }
        TechProto tech = LDB.techs.Select(techId);
        int stage = GetResearchStage(techId);
        return stage > 0 && stage <= ResearchManager.CurrentMatrixStage
            && (tech.PreTechs == null || tech.PreTechs.All(history.TechUnlocked));
    }

    /// <summary>
    /// 获取成长科技在原版“解锁”区域显示的实际效果。
    /// </summary>
    public static bool TryGetTechnologyUnlockText(TechProto tech, out string text) {
        int techId = tech?.ID ?? 0;
        text = techId switch {
            TFE终极探索 => "远古广播持续回应".Translate(),
            TFE终极仓储 => "亚空间容量：无限".Translate(),
            > TFE分馏堆叠成长 and <= TFE分馏堆叠成长 + 5 =>
                string.Format("货物堆叠上限 +{0}".Translate(), techId <= TFE分馏堆叠成长 + 2 ? 2 : 4),
            TFE亚空间能效 + 6 => "亚空间存取耗电-10%".Translate(),
            > TFE亚空间能效 + 1 and <= TFE亚空间能效 + 5 =>
                string.Format("FE.Growth.SubspaceEnergyRatio".Translate(), SubspaceEnergyRatios[techId - TFE亚空间能效 - 1]),
            > TFE高级矿物特质 and <= TFE高级矿物特质 + FiniteGrowthLevelCount => "珍奇催化物损耗 -10%".Translate(),
            > TFE高级交互特质 and <= TFE高级交互特质 + FiniteGrowthLevelCount => "繁育平均产出 +0.25".Translate(),
            > TFE高级转化特质 and <= TFE高级转化特质 + FiniteGrowthLevelCount => "部件回收倍率 +5%".Translate(),
            _ => null,
        };
        return text != null;
    }

    /// <summary>
    /// 注册第一页物流交互科技和第二页成长科技链。
    /// </summary>
    public static void AddTechs() {
        AddInfrastructureTechs();
        AddStackTechs();
        AddEfficiencyTechs();
        AddRetiredExplorationTechs();
        AddUltimateTech(TFE终极探索, "终极探索");
        AddUltimateTech(TFE终极仓储, "终极仓储");
        AddFiniteSeries(TFE高级矿物特质, "FE高级矿物特质", "FE高级矿物特质描述",
            "Assets/fe/tech资源复制", 0);
        AddFiniteSeries(TFE高级交互特质, "FE高级交互特质", "FE高级交互特质描述",
            "Assets/fe/tech物品交互", 1);
        AddFiniteSeries(TFE高级转化特质, "FE高级转化特质", "FE高级转化特质描述",
            "Assets/fe/tech物品转化", 2);
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

        StackingManager.ConfiguredMaxStack = StackingManager.ResearchMaxStack;
        StackingManager.SyncRuntimeState();
    }

    /// <summary>在所有模组原型修改完成后，按各页最终基准统一应用FE科技布局。</summary>
    public static void ApplyTechLayout() {
        LDB.techs.Select(TFE行星内物流交互).Position = GetInteractionTechPosition(false);
        LDB.techs.Select(TFE星际物流交互).Position = GetInteractionTechPosition(true);
        foreach (var entry in GrowthTechPositions) {
            LDB.techs.Select(entry.Key).Position = GetGrowthTechPosition(entry.Value.Row, entry.Value.Column);
        }
    }

    private static void AddInfrastructureTechs() {
        TechProto planetaryVanilla = LDB.techs.Select(T行星物流系统);
        TechProto planetary = ProtoRegistry.RegisterTech(
            TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果",
            planetaryVanilla.IconPath, [],
            [.. planetaryVanilla.Items], [.. planetaryVanilla.ItemPoints], planetaryVanilla.HashNeeded,
            [RFE行星内物流交互站], GetInteractionTechPosition(false));
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
            [RFE星际物流交互站], GetInteractionTechPosition(true));
        interstellar.PreTechsImplicit = [T星际物流系统];
        interstellar.PropertyOverrideItems = [.. interstellarVanilla.PropertyOverrideItems];
        interstellar.PropertyItemCounts = [.. interstellarVanilla.PropertyItemCounts];
        interstellar.IsHiddenTech = true;
        interstellar.IconTag = "txjjh";
    }

    /// <summary>红至绿矩阵各一次性降耗，白矩阵继续逐级乘算；旧蓝矩阵身份仅供旧档识别。</summary>
    private static void AddEfficiencyTechs() {
        var series = new GrowthTechSeries("FE亚空间能效", "FE亚空间能效描述", "Assets/fe/tech分馏数据中心", 4);
        TechProto retired = RegisterGrowthTech(TFE亚空间能效 + 1, series, 0, 0, 200, new GrowthTechLevel(1, 1));
        retired.IsObsolete = true;
        GrowthTechPositions.Remove(retired.ID);
        int previous = TFE行星内物流交互;
        for (int stage = 1; stage <= 5; stage++) {
            TechProto tech = RegisterGrowthTech(TFE亚空间能效 + stage + 1, series, previous, stage,
                FiniteGrowthCosts[stage], new GrowthTechLevel(stage, stage == 5 ? 4 + WhiteEfficiencyLevels : stage,
                    stage == 5 ? 10000 : 0));
            tech.IconTag = $"fe-growth-{TFE亚空间能效}";
            previous = tech.ID;
        }
    }

    private static void AddStackTechs() {
        var series = new GrowthTechSeries("FE分馏堆叠成长", "FE分馏堆叠成长描述",
            "Assets/fe/tech资源复制", 3);
        int previousTech = 0;
        for (int level = 1; level <= 5; level++) {
            int techId = TFE分馏堆叠成长 + level;
            TechProto tech = RegisterGrowthTech(techId, series, previousTech,
                StackStages[level - 1], StackCosts[level - 1], new GrowthTechLevel(level, level));
            tech.IconTag = "fe-stack-growth";
            StackTechs[level] = tech;
            previousTech = techId;
        }
        ConfigureStackProgression();
    }

    /// <summary>保留旧科技原型以读取存档身份，不再提供研究入口或效果。</summary>
    private static void AddRetiredExplorationTechs() {
        var series = new GrowthTechSeries("FE探索信号增幅", "FE探索信号增幅描述", "Assets/fe/tech物品分解", 0);
        for (int level = 1; level <= FiniteGrowthLevelCount; level++) {
            TechProto tech = RegisterGrowthTech(TFE探索信号增幅 + level, series, 0,
                level - 1, FiniteGrowthCosts[level - 1], new GrowthTechLevel(level, level == 6 ? 10 : level));
            tech.IsObsolete = true;
            GrowthTechPositions.Remove(tech.ID);
        }
    }

    private static void AddFiniteSeries(int baseTechId, string nameKey, string descriptionKey,
        string iconPath, int row) {
        var series = new GrowthTechSeries(nameKey, descriptionKey, iconPath, row);
        int previousTech = 0;
        for (int level = 1; level <= FiniteGrowthLevelCount; level++) {
            TechProto tech = RegisterGrowthTech(
                baseTechId + level, series, previousTech,
                level - 1, FiniteGrowthCosts[level - 1],
                new GrowthTechLevel(level, level));
            tech.IconTag = $"fe-growth-{baseTechId}";
            previousTech = tech.ID;
        }
    }

    private static TechProto RegisterGrowthTech(int techId, GrowthTechSeries series,
        int previousTech, int stage, int matrixCost, GrowthTechLevel levelDefinition) {
        TechProto reference = MatrixResearchCosts.GetReference(stage);
        string nodeNameKey = $"{series.NameKey} {levelDefinition.Level}";
        TechProto tech = ProtoRegistry.RegisterTech(
            techId, nodeNameKey, series.DescriptionKey, "FE成长科技结果", series.IconPath,
            previousTech > 0 ? [previousTech] : [],
            (int[])reference.Items.Clone(), (int[])reference.ItemPoints.Clone(), reference.HashNeeded,
            [], GetGrowthTechPosition(series.Row, levelDefinition.Level - 1));
        GrowthTechPositions[tech.ID] = (series.Row, levelDefinition.Level - 1);
        tech.Level = levelDefinition.Level;
        tech.MaxLevel = levelDefinition.MaxLevel;
        ResearchCosts[tech.ID] = (stage, matrixCost, levelDefinition.LinearCostIncrement);
        MatrixResearchCosts.Apply(tech, stage, matrixCost, levelDefinition.LinearCostIncrement);
        tech.AddItems = [];
        tech.AddItemCounts = [];
        tech.IsHiddenTech = true;
        tech.IsObsolete = false;
        return tech;
    }

    /// <summary>在其他模组改表结束后刷新成本、矩阵前置和物流科技的依赖关系。</summary>
    public static void FinalizeResearchCosts() {
        foreach (var entry in ResearchCosts) {
            TechProto tech = LDB.techs.Select(entry.Key);
            MatrixResearchCosts.Apply(tech, entry.Value.Stage, entry.Value.Budget, entry.Value.Increment);
            tech.Preload2();
        }
        foreach (var pair in new[] { (TFE行星内物流交互, T行星物流系统), (TFE星际物流交互, T星际物流系统) }) {
            TechProto tech = LDB.techs.Select(pair.Item1);
            TechProto reference = LDB.techs.Select(pair.Item2);
            tech.Items = (int[])reference.Items.Clone();
            tech.ItemPoints = (int[])reference.ItemPoints.Clone();
            tech.HashNeeded = reference.HashNeeded;
            tech.PropertyOverrideItems = (int[])reference.PropertyOverrideItems.Clone();
            tech.PropertyItemCounts = (int[])reference.PropertyItemCounts.Clone();
            tech.Preload2();
        }
        for (int id = 3311; id <= 3315; id++) {
            TechProto tech = LDB.techs.Select(id);
            MatrixResearchCosts.Retier(tech, 2);
            tech.Preload2();
        }
        for (int id = 3801; id <= 3803; id++) {
            TechProto tech = LDB.techs.Select(id);
            MatrixResearchCosts.Retier(tech, 2);
            tech.PreTechs = (tech.PreTechs ?? []).Where(value => value is < 3508 or > 3510)
                .Append(3315).Distinct().ToArray();
            tech.Preload2();
        }
        RefreshStackTechLinks();
    }

    /// <summary>返回独立于科研材料的阶段身份，非成长科技返回零。</summary>
    private static int GetResearchStage(int techId) => ResearchCosts.TryGetValue(techId, out var cost)
        ? cost.Stage + 1 : 0;

    private static void RefreshStackTechLinks() {
        if (LDB.techs?.dataArray == null || LDB.techs.Select(TFE分馏堆叠成长 + 1) == null) {
            return;
        }

        int[] affectedTechIds = [
            3315, 3801, 3802, 3803, T信息矩阵, 3508, 3509, 3510,
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

    /// <summary>按当前原型校准FE科研工作量与白糖计数，保持未完成节点可继续结算，不重复授予效果。</summary>
    public static void SyncResearchWorkload() {
        Dictionary<int, TechState> states = GameMain.history?.techStates;
        if (states == null) { return; }
        NormalizeEfficiencyResearch();
        for (int i = GameMain.history.techQueue.Length - 1; i >= 0; i--) {
            int id = GameMain.history.techQueue[i];
            if (id > TFE探索信号增幅 && id <= TFE探索信号增幅 + FiniteGrowthLevelCount
                || id is TFE终极探索 or TFE终极仓储) { GameMain.history.RemoveTechInQueue(i); }
        }
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

    /// <summary>按原型升级能效状态；旧白糖单级完成只折算一次白糖升级，不直接获得满级。</summary>
    private static void NormalizeEfficiencyResearch() {
        GameHistoryData history = GameMain.history;
        for (int offset = 1; offset <= 6; offset++) {
            int id = TFE亚空间能效 + offset;
            TechProto tech = LDB.techs.Select(id);
            if (!history.techStates.TryGetValue(id, out TechState state)) { continue; }
            if (offset == 6 && state.maxLevel != tech.MaxLevel) {
                state.curLevel = tech.Level + (state.unlocked ? 1 : 0);
                if (state.unlocked) { state.hashUploaded = 0; }
                state.unlocked = false;
            } else if (offset < 6) {
                state.curLevel = tech.Level;
            }
            state.maxLevel = tech.MaxLevel;
            if (tech.IsObsolete) { state.unlocked = false; state.hashUploaded = 0; }
            history.techStates[id] = state;
        }
        for (int i = history.techQueue.Length - 1; i >= 0; i--) {
            if (history.techQueue[i] == TFE亚空间能效 + 1) { history.RemoveTechInQueue(i); }
        }
    }

    /// <summary>校准单级堆叠节点；退休节点退出队列且不补偿，不从旧curLevel猜测完成状态。</summary>
    public static void NormalizeStackResearch() {
        GameHistoryData history = GameMain.history;
        if (history == null || !ResearchManager.BelongsToGeneration3) { return; }
        for (int level = 1; level <= 5; level++) {
            int techId = TFE分馏堆叠成长 + level;
            TechProto tech = LDB.techs.Select(techId);
            if (tech == null || !history.techStates.TryGetValue(techId, out TechState state)) { continue; }
            state.curLevel = tech.Level;
            state.maxLevel = tech.MaxLevel;
            state.hashNeeded = tech.GetHashNeeded(tech.Level);
            if (tech.IsObsolete) {
                state.unlocked = false;
                state.hashUploaded = 0;
            }
            history.techStates[techId] = state;
        }
        for (int i = history.techQueue.Length - 1; i >= 0; i--) {
            int techId = history.techQueue[i];
            if (techId > TFE分馏堆叠成长 && techId <= TFE分馏堆叠成长 + 5
                && LDB.techs.Select(techId)?.IsObsolete == true) {
                history.RemoveTechInQueue(i);
            }
        }
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
    private static void AddUltimateTech(int id, string name) {
        TechProto tech = ProtoRegistry.RegisterTech(id, name, "终极任务科技描述", "FE成长科技结果",
            "Assets/fe/tech分馏数据中心", [], [], [], 0, [], Vector2.zero);
        tech.Level = 11;
        tech.MaxLevel = 11;
        tech.IsHiddenTech = true;
    }

    /// <summary>获取第一页物流交互节点的空位，沿用当前启用模组的基础布局。</summary>
    /// <param name="interstellar">为true时定位星际交互，否则定位行星交互。</param>
    /// <returns>对应第一页基准的科技原型坐标。</returns>
    private static Vector2 GetInteractionTechPosition(bool interstellar) {
        if (GenesisBook.Enable) {
            return GetTechPosition(1, interstellar ? -6 : -7, -1);
        }
        if (OrbitalRing.Enable) {
            return GetTechPosition(1, interstellar ? -2 : -5, interstellar ? 13 : 5);
        }
        return GetTechPosition(1, -6, interstellar ? 8 : 6);
    }

    /// <summary>获取第二页成长节点的位置；深空元驱动链启用时，整个成长块下移两行。</summary>
    /// <param name="row">相对成长首行的行索引，向下为正。</param>
    /// <param name="column">矩阵或终极节点的列索引，向右为正。</param>
    /// <returns>当前第二页基准下的科技原型坐标。</returns>
    private static Vector2 GetGrowthTechPosition(int row, int column) =>
        GetTechPosition(2, FirstGrowthRow + row + (TheyComeFromVoid.Enable ? 2 : 0), column);

    /// <summary>以当前页基准科技的实际Position为原点，按4单位网格计算节点位置。</summary>
    /// <param name="page">玩家看到的页数，1为主科技页，2为升级页。</param>
    /// <param name="row">相对页基准的网格行，向下为正。</param>
    /// <param name="column">相对页基准的网格列，向右为正。</param>
    /// <returns>以游戏科技树坐标表示的位置。</returns>
    private static Vector2 GetTechPosition(int page, int row, int column) {
        int originTechId = page switch {
            1 => T戴森球计划,
            2 => T宇宙探索,
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, "科技页数必须为1或2。"),
        };
        return LDB.techs.Select(originTechId).Position + new Vector2(column * 4, -row * 4);
    }
}
