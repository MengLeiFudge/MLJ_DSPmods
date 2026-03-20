using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class GachaLimitedUnlocks {
    public static bool IsLimitedPoolUnlocked() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) return false;
        return GameMain.history.ItemUnlocked(I宇宙矩阵);
    }
}
