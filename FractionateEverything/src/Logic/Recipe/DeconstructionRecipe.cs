using System;
using System.Collections.Generic;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.ProtoID;

namespace FE.Logic.Recipe;

/// <summary>
/// 分解塔配方类（1A -> X原材料1 + Y原材料2 + Z原材料3 + I精华1 + J精华2 + K精华3）
/// </summary>
public class DeconstructionRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有矿物复制配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            List<OutputInfo> outputMain = [];
            if (item.maincraft != null) {
                int len = item.maincraft.Items.Length;
                float rate = 1.0f / len;
                for (int i = 0; i < len; i++) {
                    outputMain.Add(new(rate, item.maincraft.Items[i], item.maincraft.ItemCounts[i] * len));
                }
            }
            Create(item.ID, 0.25f, outputMain);
        }
    }

    /// <summary>
    /// 创建一个矿物复制配方，然后将其添加到配方列表中
    /// </summary>
    private static void Create(int inputID, float baseSuccessRate, List<OutputInfo> outputMain) {
        AddRecipe(new DeconstructionRecipe(inputID, baseSuccessRate,
            outputMain,
            [
                new OutputInfo(0.012f, IFE分馏原胚普通, 1),
                new OutputInfo(0.010f, IFE分馏原胚精良, 1),
                new OutputInfo(0.008f, IFE分馏原胚稀有, 1),
                new OutputInfo(0.006f, IFE分馏原胚史诗, 1),
                new OutputInfo(0.004f, IFE分馏原胚传说, 1),
                new OutputInfo(0.002f, IFE分馏原胚定向, 1),
                new OutputInfo(0.050f, IFE分解精华, 1),
            ]));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Deconstruction;

    /// <summary>
    /// 创建分解塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public DeconstructionRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRate, outputMain, outputAppend) { }

    /// <summary>
    /// 是否输出翻倍（突破特殊属性）
    /// </summary>
    public bool DoubleOutput { get; set; }

    /// <summary>
    /// 是否精华加成（突破特殊属性）
    /// </summary>
    public bool EssenceBoost { get; set; }

    /// <summary>
    /// 是否完全分解（突破特殊属性，可以分解出所有原材料）
    /// </summary>
    public bool CompleteDeconstruction { get; set; }

    /// <summary>
    /// 处理分解逻辑
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <param name="successRate">成功率</param>
    /// <returns>分解结果，键为物品ID，值为数量</returns>
    public Dictionary<int, int> Process(Random random, float successRate) {
        Dictionary<int, int> result = new Dictionary<int, int>();

        // // 检查是否成功分解
        // if (random.NextDouble() > successRate) {
        //     // 分解失败，返回原物品
        //     result[InputItemId] = 1;
        //     return result;
        // }
        //
        // // 处理原材料输出
        // foreach (var material in MaterialOutputs) {
        //     int materialId = material.Key;
        //     float count = material.Value;
        //
        //     // 应用完全分解加成
        //     if (CompleteDeconstruction) {
        //         count *= 1.5f;
        //     }
        //
        //     // 应用翻倍效果
        //     if (DoubleOutput) {
        //         count *= 2;
        //     }
        //
        //     // 处理小数部分
        //     int integerPart = (int)count;
        //     float decimalPart = count - integerPart;
        //
        //     int finalCount = integerPart;
        //     if (decimalPart > 0 && random.NextDouble() < decimalPart) {
        //         finalCount++;
        //     }
        //
        //     if (finalCount > 0) {
        //         result[materialId] = finalCount;
        //     }
        // }
        //
        // // 处理精华输出
        // foreach (var essence in EssenceOutputs) {
        //     int essenceId = essence.Key;
        //     float probability = essence.Value;
        //
        //     // 应用精华加成
        //     if (EssenceBoost) {
        //         probability *= 2;
        //     }
        //
        //     // 检查该精华是否产出
        //     if (random.NextDouble() < probability) {
        //         int essenceCount = 1;
        //
        //         // 应用翻倍效果（精华翻倍概率较低）
        //         if (DoubleOutput && random.NextDouble() < 0.3f) {
        //             essenceCount = 2;
        //         }
        //
        //         result[essenceId] = essenceCount;
        //     }
        // }

        return result;
    }

    /// <summary>
    /// 应用突破加成
    /// </summary>
    /// <param name="bonusType">加成类型: 1=输出翻倍, 2=精华加成, 3=完全分解</param>
    public void ApplyBreakthroughBonus(int bonusType) {
        switch (bonusType) {
            case 1:
                DoubleOutput = true;
                break;
            case 2:
                EssenceBoost = true;
                break;
            case 3:
                CompleteDeconstruction = true;
                break;
        }
    }

    /// <summary>
    /// 根据合成配方自动设置分解配方的输出
    /// </summary>
    /// <param name="ingredients">合成所需原材料列表，键为物品ID，值为数量</param>
    /// <param name="essenceIds">可能产出的精华ID列表</param>
    public void SetupFromRecipe(Dictionary<int, int> ingredients, List<int> essenceIds) {
        // // 清空现有输出
        // MaterialOutputs.Clear();
        // EssenceOutputs.Clear();
        //
        // // 设置原材料输出（数量有一定随机性）
        // float recoveryRate = 0.6f + (Level * 0.05f) + (Quality * 0.05f);// 基础回收率
        //
        // foreach (var ingredient in ingredients) {
        //     int materialId = ingredient.Key;
        //     float count = ingredient.Value * recoveryRate;
        //
        //     MaterialOutputs[materialId] = count;
        // }
        //
        // // 设置精华输出
        // if (essenceIds != null && essenceIds.Count > 0) {
        //     // 物品越复杂，产出精华的几率越高
        //     float baseEssenceChance = 0.05f + (ingredients.Count * 0.01f);
        //
        //     // 根据配方等级和星级提高精华产出概率
        //     baseEssenceChance += (Level * 0.01f) + (Quality * 0.02f);
        //
        //     // 每种精华的产出概率
        //     foreach (int essenceId in essenceIds) {
        //         // 不同精华有不同概率
        //         float probability = baseEssenceChance / Math.Max(1, essenceIds.Count);
        //
        //         // 更高级的精华概率更低
        //         int essenceLevel = essenceId % 10;// 假设精华ID的个位数表示等级
        //         if (essenceLevel > 1) {
        //             probability /= essenceLevel;
        //         }
        //
        //         EssenceOutputs[essenceId] = probability;
        //     }
        // }
    }

    // /// <summary>
    // /// 将配方数据保存到二进制流中
    // /// </summary>
    // /// <param name="w">二进制写入器</param>
    // public override void Export(BinaryWriter w) {
    //     // 先调用基类的方法保存基本属性
    //     base.Export(w);
    //
    //     // 保存分解塔特有属性
    //     w.Write(DoubleOutput);
    //     w.Write(EssenceBoost);
    //     w.Write(CompleteDeconstruction);
    //
    //     // // 保存原材料输出
    //     // w.Write(MaterialOutputs.Count);
    //     // foreach (var kvp in MaterialOutputs) {
    //     //     w.Write(kvp.Key);// 物品ID
    //     //     w.Write(kvp.Value);// 数量
    //     // }
    //     //
    //     // // 保存精华输出
    //     // w.Write(EssenceOutputs.Count);
    //     // foreach (var kvp in EssenceOutputs) {
    //     //     w.Write(kvp.Key);// 精华ID
    //     //     w.Write(kvp.Value);// 概率
    //     // }
    // }
    //
    // /// <summary>
    // /// 从二进制流中加载配方数据
    // /// </summary>
    // /// <param name="r">二进制读取器</param>
    // public override void Import(BinaryReader r) {
    //     // 先调用基类的方法读取基本属性
    //     base.Import(r);
    //
    //     // 读取分解塔特有属性
    //     DoubleOutput = r.ReadBoolean();
    //     EssenceBoost = r.ReadBoolean();
    //     CompleteDeconstruction = r.ReadBoolean();
    //
    //     // // 读取原材料输出
    //     // int materialCount = r.ReadInt32();
    //     // MaterialOutputs.Clear();
    //     // for (int i = 0; i < materialCount; i++) {
    //     //     int materialId = r.ReadInt32();
    //     //     float count = r.ReadSingle();
    //     //     MaterialOutputs[materialId] = count;
    //     // }
    //     //
    //     // // 读取精华输出
    //     // int essenceCount = r.ReadInt32();
    //     // EssenceOutputs.Clear();
    //     // for (int i = 0; i < essenceCount; i++) {
    //     //     int essenceId = r.ReadInt32();
    //     //     float probability = r.ReadSingle();
    //     //     EssenceOutputs[essenceId] = probability;
    //     // }
    // }
}
