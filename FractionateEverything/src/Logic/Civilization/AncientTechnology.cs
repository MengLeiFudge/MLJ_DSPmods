using FE.Logic.Fractionation.Recipes;
using System.Collections.Generic;
using System;
using System.IO;
using FE.Utils;
using FE.Compatibility.Nebula;

namespace FE.Logic.Civilization.Technology;

#region AncientTechNodeDefinition

/// <summary>
/// 标识固定塔型科技节点可投影到运行时的工程能力。
/// </summary>
public enum AncientTechEffectType {
    FluidOutputStacking,
    ProductOutputStacking,
    FractionationForever,
    MainOutputLock,
    ByproductDiscard,
}

/// <summary>
/// 定义科技树中的一个固定节点及其运行投影效果。
/// </summary>
public sealed class AncientTechNodeDefinition(
    string nodeKey,
    string displayNameKey,
    ERecipe towerType,
    int cost,
    AncientTechEffectType effectType,
    string prerequisiteNodeKey = null,
    bool runtimeImplemented = true) {
    public string NodeKey { get; } = nodeKey;
    public string DisplayNameKey { get; } = displayNameKey;
    public ERecipe TowerType { get; } = towerType;
    public int Cost { get; } = cost;
    public AncientTechEffectType EffectType { get; } = effectType;
    public string PrerequisiteNodeKey { get; } = prerequisiteNodeKey;
    public bool RuntimeImplemented { get; } = runtimeImplemented;
}

#endregion // AncientTechNodeDefinition

#region AncientTechTreeCatalog

/// <summary>
/// 保存四条稳定塔型主干及其固定五节点顺序。
/// </summary>
public static class AncientTechTreeCatalog {
    private static readonly List<AncientTechNodeDefinition> nodes = [];
    private static readonly Dictionary<string, AncientTechNodeDefinition> nodesByKey = [];

    public static IReadOnlyList<AncientTechNodeDefinition> All => nodes;

    public static void Initialize() {
        nodes.Clear();
        nodesByKey.Clear();
        AddTowerPath("cultivation", "培养塔", ERecipe.Cultivation);
        AddTowerPath("analysis", "解析塔", ERecipe.Analysis);
        AddTowerPath("replication", "复制塔", ERecipe.Replication);
        AddTowerPath("conversion", "转化塔", ERecipe.Conversion);
    }

    public static AncientTechNodeDefinition Get(string nodeKey) =>
        nodeKey != null && nodesByKey.TryGetValue(nodeKey, out AncientTechNodeDefinition node) ? node : null;

    private static void Add(AncientTechNodeDefinition node) {
        nodes.Add(node);
        nodesByKey[node.NodeKey] = node;
    }

    private static void AddTowerPath(string keyPrefix, string towerName, ERecipe towerType) {
        string flowKey = $"{keyPrefix}.flow-stack";
        string productKey = $"{keyPrefix}.product-stack";
        string foreverKey = $"{keyPrefix}.fractionation-forever";
        string lockKey = $"{keyPrefix}.main-lock";
        Add(new(flowKey, $"远古科技-{towerName}流动输出堆叠", towerType, 1,
            AncientTechEffectType.FluidOutputStacking));
        Add(new(productKey, $"远古科技-{towerName}产物输出堆叠", towerType, 2,
            AncientTechEffectType.ProductOutputStacking, flowKey));
        Add(new(foreverKey, $"远古科技-{towerName}分馏永动", towerType, 3,
            AncientTechEffectType.FractionationForever, productKey));
        Add(new(lockKey, $"远古科技-{towerName}主路锁定", towerType, 5,
            AncientTechEffectType.MainOutputLock, foreverKey));
        Add(new($"{keyPrefix}.byproduct-discard", $"远古科技-{towerName}副产物弃置", towerType, 8,
            AncientTechEffectType.ByproductDiscard, lockKey));
    }
}

#endregion // AncientTechTreeCatalog

#region AncientTechTreeState

/// <summary>
/// 保存单存档远古文明科技点和节点等级。
/// </summary>
public static class AncientTechTreeState {
    private static readonly Dictionary<string, int> nodeLevels = [];

    public static int AvailablePoints { get; private set; }
    public static int TotalPointsEarned { get; private set; }
    public static int TotalPointsSpent { get; private set; }

    public static int GetLevel(string nodeKey) =>
        nodeKey != null && nodeLevels.TryGetValue(nodeKey, out int level) ? level : 0;

    public static void AwardPoint() {
        AwardPoints(1);
    }

    public static void AwardPoints(int count) {
        if (count <= 0) {
            return;
        }
        AvailablePoints = AddSaturated(AvailablePoints, count);
        TotalPointsEarned = AddSaturated(TotalPointsEarned, count);
    }

    public static bool TrySpend(int cost) {
        if (cost <= 0 || AvailablePoints < cost) {
            return false;
        }
        AvailablePoints -= cost;
        TotalPointsSpent = AddSaturated(TotalPointsSpent, cost);
        return true;
    }

    /// <summary>
    /// 将非负增量加到当前值上；若结果将超过 <see cref="int.MaxValue"/> 则饱和到上限，避免 unchecked 回绕为负。
    /// </summary>
    private static int AddSaturated(int current, int increment) {
        if (increment <= 0) {
            return current;
        }
        if (current >= int.MaxValue - increment) {
            return int.MaxValue;
        }
        return current + increment;
    }

    public static void SetLevel(string nodeKey, int level) {
        nodeLevels[nodeKey] = Math.Max(0, level);
    }

    public static void Import(BinaryReader r) {
        nodeLevels.Clear();
        AvailablePoints = 0;
        TotalPointsEarned = 0;
        TotalPointsSpent = 0;
        r.ReadBlocks(
            ("Points", br => {
                AvailablePoints = Math.Max(0, br.ReadInt32());
                TotalPointsEarned = Math.Max(0, br.ReadInt32());
                TotalPointsSpent = Math.Max(0, br.ReadInt32());
            }),
            ("Nodes", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    nodeLevels[br.ReadString()] = Math.Max(0, br.ReadInt32());
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Points", bw => {
                bw.Write(AvailablePoints);
                bw.Write(TotalPointsEarned);
                bw.Write(TotalPointsSpent);
            }),
            ("Nodes", bw => {
                bw.Write(nodeLevels.Count);
                foreach (KeyValuePair<string, int> pair in nodeLevels) {
                    bw.Write(pair.Key);
                    bw.Write(pair.Value);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        nodeLevels.Clear();
        AvailablePoints = 0;
        TotalPointsEarned = 0;
        TotalPointsSpent = 0;
    }
}

#endregion // AncientTechTreeState

#region AncientTechTreeService

/// <summary>
/// 校验科技节点前置和价格，并在购买后刷新分馏运行缓存。
/// </summary>
public static class AncientTechTreeService {
    public static bool CanPurchase(string nodeKey) {
        AncientTechNodeDefinition node = AncientTechTreeCatalog.Get(nodeKey);
        if (node == null || !node.RuntimeImplemented || AncientTechTreeState.GetLevel(nodeKey) > 0
                         || AncientTechTreeState.AvailablePoints < node.Cost) {
            return false;
        }
        return node.PrerequisiteNodeKey == null || AncientTechTreeState.GetLevel(node.PrerequisiteNodeKey) > 0;
    }

    /// <summary>
    /// 尝试购买远古科技节点。联机客户端仅转发请求并返回 false（pending），不预扣科技点；
    /// 主机/单机在本地结算后广播权威快照。
    /// </summary>
    /// <param name="nodeKey">目录节点键。</param>
    /// <returns>主机/单机购买成功为 true；客户端已转发或本地失败为 false。</returns>
    public static bool TryPurchase(string nodeKey) {
        if (NebulaMultiplayerModAPI.RequestAncientTechPurchase(nodeKey)) {
            // 与协议检索/解锁一致：客户端请求成功只表示 pending，不表示本地已购买。
            return false;
        }

        AncientTechNodeDefinition node = AncientTechTreeCatalog.Get(nodeKey);
        if (node == null || !CanPurchase(nodeKey) || !AncientTechTreeState.TrySpend(node.Cost)) {
            return false;
        }
        AncientTechTreeState.SetLevel(nodeKey, 1);
        CivilizationRuntimeSync.Refresh();
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
        return true;
    }
}

#endregion // AncientTechTreeService
