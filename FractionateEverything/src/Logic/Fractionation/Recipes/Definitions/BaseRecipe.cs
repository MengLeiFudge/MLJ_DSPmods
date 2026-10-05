using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 分馏配方基类
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
    /// 获取该配方累计成功结算次数。
    /// </summary>
    public long TotalSuccessCount => Interlocked.Read(ref totalSuccessCount);

    private long totalSuccessCount;

    /// <summary>
    /// 累计该配方的成功结算次数。
    /// </summary>
    public void RecordSuccesses(int count) {
        if (count <= 0) {
            return;
        }

        long current;
        long updated;
        do {
            current = Interlocked.Read(ref totalSuccessCount);
            updated = current > long.MaxValue - count ? long.MaxValue : current + count;
        } while (Interlocked.CompareExchange(ref totalSuccessCount, updated, current) != current);
    }

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

    #region 模组存档接口实现

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public virtual void Import(BinaryReader r) {
        r.ReadBlocks(
            ("TotalSuccessCount", br => Interlocked.Exchange(ref totalSuccessCount, Math.Max(0, br.ReadInt64()))),
            ("OutputMain", br => br.ReadList(reader => {
                int id = reader.ReadInt32();
                int total = reader.ReadInt32();
                var info = OutputMain.Find(x => x.OutputID == id);
                if (info != null) info.OutputTotalCount = total;
                else LogWarning($"Output {id} not found in {TypeName} main outputs");
            })),
            ("OutputAppend", br => br.ReadList(reader => {
                int id = reader.ReadInt32();
                int total = reader.ReadInt32();
                var info = OutputAppend.Find(x => x.OutputID == id);
                if (info != null) info.OutputTotalCount = total;
                else LogWarning($"Output {id} not found in {TypeName} append outputs");
            }))
        );
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public virtual void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalSuccessCount", bw => bw.Write(TotalSuccessCount)),
            ("OutputMain", bw => bw.WriteList(OutputMain, (writer, info) => {
                writer.Write(info.OutputID);
                writer.Write(info.OutputTotalCount);
            })),
            ("OutputAppend", bw => bw.WriteList(OutputAppend, (writer, info) => {
                writer.Write(info.OutputID);
                writer.Write(info.OutputTotalCount);
            }))
        );
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public virtual void IntoOtherSave() {
        Interlocked.Exchange(ref totalSuccessCount, 0);
        foreach (OutputInfo info in OutputMain) {
            info.OutputTotalCount = 0;
        }
        foreach (OutputInfo info in OutputAppend) {
            info.OutputTotalCount = 0;
        }
    }

    #endregion
}
