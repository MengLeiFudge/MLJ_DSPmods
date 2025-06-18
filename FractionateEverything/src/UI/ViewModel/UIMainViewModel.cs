using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Utils;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.Logic.Manager.RecipeManager;
using Random = System.Random;

namespace FE.UI.ViewModel;

public static class UIMainViewModel {
    public static RectTransform _windowTrans;

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
            case 0: return "交互塔";
            case 1: return "矿物复制塔";
            case 2: return "点数聚集塔";
            case 3: return "量子复制塔";
            case 4: return "点金塔";
            case 5: return "分解塔";
            case 6: return "转化塔";
            default: return "未知";
        }
    }

    public static Random random = new Random();

    public static ConfigEntry<bool>[] EnableFluidOutputStackEntryArr = new ConfigEntry<bool>[6];
    public static ConfigEntry<int>[] MaxProductOutputStackEntryArr = new ConfigEntry<int>[6];
    public static ConfigEntry<bool>[] EnableFracForeverEntryArr = new ConfigEntry<bool>[6];

    /// <summary>
    /// 是否启用上帝模式。
    /// </summary>
    public static ConfigEntry<bool> EnableGod;
    /// <summary>
    /// 加成倍数。
    /// </summary>
    public static ConfigEntry<float> MultiRateEntry;

    public static ConfigEntry<float> OutputStackEntry;

    public static void Init(ConfigFile configFile) {
        RecipeTypeEntry = configFile.Bind("config", "Recipe Type", 0, "想要查看的配方类型。");
        BuildingTypeEntry = configFile.Bind("config", "Building Type", 0, "想要查看的建筑类型。");
        EnableFluidOutputStackEntryArr[0] = InteractionTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[1] = MineralCopyTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[2] = PointAggregateTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[3] = QuantumCopyTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[4] = AlchemyTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[5] = DeconstructionTower.EnableFluidOutputStackEntry;
        EnableFluidOutputStackEntryArr[6] = ConversionTower.EnableFluidOutputStackEntry;

        MaxProductOutputStackEntryArr[0] = InteractionTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[1] = MineralCopyTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[2] = PointAggregateTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[3] = QuantumCopyTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[4] = AlchemyTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[5] = DeconstructionTower.MaxProductOutputStackEntry;
        MaxProductOutputStackEntryArr[6] = ConversionTower.MaxProductOutputStackEntry;

        EnableFracForeverEntryArr[0] = InteractionTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[1] = MineralCopyTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[2] = PointAggregateTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[3] = QuantumCopyTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[4] = AlchemyTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[5] = DeconstructionTower.EnableFracForeverEntry;
        EnableFracForeverEntryArr[6] = ConversionTower.EnableFracForeverEntry;

        EnableGod = configFile.Bind("config", "EnableGod", false,
            new ConfigDescription(
                "Enable god mode.\n"
                + "启用上帝模式。",
                new AcceptableBoolValue(false), null));

        MultiRateEntry = configFile.Bind("config", "MultiRate", 1.0f,
            new ConfigDescription(
                "Multi Rate.\n"
                + "加成倍数。",
                new AcceptableFloatValue(1.0f, 0.1f, 10.0f), null));
    }


    public static ItemProto InputItem { get; set; } = LDB.items.Select(I铁矿);

    public static void OnButtonChangeItemClick() {
        float x = _windowTrans.position.x + _windowTrans.rect.width + 10f;
        float y = _windowTrans.position.y;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            InputItem = item;
        }, false, item => GetRecipe<BaseRecipe>(RecipeTypeToERecipe(), item.ID) != null);
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
