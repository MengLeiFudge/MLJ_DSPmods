using FE.Logic.Civilization.Configuration;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Protocols;

/// <summary>
/// 集中判断协议能否进入当前检索池，不修改任何玩家状态。
/// </summary>
public static class ProtocolEligibilityService {
    public static bool IsEligible(ProtocolDefinition definition) {
        MatrixStageDefinition stage = ProgressionProfileRegistry.Current?.GetStage(definition.StageKey);
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(definition.RecipeKey.RecipeType,
            definition.RecipeKey.InputId);
        if (stage == null || recipe == null || !recipe.IsProtocolEligible
            || stage.Order > FE.Logic.Items.ItemManager.GetCurrentProgressStageIndex()) {
            return false;
        }

        ItemProto input = LDB.items.Select(recipe.InputID);
        if (input?.Type != EItemType.DarkFog) {
            return true;
        }

        return (GameMain.history != null && GameMain.history.ItemUnlocked(recipe.InputID))
               || PlayerInventoryAccess.GetItemTotalCount(recipe.InputID) > 0;
    }
}
