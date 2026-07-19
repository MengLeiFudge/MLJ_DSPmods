using FE.Logic.Civilization.Achievements;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Fractionation.FracRecipes.Runtime;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;

namespace FE.Logic.Civilization;

/// <summary>
/// 将文明探索状态投影为分馏域只读缓存，避免分馏热路径依赖文明业务对象。
/// </summary>
public static class CivilizationRuntimeSync {
    public static void Refresh() {
        RefreshRecipeAvailability();
        RefreshAchievementModifiers();
        RefreshTowerModifiers();
        ProcessManager.RefreshFractionatorRuntimeConfig();
    }

    private static void RefreshRecipeAvailability() {
        RecipeAvailabilityStore.Reset();
        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            RecipeAvailabilityStore.RegisterManaged(definition.RecipeKey,
                ProtocolProgressStore.IsComplete(definition.RecipeKey));
        }
    }

    private static void RefreshAchievementModifiers() {
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
            }
        }
    }
}
