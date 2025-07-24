using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Manager;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分解塔配方类（1A -> X原材料1 + Y原材料2 + Z原材料3 + I精华1 + J精华2 + K精华3）
/// </summary>
public class DeconstructionRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有分解配方
    /// </summary>
    public static void CreateAll() {
        foreach (var item in LDB.items.dataArray) {
            if (itemValue[item.ID] >= maxValue
                || item.ID == IFE分馏配方核心
                || item.ID == IFE建筑增幅芯片
                || item.ID == IFE残破核心) {
                continue;
            }
            List<OutputInfo> outputMain = [];
            if (item.maincraft != null) {
                int len = item.maincraft.Items.Length;
                float rate = 1.0f / len;
                for (int i = 0; i < len; i++) {
                    outputMain.Add(new(rate, item.maincraft.Items[i], item.maincraft.ItemCounts[i] * len));
                }
            } else {
                outputMain.Add(new(1.0f, I沙土, (int)Math.Ceiling(itemValue[item.ID] / itemValue[I沙土] * 0.8)));
            }
            AddRecipe(new DeconstructionRecipe(item.ID, 1.0f / (1.0f + (float)ProcessManager.MaxTableMilli(10)),
                outputMain,
                [
                    new OutputInfo(0.01f, IFE分解精华, 1),
                ]));
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Deconstruction;

    /// <summary>
    /// 创建分解塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public DeconstructionRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) { }

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
