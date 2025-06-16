using System.Collections.Generic;
using static FE.Utils.ProtoID;
using static FE.Logic.Manager.RecipeManager;

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
        Create(I石矿, 0.04f, [new OutputInfo(0.01f, I硅石, 1), new OutputInfo(0.01f, I钛石, 1)]);
        Create(I煤矿, 0.04f, [new OutputInfo(0.01f, I金刚石, 1)]);
        Create(IGB铝矿, 0.04f);
        Create(IGB钨矿, 0.04f);
        Create(I硅石, 0.03f, [new OutputInfo(0.01f, I分形硅石, 1)]);
        Create(I钛石, 0.03f);
        Create(IGB硫矿, 0.05f);
        Create(IGB放射性矿物, 0.05f);

        Create(I水, 0.05f);
        Create(I原油, 0.05f);
        Create(I硫酸, 0.03f);
        Create(IGB海水, 0.05f);
        Create(IGB盐酸, 0.05f);
        Create(IGB硝酸, 0.05f);
        Create(IGB氨, 0.05f);

        Create(I氢, 0.03f, [new OutputInfo(0.01f, I重氢, 1)]);
        Create(I重氢, 0.02f, [new OutputInfo(0.01f, I氢, 1)]);
        Create(IGB氦, 0.02f, [new OutputInfo(0.01f, IGB氦三, 1)]);
        Create(IGB氦三, 0.02f, [new OutputInfo(0.01f, IGB氦, 1)]);
        Create(IGB氮, 0.03f);
        Create(IGB氧, 0.03f);
        Create(IGB二氧化碳, 0.03f, [new OutputInfo(0.01f, IGB二氧化碳, 1)]);
        Create(IGB二氧化硫, 0.03f, [new OutputInfo(0.01f, IGB二氧化硫, 1)]);

        Create(I可燃冰, 0.03f);
        Create(I金伯利矿石, 0.02f);
        Create(I分形硅石, 0.02f);
        Create(I光栅石, 0.02f);
        Create(I刺笋结晶, 0.02f);
        Create(I有机晶体, 0.02f);
        Create(I单极磁石, 0.01f);

        Create(I临界光子, 0.01f);
        Create(I反物质, 0.01f);
    }

    private static void Create(int inputID, float baseSuccessRate) {
        Create(inputID, baseSuccessRate, []);
    }

    /// <summary>
    /// 创建一个矿物复制配方，然后将其添加到配方列表中
    /// </summary>
    private static void Create(int inputID, float baseSuccessRate, List<OutputInfo> outputAppend) {
        if (!LDB.items.Exist(inputID)) {
            return;
        }
        AddRecipe(new MineralCopyRecipe(inputID, baseSuccessRate,
            [
                new OutputInfo(1.000f, inputID, 2),
            ],
            [
                ..outputAppend,
                new OutputInfo(0.012f, IFE分馏原胚普通, 1),
                new OutputInfo(0.010f, IFE分馏原胚精良, 1),
                new OutputInfo(0.008f, IFE分馏原胚稀有, 1),
                new OutputInfo(0.006f, IFE分馏原胚史诗, 1),
                new OutputInfo(0.004f, IFE分馏原胚传说, 1),
                new OutputInfo(0.002f, IFE分馏原胚定向, 1),
                new OutputInfo(0.050f, IFE复制精华, 1),
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
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public MineralCopyRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
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

    // /// <summary>
    // /// 将配方数据保存到二进制流中
    // /// </summary>
    // /// <param name="w">二进制写入器</param>
    // public override void Export(BinaryWriter w) {
    //     // 先调用基类的方法保存基本属性
    //     base.Export(w);
    //
    //     // 保存矿物复制塔特有属性
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
    //     // 读取矿物复制塔特有属性
    //     NoMaterialConsumption = r.ReadBoolean();
    //     DoubleOutput = r.ReadBoolean();
    // }
}
