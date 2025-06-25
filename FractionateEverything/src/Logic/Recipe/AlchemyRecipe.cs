using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.ProtoID;

namespace FE.Logic.Recipe;

/// <summary>
/// 点金塔配方类（1A -> X矩阵1 + Y矩阵2 + Z矩阵3）
/// </summary>
public class AlchemyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有点金配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            AddRecipe(new AlchemyRecipe(item.ID, itemValueDic[item.ID] / 250000f,
                [
                    new OutputInfo(0.507937f, I电磁矩阵, 1),
                    new OutputInfo(0.253968f, I能量矩阵, 1),
                    new OutputInfo(0.126984f, I结构矩阵, 1),
                    new OutputInfo(0.063492f, I信息矩阵, 1),
                    new OutputInfo(0.031746f, I引力矩阵, 1),
                    new OutputInfo(0.015873f, I宇宙矩阵, 1),
                ],
                [
                    new OutputInfo(0.010f, IFE分馏原胚普通, 1),
                    new OutputInfo(0.007f, IFE分馏原胚精良, 1),
                    new OutputInfo(0.004f, IFE分馏原胚稀有, 1),
                    new OutputInfo(0.002f, IFE分馏原胚史诗, 1),
                    new OutputInfo(0.001f, IFE分馏原胚传说, 1),
                    new OutputInfo(0.020f, IFE点金精华, 1),
                ]));
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Alchemy;

    /// <summary>
    /// 创建点金塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public AlchemyRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
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

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public virtual void Export(BinaryWriter w) {
        w.Write(1);
    }

    #endregion
}
