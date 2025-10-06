using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Recipe;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

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

#if DEBUG
    private const string SPRITE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
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
        QuantumCopyRecipe.CreateAll();
        AlchemyRecipe.CreateAll();
        DeconstructionRecipe.CreateAll();
        ConversionRecipe.CreateAll();

        LogInfo($"Added {RecipeList.Count} fractionate recipes.");
    }

    /// <summary>
    /// 添加一个配方
    /// </summary>
    public static void AddRecipe<T>(T recipe) where T : BaseRecipe {
        ERecipe recipeType = recipe.RecipeType;
        int inputID = recipe.InputID;
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
        //量子复制配方自动提升一个矩阵级别
        if (recipe.RecipeType == ERecipe.QuantumCopy && recipe.MatrixID >= I电磁矩阵 && recipe.MatrixID < I宇宙矩阵) {
            recipe.MatrixID++;
        }
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

    #endregion

    /// <summary>
    /// 解锁全部配方
    /// </summary>
    public static void UnlockAllFracRecipes() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        foreach (var recipe in RecipeList) {
            if (recipe.Locked) {
                recipe.RewardThis();
                LogInfo($"Unlocked {recipe.RecipeType} recipe - {LDB.items.Select(recipe.InputID).Name}");
            }
        }
        UIMessageBox.Show("提示".Translate(),
            "所有分馏配方已解锁。".Translate(),
            "确定".Translate(), UIMessageBox.INFO,
            null);
    }

    #region 从存档读取配方数据

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        int recipeCount = r.ReadInt32();
        for (int i = 0; i < recipeCount; i++) {
            ERecipe recipeType = (ERecipe)r.ReadInt32();
            int inputID = r.ReadInt32();
            // 读取单个配方的数据长度，用于跳过未知配方
            int recipeDataLength = r.ReadInt32();
            long startPosition = r.BaseStream.Position;
            BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, inputID);
            if (recipe == null) {
                // 配方不存在，跳过这个配方的数据
                LogWarning($"Recipe not found: {recipeType} with input {inputID}, skipping data");
                r.BaseStream.Seek(startPosition + recipeDataLength, SeekOrigin.Begin);
            } else {
                try {
                    recipe.Import(r);
                    // 验证读取的数据长度是否正确
                    long actualRead = r.BaseStream.Position - startPosition;
                    if (actualRead != recipeDataLength) {
                        LogWarning(
                            $"Recipe data length mismatch for {recipeType}-{inputID}: expected {recipeDataLength}, actual {actualRead}");
                        // 调整到正确位置
                        r.BaseStream.Seek(startPosition + recipeDataLength, SeekOrigin.Begin);
                    }
                }
                catch (Exception ex) {
                    LogError($"Failed to import recipe {recipeType}-{inputID}: {ex.Message}");
                    // 跳过损坏的数据
                    r.BaseStream.Seek(startPosition + recipeDataLength, SeekOrigin.Begin);
                }
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(RecipeList.Count);
        foreach (var recipe in RecipeList) {
            w.Write((int)recipe.RecipeType);
            w.Write(recipe.InputID);
            // 使用内存流计算数据长度
            using var memoryStream = new MemoryStream();
            using var tempWriter = new BinaryWriter(memoryStream);
            recipe.Export(tempWriter);
            byte[] recipeData = memoryStream.ToArray();
            // 写入数据长度和实际数据
            w.Write(recipeData.Length);
            w.Write(recipeData);
        }
    }

    public static void IntoOtherSave() {
        foreach (var p in RecipeTypeDic) {
            foreach (BaseRecipe recipe in p.Value) {
                recipe.IntoOtherSave();
            }
        }
    }

    #endregion
}
