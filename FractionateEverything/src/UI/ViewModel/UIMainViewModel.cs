using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using static FE.Utils.ProtoID;
using Random = System.Random;

namespace FE.UI.ViewModel;

public static class UIMainViewModel {
    public static ConfigEntry<int> RecipeTypeEntry;

    public static string RecipeTypeToStr() {
        switch (RecipeTypeEntry.Value) {
            case 0: return "矿物复制";
            case 1: return "量子复制";
            case 2: return "点金";
            case 3: return "分解";
            case 4: return "转化";
            default: return "未知";
        }
    }

    public static ERecipe RecipeTypeToERecipe() {
        switch (RecipeTypeEntry.Value) {
            case 0: return ERecipe.MineralCopy;
            case 1: return ERecipe.QuantumDuplicate;
            case 2: return ERecipe.Alchemy;
            case 3: return ERecipe.Deconstruction;
            case 4: return ERecipe.Conversion;
            default: return ERecipe.Unknown;
        }
    }

    public static ConfigEntry<int> BuildingTypeEntry;

    public static string BuildingTypeToStr() {
        switch (BuildingTypeEntry.Value) {
            case 0: return "交互";
            case 1: return "矿物复制";
            case 2: return "点数聚集";
            case 3: return "量子复制";
            case 4: return "点金";
            case 5: return "分解";
            case 6: return "转化";
            default: return "未知";
        }
    }

    public static Random random = new Random();

    public static void Init(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("config", "Recipe Type", 0, "想要查看的配方类型。");
        BuildingTypeEntry = configFile.Bind("config", "Building Type", 0, "想要查看的建筑类型。");
    }


    public static ItemProto InputItem { get; set; } = LDB.items.Select(I铁矿);

    public static void OnButtonChangeItemClick() {
        UIItemPickerExtension.Popup(new(-400f, 300f), item => {
            if (item == null) return;
            InputItem = item;
        }, true, _ => true);
    }

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
