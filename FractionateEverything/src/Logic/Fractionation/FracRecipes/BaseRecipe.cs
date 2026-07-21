using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.Process;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

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
    /// <summary>
    /// 获取带矩阵阶段颜色的配方显示名称。
    /// </summary>
    public string TypeNameWC => TypeName.WithColor(MatrixID - I电磁矩阵);

    #region 配方类型、输入输出

    /// <summary>
    /// 类型
    /// </summary>
    public abstract ERecipe RecipeType { get; }

    /// <summary>
    /// 保存热路径为当前塔实例选择的通用主产物目标。
    /// </summary>
    public static int CurrentMainOutputTargetId;

    /// <summary>
    /// 判断该配方是否存在可选择的多个主产物。
    /// </summary>
    public virtual bool SupportsMainOutputLock(int itemId) =>
        OutputMain.Count > 1 && OutputMain.Exists(output => output.OutputID == itemId);

    /// <summary>
    /// 判断该配方是否需要文明协议完整度达到 100 后才能运行。
    /// </summary>
    public virtual bool RequiresProtocolRecovery => RecipeType is ERecipe.MineralCopy or ERecipe.Conversion;

    /// <summary>
    /// 判断该协议是否计入所属文明阶段的主线完成度。
    /// </summary>
    public virtual bool CountsTowardStageCompletion => true;

    /// <summary>
    /// 读取协议固定所属阶段；返回 -1 时按输入和产物自动计算。
    /// </summary>
    public virtual int ProtocolStageOrder => -1;

    /// <summary>
    /// 判断当前存档是否已经满足该协议进入检索池的业务条件。
    /// </summary>
    public virtual bool IsProtocolEligible => true;

    /// <summary>
    /// 获取该配方累计成功结算次数，用于实例控制能力的配方校准。
    /// </summary>
    public long TotalSuccessCount { get; private set; }

    /// <summary>
    /// 获取主路锁定校准基线；建筑培养配方采用低频设备下限，其余按矩阵阶段翻倍。
    /// </summary>
    public int MainOutputLockCalibrationThreshold {
        get {
            if (RecipeType == ERecipe.BuildingTrain) {
                return 20;
            }
            int stage = FE.Logic.Items.ItemManager.GetMatrixStageIndex(MatrixID);
            return 200 << Math.Max(0, stage);
        }
    }

    /// <summary>
    /// 获取副产物弃置校准基线。
    /// </summary>
    public int ByproductDiscardCalibrationThreshold => MainOutputLockCalibrationThreshold * 5;

    /// <summary>
    /// 判断当前配方是否已经完成主路锁定校准。
    /// </summary>
    public bool IsMainOutputLockCalibrated => TotalSuccessCount >= MainOutputLockCalibrationThreshold;

    /// <summary>
    /// 判断当前配方是否已经完成副产物弃置校准。
    /// </summary>
    public bool IsByproductDiscardCalibrated => TotalSuccessCount >= ByproductDiscardCalibrationThreshold;

    /// <summary>
    /// 累计该配方的成功结算次数。
    /// </summary>
    public void RecordSuccesses(int count) {
        if (count > 0) {
            TotalSuccessCount = TotalSuccessCount > long.MaxValue - count
                ? long.MaxValue
                : TotalSuccessCount + count;
        }
    }

    /// <summary>
    /// 输入物品的ID
    /// </summary>
    public int InputID => inputID;

    /// <summary>
    /// 配方层次对应的矩阵ID
    /// </summary>
    public int MatrixID = 0;

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

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、产出产物、无变化（直通）。
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="pointsBonus">增产剂加成（比例，例如0.25）</param>
    /// <param name="successBoost">配方成功率加成</param>
    /// <param name="fluidInputIncAvg">输入物品的平均增产等级</param>
    /// <param name="fluidInputInc">该分馏塔当前的全部增产点数，将在该方法中被修改</param>
    /// <param name="inputChange">原材料会变成几个（-1表示消耗，0表示保留）</param>
    /// <param name="outputs">损毁返回null，直通返回空List，成功返回输出产物</param>
    /// <summary>
    /// 执行单次完整分馏结算并写回主产物、副产物和输入保留结果。
    /// </summary>
    public virtual void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        // 1. 损毁判定
        if (GetRandDouble(ref seed) < DestroyRatio) {
            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            outputs = null;
            return;
        }

        // 2. 成功判定
        if (GetRandDouble(ref seed) < SuccessRatio * (1 + pointsBonus) * (1 + successBoost)) {
            List<ProductOutputInfo> list = [];
            OutputInfo mainOutput = SelectMainOutput(ref seed);
            if (mainOutput != null) {
                int countReal = RollOutputCount(ref seed, mainOutput.OutputCount);
                if (countReal > 0) {
                    list.Add(new(true, mainOutput.OutputID, countReal));
                    mainOutput.OutputTotalCount += countReal;
                }
            }
            // 附加输出判定，每一项依次判定，互不影响
            foreach (var outputInfo in OutputAppend) {
                if (GetRandDouble(ref seed) <= outputInfo.SuccessRatio) {
                    float countAvg = outputInfo.OutputCount;
                    int countReal = (int)countAvg;
                    countAvg -= countReal;
                    if (countAvg > 0.0001 && GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                    if (countReal > 0) {
                        list.Add(new(false, outputInfo.OutputID, countReal));
                        outputInfo.OutputTotalCount += countReal;
                    }
                }
            }

            if (list.Count > 0) {
                inputChange = -1;
                fluidInputInc -= fluidInputIncAvg;
                outputs = list;
                return;
            }

            // 如果判定成功但产出数为0（例如由于小数判定未通过），由于原料已尝试消耗但无产出，视同损毁
            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            outputs = null;
            return;
        }

        // 3. 无变化 -> 直通输出
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        outputs = ProcessManager.emptyOutputs;
    }

    /// <summary>
    /// 执行单次轻量分馏结算，供运行热路径减少分配使用。
    /// </summary>
    public virtual FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();

        // 1. 损毁判定
        if (GetRandDouble(ref seed) < DestroyRatio) {
            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            return FractionationOutcome.Destroyed;
        }

        // 2. 成功判定
        if (GetRandDouble(ref seed) < SuccessRatio * (1 + pointsBonus) * (1 + successBoost)) {
            OutputInfo mainOutput = SelectMainOutput(ref seed);
            if (mainOutput != null) {
                int countReal = RollOutputCount(ref seed, mainOutput.OutputCount);
                if (countReal > 0) {
                    outputs.Add(true, mainOutput.OutputID, countReal);
                    mainOutput.OutputTotalCount += countReal;
                }
            }

            foreach (var outputInfo in OutputAppend) {
                if (GetRandDouble(ref seed) <= outputInfo.SuccessRatio) {
                    int countReal = RollOutputCount(ref seed, outputInfo.OutputCount);
                    if (countReal > 0) {
                        outputs.Add(false, outputInfo.OutputID, countReal);
                        outputInfo.OutputTotalCount += countReal;
                    }
                }
            }

            if (outputs.Count > 0) {
                inputChange = -1;
                fluidInputInc -= fluidInputIncAvg;
                return FractionationOutcome.Produced;
            }

            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            return FractionationOutcome.Destroyed;
        }

        // 3. 无变化 -> 直通输出
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        return FractionationOutcome.PassThrough;
    }

    /// <summary>
    /// 执行批量轻量分馏结算，供运行热路径合并多次处理。
    /// </summary>
    public virtual FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();

        int destroyedCount = RollBinomialApprox(ref seed, batchCount, DestroyRatio);
        int aliveCount = batchCount - destroyedCount;
        float successRatio = SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        int rolledSuccessCount = RollBinomialApprox(ref seed, aliveCount, successRatio);
        int passThroughCount = aliveCount - rolledSuccessCount;
        int producedSuccessCount = 0;

        OutputInfo directedMainOutput = GetDirectedMainOutput();
        if (directedMainOutput != null) {
            producedSuccessCount = AddRolledOutput(ref seed, outputs, directedMainOutput, true, rolledSuccessCount);
        } else {
            int remainingMainCount = rolledSuccessCount;
            float remainingMainRatio = 1.0f;
            for (int i = 0; i < OutputMain.Count && remainingMainCount > 0; i++) {
                OutputInfo outputInfo = OutputMain[i];
                int outputHits = i == OutputMain.Count - 1
                    ? remainingMainCount
                    : RollBinomialApprox(ref seed, remainingMainCount, outputInfo.SuccessRatio / remainingMainRatio);
                remainingMainCount -= outputHits;
                remainingMainRatio -= outputInfo.SuccessRatio;
                if (remainingMainRatio <= 0f) {
                    remainingMainRatio = 1.0f;
                }
                producedSuccessCount += AddRolledOutput(ref seed, outputs, outputInfo, true, outputHits);
            }
        }

        foreach (var outputInfo in OutputAppend) {
            int outputHits = RollBinomialApprox(ref seed, rolledSuccessCount, outputInfo.SuccessRatio);
            int producedHits = AddRolledOutput(ref seed, outputs, outputInfo, false, outputHits);
            int missingSuccessCount = rolledSuccessCount - producedSuccessCount;
            if (missingSuccessCount > 0 && producedHits > 0) {
                int rescuedCount = RollBinomialApprox(ref seed, missingSuccessCount,
                    (float)producedHits / rolledSuccessCount);
                producedSuccessCount += rescuedCount;
            }
        }

        int noOutputCount = rolledSuccessCount - producedSuccessCount;
        destroyedCount += noOutputCount;
        int inputRemoveCount = destroyedCount + passThroughCount + producedSuccessCount;
        fluidInputInc -= fluidInputIncAvg * inputRemoveCount;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        FractionationBatchResult result = new() {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = destroyedCount + producedSuccessCount,
            SuccessCount = producedSuccessCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = passThroughCount,
            PassThroughInc = fluidInputIncAvg * passThroughCount,
        };
        return result;
    }

    /// <summary>
    /// 按输出概率和数量随机结算一条产物并加入缓存，返回实际产生该产物的成功次数。
    /// </summary>
    protected static int AddRolledOutput(ref uint seed, ProductOutputBuffer outputs, OutputInfo outputInfo,
        bool isMainOutput, int outputHits) {
        if (outputHits <= 0) {
            return 0;
        }

        int baseCount = (int)outputInfo.OutputCount;
        float fractionalCount = outputInfo.OutputCount - baseCount;
        int totalCount = outputHits * baseCount + RollBinomialApprox(ref seed, outputHits, fractionalCount);
        if (totalCount <= 0) {
            return 0;
        }

        outputs.Add(isMainOutput, outputInfo.OutputID, totalCount);
        outputInfo.OutputTotalCount += totalCount;
        return baseCount > 0 ? outputHits : totalCount;
    }

    private OutputInfo GetDirectedMainOutput() => CurrentMainOutputTargetId == 0
        ? null
        : OutputMain.Find(output => output.OutputID == CurrentMainOutputTargetId);

    private OutputInfo SelectMainOutput(ref uint seed) {
        OutputInfo directedOutput = GetDirectedMainOutput();
        if (directedOutput != null) {
            return directedOutput;
        }

        double ratio = GetRandDouble(ref seed);
        float cumulativeRatio = 0f;
        foreach (OutputInfo outputInfo in OutputMain) {
            cumulativeRatio += outputInfo.SuccessRatio;
            if (ratio <= cumulativeRatio) {
                return outputInfo;
            }
        }
        return null;
    }

    /// <summary>
    /// 按小数产量随机取整得到实际输出数量。
    /// </summary>
    protected static int RollOutputCount(ref uint seed, float outputCount) {
        int countReal = (int)outputCount;
        float fractionalCount = outputCount - countReal;
        if (fractionalCount > 0.0001 && GetRandDouble(ref seed) < fractionalCount) {
            countReal++;
        }

        return countReal;
    }

    /// <summary>
    /// 用分段算法近似结算二项分布成功次数。
    /// </summary>
    public static int RollBinomialApprox(ref uint seed, int trials, float probability) {
        if (trials <= 0 || probability <= 0f) {
            return 0;
        }
        if (probability >= 1f) {
            return trials;
        }
        if (trials <= 8) {
            int result = 0;
            for (int i = 0; i < trials; i++) {
                if (GetRandDouble(ref seed) < probability) {
                    result++;
                }
            }
            return result;
        }

        double mean = trials * probability;
        double variance = mean * (1.0 - probability);
        if (variance <= 0.000001) {
            return (int)(mean + GetRandDouble(ref seed));
        }

        // 3 次均匀随机近似正态分布，比逐次判定少得多，同时保留批量波动。
        double normalized = (GetRandDouble(ref seed) + GetRandDouble(ref seed) + GetRandDouble(ref seed) - 1.5) * 2.0;
        int sampled = (int)Math.Round(mean + Math.Sqrt(variance) * normalized);
        if (sampled < 0) {
            return 0;
        }
        return sampled > trials ? trials : sampled;
    }

    #endregion

    /// <summary>
    /// 获取产物的增产点数
    /// </summary>
    public virtual byte GetOutputInc(int itemId) => 0;

    #region IModCanSave

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public virtual void Import(BinaryReader r) {
        r.ReadBlocks(
            ("TotalSuccessCount", br => TotalSuccessCount = Math.Max(0, br.ReadInt64())),
            ("OutputMain", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int id = br.ReadInt32();
                    int total = br.ReadInt32();
                    var info = OutputMain.Find(x => x.OutputID == id);
                    if (info != null) info.OutputTotalCount = total;
                    else LogWarning($"Output {id} not found in {TypeName} main outputs");
                }
            }),
            ("OutputAppend", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int id = br.ReadInt32();
                    int total = br.ReadInt32();
                    var info = OutputAppend.Find(x => x.OutputID == id);
                    if (info != null) info.OutputTotalCount = total;
                    else LogWarning($"Output {id} not found in {TypeName} append outputs");
                }
            })
        );
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public virtual void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalSuccessCount", bw => bw.Write(TotalSuccessCount)),
            ("OutputMain", bw => {
                bw.Write(OutputMain.Count);
                foreach (var info in OutputMain) {
                    bw.Write(info.OutputID);
                    bw.Write(info.OutputTotalCount);
                }
            }),
            ("OutputAppend", bw => {
                bw.Write(OutputAppend.Count);
                foreach (var info in OutputAppend) {
                    bw.Write(info.OutputID);
                    bw.Write(info.OutputTotalCount);
                }
            })
        );
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public virtual void IntoOtherSave() {
        TotalSuccessCount = 0;
        foreach (OutputInfo info in OutputMain) {
            info.OutputTotalCount = 0;
        }
        foreach (OutputInfo info in OutputAppend) {
            info.OutputTotalCount = 0;
        }
    }

    #endregion
}
