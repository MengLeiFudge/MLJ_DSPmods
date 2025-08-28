using System.Collections.Generic;
using System.IO;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 矿物复制塔配方类（1A -> 1.1A）
/// </summary>
public class MineralCopyRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有矿物复制配方
    /// </summary>
    public static void CreateAll() {
        Create(I铁矿, 0.04f);
        Create(I铜矿, 0.04f);
        Create(I硅石, 0.04f, [new OutputInfo(0.01f, I分形硅石, 1)]);
        Create(I钛石, 0.04f);
        Create(I石矿, 0.04f, [new OutputInfo(0.01f, I硅石, 1), new OutputInfo(0.01f, I钛石, 1)]);
        Create(I煤矿, 0.04f, [new OutputInfo(0.01f, I金刚石, 1)]);
        Create(IGB钨矿, 0.04f);
        Create(IGB铝矿, 0.04f);
        Create(IGB硫矿, 0.04f, [new OutputInfo(0.01f, I硫酸, 1), new OutputInfo(0.01f, IGB二氧化硫, 1)]);
        Create(IGB放射性矿物, 0.04f, [new OutputInfo(0.01f, IGB铀矿, 1), new OutputInfo(0.01f, IGB钚矿, 1)]);
        Create(IGB海水, 0.04f, [new OutputInfo(0.01f, IGB氯化钠, 1)]);
        Create(I水, 0.04f);
        Create(I原油, 0.04f);
        Create(I硫酸, 0.04f);
        Create(IGB盐酸, 0.04f);
        Create(IGB硝酸, 0.04f);
        Create(IGB氨, 0.04f, [new OutputInfo(0.01f, IGB氮, 1), new OutputInfo(0.01f, I氢, 1)]);
        Create(I氢, 0.04f, [new OutputInfo(0.01f, I重氢, 1)]);
        Create(I重氢, 0.04f, [new OutputInfo(0.01f, I氢, 1)]);
        Create(IGB氮, 0.04f, [new OutputInfo(0.01f, IGB氨, 1)]);
        Create(IGB氧, 0.04f, [new OutputInfo(0.01f, IGB二氧化碳, 1)]);
        Create(IGB氦, 0.04f, [new OutputInfo(0.01f, IGB氦三, 1)]);
        Create(IGB氦三, 0.04f, [new OutputInfo(0.01f, IGB氦, 1)]);
        Create(IGB二氧化碳, 0.04f, [new OutputInfo(0.01f, IGB氧, 1), new OutputInfo(0.01f, I高能石墨, 1)]);
        Create(IGB二氧化硫, 0.04f, [new OutputInfo(0.01f, IGB氧, 1), new OutputInfo(0.01f, IGB硫粉, 1)]);
        Create(I临界光子, 0.04f);

        Create(I可燃冰, 0.02f);
        Create(I金伯利矿石, 0.02f);
        Create(I分形硅石, 0.02f);
        Create(I光栅石, 0.02f);
        Create(I刺笋结晶, 0.02f);
        Create(I单极磁石, 0.02f);
        Create(I有机晶体, 0.02f);
        Create(I黑雾矩阵, 0.02f);
        Create(I硅基神经元, 0.02f);
        Create(I物质重组器, 0.02f);
        Create(I负熵奇点, 0.02f);
        Create(I核心素, 0.02f);
        Create(I能量碎片, 0.02f);
        Create(I反物质, 0.02f);
    }

    private static void Create(int inputID, float baseSuccessRate) {
        Create(inputID, baseSuccessRate, []);
    }

    private static void Create(int inputID, float baseSuccessRate, List<OutputInfo> outputAppend) {
        if (itemValue[inputID] >= maxValue) {
            return;
        }
        outputAppend.RemoveAll(info => itemValue[info.OutputID] >= maxValue);
        AddRecipe(new MineralCopyRecipe(inputID, baseSuccessRate,
            [
                new OutputInfo(1.000f, inputID, 2),
            ],
            [
                ..outputAppend,
                new OutputInfo(0.01f, IFE复制精华, 1),
            ]));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.MineralCopy;

    /// <summary>
    /// 创建矿物复制塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public MineralCopyRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
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
