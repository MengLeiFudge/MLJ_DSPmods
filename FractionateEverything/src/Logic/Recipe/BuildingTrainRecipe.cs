using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 建筑培养配方（1分馏塔原胚 -> 1随机分馏塔）
/// </summary>
public class BuildingTrainRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有建筑培养配方
    /// </summary>
    public static void CreateAll() {
        Create(IFE分馏塔原胚普通, 0.01f);
        Create(IFE分馏塔原胚精良, 0.02f);
        Create(IFE分馏塔原胚稀有, 0.03f);
        Create(IFE分馏塔原胚史诗, 0.04f);
        Create(IFE分馏塔原胚传说, 0.06f);
    }

    /// <summary>
    /// 添加一个建筑培养配方
    /// </summary>
    private static void Create(int inputID, float maxSuccessRate) {
        float[] ratioArr = inputID switch {
            IFE分馏塔原胚普通 => [30f, 30f / 3, 0f, 0f],
            IFE分馏塔原胚精良 => [25f, 25f / 3, 1f, 0f],
            IFE分馏塔原胚稀有 => [20f, 20f / 3, 2f, 0.5f],
            IFE分馏塔原胚史诗 => [15f, 15f / 3, 3f, 1f],
            IFE分馏塔原胚传说 => [5f, 5f / 3, 5f, 2f],
            _ => null
        };
        if (ratioArr == null) {
            return;
        }
        float sum = ratioArr.Sum();
        List<OutputInfo> OutputMain = [
            new(ratioArr[1] / sum, IFE交互塔, 1),
            new(ratioArr[0] / sum, IFE矿物复制塔, 1),
            new(ratioArr[2] / sum, IFE点数聚集塔, 1),
            new(ratioArr[3] / sum, IFE量子复制塔, 1),
            new(ratioArr[1] / sum, IFE点金塔, 1),
            new(ratioArr[1] / sum, IFE分解塔, 1),
            new(ratioArr[1] / sum, IFE转化塔, 1),
        ];
        AddRecipe(new BuildingTrainRecipe(inputID, maxSuccessRate, OutputMain, []));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.BuildingTrain;

    /// <summary>
    /// 创建建筑培养配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public BuildingTrainRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) { }

    /// <summary>
    /// 主产物数目增幅
    /// </summary>
    public override float MainOutputCountInc => 1.0f + (IsMaxQuality ? 0.1f * Level : 0);

    /// <summary>
    /// 附加产物概率增幅
    /// </summary>
    public override float AppendOutputRatioInc => 1.0f;

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
