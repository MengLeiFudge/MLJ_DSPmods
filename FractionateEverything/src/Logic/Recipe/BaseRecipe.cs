using System;
using System.Collections.Generic;
using System.IO;
using FE.Compatibility;
using FE.Logic.Manager;
using NebulaAPI;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方基类
/// </summary>
public abstract class BaseRecipe(
    int inputID,
    float baseSuccessRatio,
    List<OutputInfo> outputMain,
    List<OutputInfo> outputAppend) {
    public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name} +{Level}";
    public string TypeNameWC => TypeName.WithColor(MatrixID - I电磁矩阵);

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
    /// 配方层次对应的矩阵ID
    /// </summary>
    public int MatrixID = 0;

    public bool FullUpgrade => Level >= 10;
    /// <summary>
    /// 配方成功率
    /// </summary>
    public float SuccessRatio => baseSuccessRatio;
    /// <summary>
    /// 配方损毁率，强制5%以使增产剂对分馏效果有明显提升
    /// </summary>
    public float DestroyRatio => Level switch {
        < 8 => 0.05f,
        < 10 => 0.03f,
        _ => 0f,
    };

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
    public float RemainInputRatio => Level * 0.08f;

    /// <summary>
    /// 产物翻倍概率
    /// </summary>
    public float DoubleOutputRatio => Level * 0.05f;

    /// <summary>
    /// 获取某次输出的执行结果。
    /// 可能的情况有：损毁、无变化、产出主输出（在此基础上可能产出附加输出）
    /// </summary>
    /// <param name="seed">随机数种子</param>
    /// <param name="pointsBonus">增产剂加成</param>
    /// <param name="successRatioBonus">配方成功率加成</param>
    /// <param name="mainOutputCountBonus">主产物数目加成</param>
    /// <param name="appendOutputRatioBonus">副产物概率加成</param>
    /// <param name="inputChange">原材料会变成几个</param>
    /// <param name="outputs">损毁返回null，无变化反馈空List，成功返回输出产物(是否为主输出，物品ID，物品数目)</param>
    public virtual void GetOutputs(ref uint seed, float pointsBonus,
        float successRatioBonus, float mainOutputCountBonus, float appendOutputRatioBonus,
        out int inputChange, out List<ProductOutputInfo> outputs) {
        //损毁
        if (GetRandDouble(ref seed) < DestroyRatio) {
            inputChange = -1;
            outputs = ProcessManager.emptyOutputs;
            return;
        }
        //无变化
        if (GetRandDouble(ref seed) >= SuccessRatio * (1 + pointsBonus) * (1 + successRatioBonus)) {
            inputChange = 0;
            outputs = ProcessManager.emptyOutputs;
            return;
        }
        //成功产出
        List<ProductOutputInfo> list = [];
        //主输出判定，由于主输出概率之和为100%，所以必定输出且只会输出其中一个
        double ratio = GetRandDouble(ref seed);
        float ratioMain = 0.0f;//用于累计概率
        foreach (var outputInfo in OutputMain) {
            ratioMain += outputInfo.SuccessRatio;
            if (ratio <= ratioMain) {
                //整数部分必定输出，小数部分根据概率判定确定是否输出
                float countAvg = outputInfo.OutputCount * (1 + mainOutputCountBonus);
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0.0001) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(true, outputInfo.OutputID, countReal));
                outputInfo.OutputTotalCount += countReal;
                break;
            }
        }
        //附加输出判定，每一项依次判定，互不影响
        foreach (var outputInfo in OutputAppend) {
            if (GetRandDouble(ref seed) <= outputInfo.SuccessRatio * (1 + appendOutputRatioBonus)) {
                float countAvg = outputInfo.OutputCount;
                int countReal = (int)countAvg;
                countAvg -= countReal;
                if (countAvg > 0.0001) {
                    if (GetRandDouble(ref seed) < countAvg) {
                        countReal++;
                    }
                }
                list.Add(new(false, outputInfo.OutputID, countReal));
                outputInfo.OutputTotalCount += countReal;
                //附加输出无经验
            }
        }
        //如果仍然没有产出（例如产物数目<1且小数判定未通过），由于原料已消耗，应该返回损毁而非空列表
        if (list.Count == 0) {
            inputChange = -1;
            outputs = ProcessManager.emptyOutputs;
            return;
        }
        inputChange = -1;
        outputs = list;
    }

    #endregion

    #region 配方品质、等级

    /// <summary>
    /// 配方等级，也代表重复抽取到改配方的次数
    /// </summary>
    public int Level { get; set; } = -1;
    /// <summary>
    /// 是否未解锁
    /// </summary>
    public bool Locked => Level < 0;
    /// <summary>
    /// 是否已解锁
    /// </summary>
    public bool Unlocked => Level >= 0;
    /// <summary>
    /// 等级是否已达上限
    /// </summary>
    public bool IsMaxLevel => Level >= 10;

    /// <summary>
    /// 通过抽奖获取到该配方。
    /// 如果配方未解锁，则解锁此配方；如果已解锁，则等级+1，并检查是否可突破。
    /// </summary>
    public void RewardThis(bool manual = false) {
        lock (this) {
            if (Locked) {
                Level = 0;
            } else if (!IsMaxLevel) {
                Level++;
            } else {
                return;
            }
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RecipeChangePacket(RecipeType, inputID, 1));
        }
    }

    /// <summary>
    /// 沙盒模式修改等级
    /// </summary>
    public void ChangeLevelTo(int targetLevel, bool manual = false) {
        lock (this) {
            Level = Math.Max(-1, Math.Min(10, targetLevel));
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RecipeChangePacket(RecipeType, inputID, 2,
                targetLevel));
        }
    }

    #endregion

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
        int outputMainCount = r.ReadInt32();
        for (int i = 0; i < outputMainCount; i++) {
            int outputID = r.ReadInt32();
            int outputTotalCount = r.ReadInt32();
            var outputInfo = OutputMain.Find(info => info.OutputID == outputID);
            if (outputInfo != null) {
                outputInfo.OutputTotalCount = outputTotalCount;
            } else {
                LogWarning($"Output {outputID} not found in {TypeName} main outputs");
            }
        }
        int outputAppendCount = r.ReadInt32();
        for (int i = 0; i < outputAppendCount; i++) {
            int outputID = r.ReadInt32();
            int outputTotalCount = r.ReadInt32();
            var outputInfo = OutputAppend.Find(info => info.OutputID == outputID);
            if (outputInfo != null) {
                outputInfo.OutputTotalCount = outputTotalCount;
            } else {
                LogWarning($"Output {outputID} not found in {TypeName} append outputs");
            }
        }
        if (version < 2) {
            r.ReadInt32();
        }
        Level = Math.Max(-1, Math.Min(10, r.ReadInt32()));
        if (version < 2) {
            r.ReadSingle();
        }
        // 子类特定数据由重写的方法处理
    }

    public virtual void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(OutputMain.Count);
        foreach (OutputInfo info in OutputMain) {
            w.Write(info.OutputID);
            w.Write(info.OutputTotalCount);
        }
        w.Write(OutputAppend.Count);
        foreach (OutputInfo info in OutputAppend) {
            w.Write(info.OutputID);
            w.Write(info.OutputTotalCount);
        }
        w.Write(Level);
        // 子类特定数据由重写的方法处理
    }

    public virtual void IntoOtherSave() {
        foreach (OutputInfo info in OutputMain) {
            info.OutputTotalCount = 0;
        }
        foreach (OutputInfo info in OutputAppend) {
            info.OutputTotalCount = 0;
        }
        Level = -1;
    }

    #endregion
}
