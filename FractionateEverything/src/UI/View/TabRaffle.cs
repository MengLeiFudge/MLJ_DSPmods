using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using static FE.UI.View.TabRecipeAndBuilding;
using Random = System.Random;

namespace FE.UI.View;

public static class TabRaffle {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) {

    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        float x;
        float y;
        wnd.AddTabGroup(trans, "抽卡", "tab-group-fe2");
        {
            var tab = wnd.AddTab(trans, "抽卡");
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "卡池")
                .WithItems("矿物复制", "量子复制", "点金", "分解", "转化")
                .WithSize(150f, 0f)
                .WithConfigEntry(BuildingTypeEntry);
            y += 30f;

            wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-get-recipe1-1",
                () => Raffle(ERecipe.MineralCopy, 1));
            wnd.AddButton(x + 300, y, 200, tab, "十连", 16, "button-get-recipe1-10",
                () => Raffle(ERecipe.MineralCopy, 10));
        }
    }

    public static void UpdateUI() { }

    public static Random random = new Random();

    /// <summary>
    /// 抽卡。
    /// </summary>
    /// <param name="recipeType">配方奖池</param>
    /// <param name="count">抽卡次数</param>
    public static void Raffle(ERecipe recipeType, int count) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        BaseRecipe[] recipeArr = RecipeManager.GetRecipes(recipeType);
        for (int i = 0; i < count; i++) {
            int id = random.Next(0, recipeArr.Length - 1);
            if (recipeArr[id] == null) {
                //狗粮
                GameMain.mainPlayer.sandCount += 1000;
            } else {
                //配方
                BaseRecipe recipe = recipeArr[id];
                if (!recipe.IsUnlocked) {
                    recipe.Level = 1;
                    recipe.Quality = 1;
                } else {
                    recipe.MemoryCount++;
                }
            }
        }
    }
}
