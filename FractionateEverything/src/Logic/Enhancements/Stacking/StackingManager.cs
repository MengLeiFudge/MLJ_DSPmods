using System;
using System.IO;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using static FE.Utils.Utils;

namespace FE.Logic.Enhancements.Stacking;

/// <summary>
/// 统一堆叠系统。普通档在实际集装设备解锁前维持1；已玩过档在首级FE成长科技前维持1。
/// </summary>
public static class StackingManager {
    public const int LockedMaxStack = 1;
    public const int BaseUnlockedMaxStack = 4;
    public const int AbsoluteMaxStack = 20;
    public static readonly int[] StackMilestones = [4, 8, 12, 16, 20];
    private static readonly int[] HiddenSorterTechs = [
        T集装分拣器改良, T集装分拣器改良 + 1, T集装分拣器改良 + 2,
        T集装分拣器改良 + 3, T集装分拣器改良 + 4, T集装分拣器改良 + 5
    ];
    private static readonly int[] HiddenStationPilerTechs = [
        T运输站集装物流, T运输站集装物流 + 1, T运输站集装物流 + 2
    ];
    private static int configuredMaxStack = BaseUnlockedMaxStack;
    private static int lastSyncedStack = -1;
    private static bool lastSyncedUnlocked;

    public static bool IsUnlocked => GameMain.history != null
        && (GrowthTechnologyManager.UsesExperiencedStackProgression
            ? GameMain.history.TechUnlocked(TFE分馏堆叠成长 + 1)
            : HasPilingTechnology);

    /// <summary>当前科技树已经允许制造自动集装机或集装分拣器，不绑定原版科技编号。</summary>
    public static bool HasPilingTechnology => GameMain.history != null
        && (IsPilingItemUnlocked(I自动集装机) || IsPilingItemUnlocked(I集装分拣器));

    /// <summary>检查物品仍有集装语义，并使用当前配方解锁状态判断制造资格。</summary>
    /// <param name="itemId">集装设备物品身份。</param>
    /// <returns>设备有效且任一制造配方已解锁时为true。</returns>
    private static bool IsPilingItemUnlocked(int itemId) {
        PrefabDesc prefab = LDB.items.Select(itemId)?.prefabDesc;
        return prefab != null && (prefab.isPiler || prefab.isInserter && prefab.inserterCanStack)
               && GameMain.history.ItemUnlocked(itemId);
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
        SetHiddenTechs(HiddenSorterTechs);
        SetHiddenTechs(HiddenStationPilerTechs);
    }

    public static void SyncRuntimeState() {
        bool unlocked = IsUnlocked;
        int stack = CurrentMaxStack;
        if (GameMain.history != null && unlocked) {
            MarkTechRangeCompletedWithoutUnlockFunctions(HiddenSorterTechs);
            MarkTechRangeCompletedWithoutUnlockFunctions(HiddenStationPilerTechs);
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

    private static void SetHiddenTechs(int[] techIds) {
        foreach (int techId in techIds) {
            TechProto tech = LDB.techs.Select(techId);
            if (IsStackOnlyTech(tech)) { tech.IsHiddenTech = true; }
        }
    }

    /// <summary>只接管纯堆叠升级，防止相同编号被其他模组改作其他用途或附带额外奖励。</summary>
    /// <param name="tech">当前科技原型。</param>
    /// <returns>所有解锁效果均由统一堆叠系统负责时为true。</returns>
    private static bool IsStackOnlyTech(TechProto tech) => tech?.UnlockFunctions?.Length > 0
        && Array.TrueForAll(tech.UnlockFunctions, function => function is 29 or 39 or 40 or 41)
        && tech.UnlockRecipes.Length == 0 && tech.AddItems.Length == 0;

    private static void MarkTechRangeCompletedWithoutUnlockFunctions(int[] techIds) {
        GameHistoryData history = GameMain.history;
        foreach (int techId in techIds) {
            if (history == null || !history.techStates.ContainsKey(techId)
                || !IsStackOnlyTech(LDB.techs.Select(techId))) {
                continue;
            }

            TechState state = history.techStates[techId];
            if (state.unlocked && state.curLevel >= state.maxLevel) {
                continue;
            }

            state.unlocked = true;
            state.curLevel = state.maxLevel;
            state.hashUploaded = state.hashNeeded;
            state.unlockTick = GameMain.gameTick;
            history.techStates[techId] = state;
        }
    }

    private static void SyncHistoryStackFields(int stack) {
        GameHistoryData history = GameMain.history;
        bool inserterChanged = history.inserterStackInput != stack
                               || history.inserterStackOutput != stack
                               || !history.inserterBidirectional;
        if (inserterChanged) {
            history.inserterStackInput = stack;
            history.inserterStackOutput = stack;
            history.inserterBidirectional = true;
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
