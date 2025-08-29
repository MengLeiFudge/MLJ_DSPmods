using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        List<int> matrixList = LDB.items.dataArray
            .Where(item => item.Type == EItemType.Matrix || item.ID == I黑雾矩阵).Select(item => item.ID).ToList();
        foreach (var item in LDB.items.dataArray) {
            if (itemValue[item.ID] >= maxValue
                || item.ID == IFE分馏配方通用核心
                || item.ID == IFE分馏塔增幅芯片
                || !item.GridIndexValid()
                || item.ID == I沙土) {
                continue;
            }
            //点金塔不能处理矩阵（包括配方原材料含有矩阵的物品），也不能处理建筑
            if (matrixList.Contains(item.ID)
                || (item.maincraft != null && item.maincraft.Items.Any(itemID => matrixList.Contains(itemID)))
                || item.BuildMode != 0) {
                continue;
            }
            int matrixID = itemToMatrix[item.ID];
            AddRecipe(new AlchemyRecipe(item.ID, 0.05f,
                [
                    new OutputInfo(1.0f, matrixID, itemValue[item.ID] / itemValue[matrixID]),
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
    /// 主产物数目增幅
    /// </summary>
    public override float MainOutputCountInc => (Progress - 0.56f) / 0.88f;

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
