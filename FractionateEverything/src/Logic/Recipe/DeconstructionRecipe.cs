using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.ProtoID;

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
            List<OutputInfo> outputMain = [];
            if (item.maincraft != null) {
                int len = item.maincraft.Items.Length;
                float rate = 1.0f / len;
                for (int i = 0; i < len; i++) {
                    outputMain.Add(new(rate, item.maincraft.Items[i], item.maincraft.ItemCounts[i] * len));
                }
            }
            AddRecipe(new DeconstructionRecipe(item.ID, 0.25f,
                outputMain,
                [
                    new OutputInfo(0.010f, IFE分馏原胚普通, 1),
                    new OutputInfo(0.007f, IFE分馏原胚精良, 1),
                    new OutputInfo(0.004f, IFE分馏原胚稀有, 1),
                    new OutputInfo(0.002f, IFE分馏原胚史诗, 1),
                    new OutputInfo(0.001f, IFE分馏原胚传说, 1),
                    new OutputInfo(0.050f, IFE分解精华, 1),
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

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public virtual void Export(BinaryWriter w) {
        w.Write(1);
    }

    #endregion
}
