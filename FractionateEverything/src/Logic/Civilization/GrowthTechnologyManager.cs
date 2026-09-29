using System;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Enhancements.Stacking;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 定义科技树第二页的成长科技，并把完成状态应用到运行规则。
/// </summary>
public static class GrowthTechnologyManager {
    private static readonly int[] MatrixItems = [
        I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵,
    ];
    private static readonly int[] MatrixTechs = [
        T电磁矩阵, T能量矩阵, T结构矩阵, T信息矩阵, T引力矩阵, T宇宙矩阵,
    ];
    private static readonly int[] ExperiencedStackMatrices = [
        I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵,
    ];
    private static readonly int[] ExperiencedStackCosts = [1200, 2400, 3600, 4800, 6000];
    private static readonly int[] StandardStackMatrices = [
        I电磁矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I宇宙矩阵,
    ];
    private static readonly int[] StandardStackCosts = [1200, 6000, 8000, 10000, 40000];
    private static readonly TechProto[] StackTechs = new TechProto[6];
    private static bool stackProgressionModeApplied;

    /// <summary>当前存档是否采用已玩过档案的堆叠路线。</summary>
    public static bool UsesExperiencedStackProgression { get; private set; }

    /// <summary>获取亚空间扩容科技等级，范围 0..9。</summary>
    public static int SubspaceCapacityLevel => GetUnlockedLevel(TFE亚空间扩容, 9);

    /// <summary>获取亚空间存取能效科技等级，范围 0..5。</summary>
    public static int SubspaceEfficiencyLevel => GetUnlockedLevel(TFE亚空间能效, 5);

    /// <summary>获取探索信号成长科技等级，范围 0..5。</summary>
    public static int ExplorationLevel => GetUnlockedLevel(TFE探索信号增幅, 5);

    /// <summary>获取高级矿物塔特质科技等级，范围 0..5。</summary>
    public static int AdvancedMineralTraitLevel => GetUnlockedLevel(TFE高级矿物特质, 5);

    /// <summary>获取高级交互塔特质科技等级，范围 0..5。</summary>
    public static int AdvancedInteractionTraitLevel => GetUnlockedLevel(TFE高级交互特质, 5);

    /// <summary>获取高级转化塔特质科技等级，范围 0..5。</summary>
    public static int AdvancedConversionTraitLevel => GetUnlockedLevel(TFE高级转化特质, 5);

    /// <summary>
    /// 应用当前存档的堆叠成长路线；五个稳定科技原型始终存在，普通路线隐藏 4 堆叠节点。
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
            bool firstStandardLevel = !experienced && level == 1;
            int matrixItemId = experienced ? ExperiencedStackMatrices[level - 1] : StandardStackMatrices[level - 1];
            int matrixCost = experienced ? ExperiencedStackCosts[level - 1] : StandardStackCosts[level - 1];
            int previousTech = level == 1 || !experienced && level == 2
                ? TFE分馏数据中心
                : TFE分馏堆叠成长 + level - 1;
            tech.IsHiddenTech = firstStandardLevel;
            tech.PreTechs = !experienced && level == 2
                ? [previousTech, T集装物流系统]
                : [previousTech];
            tech.PreTechsImplicit = [GetMatrixTechId(matrixItemId)];
            tech.Items = [matrixItemId];
            tech.ItemPoints = [1];
            tech.PropertyOverrideItems = [matrixItemId];
            tech.PropertyItemCounts = [matrixCost];
            tech.HashNeeded = (long)matrixCost * 3600L;
        }
        RefreshStackTechLinks();
        stackProgressionModeApplied = true;
        SyncUnstartedStackTechStates();
    }

    /// <summary>
    /// 普通路线的隐藏 4 堆叠节点不能通过脚本或队列入口研究。
    /// </summary>
    public static bool CanResearchStackTech(int techId) =>
        UsesExperiencedStackProgression || techId != TFE分馏堆叠成长 + 1;

    /// <summary>
    /// 注册成长科技的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("FE分馏堆叠成长", "Fractionation Stack Growth", "分馏堆叠成长");
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
            "Raises Frag drop chance and reduces the guaranteed exploration-signal interval by 10%.",
            "提高 Frag 掉落率，并使探索信号定时保底间隔缩短 10%。");
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
    /// 注册第二页成长科技链。
    /// </summary>
    public static void AddTechs() {
        AddInfrastructureTechs();
        AddStackTechs();
        AddSeries(TFE亚空间扩容, 9, "FE亚空间扩容", "FE亚空间扩容描述",
            "Assets/fe/tech分馏数据中心",
            [600, 1200, 1800, 2400, 3000, 4000, 8000, 16000, 32000], 1);
        AddSeries(TFE亚空间能效, 5, "FE亚空间能效", "FE亚空间能效描述",
            "Assets/fe/tech分馏数据中心", [1200, 2400, 3600, 4800, 6000], 6);
        AddSeries(TFE探索信号增幅, 5, "FE探索信号增幅", "FE探索信号增幅描述",
            "Assets/fe/tech物品分解", [1200, 2400, 3600, 4800, 6000], 2);
        AddSeries(TFE高级矿物特质, 5, "FE高级矿物特质", "FE高级矿物特质描述",
            "Assets/fe/tech资源复制", [1200, 2400, 3600, 4800, 6000], 3);
        AddSeries(TFE高级交互特质, 5, "FE高级交互特质", "FE高级交互特质描述",
            "Assets/fe/tech物品交互", [1200, 2400, 3600, 4800, 6000], 4);
        AddSeries(TFE高级转化特质, 5, "FE高级转化特质", "FE高级转化特质描述",
            "Assets/fe/tech物品转化", [1200, 2400, 3600, 4800, 6000], 5);
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
        TechProto root = ProtoRegistry.RegisterTech(
            TFE分馏数据中心, "T分馏数据中心", "分馏数据中心描述", "分馏数据中心结果",
            "Assets/fe/tech分馏数据中心", GenesisBook.Enable ? [TGB科学理论] : [T电磁学],
            [], [], 3600L, [RFE基础矿物塔, RFE基础解析塔], GetTechPosition(6, 0));
        root.PreTechsImplicit = [T电磁矩阵];
        root.AddItems = [];
        root.AddItemCounts = [];
        root.PropertyOverrideItems = [];
        root.PropertyItemCounts = [];
        root.IsHiddenTech = true;
        root.IconTag = "flsjzx";

        TechProto planetaryVanilla = LDB.techs.Select(T行星物流系统);
        TechProto planetary = ProtoRegistry.RegisterTech(
            TFE行星内物流交互, "T行星内物流交互", "行星内物流交互描述", "行星内物流交互结果",
            planetaryVanilla.IconPath, [TFE分馏数据中心, T行星物流系统],
            [..planetaryVanilla.Items], [..planetaryVanilla.ItemPoints], planetaryVanilla.HashNeeded,
            [RFE行星内物流交互站], GetTechPosition(6, 2));
        planetary.PreTechsImplicit = [T行星物流系统];
        planetary.PropertyOverrideItems = [..planetaryVanilla.PropertyOverrideItems];
        planetary.PropertyItemCounts = [..planetaryVanilla.PropertyItemCounts];
        planetary.IconTag = "txxnjh";

        TechProto interstellarVanilla = LDB.techs.Select(T星际物流系统);
        TechProto interstellar = ProtoRegistry.RegisterTech(
            TFE星际物流交互, "T星际物流交互", "星际物流交互描述", "星际物流交互结果",
            interstellarVanilla.IconPath, [TFE分馏数据中心, T星际物流系统],
            [..interstellarVanilla.Items], [..interstellarVanilla.ItemPoints], interstellarVanilla.HashNeeded,
            [RFE星际物流交互站], GetTechPosition(6, 3));
        interstellar.PreTechsImplicit = [T星际物流系统];
        interstellar.PropertyOverrideItems = [..interstellarVanilla.PropertyOverrideItems];
        interstellar.PropertyItemCounts = [..interstellarVanilla.PropertyItemCounts];
        interstellar.IconTag = "txjjh";
    }

    private static void AddStackTechs() {
        bool experienced = PermanentProfileStore.HasPlayedFractionation;
        int previousTech = TFE分馏数据中心;
        for (int level = 1; level <= 5; level++) {
            int stack = level * 4;
            int techId = TFE分馏堆叠成长 + level;
            string name = $"FE分馏堆叠成长 {stack}";
            Register(name, $"Stack Capacity {stack}", $"堆叠容量 {stack}");
            TechProto tech = RegisterGrowthTech(techId, name, "FE分馏堆叠成长描述",
                "Assets/fe/tech资源复制", previousTech, ExperiencedStackMatrices[level - 1],
                ExperiencedStackCosts[level - 1], 0, level);
            tech.IconTag = "fe-stack-growth";
            StackTechs[level] = tech;
            previousTech = techId;
        }
        SetStackProgressionMode(experienced);
    }

    private static void AddSeries(int baseTechId, int levels, string nameKey, string descriptionKey,
        string iconPath, int[] costs, int row) {
        int previousTech = TFE分馏数据中心;
        for (int level = 1; level <= levels; level++) {
            int matrixIndex = Math.Min(MatrixItems.Length - 1,
                (level - 1) * MatrixItems.Length / Math.Max(1, levels));
            int techId = baseTechId + level;
            string levelName = $"{nameKey} {level}";
            Register(levelName,
                $"{GetSeriesEnglishName(nameKey)} {level}",
                $"{GetSeriesChineseName(nameKey)} {level}");
            TechProto tech = RegisterGrowthTech(techId, levelName, descriptionKey, iconPath,
                previousTech, MatrixItems[matrixIndex], costs[level - 1], row, level);
            tech.IconTag = $"fe-growth-{baseTechId}";
            previousTech = techId;
        }
    }

    private static TechProto RegisterGrowthTech(int techId, string nameKey, string descriptionKey,
        string iconPath, int previousTech, int matrixItemId, int matrixCost, int row, int column) {
        int matrixTechId = GetMatrixTechId(matrixItemId);
        TechProto tech = ProtoRegistry.RegisterTech(
            techId, nameKey, descriptionKey, "FE成长科技结果", iconPath,
            previousTech > 0 ? [previousTech] : [],
            [matrixItemId], [1], (long)matrixCost * 3600L,
            [], GetTechPosition(row, column));
        tech.PreTechsImplicit = matrixTechId > 0 ? [matrixTechId] : [];
        tech.AddItems = [];
        tech.AddItemCounts = [];
        tech.PropertyOverrideItems = [matrixItemId];
        tech.PropertyItemCounts = [matrixCost];
        tech.IsHiddenTech = false;
        return tech;
    }

    private static void RefreshStackTechLinks() {
        if (LDB.techs?.dataArray == null || LDB.techs.Select(TFE分馏堆叠成长 + 1) == null) {
            return;
        }
        int[] affectedTechIds = [
            TFE分馏数据中心,
            T集装物流系统,
            TFE分馏堆叠成长 + 1,
            TFE分馏堆叠成长 + 2,
            TFE分馏堆叠成长 + 3,
            TFE分馏堆叠成长 + 4,
            TFE分馏堆叠成长 + 5,
        ];
        for (int level = 1; level <= 5; level++) {
            LDB.techs.Select(TFE分馏堆叠成长 + level)?.Preload2();
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
            if (tech == null || !history.techStates.TryGetValue(techId, out TechState state)
                || state.unlocked || state.hashUploaded > 0L) {
                continue;
            }
            state.hashNeeded = tech.GetHashNeeded(state.curLevel);
            history.techStates[techId] = state;
        }
    }

    private static int GetMatrixTechId(int matrixItemId) {
        int index = Array.IndexOf(MatrixItems, matrixItemId);
        return index >= 0 ? MatrixTechs[index] : 0;
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

    private static string GetSeriesEnglishName(string nameKey) {
        return nameKey switch {
            "FE亚空间扩容" => "Subspace Capacity",
            "FE亚空间能效" => "Subspace Transfer Efficiency",
            "FE探索信号增幅" => "Exploration Signal Amplification",
            "FE高级矿物特质" => "Rare Catalyst Stability",
            "FE高级交互特质" => "Embryo Breeding Activity",
            "FE高级转化特质" => "Replacement Recovery",
            _ => nameKey,
        };
    }

    private static string GetSeriesChineseName(string nameKey) {
        return nameKey switch {
            "FE亚空间扩容" => "亚空间扩容",
            "FE亚空间能效" => "亚空间存取能效",
            "FE探索信号增幅" => "探索信号增幅",
            "FE高级矿物特质" => "珍奇催化稳定性",
            "FE高级交互特质" => "原胚繁育活性",
            "FE高级转化特质" => "置换回收效率",
            _ => nameKey,
        };
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
