using System.Collections.Generic;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;

namespace FE.Logic.Recipe;

/// <summary>
/// 量子复制塔配方类（可复制所有物品）
/// </summary>
public class QuantumCopyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有矿物复制配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            Create(item.ID, itemRatioDic[item.ID]);
        }
    }

    /// <summary>
    /// 创建一个矿物复制配方，然后将其添加到配方列表中
    /// </summary>
    private static void Create(int inputID, float baseSuccessRate) {
        AddRecipe(new QuantumCopyRecipe(inputID, baseSuccessRate,
            [
                new OutputInfo(1.000f, inputID, 2),
            ],
            [
                // new OutputInfo(0.012f, IFE分馏原胚普通, 1),
                // new OutputInfo(0.010f, IFE分馏原胚精良, 1),
                // new OutputInfo(0.008f, IFE分馏原胚稀有, 1),
                // new OutputInfo(0.006f, IFE分馏原胚史诗, 1),
                // new OutputInfo(0.004f, IFE分馏原胚传说, 1),
                // new OutputInfo(0.002f, IFE分馏原胚定向, 1),
                // new OutputInfo(0.050f, IFE复制精华, 1),
            ]));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.QuantumDuplicate;

    /// <summary>
    /// 创建量子复制塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public QuantumCopyRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
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
    /// 应用突破加成
    /// </summary>
    /// <param name="bonusType">加成类型: 1=不消耗材料, 2=输出翻倍</param>
    public void ApplyQualityBonus(int bonusType) {
        // switch (bonusType) {
        //     case 1:
        //         NoMaterialConsumption = true;
        //         break;
        //     case 2:
        //         DoubleOutput = true;
        //         OutputRatio += 0.1f;// 突破时基础产出比例也会提升
        //         break;
        // }
        // // 突破时提升分馏精华产出概率
        // EssenceChance += 0.01f;
    }

    /// <summary>
    /// 获取当前成功率（考虑物品价值和增产点数）
    /// </summary>
    /// <param name="proliferatorBonus">增产剂加成</param>
    /// <param name="proliferatorPoints">增产点数</param>
    /// <returns>调整后的成功率</returns>
    public float GetCurrentSuccessRate(float proliferatorBonus, int proliferatorPoints) {
        // // 根据增产点数调整成功率
        // float pointsRatio = Math.Min(proliferatorPoints / 10.0f, 1.0f);// 10点时达到最大效果
        //
        // // 基础成功率 * 点数比例 / 价值系数 + 增产剂加成
        // return Math.Min(BaseSuccessRate * pointsRatio / ValueFactor + proliferatorBonus, 1.0f);
        return 0;
    }

    // /// <summary>
    // /// 将配方数据保存到二进制流中
    // /// </summary>
    // /// <param name="w">二进制写入器</param>
    // public override void Export(BinaryWriter w) {
    //     // 先调用基类的方法保存基本属性
    //     base.Export(w);
    //
    //     // 保存量子复制塔特有属性
    //     w.Write(NoMaterialConsumption);
    //     w.Write(DoubleOutput);
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
    //     // 读取量子复制塔特有属性
    //     NoMaterialConsumption = r.ReadBoolean();
    //     DoubleOutput = r.ReadBoolean();
    // }
}
