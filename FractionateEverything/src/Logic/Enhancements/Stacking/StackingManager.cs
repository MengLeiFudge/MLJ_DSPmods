using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using static FE.Utils.Utils;

namespace FE.Logic.Enhancements.Stacking;

/// <summary>
/// 统一堆叠系统。集装设备研究提供四层起点，八至二十层由FE成长科技提供。
/// </summary>
public static class StackingManager {
    public const int LockedMaxStack = 1;
    public const int BaseUnlockedMaxStack = 4;
    public const int AbsoluteMaxStack = 20;
    public static readonly int[] StackMilestones = [4, 8, 12, 16, 20];
    private static readonly int[] SorterTechs = [
        T集装分拣器改良, T集装分拣器改良 + 1, T集装分拣器改良 + 2,
        T集装分拣器改良 + 3, T集装分拣器改良 + 4
    ];
    private static readonly int[] StationPilerTechs = [
        T运输站集装物流, T运输站集装物流 + 1, T运输站集装物流 + 2
    ];
    private static int configuredMaxStack = BaseUnlockedMaxStack;
    private static int lastSyncedStack = -1;
    private static bool lastSyncedUnlocked;
    /// <summary>去重后的集装触发科技；null表示尚未定位，空集合表示两设备都没有前置科技。</summary>
    private static IReadOnlyList<int> pilingTechnologyIds;
    /// <summary>当前定位结果所属的游戏历史，用于识别切档。</summary>
    private static GameHistoryData pilingHistory;
    /// <summary>定位时的物品原型数组，用于识别原型重载。</summary>
    private static ItemProto[] pilingItems;
    /// <summary>定位时的科技原型数组，用于识别原型重载。</summary>
    private static TechProto[] pilingTechs;

    public static bool IsUnlocked => GameMain.history != null && HasPilingTechnology;

    /// <summary>任一集装设备的主配方或显式前置科技已完成；其他产出配方的制造资格不参与判断。</summary>
    public static bool HasPilingTechnology {
        get {
            GameHistoryData history = GameMain.history;
            if (history == null) { return false; }
            IReadOnlyList<int> techIds = PilingTechnologyIds;
            for (int i = 0; i < techIds.Count; i++) {
                if (history.TechUnlocked(techIds[i])) { return true; }
            }
            return false;
        }
    }

    /// <summary>当前科技树中两个集装设备的去重前置科技，用于判定四层起点；不进入存档。</summary>
    public static IReadOnlyList<int> PilingTechnologyIds {
        get {
            // 原型预加载及各模组的最终修改先于游戏历史创建；菜单查询不得提前固定触发科技。
            GameHistoryData history = GameMain.history;
            if (history == null) { return Array.Empty<int>(); }
            if (pilingTechnologyIds == null || !ReferenceEquals(pilingHistory, history)
                || !ReferenceEquals(pilingItems, LDB.items.dataArray)
                || !ReferenceEquals(pilingTechs, LDB.techs.dataArray)) {
                pilingTechnologyIds = ResolvePilingTechnologies();
                pilingHistory = history;
                pilingItems = LDB.items.dataArray;
                pilingTechs = LDB.techs.dataArray;
            }
            return pilingTechnologyIds;
        }
    }

    /// <summary>收集两设备的前置科技，去除重复身份，并在本次定位时记录主配方与前置证据。</summary>
    /// <returns>只读的触发科技集合；两设备都没有前置科技时为空并记录警告。</returns>
    private static IReadOnlyList<int> ResolvePilingTechnologies() {
        TechProto sorter = GetPilingTechnology(I集装分拣器, out string sorterInfo);
        TechProto piler = GetPilingTechnology(I自动集装机, out string pilerInfo);
        bool sameTech = sorter != null && piler != null && sorter.ID == piler.ID;
        List<int> techIds = [];
        if (sorter != null) { techIds.Add(sorter.ID); }
        if (piler != null && !sameTech) { techIds.Add(piler.ID); }
        string message = $"集装触发科技定位：{sorterInfo}；{pilerInfo}；同一科技={sameTech}"
            + $"，触发科技=[{string.Join(",", techIds)}]";
        if (techIds.Count == 0) { LogWarning(message); } else { LogInfo(message); }
        return techIds.AsReadOnly();
    }

    /// <summary>读取设备主配方或显式覆盖形成的前置科技，并保留对应定位信息。</summary>
    /// <param name="itemId">集装设备物品ID。</param>
    /// <param name="description">含物品、主配方、覆盖值与触发科技身份的诊断文字。</param>
    /// <returns>设备的前置科技；设备不存在或没有前置时返回null。</returns>
    private static TechProto GetPilingTechnology(int itemId, out string description) {
        ItemProto item = LDB.items.Select(itemId);
        TechProto tech = item?.preTech;
        description = $"物品={itemId}/{item?.name ?? "无"}，maincraft={item?.maincraft?.ID ?? 0}/{item?.maincraft?.name ?? "无"}"
            + $"，PreTechOverride={item?.PreTechOverride ?? 0}，触发科技={tech?.ID ?? 0}/{tech?.name ?? "无"}";
        return tech;
    }

    public static int ConfiguredMaxStack {
        get => configuredMaxStack;
        set {
            int old = configuredMaxStack;
            configuredMaxStack = ClampConfiguredMaxStack(value);
            if (configuredMaxStack != old) {
                RefreshStackDependents();
            }
        }
    }

    public static int CurrentMaxStack => IsUnlocked
        ? Math.Max(BaseUnlockedMaxStack, ClampConfiguredMaxStack(configuredMaxStack))
        : LockedMaxStack;

    public static bool CanUpgradeStack() => IsUnlocked && configuredMaxStack < AbsoluteMaxStack;

    public static bool UpgradeStack() {
        if (!CanUpgradeStack()) {
            return false;
        }

        ConfiguredMaxStack = GetNextMilestone(configuredMaxStack);
        SyncRuntimeState();
        return true;
    }

    public static int GetNextMilestone(int stack) {
        foreach (int milestone in StackMilestones) {
            if (milestone > stack) {
                return milestone;
            }
        }
        return AbsoluteMaxStack;
    }

    public static double CurrentVanillaRecipeTimeRatio => IsUnlocked
        ? 4.0 / CurrentMaxStack
        : 1.0;

    public static int GetFractionatorMaxStack() => CurrentMaxStack;

    public static int GetLogisticStationMaxStack() => CurrentMaxStack;

    public static int GetCompatibleMaxStack(int vanillaMaxStack) => Math.Max(vanillaMaxStack, CurrentMaxStack);

    public static int ClampStack(int stack) => Math.Max(LockedMaxStack, Math.Min(AbsoluteMaxStack, stack));

    public static void ApplyVanillaTechProtoOverrides() {
        RestoreVisibleTechs(SorterTechs);
        RestoreVisibleTechs(StationPilerTechs);
        // 第六级只开启双向运输，独立显示并由玩家研究，不属于堆叠数量成长。
        TechProto bidirectionalTech = LDB.techs.Select(T集装分拣器改良 + 5);
        if (IsStackOnlyTech(bidirectionalTech)) { bidirectionalTech.IsHiddenTech = false; }
    }

    public static void SyncRuntimeState() {
        bool unlocked = IsUnlocked;
        int stack = CurrentMaxStack;
        if (GameMain.history != null && unlocked && stack > BaseUnlockedMaxStack) {
            SyncHistoryStackFields(stack);
        }

        if (stack != lastSyncedStack || unlocked != lastSyncedUnlocked) {
            lastSyncedStack = stack;
            lastSyncedUnlocked = unlocked;
            RefreshStackDependents();
        }
    }

    public static void Import(BinaryReader r) {
        pilingTechnologyIds = null;
        r.ReadBlocks(
            ("ConfiguredMaxStack", br => { configuredMaxStack = ClampConfiguredMaxStack(br.ReadInt32()); })
        );
        lastSyncedStack = -1;
        lastSyncedUnlocked = false;
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("ConfiguredMaxStack", bw => bw.Write(configuredMaxStack))
        );
    }

    public static void IntoOtherSave() {
        pilingTechnologyIds = null;
        pilingHistory = null;
        pilingItems = null;
        pilingTechs = null;
        configuredMaxStack = BaseUnlockedMaxStack;
        lastSyncedStack = -1;
        lastSyncedUnlocked = false;
    }

    private static int ClampConfiguredMaxStack(int stack) {
        int clamped = Math.Max(BaseUnlockedMaxStack, Math.Min(AbsoluteMaxStack, stack));
        foreach (int milestone in StackMilestones) {
            if (clamped <= milestone) {
                return milestone;
            }
        }
        return AbsoluteMaxStack;
    }

    private static void RestoreVisibleTechs(int[] techIds) {
        foreach (int techId in techIds) {
            TechProto tech = LDB.techs.Select(techId);
            if (IsStackOnlyTech(tech)) { tech.IsHiddenTech = false; }
        }
    }

    /// <summary>只接管纯堆叠升级，防止相同编号被其他模组改作其他用途或附带额外奖励。</summary>
    /// <param name="tech">当前科技原型。</param>
    /// <returns>所有解锁效果均由统一堆叠系统负责时为true。</returns>
    private static bool IsStackOnlyTech(TechProto tech) => tech?.UnlockFunctions?.Length > 0
        && Array.TrueForAll(tech.UnlockFunctions, function => function is 29 or 39 or 40 or 41)
        && tech.UnlockRecipes.Length == 0 && tech.AddItems.Length == 0;

    private static void SyncHistoryStackFields(int stack) {
        GameHistoryData history = GameMain.history;
        bool bidirectional = history.inserterBidirectional || history.TechUnlocked(T集装分拣器改良 + 5);
        bool inserterChanged = history.inserterStackInput != stack
                               || history.inserterStackOutput != stack
                               || history.inserterBidirectional != bidirectional;
        if (inserterChanged) {
            history.inserterStackInput = stack;
            history.inserterStackOutput = stack;
            history.inserterBidirectional = bidirectional;
            GameMain.data?.OnInserterTechChange();
        }

        if (history.stationPilerLevel != stack) {
            history.stationPilerLevel = stack;
        }
    }

    private static void RefreshStackDependents() {
        ProcessManager.RefreshFractionatorRuntimeConfig();
        FractionatorManager.SetFractionatorCacheSize();
        FE.Logic.Enhancements.VanillaRecipes.VanillaRecipeManager.SyncRuntimeStateAfterImport();
    }
}
