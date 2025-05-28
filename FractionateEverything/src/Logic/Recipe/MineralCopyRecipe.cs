using System;
using System.Collections.Generic;
using System.IO;

namespace FE.Logic.Recipe;

/// <summary>
/// 矿物复制塔配方类（1A -> 1.1A）
/// </summary>
public class MineralCopyRecipe : BaseRecipe {
    /// <summary>
    /// 存储所有矿物复制配方的静态列表
    /// </summary>
    private static readonly List<MineralCopyRecipe> mineralCopyRecipeList = [];

    /// <summary>
    /// 根据矿物ID获取矿物复制配方
    /// </summary>
    /// <param name="mineralID">矿物ID</param>
    /// <returns>对应的矿物复制配方，如果未找到则返回null</returns>
    public static MineralCopyRecipe GetRecipe(int mineralID) {
        foreach (MineralCopyRecipe r in mineralCopyRecipeList) {
            if (r.MineralId == mineralID) {
                return r;
            }
        }
        return null;
    }

    /// <summary>
    /// 添加配方到静态列表
    /// </summary>
    /// <param name="recipe">要添加的配方</param>
    public static void AddRecipe(MineralCopyRecipe recipe) {
        mineralCopyRecipeList.Add(recipe);
    }

    /// <summary>
    /// 获取所有矿物复制配方列表
    /// </summary>
    /// <returns>矿物复制配方列表</returns>
    public static List<MineralCopyRecipe> GetAllRecipes() {
        return mineralCopyRecipeList;
    }

    /// <summary>
    /// 清空配方列表
    /// </summary>
    public static void ClearRecipes() {
        mineralCopyRecipeList.Clear();
    }

    /// <summary>
    /// 创建矿物复制塔配方实例
    /// </summary>
    /// <param name="id">配方ID</param>
    /// <param name="name">配方名称</param>
    /// <param name="description">配方描述</param>
    /// <param name="mineralId">矿物ID</param>
    /// <param name="rarityFactor">稀有度系数</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputRatio">产出比例</param>
    public MineralCopyRecipe(int mineralId, float rarityFactor = 1.0f,
        float baseSuccessRate = 0.1f, float outputRatio = 1.1f, int level = 1, int star = 0) {
        MineralId = mineralId;
        RarityFactor = rarityFactor;
        BaseSuccessRate = baseSuccessRate;
        OutputRatio = outputRatio;
        Level = level;
        Star = star;
        IsUnlocked = false;
    }

    /// <summary>
    /// 输入矿物ID
    /// </summary>
    public int MineralId { get; set; }

    /// <summary>
    /// 基础产出比例（例如1.1表示平均每个输入产出1.1个）
    /// </summary>
    public float OutputRatio { get; set; } = 1.1f;

    /// <summary>
    /// 原胚产出概率（分馏原胚）
    /// </summary>
    public float EmbryoChance { get; set; } = 0.01f;

    /// <summary>
    /// 破损原胚产出概率（分馏原胚（破损））
    /// </summary>
    public float BrokenEmbryoChance { get; set; } = 0.02f;

    /// <summary>
    /// 稀有度系数（影响成功率）
    /// </summary>
    public float RarityFactor { get; set; } = 1.0f;

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
    public override ERecipe RecipeType => ERecipe.MineralCopy;

    /// <summary>
    /// 处理矿物复制逻辑
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <param name="successRate">成功率</param>
    /// <param name="embryoId">分馏原胚ID</param>
    /// <param name="brokenEmbryoId">分馏原胚（破损）ID</param>
    /// <returns>复制结果，键为物品ID，值为数量</returns>
    public Dictionary<int, int> Process(Random random, float successRate, int embryoId, int brokenEmbryoId) {
        Dictionary<int, int> result = new Dictionary<int, int>();

        // 调整成功率（考虑稀有度系数）
        float adjustedSuccessRate = successRate / RarityFactor;

        // 检查是否成功复制矿物
        bool success = random.NextDouble() < adjustedSuccessRate;

        // 处理原材料消耗
        if (!NoMaterialConsumption) {
            result[MineralId] = 1;// 基础输出
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
                if (result.ContainsKey(MineralId)) {
                    result[MineralId] += extraCount;
                } else {
                    result[MineralId] = extraCount;
                }
            }

            // 检查是否产出分馏原胚
            if (random.NextDouble() < EmbryoChance) {
                result[embryoId] = result.ContainsKey(embryoId) ? result[embryoId] + 1 : 1;
            }

            // 检查是否产出分馏原胚（破损）
            if (random.NextDouble() < BrokenEmbryoChance) {
                result[brokenEmbryoId] = result.ContainsKey(brokenEmbryoId) ? result[brokenEmbryoId] + 1 : 1;
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
    }

    /// <summary>
    /// 获取当前成功率（考虑稀有度和各种加成）
    /// </summary>
    public override float GetCurrentSuccessRate(float proliferatorBonus = 0) {
        // 基础成功率 / 稀有度系数 + 增产剂加成
        return Math.Min(BaseSuccessRate / RarityFactor + proliferatorBonus, 1.0f);
    }

    /// <summary>
    /// 将配方数据保存到二进制流中
    /// </summary>
    /// <param name="w">二进制写入器</param>
    public override void Export(BinaryWriter w) {
        // 先调用基类的方法保存基本属性
        base.Export(w);

        // 保存矿物复制塔特有属性
        w.Write(MineralId);
        w.Write(OutputRatio);
        w.Write(EmbryoChance);
        w.Write(BrokenEmbryoChance);
        w.Write(RarityFactor);
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

        // 读取矿物复制塔特有属性
        MineralId = r.ReadInt32();
        OutputRatio = r.ReadSingle();
        EmbryoChance = r.ReadSingle();
        BrokenEmbryoChance = r.ReadSingle();
        RarityFactor = r.ReadSingle();
        NoMaterialConsumption = r.ReadBoolean();
        DoubleOutput = r.ReadBoolean();
    }
}
