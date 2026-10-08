using System.Collections.Generic;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 分馏配方定义与批量结算接口；研究状态由文明研究域管理。
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float baseSuccessRatio,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    /// <summary>
    /// 获取配方类型和输入物品组成的显示名称。
    /// </summary>
    public string TypeName => $"{RecipeType.GetShortName()}-{LDB.items.Select(InputID).name}";
    #region 配方类型、输入输出

    /// <summary>
    /// 类型
    /// </summary>
    public abstract ERecipe RecipeType { get; }

    /// <summary>
    /// 输入物品的ID
    /// </summary>
    public int InputID => inputID;

    /// <summary>
    /// 配方成功率
    /// </summary>
    public float SuccessRatio => baseSuccessRatio;
    /// <summary>
    /// 配方损毁率，数值越大时，增产剂对分馏效果越有明显提升
    /// </summary>
    public virtual float DestroyRatio => 0.04f;

    /// <summary>
    /// 主产物信息，概率之和必须为100%。
    /// 当判定成功时，必定输出且仅输出其中一项。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputMain => outputMain;

    /// <summary>
    /// 副产物信息。
    /// 当判定成功时，该列表内每一项分别判定是否成功。
    /// 如果输出的物品数目为小数，则进行二次判定。
    /// </summary>
    public List<OutputInfo> OutputAppend => outputAppend;

    /// <summary>由具体配方实现批量结算，写回输入增产点并复用输出缓冲。</summary>
    /// <param name="seed">分馏随机种子。</param>
    /// <param name="pointsBonus">增产剂成功率加成。</param>
    /// <param name="successBoost">额外成功率加成。</param>
    /// <param name="batchCount">本批可处理的输入数量。</param>
    /// <param name="fluidInputIncAvg">单件输入平均增产点。</param>
    /// <param name="fluidInputInc">输入缓存的剩余增产点。</param>
    /// <param name="outputs">复用的产物缓冲。</param>
    /// <returns>本批实际消耗、产出与回流数量。</returns>
    public abstract FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs);

    #endregion
}
