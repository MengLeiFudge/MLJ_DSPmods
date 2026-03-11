using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Logic.Recipe;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;
using static FE.Logic.Recipe.ERecipeExtension;

namespace FE.Logic.Manager;

public static class RecipeManager {
    #region 配方创建、读取

    /// <summary>
    /// 所有配方
    /// </summary>
    private static readonly List<BaseRecipe> RecipeList = [];

    /// <summary>
    /// 按配方类型分类配方
    /// </summary>
    private static readonly Dictionary<ERecipe, List<BaseRecipe>> RecipeTypeDic = [];

    /// <summary>
    /// 按配方类型分类配方，加快访问速度，格式：[(int)ERecipe][(int)ItemID]
    /// </summary>
    private static readonly BaseRecipe[][] RecipeTypeArr = new BaseRecipe[Enum.GetNames(typeof(ERecipe)).Length + 1][];

    /// <summary>
    /// 按物品科技层级分类配方，Key：(int)MatrixID
    /// </summary>
    private static readonly Dictionary<int, List<BaseRecipe>> RecipeMatrixDic = [];

    /// <summary>
    /// 游戏配方
    /// </summary>
    private static readonly List<VanillaRecipe> VanillaRecipeList = [];

#if DEBUG
    private const string SPRITE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\gamedata";
    private const string SPRITE_CSV_PATH = $@"{SPRITE_CSV_DIR}\fracIconPath.csv";
#endif

    /// <summary>
    /// 添加所有分馏配方
    /// </summary>
    public static void AddFracRecipes() {
#if DEBUG
        if (File.Exists(SPRITE_CSV_PATH)) {
            File.Delete(SPRITE_CSV_PATH);
        }
#endif
        LogInfo("Add fractionate recipes...");

        for (int i = 0; i < RecipeTypeArr.Length; i++) {
            RecipeTypeArr[i] = new BaseRecipe[12000];
        }

        BuildingTrainRecipe.CreateAll();
        MineralCopyRecipe.CreateAll();
        PointAggregateRecipe.CreateAll();
        ConversionRecipe.CreateAll();
        RecycleRecipe.CreateAll();

        LogInfo($"Added {RecipeList.Count} fractionate recipes.");
    }

    /// <summary>
    /// 添加一个配方
    /// </summary>
    public static void AddRecipe<T>(T recipe) where T : BaseRecipe {
        ERecipe recipeType = recipe.RecipeType;
        int inputID = recipe.InputID;
        if (inputID <= 0 || inputID >= 12000) {
            LogError($"{recipeType.GetName()} input item {inputID} is out of range.");
            return;
        }
        if (RecipeTypeArr[(int)recipeType][inputID] != null) {
            LogError($"{recipeType.GetName()} already exists input item {inputID}({LDB.items.Select(inputID)}).");
            return;
        }
        if (itemToMatrix[inputID] == 0) {
            LogError($"{recipeType.GetName()} item {inputID}({LDB.items.Select(inputID)}) matrix is 0.");
            return;
        }
        //RecipeList
        RecipeList.Add(recipe);
        //RecipeTypeDic
        if (!RecipeTypeDic.TryGetValue(recipeType, out var recipeTypeList)) {
            RecipeTypeDic[recipeType] = [recipe];
        } else {
            recipeTypeList.Add(recipe);
        }
        //RecipeTypeArr
        RecipeTypeArr[(int)recipeType][inputID] = recipe;
        //RecipeMatrixDic
        recipe.MatrixID = itemToMatrix[inputID];
        if (!RecipeMatrixDic.TryGetValue(recipe.MatrixID, out var recipeMatrixList)) {
            RecipeMatrixDic[recipe.MatrixID] = [recipe];
        } else {
            recipeMatrixList.Add(recipe);
        }
        // LogInfo($"Add {inputID}({LDB.items.Select(inputID).name}) to {recipeType.GetName()}.");
    }

    /// <summary>
    /// 获取指定类型的指定输入ID的配方
    /// </summary>
    /// <param name="recipeType">要获取的配方类型</param>
    /// <param name="inputId">要获取的配方的输入ID</param>
    /// <typeparam name="T">BaseRecipe的子类</typeparam>
    /// <returns>类型为recipeType，输入物品ID为inputId的配方。找不到返回null</returns>
    public static T GetRecipe<T>(ERecipe recipeType, int inputId) where T : BaseRecipe {
        if (inputId <= 0 || inputId >= 12000) {
            return null;
        }
        return RecipeTypeArr[(int)recipeType][inputId] as T;
    }

    public static List<BaseRecipe> GetRecipesByType(ERecipe recipeType) {
        return RecipeTypeDic.TryGetValue(recipeType, out List<BaseRecipe> recipeList) ? recipeList : [];
    }

    public static List<BaseRecipe> GetRecipesByMatrix(int matrixId) {
        if (matrixId < I电磁矩阵 || matrixId > I宇宙矩阵) {
            //当成黑雾矩阵处理
            return RecipeMatrixDic.TryGetValue(I黑雾矩阵, out List<BaseRecipe> recipeList) ? recipeList : [];
        } else {
            return RecipeMatrixDic.TryGetValue(matrixId, out List<BaseRecipe> recipeList) ? recipeList : [];
        }
    }

    public static List<List<BaseRecipe>> GetRecipesUnderMatrix(int topMatrixId) {
        List<List<BaseRecipe>> ret = [];
        if (topMatrixId < I电磁矩阵 || topMatrixId > I宇宙矩阵) {
            //当成黑雾矩阵处理
            ret.Add(RecipeMatrixDic.TryGetValue(I黑雾矩阵, out List<BaseRecipe> recipeList) ? recipeList : []);
        } else {
            for (int matrixId = I电磁矩阵; matrixId <= topMatrixId; matrixId++) {
                ret.Add(RecipeMatrixDic.TryGetValue(matrixId, out List<BaseRecipe> recipeList) ? recipeList : []);
            }
        }
        return ret;
    }

    /// <summary>
    /// 添加原版配方
    /// </summary>
    public static void AddVanillaRecipes() {
        LogInfo("Add vanilla recipes...");
        foreach (var recipe in LDB.recipes.dataArray) {
            VanillaRecipeList.Add(new(recipe));
        }
        LogInfo($"Added {VanillaRecipeList.Count} vanilla recipes.");
    }

    /// <summary>
    /// 获取指定配方ID对应的原版配方
    /// </summary>
    public static VanillaRecipe GetVanillaRecipe(int recipeId) {
        return VanillaRecipeList.Find(vr => vr.recipe.ID == recipeId);
    }

    #endregion

    /// <summary>
    /// 全部配方等级=-1
    /// </summary>
    public static void LockAllFracRecipes() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        foreach (var recipe in RecipeList) {
            if (RecipeTypes.Contains(recipe.RecipeType)) {
                recipe.Level = -1;
            }
        }
        UIMessageBox.Show("提示".Translate(),
            "所有分馏配方已锁定。".Translate(),
            "确定".Translate(), UIMessageBox.INFO,
            null);
    }

    /// <summary>
    /// 全部配方等级++
    /// </summary>
    public static void RewardAllFracRecipes() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        foreach (var recipe in RecipeList) {
            if (RecipeTypes.Contains(recipe.RecipeType)) {
                recipe.RewardThis(true);
            }
        }
        UIMessageBox.Show("提示".Translate(),
            "所有分馏配方已等级+1。".Translate(),
            "确定".Translate(), UIMessageBox.INFO,
            null);
    }

    /// <summary>
    /// 全部配方等级=10
    /// </summary>
    public static void MaxAllFracRecipes() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        foreach (var recipe in RecipeList) {
            if (RecipeTypes.Contains(recipe.RecipeType)) {
                recipe.Level = 10;
            }
        }
        UIMessageBox.Show("提示".Translate(),
            "所有分馏配方已满级。".Translate(),
            "确定".Translate(), UIMessageBox.INFO,
            null);
    }

    #region 从存档读取配方数据

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("MainRecipes", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    ERecipe recipeType = (ERecipe)br.ReadInt32();
                    int inputID = br.ReadInt32();
                    var fRecipe = GetRecipe<BaseRecipe>(recipeType, inputID);
                    // 不管有没有实例对象，都必须执行 ReadBlocks 以确保流位置正确
                    br.ReadBlocks(
                        ("RecipeData", br => fRecipe?.Import(br))
                    );
                }
            }),
            ("VanillaRecipes", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int recipeID = br.ReadInt32();
                    var vRecipe = GetVanillaRecipe(recipeID);
                    // 不管有没有实例对象，都必须执行 ReadBlocks 以确保流位置正确
                    br.ReadBlocks(
                        ("VanillaData", br => vRecipe?.Import(br))
                    );
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("MainRecipes", bw => {
                bw.Write(RecipeList.Count);
                foreach (var fRecipe in RecipeList) {
                    bw.Write((int)fRecipe.RecipeType);
                    bw.Write(fRecipe.InputID);
                    bw.WriteBlocks(
                        ("RecipeData", fRecipe.Export)
                    );
                }
            }),
            ("VanillaRecipes", bw => {
                bw.Write(VanillaRecipeList.Count);
                foreach (var vRecipe in VanillaRecipeList) {
                    bw.Write(vRecipe.recipe.ID);
                    bw.WriteBlocks(
                        ("VanillaData", vRecipe.Export)
                    );
                }
            })
        );
    }

    public static void IntoOtherSave() {
        foreach (var p in RecipeTypeDic) {
            foreach (BaseRecipe recipe in p.Value) {
                recipe.IntoOtherSave();
            }
        }
        foreach (var vanillaRecipe in VanillaRecipeList) {
            vanillaRecipe.IntoOtherSave();
        }
    }

    #endregion
}
