using System;
using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 量子复制塔配方类（可复制除建筑外的所有物品）
/// </summary>
public class QuantumCopyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有量子复制配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            if (itemValue[item.ID] >= maxValue
                || item.ID == IFE分馏配方通用核心
                || item.ID == IFE分馏塔增幅芯片) {
                continue;
            }
            //量子复制塔不能处理建筑
            if (item.BuildMode != 0) {
                continue;
            }
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
    public override ERecipe RecipeType => ERecipe.QuantumCopy;

    /// <summary>
    /// 消耗精华数目
    /// </summary>
    public float EssenceCost { get; private set; }

    /// <summary>
    /// 创建量子复制塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public QuantumCopyRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) {
        EssenceCost = (float)(0.01 * Math.Pow(itemValue[InputID], Math.Log(2, 3)));
    }

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
