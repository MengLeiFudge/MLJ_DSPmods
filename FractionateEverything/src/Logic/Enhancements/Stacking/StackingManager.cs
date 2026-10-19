using System;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using static FE.Utils.Utils;

namespace FE.Logic.Enhancements.Stacking;

/// <summary>
/// 分馏塔随物流集装科技从单层成长；八层研究完成后统一接管集装设备。
/// </summary>
public static class StackingManager {
    public const int LockedMaxStack = 1;
    public const int BaseUnlockedMaxStack = 4;
    public const int AbsoluteMaxStack = 20;
    private static readonly int[] SorterTechs = [
        T集装分拣器改良, T集装分拣器改良 + 1, T集装分拣器改良 + 2,
        T集装分拣器改良 + 3, T集装分拣器改良 + 4
    ];
    private static readonly int[] StationPilerTechs = [
        T运输站集装物流, T运输站集装物流 + 1, T运输站集装物流 + 2
    ];
    private static int configuredMaxStack = LockedMaxStack;
    private static int lastSyncedStack = -1;
    private static bool lastSyncedUnlocked;
    /// <summary>只有已完成八层科技才接管原版集装设备，制造设备的资格不触发接管。</summary>
    public static bool IsUnlocked => GameMain.history?.TechUnlocked(TFE分馏堆叠成长 + 2) == true;

    /// <summary>分馏塔的基础层数只取物流集装的已完成科技，不读可能被旧FE覆盖的历史字段。</summary>
    public static int StationResearchStack {
        get {
            int stack = LockedMaxStack;
            foreach (int techId in StationPilerTechs) {
                if (GameMain.history?.TechUnlocked(techId) == true) { stack++; }
            }
            return stack;
        }
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

    /// <summary>已同步的层数仍须满足实际研究资格，旧存档或延迟快照不能提前授予高堆叠。</summary>
    public static int CurrentMaxStack => IsUnlocked
        ? Math.Min(Math.Max(8, configuredMaxStack),
            GameMain.history.TechUnlocked(TFE分馏堆叠成长 + 3) ? AbsoluteMaxStack : 8)
        : StationResearchStack;

    public static double CurrentVanillaRecipeTimeRatio => IsUnlocked
        ? 4.0 / CurrentMaxStack
        : 1.0;

    public static int GetFractionatorMaxStack() => CurrentMaxStack;

    public static int GetLogisticStationMaxStack() => IsUnlocked
        ? CurrentMaxStack : GameMain.history?.stationPilerLevel ?? LockedMaxStack;

    public static int GetCompatibleMaxStack(int vanillaMaxStack) => IsUnlocked
        ? Math.Max(vanillaMaxStack, CurrentMaxStack) : vanillaMaxStack;

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
        configuredMaxStack = LockedMaxStack;
        lastSyncedStack = -1;
        lastSyncedUnlocked = false;
    }

    private static int ClampConfiguredMaxStack(int stack) {
        if (stack >= AbsoluteMaxStack) { return AbsoluteMaxStack; }
        return stack >= 8 ? 8 : Math.Max(LockedMaxStack, Math.Min(BaseUnlockedMaxStack, stack));
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

    /// <summary>导入后修复旧FE提前写入的高堆叠；不触碰设备内或传送带上的真实货物。</summary>
    public static void RestoreVanillaStackFields() {
        GameHistoryData history = GameMain.history;
        if (history == null || IsUnlocked) { return; }
        int input = 2;
        int output = 1;
        foreach (int techId in SorterTechs) {
            if (!history.TechUnlocked(techId)) { continue; }
            TechProto tech = LDB.techs.Select(techId);
            for (int i = 0; i < tech.UnlockFunctions.Length; i++) {
                int value = (int)tech.UnlockValues[i];
                if (tech.UnlockFunctions[i] == 41) { input = Math.Max(input, value); }
                if (tech.UnlockFunctions[i] == 39) { output = Math.Max(output, value); }
            }
        }
        bool changed = false;
        if (history.inserterStackInput > BaseUnlockedMaxStack && history.inserterStackInput != input) {
            history.inserterStackInput = input;
            changed = true;
        }
        if (history.inserterStackOutput > BaseUnlockedMaxStack && history.inserterStackOutput != output) {
            history.inserterStackOutput = output;
            changed = true;
        }
        if (changed) { GameMain.data?.OnInserterTechChange(); }
        if (history.stationPilerLevel > BaseUnlockedMaxStack) {
            history.stationPilerLevel = StationResearchStack;
        }
    }

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
