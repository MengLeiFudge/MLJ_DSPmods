using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Logic.Recipe;
using static FE.Utils.LogUtils;

namespace FE.Logic.Manager;

public static class RecipeManager {
    #region 配方列表

    /// <summary>
    /// 配方列表，[(int)ERecipe][(int)ItemID]
    /// </summary>
    private static BaseRecipe[][] BaseRecipes = null;

    /// <summary>
    /// 临时存储所有配方，生成baseRecipes之后就不需要了
    /// </summary>
    private static readonly Dictionary<ERecipe, List<BaseRecipe>> RecipesWithType = [];

    /// <summary>
    /// 存储所有配方
    /// </summary>
    private static readonly List<BaseRecipe> RecipeList = [];

    /// <summary>
    /// 添加一个配方
    /// </summary>
    public static void AddRecipe<T>(T recipe) where T : BaseRecipe {
        ERecipe recipeType = recipe.RecipeType;
        if (!RecipesWithType.TryGetValue(recipeType, out var recipeList)) {
            recipeList = [];
            RecipesWithType[recipeType] = recipeList;
        }
        if (recipeList.Any(r => r.InputID == recipe.InputID)) {
            LogError($"Recipe with ID {recipe.InputID} already exists for type {recipeType}");
            return;
        }
        recipeList.Add(recipe);
        RecipeList.Add(recipe);
        LogInfo($"Add {recipe.InputID} {LDB.items.Select(recipe.InputID).Name} to {recipeType.ToString()} Recipe.");
    }

    /// <summary>
    /// 获取指定类型的指定输入ID的配方
    /// </summary>
    /// <param name="recipeType">要获取的配方类型</param>
    /// <param name="inputId">要获取的配方的输入ID</param>
    /// <typeparam name="T">BaseRecipe的子类</typeparam>
    /// <returns>类型为recipeType，输入物品ID为inputId的配方。找不到返回null</returns>
    public static T GetRecipe<T>(ERecipe recipeType, int inputId) where T : BaseRecipe {
        return BaseRecipes[(int)recipeType][inputId] as T;
    }

    public static BaseRecipe[] GetRecipes(ERecipe recipeType) {
        return BaseRecipes[(int)recipeType];
    }

    #endregion

    public static void UnlockAll() {
        foreach (var recipe in RecipeList) {
            if (!recipe.IsUnlocked) {
                recipe.Level = 1;
                recipe.Quality = 1;
                LogInfo($"Unlocked {recipe.RecipeType} recipe - {LDB.items.Select(recipe.InputID).Name}");
            }
        }
    }

    #region 创建配方

#if DEBUG
    private const string SPRITE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
    private const string SPRITE_CSV_PATH = $@"{SPRITE_CSV_DIR}\fracIconPath.csv";
#endif

    public static void AddBaseRecipes() {
#if DEBUG
        if (File.Exists(SPRITE_CSV_PATH)) {
            File.Delete(SPRITE_CSV_PATH);
        }
#endif
        LogInfo("Begin to add fractionate recipes...");

        MineralCopyRecipe.CreateAll();
        QuantumCopyRecipe.CreateAll();
        AlchemyRecipe.CreateAll();
        DeconstructionRecipe.CreateAll();
        ConversionRecipe.CreateAll();

        BaseRecipes = new BaseRecipe[Enum.GetNames(typeof(ERecipe)).Length + 1][];
        for (int i = 0; i < BaseRecipes.Length; i++) {
            BaseRecipes[i] = new BaseRecipe[12000];
        }
        foreach (var p in RecipesWithType) {
            foreach (var recipe in p.Value) {
                BaseRecipes[(int)p.Key][recipe.InputID] = recipe;
            }
        }

        LogInfo("Finish to add fractionate recipes.");
    }

    #endregion

    #region 从存档读取配方数据

    public static void Import(BinaryReader r) {
        int typeCount = r.ReadInt32();
        for (int typeIndex = 0; typeIndex < typeCount; typeIndex++) {
            ERecipe recipeType = (ERecipe)r.ReadInt32();
            int recipeCount = r.ReadInt32();
            for (int i = 0; i < recipeCount; i++) {
                int inputID = r.ReadInt32();
                BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, inputID);
                if (recipe == null) {
                    int byteCount = r.ReadInt32();
                    for (int j = 0; j < byteCount; j++) {
                        r.ReadByte();
                    }
                } else {
                    recipe.Import(r);
                }
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(RecipesWithType.Count);
        foreach (var p in RecipesWithType) {
            w.Write((int)p.Key);
            w.Write(p.Value.Count);
            foreach (BaseRecipe recipe in p.Value) {
                w.Write(recipe.InputID);
                recipe.Export(w);
            }
        }
    }

    public static void IntoOtherSave() { }

    #endregion
}
