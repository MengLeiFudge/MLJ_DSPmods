using System;
using System.Collections.Generic;
using System.IO;

namespace FE.Logic.Recipe;

/// <summary>
/// 点金塔配方类（1A -> X矩阵1 + Y矩阵2 + Z矩阵3）
/// </summary>
public class AlchemyRecipe : BaseRecipe {
    /// <summary>
    /// 存储所有点金配方的静态列表
    /// </summary>
    private static readonly List<AlchemyRecipe> alchemyRecipeList = [];

    /// <summary>
    /// 根据输入物品ID获取点金配方
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <returns>对应的点金配方，如果未找到则返回null</returns>
    public static AlchemyRecipe GetRecipe(int inputID) {
        foreach (AlchemyRecipe r in alchemyRecipeList) {
            if (r.InputItemId == inputID) {
                return r;
            }
        }
        return null;
    }

    /// <summary>
    /// 添加配方到静态列表
    /// </summary>
    /// <param name="recipe">要添加的配方</param>
    public static void AddRecipe(AlchemyRecipe recipe) {
        alchemyRecipeList.Add(recipe);
    }

    /// <summary>
    /// 获取所有点金配方列表
    /// </summary>
    /// <returns>点金配方列表</returns>
    public static List<AlchemyRecipe> GetAllRecipes() {
        return alchemyRecipeList;
    }

    /// <summary>
    /// 清空配方列表
    /// </summary>
    public static void ClearRecipes() {
        alchemyRecipeList.Clear();
    }

    /// <summary>
    /// 创建点金塔配方实例
    /// </summary>
    /// <param name="inputItemId">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="level">配方等级</param>
    /// <param name="star">配方星级</param>
    public AlchemyRecipe(int inputItemId, float baseSuccessRate = 0.05f, int level = 1, int star = 0) {
        InputItemId = inputItemId;
        BaseSuccessRate = baseSuccessRate;
        Level = level;
        Star = star;
        IsUnlocked = false;

        // 初始化矩阵输出概率表
        MatrixOutputs = new Dictionary<int, float>();
    }

    /// <summary>
    /// 输入物品ID
    /// </summary>
    public int InputItemId { get; set; }

    /// <summary>
    /// 矩阵输出概率表
    /// 结构: Dictionary<矩阵ID, 成功率>
    /// </summary>
    public Dictionary<int, float> MatrixOutputs { get; set; } = new Dictionary<int, float>();

    /// <summary>
    /// 是否不消耗材料（突破特殊属性）
    /// </summary>
    public bool NoMaterialConsumption { get; set; }

    /// <summary>
    /// 是否输出翻倍（突破特殊属性）
    /// </summary>
    public bool DoubleOutput { get; set; }

    /// <summary>
    /// 优先矩阵ID（突破特殊属性）
    /// </summary>
    public int PriorityMatrixId { get; set; }

    /// <summary>
    /// 优先矩阵加成系数（突破特殊属性）
    /// </summary>
    public float PriorityBonus { get; set; } = 1.0f;

    /// <summary>
    /// 物品价值系数（影响可能产出的矩阵种类和概率）
    /// </summary>
    public float ValueFactor { get; set; } = 1.0f;

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Alchemy;

    /// <summary>
    /// 添加矩阵输出
    /// </summary>
    /// <param name="matrixId">矩阵ID</param>
    /// <param name="probability">产出概率</param>
    public void AddMatrixOutput(int matrixId, float probability) {
        MatrixOutputs[matrixId] = probability;
    }

    /// <summary>
    /// 移除矩阵输出
    /// </summary>
    /// <param name="matrixId">矩阵ID</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveMatrixOutput(int matrixId) {
        return MatrixOutputs.Remove(matrixId);
    }

    /// <summary>
    /// 处理点金逻辑
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <param name="successRate">成功率</param>
    /// <returns>点金结果，键为矩阵ID，值为数量</returns>
    public Dictionary<int, int> Process(Random random, float successRate) {
        Dictionary<int, int> result = new Dictionary<int, int>();

        // 检查是否成功转化
        if (random.NextDouble() > successRate) {
            // 转化失败，如果不消耗材料则返回原物品
            if (NoMaterialConsumption) {
                result[InputItemId] = 1;
            }
            return result;
        }

        // 处理所有可能的矩阵产出
        foreach (var matrix in MatrixOutputs) {
            int matrixId = matrix.Key;
            float probability = matrix.Value;

            // 应用价值系数调整
            float adjustedProbability = probability * ValueFactor;

            // 应用优先矩阵加成
            if (matrixId == PriorityMatrixId && PriorityBonus > 1.0f) {
                adjustedProbability *= PriorityBonus;
            }

            // 检查该矩阵是否产出
            if (random.NextDouble() < adjustedProbability) {
                int outputCount = 1;

                // 应用翻倍效果
                if (DoubleOutput) {
                    outputCount *= 2;
                }

                result[matrixId] = outputCount;

                // 每次最多产出一种矩阵
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// 应用突破加成
    /// </summary>
    /// <param name="bonusType">加成类型: 1=不消耗材料, 2=输出翻倍, 3=优先矩阵</param>
    /// <param name="priorityMatrixId">优先矩阵ID（仅在bonusType=3时有效）</param>
    public void ApplyBreakthroughBonus(int bonusType, int priorityMatrixId = 0) {
        switch (bonusType) {
            case 1:
                NoMaterialConsumption = true;
                break;
            case 2:
                DoubleOutput = true;
                break;
            case 3:
                PriorityMatrixId = priorityMatrixId;
                PriorityBonus = 2.0f;// 优先矩阵概率加倍
                break;
        }
    }

    /// <summary>
    /// 根据物品价值自动设置可能的矩阵输出
    /// </summary>
    /// <param name="itemValue">物品价值</param>
    public void SetupMatrixOutputsByValue(float itemValue) {
        // 清空现有输出
        MatrixOutputs.Clear();

        // 价值系数影响可能产出的矩阵类型
        ValueFactor = Math.Max(0.1f, Math.Min(2.0f, itemValue / 100.0f));

        // 根据物品价值设置各种矩阵的产出概率
        // 物品价值越高，产出高级矩阵的概率越大

        // 电磁矩阵 (ID 1101)
        MatrixOutputs[1101] = Math.Max(0.01f, 0.5f - (itemValue / 500.0f));

        // 能量矩阵 (ID 1102)
        if (itemValue > 50) {
            MatrixOutputs[1102] = Math.Max(0.01f, 0.3f - (itemValue / 1000.0f));
        }

        // 结构矩阵 (ID 1103)
        if (itemValue > 200) {
            MatrixOutputs[1103] = Math.Max(0.01f, 0.2f - (itemValue / 2000.0f));
        }

        // 信息矩阵 (ID 1104)
        if (itemValue > 500) {
            MatrixOutputs[1104] = Math.Max(0.01f, 0.1f - (itemValue / 5000.0f));
        }

        // 引力矩阵 (ID 1105)
        if (itemValue > 1000) {
            MatrixOutputs[1105] = Math.Max(0.01f, 0.05f - (itemValue / 10000.0f));
        }

        // 宇宙矩阵 (ID 1106)
        if (itemValue > 5000) {
            MatrixOutputs[1106] = Math.Max(0.005f, 0.02f - (itemValue / 50000.0f));
        }
    }

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public override void Export(BinaryWriter w) {
        // 先调用基类的方法保存基本属性
        base.Export(w);

        // 保存点金塔特有属性
        w.Write(InputItemId);
        w.Write(ValueFactor);
        w.Write(NoMaterialConsumption);
        w.Write(DoubleOutput);
        w.Write(PriorityMatrixId);
        w.Write(PriorityBonus);

        // 保存矩阵输出表
        w.Write(MatrixOutputs.Count);
        foreach (var kvp in MatrixOutputs) {
            w.Write(kvp.Key);// 矩阵ID
            w.Write(kvp.Value);// 概率
        }
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public override void Import(BinaryReader r) {
        // 先调用基类的方法读取基本属性
        base.Import(r);

        // 读取点金塔特有属性
        InputItemId = r.ReadInt32();
        ValueFactor = r.ReadSingle();
        NoMaterialConsumption = r.ReadBoolean();
        DoubleOutput = r.ReadBoolean();
        PriorityMatrixId = r.ReadInt32();
        PriorityBonus = r.ReadSingle();

        // 读取矩阵输出表
        int outputCount = r.ReadInt32();
        MatrixOutputs.Clear();
        for (int i = 0; i < outputCount; i++) {
            int matrixId = r.ReadInt32();
            float probability = r.ReadSingle();
            MatrixOutputs[matrixId] = probability;
        }
    }
}
