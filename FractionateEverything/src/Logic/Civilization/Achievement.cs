using FE.Logic.Fractionation.Recipes;
using System.Collections.Generic;
using System;
using System.IO;
using FE.Utils;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Fractionation.Fractionators;
using FE.Compatibility.Nebula;
using static FE.Utils.Utils;
using System.Text;
using BepInEx;

namespace FE.Logic.Civilization.Achievements;

#region 成就定义

/// <summary>
/// 标识单存档成就使用的累计进度条件。
/// </summary>
public enum AchievementConditionType {
    /// <summary>已完成协议数量。</summary>
    CompletedProtocols,
    /// <summary>累计投入的远古科技点。</summary>
    SpentTechPoints,
    /// <summary>累计分馏成功次数。</summary>
    FractionationSuccesses,
}

/// <summary>
/// 标识成就完成后投影到配方运行时的固定奖励。
/// </summary>
public enum AchievementRewardType {
    /// <summary>指定配方类型成功率加成。</summary>
    RecipeTypeSuccessRate,
    /// <summary>全部配方成功率加成。</summary>
    AllRecipeSuccessRate,
}

/// <summary>
/// 普通成就完成时的固定触发来源字符串，不写入隐私字段。
/// </summary>
public static class AchievementTriggerSources {
    /// <summary>主机/单人低频 Tick 评估完成。</summary>
    public const string TickEvaluation = "tick-eval";
    /// <summary>旧 version10 仅 key 列表导入时合成的兼容元数据。</summary>
    public const string LegacyV10Import = "legacy-v10-import";
}

/// <summary>
/// 定义单存档成就的类型化条件和固定运行奖励。
/// </summary>
public sealed class AchievementDefinition(
    string achievementKey,
    string displayNameKey,
    AchievementConditionType conditionType,
    long target,
    AchievementRewardType rewardType,
    float rewardValue,
    ERecipe recipeType = 0) {
    /// <summary>稳定成就键；存档只依赖此键，不依赖显示名。</summary>
    public string AchievementKey { get; } = achievementKey;
    /// <summary>显示名翻译键。</summary>
    public string DisplayNameKey { get; } = displayNameKey;
    /// <summary>累计进度条件类型。</summary>
    public AchievementConditionType ConditionType { get; } = conditionType;
    /// <summary>达成条件所需的目标累计值。</summary>
    public long Target { get; } = target;
    /// <summary>完成后的固定奖励类型。</summary>
    public AchievementRewardType RewardType { get; } = rewardType;
    /// <summary>固定奖励数值。</summary>
    public float RewardValue { get; } = rewardValue;
    /// <summary>配方类型奖励目标；全配方奖励时可为 0。</summary>
    public ERecipe RecipeType { get; } = recipeType;
}

/// <summary>
/// 当前存档中一项普通成就的完成元数据。
/// </summary>
public sealed class AchievementCompletionRecord(
    string achievementKey,
    int unlockOrder,
    long gameTick,
    string triggerSource,
    long triggerValue) {
    /// <summary>成就键。</summary>
    public string AchievementKey { get; } = achievementKey;
    /// <summary>本档首次解锁顺序，从 1 开始。</summary>
    public int UnlockOrder { get; } = unlockOrder;
    /// <summary>完成时的游戏 tick；旧格式导入时为 0。</summary>
    public long GameTick { get; } = gameTick;
    /// <summary>固定触发来源字符串，不含存档名或玩家名。</summary>
    public string TriggerSource { get; } = triggerSource ?? "";
    /// <summary>触发时条件累计值快照；旧格式导入时为 0。</summary>
    public long TriggerValue { get; } = triggerValue;
}

#endregion // 成就定义

#region 成就目录

/// <summary>
/// 保存单存档普通成就定义，不把条件和奖励闭包放入 UI。
/// </summary>
public static class AchievementCatalog {
    private static readonly List<AchievementDefinition> definitions = [];

    /// <summary>当前已注册的普通成就定义，按展示顺序排列。</summary>
    public static IReadOnlyList<AchievementDefinition> All => definitions;

    /// <summary>
    /// 重建无阶段文明里程碑目录；保留既有 version10 键以兼容已完成状态。
    /// </summary>
    public static void Initialize() {
        definitions.Clear();
        definitions.Add(new("first-protocol", "成就-首项协议", AchievementConditionType.CompletedProtocols, 1,
            AchievementRewardType.RecipeTypeSuccessRate, 0.01f, ERecipe.Replication));
        // 保留 first-stage 键；条件改为协议数量里程碑，避免已完成 version10 状态丢失。
        definitions.Add(new("first-stage", "成就-协议进展", AchievementConditionType.CompletedProtocols, 4,
            AchievementRewardType.RecipeTypeSuccessRate, 0.01f, ERecipe.Conversion));
        definitions.Add(new("first-tech", "成就-首次科技投入", AchievementConditionType.SpentTechPoints, 1,
            AchievementRewardType.AllRecipeSuccessRate, 0.005f));
        definitions.Add(new("fractionation-1000", "成就-千次分馏", AchievementConditionType.FractionationSuccesses,
            1000, AchievementRewardType.AllRecipeSuccessRate, 0.005f));
    }
}

#endregion // 成就目录

#region 成就状态

/// <summary>
/// 保存当前存档已完成的普通成就及其首次解锁元数据。
/// </summary>
public static class AchievementState {
    /// <summary>单存档普通成就完成记录条数上限。</summary>
    public const int MaxRecordCount = 256;

    private static readonly Dictionary<string, AchievementCompletionRecord> completed = new();

    /// <summary>当前存档已完成的成就记录，按 UnlockOrder 再按 Key 排序。</summary>
    public static IReadOnlyList<AchievementCompletionRecord> Records => GetOrderedRecords();

    /// <summary>判断当前存档是否已完成指定普通成就。</summary>
    public static bool IsCompleted(string achievementKey) =>
        !string.IsNullOrEmpty(achievementKey) && completed.ContainsKey(achievementKey);

    /// <summary>尝试读取指定成就的完成元数据。</summary>
    public static bool TryGetRecord(string achievementKey, out AchievementCompletionRecord record) {
        if (string.IsNullOrEmpty(achievementKey)) {
            record = null;
            return false;
        }
        return completed.TryGetValue(achievementKey, out record);
    }

    /// <summary>
    /// 幂等记录普通成就首次完成元数据。
    /// 新记录的 UnlockOrder 取现有最大顺序 + 1（已达 int 上限时钳制为 int.MaxValue）。
    /// </summary>
    /// <param name="achievementKey">成就键。</param>
    /// <param name="gameTick">完成时游戏 tick。</param>
    /// <param name="triggerSource">固定触发来源，不得含隐私字段。</param>
    /// <param name="triggerValue">触发时条件累计值快照。</param>
    /// <returns>本次为首次完成时返回 true。</returns>
    public static bool Complete(string achievementKey, long gameTick, string triggerSource, long triggerValue) {
        if (string.IsNullOrEmpty(achievementKey) || completed.ContainsKey(achievementKey)) {
            return false;
        }
        if (completed.Count >= MaxRecordCount) {
            return false;
        }

        int unlockOrder = AllocateNextUnlockOrder();
        completed[achievementKey] = new AchievementCompletionRecord(
            achievementKey,
            unlockOrder,
            gameTick,
            string.IsNullOrEmpty(triggerSource) ? AchievementTriggerSources.TickEvaluation : triggerSource,
            triggerValue);
        return true;
    }

    /// <summary>
    /// 从存档导入普通成就完成状态。
    /// 兼容旧 version10 的 raw count+keys；Records 为子集时仍合并 Completed 中缺失的旧键为合成元数据。
    /// </summary>
    public static void Import(BinaryReader r) {
        completed.Clear();
        List<string> legacyKeys = [];
        r.ReadBlocks(
            ("Completed", br => {
                int count = br.ReadInt32();
                if (count < 0) {
                    count = 0;
                }
                count = Math.Min(count, MaxRecordCount);
                for (int i = 0; i < count; i++) {
                    string key = br.ReadString();
                    if (!string.IsNullOrEmpty(key)) {
                        legacyKeys.Add(key);
                    }
                }
            }),
            ("Records", br => {
                int count = br.ReadInt32();
                if (count < 0) {
                    count = 0;
                }
                count = Math.Min(count, MaxRecordCount);
                for (int i = 0; i < count; i++) {
                    string key = br.ReadString();
                    int unlockOrder = br.ReadInt32();
                    long gameTick = br.ReadInt64();
                    string triggerSource = br.ReadString() ?? "";
                    long triggerValue = br.ReadInt64();
                    if (string.IsNullOrEmpty(key) || completed.ContainsKey(key)) {
                        continue;
                    }
                    if (completed.Count >= MaxRecordCount) {
                        break;
                    }
                    if (unlockOrder <= 0) {
                        unlockOrder = AllocateNextUnlockOrder();
                    }
                    completed[key] = new AchievementCompletionRecord(
                        key, unlockOrder, gameTick, triggerSource, triggerValue);
                }
            }));

        // Records 无论是否非空，都把 Completed 中尚未覆盖的旧 key 合并为合成记录。
        foreach (string key in legacyKeys) {
            if (string.IsNullOrEmpty(key) || completed.ContainsKey(key)) {
                continue;
            }
            if (completed.Count >= MaxRecordCount) {
                break;
            }
            completed[key] = new AchievementCompletionRecord(
                key,
                AllocateNextUnlockOrder(),
                0L,
                AchievementTriggerSources.LegacyV10Import,
                0L);
        }
    }

    /// <summary>
    /// 导出普通成就完成键与完整元数据，保证可被再次导入。
    /// </summary>
    public static void Export(BinaryWriter w) {
        List<AchievementCompletionRecord> ordered = GetOrderedRecords();
        w.WriteBlocks(
            ("Completed", bw => {
                bw.Write(ordered.Count);
                foreach (AchievementCompletionRecord record in ordered) {
                    bw.Write(record.AchievementKey);
                }
            }),
            ("Records", bw => {
                bw.Write(ordered.Count);
                foreach (AchievementCompletionRecord record in ordered) {
                    bw.Write(record.AchievementKey);
                    bw.Write(record.UnlockOrder);
                    bw.Write(record.GameTick);
                    bw.Write(record.TriggerSource ?? "");
                    bw.Write(record.TriggerValue);
                }
            }));
    }

    /// <summary>切档时清空当前存档普通成就状态。</summary>
    public static void IntoOtherSave() => completed.Clear();

    /// <summary>
    /// 取现有最大 UnlockOrder + 1；已达 int.MaxValue 时钳制为 int.MaxValue。
    /// </summary>
    internal static int AllocateNextUnlockOrder() {
        int maxOrder = 0;
        foreach (AchievementCompletionRecord record in completed.Values) {
            if (record.UnlockOrder > maxOrder) {
                maxOrder = record.UnlockOrder;
            }
        }
        return maxOrder == int.MaxValue ? int.MaxValue : maxOrder + 1;
    }

    private static List<AchievementCompletionRecord> GetOrderedRecords() {
        List<AchievementCompletionRecord> ordered = [.. completed.Values];
        ordered.Sort(CompareRecords);
        return ordered;
    }

    private static int CompareRecords(AchievementCompletionRecord a, AchievementCompletionRecord b) {
        int orderCompare = a.UnlockOrder.CompareTo(b.UnlockOrder);
        return orderCompare != 0
            ? orderCompare
            : string.CompareOrdinal(a.AchievementKey, b.AchievementKey);
    }
}

#endregion // 成就状态

#region 成就服务

/// <summary>
/// 低频评估类型化条件，完成后立即让固定奖励生效。
/// </summary>
public static class AchievementService {
    private static long lastEvaluationTick = long.MinValue;

    /// <summary>
    /// 主机/单人低频评估普通成就；有新完成时返回 true。
    /// </summary>
    public static bool Tick() {
        long tick = GameMain.gameTick;
        if (tick >= 0 && lastEvaluationTick >= 0 && tick - lastEvaluationTick < 60) {
            return false;
        }
        lastEvaluationTick = tick;

        bool changed = false;
        foreach (AchievementDefinition definition in AchievementCatalog.All) {
            if (AchievementState.IsCompleted(definition.AchievementKey)) {
                continue;
            }
            long currentValue = GetCurrentValue(definition);
            if (currentValue < definition.Target) {
                continue;
            }
            changed |= AchievementState.Complete(
                definition.AchievementKey,
                tick,
                AchievementTriggerSources.TickEvaluation,
                currentValue);
        }
        return changed;
    }

    /// <summary>返回当前存档已完成的普通成就定义。</summary>
    public static IReadOnlyList<AchievementDefinition> GetCompletedDefinitions() {
        List<AchievementDefinition> result = [];
        foreach (AchievementDefinition definition in AchievementCatalog.All) {
            if (AchievementState.IsCompleted(definition.AchievementKey)) {
                result.Add(definition);
            }
        }
        return result;
    }

    /// <summary>查询指定成就当前累计进度值。</summary>
    public static long GetCurrentValue(AchievementDefinition definition) {
        return definition.ConditionType switch {
            AchievementConditionType.CompletedProtocols => CountCompletedProtocols(),
            AchievementConditionType.SpentTechPoints => AncientTechTreeState.TotalPointsSpent,
            AchievementConditionType.FractionationSuccesses => ProcessManager.totalFractionSuccesses,
            _ => 0,
        };
    }

    private static int CountCompletedProtocols() {
        int count = 0;
        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            // 普通成就只统计正式解锁，不统计仅 100% 购买资格。
            if (ProtocolProgressStore.IsFormallyUnlocked(definition.RecipeKey)) {
                count++;
            }
        }
        return count;
    }

    /// <summary>切档时重置低频评估时钟。</summary>
    public static void IntoOtherSave() {
        lastEvaluationTick = long.MinValue;
    }
}

#endregion // 成就服务

#region 成就模块

/// <summary>
/// 成就域的翻译、初始化、低频评估、存档和运行投影入口。
/// 秘密成就仅走本机 profile，不进入本模块的存档或联机快照。
/// </summary>
public static class AchievementModule {
    /// <summary>注册普通与秘密成就相关翻译。</summary>
    public static void AddTranslations() {
        Register("成就-首项协议", "First Recovered Protocol");
        Register("成就-协议进展", "Protocol Progress", "协议进展");
        Register("成就-首次科技投入", "First Ancient Technology");
        Register("成就-千次分馏", "One Thousand Fractionations");
        Register("秘密成就-旧日余响", "Echoes of the Past", "旧日余响");
        Register("秘密成就-旧日余响描述",
            "Load a save left behind by FE 2.x.",
            "载入一个由 FE 2.x 留下的存档。");
    }

    /// <summary>
    /// 初始化普通目录、秘密目录，并加载本机秘密 profile。
    /// </summary>
    public static void Initialize() {
        AchievementCatalog.Initialize();
        SecretAchievementCatalog.Initialize();
        SecretAchievementProfile.Load();
        RefreshRuntimeProjection();
    }

    /// <summary>
    /// 主机/单人 Tick 结算普通成就；客户端跳过。
    /// </summary>
    public static void Tick() {
        if (NebulaMultiplayerModAPI.IsMultiplayerActive && NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        if (!AchievementService.Tick()) {
            return;
        }

        RefreshRuntimeProjection();
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
    }

    /// <summary>导入当前存档普通成就状态；不读写秘密 profile。</summary>
    public static void Import(BinaryReader r) => AchievementState.Import(r);

    /// <summary>导出当前存档普通成就状态；不读写秘密 profile。</summary>
    public static void Export(BinaryWriter w) => AchievementState.Export(w);

    /// <summary>导入后重建普通成就奖励投影。</summary>
    public static void AfterImport() => RefreshRuntimeProjection();

    /// <summary>
    /// 切档清空普通成就状态并刷新投影；秘密 profile 跨档保留。
    /// </summary>
    public static void IntoOtherSave() {
        AchievementState.IntoOtherSave();
        AchievementService.IntoOtherSave();
        RefreshRuntimeProjection();
    }

    private static void RefreshRuntimeProjection() {
        RecipeModifierCache.Reset();
        foreach (AchievementDefinition definition in AchievementService.GetCompletedDefinitions()) {
            switch (definition.RewardType) {
                case AchievementRewardType.RecipeTypeSuccessRate:
                    RecipeModifierCache.AddSuccessRateBonus(definition.RecipeType, definition.RewardValue);
                    break;
                case AchievementRewardType.AllRecipeSuccessRate:
                    RecipeModifierCache.AddAllRecipeSuccessRateBonus(definition.RewardValue);
                    break;
            }
        }
        ProcessManager.RefreshFractionatorRuntimeConfig();
    }
}

#endregion // 成就模块

#region 秘密成就定义

/// <summary>
/// 秘密成就完成时的固定触发来源字符串，不写入隐私字段。
/// </summary>
public static class SecretAchievementSources {
    /// <summary>精确载入 FE 自有 version1 旧档。</summary>
    public const string Legacy2xSave = "legacy-2x-save";
}

/// <summary>
/// 定义本机跨存档秘密成就；无生产强度奖励。
/// </summary>
public sealed class SecretAchievementDefinition(
    string achievementKey,
    string displayNameKey,
    string descriptionKey) {
    /// <summary>稳定秘密成就键。</summary>
    public string AchievementKey { get; } = achievementKey;
    /// <summary>解锁后可见的名称翻译键。</summary>
    public string DisplayNameKey { get; } = displayNameKey;
    /// <summary>解锁后可见的描述翻译键。</summary>
    public string DescriptionKey { get; } = descriptionKey;
}

/// <summary>
/// 本机秘密成就的完成记录；不含存档名、玩家名等隐私字段。
/// </summary>
public sealed class SecretAchievementRecord(
    string achievementKey,
    long unlockedUtcTicks,
    string triggerSource) {
    /// <summary>秘密成就键。</summary>
    public string AchievementKey { get; } = achievementKey;
    /// <summary>本机解锁时的 UTC ticks。</summary>
    public long UnlockedUtcTicks { get; } = unlockedUtcTicks;
    /// <summary>固定触发来源枚举字符串。</summary>
    public string TriggerSource { get; } = triggerSource ?? "";
}

#endregion // 秘密成就定义

#region 秘密成就目录

/// <summary>
/// 本机秘密成就定义目录；未解锁项不进入可见列表。
/// </summary>
public static class SecretAchievementCatalog {
    /// <summary>FE 2.x 旧档纪念成就键。</summary>
    public const string Legacy2xSaveKey = "legacy-2x-save";

    private static readonly List<SecretAchievementDefinition> definitions = [];
    private static readonly Dictionary<string, SecretAchievementDefinition> byKey = new();

    /// <summary>全部秘密成就定义，含未解锁项。</summary>
    public static IReadOnlyList<SecretAchievementDefinition> All => definitions;

    /// <summary>
    /// 重建初版秘密目录。
    /// </summary>
    public static void Initialize() {
        definitions.Clear();
        byKey.Clear();
        Add(new(
            Legacy2xSaveKey,
            "秘密成就-旧日余响",
            "秘密成就-旧日余响描述"));
    }

    /// <summary>按键查找秘密成就定义。</summary>
    public static SecretAchievementDefinition Get(string achievementKey) =>
        !string.IsNullOrEmpty(achievementKey) && byKey.TryGetValue(achievementKey, out SecretAchievementDefinition definition)
            ? definition
            : null;

    /// <summary>
    /// 返回已解锁且因此可见的秘密成就；未解锁项不进入可见目录。
    /// </summary>
    public static IReadOnlyList<SecretAchievementDefinition> GetVisible() {
        List<SecretAchievementDefinition> visible = [];
        foreach (SecretAchievementDefinition definition in definitions) {
            if (SecretAchievementProfile.IsUnlocked(definition.AchievementKey)) {
                visible.Add(definition);
            }
        }
        return visible;
    }

    private static void Add(SecretAchievementDefinition definition) {
        definitions.Add(definition);
        byKey[definition.AchievementKey] = definition;
    }
}

#endregion // 秘密成就目录

#region 秘密成就本机档案

/// <summary>
/// 本机秘密成就 profile：跨存档保留，不进入存档/Nebula，不提供生产奖励。
/// </summary>
public static class SecretAchievementProfile {
    /// <summary>文件魔数 "FESA"。</summary>
    public const int FileMagic = 0x46534541;
    /// <summary>当前 profile 格式版本。</summary>
    public const int FileVersion = 1;
    /// <summary>单文件最多保留的秘密成就条数上限。</summary>
    public const int MaxEntryCount = 64;
    /// <summary>profile 文件最大字节数（64 KiB）；超限在任何 ReadString 前拒绝。</summary>
    public const int MaxFileBytes = 64 * 1024;
    /// <summary>key / triggerSource 最大字符数；越界视作损坏。</summary>
    public const int MaxStringChars = 128;

    private static readonly Dictionary<string, SecretAchievementRecord> unlocked = new();
    private static bool loaded;

    /// <summary>当前已解锁的秘密成就记录。</summary>
    public static IReadOnlyCollection<SecretAchievementRecord> UnlockedRecords => unlocked.Values;

    /// <summary>profile 文件完整路径：ConfigPath/&lt;Plugin GUID&gt;/secret-achievements.dat。</summary>
    public static string ProfilePath =>
        Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_GUID, "secret-achievements.dat");

    /// <summary>判断本机是否已解锁指定秘密成就。</summary>
    public static bool IsUnlocked(string achievementKey) =>
        !string.IsNullOrEmpty(achievementKey) && unlocked.ContainsKey(achievementKey);

    /// <summary>
    /// 从本机 profile 加载秘密成就；损坏或 I/O 失败时记录警告并保持进程可用。
    /// </summary>
    public static void Load() {
        unlocked.Clear();
        loaded = true;
        string path = ProfilePath;
        if (!File.Exists(path)) {
            return;
        }

        try {
            long fileLength = new FileInfo(path).Length;
            // 在打开 BinaryReader / 任何 ReadString 前按总字节数拒绝超限文件。
            if (fileLength > MaxFileBytes) {
                LogWarning(
                    $"[SecretAchievements] profile size {fileLength} exceeds {MaxFileBytes} bytes, ignored: {path}");
                return;
            }
            if (fileLength < sizeof(int) * 3) {
                LogWarning($"[SecretAchievements] profile too small, ignored: {path}");
                return;
            }

            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.UTF8);
            int magic = reader.ReadInt32();
            if (magic != FileMagic) {
                LogWarning($"[SecretAchievements] bad magic 0x{magic:X8}, ignored: {path}");
                return;
            }

            int version = reader.ReadInt32();
            if (version != FileVersion) {
                LogWarning($"[SecretAchievements] unsupported version {version}, ignored: {path}");
                return;
            }

            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEntryCount) {
                LogWarning($"[SecretAchievements] entry count {count} out of bounds, ignored: {path}");
                return;
            }

            for (int i = 0; i < count; i++) {
                if (!TryReadBoundedString(reader, out string key) ||
                    !TryReadInt64(reader, out long unlockedUtcTicks) ||
                    !TryReadBoundedString(reader, out string triggerSource)) {
                    unlocked.Clear();
                    LogWarning($"[SecretAchievements] truncated or malformed entry at index {i}, cleared: {path}");
                    return;
                }

                if (key.Length == 0 || key.Length > MaxStringChars || triggerSource.Length > MaxStringChars) {
                    unlocked.Clear();
                    LogWarning(
                        $"[SecretAchievements] string length out of bounds at index {i} " +
                        $"(key={key.Length}, source={triggerSource.Length}), cleared: {path}");
                    return;
                }

                if (unlocked.ContainsKey(key)) {
                    continue;
                }
                unlocked[key] = new SecretAchievementRecord(key, unlockedUtcTicks, triggerSource);
            }
        }
        catch (Exception ex) {
            unlocked.Clear();
            LogWarning($"[SecretAchievements] failed to load profile '{path}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 幂等解锁秘密成就并原子写入本机 profile。
    /// </summary>
    /// <param name="achievementKey">秘密成就键。</param>
    /// <param name="triggerSource">固定来源字符串，不得含隐私字段。</param>
    /// <returns>本次新解锁并成功持久化时返回 true；已解锁或定义未知时返回 false。</returns>
    public static bool TryUnlock(string achievementKey, string triggerSource) {
        if (!loaded) {
            Load();
        }
        if (string.IsNullOrEmpty(achievementKey) || unlocked.ContainsKey(achievementKey)) {
            return false;
        }
        if (achievementKey.Length > MaxStringChars) {
            LogWarning($"[SecretAchievements] key length {achievementKey.Length} exceeds {MaxStringChars}, ignored");
            return false;
        }
        // Import 可能早于 FinalAction；目录为空时先重建，避免 version1 旧档触发被误判为未知键。
        if (SecretAchievementCatalog.All.Count == 0) {
            SecretAchievementCatalog.Initialize();
        }
        if (SecretAchievementCatalog.Get(achievementKey) == null) {
            LogWarning($"[SecretAchievements] unknown key ignored: {achievementKey}");
            return false;
        }
        if (unlocked.Count >= MaxEntryCount) {
            LogWarning($"[SecretAchievements] entry cap {MaxEntryCount} reached, skip unlock: {achievementKey}");
            return false;
        }

        string source = string.IsNullOrEmpty(triggerSource) ? achievementKey : triggerSource;
        if (source.Length > MaxStringChars) {
            LogWarning($"[SecretAchievements] source length {source.Length} exceeds {MaxStringChars}, ignored");
            return false;
        }

        unlocked[achievementKey] = new SecretAchievementRecord(
            achievementKey,
            DateTime.UtcNow.Ticks,
            source);
        if (!Save()) {
            unlocked.Remove(achievementKey);
            return false;
        }
        return true;
    }

    /// <summary>
    /// 将当前解锁集合原子写入本机 profile。
    /// </summary>
    public static bool Save() {
        string path = ProfilePath;
        string directory = Path.GetDirectoryName(path);
        string tempPath = path + ".tmp";
        try {
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = File.Create(tempPath))
            using (BinaryWriter writer = new(stream, Encoding.UTF8)) {
                writer.Write(FileMagic);
                writer.Write(FileVersion);
                writer.Write(unlocked.Count);
                foreach (SecretAchievementRecord record in unlocked.Values) {
                    writer.Write(record.AchievementKey ?? "");
                    writer.Write(record.UnlockedUtcTicks);
                    writer.Write(record.TriggerSource ?? "");
                }
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(path)) {
                File.Replace(tempPath, path, null);
            } else {
                File.Move(tempPath, path);
            }
            return true;
        }
        catch (Exception ex) {
            LogWarning($"[SecretAchievements] failed to save profile '{path}': {ex.GetType().Name}: {ex.Message}");
            try {
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }
            }
            catch {
                // 清理失败不升级为致命错误。
            }
            return false;
        }
    }

    /// <summary>
    /// 有界读取 BinaryWriter 兼容的 7-bit 长度前缀字符串；长度越界不分配完整内容。
    /// </summary>
    private static bool TryReadBoundedString(BinaryReader reader, out string value) {
        value = "";
        if (!TryRead7BitEncodedInt(reader, out int byteLength) || byteLength < 0) {
            return false;
        }
        // 分配前按 UTF-8 最坏 4 字节/字符限制字节预算，避免未校验长串进入集合。
        int maxBytes = MaxStringChars * 4;
        if (byteLength > maxBytes) {
            return false;
        }
        long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (byteLength > remaining) {
            return false;
        }

        byte[] bytes = reader.ReadBytes(byteLength);
        if (bytes.Length != byteLength) {
            return false;
        }
        value = Encoding.UTF8.GetString(bytes);
        return value.Length <= MaxStringChars;
    }

    private static bool TryReadInt64(BinaryReader reader, out long value) {
        value = 0;
        if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(long)) {
            return false;
        }
        value = reader.ReadInt64();
        return true;
    }

    private static bool TryRead7BitEncodedInt(BinaryReader reader, out int value) {
        value = 0;
        int shift = 0;
        for (int i = 0; i < 5; i++) {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) {
                return false;
            }
            byte b = reader.ReadByte();
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) {
                return true;
            }
            shift += 7;
        }
        return false;
    }
}

#endregion // 秘密成就本机档案
