using System.Collections.Generic;
using System.IO;
using FE.Logic.Manager;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

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
            //点金塔不能处理矩阵
            if ((item.ID >= I电磁矩阵 && item.ID <= I宇宙矩阵)
                || (item.ID >= IGB玻色矩阵 && item.ID <= IGB奇点矩阵)) {
                continue;
            }
            int matrixID = ItemToMatrix[item.ID];
            float matrixValue = itemValue[matrixID];
            AddRecipe(new AlchemyRecipe(item.ID, itemValue[item.ID] / matrixValue,
                [
                    new OutputInfo(1.0f, matrixID, 1),
                ],
                [
                    new OutputInfo(0.01f, IFE点金精华, 1),
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
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public AlchemyRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) { }

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

    public override void Import(BinaryReader r) {
        base.Import(r);
        int version = r.ReadInt32();
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.Write(1);
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
