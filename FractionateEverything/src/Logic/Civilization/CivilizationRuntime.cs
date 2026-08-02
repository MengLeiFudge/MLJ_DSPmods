using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Fractionators;

namespace FE.Logic.Civilization;

#region CivilizationRuntimeSync

/// <summary>
/// 将文明探索状态投影为分馏域只读缓存，避免分馏热路径依赖文明业务对象。
/// </summary>
public static class CivilizationRuntimeSync {
    public static void Refresh() {
        RefreshRecipeAvailability();
        RefreshTowerModifiers();
        ProcessManager.RefreshFractionatorRuntimeConfig();
    }

    private static void RefreshRecipeAvailability() {
        RecipeAvailabilityStore.Reset();
        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            // 100% 仅购买资格；运行可用性只按正式解锁开放。
            RecipeAvailabilityStore.RegisterManaged(definition.RecipeKey,
                ProtocolProgressStore.IsFormallyUnlocked(definition.RecipeKey));
        }
    }

    private static void RefreshTowerModifiers() {
        TowerRuntimeModifierCache.Reset();
        foreach (AncientTechNodeDefinition node in AncientTechTreeCatalog.All) {
            if (AncientTechTreeState.GetLevel(node.NodeKey) <= 0) {
                continue;
            }
            switch (node.EffectType) {
                case AncientTechEffectType.FluidOutputStacking:
                    TowerRuntimeModifierCache.EnableFluidOutputStacking(node.TowerType);
                    break;
                case AncientTechEffectType.ProductOutputStacking:
                    TowerRuntimeModifierCache.EnableProductOutputStacking(node.TowerType);
                    break;
                case AncientTechEffectType.FractionationForever:
                    TowerRuntimeModifierCache.EnableFractionationForever(node.TowerType);
                    break;
                case AncientTechEffectType.MainOutputLock:
                    TowerRuntimeModifierCache.EnableMainOutputLock(node.TowerType);
                    break;
                case AncientTechEffectType.ByproductDiscard:
                    TowerRuntimeModifierCache.EnableByproductDiscard(node.TowerType);
                    break;
            }
        }
    }
}

#endregion // CivilizationRuntimeSync
