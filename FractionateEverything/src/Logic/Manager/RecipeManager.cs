using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        LogInfo("Begin to add fractionate recipes...");

        for (int i = 0; i < RecipeTypeArr.Length; i++) {
            RecipeTypeArr[i] = new BaseRecipe[12000];
        }

        BuildingTrainRecipe.CreateAll();
        MineralCopyRecipe.CreateAll();
        QuantumCopyRecipe.CreateAll();
        AlchemyRecipe.CreateAll();
        DeconstructionRecipe.CreateAll();
        ConversionRecipe.CreateAll();

        //横向：配方类型    纵向：矩阵层级
        int[,] counts = new int[RecipeMatrixDic.Keys.Count + 1, RecipeTypeDic.Keys.Count + 1];
        // 创建索引映射
        var matrixKeys = RecipeMatrixDic.Keys.ToArray();
        var recipeTypeKeys = RecipeTypeDic.Keys.ToArray();
        // 填充统计数据
        foreach (var recipe in RecipeList) {
            int matrixIndex = Array.IndexOf(matrixKeys, itemToMatrix[recipe.InputID]);
            int typeIndex = Array.IndexOf(recipeTypeKeys, recipe.RecipeType);
            if (matrixIndex >= 0 && typeIndex >= 0) {
                counts[matrixIndex, typeIndex]++;
            }
        }
        // 计算行列总计
        for (int i = 0; i < matrixKeys.Length; i++) {
            for (int j = 0; j < recipeTypeKeys.Length; j++) {
                counts[matrixKeys.Length, j] += counts[i, j];// 列总计
                counts[i, recipeTypeKeys.Length] += counts[i, j];// 行总计
            }
        }
        // 计算总计
        for (int i = 0; i < matrixKeys.Length; i++) {
            counts[matrixKeys.Length, recipeTypeKeys.Length] += counts[i, recipeTypeKeys.Length];
        }
        // 打印表格
        LogInfo("Recipe count table (Matrix × RecipeType):");
        // 辅助方法：计算字符串的显示宽度（中文字符算2个宽度）
        int GetDisplayWidth(string text) {
            int width = 0;
            foreach (char c in text) {
                if (c >= 0x4E00 && c <= 0x9FFF) { // 中文字符范围
                    width += 2;
                } else {
                    width += 1;
                }
            }
            return width;
        }
        // 辅助方法：按显示宽度右填充字符串
        string PadRightByWidth(string text, int totalWidth) {
            int currentWidth = GetDisplayWidth(text);
            int paddingNeeded = Math.Max(0, totalWidth - currentWidth);
            return text + new string(' ', paddingNeeded);
        }
        // 定义列宽
        int matrixColumnWidth = 10;
        int typeColumnWidth = 10;
        int totalColumnWidth = 8;
        // 打印表头
        string header = PadRightByWidth("矩阵层级", matrixColumnWidth);
        foreach (var recipeType in recipeTypeKeys) {
            header += PadRightByWidth(recipeType.GetShortName(), typeColumnWidth);
        }
        header += PadRightByWidth("总计", totalColumnWidth);
        LogInfo(header);
        // 打印分隔线
        LogInfo(new string('-', GetDisplayWidth(header)));
        // 打印数据行
        for (int i = 0; i < matrixKeys.Length; i++) {
            ItemProto matrix = LDB.items.Select(matrixKeys[i]);
            string row = PadRightByWidth(matrix.name, matrixColumnWidth);
            for (int j = 0; j < recipeTypeKeys.Length; j++) {
                row += PadRightByWidth(counts[i, j].ToString(), typeColumnWidth);
            }
            row += PadRightByWidth(counts[i, recipeTypeKeys.Length].ToString(), totalColumnWidth);
            LogInfo(row);
        }
        // 打印总计行
        string totalRow = PadRightByWidth("总计", matrixColumnWidth);
        for (int j = 0; j < recipeTypeKeys.Length; j++) {
            totalRow += PadRightByWidth(counts[matrixKeys.Length, j].ToString(), typeColumnWidth);
        }
        totalRow += PadRightByWidth(counts[matrixKeys.Length, recipeTypeKeys.Length].ToString(), totalColumnWidth);
        LogInfo(new string('-', GetDisplayWidth(header)));
        LogInfo(totalRow);

        LogInfo("Finish to add fractionate recipes.");
    }

    /// <summary>
    /// 添加一个配方
    /// </summary>
    public static void AddRecipe<T>(T recipe) where T : BaseRecipe {
        ERecipe recipeType = recipe.RecipeType;
        if (RecipeTypeArr[(int)recipeType][recipe.InputID] != null) {
            LogError(
                $"{recipeType.GetName()} already exists input item {recipe.InputID}({LDB.items.Select(recipe.InputID)}).");
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
        RecipeTypeArr[(int)recipeType][recipe.InputID] = recipe;
        //RecipeMatrixDic
        if (!RecipeMatrixDic.TryGetValue(itemToMatrix[recipe.InputID], out var recipeMatrixList)) {
            RecipeMatrixDic[itemToMatrix[recipe.InputID]] = [recipe];
        } else {
            recipeMatrixList.Add(recipe);
        }
        LogInfo($"Add {recipe.InputID}({LDB.items.Select(recipe.InputID).name}) to {recipeType.GetName()}.");
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
        if (matrixId == I宇宙矩阵) {
            return RecipeList;
        }
        return RecipeMatrixDic.TryGetValue(matrixId, out List<BaseRecipe> recipeList) ? recipeList : [];
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
            if (!recipe.IsUnlocked) {
                recipe.Level = 1;
                recipe.Quality = 1;
                LogInfo($"Unlocked {recipe.RecipeType} recipe - {LDB.items.Select(recipe.InputID).Name}");
            }
        }
        UIMessageBox.Show("提示".Translate(), "所有配方已解锁。".Translate(), "确定".Translate(), UIMessageBox.INFO, null);
    }

    #region 从存档读取配方数据

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
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
        w.Write(RecipeTypeDic.Count);
        foreach (var p in RecipeTypeDic) {
            w.Write((int)p.Key);
            w.Write(p.Value.Count);
            foreach (BaseRecipe recipe in p.Value) {
                w.Write(recipe.InputID);
                recipe.Export(w);
            }
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