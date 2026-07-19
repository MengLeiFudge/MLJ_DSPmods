using System.Collections.Generic;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Technology;

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
        AddTowerPath("interaction", "交互塔", ERecipe.BuildingTrain);
        AddTowerPath("resource", "资源塔", ERecipe.MineralCopy);
        AddTowerPath("conversion", "转化塔", ERecipe.Conversion);
        AddTowerPath("analysis", "解析塔", ERecipe.Rectification);
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
            AncientTechEffectType.MainOutputLock, foreverKey, runtimeImplemented: false));
        Add(new($"{keyPrefix}.byproduct-discard", $"远古科技-{towerName}副产物弃置", towerType, 8,
            AncientTechEffectType.ByproductDiscard, lockKey, runtimeImplemented: false));
    }
}
