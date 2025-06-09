using System;
using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.ProtoID;

namespace FE.Logic.Recipe;

/// <summary>
/// 转化塔配方类（1A -> XA + YB + ZC）
/// </summary>
public class ConversionRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有矿物复制配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            List<OutputInfo> outputMain = [
                new OutputInfo(0.99f, I宇宙矩阵, 1),
                new OutputInfo(0.01f, IFE分馏原胚定向, 10),
            ];
            Create(item.ID, itemRatioDic[item.ID], outputMain);
        }
    }

    /// <summary>
    /// 创建一个矿物复制配方，然后将其添加到配方列表中
    /// </summary>
    private static void Create(int inputID, float baseSuccessRate, List<OutputInfo> outputMain) {
        AddRecipe(new ConversionRecipe(inputID, baseSuccessRate,
            outputMain,
            [
                new OutputInfo(0.012f, IFE分馏原胚普通, 1),
                new OutputInfo(0.010f, IFE分馏原胚精良, 1),
                new OutputInfo(0.008f, IFE分馏原胚稀有, 1),
                new OutputInfo(0.006f, IFE分馏原胚史诗, 1),
                new OutputInfo(0.004f, IFE分馏原胚传说, 1),
                new OutputInfo(0.002f, IFE分馏原胚定向, 1),
                new OutputInfo(0.050f, IFE转化精华, 1),
            ]));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Conversion;

    /// <summary>
    /// 创建转化塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public ConversionRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRate, outputMain, outputAppend) { }

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
    /// 处理转化逻辑
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <param name="successRate">成功率</param>
    /// <returns>转化结果，键为物品ID，值为数量</returns>
    public Dictionary<int, int> Process(Random random, float successRate) {
        Dictionary<int, int> result = new Dictionary<int, int>();

        // // 检查是否成功转化
        // if (random.NextDouble() > successRate) {
        //     // 检查是否损毁
        //     if (random.NextDouble() < DestructionRate) {
        //         // 材料损毁，返回空
        //         return result;
        //     }
        //
        //     // 未成功也未损毁，原物品流出
        //     if (!NoMaterialConsumption) {
        //         result[InputItemId] = 1;
        //     }
        //
        //     return result;
        // }
        //
        // // 处理所有可能的产物
        // foreach (var output in OutputItems) {
        //     int itemId = output.Key;
        //     float probability = output.Value.Item1;
        //     float amount = output.Value.Item2;
        //
        //     // 应用专精加成
        //     if (itemId == SpecializedOutputId && SpecializedBonus > 1.0f) {
        //         probability *= SpecializedBonus;
        //         amount *= (SpecializedBonus + 1) / 2;// 数量小幅提升
        //     }
        //
        //     // 检查该产物是否产出
        //     if (random.NextDouble() < probability) {
        //         // 处理小数部分
        //         int integerPart = (int)amount;
        //         float decimalPart = amount - integerPart;
        //
        //         int finalAmount = integerPart;
        //         if (decimalPart > 0 && random.NextDouble() < decimalPart) {
        //             finalAmount++;
        //         }
        //
        //         // 应用翻倍效果
        //         if (DoubleOutput) {
        //             finalAmount *= 2;
        //         }
        //
        //         if (finalAmount > 0) {
        //             result[itemId] = finalAmount;
        //         }
        //     }
        // }
        //
        // // 如果配置了不消耗材料且没有产出，返回原物品
        // if (NoMaterialConsumption && result.Count == 0) {
        //     result[InputItemId] = 1;
        // }

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
        w.Write(NoMaterialConsumption);
        w.Write(DoubleOutput);
        w.Write(SpecializedOutputId);
        w.Write(SpecializedBonus);

        // // 保存转化产物表
        // w.Write(OutputItems.Count);
        // foreach (var kvp in OutputItems) {
        //     w.Write(kvp.Key);// 物品ID
        //     w.Write(kvp.Value.Item1);// 成功率
        //     w.Write(kvp.Value.Item2);// 数量
        // }
    }

    /// <summary>
    /// 从二进制流中加载配方数据
    /// </summary>
    /// <param name="r">二进制读取器</param>
    public override void Import(BinaryReader r) {
        // 先调用基类的方法读取基本属性
        base.Import(r);

        // 读取转化塔特有属性
        NoMaterialConsumption = r.ReadBoolean();
        DoubleOutput = r.ReadBoolean();
        SpecializedOutputId = r.ReadInt32();
        SpecializedBonus = r.ReadSingle();

        // // 读取转化产物表
        // int outputCount = r.ReadInt32();
        // OutputItems.Clear();
        // for (int i = 0; i < outputCount; i++) {
        //     int itemId = r.ReadInt32();
        //     float probability = r.ReadSingle();
        //     float amount = r.ReadSingle();
        //     OutputItems[itemId] = new Tuple<float, float>(probability, amount);
        // }
    }
}
