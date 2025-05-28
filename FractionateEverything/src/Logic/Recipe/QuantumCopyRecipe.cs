using System;
using System.Collections.Generic;
using System.IO;

namespace FE.Logic.Recipe;

/// <summary>
/// 量子复制塔配方类（可复制所有物品）
/// </summary>
public class QuantumCopyRecipe : BaseRecipe {
    /// <summary>
    /// 存储所有量子复制配方的静态列表
    /// </summary>
    private static readonly List<QuantumCopyRecipe> quantumCopyRecipeList = [];

    /// <summary>
    /// 根据物品ID获取量子复制配方
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <returns>对应的量子复制配方，如果未找到则返回null</returns>
    public static QuantumCopyRecipe GetRecipe(int itemID) {
        foreach (QuantumCopyRecipe r in quantumCopyRecipeList) {
            if (r.ItemId == itemID) {
                return r;
            }
        }
        return null;
    }

    /// <summary>
    /// 添加配方到静态列表
    /// </summary>
    /// <param name="recipe">要添加的配方</param>
    public static void AddRecipe(QuantumCopyRecipe recipe) {
        quantumCopyRecipeList.Add(recipe);
    }

    /// <summary>
    /// 获取所有量子复制配方列表
    /// </summary>
    /// <returns>量子复制配方列表</returns>
    public static List<QuantumCopyRecipe> GetAllRecipes() {
        return quantumCopyRecipeList;
    }

    /// <summary>
    /// 清空配方列表
    /// </summary>
    public static void ClearRecipes() {
        quantumCopyRecipeList.Clear();
    }

    /// <summary>
    /// 创建量子复制塔配方实例
    /// </summary>
    /// <param name="id">配方ID</param>
    /// <param name="name">配方名称</param>
    /// <param name="description">配方描述</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="valueFactor">价值系数</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputRatio">产出比例</param>
    public QuantumCopyRecipe(int id, string name, string description, int itemId, float valueFactor = 1.0f,
        float baseSuccessRate = 0.05f, float outputRatio = 1.1f) {
        InputID = id;
        Name = name;
        Description = description;
        ItemId = itemId;
        ValueFactor = valueFactor;
        BaseSuccessRate = baseSuccessRate;
        OutputRatio = outputRatio;
        Level = 1;
        Star = 0;
        IsUnlocked = false;
    }

    /// <summary>
    /// 复制的物品ID
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// 基础产出比例（例如1.1表示平均每个输入产出1.1个）
    /// </summary>
    public float OutputRatio { get; set; } = 1.1f;

    /// <summary>
    /// 分馏精华产出概率
    /// </summary>
    public float EssenceChance { get; set; } = 0.05f;

    /// <summary>
    /// 物品价值系数（影响成功率，价值越高系数越大）
    /// </summary>
    public float ValueFactor { get; set; } = 1.0f;

    /// <summary>
    /// 是否不消耗材料（突破特殊属性）
    /// </summary>
    public bool NoMaterialConsumption { get; set; }

    /// <summary>
    /// 是否输出翻倍（突破特殊属性）
    /// </summary>
    public bool DoubleOutput { get; set; }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.QuantumDuplicate;

    /// <summary>
    /// 处理量子复制逻辑
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <param name="successRate">成功率</param>
    /// <param name="essenceId">分馏精华ID</param>
    /// <param name="proliferatorPoints">增产点数</param>
    /// <returns>复制结果，键为物品ID，值为数量</returns>
    public Dictionary<int, int> Process(Random random, float successRate, int essenceId, int proliferatorPoints) {
        Dictionary<int, int> result = new Dictionary<int, int>();

        // 根据增产点数调整成功率
        float pointsRatio = Math.Min(proliferatorPoints / 10.0f, 1.0f);// 10点时达到最大效果
        float adjustedSuccessRate = successRate * pointsRatio / ValueFactor;

        // 检查是否成功复制物品
        bool success = random.NextDouble() < adjustedSuccessRate;

        // 处理原材料消耗
        if (!NoMaterialConsumption) {
            result[ItemId] = 1;// 基础输出
        }

        // 如果成功复制
        if (success) {
            // 计算额外产出数量
            float extraAmount = OutputRatio - 1.0f;

            if (DoubleOutput) {
                extraAmount *= 2;
            }

            int extraCount = (int)extraAmount;
            float decimalPart = extraAmount - extraCount;

            // 处理小数部分
            if (decimalPart > 0 && random.NextDouble() < decimalPart) {
                extraCount++;
            }

            // 添加到结果
            if (extraCount > 0) {
                if (result.ContainsKey(ItemId)) {
                    result[ItemId] += extraCount;
                } else {
                    result[ItemId] = extraCount;
                }
            }

            // 检查是否产出分馏精华
            if (random.NextDouble() < EssenceChance) {
                result[essenceId] = result.ContainsKey(essenceId) ? result[essenceId] + 1 : 1;
            }
        }

        return result;
    }

    /// <summary>
    /// 应用突破加成
    /// </summary>
    /// <param name="bonusType">加成类型: 1=不消耗材料, 2=输出翻倍</param>
    public void ApplyBreakthroughBonus(int bonusType) {
        switch (bonusType) {
            case 1:
                NoMaterialConsumption = true;
                break;
            case 2:
                DoubleOutput = true;
                OutputRatio += 0.1f;// 突破时基础产出比例也会提升
                break;
        }

        // 突破时提升分馏精华产出概率
        EssenceChance += 0.01f;
    }

    /// <summary>
    /// 获取当前成功率（考虑物品价值和增产点数）
    /// </summary>
    /// <param name="proliferatorBonus">增产剂加成</param>
    /// <param name="proliferatorPoints">增产点数</param>
    /// <returns>调整后的成功率</returns>
    public float GetCurrentSuccessRate(float proliferatorBonus, int proliferatorPoints) {
        // 根据增产点数调整成功率
        float pointsRatio = Math.Min(proliferatorPoints / 10.0f, 1.0f);// 10点时达到最大效果

        // 基础成功率 * 点数比例 / 价值系数 + 增产剂加成
        return Math.Min(BaseSuccessRate * pointsRatio / ValueFactor + proliferatorBonus, 1.0f);
    }

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public override void Export(BinaryWriter w) {
        // 先调用基类的方法保存基本属性
        base.Export(w);

        // 保存量子复制塔特有属性
        w.Write(ItemId);
        w.Write(OutputRatio);
        w.Write(EssenceChance);
        w.Write(ValueFactor);
        w.Write(NoMaterialConsumption);
        w.Write(DoubleOutput);
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public override void Import(BinaryReader r) {
        // 先调用基类的方法读取基本属性
        base.Import(r);

        // 读取量子复制塔特有属性
        ItemId = r.ReadInt32();
        OutputRatio = r.ReadSingle();
        EssenceChance = r.ReadSingle();
        ValueFactor = r.ReadSingle();
        NoMaterialConsumption = r.ReadBoolean();
        DoubleOutput = r.ReadBoolean();
    }
}
