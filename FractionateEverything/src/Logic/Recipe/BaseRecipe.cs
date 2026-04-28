using System;
using System.Collections.Generic;
using System.IO;
using FE.Compatibility;
using FE.Logic.Manager;
using FE.Logic.RecipeGrowth;
using NebulaAPI;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

public enum ERecipeGrowthRole {
    Production = 0,
    ToolUnlock = 1,
    SpecialGrowth = 2,
}

/// <summary>
/// 分馏配方基类
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float baseSuccessRatio,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    public string TypeName => $"{RecipeType.GetShortName()}-{LDB.items.Select(InputID).name}"
                              + (RecipeGrowthQueries.IsUnlocked(this)
                                  ? $" Lv{RecipeGrowthQueries.GetLevel(this)}"
                                  : "");
    public string TypeNameWC => TypeName.WithColor(MatrixID - I电磁矩阵);

    #region 配方类型、输入输出

    /// <summary>
    /// 类型
    /// </summary>
    public abstract ERecipe RecipeType { get; }

    /// <summary>
    /// 配方成长语义。用于区分生产型、工具/解锁型、特殊成长型。
    /// </summary>
    public virtual ERecipeGrowthRole GrowthRole => ERecipeGrowthRole.Production;

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
    public float DestroyRatio {
        get {
            float raw = 0.04f;
            float reduce = GachaGalleryBonusManager.GetDestroyReduction(RecipeType);
            float result = raw - reduce;
            return result > 0f ? result : 0f;
        }
    }

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
    /// 原料不消耗概率
    /// </summary>
    public float RemainInputRatio => RecipeGrowthQueries.GetSnapshot(this).RemainInputRatio;

    /// <summary>
    /// 产物翻倍概率
    /// </summary>
    public float DoubleOutputRatio => RecipeGrowthQueries.GetSnapshot(this).DoubleOutputRatio;

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
            // 主输出判定，由于主输出概率之和为100%，所以必定输出且只会输出其中一个
            double ratio = GetRandDouble(ref seed);
            float ratioMain = 0.0f;// 用于累计概率
            foreach (var outputInfo in OutputMain) {
                ratioMain += outputInfo.SuccessRatio;
                if (ratio <= ratioMain) {
                    // 整数部分必定输出，小数部分根据概率判定确定是否输出
                    float countAvg = outputInfo.OutputCount;
                    int countReal = (int)countAvg;
                    countAvg -= countReal;
                    if (countAvg > 0.0001 && GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }

                    // 产物翻倍判定
                    if (GetRandDouble(ref seed) < DoubleOutputRatio) {
                        countReal *= 2;
                    }

                    if (countReal > 0) {
                        list.Add(new(true, outputInfo.OutputID, countReal));
                        outputInfo.OutputTotalCount += countReal;
                    }
                    break;
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
                // 原料不消耗判定
                inputChange = (GetRandDouble(ref seed) < RemainInputRatio) ? 0 : -1;
                if (inputChange < 0) {
                    fluidInputInc -= fluidInputIncAvg;
                }
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

    #endregion

    /// <summary>
    /// 获取产物的增产点数
    /// </summary>
    public virtual byte GetOutputInc(int itemId) => 0;

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        r.ReadBlocks(
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
            }),
            ("Meta", br => { RecipeGrowthManager.ImportLegacyState(this, br.ReadInt32()); })
        );
    }

    public virtual void Export(BinaryWriter w) {
        w.WriteBlocks(
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

    public virtual void IntoOtherSave() {
        foreach (OutputInfo info in OutputMain) {
            info.OutputTotalCount = 0;
        }
        foreach (OutputInfo info in OutputAppend) {
            info.OutputTotalCount = 0;
        }
    }

    #endregion
}
