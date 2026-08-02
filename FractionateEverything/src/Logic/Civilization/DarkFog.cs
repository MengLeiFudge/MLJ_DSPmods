using System;
using System.IO;
using FE.Utils;
using static FE.Utils.Utils;
using System.Collections.Generic;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.Recipes;
using HarmonyLib;

namespace FE.Logic.Civilization.DarkFog;

#region DarkFogEnemyEvidenceKind

/// <summary>
/// 黑雾击杀证据分类。仅统计 LDB.enemies 声明模型中可识别的六类实体。
/// </summary>
public enum DarkFogEnemyEvidenceKind : byte {
    None = 0,
    GroundUnit = 1,
    GroundBase = 2,
    SpaceUnit = 3,
    HiveCore = 4,
    Relay = 5,
    Tinder = 6,
}

#endregion // DarkFogEnemyEvidenceKind

#region DarkFogEnemyModelCatalog

/// <summary>
/// 预构建 modelIndex → 证据分类表，避免击杀热路径查询 EnemyProto。
/// </summary>
public static class DarkFogEnemyModelCatalog {
    /// <summary>与原版 AstroKillStat.killRegister 长度对齐的安全上界。</summary>
    public const int RegisterModelCapacity = 2048;

    private static DarkFogEnemyEvidenceKind[] kinds = [];
    private static bool built;

    /// <summary>分类表是否已根据当前 LDB.enemies 构建。</summary>
    public static bool IsBuilt => built;

    /// <summary>
    /// 根据 EnemyProto / PrefabDesc 预构建分类表；可重复调用以刷新。
    /// </summary>
    public static void Rebuild() {
        int capacity = RegisterModelCapacity;
        if (ModelProto.maxModelIndex > 0) {
            capacity = Math.Max(capacity, ModelProto.maxModelIndex + 1);
        }

        var next = new DarkFogEnemyEvidenceKind[capacity];
        EnemyProto[] enemies = LDB.enemies?.dataArray;
        if (enemies != null) {
            for (int i = 0; i < enemies.Length; i++) {
                EnemyProto enemy = enemies[i];
                if (enemy == null || enemy.ModelIndex <= 0) {
                    continue;
                }

                PrefabDesc prefab = enemy.prefabDesc;
                if (prefab == null || prefab == PrefabDesc.none) {
                    ModelProto[] modelArray = LDB.models?.modelArray;
                    if (modelArray != null
                        && (uint)enemy.ModelIndex < (uint)modelArray.Length
                        && modelArray[enemy.ModelIndex] != null) {
                        prefab = modelArray[enemy.ModelIndex].prefabDesc;
                    }
                }
                if (prefab == null || prefab == PrefabDesc.none) {
                    continue;
                }

                DarkFogEnemyEvidenceKind kind = Classify(enemy, prefab);
                if (kind == DarkFogEnemyEvidenceKind.None) {
                    continue;
                }

                int modelCount = Math.Max(1, enemy.ModelCount);
                int end = enemy.ModelIndex + modelCount;
                for (int modelIndex = enemy.ModelIndex; modelIndex < end; modelIndex++) {
                    if ((uint)modelIndex >= (uint)next.Length) {
                        int grown = Math.Max(modelIndex + 1, next.Length * 2);
                        Array.Resize(ref next, grown);
                    }
                    next[modelIndex] = kind;
                }
            }
        }

        kinds = next;
        built = true;
    }

    /// <summary>
    /// 查询 modelIndex 对应的证据分类；未声明或非目标模型返回 None。
    /// </summary>
    public static DarkFogEnemyEvidenceKind GetKind(int modelIndex) {
        if (!built) {
            Rebuild();
        }
        if ((uint)modelIndex >= (uint)kinds.Length) {
            return DarkFogEnemyEvidenceKind.None;
        }
        return kinds[modelIndex];
    }

    /// <summary>
    /// 按 EnemyProto/PrefabDesc 规则分类单个敌人原型，供构建表与结构探针复用。
    /// </summary>
    public static DarkFogEnemyEvidenceKind Classify(EnemyProto enemy, PrefabDesc prefab) {
        if (enemy == null || prefab == null || prefab == PrefabDesc.none) {
            return DarkFogEnemyEvidenceKind.None;
        }

        if (prefab.isDFGroundBase) {
            return DarkFogEnemyEvidenceKind.GroundBase;
        }
        if (prefab.isDFSpaceCore) {
            return DarkFogEnemyEvidenceKind.HiveCore;
        }
        if (prefab.isDFRelay) {
            return DarkFogEnemyEvidenceKind.Relay;
        }
        if (prefab.isDFTinder) {
            return DarkFogEnemyEvidenceKind.Tinder;
        }
        if (prefab.isEnemyUnit) {
            return enemy.IsSpace
                ? DarkFogEnemyEvidenceKind.SpaceUnit
                : DarkFogEnemyEvidenceKind.GroundUnit;
        }
        return DarkFogEnemyEvidenceKind.None;
    }

    /// <summary>切档或重置时丢弃分类表，下次访问再重建。</summary>
    public static void Reset() {
        kinds = [];
        built = false;
    }
}

#endregion // DarkFogEnemyModelCatalog

#region DarkFogInvestigationState

/// <summary>
/// 主机权威的黑雾调查证据状态。等级由证据派生，不重复保存。
/// </summary>
public static class DarkFogInvestigationState {
    /// <summary>当前存档块格式版本。</summary>
    public const int CurrentVersion = 1;

    /// <summary>单计数字段允许的最大导入值；超过视为损坏并拒读该字段。</summary>
    public const long MaxImportedCount = long.MaxValue;

    private static long groundUnits;
    private static long groundBases;
    private static long spaceUnits;
    private static long hiveCores;
    private static long relays;
    private static long tinders;
    private static bool firstPlanetCleared;
    private static bool firstSystemCleared;

    public static long GroundUnits => groundUnits;
    public static long GroundBases => groundBases;
    public static long SpaceUnits => spaceUnits;
    public static long HiveCores => hiveCores;
    public static long Relays => relays;
    public static long Tinders => tinders;
    public static bool FirstPlanetCleared => firstPlanetCleared;
    public static bool FirstSystemCleared => firstSystemCleared;

    /// <summary>
    /// 地表认知等级 0..4，由证据固定阈值派生。
    /// </summary>
    public static int GroundRank => DeriveGroundRank(
        groundUnits, groundBases, firstPlanetCleared);

    /// <summary>
    /// 星域认知等级 0..4，由证据固定阈值派生。
    /// </summary>
    public static int SpaceRank => DeriveSpaceRank(
        spaceUnits, hiveCores, relays, tinders, firstSystemCleared);

    /// <summary>
    /// 地表 rank 固定阈值：G1 units&gt;=20；G2 &gt;=100 且 bases&gt;=1；
    /// G3 &gt;=400 且 bases&gt;=3 且 firstPlanetCleared；G4 &gt;=1000 且 bases&gt;=8 且 firstPlanetCleared。
    /// </summary>
    public static int DeriveGroundRank(long units, long bases, bool planetCleared) {
        if (units >= 1000L && bases >= 8L && planetCleared) {
            return 4;
        }
        if (units >= 400L && bases >= 3L && planetCleared) {
            return 3;
        }
        if (units >= 100L && bases >= 1L) {
            return 2;
        }
        if (units >= 20L) {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// 星域 rank 固定阈值：S1 units&gt;=10；S2 &gt;=50 且 relays&gt;=1；
    /// S3 &gt;=200 且 hiveCores&gt;=1 且 tinders&gt;=1；S4 &gt;=600 且 hiveCores&gt;=3 且 firstSystemCleared。
    /// </summary>
    public static int DeriveSpaceRank(long units, long cores, long relayCount, long tinderCount,
        bool systemCleared) {
        if (units >= 600L && cores >= 3L && systemCleared) {
            return 4;
        }
        if (units >= 200L && cores >= 1L && tinderCount >= 1L) {
            return 3;
        }
        if (units >= 50L && relayCount >= 1L) {
            return 2;
        }
        if (units >= 10L) {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// 按分类饱和递增对应证据计数；返回是否实际发生变化。
    /// </summary>
    public static bool TryAddEvidence(DarkFogEnemyEvidenceKind kind) {
        return kind switch {
            DarkFogEnemyEvidenceKind.GroundUnit => SaturatingIncrement(ref groundUnits),
            DarkFogEnemyEvidenceKind.GroundBase => SaturatingIncrement(ref groundBases),
            DarkFogEnemyEvidenceKind.SpaceUnit => SaturatingIncrement(ref spaceUnits),
            DarkFogEnemyEvidenceKind.HiveCore => SaturatingIncrement(ref hiveCores),
            DarkFogEnemyEvidenceKind.Relay => SaturatingIncrement(ref relays),
            DarkFogEnemyEvidenceKind.Tinder => SaturatingIncrement(ref tinders),
            _ => false,
        };
    }

    /// <summary>
    /// 幂等标记首次清剿行星；已为 true 时返回 false。
    /// </summary>
    public static bool TrySetFirstPlanetCleared() {
        if (firstPlanetCleared) {
            return false;
        }
        firstPlanetCleared = true;
        return true;
    }

    /// <summary>
    /// 幂等标记首次清除星系；已为 true 时返回 false。
    /// </summary>
    public static bool TrySetFirstSystemCleared() {
        if (firstSystemCleared) {
            return false;
        }
        firstSystemCleared = true;
        return true;
    }

    /// <summary>
    /// 版本化导入调查证据。损坏或超限计数字段拒读并保持 0；rank 不从存档读取。
    /// </summary>
    public static void Import(BinaryReader r) {
        IntoOtherSave();
        if (r == null) {
            return;
        }

        r.ReadBlocks(
            ("Version", br => {
                int version = br.ReadInt32();
                if (version < 1 || version > CurrentVersion) {
                    LogWarning($"[DarkFog] Unsupported investigation state version: {version}");
                }
            }),
            ("Evidence", br => {
                groundUnits = ReadBoundedCount(br);
                groundBases = ReadBoundedCount(br);
                spaceUnits = ReadBoundedCount(br);
                hiveCores = ReadBoundedCount(br);
                relays = ReadBoundedCount(br);
                tinders = ReadBoundedCount(br);
                firstPlanetCleared = br.ReadBoolean();
                firstSystemCleared = br.ReadBoolean();
            })
        );
    }

    /// <summary>
    /// 导出调查证据；不写入派生 rank。
    /// </summary>
    public static void Export(BinaryWriter w) {
        if (w == null) {
            return;
        }

        w.WriteBlocks(
            ("Version", bw => bw.Write(CurrentVersion)),
            ("Evidence", bw => {
                bw.Write(groundUnits);
                bw.Write(groundBases);
                bw.Write(spaceUnits);
                bw.Write(hiveCores);
                bw.Write(relays);
                bw.Write(tinders);
                bw.Write(firstPlanetCleared);
                bw.Write(firstSystemCleared);
            })
        );
    }

    /// <summary>
    /// 切档清空全部调查证据。
    /// </summary>
    public static void IntoOtherSave() {
        groundUnits = 0L;
        groundBases = 0L;
        spaceUnits = 0L;
        hiveCores = 0L;
        relays = 0L;
        tinders = 0L;
        firstPlanetCleared = false;
        firstSystemCleared = false;
    }

    private static long ReadBoundedCount(BinaryReader br) {
        long value = br.ReadInt64();
        if (value < 0L || value > MaxImportedCount) {
            LogWarning($"[DarkFog] Rejected out-of-range evidence count: {value}");
            return 0L;
        }
        return value;
    }

    private static bool SaturatingIncrement(ref long value) {
        if (value >= long.MaxValue) {
            return false;
        }
        value++;
        return true;
    }
}

#endregion // DarkFogInvestigationState

#region DarkFogInvestigationService

/// <summary>
/// 黑雾调查运行时：击杀证据累计、低频清剿检测、认知投影、协议特殊资格与节流广播。
/// </summary>
public static class DarkFogInvestigationService {
    private const int ClearCheckIntervalTicks = 60;
    private const int BroadcastIntervalTicks = 60;

    private static bool initialized;
    private static bool evidenceDirty;
    private static bool authoritySnapshotDirty;
    private static bool planetClearCheckPending;
    private static bool systemClearCheckPending;
    private static long lastClearCheckTick = long.MinValue;
    private static long lastBroadcastTick = long.MinValue;
    private static readonly HashSet<int> pendingPlanetFactories = [];
    private static readonly HashSet<int> pendingSystemStars = [];

    /// <summary>
    /// 构建敌人模型分类表并投影当前 rank。可重复调用。
    /// </summary>
    public static void Initialize() {
        DarkFogEnemyModelCatalog.Rebuild();
        ProjectCognitionCache();
        initialized = true;
    }

    /// <summary>
    /// 存档导入后重建分类表并投影 rank。
    /// </summary>
    public static void AfterImport() {
        EnsureInitialized();
        pendingPlanetFactories.Clear();
        pendingSystemStars.Clear();
        planetClearCheckPending = false;
        systemClearCheckPending = false;
        evidenceDirty = false;
        authoritySnapshotDirty = false;
        lastClearCheckTick = long.MinValue;
        lastBroadcastTick = long.MinValue;
        ProjectCognitionCache();
    }

    /// <summary>
    /// 主机低频 Tick：清剿检测、缓存投影与最多每 60 tick 一次的文明权威快照广播。
    /// 客户端立即返回。
    /// </summary>
    public static void Tick() {
        if (NebulaMultiplayerModAPI.IsClient) {
            return;
        }

        EnsureInitialized();

        long tick = GameMain.gameTick;
        if (ShouldRunClearChecks(tick)) {
            lastClearCheckTick = tick;
            if (RunPendingClearChecks()) {
                evidenceDirty = true;
                authoritySnapshotDirty = true;
            }
        }

        if (evidenceDirty) {
            ProjectCognitionCache();
            evidenceDirty = false;
        }

        if (authoritySnapshotDirty && ShouldBroadcast(tick)) {
            lastBroadcastTick = tick;
            authoritySnapshotDirty = false;
            NebulaMultiplayerModAPI.BroadcastCivilizationState();
        }
    }

    /// <summary>
    /// 黑雾协议特殊前置：只检查调查 rank + 样本解译，不检查普通物品接触。
    /// 完整配方未涉及原版黑雾物品时返回 true。
    /// </summary>
    public static bool MeetsProtocolPrerequisite(BaseRecipe recipe) {
        if (recipe == null) {
            return true;
        }

        int tier = ResolveDarkFogProtocolTier(recipe);
        if (tier <= 0) {
            return true;
        }

        int groundRank = DarkFogInvestigationState.GroundRank;
        int spaceRank = DarkFogInvestigationState.SpaceRank;
        return tier switch {
            1 => HasSampleInterpreted(IFE黑雾样本A) && groundRank >= 1,
            2 => HasSampleInterpreted(IFE黑雾样本B) && groundRank >= 2 && spaceRank >= 1,
            3 => HasSampleInterpreted(IFE黑雾样本C) && groundRank >= 3 && spaceRank >= 2,
            _ => HasSampleInterpreted(IFE黑雾样本C) && groundRank >= 4 && spaceRank >= 3,
        };
    }

    /// <summary>
    /// 切档时重置运行时缓存；状态本体由 <see cref="DarkFogInvestigationState.IntoOtherSave"/> 清理。
    /// </summary>
    public static void IntoOtherSave() {
        DarkFogInvestigationState.IntoOtherSave();
        DarkFogEnemyModelCatalog.Reset();
        pendingPlanetFactories.Clear();
        pendingSystemStars.Clear();
        planetClearCheckPending = false;
        systemClearCheckPending = false;
        evidenceDirty = false;
        authoritySnapshotDirty = false;
        lastClearCheckTick = long.MinValue;
        lastBroadcastTick = long.MinValue;
        initialized = false;
        DarkFogCognitionCache.Reset();
    }

    /// <summary>
    /// 处理工厂击杀登记（地表单位/基地等）。客户端与非敌模型均忽略。
    /// </summary>
    public static void RecordFactoryKill(int factoryIndex, int modelIndex) {
        if (NebulaMultiplayerModAPI.IsClient || !IsCombatModeActive()) {
            return;
        }

        DarkFogEnemyEvidenceKind kind = DarkFogEnemyModelCatalog.GetKind(modelIndex);
        if (kind == DarkFogEnemyEvidenceKind.None) {
            return;
        }

        if (!TryAddEvidenceAndMark(kind)) {
            return;
        }
        if (kind == DarkFogEnemyEvidenceKind.GroundBase && factoryIndex >= 0) {
            pendingPlanetFactories.Add(factoryIndex);
            planetClearCheckPending = true;
        }
    }

    /// <summary>
    /// 处理星际击杀登记（太空单位/蜂巢核心/中继/火种）。客户端与非敌模型均忽略。
    /// </summary>
    public static void RecordStarKill(int starId, int modelIndex) {
        if (NebulaMultiplayerModAPI.IsClient || !IsCombatModeActive()) {
            return;
        }

        DarkFogEnemyEvidenceKind kind = DarkFogEnemyModelCatalog.GetKind(modelIndex);
        if (kind == DarkFogEnemyEvidenceKind.None) {
            return;
        }

        if (!TryAddEvidenceAndMark(kind)) {
            return;
        }
        if (kind == DarkFogEnemyEvidenceKind.HiveCore && starId > 0) {
            pendingSystemStars.Add(starId);
            systemClearCheckPending = true;
        }
    }

    /// <summary>
    /// 结构探针入口：直接按分类累计证据（不经过 model 表）。
    /// </summary>
    public static bool RecordEvidenceDirect(DarkFogEnemyEvidenceKind kind) {
        if (NebulaMultiplayerModAPI.IsClient || !IsCombatModeActive()) {
            return false;
        }
        if (!TryAddEvidenceAndMark(kind)) {
            return false;
        }
        if (kind == DarkFogEnemyEvidenceKind.GroundBase) {
            planetClearCheckPending = true;
        } else if (kind == DarkFogEnemyEvidenceKind.HiveCore) {
            systemClearCheckPending = true;
        }
        return true;
    }

    /// <summary>
    /// 完整配方涉及的原版黑雾物品最高层：5206/5201→1，5203/5202→2，5204→3，5205→4；无则 0。
    /// </summary>
    public static int ResolveDarkFogProtocolTier(BaseRecipe recipe) {
        if (recipe == null) {
            return 0;
        }

        int tier = ItemToDarkFogTier(recipe.InputID);
        if (recipe.OutputMain != null) {
            for (int i = 0; i < recipe.OutputMain.Count; i++) {
                OutputInfo output = recipe.OutputMain[i];
                if (output != null) {
                    tier = Math.Max(tier, ItemToDarkFogTier(output.OutputID));
                }
            }
        }
        if (recipe.OutputAppend != null) {
            for (int i = 0; i < recipe.OutputAppend.Count; i++) {
                OutputInfo output = recipe.OutputAppend[i];
                if (output != null) {
                    tier = Math.Max(tier, ItemToDarkFogTier(output.OutputID));
                }
            }
        }
        return tier;
    }

    private static void EnsureInitialized() {
        if (!initialized || !DarkFogEnemyModelCatalog.IsBuilt) {
            Initialize();
        }
    }

    /// <summary>
    /// 无战斗模式时不累计证据、不报错；缺 data 时视为非战斗。
    /// </summary>
    private static bool IsCombatModeActive() =>
        GameMain.logic?.isCombatMode
        ?? GameMain.data?.gameDesc?.isCombatMode
        ?? false;

    /// <summary>
    /// 累计证据；任意有效证据变化都刷新缓存投影，并标记权威快照脏位（仍由 Tick 按 60 tick 节流广播）。
    /// </summary>
    private static bool TryAddEvidenceAndMark(DarkFogEnemyEvidenceKind kind) {
        if (!DarkFogInvestigationState.TryAddEvidence(kind)) {
            return false;
        }
        evidenceDirty = true;
        // 客户端总览依赖证据计数本身，不只是 rank；每次成功累计都进入节流广播。
        authoritySnapshotDirty = true;
        return true;
    }

    private static void ProjectCognitionCache() {
        DarkFogCognitionCache.SetRanks(
            DarkFogInvestigationState.GroundRank,
            DarkFogInvestigationState.SpaceRank);
    }

    private static bool ShouldRunClearChecks(long tick) {
        if (!planetClearCheckPending && !systemClearCheckPending
            && pendingPlanetFactories.Count == 0 && pendingSystemStars.Count == 0) {
            return false;
        }
        if (lastClearCheckTick == long.MinValue) {
            return true;
        }
        return tick < 0 || tick - lastClearCheckTick >= ClearCheckIntervalTicks;
    }

    private static bool ShouldBroadcast(long tick) {
        if (lastBroadcastTick == long.MinValue) {
            return true;
        }
        return tick < 0 || tick - lastBroadcastTick >= BroadcastIntervalTicks;
    }

    private static bool RunPendingClearChecks() {
        bool changed = false;
        if (!DarkFogInvestigationState.FirstPlanetCleared
            && (planetClearCheckPending || pendingPlanetFactories.Count > 0)) {
            if (TryDetectFirstPlanetCleared()) {
                changed |= DarkFogInvestigationState.TrySetFirstPlanetCleared();
            }
        }
        if (DarkFogInvestigationState.FirstPlanetCleared) {
            pendingPlanetFactories.Clear();
            planetClearCheckPending = false;
        }

        if (!DarkFogInvestigationState.FirstSystemCleared
            && (systemClearCheckPending || pendingSystemStars.Count > 0)) {
            if (TryDetectFirstSystemCleared()) {
                changed |= DarkFogInvestigationState.TrySetFirstSystemCleared();
            }
        }
        if (DarkFogInvestigationState.FirstSystemCleared) {
            pendingSystemStars.Clear();
            systemClearCheckPending = false;
        }
        return changed;
    }

    private static bool TryDetectFirstPlanetCleared() {
        GameData data = GameMain.data;
        if (data?.factories == null) {
            return false;
        }

        if (pendingPlanetFactories.Count > 0) {
            foreach (int factoryIndex in pendingPlanetFactories) {
                if ((uint)factoryIndex >= (uint)data.factoryCount) {
                    continue;
                }
                PlanetFactory factory = data.factories[factoryIndex];
                if (factory != null && CountAliveGroundBases(factory) == 0) {
                    return true;
                }
            }
            return false;
        }

        // 仅有脏标记、无具体工厂索引时（例如直接证据探针），扫描全部工厂。
        for (int i = 0; i < data.factoryCount; i++) {
            PlanetFactory factory = data.factories[i];
            if (factory?.enemySystem?.bases == null) {
                continue;
            }
            // 无基地缓冲活动痕迹的工厂跳过，避免把从未生成黑雾的行星算作清剿。
            if (factory.enemySystem.bases.cursor <= 1) {
                continue;
            }
            if (CountAliveGroundBases(factory) == 0) {
                return true;
            }
        }
        return false;
    }

    private static bool TryDetectFirstSystemCleared() {
        SpaceSector sector = GameMain.data?.spaceSector;
        GalaxyData galaxy = GameMain.galaxy;
        if (sector?.dfHives == null || galaxy == null) {
            return false;
        }

        if (pendingSystemStars.Count > 0) {
            foreach (int starId in pendingSystemStars) {
                StarData star = galaxy.StarById(starId);
                if (star == null) {
                    continue;
                }
                if (IsStarSystemCleared(sector, star.index)) {
                    return true;
                }
            }
            return false;
        }

        for (int starIndex = 0; starIndex < sector.dfHives.Length; starIndex++) {
            if (sector.dfHives[starIndex] == null) {
                continue;
            }
            if (IsStarSystemCleared(sector, starIndex)) {
                return true;
            }
        }
        return false;
    }

    private static int CountAliveGroundBases(PlanetFactory factory) {
        var bases = factory.enemySystem?.bases;
        if (bases == null) {
            return 0;
        }

        int count = 0;
        for (int baseIndex = 1; baseIndex < bases.cursor; baseIndex++) {
            DFGBaseComponent current = bases.buffer[baseIndex];
            if (current != null && current.id == baseIndex) {
                count++;
            }
        }
        return count;
    }

    private static bool IsStarSystemCleared(SpaceSector sector, int starIndex) {
        if ((uint)starIndex >= (uint)sector.dfHives.Length) {
            return false;
        }

        EnemyDFHiveSystem hive = sector.dfHives[starIndex];
        if (hive == null) {
            // 从未生成蜂巢的星系不算“清除”。
            return false;
        }

        bool sawHive = false;
        for (EnemyDFHiveSystem current = hive; current != null; current = current.nextSibling) {
            sawHive = true;
            if (!current.isEmpty && current.isAlive) {
                return false;
            }
        }
        return sawHive;
    }

    private static bool HasSampleInterpreted(int sampleItemId) {
        AnalysisRecipe recipe = RecipeManager.GetRecipe<AnalysisRecipe>(ERecipe.Analysis, sampleItemId);
        return recipe != null && recipe.TotalSuccessCount > 0L;
    }

    private static int ItemToDarkFogTier(int itemId) => itemId switch {
        I能量碎片 or I黑雾矩阵 => 1,
        I物质重组器 or I硅基神经元 => 2,
        I负熵奇点 => 3,
        I核心素 => 4,
        _ => 0,
    };
}

#endregion // DarkFogInvestigationService

#region DarkFogKillStatisticsPatches

/// <summary>
/// 仅挂钩原版 KillStatistics 工厂/星际击杀登记；不重复统计 Mecha 击杀。
/// </summary>
public static class DarkFogKillStatisticsPatches {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.RegisterFactoryKillStat))]
    public static void KillStatistics_RegisterFactoryKillStat_Postfix(int factoryIndex, int modelIndex) {
        DarkFogInvestigationService.RecordFactoryKill(factoryIndex, modelIndex);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(KillStatistics), nameof(KillStatistics.RegisterStarKillStat))]
    public static void KillStatistics_RegisterStarKillStat_Postfix(int starId, int modelIndex) {
        DarkFogInvestigationService.RecordStarKill(starId, modelIndex);
    }
}

#endregion // DarkFogKillStatisticsPatches
