using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;
using static FE.Logic.Fractionation.FracRecipes.ERecipeExtension;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 分馏配方创建、查找、翻译和存档聚合入口。
/// </summary>
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
    private static readonly BaseRecipe[][] RecipeTypeArr =
        new BaseRecipe[Enum.GetValues(typeof(ERecipe)).Cast<int>().Max() + 1][];

    /// <summary>
    /// 按物品科技层级分类配方，Key：(int)MatrixID
    /// </summary>
    private static readonly Dictionary<int, List<BaseRecipe>> RecipeMatrixDic = [];
    private static bool fracRecipesReady;

#if DEBUG
    private const string SPRITE_CSV_DIR = @"D:\project\dsp\MLJ_DSPmods\gamedata";
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
        fracRecipesReady = false;

        for (int i = 0; i < RecipeTypeArr.Length; i++) {
            RecipeTypeArr[i] = new BaseRecipe[12000];
        }

        BuildingTrainRecipe.CreateAll();
        MineralCopyRecipe.CreateAll();
        ConversionRecipe.CreateAll();
        RectificationRecipe.CreateAll();
        fracRecipesReady = true;

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
    /// 按配方类型和输入物品读取分馏配方。
    /// </summary>
    /// <param name="recipeType">配方类型。</param>
    /// <param name="inputId">输入物品 ID。</param>
    /// <typeparam name="T">需要返回的配方子类。</typeparam>
    /// <returns>匹配配方；不存在时返回 null。</returns>
    public static T GetRecipe<T>(ERecipe recipeType, int inputId) where T : BaseRecipe {
        int recipeTypeIndex = (int)recipeType;
        if (recipeTypeIndex <= 0 || recipeTypeIndex >= RecipeTypeArr.Length || inputId <= 0) {
            return null;
        }
        BaseRecipe[] recipeArr = RecipeTypeArr[recipeTypeIndex];
        if (recipeArr == null || inputId >= recipeArr.Length) {
            return null;
        }
        return recipeArr[inputId] as T;
    }

    /// <summary>
    /// 获取当前已注册的全部 FE 分馏配方。
    /// </summary>
    public static IReadOnlyList<BaseRecipe> AllRecipes => RecipeList;
    /// <summary>
    /// 判断分馏配方注册流程是否已完成。
    /// </summary>
    public static bool AreFracRecipesReady => fracRecipesReady;

    /// <summary>
    /// 按分馏配方类型查询配方列表。
    /// </summary>
    public static List<BaseRecipe> GetRecipesByType(ERecipe recipeType) {
        return RecipeTypeDic.TryGetValue(recipeType, out List<BaseRecipe> recipeList) ? recipeList : [];
    }

    /// <summary>
    /// 按矩阵阶段查询配方列表。
    /// </summary>
    public static List<BaseRecipe> GetRecipesByMatrix(int matrixId) {
        if (matrixId < I电磁矩阵 || matrixId > I宇宙矩阵) {
            //当成黑雾矩阵处理
            return RecipeMatrixDic.TryGetValue(I黑雾矩阵, out List<BaseRecipe> recipeList) ? recipeList : [];
        } else {
            return RecipeMatrixDic.TryGetValue(matrixId, out List<BaseRecipe> recipeList) ? recipeList : [];
        }
    }

    /// <summary>
    /// 查询指定矩阵阶段以下的分层配方列表。
    /// </summary>
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

    #endregion

    #region 从存档读取配方数据

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
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
            })
        );
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
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
            })
        );
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public static void IntoOtherSave() {
        foreach (var p in RecipeTypeDic) {
            foreach (BaseRecipe recipe in p.Value) {
                recipe.IntoOtherSave();
            }
        }
    }

    #endregion
}
