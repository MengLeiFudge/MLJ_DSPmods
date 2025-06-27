using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;

namespace FE.Logic.Recipe;

/// <summary>
/// 量子复制塔配方类（可复制所有物品）
/// </summary>
public class QuantumCopyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有量子复制配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            AddRecipe(new QuantumCopyRecipe(item.ID, itemRatio[item.ID],
                [
                    new OutputInfo(1.000f, item.ID, 2),
                ],
                []));
        }
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
