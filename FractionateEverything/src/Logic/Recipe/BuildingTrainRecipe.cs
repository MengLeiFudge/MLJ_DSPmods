using System.Collections.Generic;
using System.IO;
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
        List<OutputInfo> OutputMain = [
            new(0.2f, IFE交互塔, 1),
            new(0.2f, IFE矿物复制塔, 1),
            new(0.1f, IFE点数聚集塔, 1),
            new(0.05f, IFE量子复制塔, 1),
            new(0.15f, IFE点金塔, 1),
            new(0.15f, IFE分解塔, 1),
            new(0.15f, IFE转化塔, 1),
        ];
        AddRecipe(new BuildingTrainRecipe(IFE分馏塔原胚普通, 0.01f, OutputMain, []));
        AddRecipe(new BuildingTrainRecipe(IFE分馏塔原胚精良, 0.02f, OutputMain, []));
        AddRecipe(new BuildingTrainRecipe(IFE分馏塔原胚稀有, 0.03f, OutputMain, []));
        AddRecipe(new BuildingTrainRecipe(IFE分馏塔原胚史诗, 0.04f, OutputMain, []));
        AddRecipe(new BuildingTrainRecipe(IFE分馏塔原胚传说, 0.06f, OutputMain, []));
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
    /// 是否不消耗材料（突破特殊属性）
    /// </summary>
    public bool NoMaterialConsumption { get; set; }

    /// <summary>
    /// 是否输出翻倍（突破特殊属性）
    /// </summary>
    public bool DoubleOutput { get; set; }

    /// <summary>
    /// 专精产物ID（突破特殊属性）
    /// </summary>
    public int SpecializedOutputId { get; set; }

    /// <summary>
    /// 专精产物加成系数（突破特殊属性）
    /// </summary>
    public float SpecializedBonus { get; set; } = 1.0f;

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
