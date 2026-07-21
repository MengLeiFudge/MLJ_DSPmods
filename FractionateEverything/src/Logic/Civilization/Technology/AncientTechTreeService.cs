using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Protocols;

namespace FE.Logic.Civilization.Technology;

/// <summary>
/// 校验科技节点前置和价格，并在购买后刷新分馏运行缓存。
/// </summary>
public static class AncientTechTreeService {
    public static bool CanPurchase(string nodeKey) {
        AncientTechNodeDefinition node = AncientTechTreeCatalog.Get(nodeKey);
        if (node == null || !node.RuntimeImplemented || AncientTechTreeState.GetLevel(nodeKey) > 0
                         || AncientTechTreeState.AvailablePoints < node.Cost
                         || ProtocolCatalog.GetCompletedStageCount() <= 0) {
            return false;
        }
        return node.PrerequisiteNodeKey == null || AncientTechTreeState.GetLevel(node.PrerequisiteNodeKey) > 0;
    }

    public static bool TryPurchase(string nodeKey) {
        if (NebulaMultiplayerModAPI.RequestAncientTechPurchase(nodeKey)) {
            return true;
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
