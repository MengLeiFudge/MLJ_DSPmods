using System;
using System.Collections.Generic;
using System.IO;

namespace FE.Logic.Recipe;

/// <summary>
/// 转化塔配方类（1A -> XA + YB + ZC）
/// </summary>
public class ConversionRecipe : BaseRecipe {
    /// <summary>
    /// 存储所有转化配方的静态列表
    /// </summary>
    private static readonly List<ConversionRecipe> conversionRecipeList = [];

    /// <summary>
    /// 根据输入物品ID获取转化配方
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <returns>对应的转化配方，如果未找到则返回null</returns>
    public static ConversionRecipe GetRecipe(int inputID) {
        foreach (ConversionRecipe r in conversionRecipeList) {
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
    public static void AddRecipe(ConversionRecipe recipe) {
        conversionRecipeList.Add(recipe);
    }

    /// <summary>
    /// 获取所有转化配方列表
    /// </summary>
    /// <returns>转化配方列表</returns>
    public static List<ConversionRecipe> GetAllRecipes() {
        return conversionRecipeList;
    }

    /// <summary>
    /// 清空配方列表
    /// </summary>
    public static void ClearRecipes() {
        conversionRecipeList.Clear();
    }

    /// <summary>
    /// 创建转化塔配方实例
    /// </summary>
    /// <param name="id">配方ID</param>
    /// <param name="name">配方名称</param>
    /// <param name="description">配方描述</param>
    /// <param name="inputItemId">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="destructionRate">损毁率</param>
    public ConversionRecipe(int inputItemId, List<OutputItem> outputs,
        float baseSuccessRate = 0.05f, float destructionRate = 0.01f, int level = 1, int star = 0) {
        InputItemId = inputItemId;
        BaseSuccessRate = baseSuccessRate;
        DestructionRate = destructionRate;
        Level = level;
        Star = star;
        IsUnlocked = false;

        // 初始化输出物品
        OutputItems = new Dictionary<int, Tuple<float, float>>();
        foreach (var output in outputs) {
            OutputItems[output.ItemId] = new Tuple<float, float>(output.Ratio, output.Count);
        }
    }

    /// <summary>
    /// 向后兼容的构造函数，支持元组列表
    /// </summary>
    public ConversionRecipe(int inputItemId, List<(int outputId, float ratio, float count)> outputs,
        float baseSuccessRate = 0.05f, float destructionRate = 0.01f, int level = 1, int star = 0)
        : this(inputItemId, ConvertToOutputItems(outputs), baseSuccessRate, destructionRate, level, star) { }

    /// <summary>
    /// 将元组列表转换为OutputItem列表
    /// </summary>
    private static List<OutputItem> ConvertToOutputItems(List<(int outputId, float ratio, float count)> tuples) {
        var result = new List<OutputItem>(tuples.Count);
        foreach (var tuple in tuples) {
            result.Add(new OutputItem(tuple.outputId, tuple.ratio, tuple.count));
        }
        return result;
    }

    /// <summary>
    /// 输入物品ID
    /// </summary>
    public int InputItemId { get; set; }

    /// <summary>
    /// 转化产物表
    /// 结构: Dictionary<物品ID, Tuple<成功率, 数量>>
    /// </summary>
    public Dictionary<int, Tuple<float, float>> OutputItems { get; set; } = new Dictionary<int, Tuple<float, float>>();

    /// <summary>
    /// 损毁率（材料消失的概率）
    /// </summary>
    public float DestructionRate { get; set; }

    /// <summary>
    /// 是否不消耗材料（突破特殊属性）
    /// </summary>
    public bool NoMaterialConsumption { get; set; }

    /// <summary>
    /// 是否输出翻倍（突破特殊属性）
    /// </summary>
    public bool DoubleOutput { get; set; }

    /// <summary>
    /// 专精产物ID（突破特殊属性）
    /// </summary>
    public int SpecializedOutputId { get; set; }

    /// <summary>
    /// 专精产物加成系数（突破特殊属性）
    /// </summary>
    public float SpecializedBonus { get; set; } = 1.0f;

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Conversion;

    /// <summary>
    /// 获取所有输出物品
    /// </summary>
    /// <returns>输出物品列表</returns>
    public List<OutputItem> GetOutputItems() {
        var result = new List<OutputItem>(OutputItems.Count);
        foreach (var kvp in OutputItems) {
            result.Add(new OutputItem(kvp.Key, kvp.Value.Item1, kvp.Value.Item2));
        }
        return result;
    }

    /// <summary>
    /// 添加输出物品
    /// </summary>
    /// <param name="output">要添加的输出物品</param>
    public void AddOutputItem(OutputItem output) {
        OutputItems[output.ItemId] = new Tuple<float, float>(output.Ratio, output.Count);
    }

    /// <summary>
    /// 添加输出物品
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="ratio">成功率</param>
    /// <param name="count">数量</param>
    public void AddOutputItem(int itemId, float ratio, float count) {
        OutputItems[itemId] = new Tuple<float, float>(ratio, count);
    }

    /// <summary>
    /// 移除输出物品
    /// </summary>
    /// <param name="itemId">要移除的物品ID</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveOutputItem(int itemId) {
        return OutputItems.Remove(itemId);
    }

    /// <summary>
    /// 处理转化逻辑
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <param name="successRate">成功率</param>
    /// <returns>转化结果，键为物品ID，值为数量</returns>
    public Dictionary<int, int> Process(Random random, float successRate) {
        Dictionary<int, int> result = new Dictionary<int, int>();

        // 检查是否成功转化
        if (random.NextDouble() > successRate) {
            // 检查是否损毁
            if (random.NextDouble() < DestructionRate) {
                // 材料损毁，返回空
                return result;
            }

            // 未成功也未损毁，原物品流出
            if (!NoMaterialConsumption) {
                result[InputItemId] = 1;
            }

            return result;
        }

        // 处理所有可能的产物
        foreach (var output in OutputItems) {
            int itemId = output.Key;
            float probability = output.Value.Item1;
            float amount = output.Value.Item2;

            // 应用专精加成
            if (itemId == SpecializedOutputId && SpecializedBonus > 1.0f) {
                probability *= SpecializedBonus;
                amount *= (SpecializedBonus + 1) / 2;// 数量小幅提升
            }

            // 检查该产物是否产出
            if (random.NextDouble() < probability) {
                // 处理小数部分
                int integerPart = (int)amount;
                float decimalPart = amount - integerPart;

                int finalAmount = integerPart;
                if (decimalPart > 0 && random.NextDouble() < decimalPart) {
                    finalAmount++;
                }

                // 应用翻倍效果
                if (DoubleOutput) {
                    finalAmount *= 2;
                }

                if (finalAmount > 0) {
                    result[itemId] = finalAmount;
                }
            }
        }

        // 如果配置了不消耗材料且没有产出，返回原物品
        if (NoMaterialConsumption && result.Count == 0) {
            result[InputItemId] = 1;
        }

        return result;
    }

    /// <summary>
    /// 应用突破加成
    /// </summary>
    /// <param name="bonusType">加成类型: 1=不消耗材料, 2=输出翻倍, 3=专精产物</param>
    /// <param name="specializedItemId">专精产物ID（仅在bonusType=3时有效）</param>
    public void ApplyBreakthroughBonus(int bonusType, int specializedItemId = 0) {
        switch (bonusType) {
            case 1:
                NoMaterialConsumption = true;
                break;
            case 2:
                DoubleOutput = true;
                break;
            case 3:
                SpecializedOutputId = specializedItemId;
                SpecializedBonus = 2.0f;// 专精产物成功率加倍
                break;
        }
    }

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public override void Export(BinaryWriter w) {
        // 先调用基类的方法保存基本属性
        base.Export(w);

        // 保存转化塔特有属性
        w.Write(InputItemId);
        w.Write(DestructionRate);
        w.Write(NoMaterialConsumption);
        w.Write(DoubleOutput);
        w.Write(SpecializedOutputId);
        w.Write(SpecializedBonus);

        // 保存转化产物表
        w.Write(OutputItems.Count);
        foreach (var kvp in OutputItems) {
            w.Write(kvp.Key);// 物品ID
            w.Write(kvp.Value.Item1);// 成功率
            w.Write(kvp.Value.Item2);// 数量
        }
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public override void Import(BinaryReader r) {
        // 先调用基类的方法读取基本属性
        base.Import(r);

        // 读取转化塔特有属性
        InputItemId = r.ReadInt32();
        DestructionRate = r.ReadSingle();
        NoMaterialConsumption = r.ReadBoolean();
        DoubleOutput = r.ReadBoolean();
        SpecializedOutputId = r.ReadInt32();
        SpecializedBonus = r.ReadSingle();

        // 读取转化产物表
        int outputCount = r.ReadInt32();
        OutputItems.Clear();
        for (int i = 0; i < outputCount; i++) {
            int itemId = r.ReadInt32();
            float probability = r.ReadSingle();
            float amount = r.ReadSingle();
            OutputItems[itemId] = new Tuple<float, float>(probability, amount);
        }
    }
}
